using System;
using BepInEx;
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

    private void Awake()
    {
        Log = Logger;
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
        Log.LogInfo($"[PeakStranding] Player {photonEvent.Sender} placed {prefabName} at {pos}, saving it.");
        SaveManager.AddItemToSave(new PlacedItemData
        {
            PrefabName = prefabName,
            Position = pos,
            Rotation = rot
        });
    }
}
