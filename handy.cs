using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

namespace DillDoe {
	public class Handy : MVRScript {

        #region PluginInfo    
        public string pluginAuthor = "DillDoe";
        public string pluginName = "Handy";
        public string pluginVersion = "1.0";
        public string pluginDate = "01/11" + "/2019";
        public string pluginDescription = @"This plugin plays animation for handy asset when the trigger is pressed.
        You are free to edit/change everything below this, please do not delete the PluginInfo. 
        Append it if you modify the script";
        #endregion

        #region Vars
        protected GameObject hL;
        protected GameObject hR;
        protected SteamVR_Controller.Device LC;
        protected SteamVR_Controller.Device RC;
        protected const string animName = "newHands";
        #endregion
    
        public override void Init() {
			try {
                //SuperController.LogMessage();
                // get controllers
                GameObject Rctrl = GameObject.Find("Controller (right)");
                if (Rctrl != null)
                {
                    SteamVR_TrackedObject Ro = Rctrl.GetComponent<SteamVR_TrackedObject>();
                    RC = SteamVR_Controller.Input((int)Ro.index);
                }
                GameObject Lctrl = GameObject.Find("Controller (left)");
                if (Lctrl != null)
                {
                    SteamVR_TrackedObject Lo = Lctrl.GetComponent<SteamVR_TrackedObject>();
                    LC = SteamVR_Controller.Input((int)Lo.index);
                }

                //  find hand assets
                hL = GameObject.Find("left_hand(Clone)");
                if (hL == null) { hL = GameObject.Find("left_hand_alpha(Clone)"); }
                hR = GameObject.Find("right_hand(Clone)");
                if (hR == null) { hR = GameObject.Find("right_hand_alpha(Clone)"); }

                // show in label if one or both hands are found
                if (hL != null) {
                    pluginLabelJSON.val = "+Left Hand ";
                }
                if (hR != null)
                {
                    pluginLabelJSON.val += "+Right Hand ";
                }
                
                JSONStorableBool hlClose = new JSONStorableBool("close left hand", false, closeHL);
                RegisterBool(hlClose);
                CreateToggle(hlClose);

                JSONStorableBool hrClose = new JSONStorableBool("close right hand", false, closeHR);
                RegisterBool(hrClose);
                CreateToggle(hrClose);

            }
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		// Update is called with each rendered frame by Unity
		void Update() {
            try
            {
                if (LC != null) { 
                    // Check to see that trigger is press & hand is present.
                    if (LC.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x > 0.01f)
                    {   
                        if (hL != null)
                        {   //  play hand animation based on trigger position
                            hL.GetComponent<Animator>().speed = LC.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x;
                        hL.GetComponent<Animator>().Play(animName, 0, LC.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x);
                            hL.GetComponent<Animator>().speed = 0;
                        }
                    }
                }
                if (RC != null){
                    if (RC.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x > 0.01f)
                    {                    
                        if (hR != null)
                        {                    
                            hR.GetComponent<Animator>().speed = RC.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x;
                            hR.GetComponent<Animator>().Play(animName, 0, RC.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x);
                            hR.GetComponent<Animator>().speed = 0;
                        }
                    }
                }
            }
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

        #region HandToggles
        //  toggle close/open hands
        protected void closeHL(bool e)
        {
            if (hL != null)
            {
                if (e == true)
                {                    
                    hL.GetComponent<Animator>().Play(animName, 0, 1f);
                    hL.GetComponent<Animator>().speed=0;
                }
                else
                {
                    hL.GetComponent<Animator>().speed = 1;
                }
            }
        }

        protected void closeHR(bool e)
        {
            if (hR != null)
            {
                if (e == true)
                {
                    hR.GetComponent<Animator>().Play(animName, 0, 1f);
                    hR.GetComponent<Animator>().speed = 0;
                }
                else
                {
                    hR.GetComponent<Animator>().speed = 1;
                }
            }
        }
        #endregion
    }
}