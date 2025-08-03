using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
// using Steamworks;
using UnityEngine;
using Photon.Pun;
using System.Collections;
using PeakStranding.Data;
using PeakStranding.Online;
using System;
using System.Linq;
using System.Diagnostics;

namespace PeakStranding
{
    public static class SaveManager
    {
        public const string RESTORED_ITEM_MARKER = "PEAK_STRANDING_RESTORED";

        public static bool IsRestoring { get; private set; }
        public static void BeginRestore() => IsRestoring = true;
        public static void EndRestore() => IsRestoring = false;

        private static readonly List<PlacedItemData> SessionPlacedItems = new List<PlacedItemData>();
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Vector3Converter(), new QuaternionConverter() }
        };

        private static string GetSaveFilePath(int mapId)
        {
            //var steamId = SteamUser.GetSteamID();
            var folderPath = Path.Combine(Paths.ConfigPath, "PeakStranding", "PlacedItems");
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, $"PlacedItems_{mapId}.json");
        }

        public static void ClearSessionItems()
        {
            SessionPlacedItems.Clear();
        }

        public static void SaveItem(PlacedItemData data)
        {
            if (!Plugin.CfgLocalSaveStructures && !Plugin.CfgRemoteSaveStructures) // no saving at all
            {
                //Plugin.Log.LogWarning("Saving items is disabled in the config, skipping");
                return;
            }

            if (data == null) return;
            if (!PhotonNetwork.IsMasterClient) return; // Only the host saves items

            SessionPlacedItems.Add(data);
            var mapId = DataHelper.GetCurrentLevelIndex();

            if (Plugin.CfgLocalSaveStructures)
            {
                var filePath = GetSaveFilePath(mapId);
                var existingItems = GetSavedItemsForSeed(mapId);
                existingItems.Add(data);
                var json = JsonConvert.SerializeObject(existingItems, Formatting.Indented, JsonSettings);
                File.WriteAllText(filePath, json);
                Plugin.Log.LogInfo($"Saved local item {data.PrefabName}. Total items for map {mapId}: {existingItems.Count}");
            }

            if (Plugin.CfgRemoteSaveStructures)
            {
                try
                {
                    RemoteApi.Upload(mapId, data);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Failed to upload item {data.PrefabName}: {ex.Message}");
                }
            }
        }

        private static List<PlacedItemData> GetSavedItemsForSeed(int mapId)
        {
            var filePath = GetSaveFilePath(mapId);
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<List<PlacedItemData>>(json, JsonSettings) ?? new List<PlacedItemData>();
            }
            return new List<PlacedItemData>();
        }

        public static void LoadLocalStructures()
        {
            BeginRestore();
            try { LoadItems(); }
            finally { EndRestore(); }
        }

        public static void LoadRemoteStructures(List<ServerStructureDto> items)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            BeginRestore();
            try
            {
                if (items == null || items.Count == 0)
                {
                    Plugin.Log.LogInfo("No remote structures to load.");
                    return;
                }

                foreach (var item in items)
                {
                    SpawnItem(item.ToPlacedItemData(), item.username);
                }
            }
            finally
            {
                EndRestore();
            }
            stopwatch.Stop();
            //UIHandler.Instance.Toast($"Spawned {items.Count} online structures ({stopwatch.ElapsedMilliseconds} ms)", Color.green, 5f, 3f);
        }

        private static void LoadItems()
        {
            var mapId = DataHelper.GetCurrentLevelIndex();
            var allItems = GetSavedItemsForSeed(mapId);
            var itemsToLoad = allItems;

            // Apply limit if configured
            if (Plugin.CfgLocalStructuresLimit > 0 && allItems.Count > Plugin.CfgLocalStructuresLimit)
            {
                // Take only the most recent items up to the limit
                itemsToLoad = allItems
                    .Skip(allItems.Count - Plugin.CfgLocalStructuresLimit)
                    .ToList();

                Plugin.Log.LogInfo($"Limiting local items from {allItems.Count} to {Plugin.CfgLocalStructuresLimit} most recent items.");
            }

            Plugin.Log.LogInfo($"Loading {itemsToLoad.Count} local items for map {mapId}.");

            foreach (var itemData in itemsToLoad)
            {
                SpawnItem(itemData, "You");
            }
        }

        public static void AddCreditsToItem(GameObject gameObj, string label)
        {
            if (gameObj == null || string.IsNullOrEmpty(label) || !Plugin.CfgShowStructureCredits) return;
            var credits = gameObj.AddComponent<RestoredItemCredits>();
            credits.displayText = label;
        }

        public static void SpawnItem(PlacedItemData itemData, string label = "")
        {
            // print full serialize
            // Plugin.Log.LogInfo($"[PeakStranding] Spawning item: {JsonConvert.SerializeObject(itemData, Formatting.Indented, JsonSettings)}");
            var prefabPath = itemData.PrefabName;
            // easy case, just instantiate
            if (!prefabPath.StartsWith("PeakStranding/"))
            {
                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Plugin.Log.LogError($"Prefab not found in Resources: {prefabPath}");
                    return;
                }
                var spawnedItem = PhotonNetwork.Instantiate(prefabPath, itemData.Position, itemData.Rotation, 0, [RESTORED_ITEM_MARKER]);
                if (spawnedItem == null)
                {
                    Plugin.Log.LogError($"Failed to instantiate prefab via Photon: {prefabPath}");
                    return;
                }
                var restoredItemComponent = spawnedItem.AddComponent<RestoredItem>();
                AddCreditsToItem(spawnedItem, label);
            }
            // oh no, not an easy case
            else if (prefabPath.StartsWith("PeakStranding/JungleVine"))
            {
                if (itemData.RopeStart == Vector3.zero) // TODO: legacy, remove this when all items are saved with from/to/mid
                {
                    itemData.RopeStart = itemData.from;
                    itemData.RopeEnd = itemData.to;
                    itemData.RopeFlyingRotation = itemData.mid;
                    itemData.RopeLength = itemData.hang;
                }

                var spawnedItem = PhotonNetwork.Instantiate("ChainShootable", itemData.RopeStart, Quaternion.identity, 0, [RESTORED_ITEM_MARKER]);
                AddCreditsToItem(spawnedItem, label);
                var vine = spawnedItem.GetComponent<JungleVine>();

                if (vine != null)
                {
                    vine.photonView.RPC("ForceBuildVine_RPC",
                                            RpcTarget.AllBuffered,
                                            itemData.RopeStart,
                                            itemData.RopeEnd,
                                            itemData.RopeLength,
                                            itemData.RopeFlyingRotation);
                }
                else
                {
                    Plugin.Log.LogError($"Failed to instantiate JungleVine prefab: vine == null");
                }
            }
            else if (prefabPath.StartsWith("PeakStranding/RopeShooter"))
            {
                string anchorPrefab = itemData.RopeAntiGrav
                        ? "RopeAnchorForRopeShooterAnti"
                        : "RopeAnchorForRopeShooter";

                var anchorObj = PhotonNetwork.Instantiate(
                                        anchorPrefab,
                                        itemData.RopeStart,
                                        itemData.RopeAnchorRotation,
                                        0,
                                        [RESTORED_ITEM_MARKER]);
                AddCreditsToItem(anchorObj, label);
                var projectile = anchorObj.GetComponent<RopeAnchorProjectile>();
                if (projectile == null)
                {
                    Plugin.Log.LogError($"Failed to instantiate Rope: Prefab lacks RopeAnchorProjectile");
                    return;
                }

                float travelTime = Vector3.Distance(itemData.RopeStart, itemData.RopeEnd) * 0.01f;

                projectile.photonView.RPC("GetShot",
                                            RpcTarget.AllBuffered,
                                            itemData.RopeEnd,
                                            travelTime,
                                            itemData.RopeLength,
                                            itemData.RopeFlyingRotation);

                if (itemData.RopeAntiGrav)
                {
                    var rapw = anchorObj.GetComponent<RopeAnchorWithRope>();
                    if (rapw != null)
                        projectile.StartCoroutine(SetAntigravWhenReady(rapw));
                }
            }
            else if (prefabPath.StartsWith("PeakStranding/RopeSpool"))
            {
                // choose the correct prefabs
                string realSpoolPrefab = itemData.RopeAntiGrav ? "0_Items/Anti-Rope Spool" : "0_Items/RopeSpool";
                string anchorPrefabName = itemData.RopeAntiGrav ? "RopeAnchorAnti" : "RopeAnchor";

                // ── 1. spool ──
                var spoolObj = PhotonNetwork.Instantiate(
                                    realSpoolPrefab,
                                    itemData.Position,
                                    itemData.Rotation,
                                    0,
                                    [RESTORED_ITEM_MARKER]);

                spoolObj.AddComponent<RestoredItem>();
                var spool = spoolObj.GetComponent<RopeSpool>();

                // ── 2. rope + attach-to-spool ──
                var ropeObj = PhotonNetwork.Instantiate(
                                    spool.ropePrefab.name,
                                    spool.ropeBase.position,
                                    spool.ropeBase.rotation,
                                    0,
                                    [RESTORED_ITEM_MARKER]);
                ropeObj.AddComponent<RestoredItem>();
                var rope = ropeObj.GetComponent<Rope>();
                rope.photonView.RPC("AttachToSpool_Rpc", RpcTarget.AllBuffered, spool.photonView);

                // pre-set the desired length & flags so Update() can spawn segments
                float seg = Mathf.Max(itemData.RopeLength, spool.minSegments);
                spool.Segments = seg;
                rope.Segments = seg;
                if (itemData.RopeAntiGrav) { spool.isAntiRope = true; rope.antigrav = true; }

                // ── 3. finish when at least one segment exists ──
                spool.StartCoroutine(FinaliseRope());

                IEnumerator FinaliseRope()
                {
                    // wait until Update/AddSegment has produced joints
                    while (rope.SegmentCount == 0) yield return null;

                    // spawn anchor
                    var anchorObj = PhotonNetwork.Instantiate(
                                        anchorPrefabName,
                                        itemData.RopeEnd,
                                        itemData.RopeAnchorRotation,
                                        0,
                                        [RESTORED_ITEM_MARKER]);
                    anchorObj.AddComponent<RestoredItem>();
                    AddCreditsToItem(anchorObj, label);
                    anchorObj.GetComponent<RopeAnchor>().Ghost = false;

                    // attach rope to anchor
                    rope.photonView.RPC("AttachToAnchor_Rpc",
                                            RpcTarget.AllBuffered,
                                            anchorObj.GetComponent<PhotonView>());

                    // sync fuel so UI looks right
                    float used = seg - spool.minSegments;
                    spool.RopeFuel = Mathf.Max(0f, spool.RopeFuel - used);

                    // optional: despawn the spool if you don’t want it lying around
                    PhotonNetwork.Destroy(spoolObj);
                }
            }
            else if (prefabPath.StartsWith("PeakStranding/MagicBeanVine"))
            {
                // 1. spawn a bean network-object (the same prefab the game picks up)
                var beanObj = PhotonNetwork.Instantiate(
                                    "0_Items/MagicBean",
                                    itemData.Position,
                                    Quaternion.identity,
                                    0,
                                    [RESTORED_ITEM_MARKER]);

                beanObj.AddComponent<RestoredItem>();          // so it won't be re-saved

                // AddCreditsToItem(beanObj, label); // bean is gone after RPC
                // spawn a dummy GameObject to hold credits
                var dummyObj = new GameObject("PeakStranding/CreditsDummy");
                dummyObj.transform.position = itemData.Position;
                AddCreditsToItem(dummyObj, label);

                var bean = beanObj.GetComponent<MagicBean>();
                if (bean == null)
                {
                    Plugin.Log.LogError("Failed to instantiate MagicBean: prefab missing MagicBean script");
                    return;
                }

                // 2. broadcast the original RPC so every client spawns the vine
                Vector3 upDir = itemData.Rotation * Vector3.forward;

                bean.photonView.RPC("GrowVineRPC",
                                        RpcTarget.AllBuffered,     // buffer → late joiners also get it
                                        itemData.Position,
                                        upDir,
                                        itemData.RopeLength);

                // 3. optional: destroy the dummy bean on the host once the RPC is out
                UnityEngine.Object.Destroy(beanObj);
            }
            else
            {
                Plugin.Log.LogError($"Failed to instantiate some prefab: Unhandled type: {prefabPath}");
            }
        }

        private static IEnumerator SetAntigravWhenReady(RopeAnchorWithRope rapw)
        {
            while (rapw.rope == null) yield return null;   // wait until SpawnRope() is done
            rapw.rope.antigrav = true;
        }
    }
}