using System.Collections;
using HarmonyLib;
using PeakStranding.Data;
using PeakStranding.Online;
using Photon.Pun;
using UnityEngine;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
public class RunManagerStartRunPatch
{
    private static void Postfix(RunManager __instance)
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

            int mapId = GameHandler.GetService<NextLevelService>()
                               .Data.Value.CurrentLevelIndex;

            // Kick off coroutine on the RunManager MonoBehaviour
            __instance.StartCoroutine(FetchAndSpawn(mapId));
        }
    }

    private static IEnumerator FetchAndSpawn(int mapId)
    {
        var task = RemoteApi.FetchAmbientAsync(mapId);

        // Wait until the async HTTP finishes (or fails)
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
            yield break;

        foreach (PlacedItemData item in task.Result)
            SaveManager.SpawnItem(item);
    }
}
