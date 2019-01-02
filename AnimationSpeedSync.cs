using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HSTA
{

    public class AnimationSpeedSync : MVRScript
    {
        const string pluginText = "V1.0.0";

        public override void Init()
        {
            try
            {
                _animationPattern = containingAtom.GetStorableByID("AnimationPattern") as AnimationPattern;
                if (null == _animationPattern )
                {
                    SuperController.LogError("Use this plugin on a AnimationPattern only");
                    return;
                }
                _speedJSON = _animationPattern.GetFloatJSONParam("speed");
                _enabled = new JSONStorableBool("Sync animation speed", true);
                CreateToggle(_enabled);
            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

 
        protected void Update()
        {
            try
            {
                if( _animationPattern != null && _enabled.val )
                {
                    _speedJSON.val = SuperController.singleton.motionAnimationMaster.playbackSpeed;
                }
            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }


        JSONStorableBool _enabled;
        JSONStorableFloat _speedJSON;
        AnimationPattern _animationPattern;

    }

}