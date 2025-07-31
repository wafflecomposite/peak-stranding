using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using System.Runtime.CompilerServices;
using PeakStranding;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(Rope), nameof(Rope.AttachToAnchor_Rpc))]
public static class RopeAttachToAnchorPatch
{
    private static readonly ConditionalWeakTable<Rope, object> saved = new();

    private static void Prefix(Rope __instance, PhotonView anchorView)
    {
        if (SaveManager.IsRestoring) return;
        if (saved.TryGetValue(__instance, out _)) return;

        var spool = AccessTools.Field(typeof(Rope), "spool")
                               .GetValue(__instance) as RopeSpool;
        if (spool == null) return;
        if (spool.GetComponent<RestoredItem>() != null) return;

        var anchor = anchorView.GetComponent<RopeAnchor>();
        Vector3 anchorPos = anchor.transform.position;
        Quaternion anchorRot = anchor.transform.rotation;

        saved.Add(__instance, null);

        float seg = spool.Segments > 0.01f
              ? spool.Segments
              : Mathf.Max(__instance.SegmentCount,
                          spool.minSegments);

        SaveManager.AddItemToSave(new PlacedItemData
        {
            PrefabName = "PeakStranding/RopeSpool",
            Position = spool.transform.position,
            Rotation = spool.transform.rotation,
            SpoolSegments = seg,
            RopeAntiGrav = spool.isAntiRope || __instance.antigrav,
            RopeStart = spool.ropeBase.position,
            RopeEnd = anchorPos,
            RopeAnchorRotation = anchorRot
        });

        Debug.Log($"[ItemPersistence] Saved rope spool @ {spool.transform.position}");
    }
}
