using HarmonyLib;
using UnityEngine;
using PeakStranding;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(JungleVine), nameof(JungleVine.ForceBuildVine_RPC))]
public class JungleVineForceBuildPatch
{
    private static void Postfix(Vector3 from, Vector3 to, float hang, Vector3 mid)
    {
        if (SaveManager.IsRestoring) return;
        var itemData = new PlacedItemData
        {
            PrefabName = "PeakStranding/JungleVine",
            from = from,
            to = to,
            hang = hang,
            mid = mid
        };
        SaveManager.AddItemToSave(itemData);
    }
}
