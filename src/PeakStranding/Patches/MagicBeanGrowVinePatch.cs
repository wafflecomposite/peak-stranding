using HarmonyLib;
using UnityEngine;
using System.Runtime.CompilerServices;
using PeakStranding;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(MagicBean), "GrowVineRPC")]
public static class MagicBeanGrowVinePatch
{
    private static readonly ConditionalWeakTable<MagicBean, object> saved = new();

    private static void Postfix(MagicBean __instance,
                                Vector3 pos,
                                Vector3 direction,
                                float maxLength)
    {
        if (SaveManager.IsRestoring) return;
        if (saved.TryGetValue(__instance, out _)) return;
        saved.Add(__instance, null);

        SaveManager.AddItemToSave(new PlacedItemData
        {
            PrefabName = "PeakStranding/MagicBeanVine",
            Position = pos,
            Rotation = Quaternion.LookRotation(direction),
            RopeLength = maxLength
        });
    }
}
