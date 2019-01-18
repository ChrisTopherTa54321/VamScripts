using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace HSTA
{
    public class HairStylist : MVRScript
    {
        public static string pluginName = "HairStylist";
        public static string pluginVersion = "V0.3.0";
        public static string saveExt = "json";

        public override void Init()
        {
            try
            {
                if (containingAtom.type != "Person")
                {
                    SuperController.LogError("Use this plugin on a Person only");
                    return;
                }

                if (String.IsNullOrEmpty(_lastBrowseDir))
                {
                    _lastBrowseDir = GetPluginPath() + @"hair_presets\";
                }

                pluginLabelJSON.val = pluginName + " " + pluginVersion;

                // Add Load/Save buttons
                var btn = CreateButton("Load Preset");
                btn.button.onClick.AddListener(() =>
                {
                    SuperController.singleton.NormalizeMediaPath(_lastBrowseDir); // This sets the path iff it exists
                    SuperController.singleton.GetMediaPathDialog(HandleLoadPreset, saveExt);
                });

                _loadStyle = new JSONStorableBool("loadStyle", true);
                var toggle = CreateToggle(_loadStyle);
                toggle.label = "Load Style";

                _loadColor = new JSONStorableBool("loadColor", true);
                toggle = CreateToggle(_loadColor);
                toggle.label = "Load Color";

                _loadPhysics = new JSONStorableBool("loadPhysics", true);
                toggle = CreateToggle(_loadPhysics);
                toggle.label = "Load Physics";


                var spacer = CreateSpacer();
                spacer.height = 100;

                var label = CreateTextField(new JSONStorableString("", ""));
                label.text = pluginName + " " + pluginVersion + "\n"
                           + "\n"
                           + "Load Original loads hair when plugin was loaded.\n"
                           + "\n"
                           + "'Quick Load/Save' can be used as a temporary save slot.\n"
                           + "\n"
                           + "All 'Load' buttons respect selection checkboxes."
                           ;
                label.height = 350;
                spacer = CreateSpacer();
                spacer.height = 100;

                btn = CreateButton("Load Original");
                btn.button.onClick.AddListener(() =>
                {
                    _original?.ApplyToPerson(containingAtom, _loadColor.val, _loadStyle.val, _loadPhysics.val);
                });
                _original = HairStyle.CreateFromPerson(containingAtom);

                btn = CreateButton("Quick Save");
                btn.button.onClick.AddListener(() =>
                {
                    _quickSaved = HairStyle.CreateFromPerson(containingAtom);
                });

                btn = CreateButton("Quick Load");
                btn.button.onClick.AddListener(() =>
                {
                    _quickSaved?.ApplyToPerson(containingAtom, _loadColor.val, _loadStyle.val, _loadPhysics.val);
                });

                btn = CreateButton("Save Preset", true);
                btn.button.onClick.AddListener(() =>
                {
                    SuperController.singleton.NormalizeMediaPath(_lastBrowseDir); // This sets the path iff it exists
                    SuperController.singleton.GetMediaPathDialog(HandleSavePreset, saveExt);

                    // Update the browser to be a Save browser
                    uFileBrowser.FileBrowser browser = SuperController.singleton.mediaFileBrowserUI;
                    browser.SetTextEntry(true);
                    browser.fileEntryField.text = String.Format("{0}.{1}", ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(), saveExt);
                    browser.ActivateFileNameField();
                });
                spacer = CreateSpacer(true);
                spacer.height = 300;

                btn = CreateButton("Open Hair Settings", true);
                btn.button.onClick.AddListener(() =>
                {
                    DAZCharacterSelector character = containingAtom?.GetStorableByID("geometry") as DAZCharacterSelector;
                    var ui = character.selectedHairGroup?.customizationUI;
                    if( null != ui )
                    {
                        SuperController.singleton.SetCustomUI(ui);

                    }
                });

            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        protected void Start()
        {
        }

        string GetPluginPath()
        {
            SuperController.singleton.currentSaveDir = SuperController.singleton.currentLoadDir;
            string pluginId = this.storeId.Split('_')[0];
            string pathToScriptFile = this.manager.GetJSON(true, true)["plugins"][pluginId].Value;
            string pathToScriptFolder = pathToScriptFile.Substring(0, pathToScriptFile.LastIndexOfAny(new char[] { '/', '\\' }) + 1);
            pathToScriptFolder = pathToScriptFolder.Replace('/', '\\');
            return pathToScriptFolder;
        }

        void HandleSavePreset(string aPath)
        {
            if (String.IsNullOrEmpty(aPath))
            {
                return;
            }
            _lastBrowseDir = aPath.Substring(0, aPath.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";

            if (!aPath.ToLower().EndsWith(saveExt.ToLower()))
            {
                aPath += "." + saveExt;
            }

            HairStyle style = HairStyle.CreateFromPerson(containingAtom);
            JSONClass json = style.GetSaveJson();
            this.SaveJSON(json, aPath);
        }

        void HandleLoadPreset(string aPath)
        {
            if (String.IsNullOrEmpty(aPath))
            {
                return;
            }
            _lastBrowseDir = aPath.Substring(0, aPath.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";

            JSONNode json = this.LoadJSON(aPath);
            HairStyle style = HairStyle.CreateFromSavedJson(json);
            style?.ApplyToPerson(containingAtom, _loadColor.val, _loadStyle.val, _loadPhysics.val);
        }


        protected JSONStorableBool _loadStyle;
        protected JSONStorableBool _loadColor;
        protected JSONStorableBool _loadPhysics;

        protected HairStyle _quickSaved;
        protected HairStyle _original;
        protected string _lastBrowseDir;

    }


    public class HairStyle
    {
        public static HairStyle CreateFromPerson( Atom aPerson )
        {
            HairStyle newHair = null;
            DAZCharacterSelector character = aPerson?.GetStorableByID("geometry") as DAZCharacterSelector;
            HairSimControl hairControl = character?.GetComponentInChildren<HairSimControl>();

            if( null != hairControl)
            {
                newHair = new HairStyle();
                newHair.SaveStorable(hairControl, out newHair._savedJson);


                // TODO: Integrate this better
                var style = character.GetComponentInChildren<HairSimStyleControl>();
                var styleChoice = style.GetStringChooserJSONParam("choiceName");
                newHair._style = styleChoice.val;
            }
            else
            {
                SuperController.LogError("This plugin only works on SimV2 Hair");
            }

            return newHair;
        }

        public static HairStyle CreateFromSavedJson(JSONNode aJson)
        {
            HairStyle newHair = null;
            if( aJson["savedByPlugin"].Value == HairStylist.pluginName )
            {
                newHair = new HairStyle();
                newHair._savedJson = new Dictionary<string, JSONClass>();
                foreach (JSONNode kp in aJson["storables"].AsArray)
                {
                    newHair._savedJson.Add(kp["id"], kp.AsObject);
                }
                newHair._style = aJson["style"];

            }
            else
            {
                SuperController.LogError("Saved file was not created by HairStylist");
            }
            return newHair;
        }


        public void ApplyToPerson( Atom aPerson, bool aColor = true, bool aStyle = true, bool aPhysics = true )
        {
            DAZCharacterSelector character = aPerson.GetStorableByID("geometry") as DAZCharacterSelector;
            List<string> restoreList = new List<string>();

            HairStyle colorStyle = null;

            if (aColor)
            {
                restoreList.AddRange(colorList);
            }
            else if (aStyle)
            {
                // If we are copying Style, but not Color, the color may change to whatever
                // was last stored on the new style. So, let's copy the current color over.
                colorStyle = HairStyle.CreateFromPerson(aPerson);
            }

            if (aStyle)
            {
                restoreList.AddRange(styleList);
                var styleJson = character?.GetComponentInChildren<HairSimStyleControl>()?.GetStringChooserJSONParam("choiceName");
                if (styleJson != null)
                {
                    styleJson.val = _style;
                }
            }
            if( aPhysics )
            {
                restoreList.AddRange(physicsList);
            }

            // Must get hairControl *after* changing style
            HairSimControl hairControl = character?.GetComponentInChildren<HairSimControl>();
            RestoreStorable(hairControl, this._savedJson, restoreList);

            // If we switched styles without loading color, then copy over the color from the last style
            if( colorStyle != null )
            {
                RestoreStorable(hairControl, colorStyle._savedJson, colorList);
            }
        }


        public JSONClass GetSaveJson()
        {
            JSONClass saveJson = new JSONClass();
            saveJson["savedByPlugin"] = HairStylist.pluginName;
            saveJson["savedByVer"] = HairStylist.pluginVersion;

            saveJson["style"] = _style;
            saveJson["storables"] = new JSONArray();

            foreach (var kp in _savedJson)
            {
                saveJson["storables"].Add(kp.Key, kp.Value);
            }
            return saveJson;
        }


        private void SaveStorable( JSONStorable aStorable, out Dictionary<string, JSONClass> aDict )
        {
            aDict = new Dictionary<string, JSONClass>();

            foreach(var param in aStorable.GetAllParamAndActionNames() )
            {
                var type = aStorable.GetParamOrActionType(param);
                JSONClass json = new JSONClass();
                json["type"] = type.ToString();
                json["id"] = param;

                JSONClass storableParams = new JSONClass();

                switch (type)
                {
                    case JSONStorable.Type.Color:
                        aStorable.GetColorJSONParam(param)?.StoreJSON(storableParams);
                        break;

                    case JSONStorable.Type.Float:
                        aStorable.GetFloatJSONParam(param)?.StoreJSON(storableParams);
                        break;

                    case JSONStorable.Type.Bool:
                        aStorable.GetBoolJSONParam(param)?.StoreJSON(storableParams);
                        break;

                    case JSONStorable.Type.String:
                        aStorable.GetStringJSONParam(param)?.StoreJSON(storableParams);
                        break;

                    default:
                        SuperController.LogError("Unhandled type: " + type.ToString());
                        break;
                }
                json["params"] = storableParams;

                aDict[param] = json;
            }
        }


        private void RestoreStorable(JSONStorable aStorable, Dictionary<string, JSONClass> aDict, List<string> aRestoreList = null)
        {
            foreach( var kp in aDict)
            {
                JSONClass json = kp.Value;
                string param = kp.Key;
                if(aRestoreList != null && !aRestoreList.Contains(param))
                {
                    continue;
                }

                JSONStorable.Type type = (JSONStorable.Type)Enum.Parse(typeof(JSONStorable.Type), json["type"].Value);
                JSONClass paramJson = json["params"].AsObject;
                switch( type )
                {
                    case JSONStorable.Type.Color:
                        aStorable.GetColorJSONParam(param)?.RestoreFromJSON(paramJson);
                        break;

                    case JSONStorable.Type.Float:
                        aStorable.GetFloatJSONParam(param)?.RestoreFromJSON(paramJson);
                        break;

                    case JSONStorable.Type.Bool:
                        aStorable.GetBoolJSONParam(param)?.RestoreFromJSON(paramJson);
                        break;

                    case JSONStorable.Type.String:
                        aStorable.GetStringJSONParam(param)?.RestoreFromJSON(paramJson);
                        break;

                    default:
                        SuperController.LogError("Unhandled type: " + type.ToString());
                        break;
                }
            }

        }

        private Dictionary<string, JSONClass> _savedJson;
        private string _style;


        static private List<string> styleList = new List<string>
        {
            "curlX",
            "curlY",
            "curlZ",
            "curlScale",
            "curlFrequency",
            "length1",
            "length2",
            "length3",
            "width"
        };

        static private List<string> colorList = new List<string>
        {
            "rootColor",
            "tipColor",
            "specularColor",
            "diffuseSoftness",
            "primarySpecularSharpness",
            "secondarySpecularSharpness",
            "specularShift",
            "fresnelPower",
            "fresnelAttenuation",
            "randomColorPower",
            "randomColorOffset",
            "IBLFactor"
        };

        static private List<string> physicsList = new List<string>
        {
            "collisionRadius",
            "drag",
            "elasticityOffset",
            "elasticityMultiplier",
            "friction",
            "gravityMultiplier",
            "quickMultiplier",
            "iterations",
            "curveDensity",
            "hairMultiplier"
        };
    }

}