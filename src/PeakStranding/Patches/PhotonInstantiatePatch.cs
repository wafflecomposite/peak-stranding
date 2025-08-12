using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using System;
using PeakStranding.Data;
using UnityEngine.SceneManagement;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.Instantiate))]
public class PhotonInstantiatePatch
{
    private static void Postfix(string prefabName, Vector3 position, Quaternion rotation, byte group, object[]? data = null)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (SaveManager.IsRestoring) return;
        if (data?.Length > 0 && data[0] as string == SaveManager.RESTORED_ITEM_MARKER) return;
        // Debug.Log($"[PhotonNetwork.Instantiate] Prefab: '{prefabName}', Position: {position}, Rotation: {rotation}, Group: {group}");

        string[] basicSpawnable =
        {
            "0_Items/ClimbingSpikeHammered",
            "0_Items/ShelfShroomSpawn",
            "0_Items/BounceShroomSpawn",
            "Flag_planted_seagull",
            "Flag_planted_turtle",
            "PortableStovetop_Placed",
            "ScoutCannon_Placed"
        };

        if (Array.Exists(basicSpawnable, p => p == prefabName))
        {
            var itemData = new PlacedItemData
            {
                PrefabName = prefabName,
                Position = position,
                Rotation = rotation,
            };
            itemData.AddCurrentRunContext();
            SaveManager.SaveItem(itemData);
            return;
        }
    }
}
