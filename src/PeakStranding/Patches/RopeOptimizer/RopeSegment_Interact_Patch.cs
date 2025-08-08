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
                // Wake the rope when a segment is interacted with. The sleep
                // timer is managed on the master via RPC, so no additional
                // local state changes are required here.
                __instance.rope.WakeUp();
            }
        }
    }
}