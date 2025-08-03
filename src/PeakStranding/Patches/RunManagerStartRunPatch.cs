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
            Plugin.Log.LogInfo("New run started as a CLIENT, items would not be saved or loaded");
        }
        else
        {
            Plugin.Log.LogInfo("New run started as a HOST");
            SaveManager.ClearSessionItems();
            if (Plugin.CfgLocalLoadStructures)
            {
                Plugin.Log.LogInfo("Loading local structures from save");
                SaveManager.LoadLocalStructures();
            }
            else
            {
                Plugin.Log.LogInfo("Loading local structures is disabled, skipping");
            }

            if (Plugin.CfgRemoteLoadStructures)
            {
                if (DataHelper.GetCurrentSceneName() == "Airport")
                {
                    Plugin.Log.LogInfo("Ain't gonna load remote structures in the airport :P");
                    return;
                }
                Plugin.Log.LogInfo("Loading remote structures from server");
                __instance.StartCoroutine(FetchAndSpawn(DataHelper.GetCurrentLevelIndex()));
            }
            else
            {
                Plugin.Log.LogInfo("Loading remote structures is disabled, skipping");
            }
        }
    }

    private static IEnumerator FetchAndSpawn(int mapId)
    {
        var task = RemoteApi.FetchStructuresAsync(mapId);

        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
            yield break;

        SaveManager.LoadRemoteStructures(task.Result);
    }
}
