using HarmonyLib;
using Photon.Pun;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(GameOverHandler), nameof(GameOverHandler.LoadAirportMaster))]
    public static class GameOverHandlerLoadAirportPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogInfo("Run is over, cleaning up all spawned structures and buffered RPCs.");
                SaveManager.CleanupRunStructures();
            }
        }
    }
}