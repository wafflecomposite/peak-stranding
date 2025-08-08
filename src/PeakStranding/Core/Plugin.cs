using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PeakStranding.Data;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakStranding;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin, IOnEventCallback
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> saveStructuresLocallyConfig;
    internal static ConfigEntry<bool> loadLocalStructuresConfig;
    internal static ConfigEntry<int> localStructuresLimitConfig;
    internal static ConfigEntry<bool> sendStructuresToRemoteConfig;
    internal static ConfigEntry<bool> loadRemoteStructuresConfig;
    internal static ConfigEntry<int> remoteStructuresLimitConfig;
    internal static ConfigEntry<string> remoteApiUrlConfig;
    internal static ConfigEntry<bool> showStructureCreditsConfig;
    // internal static ConfigEntry<bool> showToastsConfig;
    internal static ConfigEntry<bool> ropeOptimizerExperimentalConfig;


    public static bool CfgLocalSaveStructures => saveStructuresLocallyConfig.Value;
    public static bool CfgLocalLoadStructures => loadLocalStructuresConfig.Value;
    public static int CfgLocalStructuresLimit => localStructuresLimitConfig.Value;
    public static bool CfgRemoteSaveStructures => sendStructuresToRemoteConfig.Value;
    public static bool CfgRemoteLoadStructures => loadRemoteStructuresConfig.Value;
    public static int CfgRemoteStructuresLimit => remoteStructuresLimitConfig.Value;
    public static string CfgRemoteApiUrl => remoteApiUrlConfig.Value;
    public static bool CfgShowStructureCredits => showStructureCreditsConfig.Value;
    public static bool CfgRopeOptimizerExperimental => ropeOptimizerExperimentalConfig.Value;
    // public static bool CfgShowToasts => showToastsConfig.Value;

    private void Awake()
    {
        Log = Logger;

        // showToastsConfig = Config.Bind("UI", "ShowToasts", true, "Enable or disable toast notifications in the UI.");
        saveStructuresLocallyConfig = Config.Bind("Local", "Save_Structures_Locally", true,
            "Whether to save structures placed in your lobby locally");
        loadLocalStructuresConfig = Config.Bind("Local", "Load_Local_Structures", false,
            "Whether to load previously saved structures at the start of a new run");
        localStructuresLimitConfig = Config.Bind("Local", "Local_Structures_Limit", -1,
            "How many local structures to load at the start of a new run (-1 for no limit). If you have more than this, only the most recent ones will be loaded.");
        sendStructuresToRemoteConfig = Config.Bind("Online", "Send_Structures_To_Online", true,
            "Whether to send structures placed in your lobby to other players");
        loadRemoteStructuresConfig = Config.Bind("Online", "Load_Online_Structures", true,
            "Whether to load random structures placed by other players");
        remoteStructuresLimitConfig = Config.Bind("Online", "Online_Structures_Limit", 30,
            "How many remote structures to load at the start of a new run");
        showStructureCreditsConfig = Config.Bind("UI", "Show_Structure_Credits", true,
            "Whether to show usernames for structures placed by other players in the UI");
        remoteApiUrlConfig = Config.Bind("Online", "Custom_Server_Api_BaseUrl", "", "Custom Server URL. Leave empty to use official Peak Stranding server");
        ropeOptimizerExperimentalConfig = Config.Bind("Experimental", "Experimental_Rope_Optimizer", false,
            "Enable experimental optimizations for the ropes.");

        //if (CfgShowToasts) new GameObject("PeakStranding UI Manager").AddComponent<UIHandler>();

        PhotonNetwork.AddCallbackTarget(this);
        Log.LogInfo($"Plugin {Name} is patching...");
        var harmony = new Harmony("com.github.wafflecomposite.PeakStranding");
        harmony.PatchAll();
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        // Not an instantiation → ignore
        if (photonEvent.Code != 202) return; // 202 is the code for PhotonNetwork.Instantiate
        // Debug.Log($"[PeakStranding] Client instantiated *something*");
        // PUN packs the payload in a Hashtable
        var ev = (ExitGames.Client.Photon.Hashtable)photonEvent.CustomData;

        string prefabName = (string)ev[0];
        Vector3 pos = ev.ContainsKey(1) ? (Vector3)ev[1] : Vector3.zero;
        Quaternion rot = ev.ContainsKey(2) ? (Quaternion)ev[2] : Quaternion.identity;
        object[]? data = ev.ContainsKey(5) ? (object[])ev[5] : null;

        // Skip anything you yourself restored
        if (data?.Length > 0 && data[0] as string == SaveManager.RESTORED_ITEM_MARKER) return;
        var basicSpawnable = new string[]
        {
            "0_Items/ClimbingSpikeHammered",
            "0_Items/ShelfShroomSpawn",
            "0_Items/BounceShroomSpawn",
            "Flag_planted_seagull",
            "Flag_planted_turtle",
            "PortableStovetop_Placed"
        };
        // Debug.Log($"[PeakStranding] It was {prefabName}");
        if (!Array.Exists(basicSpawnable, p => p == prefabName)) return;
        Log.LogInfo($"Player {photonEvent.Sender} placed {prefabName} at {pos}, saving it...");

        var itemData = new PlacedItemData
        {
            PrefabName = prefabName,
            Position = pos,
            Rotation = rot
        };
        itemData.AddCurrentRunContext();
        SaveManager.SaveItem(itemData);
    }


}
