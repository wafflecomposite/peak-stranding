using System.Collections;
using HarmonyLib;
using PeakStranding.Data;
using PeakStranding.Online;
using PeakStranding.Components;
using Photon.Pun;
using UnityEngine;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
public class RunManagerStartRunPatch
{
    private static void Postfix(RunManager __instance)
    {
        var sync = __instance.GetComponent<PeakStrandingSyncManager>();
        if (sync == null)
        {
            sync = __instance.gameObject.AddComponent<PeakStrandingSyncManager>();
        }
        sync.ResetRunLikes();

        if (!PhotonNetwork.IsMasterClient)
        {
            Plugin.Log.LogInfo("New run started as a CLIENT, structures will be synced by the host.");
            return;
        }

        Plugin.Log.LogInfo("New run started as a HOST. Caching structures.");

        SaveManager.ClearCache(); // Clear data from any previous run

        if (Plugin.CfgLocalLoadStructures)
        {
            Plugin.Log.LogInfo("Caching local structures from save.");
            SaveManager.CacheLocalStructures();
        }
        else
        {
            Plugin.Log.LogInfo("Caching local structures is disabled, skipping.");
        }

        if (Plugin.CfgRemoteLoadStructures && DataHelper.GetCurrentSceneName() != "Airport")
        {
            Plugin.Log.LogInfo("Fetching and caching remote structures from server.");
            __instance.StartCoroutine(FetchCacheAndSpawnInitial(DataHelper.GetCurrentLevelIndex()));
        }
        else
        {
            Plugin.Log.LogInfo("Remote structures disabled or in Airport. Spawning initial local structures.");
            SaveManager.SpawnStructuresForSegment(0); // Spawn for segment 0
        }
    }


    private static IEnumerator FetchCacheAndSpawnInitial(int mapId)
    {
        var task = RemoteApi.FetchStructuresAsync(mapId);
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Plugin.Log.LogError($"Failed to fetch remote structures: {task.Exception}");
        }
        else
        {
            SaveManager.CacheRemoteStructures(task.Result);
        }

        Plugin.Log.LogInfo("Initial caching complete. Spawning structures for segment 0.");
        SaveManager.SpawnStructuresForSegment(0);
    }
}
