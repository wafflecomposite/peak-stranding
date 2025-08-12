using HarmonyLib;
using Photon.Pun;
using PeakStranding.Components;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(GameOverHandler), nameof(GameOverHandler.LoadAirportMaster))]
    public static class GameOverHandlerLoadAirportPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            PeakStrandingSyncManager.DestroyInstance();

            if (PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogInfo("Run is over, cleaning up all spawned structures and buffered RPCs.");
                SaveManager.CleanupRunStructures();
            }
        }
    }
}