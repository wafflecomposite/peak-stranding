using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
public class RunManagerStartRunPatch
{
    private static void Postfix()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[PeakStranding] New run started as a CLIENT, items would not be saved or loaded.");
        }
        else
        {
            Debug.Log("[PeakStranding] New run started as a HOST, loading saved items.");
            SaveManager.ClearSessionItems();
            SaveManager.LoadAndSpawnItems();
        }
    }
}
