using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace HSTA
{
    using JsonDict = Dictionary<string, JSONClass>;
    public class HairStylist : MVRScript
    {
        public static string pluginName = "HairStylist";
        public static string pluginVersion = "V0.8.0+";
        public static string saveExt = "hair";

        public override void Init()
        {
            try
            {
                if (containingAtom.type != "Person")
                {
                    SuperController.LogError("Use this plugin on a Person only");
                    return;
                }

                // Create preset directory
                _lastBrowseDir = CreateDirectory(GetPluginPath() + @"hair_presets\" );

                pluginLabelJSON.val = pluginName + " " + pluginVersion;

                // Add Load/Save buttons
                var btn = CreateButton("Load Preset");
                btn.button.onClick.AddListener(() =>
                {
                    _undoLoadPreset = HairStyle.CreateFromPerson(containingAtom);
                    SuperController.singleton.NormalizeMediaPath(_lastBrowseDir); // This sets the path iff it exists
                    SuperController.singleton.GetMediaPathDialog(HandleLoadPreset, saveExt);
                });

                btn = CreateButton("Load Pre-Load Preset");
                btn.button.onClick.AddListener(() =>
                {
                    _undoLoadPreset?.ApplyToPerson(containingAtom, _loadColor.val, _loadStyle.val, _loadPhysics.val);
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
                        _undoHairSettings = HairStyle.CreateFromPerson(containingAtom);
                        SuperController.singleton.SetCustomUI(ui);
                    }
                });
                btn = CreateButton("Load Pre-Open Hair Settings", true);
                btn.button.onClick.AddListener(() =>
                {
                    _undoHairSettings?.ApplyToPerson(containingAtom);
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

        string CreateDirectory( string aPath )
        {
            JSONNode node = new JSONNode();
            if( !( aPath.EndsWith( "/" ) || aPath.EndsWith( @"\" ) ) )
            {
                aPath += @"\";
            }

            try
            {
                node.SaveToFile(aPath);
            }
            catch( Exception e )
            {
            }
            return aPath;
        }

        void HandleSavePreset(string aPath)
        {
            if (String.IsNullOrEmpty(aPath))
            {
                return;
            }
            _lastBrowseDir = aPath.Substring(0, aPath.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";

            if (!aPath.ToLower().EndsWith("." + saveExt.ToLower()))
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
        protected HairStyle _undoHairSettings;
        protected HairStyle _original;
        protected HairStyle _undoLoadPreset;
        protected string _lastBrowseDir;

    }


    public class HairStyle
    {
        public static HairStyle CreateFromPerson( Atom aPerson )
        {
            HairStyle newHair = new HairStyle();
            DAZCharacterSelector character = aPerson?.GetStorableByID("geometry") as DAZCharacterSelector;

            // Find relevant storables
            JSONStorable moveContainer = FindStorableByName(aPerson, "ScalpContainer");
            JSONStorable hairSettings = FindStorableByName(aPerson, "HairSettings");
            JSONStorable scalps = FindStorableByName( aPerson, "Scalps");
            JSONStorable styles = FindStorableByName(aPerson, "Styles");

            // Get Style and Scalp choices
            string scalpName = scalps.GetStringChooserParamValue("choiceName");
            string styleName = styles.GetStringChooserParamValue("choiceName");

            // Scalp store name is based on scalp name, so find the correct storable based on it
            JSONStorable scalpStorable = null;
            if (scalpName != "NoScalp")
            {
                scalpStorable = FindStorableByName(aPerson, scalpName.Replace("Scalp", "HairScalp"));
            }

            List<JSONStorable> saveList = new List<JSONStorable>();
            saveList.Add(styles);
            saveList.Add(scalps);
            saveList.Add(moveContainer);
            saveList.Add(hairSettings);
            if( scalpStorable != null )
            {
                saveList.Add(scalpStorable);
            }


            // Create save
            JsonDict newSaveStorable;
            foreach( var storable in saveList )
            {
                string saveName = storable.name;
                // Scalp storable name changes depending on scalp, so we'll rename it to a constant name
                if( storable == scalpStorable )
                {
                    saveName = "scalpParams";
                }
                newHair.SaveStorable(storable, out newSaveStorable);
                newHair._savedJson[saveName] = newSaveStorable;
            }

            return newHair;
        }


        public static HairStyle CreateFromSavedJson(JSONNode aJson)
        {
            HairStyle newHair = null;
            if (aJson["savedByPlugin"].Value == HairStylist.pluginName)
            {
                newHair = new HairStyle();

                foreach (JSONClass storableNode in aJson["storables"].AsArray)
                {
                    string saveGroup = storableNode["storableName"];
                    JsonDict groupDict = new JsonDict();
                    foreach( JSONClass paramNode in storableNode["params"].AsArray)
                    {
                        groupDict.Add(paramNode["id"], paramNode.AsObject);
                    }
                    newHair._savedJson[saveGroup] = groupDict;
                }
            }
            else
            {
                SuperController.LogError("Saved file was not created by HairStylist");
            }
            return newHair;
        }

        public JSONClass GetSaveJson()
        {
            JSONClass saveJson = new JSONClass();
            saveJson["savedByPlugin"] = HairStylist.pluginName;
            saveJson["savedByVer"] = HairStylist.pluginVersion;
            var storables = new JSONArray();
            foreach (var saveGroup in _savedJson)
            {
                JSONClass newNode = new JSONClass();
                string groupName = saveGroup.Key;
                newNode["storableName"] = groupName;

                var newGroup = new JSONArray();
                foreach (var kp in saveGroup.Value)
                {
                    newGroup.Add(kp.Key, kp.Value);
                }
                newNode["params"] = newGroup;
                storables.Add(newNode);
            }
            saveJson["storables"] = storables;
            return saveJson;
        }


        public void ApplyToPerson( Atom aPerson, bool aColor = true, bool aStyle = true, bool aPhysics = true )
        {
            DAZCharacterSelector character = aPerson.GetStorableByID("geometry") as DAZCharacterSelector;
            List<string> hairSettingsRestoreList = new List<string>();
            List<string> restoreStorablesList = new List<string>();

            HairStyle origStyle = null;
            // Colors are stored per-style. If we change styles, but not color, the color will change.
            // So, if we are changing style but not color, we need to store off the current color and
            // restore it after the style change to ensure the color doesn't actually change.
            if( aStyle && !aColor )
            {
                origStyle = HairStyle.CreateFromPerson(aPerson);
            }

            JSONStorable scalps = FindStorableByName(aPerson, "Scalps");
            JSONStorable styles = FindStorableByName(aPerson, "Styles");

            // Apply any style changes so we are sure to apply modifications to the correct style
            if ( aStyle )
            {
                RestoreStorable(scalps, _savedJson["Scalps"]);
                RestoreStorable(styles, _savedJson["Styles"]);
            }

            // Now that any style change is done, let's get the rest of the storables
            JSONStorable moveContainer = FindStorableByName(aPerson, "ScalpContainer");
            JSONStorable hairSettings = FindStorableByName(aPerson, "HairSettings");

            // Get Style and Scalp choices
            string scalpName = scalps.GetStringChooserParamValue("choiceName");
            JSONStorable scalpStorable = null;
            if( scalpName != "NoScalp" )
            {
                scalpStorable = FindStorableByName(aPerson, scalpName.Replace("Scalp", "HairScalp"));
            }

            // HairSettings contains color, style and physics, so has a special-case filter list
            if (aColor)
            {
                restoreStorablesList.AddRange(colorLoadList);
                hairSettingsRestoreList.AddRange(simColorList);
            }
            if (aStyle)
            {
                restoreStorablesList.AddRange(styleLoadList);
                hairSettingsRestoreList.AddRange(simStyleList);
                // Scalp style has already been restored, if applicable.
            }
            if( aPhysics )
            {
                restoreStorablesList.AddRange(physicsLoadList);
                hairSettingsRestoreList.AddRange(simPhysicsList);
            }

            foreach (var saveGroup in _savedJson)
            {
                List<string> restoreList = null;
                string groupName = saveGroup.Key;
                if( !restoreStorablesList.Contains(groupName))
                {
                    continue;
                }

                // Hair Settings contains all parameters for sim hair so must be filtered
                if( groupName == "HairSettings")
                {
                    restoreList = hairSettingsRestoreList;
                }

                // Scalp params are stored per-style, but saved as scalpParams. Adjust the name to the current style.
                if( groupName == "scalpParams" )
                {
                    if( null != scalpStorable )
                    {
                        groupName = scalpStorable.name;
                    }
                    else
                    {
                        continue; // Can't save scalp parameters when there is no scalp
                    }
                }

                // Restore the setting!
                var storable = FindStorableByName(aPerson, groupName);
                RestoreStorable(storable, saveGroup.Value, restoreList);
            }

            // If we switched styles without loading color, then copy over the color from the last style
            origStyle?.ApplyToPerson(aPerson, true, false, false);
        }

        private void SaveStorable( JSONStorable aStorable, out JsonDict aDict )
        {
            aDict = new JsonDict();
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

                    case JSONStorable.Type.StringChooser:
                        aStorable.GetStringChooserJSONParam(param)?.StoreJSON(storableParams);
                        break;

                    default:
                        SuperController.LogError("Unhandled type: " + type.ToString());
                        break;
                }
                json["params"] = storableParams;

                aDict[param] = json;
            }
        }


        private void RestoreStorable(JSONStorable aStorable, JsonDict aDict, List<string> aRestoreList = null)
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

                    case JSONStorable.Type.StringChooser:
                        aStorable.GetStringChooserJSONParam(param)?.RestoreFromJSON(paramJson);
                        break;

                    default:
                        SuperController.LogError("Unhandled type: " + type.ToString());
                        break;
                }
            }
        }


        static JSONStorable FindStorableByName( Atom aAtom, string aName )
        {
            JSONStorable ret = null;
            foreach (var storable in aAtom.GetComponentsInChildren<JSONStorable>())
            {
                if( storable.name == aName )
                {
                    ret = storable;
                    break;
                }
            }

            if( null == ret )
            {
                SuperController.LogError("Couldn't find storable named " + aName);
            }
            return ret;
        }

        Dictionary<string, JsonDict> _savedJson = new Dictionary<string, JsonDict>();

        static private List<string> styleLoadList = new List<string>()
        {
            "Styles",
            "Scalps",
            "ScalpContainer",
            "HairSettings",
            "scalpParams",
        };

        static private List<string> colorLoadList = new List<string>()
        {
            "HairSettings",
            "scalpParams",
        };

        static private List<string> physicsLoadList = new List<string>()
        {
            "HairSettings",
        };

        static private List<string> simStyleList = new List<string>
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

        static private List<string> simColorList = new List<string>
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

        static private List<string> simPhysicsList = new List<string>
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