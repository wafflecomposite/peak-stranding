using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakStranding;

// Here are some basic resources on code style and naming conventions to help
// you in your first CSharp plugin!
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces

// This BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin, IOnEventCallback
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        // BepInEx gives us a logger which we can use to log information.
        // See https://lethal.wiki/dev/fundamentals/logging
        Log = Logger;

        PhotonNetwork.AddCallbackTarget(this);

        // BepInEx also gives us a config file for easy configuration.
        // See https://lethal.wiki/dev/intermediate/custom-configs

        // We can apply our hooks here.
        // See https://lethal.wiki/dev/fundamentals/patching-code

        // Log our awake here so we can see it in LogOutput.log file
        Log.LogInfo($"Plugin {Name} is patching...");

        var harmony = new Harmony("com.github.wafflecomposite.peak-stranding");
        harmony.PatchAll();

        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // ----------  Network callback ----------
    public void OnEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        // Only the host needs to save things
        if (!PhotonNetwork.IsMasterClient) return;
        // Not an instantiation → ignore
        if (photonEvent.Code != 202) return; // 202 is the code for PhotonNetwork.Instantiate
        Debug.Log($"[ItemPersistence] Client instantiated *something*");
        // PUN packs the payload in a Hashtable
        var ev = (ExitGames.Client.Photon.Hashtable)photonEvent.CustomData;

        string prefabName = (string)ev[(byte)0];  // prefab path/name
        Vector3 pos = ev.ContainsKey((byte)1) ? (Vector3)ev[(byte)1] : Vector3.zero;
        Quaternion rot = ev.ContainsKey((byte)2) ? (Quaternion)ev[(byte)2] : Quaternion.identity;
        object[]? data = ev.ContainsKey((byte)5) ? (object[])ev[(byte)5] : null;

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
        Debug.Log($"[ItemPersistence] It was {prefabName}");
        if (!Array.Exists(basicSpawnable, p => p == prefabName)) return;

        Log.LogInfo($"[ItemPersistence] Player {photonEvent.Sender} placed {prefabName} at {pos}, gotta save it!");

        SaveManager.AddItemToSave(new PlacedItemData
        {
            PrefabName = prefabName,   // or prefab if you prefer full path
            Position = pos,
            Rotation = rot
        });
    }
}
