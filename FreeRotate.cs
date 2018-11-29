using UnityEngine;

namespace HSTA
{
    public class Fly : MVRScript
    {
        const float DEADZONE = 0.01f;

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
            // SuperController doesn't give a great API for dealing with Oculus and Vive,
            // but we'll try to use headset-agnostic methods...
            SuperController sc = SuperController.singleton;

            // Pitch and roll ... ish. Whatever, it gets the job done.
            JoystickControl.Axis pitchAxis = sc.navigationForwardAxis;
            JoystickControl.Axis rollAxis = sc.navigationSideAxis;

            // Whether or not axis is swapped is a private variable, and Reflection is prohibited, so we can't know to swap

            // Can only query button changes, not value, so we have to watch for up and down
            if( sc.GetRightQuickGrab() )
            {
                _rightBtn1Down = true;
            }
            else if( sc.GetRightQuickRelease() )
            {
                _rightBtn1Down = false;
            }

           if( sc.GetLeftQuickGrab() )
            {
                _leftBtn1Down = true;
            }
            else if( sc.GetLeftQuickRelease() )
            {
                _leftBtn1Down = false;
            }
            

            float pitchVal = JoystickControl.GetAxis(pitchAxis);
            float rollVal = JoystickControl.GetAxis(rollAxis);

            // Second button can only be checked for change to down position, but good enough for reset switch
            bool bothPressed = _rightBtn1Down && sc.GetRightGrabToggle();

            if ( _rightBtn1Down )
            {
                Transform trans = sc.navigationRig;
                Transform around = sc.navigationCamera;//.transform;

                if( pitchVal > DEADZONE || pitchVal < -DEADZONE )
                {
                    trans.RotateAround( around.position, Vector3.right, pitchVal);
                }
                if (rollVal > DEADZONE || rollVal < -DEADZONE)
                {
                    trans.RotateAround( around.position, Vector3.forward, rollVal);
                }
            }


            if( _leftBtn1Down )
            {
                if( pitchVal > DEADZONE || pitchVal < -DEADZONE )
                {
                    Vector3 forward = sc.OVRCenterCamera.transform.forward;
                    forward *= (-pitchVal * 0.5f * Time.deltaTime / Time.timeScale);
                    sc.navigationRig.position += forward;
                }
            }
            
            
            // If both pressed then reset rotation
            if( bothPressed )
            {
                Vector3 rotation = SuperController.singleton.navigationRig.rotation.eulerAngles;
                rotation.x = 0;
                rotation.z = 0;
                SuperController.singleton.navigationRig.rotation = Quaternion.Euler(rotation);
            }
        }

        private bool _rightBtn1Down = false;
        private bool _leftBtn1Down = false;

    }
}
