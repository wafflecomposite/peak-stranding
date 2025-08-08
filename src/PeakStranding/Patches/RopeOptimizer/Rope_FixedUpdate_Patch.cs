using HarmonyLib;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(Rope), "FixedUpdate")]
    public static class Rope_FixedUpdate_Patch
    {
        private static bool Prefix(Rope __instance)
        {
            var data = __instance.GetData();
            return !data.IsSleeping;
        }
    }
}