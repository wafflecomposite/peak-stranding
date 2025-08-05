using HarmonyLib;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(RopeSyncer), nameof(RopeSyncer.ShouldSendData))]
    public static class RopeSyncer_ShouldSendData_Patch
    {
        private static bool Prefix(RopeSyncer __instance, ref bool __result)
        {
            var rope = (Rope)AccessTools.Field(typeof(RopeSyncer), "rope").GetValue(__instance);
            if (rope != null && rope.GetData().IsSleeping)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}