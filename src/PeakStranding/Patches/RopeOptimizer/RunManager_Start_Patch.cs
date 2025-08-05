using HarmonyLib;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(RunManager), "Start")]
    public static class RunManager_Start_Patch
    {
        private static void Postfix()
        {
            RopeSleepManager.Initialize();
        }
    }
}