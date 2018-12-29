using UnityEngine;

namespace HSTA
{
    public class FreeRotate : MVRScript
    {
        const float DEADZONE = 0.01f;

        OVRInput.Controller[] _controllers = { OVRInput.Controller.RTouch, OVRInput.Controller.LTouch };
        float _enableNavigationCountdown = 0;

        const float NAVIGATION_DISABLE_TIMER = 0.05f;
        const float DOUBLE_CLICK_TIME = 0.25f;

        private float _doubleClickTimer = 0.0f;
        private bool _bothPressed = false;

        public override void Init()
        {
            try
            {
            }
            catch (System.Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        protected void Update()
        {
            bool didSomething = false;
            // SuperController doesn't give a great API for dealing with Oculus and Vive,
            // but we'll try to use headset-agnostic methods...
            SuperController sc = SuperController.singleton;

            OVRInput.Controller activeController = OVRInput.Controller.None;

            // Find the active controller
            foreach( var controller in _controllers )
            {
                if( OVRInput.IsControllerConnected( controller ) )
                {
                    activeController = controller;
                    break;
                }
            }

            // Couldn't find a controller!
            if( activeController == OVRInput.Controller.None )
            {
                return;
            }
            
            bool btn1 = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, activeController);
            bool btn2 = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, activeController);
            bool btn3 = OVRInput.Get(OVRInput.Button.One, activeController);
            bool btn4 = OVRInput.Get(OVRInput.Button.Two, activeController);
            bool bothPressed = btn1 && btn2;
            bool thumbPressed = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, activeController);
            float pitchVal = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, activeController).y;
            float rollVal = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, activeController).x;
            bool doubleClicked = false;

            // Check for double clicking
            // Both were pressed last update, but not this update
            if (!bothPressed && _bothPressed)
            {
                _doubleClickTimer = DOUBLE_CLICK_TIME;
                _bothPressed = false;
            }
            else if (_bothPressed) // Both pressed last update, still pressed
            {
            }
            else if( bothPressed ) // Both pressed, but weren't last update
            {
                if( _doubleClickTimer > 0.0f )
                { 
                    doubleClicked = true;
                    _doubleClickTimer = 0.0f;
                }
                else
                {
                    _bothPressed = true;
                }
            }
            if( _doubleClickTimer > 0.0f )
            {
                _doubleClickTimer -= Time.unscaledDeltaTime;
            }



            Transform rotateTarget = sc.navigationRigParent;
            if( !bothPressed )
            {
                if ( btn1 )
                {
                    // Rotate

                    if( pitchVal > DEADZONE || pitchVal < -DEADZONE )
                    {
                        Vector3 axis = sc.OVRCenterCamera.transform.right;
                        rotateTarget.Rotate(axis, pitchVal, Space.World);
                        didSomething = true;
                    }
                    if (rollVal > DEADZONE || rollVal < -DEADZONE)
                    {
                        Vector3 axis = sc.OVRCenterCamera.transform.forward;
                        rotateTarget.Rotate(axis, -rollVal, Space.World);
                        didSomething = true;
                    }
                }


                if( btn2 )
                {
                    // Fly
                    if( pitchVal > DEADZONE || pitchVal < -DEADZONE )
                    {
                        Vector3 dir = sc.OVRCenterCamera.transform.forward;
                        dir *= (pitchVal * Time.deltaTime / Time.timeScale);
                        sc.navigationRig.position += dir;
                        didSomething = true;
                    }
                    if (rollVal > DEADZONE || rollVal < -DEADZONE)
                    {
                        Vector3 dir = sc.OVRCenterCamera.transform.right;
                        dir *= (rollVal * Time.deltaTime / Time.timeScale);
                        sc.navigationRig.position += dir;
                        didSomething = true;
                    }
                }
            }
            else if( !doubleClicked ) // Both pressed, no double click
            {
                // Fly up/down
                if (pitchVal > DEADZONE || pitchVal < -DEADZONE)
                {
                    Vector3 dir = sc.OVRCenterCamera.transform.up;
                    dir *= (pitchVal * Time.deltaTime / Time.timeScale);
                    sc.navigationRig.position += dir;
                    didSomething = true;
                }
                if (rollVal > DEADZONE || rollVal < -DEADZONE)
                {
                    // Rotate left/right

                    Vector3 axis = sc.OVRCenterCamera.transform.up;
                    rotateTarget.Rotate(axis, rollVal, Space.World);
                    didSomething = true;
                }
            }
            else // Double clicked
            {
                if( btn3 )
                {
                    if( SuperController.singleton.gameMode == SuperController.GameMode.Play )
                    {
                        SuperController.singleton.gameMode = SuperController.GameMode.Edit;
                    }
                    else
                    {
                        SuperController.singleton.gameMode = SuperController.GameMode.Play;
                    }
                }
                else
                {
                    Vector3 rotation = SuperController.singleton.navigationRig.rotation.eulerAngles;
                    rotation.x = 0;
                    rotation.z = 0;
                    SuperController.singleton.navigationRig.rotation = Quaternion.Euler(rotation);
                }
                didSomething = true;
            }


            // If any action was performed then temporarily disable standard navigation
            if( didSomething )
            {
                sc.disableNavigation = true;
                _enableNavigationCountdown = NAVIGATION_DISABLE_TIMER;
            }
            else if( _enableNavigationCountdown > 0 )
            {
                _enableNavigationCountdown -= Time.deltaTime;
                if( _enableNavigationCountdown <= 0.0f )
                {
                    _enableNavigationCountdown = 0;
                    sc.disableNavigation = false;
                }
            }
        }

    }
}
