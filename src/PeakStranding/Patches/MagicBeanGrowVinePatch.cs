using HarmonyLib;
using UnityEngine;
using System.Runtime.CompilerServices;
using PeakStranding.Data;
using Photon.Pun;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(MagicBeanVine), nameof(MagicBeanVine.RPC_GrowVine))]
public static class MagicBeanGrowVinePatch
{
    private static readonly ConditionalWeakTable<MagicBeanVine, object> saved = new();
    private static readonly object s_token = new object();

    private static void Postfix(MagicBeanVine __instance, float length)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (SaveManager.IsRestoring) return;
        if (__instance.GetComponent<RestoredItem>() != null) return;
        if (saved.TryGetValue(__instance, out _)) return;
        saved.Add(__instance, s_token);

        var itemData = new PlacedItemData
        {
            PrefabName = "PeakStranding/MagicBeanVine",
            Position = __instance.transform.position,
            // Keep the legacy wire format: Rotation.forward stores the vine's up direction.
            Rotation = Quaternion.FromToRotation(Vector3.forward, __instance.transform.up),
            RopeLength = length
        };
        itemData.AddCurrentRunContext();
        SaveManager.SaveItem(itemData);
    }
}
