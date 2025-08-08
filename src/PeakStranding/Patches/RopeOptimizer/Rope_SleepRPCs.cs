using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

namespace PeakStranding.Patches
{
    public class Rope_SleepRPCs : MonoBehaviourPun
    {
        public Rope rope;

        [PunRPC]
        private void EnterSleepState_RPC()
        {
            if (!Plugin.CfgRopeOptimizerExperimental) return;
            var data = rope.GetData();
            data.IsSleeping = true;
            data.SleepCountdown = -1f;
            data.WasBeingClimbed = false;

            var segments = (List<Transform>)AccessTools.Field(typeof(Rope), "simulationSegments").GetValue(rope);
            if (photonView.IsMine)
            {
                foreach (Transform segmentTransform in segments)
                {
                    Rigidbody rb = segmentTransform.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;
                }
            }

            Debug.Log($"Rope {rope.photonView.ViewID} is now sleeping.");
        }

        [PunRPC]
        private void ExitSleepState_RPC()
        {
            if (!Plugin.CfgRopeOptimizerExperimental) return;
            var data = rope.GetData();
            data.IsSleeping = false;
            data.SleepCountdown = Rope_ExtendedData.SleepDelay;
            data.WasBeingClimbed = false;

            var segments = (List<Transform>)AccessTools.Field(typeof(Rope), "simulationSegments").GetValue(rope);
            if (photonView.IsMine)
            {
                foreach (Transform segmentTransform in segments)
                {
                    Rigidbody rb = segmentTransform.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.WakeUp();
                    }
                }
            }

            Debug.Log($"Rope {rope.photonView.ViewID} has woken up.");
        }
    }
}