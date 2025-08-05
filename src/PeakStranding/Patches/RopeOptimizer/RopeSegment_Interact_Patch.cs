using HarmonyLib;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(RopeSegment), nameof(RopeSegment.Interact))]
    public static class RopeSegment_Interact_Patch
    {
        private static void Prefix(RopeSegment __instance)
        {
            if (__instance.rope != null)
            {
                __instance.rope.WakeUp();
                if (__instance.rope.photonView.IsMine)
                {
                    var data = __instance.rope.GetData();
                    data.SleepCountdown = -1f;
                }
            }
        }
    }
}