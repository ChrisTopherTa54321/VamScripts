using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

namespace MVRPlugin {

	// This class will produce a random number that can be used to set any float param available in all atoms
	// includes random generation period, smoothing, and range selection options
	public class FloatParamRandomizer : MVRScript {

		protected void SyncAtomChocies() {
			List<string> atomChoices = new List<string>();
			atomChoices.Add("None");
			foreach (string atomUID in SuperController.singleton.GetAtomUIDs()) {
				atomChoices.Add(atomUID);
			}
			atomJSON.choices = atomChoices;
		}

		// receiver Atom
		protected Atom receivingAtom;
		protected void SyncAtom(string atomUID) {
			List<string> receiverChoices = new List<string>();
			receiverChoices.Add("None");
			if (atomUID != null) {
				receivingAtom = SuperController.singleton.GetAtomByUid(atomUID);
				if (receivingAtom != null) {
					foreach (string receiverChoice in receivingAtom.GetStorableIDs()) {
						receiverChoices.Add(receiverChoice);
						//SuperController.LogMessage("Found receiver " + receiverChoice);
					}
				}
			} else {
				receivingAtom = null;
			}
			receiverJSON.choices = receiverChoices;
			receiverJSON.val = "None";
		}
		protected JSONStorableStringChooser atomJSON;

		protected string _missingReceiverStoreId = "";
		protected void CheckMissingReceiver() {
			if (_missingReceiverStoreId != "" && receivingAtom != null) {
				JSONStorable missingReceiver = receivingAtom.GetStorableByID(_missingReceiverStoreId);
				if (missingReceiver != null) {
					//Debug.Log("Found late-loading receiver " + _missingReceiverStoreId);
					string saveTargetName = _receiverTargetName;
					SyncReceiver(_missingReceiverStoreId);
					_missingReceiverStoreId = "";
					insideRestore = true;
					receiverTargetJSON.val = saveTargetName;
					insideRestore = false;
				}
			}
		}

		// receiver JSONStorable
		protected JSONStorable receiver;
		protected void SyncReceiver(string receiverID) {
			List<string> receiverTargetChoices = new List<string>();
			receiverTargetChoices.Add("None");
			if (receivingAtom != null && receiverID != null) {
				receiver = receivingAtom.GetStorableByID(receiverID);
				if (receiver != null) {
					foreach (string floatParam in receiver.GetFloatParamNames()) {
						receiverTargetChoices.Add(floatParam);
					}
				} else if (receiverID != "None") {
					// some storables can be late loaded, like skin, clothing, hair, etc so must keep track of missing receiver
					//Debug.Log("Missing receiver " + receiverID);
					_missingReceiverStoreId = receiverID;
				}
			} else {
				receiver = null;
			}
			receiverTargetJSON.choices = receiverTargetChoices;
			receiverTargetJSON.val = "None";
		}
		protected JSONStorableStringChooser receiverJSON;

		// receiver target parameter
		protected string _receiverTargetName;
		protected JSONStorableFloat receiverTarget;
		protected void SyncReceiverTarget(string receiverTargetName) {
			_receiverTargetName = receiverTargetName;
			receiverTarget = null;
			if (receiver != null && receiverTargetName != null) {
				receiverTarget = receiver.GetFloatJSONParam(receiverTargetName);
				if (receiverTarget != null) {
					lowerValueJSON.min = receiverTarget.min;
					lowerValueJSON.max = receiverTarget.max;
					upperValueJSON.min = receiverTarget.min;
					upperValueJSON.max = receiverTarget.max;
					currentValueJSON.min = receiverTarget.min;
					currentValueJSON.max = receiverTarget.max;
					targetValueJSON.min = receiverTarget.min;
					targetValueJSON.max = receiverTarget.max;
					if (!insideRestore) {
						// only sync up val if not in restore
						lowerValueJSON.val = receiverTarget.val;
						upperValueJSON.val = receiverTarget.val;
						currentValueJSON.val = receiverTarget.val;
						targetValueJSON.val = receiverTarget.val;
					}
				}
			}
		}
		protected JSONStorableStringChooser receiverTargetJSON;

		protected JSONStorableFloat periodJSON;
		protected JSONStorableFloat quicknessJSON;
		protected JSONStorableFloat lowerValueJSON;
		protected JSONStorableFloat upperValueJSON;
		protected JSONStorableFloat targetValueJSON;
		protected JSONStorableFloat currentValueJSON;

		public override void Init() {
			try {
				// make atom selector
				atomJSON = new JSONStorableStringChooser("atom", SuperController.singleton.GetAtomUIDs(), null, "Atom", SyncAtom);
				RegisterStringChooser(atomJSON);
				SyncAtomChocies();
				UIDynamicPopup dp = CreateScrollablePopup(atomJSON);
				dp.popupPanelHeight = 1100f;
				// want to always resync the atom choices on opening popup since atoms can be added/removed
				dp.popup.onOpenPopupHandlers += SyncAtomChocies;

				// make receiver selector
				receiverJSON = new JSONStorableStringChooser("receiver", null, null, "Receiver", SyncReceiver);
				RegisterStringChooser(receiverJSON);
				dp = CreateScrollablePopup(receiverJSON);
				dp.popupPanelHeight = 960f;

				// make receiver target selector
				receiverTargetJSON = new JSONStorableStringChooser("receiverTarget", null, null, "Target", SyncReceiverTarget);
				RegisterStringChooser(receiverTargetJSON);
				dp = CreateScrollablePopup(receiverTargetJSON);
				dp.popupPanelHeight = 820f;

				// set atom to current atom to initialize
				atomJSON.val = containingAtom.uid;

				// create random value generation period
				periodJSON = new JSONStorableFloat("period", 0.5f, 0f, 10f, false);
				RegisterFloat(periodJSON);
				CreateSlider(periodJSON, true);

				// quickness (smoothness)
				quicknessJSON = new JSONStorableFloat("quickness", 10f, 0f, 100f, true);
				RegisterFloat(quicknessJSON);
				CreateSlider(quicknessJSON, true);

				// lower val
				lowerValueJSON = new JSONStorableFloat("lowerValue", 0f, 0f, 1f, false);
				RegisterFloat(lowerValueJSON);
				CreateSlider(lowerValueJSON, true);

				// upper val
				upperValueJSON = new JSONStorableFloat("upperValue", 0f, 0f, 1f, false);
				RegisterFloat(upperValueJSON);
				CreateSlider(upperValueJSON, true);

				// target val
				targetValueJSON = new JSONStorableFloat("targetValue", 0f, 0f, 1f, false, false);
				// don't register - this is for viewing only and is generated
				UIDynamicSlider ds = CreateSlider(targetValueJSON, true);
				ds.defaultButtonEnabled = false;
				ds.quickButtonsEnabled = false;

				// current val
				currentValueJSON = new JSONStorableFloat("currentValue", 0f, 0f, 1f, false, false);
				// don't register - this is for viewing only and is generated
				ds = CreateSlider(currentValueJSON, true);
				ds.defaultButtonEnabled = false;
				ds.quickButtonsEnabled = false;

			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		protected float timer = 0f;

		protected void Update() {
			try {
				timer -= Time.deltaTime;
				if (timer < 0.0f) {
					// reset timer and set a new random target value
					timer = periodJSON.val;
					targetValueJSON.val = UnityEngine.Random.Range(lowerValueJSON.val, upperValueJSON.val);
				}
				currentValueJSON.val = Mathf.Lerp(currentValueJSON.val, targetValueJSON.val, Time.deltaTime * quicknessJSON.val);
				// check for receivers that might have been missing on load due to asynchronous load of some assets like skin, clothing, hair
				CheckMissingReceiver();
				if (receiverTarget != null) {
					receiverTarget.val = currentValueJSON.val;
				}
			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}


	}
}