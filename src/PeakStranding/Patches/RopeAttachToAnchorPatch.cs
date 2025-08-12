using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using System.Runtime.CompilerServices;
using PeakStranding.Data;
using PeakStranding.Components;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(Rope), nameof(Rope.AttachToAnchor_Rpc))]
public static class RopeAttachToAnchorPatch
{
    private static readonly ConditionalWeakTable<Rope, object> saved = new();
    private static readonly object s_token = new object();

    private static void Prefix(Rope __instance, PhotonView anchorView)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (SaveManager.IsRestoring) return;
        if (saved.TryGetValue(__instance, out _)) return;

        var spool = AccessTools.Field(typeof(Rope), "spool")
                               .GetValue(__instance) as RopeSpool;
        if (spool == null) return;
        if (spool.GetComponent<RestoredItem>() != null) return;

        if (anchorView == null) return;
        if (!anchorView.TryGetComponent<RopeAnchor>(out var anchor) || anchor == null) return;
        Vector3 anchorPos = anchor.transform.position;
        Quaternion anchorRot = anchor.transform.rotation;

        saved.Add(__instance, s_token);

        // Ensure grouped deletion: attach group on anchor and include rope + anchor PVs
        var root = anchorView.gameObject;
        var group = root.GetComponent<DeletableGroup>();
        if (group == null) group = root.AddComponent<DeletableGroup>();
        group.Add(anchorView);
        if (__instance != null && __instance.photonView != null)
            group.Add(__instance.photonView);

        float seg = spool.Segments > 0.01f
              ? spool.Segments
              : Mathf.Max(__instance!.SegmentCount,
                          spool.minSegments);

        var itemData = new PlacedItemData
        {
            PrefabName = "PeakStranding/RopeSpool",
            Position = spool.transform.position,
            Rotation = spool.transform.rotation,
            RopeLength = seg,
            RopeAntiGrav = spool.isAntiRope || __instance!.antigrav,
            RopeStart = spool.ropeBase.position,
            RopeEnd = anchorPos,
            RopeAnchorRotation = anchorRot
        };
        itemData.AddCurrentRunContext();
        SaveManager.SaveItem(itemData);

        // Debug.Log($"[PeakStranding] Saved rope spool @ {spool.transform.position}");
    }
}
