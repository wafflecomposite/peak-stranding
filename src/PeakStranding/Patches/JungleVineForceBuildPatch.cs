using HarmonyLib;
using UnityEngine;
using PeakStranding.Data;
using Photon.Pun;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(JungleVine), nameof(JungleVine.ForceBuildVine_RPC))]
public class JungleVineForceBuildPatch
{
    private static void Postfix(Vector3 from, Vector3 to, float hang, Vector3 mid)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (SaveManager.IsRestoring) return;
        var itemData = new PlacedItemData
        {
            PrefabName = "PeakStranding/JungleVine",
            RopeStart = from,
            RopeEnd = to,
            RopeLength = hang,
            RopeFlyingRotation = mid
        };
        itemData.AddCurrentRunContext();
        SaveManager.SaveItem(itemData);
    }
}
