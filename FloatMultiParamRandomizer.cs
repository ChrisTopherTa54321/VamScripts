using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

// Original FloatParamRandomizer written by MeshedVR
// Multi-Param rewrite by HSThrowaway5

namespace HSTA
{

    // This class will produce a random number that can be used to set any float param available in all atoms
    // includes random generation period, smoothing, and range selection options
    public class FloatMultiParamRandomizer : MVRScript
    {
        const string pluginName = "FloatMultiParamRandomizer";
        const string pluginVersion = "V1.0.5+";
        const string saveExt = "fmpr";
        public override void Init()
        {
            try
            {
                _origFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;

                _displayRandomizer = new ParamRandomizer("display", null);
                _displayRandomizer.UpdateEnabledListEvnt += UpdateEnabledList;

                // make atom selector
                _atomJSON = new JSONStorableStringChooser("atom", SuperController.singleton.GetAtomUIDs(), null, "Atom", SyncAtom);
                RegisterStringChooser(_atomJSON);
                SyncAtomChoices();
                _displayPopup = CreateScrollablePopup(_atomJSON);
                _displayPopup.popupPanelHeight = 1100f;
                // want to always resync the atom choices on opening popup since atoms can be added/removed
                _displayPopup.popup.onOpenPopupHandlers += SyncAtomChoices;
                _displayPopup.label = "Atom in Scene";

                // make receiver selector
                _receiverJSON = new JSONStorableStringChooser("receiver", null, null, "Receiver", SyncReceiver);
                RegisterStringChooser(_receiverJSON);
                _displayPopup = CreateScrollablePopup(_receiverJSON);
                _displayPopup.popupPanelHeight = 960f;
                // want to always resync the receivers, since plugins can be added/removed
                _displayPopup.popup.onOpenPopupHandlers += SyncReceiverChoices;
                _displayPopup.label = "Receiver on Atom";

                // make receiver target selector
                _targetJson = new JSONStorableStringChooser("receiverTarget", null, null, "Target", SyncTargets);
                _displayPopup = CreateScrollablePopup(_targetJson);
                _displayPopup.popupPanelHeight = 820f;
                // want to always resync the targets, since morphs can be marked animatable
                _displayPopup.popup.onOpenPopupHandlers += SyncTargetChoices;
                _displayPopup.label = "Parameter on Receiver";

                // Add Load/Save buttons
                var btn = CreateButton("Load Preset");
                btn.button.onClick.AddListener(() =>
                {
                    uFileBrowser.FileBrowser browser = SuperController.singleton.fileBrowserUI;

                    browser.defaultPath = _lastBrowseDir;
                    browser.SetTextEntry(false);
                    browser.fileFormat = saveExt;
                    browser.Show(HandleLoadPreset);
                });


                _addAnimatable = new JSONStorableBool("autoSetAnimatable", true);
                var toggle = CreateToggle(_addAnimatable);
                toggle.label = "Auto-set 'animatable' on load";
                _setReceiverOnLoad = new JSONStorableBool("setReceiverOnLoad", true);
                toggle = CreateToggle(_setReceiverOnLoad);
                toggle.label = "Set 'receiver' on load";

                _setAtomOnLoad = new JSONStorableBool("setAtomOnLoad", false);

                btn = CreateButton("Save Preset");
                btn.button.onClick.AddListener(() =>
                {
                    uFileBrowser.FileBrowser browser = SuperController.singleton.fileBrowserUI;

                    browser.defaultPath = _lastBrowseDir;
                    browser.SetTextEntry(true);
                    browser.fileFormat = saveExt;
                    browser.Show(HandleSavePreset);
                    browser.fileEntryField.text = String.Format("{0}.{1}", ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(), saveExt);
                    browser.ActivateFileNameField();
                });


                // set atom to current atom to initialize
                _atomJSON.val = containingAtom.uid;

                if( String.IsNullOrEmpty( _lastBrowseDir ) )
                {
                    _lastBrowseDir = GetPluginPath() + @"\fpmr_presets";
                }

                var spacer = CreateSpacer();
                spacer.height = 40;

                btn = CreateButton("Set Min to Current");
                btn.button.onClick.AddListener(() =>
                {
                    _displayRandomizer._minVal.val = _displayRandomizer._curVal.val;
                });

                btn = CreateButton("Set Max to Current");
                btn.button.onClick.AddListener(() =>
                {
                    _displayRandomizer._maxVal.val = _displayRandomizer._curVal.val;
                });

                spacer = CreateSpacer();
                spacer.height = 40;

                var countLabel = CreateTextField(new JSONStorableString("", ""));
                JSONStorableFloat.SetFloatCallback callback = (float aVal) =>
                {
                    countLabel.text = pluginName + " " + pluginVersion + "\n"
                                    + "\n"
                                    + "Active randomizers: " + aVal.ToString()
                                    ;
                    pluginLabelJSON.val = String.Format("{0}->{1}, {2} running [{3}]", _atom.name, _receiverJSON.val, _randomizerEnabledList.Count.ToString(), pluginVersion);
                };
                countLabel.height = 10;
                _activeRandomizersJson = new JSONStorableFloat("activeRandomizers", -1.0f, callback, 0.0f, 0.0f, false, false);

                spacer = CreateSpacer();
                spacer.height = 40;

                _updateRate = new JSONStorableFloat("update_rate_ms", 15.0f, 0.0f, 1000f, false);
                RegisterFloat(_updateRate);
                var slider = CreateSlider(_updateRate);
                slider.label = "Update Delay (ms)";
                toggle = CreateToggle(_displayRandomizer._enabled);
                toggle.label = "Enable Randomizer";


                // Create per-randomizer sliders
                slider = CreateSlider(_displayRandomizer._period, true);
                slider.label = "Period\nSec between updates";

                slider = CreateSlider(_displayRandomizer._periodRandomMin, true);
                slider.label = "Period min val";

                slider = CreateSlider(_displayRandomizer._periodRandomMax, true);
                slider.label = "Period max val";

                slider = CreateSlider(_displayRandomizer._quickness, true);
                slider.label = "Quickness";

                slider = CreateSlider(_displayRandomizer._minVal, true);
                slider.label = "Random range min val";

                slider = CreateSlider(_displayRandomizer._maxVal, true);
                slider.label = "Random range max val";

                slider = CreateSlider(_displayRandomizer._percentage, true);
                slider.label = "Max target val change %";

                slider = CreateSlider(_displayRandomizer._targetVal, true);
                slider.label = "Target Value";
                slider.defaultButtonEnabled = false;
                slider.quickButtonsEnabled = false;

                slider = CreateSlider(_displayRandomizer._curVal, true);
                slider.label = "Current value\nYou can slide manually";
                slider.defaultButtonEnabled = false;
                slider.quickButtonsEnabled = false;

                // Trigger setting labels
                _activeRandomizersJson.val = 0.0f;
            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        protected void Start()
        {
            // If the scene is loading then manually restore parameters from JSON
            if( SuperController.singleton.isLoading )
            {
                RestoreParamsFromSaveJson();
            }
            UpdateEnabledList();
            _coroutine = StartCoroutine(Update_Routine());
        }

        protected void OnDisable()
        {
            StopRoutines();
        }

        protected void OnEnable()
        {
            StartRoutine();
        }


        private void StartRoutine()
        {
            if( null == _coroutine )
            {
                _coroutine = StartCoroutine(Update_Routine());
            }
        }

        private void StopRoutines()
        {
            if( null != _coroutine )
            {
                StopCoroutine(_coroutine);
                _coroutine = null;
            }
            if (null != _missingRecvCo)
            {
                StopCoroutine(_missingRecvCo);
                _missingRecvCo = null;
            }
        }

        protected void SetWaitForMissingReceiver(string aReceiver)
        {
            StopCoroutine(_missingRecvCo);
            _missingReceiverStoreId = aReceiver;
            _missingRecvCo = StartCoroutine(MissingReceiverCo());
        }


        protected IEnumerator MissingReceiverCo()
        {
            while( WaitingForMissingReceiver() )
            {
                yield return null;
            }
            yield break;
        }

        protected IEnumerator Update_Routine()
        {
            while( true )
            {
                float waitTime = _updateRate.val/1000;
                if (waitTime > 0.0f )
                {
                    yield return new WaitForSecondsRealtime(waitTime);
                }
                else
                {
                    yield return new WaitForFixedUpdate();
                    waitTime = Time.fixedUnscaledDeltaTime;
                }

                try
                {
                    foreach (var randomizer in _randomizerEnabledList)
                    {
                        randomizer.Update(waitTime);
                    }

                    if (null != _displayRandomizer._syncTarget && _displayPopup.isActiveAndEnabled)
                    {
                        ParamRandomizer.CopyValues(_displayRandomizer, _displayRandomizer._syncTarget);
                    }
                }
                catch( Exception e )
                {
                    SuperController.LogError(e.ToString());
                }
            }
        }


        string GetPluginPath()
        {
            string path = "";
            string pluginId = this.storeId.Split('_')[0];
            foreach (var pluginUi in this.manager.pluginListPanel.GetComponentsInChildren<MVRPluginUI>())
            {
                if (pluginUi.uidText.text == pluginId)
                {
                    path = pluginUi.urlText.text;
                    path = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' }));
                    path = path.Replace('/', '\\'); // backslash required for file dialogs
                    break;
                }
            }
            return path;
        }


        JSONClass GetSaveJson()
        {
            JSONClass saveJson = new JSONClass();
            saveJson["atom"] = _atom.name;
            saveJson["receiver"] = _receiver.name;
            saveJson["savedBy"] = pluginVersion;
            saveJson["targets"] = new JSONArray();
            foreach (var randomizer in _randomizerList)
            {
                JSONClass randomizerNode = new JSONClass();
                foreach (var storable in randomizer.GetStorableBools())
                {
                    storable.StoreJSON(randomizerNode);
                }
                foreach (var storable in randomizer.GetStorableFloats())
                {
                    storable.StoreJSON(randomizerNode);
                }

                if (randomizerNode.Count > 0)
                {
                    randomizerNode["id"] = randomizer._id;
                    saveJson["targets"].Add(randomizerNode);
                }
            }
            return saveJson;
        }


        void LoadSaveJson(JSONNode aJson)
        {
            string receiver;
            if (_setReceiverOnLoad.val)
            {
                receiver = aJson["receiver"].Value;
            }
            else
            {
                receiver = _receiverJSON.val;
            }

            string atom;
            if( _setAtomOnLoad.val )
            {
                atom = aJson["atom"].Value;
            }
            else
            {
                atom = _atom.name;
            }
            _atomJSON.val = atom; // sync atom

            _receiverJSON.val = receiver; // sync receiver

            foreach (JSONNode target in aJson["targets"].AsArray)
            {
                string targetId = target["id"].Value;
                var randomizer = GetRandomizerById(targetId);
                if (randomizer != null)
                {
                    randomizer.RestoreFromJson(target);
                }
                else
                {
                    if (_addAnimatable.val && receiver == "geometry")
                    {
                        // Ensure target is animatable
                        JSONStorable geometry = containingAtom.GetStorableByID("geometry");
                        DAZCharacterSelector character = geometry as DAZCharacterSelector;
                        GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
                        DAZMorph morph = morphControl.GetMorphByDisplayName(targetId);
                        if (morph != null)
                        {
                            morph.animatable = true;
                            // Add the newly-animatable target to the list and try again
                            SyncTargetChoices();
                            randomizer = GetRandomizerById(targetId);
                            if (null != randomizer)
                            {
                                randomizer.RestoreFromJson(target);
                            }
                        }
                    }

                    // Still no randomizer? Report the missing param
                    if (randomizer == null)
                    {
                        SuperController.LogMessage("Failed to add randomizer for " + targetId);
                    }

                }
            }

            SyncTargetChoices();
            SyncTargets(_targetJson.val);
            UpdateEnabledList();
        }


        void HandleSavePreset(string aPath)
        {
            SuperController.singleton.fileBrowserUI.fileFormat = _origFileFormat;
            if (String.IsNullOrEmpty(aPath))
            {
                return;
            }
            _lastBrowseDir = aPath;

            if ( !aPath.ToLower().EndsWith(saveExt.ToLower()))
            {
                aPath += "." + saveExt;
            }
            JSONClass saveJson = GetSaveJson();
            this.SaveJSON(saveJson, aPath);
        }

        void HandleLoadPreset( string aPath )
        {
            SuperController.singleton.fileBrowserUI.fileFormat = _origFileFormat;
            if (String.IsNullOrEmpty(aPath))
            {
                return;
            }
            _lastBrowseDir = aPath;

            LoadSaveJson(this.LoadJSON(aPath));
        }

        void RestoreParamsFromSaveJson()
        {
            JSONNode savedJson = GetPluginJsonFromSave();
            if (savedJson != null)
            {
                foreach (var randomizer in _randomizerList)
                {
                    randomizer.RestoreFromJson(savedJson);
                }
            }
        }


        ParamRandomizer GetRandomizerById( string aId )
        {
            foreach( var randomizer in _randomizerList )
            {
                if( randomizer._id == aId )
                {
                    return randomizer;
                }
            }
            return null;
        }

        JSONNode GetPluginJsonFromSave()
        {
            foreach (JSONNode atoms in SuperController.singleton.loadJson["atoms"].AsArray)
            {
                if (atoms["id"].Value == _atom.name)
                {
                    foreach (JSONNode storable in atoms["storables"].AsArray)
                    {
                        if (storable["id"].Value == this.storeId)
                        {
                            return storable;
                        }
                    }
                }
            }
            return null;
        }


        protected void SyncAtomChoices()
        {
            List<string> atomChoices = new List<string>();
            atomChoices.Add("None");
            foreach (string atomUID in SuperController.singleton.GetAtomUIDs())
            {
                atomChoices.Add(atomUID);
            }
            _atomJSON.choices = atomChoices;
        }


        IEnumerator DelayLoadSavedJson( JSONClass aJson, float aDelay )
        {
            yield return new WaitForSecondsRealtime(aDelay);

            pluginLabelJSON.val = "*Values lost* This happens on Load Look";

            bool prevVal = _setAtomOnLoad.val;
            _setAtomOnLoad.valNoCallback = true;
            LoadSaveJson(aJson);
            _setAtomOnLoad.valNoCallback = prevVal;
        }

        // receiver Atom
        protected void SyncAtom(string atomUID)
        {
            // Workaround Load Look losing all of our values
            if ( String.IsNullOrEmpty(atomUID) )
            {
                // This will restore Atom and Receiver, but randomizers are already lost
                StartCoroutine( DelayLoadSavedJson( GetSaveJson(), 0.1f) );
                return;
            }

            string defaultReceiver = "None";
            List<string> receiverChoices = new List<string>();
            receiverChoices.Add( defaultReceiver );
            if (atomUID != null)
            {
                _atom = SuperController.singleton.GetAtomByUid(atomUID);
                if (_atom != null)
                {
                    foreach (string receiverChoice in _atom.GetStorableIDs())
                    {
                        // Only add receiver if it has at least one float param
                        if( _atom.GetStorableByID(receiverChoice)?.GetFloatParamNames()?.Count > 0 )
                        {
                            receiverChoices.Add(receiverChoice);

                            // Set 'geometry' as the default receiver
                            if (receiverChoice == "geometry")
                            {
                                defaultReceiver = receiverChoice;
                            }
                        }
                    }
                }
            }
            else
            {
                _atom = null;
            }
            _receiverJSON.choices = receiverChoices;

            if (!_skipUpdateVal || !receiverChoices.Contains(_receiverJSON.val)) 
            {
                _receiverJSON.val = defaultReceiver;
            }
        }


        protected bool WaitingForMissingReceiver()
        {
            bool waiting = false;
            if (_missingReceiverStoreId != "" && _atom != null)
            {
                JSONStorable missingReceiver = _atom.GetStorableByID(_missingReceiverStoreId);
                if (missingReceiver != null)
                {
                    SyncReceiverChoices();
                    SyncReceiver(_missingReceiverStoreId);
                    RestoreParamsFromSaveJson();
                    UpdateEnabledList();
                    _missingReceiverStoreId = "";
                }
                waiting = true;
            }
            return waiting;
        }

        protected void SyncReceiverChoices()
        {
            _skipUpdateVal = true;
            SyncAtom(_atom.name);
            _skipUpdateVal = false;
        }

        // receiver JSONStorable
        protected void SyncReceiver(string receiverID)
        {
            List<string> targetChoices = new List<string>();
            foreach (ParamRandomizer randomizer in _randomizerList)
            {
                DeregisterRandomizer(randomizer);
            }

            Dictionary<string, ParamRandomizer> oldDict = null;

            // If this is just refreshing the list we need to save existing randomizers
            string defaultTarget;
            if (_skipUpdateVal)
            {
                oldDict = _randomizerDict;
                _randomizerDict = new Dictionary<string, ParamRandomizer>();
                defaultTarget = _targetJson.val;
            }
            else
            {
                // Otherwise if the receiver changed then clear the enabled list
                _randomizerEnabledList.Clear();
                defaultTarget = "None";
            }
            _randomizerDict.Clear();
            _randomizerList.Clear();
            UpdateEnabledList();

            targetChoices.Add("None");
            if (_atom != null && receiverID != null)
            {
                if (!_skipUpdateVal)
                {
                    _receiver = _atom.GetStorableByID(receiverID);

                }
                if (_receiver != null)
                {
                    foreach (string paramName in _receiver.GetFloatParamNames())
                    {
                        ParamRandomizer randomizer;
                        // Use the old randomizer if it exists, otherwise make a new one
                        if (!(oldDict != null && oldDict.TryGetValue(paramName, out randomizer)))
                        {
                            randomizer = new ParamRandomizer(paramName, _receiver.GetFloatJSONParam(paramName));
                        }
                        RegisterRandomizer(randomizer);
                        _randomizerList.Add(randomizer);
                        _randomizerDict[paramName] = randomizer;
                        targetChoices.Add(paramName);
                    }
                }
                else if (receiverID != defaultTarget)
                {
                    // some storables can be late loaded, like skin, clothing, hair, etc so must keep track of missing receiver
                    //SuperController.LogMessage("Missing receiver: " + receiverID);
                    SetWaitForMissingReceiver(receiverID);
                }
            }
            else
            {
                _receiver = null;
            }

            _targetJson.choices = targetChoices;
            if (!_skipUpdateVal || !targetChoices.Contains(_receiverJSON.val))
            {
                _targetJson.val = defaultTarget;
                // Clear the display
                ParamRandomizer.CopyValues(_displayRandomizer, new ParamRandomizer("display", null));
            }
        }


        protected void SyncTargetChoices()
        {
            _skipUpdateVal = true;
            SyncReceiver( _receiver.name );
            _skipUpdateVal = false;
        }


        protected void SyncTargets(string receiverTargetName)
        {
            _receiverTargetName = receiverTargetName;
            ParamRandomizer randomizer;

            if (_receiver != null && receiverTargetName != null && _randomizerDict.TryGetValue( receiverTargetName, out randomizer) )
            {
                // Sync the display randomizer with the actual randomizer
                _displayRandomizer.SyncWith(randomizer);
            }
            else if( receiverTargetName == "None" )
            {
                _displayRandomizer.SyncWith(new ParamRandomizer("", null));
            }
        }

        void UpdateEnabledList()
        {
            _randomizerEnabledList.Clear();
            foreach (var randomizer in _randomizerList)
            {
                if (randomizer._enabled.val)
                {
                    _randomizerEnabledList.Add(randomizer);
                }
            }

            if( _activeRandomizersJson != null )
            {
                _activeRandomizersJson.val = _randomizerEnabledList.Count;
            }
        }


        void RegisterRandomizer( ParamRandomizer aRandomizer )
        {
            var floatList = aRandomizer.GetStorableFloats();
            foreach( var storable in floatList )
            {
                RegisterFloat(storable);
            }

            var boolList = aRandomizer.GetStorableBools();
            foreach( var storable in boolList )
            {
                RegisterBool(storable);
            }
        }

        void DeregisterRandomizer( ParamRandomizer aRandomizer )
        {
            var floatList = aRandomizer.GetStorableFloats();
            foreach (var storable in floatList)
            {
                DeregisterFloat(storable);
            }

            var boolList = aRandomizer.GetStorableBools();
            foreach (var storable in boolList)
            {
                DeregisterBool(storable);
            }
        }

        // Saved in JSON
        protected JSONStorableStringChooser _atomJSON;
        protected JSONStorableStringChooser _receiverJSON;

        // Not saved in JSON
        protected Atom _atom;
        protected JSONStorable _receiver;
        protected JSONStorableFloat _activeRandomizersJson;
        protected bool _skipUpdateVal = false;

        // receiver target parameter
        protected JSONStorableStringChooser _targetJson;
        protected string _receiverTargetName;
        protected JSONStorableFloat _target;

        protected string _missingReceiverStoreId = "";

        protected Coroutine _coroutine = null;
        protected Coroutine _missingRecvCo = null;
        protected JSONStorableFloat _updateRate;
        protected ParamRandomizer _displayRandomizer;
        protected UIDynamicPopup _displayPopup; // any UI element, just to check if visible
        protected JSONStorableBool _addAnimatable;
        protected JSONStorableBool _setAtomOnLoad;
        protected JSONStorableBool _setReceiverOnLoad;
        protected string _origFileFormat;
        protected string _lastBrowseDir;

        List<ParamRandomizer> _randomizerList = new List<ParamRandomizer>();
        List<ParamRandomizer> _randomizerEnabledList = new List<ParamRandomizer>();
        Dictionary<string, ParamRandomizer> _randomizerDict = new Dictionary<string, ParamRandomizer>();
    }


    public class ParamRandomizer
    {

        public ParamRandomizer(string aId, JSONStorableFloat aTarget )
        {
            _id = aId;
            _target = aTarget;

            string prefix = "";
            if( null != _target)
            {
                prefix = _id + "_";
                _shouldSave = true;
            }

            _enabled = new JSONStorableBool(prefix + "enabled", false, onEnabledChanged);
            _period = new JSONStorableFloat(prefix + "period", 0.5f, onPeriodChanged, 0f, 10f, false);
            _periodRandomMin = new JSONStorableFloat(prefix + "periodLowerValue", 0.0f, onFloatChanged, 0f, 10f, false);
            _periodRandomMax = new JSONStorableFloat(prefix + "periodUpperValue", 2.0f, onFloatChanged, 0f, 10f, false);
            _quickness = new JSONStorableFloat(prefix + "quickness", 10f, onFloatChanged, 0f, 100f, true);
            _minVal = new JSONStorableFloat(prefix + "lowerValue", 0f, onFloatChanged, 0f, 1f, false);
            _maxVal = new JSONStorableFloat(prefix + "upperValue", 1f, onFloatChanged, 0f, 1f, false);
            _percentage = new JSONStorableFloat(prefix + "percentage", 100.0f, onFloatChanged, 0f, 100f, false);

            _targetVal = new JSONStorableFloat("targetValue", 0f, 0f, 1f, false, false);
            _curVal = new JSONStorableFloat("currentValue", 0f, onCurValChanged, 0f, 1f, false, true);
            if( null != _target)
            {
                _minVal.min = _target.min;
                _minVal.max = _target.max;
                _maxVal.min = _target.min;
                _maxVal.max = _target.max;
                _targetVal.min = _target.min;
                _targetVal.max = _target.max;
                _curVal.min = _target.min;
                _curVal.max = _target.max;
            }
        }

        public List<JSONStorableBool> GetStorableBools()
        {
            var storables = new List<JSONStorableBool>();
            storables.Add(_enabled);
            return storables;
        }

        public List<JSONStorableFloat> GetStorableFloats()
        {
            var storables = new List<JSONStorableFloat>();
            storables.Add(_period);
            storables.Add(_periodRandomMin);
            storables.Add(_periodRandomMax);
            storables.Add(_percentage);
            storables.Add(_quickness);
            storables.Add(_minVal);
            storables.Add(_maxVal);
            return storables;
        }

        public void RestoreFromJson(JSONNode aJson)
        {
            foreach (var storable in GetStorableFloats())
            {
                if( aJson[storable.name] != null )
                {
                    storable.val = aJson[storable.name].AsFloat;
                }
            }
            foreach (var storable in GetStorableBools())
            {
                if (aJson[storable.name] != null)
                {
                    storable.val = aJson[storable.name].AsBool;
                }
            }
        }


        public void SyncWith( ParamRandomizer aRandomizer )
        {
            _syncTarget = aRandomizer;
            this._disableHandlers = true;
            CopyValues(this, _syncTarget);
            this._disableHandlers = false;
        }

        public static void CopyValues( ParamRandomizer aDst, ParamRandomizer aSrc )
        {
            bool prevDisable = aDst._disableHandlers;
            aDst._disableHandlers = true;

            aDst._enabled.val = aSrc._enabled.val;
            CopyStorableFloat(aDst._period, aSrc._period, aDst._disableHandlers);
            CopyStorableFloat(aDst._periodRandomMin, aSrc._periodRandomMin, aDst._disableHandlers);
            CopyStorableFloat(aDst._periodRandomMax, aSrc._periodRandomMax, aDst._disableHandlers);
            CopyStorableFloat(aDst._percentage, aSrc._percentage, aDst._disableHandlers);
            CopyStorableFloat(aDst._quickness, aSrc._quickness, aDst._disableHandlers);
            CopyStorableFloat(aDst._minVal, aSrc._minVal, aDst._disableHandlers );
            CopyStorableFloat(aDst._maxVal, aSrc._maxVal, aDst._disableHandlers);
            CopyStorableFloat(aDst._curVal, aSrc._curVal, aDst._disableHandlers);
            CopyStorableFloat(aDst._targetVal, aSrc._targetVal, aDst._disableHandlers);

            aDst._disableHandlers = prevDisable;
        }

        public static void CopyStorableFloat( JSONStorableFloat aDst, JSONStorableFloat aSrc, bool aDisableHandlers = false )
        {
            aDst.max = aSrc.max;
            aDst.min = aSrc.min;

            if (aDisableHandlers)
            {
                aDst.valNoCallback = aSrc.val;
            }
            else
            {
                aDst.val = aSrc.val;
            }
        }

        const float lerping_thresh = .001f;
        public void Update(float aDeltaTime)
        {
            if( !_enabled.val )
            {
                return;
            }

            _timer += aDeltaTime;
            if (_timer > _targetTime )
            {
                // Change period?
                _period.valNoCallback = UnityEngine.Random.Range(_periodRandomMin.val, _periodRandomMax.val);

                // reset timer and set a new random target value
                _targetTime += _period.val;

                // Pick a new random value, but make sure it's within a percentage from our current value
                float range = ((_maxVal.val - _minVal.val) / 100) * _percentage.val;
                float randomMin = Math.Max( _curVal.val - range, _minVal.val );
                float randomMax = Math.Min( _curVal.val + range, _maxVal.val );

                // Start the lerping
                _targetVal.val = UnityEngine.Random.Range(randomMin, randomMax);
                _lerping = true;
            }


            if( _lerping )
            {
                _curVal.valNoCallback = Mathf.Lerp(_curVal.val, _targetVal.val, aDeltaTime * _quickness.val);
                if (_target != null)
                {
                    _target.val = _curVal.val;
                }

                if (Math.Abs(_curVal.val - _targetVal.val) < lerping_thresh)
                {
                    _curVal.valNoCallback = _targetVal.val;
                    _lerping = false;
                }
            }

        }


        void onEnabledChanged( bool aVal )
        {
            if( !_disableHandlers && _syncTarget != null )
            {
                CopyValues(_syncTarget, this);
                UpdateEnabledListEvnt.Invoke();
            }
        }


        void onBoolChanged(bool aVal)
        {
            if (!_disableHandlers && _syncTarget != null)
            {
                CopyValues(_syncTarget, this);
            }
        }


        void onCurValChanged( float aVal )
        {
            if (!_disableHandlers && _syncTarget?._target != null && (!_syncTarget._enabled.val || !_lerping ) )
            {
                _syncTarget._target.val = aVal;
                CopyValues(_syncTarget, this);
            }
        }

        void onPeriodChanged( float aVal )
        {
            if( !_disableHandlers )
            {
                _periodRandomMax.val = aVal;
                _periodRandomMin.val = aVal;
            }
            _targetTime = _timer + _period.val;
            onFloatChanged(aVal);
        }

        void onFloatChanged( float aVal )
        {
            if (!_disableHandlers && _syncTarget != null)
            {
                CopyValues(_syncTarget, this);
            }
        }

        // Saveables
        public string _id;
        public JSONStorableBool _enabled;
        public JSONStorableFloat _period;
        public JSONStorableFloat _periodRandomMin;
        public JSONStorableFloat _periodRandomMax;
        public JSONStorableFloat _percentage;
        public JSONStorableFloat _quickness;
        public JSONStorableFloat _minVal;
        public JSONStorableFloat _maxVal;

        // Non-saved
        public JSONStorableFloat _targetVal;
        public JSONStorableFloat _curVal;

        public bool _shouldSave = false;

        public event Action UpdateEnabledListEvnt;
        public ParamRandomizer _syncTarget { get; private set; }
        JSONStorableFloat _target;
        private bool _disableHandlers = false;

        float _timer;
        float _targetTime;
        bool _lerping = false;
    }

}