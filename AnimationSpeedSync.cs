using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HSTA
{

    public class AnimationSpeedSync : MVRScript
    {
        const string pluginName = "AnimationSpeedSync";
        const string pluginVersion = "V1.0.0+";
        const string pluginAuthor = "hsthrowaway5";

        const float UPDATE_RATE_MS = 0.02f;
        
        public override void Init()
        {
            try
            {
                _animationPattern = containingAtom.GetStorableByID("AnimationPattern") as AnimationPattern;
                if (null == _animationPattern )
                {
                    _audioSource = containingAtom.GetStorableByID("AudioSource") as AudioSourceControl;
                    if( null == _audioSource )
                    {

                        pluginLabelJSON.val = String.Format("Error - Use on Animation Pattern or AudioSource only");
                        SuperController.LogError("Use this plugin on a AnimationPattern or AudioSource only");
                        return;
                    }
                }
                _speedJSON = _animationPattern?.GetFloatJSONParam("speed");
                _pitchJSON = _audioSource?.GetFloatJSONParam("pitch");
                _enabled = new JSONStorableBool("Sync animation speed", true);
                CreateToggle(_enabled);

                pluginLabelJSON.val = String.Format("{0} {1}, by {2}", pluginName, pluginVersion, pluginAuthor);
            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        protected void Start()
        {
            if (_animationPattern || _audioSource)
            {
                StartRoutine();
            }
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
            if (null == _coroutine)
            {
                _coroutine = StartCoroutine(Update_Routine());
            }
        }

        private void StopRoutines()
        {
            if (null != _coroutine)
            {
                StopCoroutine(_coroutine);
                _coroutine = null;
            }
        }


        protected IEnumerator Update_Routine()
        {

            while (true)
            {
                float waitTime = UPDATE_RATE_MS;
                if (waitTime > 0.0f)
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
                    if (_enabled.val)
                    {
                        if (_animationPattern)
                        {
                            HandleAnimationPattern();
                        }
                        if (_audioSource)
                        {
                            HandleAudioSource();
                        }
                    }
                }
                catch (Exception e)
                {
                    SuperController.LogError(e.ToString());
                }
            }
        }

        protected void HandleAnimationPattern()
        {
            _speedJSON.val = SuperController.singleton.motionAnimationMaster.playbackSpeed;
            bool gamePaused = SuperController.singleton.freezeAnimation;
            if (!_paused && gamePaused)
            {
                _animationPattern.Pause();
                _paused = true;
            }
            else if (_paused && !gamePaused)
            {
                _animationPattern.UnPause();
                _paused = false;
            }
        }

        protected void HandleAudioSource()
        {
            float newPitch = TimeControl.singleton.currentScale * SuperController.singleton.motionAnimationMaster.playbackSpeed;
            _pitchJSON.val = newPitch;
            bool gamePaused = SuperController.singleton.freezeAnimation;
            if (!_paused && gamePaused)
            {
                _audioSource.Pause();
                _paused = true;
            }
            else if (_paused && !gamePaused)
            {
                _audioSource.UnPause();
                _paused = false;
            }
        }

        JSONStorableBool _enabled;
        JSONStorableFloat _speedJSON;
        JSONStorableFloat _pitchJSON;
        bool _paused;
        AnimationPattern _animationPattern;
        AudioSourceControl _audioSource;
        protected Coroutine _coroutine = null;

    }

}