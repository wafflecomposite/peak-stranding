using HarmonyLib;
using UnityEngine;
using PeakStranding;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
public class RunManagerStartRunPatch
{
    private static void Postfix()
    {
        Debug.Log("[ItemPersistence] New run started. Loading saved items.");
        SaveManager.ClearSessionItems();
        SaveManager.LoadAndSpawnItems();
    }
}
