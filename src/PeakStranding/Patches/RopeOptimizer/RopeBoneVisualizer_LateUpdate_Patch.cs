using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(RopeBoneVisualizer), "LateUpdate")]
    public static class RopeBoneVisualizer_LateUpdate_Patch
    {
        /// <summary>
        /// This prefix patch prevents the RopeBoneVisualizer's LateUpdate from running
        /// if the associated rope is in a sleep state. This fixes the "Look rotation viewing vector is zero"
        /// error spam and provides an additional performance optimization.
        /// </summary>
        private static bool Prefix(RopeBoneVisualizer __instance)
        {
            // clients seems to have a problem with that?
            if (PhotonNetwork.IsMasterClient && Plugin.CfgRopeOptimizerExperimental)
            {
                // Access the private 'rope' field from the visualizer instance.
                var rope = (Rope)AccessTools.Field(typeof(RopeBoneVisualizer), "rope").GetValue(__instance);

                // If the rope exists and is sleeping, skip the original LateUpdate method.
                if (rope != null && rope.GetData().IsSleeping)
                {
                    return false; // Returning false skips the original method.
                }

                // Otherwise, allow the original LateUpdate method to run as normal.
                return true;
            }
            else return true;

        }
    }
}