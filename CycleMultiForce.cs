using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

// original CycleForce by MeshedVr
// Multi-angle mod by physis
// Minor tweaks by hsthrowaway5

namespace HSTA
{

    // this class is a bit like the built-in CycleForceProducer but can only attach to rigidbodies within the containing atom
    // and force/torque is relative to the body itself by choosing apply axis rather than world axis
    public class CycleForce : MVRScript
    {

        protected Rigidbody RB;
        protected void SyncReceiver(string receiver)
        {
            if (receiver != null)
            {
                ForceReceiver fr;
                if (receiverNameToForceReceiver.TryGetValue(receiver, out fr))
                {
                    RB = fr.GetComponent<Rigidbody>();
                    pluginLabelJSON.val = receiver;
                }
                else
                {
                    RB = null;
                }
            }
            else
            {
                RB = null;
            }
        }
        protected JSONStorableStringChooser receiverChoiceJSON;

        protected JSONStorableFloat periodJSON;
        protected JSONStorableFloat periodRatioJSON;
        protected JSONStorableFloat forceDurationJSON;
        protected JSONStorableFloat forceFactorJSON;
        protected JSONStorableFloat forceQuicknessJSON;
        protected JSONStorableFloat torqueFactorJSON;
        protected JSONStorableFloat torqueQuicknessJSON;
        protected JSONStorableBool applyForceOnReturnJSON;

        protected JSONStorableFloat forceDirectionXJSON;
        protected JSONStorableFloat forceDirectionYJSON;
        protected JSONStorableFloat forceDirectionZJSON;

        protected JSONStorableFloat torqueDirectionXJSON;
        protected JSONStorableFloat torqueDirectionYJSON;
        protected JSONStorableFloat torqueDirectionZJSON;

        protected List<string> receiverChoices;
        protected Dictionary<string, ForceReceiver> receiverNameToForceReceiver;

        public override void Init()
        {
            try
            {
                receiverChoices = new List<string>();
                receiverNameToForceReceiver = new Dictionary<string, ForceReceiver>();
                foreach (ForceReceiver fr in containingAtom.forceReceivers)
                {
                    receiverChoices.Add(fr.name);
                    receiverNameToForceReceiver.Add(fr.name, fr);
                }
                receiverChoiceJSON = new JSONStorableStringChooser("receiver", receiverChoices, null, "Receiver", SyncReceiver);
                receiverChoiceJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterStringChooser(receiverChoiceJSON);
                UIDynamicPopup dp = CreateScrollablePopup(receiverChoiceJSON);
                dp.popupPanelHeight = 1100f;
                dp.popup.alwaysOpen = true;

                forceDirectionXJSON = new JSONStorableFloat("Force direction X", 0f, -1f, 1f, false, true);
                forceDirectionXJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(forceDirectionXJSON);
                CreateSlider(forceDirectionXJSON, true);

                forceDirectionYJSON = new JSONStorableFloat("Force direction Y", 0f, -1f, 1f, false, true);
                forceDirectionYJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(forceDirectionYJSON);
                CreateSlider(forceDirectionYJSON, true);

                forceDirectionZJSON = new JSONStorableFloat("Force direction Z", 0f, -1f, 1f, false, true);
                forceDirectionZJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(forceDirectionZJSON);
                CreateSlider(forceDirectionZJSON, true);

                torqueDirectionXJSON = new JSONStorableFloat("Torque direction X", 0f, -1f, 1f, false, true);
                torqueDirectionXJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(torqueDirectionXJSON);
                CreateSlider(torqueDirectionXJSON, true);

                torqueDirectionYJSON = new JSONStorableFloat("Torque direction Y", 0f, -1f, 1f, false, true);
                torqueDirectionYJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(torqueDirectionYJSON);
                CreateSlider(torqueDirectionYJSON, true);

                torqueDirectionZJSON = new JSONStorableFloat("Torque direction Z", 0f, -1f, 1f, false, true);
                torqueDirectionZJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(torqueDirectionZJSON);
                CreateSlider(torqueDirectionZJSON, true);

                periodJSON = new JSONStorableFloat("period", 0.5f, 0f, 10f, false);
                periodJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(periodJSON);
                CreateSlider(periodJSON, true);

                periodRatioJSON = new JSONStorableFloat("periodRatio", 0.5f, 0f, 1f, true);
                periodRatioJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(periodRatioJSON);
                CreateSlider(periodRatioJSON, true);

                forceDurationJSON = new JSONStorableFloat("forceDuration", 1f, 0f, 1f, true);
                forceDurationJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(forceDurationJSON);
                CreateSlider(forceDurationJSON, true);

                forceFactorJSON = new JSONStorableFloat("forceFactor", 0f, 0f, 1000f, false, true);
                forceFactorJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(forceFactorJSON);
                CreateSlider(forceFactorJSON, true);

                forceQuicknessJSON = new JSONStorableFloat("forceQuickness", 10f, 0f, 50f, false, true);
                forceQuicknessJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(forceQuicknessJSON);
                CreateSlider(forceQuicknessJSON, true);

                torqueFactorJSON = new JSONStorableFloat("torqueFactor", 5f, 0f, 100f, false, true);
                torqueFactorJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(torqueFactorJSON);
                CreateSlider(torqueFactorJSON, true);

                torqueQuicknessJSON = new JSONStorableFloat("torqueQuickness", 10f, 0f, 50f, false, true);
                torqueQuicknessJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterFloat(torqueQuicknessJSON);
                CreateSlider(torqueQuicknessJSON, true);

                applyForceOnReturnJSON = new JSONStorableBool("applyForceOnReturn", true);
                applyForceOnReturnJSON.storeType = JSONStorableParam.StoreType.Full;
                RegisterBool(applyForceOnReturnJSON);
                CreateToggle(applyForceOnReturnJSON, true);


            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        protected float timer;
        protected float forceTimer;
        protected float flip;
        protected Vector3 targetForce;
        protected Vector3 targetTorque;
        protected Vector3 currentForce;
        protected Vector3 currentTorque;

        protected void Start()
        {
            timer = 0f;
            forceTimer = 0f;
            flip = 1f;
        }

        protected void SetTargets(float percent)
        {
            Vector3 forceDirection;
            forceDirection.x = forceDirectionXJSON.val;
            forceDirection.y = forceDirectionYJSON.val;
            forceDirection.z = forceDirectionZJSON.val;

            Vector3 torqueDirection;
            torqueDirection.x = torqueDirectionXJSON.val;
            torqueDirection.y = torqueDirectionYJSON.val;
            torqueDirection.z = torqueDirectionZJSON.val;

            targetForce = forceDirection * percent * forceFactorJSON.val;
            targetTorque = torqueDirection * percent * torqueFactorJSON.val;
        }

        // Use Update for the timers since this can account for time scale
        protected void Update()
        {
            timer -= Time.deltaTime;
            forceTimer -= Time.deltaTime;
            if (timer < 0.0f)
            {
                if ((flip > 0f && periodRatioJSON.val != 1f) || periodRatioJSON.val == 0f)
                {
                    if (applyForceOnReturnJSON.val)
                    {
                        flip = -1f;
                    }
                    else
                    {
                        flip = 0f;
                    }
                    timer = periodJSON.val * (1f - periodRatioJSON.val);
                    forceTimer = forceDurationJSON.val * periodJSON.val;
                }
                else
                {
                    flip = 1f;
                    timer = periodJSON.val * periodRatioJSON.val;
                    forceTimer = forceDurationJSON.val * periodJSON.val;
                }
                SetTargets(flip);
            }
            else if (forceTimer < 0.0f)
            {
                SetTargets(0f);
            }
        }

        // FixedUpdate is called with each physics simulation frame by Unity
        void FixedUpdate()
        {
            try
            {
                // apply forces here
                float timeFactor = Time.fixedDeltaTime;
                currentForce = Vector3.Lerp(currentForce, targetForce, timeFactor * forceQuicknessJSON.val);
                currentTorque = Vector3.Lerp(currentTorque, targetTorque, timeFactor * torqueQuicknessJSON.val);
                if (RB && (!SuperController.singleton || !SuperController.singleton.freezeAnimation))
                {
                    RB.AddForce(currentForce, ForceMode.Force);
                    RB.AddTorque(currentTorque, ForceMode.Force);
                }
            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

    }
}