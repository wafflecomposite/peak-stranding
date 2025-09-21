using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PeakStranding.Data;
using PeakStranding.UI;
using PeakStranding.Components;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakStranding;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin, IOnEventCallback
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> saveStructuresLocallyConfig = null!;
    internal static ConfigEntry<bool> loadLocalStructuresConfig = null!;
    internal static ConfigEntry<int> localStructuresLimitConfig = null!;
    internal static ConfigEntry<bool> sendStructuresToRemoteConfig = null!;
    internal static ConfigEntry<bool> loadRemoteStructuresConfig = null!;
    internal static ConfigEntry<int> remoteStructuresLimitConfig = null!;
    internal static ConfigEntry<string> remoteApiUrlConfig = null!;
    internal static ConfigEntry<bool> showStructureOverlayConfig = null!;
    internal static ConfigEntry<bool> showToastsConfig = null!;
    internal static ConfigEntry<bool> ropeOptimizerExperimentalConfig = null!;
    internal static ConfigEntry<string> structureAllowListConfig = null!;
    internal static ConfigEntry<bool> allowClientsLikeConfig = null!;
    internal static ConfigEntry<bool> allowClientsDeleteConfig = null!;

    internal static ConfigEntry<KeyCode> overlayLikeKeyConfig = null!;
    internal static ConfigEntry<KeyCode> overlayRemoveKeyConfig = null!;


    public static bool CfgLocalSaveStructures => saveStructuresLocallyConfig.Value;
    public static bool CfgLocalLoadStructures => loadLocalStructuresConfig.Value;
    public static int CfgLocalStructuresLimit => localStructuresLimitConfig.Value;
    public static bool CfgRemoteSaveStructures => sendStructuresToRemoteConfig.Value;
    public static bool CfgRemoteLoadStructures => loadRemoteStructuresConfig.Value;
    public static int CfgRemoteStructuresLimit => remoteStructuresLimitConfig.Value;
    public static string CfgRemoteApiUrl => remoteApiUrlConfig.Value;
    public static bool CfgShowStructureOverlay => showStructureOverlayConfig.Value;
    public static bool CfgRopeOptimizerExperimental => ropeOptimizerExperimentalConfig.Value;
    public static string CfgStructureAllowList => structureAllowListConfig.Value;
    public static bool CfgAllowClientLike => allowClientsLikeConfig.Value;
    public static bool CfgAllowClientDelete => allowClientsDeleteConfig.Value;
    public static bool CfgShowToasts => showToastsConfig.Value;

    private void Awake()
    {
        Log = Logger;

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
        remoteStructuresLimitConfig = Config.Bind("Online", "Online_Structures_Limit", 40,
            "How many remote structures to load at the start of a new run");
        showStructureOverlayConfig = Config.Bind("UI", "Show_Structure_Overlay", true,
            "Whether to show the overlay for structures placed by other players in the world");
        showToastsConfig = Config.Bind("UI", "Show_Toasts", true, "Enable or disable toast notifications in the UI.");
        structureAllowListConfig = Config.Bind("Online", "Structure_Allow_List", string.Join(" ", DataHelper.prefabMapping.GetAllSeconds()),
            "A space-separated list of structure prefab names that are allowed to be placed by other players. Leave empty to allow all structures.");
        ropeOptimizerExperimentalConfig = Config.Bind("Experimental", "Experimental_Rope_Optimizer", true,
            "Enable experimental optimizations for the ropes.");
        remoteApiUrlConfig = Config.Bind("Online", "Custom_Server_Api_BaseUrl", "", "Custom Server URL. Leave empty to use official Peak Stranding server");
        allowClientsLikeConfig = Config.Bind("Online", "Allow_Clients_Like", true, "Allow clients to like structures.");
        allowClientsDeleteConfig = Config.Bind("Online", "Allow_Clients_Delete", true, "Allow clients to delete structures.");

        overlayLikeKeyConfig = Config.Bind("Controls", "Overlay_Like_Key", KeyCode.L, "Key used to like structures from the overlay.");
        overlayRemoveKeyConfig = Config.Bind("Controls", "Overlay_Remove_Key", KeyCode.Delete, "Key used to remove structures from the overlay.");

        OverlayManager.LikeShortcut = overlayLikeKeyConfig.Value;
        OverlayManager.RemoveShortcut = overlayRemoveKeyConfig.Value;
        overlayLikeKeyConfig.SettingChanged += (_, _) => OverlayManager.LikeShortcut = overlayLikeKeyConfig.Value;
        overlayRemoveKeyConfig.SettingChanged += (_, _) => OverlayManager.RemoveShortcut = overlayRemoveKeyConfig.Value;

        PhotonNetwork.AddCallbackTarget(this);
        Log.LogInfo($"Plugin {Name} is patching...");
        var harmony = new Harmony("com.github.wafflecomposite.PeakStranding");
        harmony.PatchAll();
        Log.LogInfo($"Plugin {Name} is loaded!");

        if (ToastController.Instance == null)
            new GameObject("PeakStranding UI Toasts").AddComponent<ToastController>();

        // check if any of the structures in allow list are not in the mapping
        var readablePrefabNames = DataHelper.prefabMapping.GetAllSeconds();
        if (!string.IsNullOrWhiteSpace(CfgStructureAllowList))
        {
            var allowedPrefabs = CfgStructureAllowList.ToLower().Split([' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var prefab in allowedPrefabs)
            {
                if (!readablePrefabNames.Contains(prefab))
                {
                    Log.LogWarning($"Unrecognized prefab name in allow list: '{prefab}'. Please check your structure allow list.");
                }
            }
        }
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
            "PortableStovetop_Placed",
            "ScoutCannon_Placed",
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
