using HarmonyLib;
using Photon.Pun;
using PeakStranding.Components;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(RopeAnchorWithRope), nameof(RopeAnchorWithRope.SpawnRope))]
    public static class RopeAnchorWithRope_SpawnRope_Patch
    {
        private static void Postfix(RopeAnchorWithRope __instance, ref Rope __result)
        {
            // Only host manages grouping; other clients will receive RPCs
            if (!PhotonNetwork.IsMasterClient) return;
            if (__instance == null) return;

            var rootPv = __instance.photonView;
            if (rootPv == null) return;

            var group = __instance.GetComponent<DeletableGroup>();
            if (group == null) group = __instance.gameObject.AddComponent<DeletableGroup>();

            group.Add(rootPv);
            if (__result != null && __result.photonView != null)
                group.Add(__result.photonView);
            if (__instance.anchor != null && __instance.anchor.TryGetComponent<PhotonView>(out var anchorPv))
                group.Add(anchorPv);
        }
    }
}
