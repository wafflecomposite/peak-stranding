using System.Collections;
using System.Threading.Tasks;
using HarmonyLib;
using PeakStranding.Data;
using PeakStranding.Online;
using PeakStranding.Components;
using Photon.Pun;
using UnityEngine;
using PeakStranding.UI;

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
            __instance.StartCoroutine(FetchAndLogStats());
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
            ToastController.Instance.Toast($"PeakStranding: failed to fetch online items!\n{task.Exception}", Color.red, 5f, 3f);
        }
        else
        {
            SaveManager.CacheRemoteStructures(task.Result);
        }

        Plugin.Log.LogInfo("Initial caching complete. Spawning structures for segment 0.");
        SaveManager.SpawnStructuresForSegment(0);
    }


    private static IEnumerator FetchAndLogStats()
    {
        var globalTask = RemoteApi.FetchGlobalStatsAsync();
        var userTask = RemoteApi.FetchUserStatsAsync();

        yield return new WaitUntil(() => globalTask.IsCompleted && userTask.IsCompleted);

        var message = "PeakStranding Stats:\n\n";
        var color = Color.green;


        if (userTask.Status == TaskStatus.RanToCompletion && userTask.Result != null)
        {
            var stats = userTask.Result;
            Plugin.Log.LogInfo($"Your stats: {stats.TotalStructuresUploaded} structures uploaded (last 24h: {stats.StructuresUploadedLast24H}), {stats.TotalLikesReceived} likes received, {stats.TotalLikesSent} likes sent.");
            message += $"You:\nitems uploaded total: {stats.TotalStructuresUploaded}\nitems uploaded last day: {stats.StructuresUploadedLast24H}\n" +
            $"likes received: {stats.TotalLikesReceived}\nlikes sent: {stats.TotalLikesSent}\n";

        }
        else if (userTask.IsFaulted)
        {
            var str = $"Failed to fetch user stats: {DescribeTaskFailure(userTask)}";
            Plugin.Log.LogWarning(str);
            //color = Color.red;
            //message += str + "\n";
        }
        else if (userTask.IsCanceled)
        {
            var str = "Failed to fetch user stats: request was cancelled.";
            Plugin.Log.LogWarning(str);
            //color = Color.red;
            //message += str + "\n";
        }

        if (globalTask.Status == TaskStatus.RanToCompletion && globalTask.Result != null)
        {
            var stats = globalTask.Result;
            Plugin.Log.LogInfo($"Global stats: {stats.TotalUniquePlayersAllTime} unique players all-time, {stats.TotalStructuresUploadedAllTime} structures uploaded (last 24h: {stats.TotalStructuresUploadedLast24H} from {stats.TotalUniquePlayersLast24H} players), {stats.TotalLikesGivenAllTime} likes given. Server version {stats.ServerVersion}.");
            message += $"\nGlobal:\ntotal items uploaded: {stats.TotalStructuresUploadedAllTime}\nitems uploaded last day: {stats.TotalStructuresUploadedLast24H}\n"+
            $"total players: {stats.TotalUniquePlayersAllTime}\nplayers last day: {stats.TotalUniquePlayersLast24H}\ntotal likes given: {stats.TotalLikesGivenAllTime}\nserver version: {stats.ServerVersion}";
        }
        else if (globalTask.IsFaulted)
        {
            var str = $"Failed to fetch global stats: {DescribeTaskFailure(globalTask)}";
            Plugin.Log.LogWarning(str);
            color = Color.red;
            message += str + "\n";
        }
        else if (globalTask.IsCanceled)
        {
            var str = "Failed to fetch global stats: request was cancelled.";
            Plugin.Log.LogWarning(str);
            color = Color.red;
            message += str + "\n";
        }

        ToastController.Instance.Toast(message, color, 15f, 4f);
    }


    private static string DescribeTaskFailure(Task task)
    {
        return task.Exception?.GetBaseException().Message ?? "Unknown error";
    }
}
