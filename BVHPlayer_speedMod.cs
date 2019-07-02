using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using SimpleJSON;

namespace ElkVR {

/*
    BVH Player,

        Biovision Hierarchical Data file "importer". This plugin will load a bvh file and allow you to play it on a character within VaM.
        It also has a "bake" function that saves the animation data into the scene.

    Version: 0.9.2
    Author: ElkVR
    License: Creative Commons Attribution - https://creativecommons.org/licenses/by/3.0/
    Changelog:
     * 0.9.0 Initial release
     * 0.9.1 Bugfixes
      - Male support
      - Rotation order support
      - More translation modes (exposed in UI)
      - pause simulation when scrubbing/looping
      - don't play animation after baking
     * 1.0.0 Smooth
      - Additional bones
      - Interpolate animation
      - Ping pong
      - Remember previous folder
     * 1.0.0_speedMod - Sync playback speed with global animation speed mod - hsthrowaway5
 */

public class BVHPlayer : MVRScript {

    Dictionary<string, FreeControllerV3> controllerMap;
    Dictionary<string, MotionAnimationControl> macMap;

    Dictionary<string, string> cnameToBname = new Dictionary<string, string>() {
        { "hipControl", "hip" },
        { "headControl", "head" },
        { "chestControl", "chest" },
        { "lHandControl", "lHand" },
        { "rHandControl", "rHand" },
        { "lFootControl", "lFoot" },
        { "rFootControl", "rFoot" },
        { "lKneeControl", "lShin" },
        { "rKneeControl", "rShin" },
        { "neckControl", "neck" },
        { "lElbowControl", "lForeArm" },
        { "rElbowControl", "rForeArm" },
        { "lArmControl", "lShldr" },
        { "rArmControl", "rShldr" },
        // Additional bones
        { "lShoulderControl", "lCollar" },
        { "rShoulderControl", "rCollar" },
        { "abdomenControl", "abdomen" },
        { "abdomen2Control", "abdomen2" },
        { "pelvisControl", "pelvis" },
        { "lThighControl", "lThigh" },
        { "rThighControl", "rThigh" },
        // { "lToeControl", "lToe" },
        // { "rToeControl", "rToe" },
    };

    JSONStorableString uiStatus;
    JSONStorableFloat uiAnimationPos, uiAnimationSpeed;
    Transform shadow = null;

    BvhFile bvh = null;
    float elapsed = 0;
    int frame = 0;
    bool playing = false;
    bool baking = false;
    bool reverse = false;
    bool isUpdating = false;
    bool loopPlay = false;
    bool loopBake = false;
    bool pingpongPlay = false;
    bool pingpongBake = false;
    bool onlyHipTranslation = true;
    bool translationIsDelta = false;
    float frameTime;

    // Apparently we shouldn't use enums because it causes a compiler crash
    const int translationModeOffsetPlusFrame = 0;
    const int translationModeFrameOnly = 1;
    const int translationModeInitialPlusFrameMinusOffset = 2;
    const int translationModeInitialPlusFrameMinusZero = 3;

    int translationMode = translationModeInitialPlusFrameMinusZero;


    void UpdateUI() {
        isUpdating = true;
        uiAnimationPos.SetVal(frame * 100f / bvh.nFrames);
        isUpdating = false;
    }

    void UpdateSpeed(float speed) {
        float oldFrameTime = frameTime;
        frameTime = bvh.frameTime / (speed * 0.01f);

        // If speed crosses zero then reverse playback direction
        if( ( oldFrameTime >= 0 ) != ( frameTime >= 0 ) ) {
            reverse = !reverse;
        }
    }

    void UpdateStatus() {
        if(bvh == null)
            uiStatus.SetVal("Select BVH File");
        else if(baking)
            uiStatus.SetVal("Recording now...");
        else
            uiStatus.SetVal(string.Format("Loaded {0}. {1} bones. {2} frames. {3} FPS. Duration {4:0.#}s. {5}.", bvh.path, bvh.bones.Length, bvh.nFrames,
                (int)Mathf.Round(1f / bvh.frameTime), bvh.nFrames * bvh.frameTime, bvh.isTranslationLocal ? "Local translation" : "Absolute Translation"));
    }

    string prevFolder = "";

    void UpdatePrevFolder(string path) {
        var parts = path.Split('\\');
        path = path.Substring(0, path.Length - parts[parts.Length - 1].Length);
        prevFolder = path;
        // SuperController.LogMessage("Prev Folder: " + prevFolder);
    }

    public void Load(string path) {
        bvh = new BvhFile(path);
        uiAnimationPos.SetVal(0);
        UpdateStatus();
        UpdateSpeed(uiAnimationSpeed.val);
        UpdatePrevFolder(path);
        // Not using automatic detection anymore, not reliable
        // if(! bvh.isTranslationLocal && tposeBoneOffsets == null) {
        //     // Ensure we have initial position offsets
        //     RecordOffsets();
        //     CreateControllerMap();  // Reset the controllers
        // }
    }

    bool discontinuity = false;

    public override void Init() {
        try {
    #region UI
            CreateButton("Select BVH File").button.onClick.AddListener(() => {
                if(prevFolder == "")
                    prevFolder = SuperController.singleton.savesDir + "\\Animation";
                SuperController.singleton.GetMediaPathDialog((string path) => {
                    Load(path);
                }, "bvh", prevFolder, false);
            });

            uiStatus = new JSONStorableString("Status:", "");
            CreateTextField(uiStatus, true);
            uiStatus.SetVal("Select a BVH file");

            var showSkeleton = new JSONStorableBool("Show Skeleton", false, (bool val) => {
                if(val)
                    ShowSkeleton();
                else
                    HideSkeleton();
            });
            RegisterBool(showSkeleton);
            CreateToggle(showSkeleton);
            // var uiOnlyHipTrans = new JSONStorableBool("Only hip translation", true, (bool val) => {
            //     onlyHipTranslation = val;
            // });
            // onlyHipTranslation = uiOnlyHipTrans.val;
            // RegisterBool(uiOnlyHipTrans);
            // CreateToggle(uiOnlyHipTrans);

            var modes = new List<string>();
            modes.Add("Offset + Frame (DAZ)");
            modes.Add("Frame only");
            modes.Add("Initial + Frame - Offset (MB)");
            modes.Add("Initial + Frame - Frame[0] (CMU)");

            var uiTranslationMode = new JSONStorableStringChooser("transmode", modes, modes[translationMode], "Translation Mode", (string val) => {
                translationMode = modes.FindIndex((string mode) => { return mode == val; });
                if((translationMode == translationModeInitialPlusFrameMinusOffset || translationMode == translationModeInitialPlusFrameMinusZero) && tposeBoneOffsets == null) {
                    // We need t-pose measurements, and don't have them yet
                    RecordOffsets();
                    CreateControllerMap();
                }
            });
            CreatePopup(uiTranslationMode);

            // var uiTransDelta = new JSONStorableBool("Translation is local", true, (bool val) => {
            //     translationIsDelta = val;
            //     if(! translationIsDelta && tposeBoneOffsets == null) {
            //         RecordOffsets();
            //         CreateControllerMap();
            //     }
            // });
            // translationIsDelta = uiTransDelta.val;
            // RegisterBool(uiTransDelta);
            // CreateToggle(uiTransDelta);

            var space = CreateSpacer(true);
            space.height = 20f;
            space = CreateSpacer();
            space.height = 10f;

            CreateButton("<<").button.onClick.AddListener(() => {
                uiAnimationPos.SetVal(0);
            });
            CreateButton(">>", true).button.onClick.AddListener(() => {
                uiAnimationPos.SetVal(100);
            });

            var playModes = new List<string>();
            playModes.Add("Once");
            playModes.Add("Loop");
            playModes.Add("PingPong Once");
            playModes.Add("PingPong Loop");

            var uiPlayMode = new JSONStorableStringChooser("playmode", playModes, playModes[0], "Play Mode", (string val) => {
                var i = playModes.IndexOf(val);
                loopPlay = (i & 1) == 1;
                pingpongPlay = i >= 2;
            });
            var uiBakeMode = new JSONStorableStringChooser("bakemode", playModes, playModes[0], "Bake Mode", (string val) => {
                var i = playModes.IndexOf(val);
                loopBake = (i & 1) == 1;
                pingpongBake = i >= 2;
            });

            uiAnimationPos = new JSONStorableFloat("Animation (%)", 0, (float value) => {
                if(bvh != null && !isUpdating) {
                    frame = (int)(bvh.nFrames * value * 0.01);
                    if(frame >= bvh.nFrames)
                        frame = bvh.nFrames - 1;
                    discontinuity = true;
                }
            }, 0, 100);
            CreateSlider(uiAnimationPos);
            uiAnimationSpeed = new JSONStorableFloat("Speed (%)", 100, UpdateSpeed, 1, 300);
            CreateSlider(uiAnimationSpeed, true);

            CreatePopup(uiPlayMode);
            CreateButton("Play").button.onClick.AddListener(() => {
                playing = true;
                reverse = false;
            });
            CreatePopup(uiBakeMode, true);
            CreateButton("Bake", true).button.onClick.AddListener(() => {
                Bake();
            });
            CreateButton("Stop", true).button.onClick.AddListener(() => {
                playing = false;
                if(baking)
                    BakeStop();
            });
    #endregion

            // saveCollisionEnabled = containingAtom.collisionEnabledJSON.val;
            // containingAtom.collisionEnabledJSON.val = false;

            CreateShadowSkeleton();
            if(showSkeleton.val)
                ShowSkeleton();
            RecordOffsets();
            CreateControllerMap();
        }
        catch(Exception e) {
            SuperController.LogError("Init: " + e);
        }
    }

    void CreateControllerMap() {
        controllerMap = new Dictionary<string, FreeControllerV3>();
        foreach(FreeControllerV3 controller in containingAtom.freeControllers)
            controllerMap[controller.name] = controller;

        foreach(var item in cnameToBname) {
            var c = controllerMap[item.Key];
            c.currentRotationState = FreeControllerV3.RotationState.On;
            c.currentPositionState = FreeControllerV3.PositionState.On;
        }

        macMap = new Dictionary<string, MotionAnimationControl>();
        foreach(var mac in containingAtom.motionAnimationControls) {
            macMap[mac.name] = mac;
        }
    }

    Transform CreateMarker(Transform parent) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        go.parent = parent;
        go.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        go.localPosition = Vector3.zero;
        go.localRotation = Quaternion.identity;
        Destroy(go.GetComponent<BoxCollider>());
        return go;
    }

    List<Transform> markers = null;

    void ShowSkeleton() {
        if(markers != null)
            HideSkeleton();
        markers = new List<Transform>();
        foreach(var bone in bones)
            markers.Add(CreateMarker(bone.Value));
    }

    void HideSkeleton() {
        foreach(var marker in markers)
            Destroy(marker.gameObject);
        markers = null;
    }

    Dictionary<string, Transform> bones;
    Dictionary<string, Vector3> tposeBoneOffsets = null;

    void RecordOffsets() {
        containingAtom.ResetPhysical();
        CreateShadowSkeleton();     // re-create
        tposeBoneOffsets = new Dictionary<string, Vector3>();
        foreach(var item in bones)
            tposeBoneOffsets[item.Key] = item.Value.localPosition;
    }

    public void CreateShadow(Transform skeleton, Transform shadow) {
        bones[shadow.gameObject.name] = shadow;
        shadow.localPosition = skeleton.localPosition;
        shadow.localRotation = skeleton.localRotation;
        // SuperController.LogMessage(string.Format("{0} {1} {2},{3},{4}", skeleton.gameObject.name, shadow.gameObject.name, shadow.position.x, shadow.position.y, shadow.position.z));
        // CreateMarker(shadow);
        for(var i = 0; i < skeleton.childCount; i++) {
            var child = skeleton.GetChild(i);
            if(child.gameObject.GetComponent<DAZBone>() != null) {
                var n = new GameObject(child.gameObject.name).transform;
                n.parent = shadow;
                CreateShadow(child, n);
            }
        }
    }

    void CreateShadowSkeleton() {
        foreach(var parent in containingAtom.gameObject.GetComponentsInChildren<DAZBones>()) {
            // SuperController.LogMessage(string.Format("{0}", parent.gameObject.name));
            if(parent.gameObject.name == "Genesis2Female" || parent.gameObject.name == "Genesis2Male") {
                // SuperController.LogMessage(parent.gameObject.name);
                if(shadow != null)
                    GameObject.Destroy(shadow.gameObject);
                bones = new Dictionary<string, Transform>();
                shadow = new GameObject("Shadow").transform;
                shadow.position = parent.transform.position;
                CreateShadow(parent.gameObject.transform, shadow);
            }
        }
    }

    BvhTransform[] prevFrame, nextFrame;

    BvhTransform[] Interpolate(BvhTransform[] a, BvhTransform[] b, float t) {
        var ret = new BvhTransform[a.Length];
        for(var i = 0; i < a.Length; i++) {
            var at = a[i];
            var bt = b[i];
            
            var res = new BvhTransform();
            res.bone = at.bone;
            res.position = Vector3.Lerp(at.position, bt.position, t);
            res.rotation = Quaternion.Lerp(at.rotation, bt.rotation, t);
            ret[i] = res;
        }
        return ret;
    }

    void UpdateModel(BvhTransform[] data) {
		foreach(var item in data) {
			// Copy on to model
			if(bones.ContainsKey(item.bone.name)) {
				bones[item.bone.name].localRotation = item.rotation;
				if(item.bone.hasPosition) {
                    if(item.bone.isHipBone || ! onlyHipTranslation) {
                        Vector3 pos;
                        if(translationMode == translationModeFrameOnly)
                            pos = item.position;
                        else if(translationMode == translationModeOffsetPlusFrame)
                            pos = item.position + item.bone.offset;
                        else if(translationMode == translationModeInitialPlusFrameMinusOffset)
                            pos = tposeBoneOffsets[item.bone.name] + item.position - item.bone.offset;
                        else //if(translationMode == translationModeInitialPlusFrameMinusZero)
                            pos = tposeBoneOffsets[item.bone.name] + item.position - item.bone.posZero;
                        bones[item.bone.name].localPosition = pos;
                    }
                }
			}
		}
    }

    void FixedUpdate() {
        try {
            if(bvh == null || bvh.nFrames == 0)
                return;

            // Sync playback speed with global speed
            float playSpeed = SuperController.singleton.motionAnimationMaster.playbackSpeed;
            bool gamePaused = SuperController.singleton.freezeAnimation;
            if( gamePaused )
                {
                return;
                }
            UpdateSpeed( 100 * playSpeed );

            if(playing) {
                elapsed += Time.deltaTime;
                if(elapsed >= Math.Abs(frameTime)) {
                    elapsed = 0;
                    if(reverse)
                        frame--;
                    else
                        frame++;
                    UpdateUI();
                }
            }

            if(frame < 0) {
                reverse = false;
                frame = 0;
                if(baking) {
                    if(! loopBake)
                        BakeStop();
                }
                else {
                    if(! loopPlay)
                        playing = false;
                }
            }

            if(frame >= bvh.nFrames) {
                if(baking) {
                    if(pingpongBake) {
                        reverse = true;
                        frame--;
                    }
                    else if(loopBake)
                        frame = 0;
                    else {
                        BakeStop();
                        return;
                    }
                }
                else {
                    if(pingpongPlay) {
                        reverse = true;
                        frame--;
                    }
                    else if(loopPlay)
                        frame = 0;
                    else {
                        playing = false;
                        return;
                    }
                }
            }

            if(reverse) {
                if(frame == 0)
                    UpdateModel(bvh.ReadFrame(frame));
                else {
                    // Interpolate in reverse
                    var frm = bvh.ReadFrame(frame);
                    var to = bvh.ReadFrame(frame - 1);
                    float t = elapsed / Math.Abs( frameTime );
                    UpdateModel(Interpolate(frm, to, t));
                }
            }
            else {
                if(frame >= bvh.nFrames - 1) {
                    // Last frame
                    UpdateModel(bvh.ReadFrame(frame));
                }
                else {
                    // Interpolate
                    var frm = bvh.ReadFrame(frame);
                    var to = bvh.ReadFrame(frame + 1);
                    float t = elapsed / Math.Abs( frameTime );
                    UpdateModel(Interpolate(frm, to, t));
                }
            }

            if(discontinuity)
                BeginRestore();

            Vector3 pivotPos = containingAtom.transform.position;
            foreach(var item in cnameToBname) {
                controllerMap[item.Key].transform.localPosition = bones[item.Value].position - pivotPos;
                controllerMap[item.Key].transform.localRotation = bones[item.Value].rotation;
            }

            if(discontinuity)
                EndRestore();
        }
        catch(Exception e) {
            SuperController.LogError("Fixed Update: " + e);
        }
    }

    // JSONClass jSONClasses;

    // Restore / Teleport is difficult to achieve because while we know where the control points should be, we don't know where all the rigid
    // bodies should be. We would need to turn off the atom, move all rigid bodies into place, then turn the atom back on. This feels a little
    // fragile, unless we went for an approximate solution, and rely on parenting by moving the parent(s) of the rigid bodies *close* to where
    // they should be, then the physics upset would be minimal. Pushed to a later version...
    // bool saveCollisionEnabled;
    void BeginRestore() {
        // var a = containingAtom;
        // a.SetOn(false);
        // jSONClasses = new JSONClass();
        // a.PreRestore();
        // a.RestoreTransform(jSONClasses);
        // a.RestoreParentAtom(jSONClasses);
        // containingAtom.ResetPhysical();
        // discontinuity = false;
    }

    void EndRestore() {
        var a = containingAtom;
        // a.Restore(jSONClasses, true, false, false, null, true);
        // a.Restore(jSONClasses, true, false, false, null, false);
        // a.LateRestore(jSONClasses, true, false, false);
        // a.PostRestore();
        // a.SetOn(true);
        // a.collisionEnabledJSON.val = saveCollisionEnabled;
        SuperController.singleton.PauseSimulation(5, string.Concat("Restore Animation ", a.uid));
        discontinuity = false;
    }

	void OnDestroy() {
        if(shadow != null)
            Destroy(shadow.gameObject);
        // containingAtom.collisionEnabledJSON.val = saveCollisionEnabled;
	}

    public void Bake() {
        var mam = SuperController.singleton.motionAnimationMaster;
        foreach(var item in controllerMap) {
            var mac = macMap[item.Value.name];
            mac.armedForRecord = true;
        }
        baking = true;
        playing = true;
        reverse = false;
        mam.autoRecordStop = false;
        mam.showRecordPaths = true;
        mam.showStartMarkers = true;
        mam.StartRecord();
        UpdateStatus();
    }

    public void BakeStop() {
        var mam = SuperController.singleton.motionAnimationMaster;
        float pos = mam.playbackCounter;
        mam.StopRecord();
        // StopRecord already disarms the animation controllers
        // foreach(var item in controllerMap) {
        //     var mac = macMap[item.Value.name];
        //     mac.armedForRecord = false;
        // }
        baking = false;
        playing = false;
        mam.showRecordPaths = false;
        mam.showStartMarkers = false;
        mam.StopPlayback();
        mam.playbackCounter = pos;  // StopRecord winds back to where recording started; undo this
        UpdateStatus();
    }
}

public class BvhTransform {
	public BvhBone bone;
	public Vector3 position;
	public Quaternion rotation;
}

// enums are not allowed in scripts (they crash VaM)
public class RotationOrder {
    public const int XYZ = 0, XZY = 1;
    public const int YXZ = 2, YZX = 3;
    public const int ZXY = 4, ZYX = 5;
}

public class BvhBone {
	public string name;
	public BvhBone parent;
	public bool hasPosition, hasRotation;
	public int frameOffset;
	public Vector3 offset, posZero = Vector3.zero;
    public bool isHipBone = false;
    public int rotationOrder = RotationOrder.ZXY;

	public string ToDebugString() {
		return string.Format("{0} {1} {2} fo:{3} par:{4}", name, hasPosition ? "position" : "", hasRotation ? "rotation": "", frameOffset, parent != null ? parent.name : "(null)");
	}
}

public class BvhFile {
    string[] raw;
	int posMotion;
	public BvhBone[] bones;
	float[][] frames;
	public int nFrames;
    public float frameTime;
    public string path;
    public bool isTranslationLocal;

	public BvhFile(string _path) {
        path = _path;
		Load(path);
	}

	public void Load(string path) {
        char[] delims = { '\r', '\n' };
        var raw = SuperController.singleton.ReadFileIntoString(path).Split(delims, System.StringSplitOptions.RemoveEmptyEntries);
		bones = ReadHierarchy(raw);
		frames = ReadMotion(raw);
        frameTime = ReadFrameTime(raw);
		nFrames = frames.Length;
        isTranslationLocal = IsEstimatedLocalTranslation();
        ReadZeroPos();
	}

    void ReadZeroPos() {
        if(nFrames > 0) {
            foreach(var tf in ReadFrame(0)) {
                if(tf.bone.hasPosition)
                    tf.bone.posZero = tf.position;
            }
        }
    }

    bool IsEstimatedLocalTranslation() {
        BvhBone hip = null;
        foreach(var bone in bones)
            if(bone.isHipBone)
                hip = bone;
        if(hip == null)
            return true;    // best estimate without a hip bone
        var index = hip.frameOffset + 1;
        // Use hip 'y' to estimate the translation mode (local or "absolute")
        float sum = 0;
        for(var i = 0; i < nFrames; i++) {
    		var data = frames[i];
            sum += data[index];
        }
        float average = sum / nFrames;
        float absScore = Mathf.Abs(hip.offset.y - average);    // absolute will have average close to offset
        float locScore = Mathf.Abs(average);    // lowest score wins
        return locScore < absScore;
    }

	public void LogHierarchy() {
		foreach(var bone in bones) {
			Debug.Log(bone.ToDebugString());
		}
	}

    float ReadFrameTime(string[] lines) {
        foreach(var line in lines) {
            if(line.StartsWith("Frame Time:")) {
                var parts = line.Split(':');
                return float.Parse(parts[1]);
            }
        }
        return (1f / 30);   // default to 30 FPS
    }

    int GetRotationOrder(string c1, string c2, string c3) {
        c1 = c1.ToLower().Substring(0, 1); c2 = c2.ToLower().Substring(0, 1); c3 = c3.ToLower().Substring(0, 1);
        if(c1 == "x" && c2 == "y" && c3 == "z") return RotationOrder.XYZ;
        if(c1 == "x" && c2 == "z" && c3 == "y") return RotationOrder.XZY;
        if(c1 == "y" && c2 == "x" && c3 == "z") return RotationOrder.YXZ;
        if(c1 == "y" && c2 == "z" && c3 == "x") return RotationOrder.YZX;
        if(c1 == "z" && c2 == "x" && c3 == "y") return RotationOrder.ZXY;
        if(c1 == "z" && c2 == "y" && c3 == "x") return RotationOrder.ZYX;
        return RotationOrder.ZXY;
    }

	BvhBone[] ReadHierarchy(string[] lines) {
        char[] delims = {' ', '\t'};
        var boneList = new List<BvhBone>();
		BvhBone current = null;
		int frameOffset = 0;
        for(var i = 0; i < lines.Length; i++) {
            if(lines[i] == "MOTION")
				break;
            var parts = lines[i].Split(delims, System.StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length >= 2 && (parts[0] == "JOINT" || parts[0] == "ROOT")) {
				current = new BvhBone();
				current.name = parts[1];
				current.offset = Vector3.zero;
				current.frameOffset = frameOffset;
                if(current.name == "hip")
                    current.isHipBone = true;
                boneList.Add(current);
            }
			if(parts.Length >= 4 && parts[0] == "OFFSET" && current != null)
				current.offset = new Vector3(-float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])) * 0.01f;
			if(parts.Length >= 2 && parts[0] == "CHANNELS" && current != null) {
				var nChannels = int.Parse(parts[1]);
				frameOffset += nChannels;
				// XXX: examples may exist that are not covered here (but I think they're rare) -- Found some!
                // We now support 6 channels with X,Y,Zpos in first 3 and any rotation order
                // Or 3 channels with any rotation order
				if(nChannels == 3) {
					current.hasPosition = false;
					current.hasRotation = true;
                    current.rotationOrder = GetRotationOrder(parts[2], parts[3], parts[4]);
				}
				else if(nChannels == 6) {
					current.hasPosition = true;
					current.hasRotation = true;
                    current.rotationOrder = GetRotationOrder(parts[5], parts[6], parts[7]);
				}
				else
                    SuperController.LogError(string.Format("Unexpect number of channels in BVH Hierarchy {1} {0}", nChannels, current.name));
			}
			if(parts.Length >= 2 && parts[0] == "End" && parts[1] == "Site")
				current = null;
        }
		return boneList.ToArray();
	}

	float[][] ReadMotion(string[] lines) {
        char[] delims = {' ', '\t'};
		var output = new List<float[]>();
		var i = 0;
        for(; i < lines.Length; i++) {
            if(lines[i] == "MOTION")
				break;
		}
		i++;
		for(; i < lines.Length; i++) {
			var raw = lines[i].Split(delims, System.StringSplitOptions.RemoveEmptyEntries);
			if(raw[0].StartsWith("F"))	// Frame Time: and Frames:
				continue;
			var frame = new float[raw.Length];
			for(var j = 0; j < raw.Length; j++)
				frame[j] = float.Parse(raw[j]);
			output.Add(frame);
		}
		return output.ToArray();
	}

	public BvhTransform[] ReadFrame(int frame) {
		var data = frames[frame];
		var ret = new BvhTransform[bones.Length];
		for(var i = 0; i < bones.Length; i++) {
			var tf = new BvhTransform();
			var bone = bones[i];
			tf.bone = bone;
			var offset = bone.frameOffset;
			if(bone.hasPosition) {
				// Use -'ve X to convert RH->LH
				tf.position = new Vector3(-data[offset], data[offset + 1], data[offset + 2]) * 0.01f;
				offset += 3;
			}
            float v1 = data[offset], v2 = data[offset + 1], v3 = data[offset + 2];

            Quaternion qx, qy, qz;
            switch(bone.rotationOrder) {
                case RotationOrder.XYZ:
                    qx = Quaternion.AngleAxis(-v1, Vector3.left);
                    qy = Quaternion.AngleAxis(-v2, Vector3.up);
                    qz = Quaternion.AngleAxis(-v3, Vector3.forward);
                    tf.rotation = qx * qy * qz;
                break;
                case RotationOrder.XZY:
                    qx = Quaternion.AngleAxis(-v1, Vector3.left);
                    qy = Quaternion.AngleAxis(-v3, Vector3.up);
                    qz = Quaternion.AngleAxis(-v2, Vector3.forward);
                    tf.rotation = qx * qz * qy;
                break;
                case RotationOrder.YXZ:
                    qx = Quaternion.AngleAxis(-v2, Vector3.left);
                    qy = Quaternion.AngleAxis(-v1, Vector3.up);
                    qz = Quaternion.AngleAxis(-v3, Vector3.forward);
                    tf.rotation = qy * qx * qz;
                break;
                case RotationOrder.YZX:
                    qx = Quaternion.AngleAxis(-v3, Vector3.left);
                    qy = Quaternion.AngleAxis(-v1, Vector3.up);
                    qz = Quaternion.AngleAxis(-v2, Vector3.forward);
                    tf.rotation = qy * qz * qx;
                break;
                case RotationOrder.ZXY:
                    qx = Quaternion.AngleAxis(-v2, Vector3.left);
                    qy = Quaternion.AngleAxis(-v3, Vector3.up);
                    qz = Quaternion.AngleAxis(-v1, Vector3.forward);
                    tf.rotation = qz * qx * qy;
                break;
                case RotationOrder.ZYX:
                    qx = Quaternion.AngleAxis(-v3, Vector3.left);
                    qy = Quaternion.AngleAxis(-v2, Vector3.up);
                    qz = Quaternion.AngleAxis(-v1, Vector3.forward);
                    tf.rotation = qz * qy * qx;
                break;
            }

			ret[i] = tf;
		}
		return ret;
	}
}


}