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
        public string pluginVersion = "1.1+OVR";
        public string pluginDate = "01/11" + "/2019";
        public string pluginDescription = @"This plugin plays animation for handy asset when the trigger is pressed.
        You are free to edit/change everything below this, please do not delete the PluginInfo. 
        Append it if you modify the script

        01/13/2019 - updated open/close toggle option to remove delay.  added slider option
        01/13/2019 - HsThrowAway5 - Oculus support
        ";

        #endregion

        #region Vars
        protected Animator hL;
        protected Animator hR;
        protected VrController LC;
        protected VrController RC;
        protected JSONStorableBool _swapTriggers;
        protected const string animName = "newHands";

        protected UIDynamicSlider Lslider;
        protected UIDynamicSlider Rslider;
        #endregion

        public override void Init() {
            try {
                //SuperController.LogMessage("");

                // get controllers
                if (!SuperController.singleton.isOVR)
                {
                    GameObject Rctrl = GameObject.Find("Controller (right)");
                    if (Rctrl != null)
                    {
                        RC = new SteamVRController(Rctrl);
                    }

                    GameObject Lctrl = GameObject.Find("Controller (left)");
                    if (Lctrl != null)
                    {
                        LC = new SteamVRController(Lctrl);
                    }
                }
                else
                {
                    // Triggers will be made in 'Start' so that saved swapTriggers will have been loaded
                    _swapTriggers = new JSONStorableBool("swap triggers", false, (bool aSwap) =>
                    {
                        OVRInput.Axis1D axis;
                        if (!aSwap)
                        {
                            axis = OVRInput.Axis1D.PrimaryIndexTrigger;
                        }
                        else
                        {
                            axis = OVRInput.Axis1D.PrimaryHandTrigger;
                        }
                        LC = new OculusController(OVRInput.Controller.LTouch, axis);
                        RC = new OculusController(OVRInput.Controller.RTouch, axis);
                    });
                    RegisterBool(_swapTriggers);
                    CreateToggle(_swapTriggers);
                }

                //  find hand assets
                hL = GameObject.Find("left_hand(Clone)")?.GetComponent<Animator>();
                if (hL == null) { hL = GameObject.Find("left_hand_alpha(Clone)")?.GetComponent<Animator>(); }
                hR = GameObject.Find("right_hand(Clone)")?.GetComponent<Animator>();
                if (hR == null) { hR = GameObject.Find("right_hand_alpha(Clone)")?.GetComponent<Animator>(); }

                // show in label if one or both hands are found
                if (hL != null) {
                    pluginLabelJSON.val = "+Left Hand ";
                }
                if (hR != null)
                {
                    pluginLabelJSON.val += "+Right Hand ";
                }

                pluginLabelJSON.val += " [" + pluginVersion + "]";
                
                JSONStorableBool hlClose = new JSONStorableBool("close left hand", false, closeHL);
                RegisterBool(hlClose);
                CreateToggle(hlClose);

                JSONStorableFloat Lval = new JSONStorableFloat("left hand", 0f, LhandVal, 0f, 1f, true);
                RegisterFloat(Lval);
                Lslider = CreateSlider(Lval);

                JSONStorableBool hrClose = new JSONStorableBool("close right hand", false, closeHR);
                RegisterBool(hrClose);
                CreateToggle(hrClose);

                JSONStorableFloat Rval = new JSONStorableFloat("right hand", 0f, RhandVal, 0f, 1f, true);
                RegisterFloat(Rval);
                Lslider = CreateSlider(Rval);

            }
            catch (Exception e) {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        void Start()
        {
            // Cause the controllers to be created with saved swapTrigger value
            if( _swapTriggers != null )
            {
                _swapTriggers.val = !_swapTriggers.val;
                _swapTriggers.val = !_swapTriggers.val;
            }
        }

        // Update is called with each rendered frame by Unity
        void Update() {
            try
            {
                if (LC != null && hL != null ) {
                    float axis = LC.GetTrigger();
                    // Check to see that trigger is press & hand is present.
                    if (axis > 0.01f)
                    {    //  play hand animation based on trigger position
                        hL.speed = axis;
                        hL.Play(animName, 0, axis);
                        hL.speed = 0;
                    }
                }
                if (RC != null && hR != null ){
                    float axis = RC.GetTrigger();
                    if (axis > 0.01f)
                    {                    
                        hR.speed = axis;
                        hR.Play(animName, 0, axis);
                        hR.speed = 0;
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
                { hL.Play(animName, 0, 1f); }
                else
                { hL.Play(animName, 0, 0); }
                hL.speed = 0;
            }
        }

        protected void closeHR(bool e)
        {
            if (hR != null)
            {
                if (e == true)
                { hR.Play(animName, 0, 1f); }
                else
                { hR.Play(animName, 0, 0); }
                hR.speed = 0;
            }
        }

        protected void LhandVal(JSONStorableFloat lh)
        {
            if (hL != null)
            {
                hL.Play(animName, 0, lh.val);
                hL.speed = 0;
            }
        }

        protected void RhandVal(JSONStorableFloat rh)
        {
            if (hR != null)
            {
                hR.Play(animName, 0, rh.val);
                hR.speed = 0;
            }
        }
        #endregion

        #region Controller Abstractions

        protected interface VrController
        {
            float GetTrigger();
        }

        class OculusController : VrController
        {
            public OculusController( OVRInput.Controller aController, OVRInput.Axis1D aAxis = OVRInput.Axis1D.PrimaryHandTrigger )
            {
                _controller = aController;
                _axis = aAxis;
            }

            public float GetTrigger()
            {
                return OVRInput.Get(_axis, _controller);
            }

            OVRInput.Controller _controller;
            OVRInput.Axis1D _axis;
        }

        class SteamVRController : VrController
        {
            public SteamVRController(GameObject aCtrl)
            {
                SteamVR_TrackedObject trackedObj = aCtrl.GetComponent<SteamVR_TrackedObject>();
                _controller = SteamVR_Controller.Input((int)trackedObj.index);
            }

            public float GetTrigger()
            {
                return  _controller.GetAxis(Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger).x;
            }

            SteamVR_Controller.Device _controller;
        }

        #endregion

    }
}