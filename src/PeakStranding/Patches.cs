using HarmonyLib;
using UnityEngine;
using Photon.Pun;
using System;
using System.Runtime.CompilerServices;

namespace ItemPersistenceMod.Patches
{
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
    public class RunManager_StartRun_Patch
    {
        private static void Postfix()
        {
            Debug.Log("[ItemPersistence] New run started. Loading saved items.");
            SaveManager.ClearSessionItems();
            SaveManager.LoadAndSpawnItems();
        }
    }

    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.Instantiate))]
    public class PhotonNetwork_Instantiate_Patch
    {
        private static void Postfix(string prefabName, Vector3 position, Quaternion rotation, byte group, object[] data = null)
        {
            if (SaveManager.IsRestoring) return;
            if (data?.Length > 0 && data[0] as string == SaveManager.RESTORED_ITEM_MARKER) return;
            Debug.Log($"[PhotonNetwork.Instantiate] Prefab: {prefabName}, Position: {position}, Rotation: {rotation}, Group: {group}");

            // Items that we can just spawn without further considerations
            var basicSpawnable = new string[]
            {
                "0_Items/ClimbingSpikeHammered",
                "0_Items/ShelfShroomSpawn",
                "0_Items/BounceShroomSpawn",
                "Flag_planted_seagull",
                "Flag_planted_turtle",
                "PortableStovetop_Placed"
                //"RopeAnchorForRopeShooter",
            };
            if (Array.Exists(basicSpawnable, p => p == prefabName))
            {
                var itemData = new PlacedItemData
                {
                    PrefabName = prefabName,
                    Position = position,
                    Rotation = rotation
                };
                SaveManager.AddItemToSave(itemData);
                return;
            }

            /* vine
            ChainShootable
            JungleVine component2 = PhotonNetwork.Instantiate(vinePrefab.name, vector, Quaternion.identity, 0).GetComponent<JungleVine>();
			component2.photonView.RPC("ForceBuildVine_RPC", RpcTarget.AllBuffered, vector, hit.point, num2, mid);

            RopeAnchorForRopeShooter
            RopeDynamic2 Variant
            */
        }
    }

    [HarmonyPatch(typeof(JungleVine), nameof(JungleVine.ForceBuildVine_RPC))]
    public class JungleVine_ForceBuildVine_RPC_Patch
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
            return;
        }
    }

    [HarmonyPatch(typeof(RopeAnchorProjectile), nameof(RopeAnchorProjectile.GetShot))]
    public static class RopeAnchorProjectile_GetShot_Patch
    {
        private static void Postfix(RopeAnchorProjectile __instance,
                                    Vector3 to,
                                    float travelTime,
                                    float ropeLength,
                                    Vector3 flyingRotation)
        {
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

    [HarmonyPatch(typeof(Rope), nameof(Rope.AttachToAnchor_Rpc))]
    public static class Rope_AttachToAnchor_Patch
    {
        private static readonly ConditionalWeakTable<Rope, object> saved = new();

        static void Prefix(Rope __instance, PhotonView anchorView)
        {
            if (SaveManager.IsRestoring) return;
            if (saved.TryGetValue(__instance, out _)) return;

            var spool = AccessTools.Field(typeof(Rope), "spool")
                                   .GetValue(__instance) as RopeSpool;
            if (spool == null) return;
            if (spool.GetComponent<RestoredItem>() != null) return;


            var anchor = anchorView.GetComponent<RopeAnchor>();
            var anchorTf = anchor.anchorPoint ?? anchor.transform;

            saved.Add(__instance, null);

            SaveManager.AddItemToSave(new PlacedItemData
            {
                PrefabName = "PeakStranding/RopeSpool",
                Position = spool.transform.position,
                Rotation = spool.transform.rotation,
                SpoolSegments = spool.Segments,
                RopeAntiGrav = spool.isAntiRope || __instance.antigrav,
                RopeStart = spool.ropeBase.position,
                RopeEnd = anchorTf.position,
                RopeAnchorRotation = anchor.transform.rotation
            });

            Debug.Log($"[ItemPersistence] Saved rope spool @ {spool.transform.position}");
        }
    }

    [HarmonyPatch(typeof(MagicBean), "GrowVineRPC")]
    public static class MagicBean_GrowVineRPC_Patch
    {
        private static readonly ConditionalWeakTable<MagicBean, object> saved = new();

        // POSTFIX – bean still exists when this runs
        static void Postfix(MagicBean __instance,
                            Vector3 pos,
                            Vector3 direction,
                            float maxLength)
        {
            if (SaveManager.IsRestoring) return;           // skip replay
            if (saved.TryGetValue(__instance, out _)) return;           // skip 2nd call
            saved.Add(__instance, null);

            SaveManager.AddItemToSave(new PlacedItemData
            {
                PrefabName = "PeakStranding/MagicBeanVine",
                Position = pos,
                Rotation = Quaternion.LookRotation(direction), // reuse generic slot
                RopeLength = maxLength                           // reuse float slot
            });
        }
    }
}