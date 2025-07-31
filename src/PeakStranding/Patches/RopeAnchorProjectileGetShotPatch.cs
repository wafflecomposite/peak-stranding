using HarmonyLib;
using UnityEngine;
using PeakStranding.Data;
using Photon.Pun;

namespace PeakStranding.Patches;

[HarmonyPatch(typeof(RopeAnchorProjectile), nameof(RopeAnchorProjectile.GetShot))]
public static class RopeAnchorProjectileGetShotPatch
{
    private static void Postfix(RopeAnchorProjectile __instance,
                                Vector3 to,
                                float travelTime,
                                float ropeLength,
                                Vector3 flyingRotation)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (SaveManager.IsRestoring) return;

        var rapw = __instance.GetComponent<RopeAnchorWithRope>();
        bool antigrav = rapw?.ropePrefab?.GetComponent<Rope>()?.antigrav == true;

        var item = new PlacedItemData
        {
            PrefabName = "PeakStranding/RopeShooter",
            RopeStart = __instance.transform.position,
            RopeEnd = to,
            RopeLength = ropeLength,
            RopeFlyingRotation = flyingRotation,
            RopeAnchorRotation = __instance.transform.rotation,
            RopeAntiGrav = antigrav
        };

        SaveManager.AddItemToSave(item);
    }
}
