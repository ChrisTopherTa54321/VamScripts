using UnityEngine;

namespace HSTA
{
    public class FreeRotate : MVRScript
    {
        const float DEADZONE = 0.01f;

        OVRInput.Controller[] _controllers = { OVRInput.Controller.RTouch, OVRInput.Controller.LTouch };
        float _enableNavigationCountdown = 0;
        const float NAVIGATION_DISABLE_TIMER = .25f;

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
            bool clicked = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, activeController);
            float pitchVal = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, activeController).y;
            float rollVal = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, activeController).x;

            if ( btn1 )
            {
                Transform target = sc.navigationRigParent;
                //Transform target = sc.navigationRig.transform;
                Vector3 rotation = target.rotation.eulerAngles;

                if( pitchVal > DEADZONE || pitchVal < -DEADZONE )
                {
                    Vector3 axis = sc.OVRCenterCamera.transform.right;
                    target.Rotate(axis, pitchVal, Space.World);
                    didSomething = true;
                }
                if (rollVal > DEADZONE || rollVal < -DEADZONE)
                {
                    Vector3 axis = sc.OVRCenterCamera.transform.forward;
                    target.Rotate(axis, rollVal, Space.World);
                    didSomething = true;
                }
            }


            if( btn2 )
            {
                if( clicked )
                {
                    if (pitchVal > DEADZONE || pitchVal < -DEADZONE)
                    {
                        Vector3 dir = sc.OVRCenterCamera.transform.up;
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
                else
                {
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


            // If both pressed then reset rotation
            if( btn1 && btn2 )
            {
                Vector3 rotation = SuperController.singleton.navigationRig.rotation.eulerAngles;
                rotation.x = 0;
                rotation.z = 0;
                SuperController.singleton.navigationRig.rotation = Quaternion.Euler(rotation);
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
