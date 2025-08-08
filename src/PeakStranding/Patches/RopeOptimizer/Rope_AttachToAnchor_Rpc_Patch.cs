using HarmonyLib;
using Photon.Pun;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(Rope), nameof(Rope.AttachToAnchor_Rpc))]
    public static class Rope_AttachToAnchor_Rpc_Patch
    {
        private static void Postfix(Rope __instance)
        {
            if (PhotonNetwork.IsMasterClient && Plugin.CfgRopeOptimizerExperimental)
            {
                var data = __instance.GetData();
                data.SleepCountdown = Rope_ExtendedData.SleepDelay;
            }
        }
    }
}