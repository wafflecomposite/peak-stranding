using HarmonyLib;
using Photon.Pun;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(Rope), "Update")]
    public static class Rope_Update_Patch
    {
        private static void Postfix(Rope __instance)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                Rope_ExtendedData.UpdateSleepLogic(__instance);
            }
        }
    }
}