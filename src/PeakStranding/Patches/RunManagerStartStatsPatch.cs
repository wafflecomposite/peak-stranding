using System.Collections;
using System.Threading.Tasks;
using HarmonyLib;
using Peak.Network;
using PeakStranding.Online;
using PeakStranding.UI;
using UnityEngine;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(RunManager), "Start")]
public static class RunManagerStartStatsPatch
{
    private static void Postfix(RunManager __instance)
    {
        __instance.StartCoroutine(ShowStatsWhenAirportIsReady());
    }

    private static IEnumerator ShowStatsWhenAirportIsReady()
    {
        while (!NetCode.Session.InRoom || !Character.localCharacter || LoadingScreenHandler.loading)
        {
            yield return null;
        }

        if (!Character.localCharacter.inAirport)
        {
            yield break;
        }

        Plugin.Log.LogInfo("Airport ready. Fetching PeakStranding stats.");
        yield return FetchAndShowStats();
    }

    private static IEnumerator FetchAndShowStats()
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
            Plugin.Log.LogWarning($"Failed to fetch user stats: {DescribeTaskFailure(userTask)}");
        }
        else if (userTask.IsCanceled)
        {
            Plugin.Log.LogWarning("Failed to fetch user stats: request was cancelled.");
        }

        if (globalTask.Status == TaskStatus.RanToCompletion && globalTask.Result != null)
        {
            var stats = globalTask.Result;
            Plugin.Log.LogInfo($"Global stats: {stats.TotalUniquePlayersAllTime} unique players all-time, {stats.TotalStructuresUploadedAllTime} structures uploaded (last 24h: {stats.TotalStructuresUploadedLast24H} from {stats.TotalUniquePlayersLast24H} players), {stats.TotalLikesGivenAllTime} likes given. Server version {stats.ServerVersion}.");
            message += $"\nGlobal:\ntotal items uploaded: {stats.TotalStructuresUploadedAllTime}\nitems uploaded last day: {stats.TotalStructuresUploadedLast24H}\n" +
                       $"total players: {stats.TotalUniquePlayersAllTime}\nplayers last day: {stats.TotalUniquePlayersLast24H}\ntotal likes given: {stats.TotalLikesGivenAllTime}\nserver version: {stats.ServerVersion}";
        }
        else if (globalTask.IsFaulted)
        {
            var failure = $"Failed to fetch global stats: {DescribeTaskFailure(globalTask)}";
            Plugin.Log.LogWarning(failure);
            color = Color.red;
            message += failure + "\n";
        }
        else if (globalTask.IsCanceled)
        {
            const string failure = "Failed to fetch global stats: request was cancelled.";
            Plugin.Log.LogWarning(failure);
            color = Color.red;
            message += failure + "\n";
        }

        ToastController.Instance.Toast(message, color, 15f, 4f);
    }

    private static string DescribeTaskFailure(Task task)
    {
        return task.Exception?.GetBaseException().Message ?? "Unknown error";
    }
}
