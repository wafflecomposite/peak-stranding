using HarmonyLib;
using UnityEngine;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(Rope), "Awake")]
    public static class Rope_Awake_Patch
    {
        private static void Postfix(Rope __instance)
        {
            if (!Plugin.CfgRopeOptimizerExperimental) return;
            var rpcHandler = __instance.gameObject.AddComponent<Rope_SleepRPCs>();
            rpcHandler.rope = __instance;
        }
    }
}