using UnityEngine;
using System.Collections.Generic;

namespace HSTA
{
    public class HotKeys : MVRScript
    {

        private delegate void ActionFunction();
        private delegate void ChangeScaleFunction(float aMult);
        private Dictionary<string, ActionFunction> _shortcuts;

        struct Range
        {
            public Range( float aStep, float aMin, float aMax )
            {
                step = aStep;
                min = aMin;
                max = aMax;
            }
            public float step;
            public float min;
            public float max;
        };


        // **** MODIFY THESE TO TWEAK PARAMETERS ****
        // Ranges for each modifiable variable. Order is 'step size', 'minimum value', 'maximum value'
        Range _worldScale = new Range(0.00025f, 0.01f, 10.0f);
        Range _timeScale = new Range(0.01f, 0.01f, 1.0f);
        Range _animationSpeed = new Range(0.05f, -3.0f, 5.0f);

        const float SHIFT_MULTIPLIER = 5.0f; // Multiplier to apply to step size if holding down shift

        const float REPEAT_HOLD_DELAY = 0.5f; // How long do you have to hold a key before it begins repeating
        const float REPEAT_RATE_DELAY = 0.100f; // What is the delay between key repeats
        private string _lastKeyPressed;
        private float _keyRepeatTimestamp;

        // List of functions to scroll through for controlling with scale func
        private List<ChangeScaleFunction> _changeScaleFuncs;
        private List<ChangeScaleFunction> _setScaleFuncs;
        private int _scaleFuncsIdx = 0;

        const float WORLD_SCALE_HEIGHT_STEP = .0001f;
        const float WORLD_SCALE_HEIGHT_STEP_DEFAULT = .0011f;
        float _ws_height_mult = WORLD_SCALE_HEIGHT_STEP_DEFAULT;

        public override void Init()
        {
            try
            {
                _changeScaleFuncs = new List<ChangeScaleFunction>() { ChangeTimeScale, ChangeAnimationSpeed };
                _setScaleFuncs = new List<ChangeScaleFunction>() { SetTimeScale, SetAnimationSpeed };

                // **** CHANGE KEYS HERE!
                _shortcuts = new Dictionary<string, ActionFunction>()
                {
                    {
                        "u",
                        () => ChangeWorldScale( -1.0f )
                    },
                    {
                        "i",
                        () => ChangeWorldScale( 1.0f )
                    },
                    {
                        "return",
                        () => TogglePause()
                    },
                    {
                        "p",
                        () => _scaleFuncsIdx = ( _scaleFuncsIdx + 1 ) % _changeScaleFuncs.Count
                    },
                    {
                        "up",
                        () => _changeScaleFuncs[_scaleFuncsIdx]( 1.0f )
                    },
                    {
                        "down",
                        () => _changeScaleFuncs[_scaleFuncsIdx]( -1.0f )
                    },
                    {
                        "right",
                        () => _setScaleFuncs[_scaleFuncsIdx]( 0.0f )
                    },
                    {
                        "left",
                        () => _setScaleFuncs[_scaleFuncsIdx]( 0.2f )
                    },
                    {
                        "l",
                        () => _setScaleFuncs[_scaleFuncsIdx]( 1.0f )
                    },
                    {
                        "j",
                        () => _ws_height_mult += WORLD_SCALE_HEIGHT_STEP
                    },
                    {
                        "k",
                        () => _ws_height_mult -= WORLD_SCALE_HEIGHT_STEP
                    },
                    {
                        "h",
                        () => _ws_height_mult = ( _ws_height_mult == 0.0f ) ? WORLD_SCALE_HEIGHT_STEP_DEFAULT : 0.0f
                              
                    }


                };
            }
            catch (System.Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }
        

        protected void Update()
        {
            // If no keys are pressed then just exit out
            if( !Input.anyKey )
            {
                return;
            }
            foreach (KeyValuePair<string, ActionFunction> shortcut in _shortcuts)
            {
                if (shortcut.Key.Length > 0 && Input.GetKeyDown(shortcut.Key))
                {
                    // Handle the keypress and set the repeat delay timer
                    shortcut.Value();
                    _lastKeyPressed = shortcut.Key;
                    _keyRepeatTimestamp = Time.unscaledTime + REPEAT_HOLD_DELAY;
                    break;
                }
            }

            // Check repeat if still holding down last key
            if (Input.GetKey(_lastKeyPressed) )
            {
                // Still holding down the key... is it time for a repeat?
                if (Time.unscaledTime >= _keyRepeatTimestamp)
                {
                    _keyRepeatTimestamp = Time.unscaledTime + REPEAT_RATE_DELAY;
                    _shortcuts[_lastKeyPressed]();
                }
            }
            else
            {
                // Not still holding down the key? Clear last key
                _lastKeyPressed = "";
            }

        }


        private void ChangeWorldScale(float aStepMult)
        {
            SuperController sc = SuperController.singleton;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                aStepMult *= SHIFT_MULTIPLIER;
            }
            SetWorldScale(sc.worldScale + _worldScale.step * aStepMult );


            // Modify player height with scale
            Vector3 dir = Vector3.down;
            dir *= aStepMult * _ws_height_mult;
            sc.navigationRig.position += dir;
        }


        private void SetWorldScale(float aScale)
        {
            if (aScale > _worldScale.max)
            {
                aScale = _worldScale.max;
            }
            else if (aScale < _worldScale.min)
            {
                aScale = _worldScale.min;
            }
            SuperController.singleton.worldScale = aScale;
        }


        private void ChangeTimeScale(float aStepMult)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                aStepMult *= SHIFT_MULTIPLIER;
            }
            SetTimeScale(TimeControl.singleton.currentScale + _timeScale.step * aStepMult);
        }


        private void SetTimeScale(float aSpeed)
        {
            if (aSpeed > _timeScale.max)
            {
                aSpeed = _timeScale.max;
            }
            else if (aSpeed < _timeScale.min)
            {
                aSpeed = _timeScale.min;
            }
            TimeControl.singleton.currentScale = aSpeed;
        }


        private void ChangeAnimationSpeed(float aStepMult)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                aStepMult *= SHIFT_MULTIPLIER;
            }
            SetAnimationSpeed(SuperController.singleton.motionAnimationMaster.playbackSpeed + _animationSpeed.step * aStepMult);
        }


        private void SetAnimationSpeed(float aSpeed)
        {
            if (aSpeed > _animationSpeed.max)
            {
                aSpeed = _animationSpeed.max;
            }
            else if (aSpeed < _animationSpeed.min)
            {
                aSpeed = _animationSpeed.min;
            }
            SuperController.singleton.motionAnimationMaster.playbackSpeed = aSpeed;
        }

        private void TogglePause()
        {
            SuperController.singleton.SetFreezeAnimation(!SuperController.singleton.freezeAnimation);
        }
        
    }
}
