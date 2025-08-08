using HarmonyLib;
using UnityEngine;


namespace PeakStranding.Patches
{
    // RopeSyncer_ShouldSendData_Patch.cs
    [HarmonyPatch(typeof(RopeSyncer), nameof(RopeSyncer.ShouldSendData))]
    public static class RopeSyncer_ShouldSendData_Patch
    {
        private static bool Prefix(RopeSyncer __instance, ref bool __result)
        {
            var rope = (Rope)AccessTools.Field(typeof(RopeSyncer), "rope").GetValue(__instance);
            if (rope == null) return true;

            var data = rope.GetData();

            // Awake → let the original logic decide (we want full-rate while moving/being used)
            if (!data.IsSleeping) return true;

            // Sleeping → send a heartbeat at most once per configured interval
            float now = Time.unscaledTime;
            if (now >= data.NextSleepSyncTime)
            {
                data.NextSleepSyncTime = now + 1.0f;
                __result = true;   // allow this frame's snapshot
            }
            else
            {
                __result = false;  // skip sending this frame
            }
            return false; // we set __result
        }
    }

}
