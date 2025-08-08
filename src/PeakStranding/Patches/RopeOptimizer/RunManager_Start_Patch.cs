using HarmonyLib;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(RunManager), "Start")]
    public static class RunManager_Start_Patch
    {
        private static void Postfix()
        {
            if (!Plugin.CfgRopeOptimizerExperimental) { return; }
            Plugin.Log.LogInfo("Experimental Rope Optimizer enabled! Enjoy the performance boost. Looking forward to your feedback!");
            RopeSleepManager.Initialize();
        }
    }
}