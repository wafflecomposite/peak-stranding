using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;
using PeakStranding.Data;
using PeakStranding.Online;
using PeakStranding.Patches;
using Photon.Pun;
using UnityEngine;

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

        private static readonly Dictionary<int, List<(PlacedItemData data, string label)>> CachedStructures = new();
        private static readonly Dictionary<int, List<GameObject>> SpawnedInstances = new();

        private static string GetSaveFilePath(int mapId)
        {
            var folderPath = Path.Combine(Paths.ConfigPath, "PeakStranding", "PlacedItems");
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, $"PlacedItems_{mapId}.json");
        }

        public static void ClearCache()
        {
            SessionPlacedItems.Clear();
            CachedStructures.Clear();
            foreach (var segmentIndex in SpawnedInstances.Keys.ToList())
            {
                DespawnStructuresForSegment(segmentIndex);
            }
            SpawnedInstances.Clear();
        }

        public static void SaveItem(PlacedItemData data)
        {
            if (!Plugin.CfgLocalSaveStructures && !Plugin.CfgRemoteSaveStructures) return;
            if (data == null) return;
            if (!PhotonNetwork.IsMasterClient) return;

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
                try { RemoteApi.Upload(mapId, data); }
                catch (Exception ex) { Plugin.Log.LogError($"Failed to upload item {data.PrefabName}: {ex.Message}"); }
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

        public static void CacheLocalStructures()
        {
            var mapId = DataHelper.GetCurrentLevelIndex();
            var allItems = GetSavedItemsForSeed(mapId);
            var itemsToLoad = allItems;

            if (Plugin.CfgLocalStructuresLimit > 0 && allItems.Count > Plugin.CfgLocalStructuresLimit)
            {
                itemsToLoad = allItems.Skip(allItems.Count - Plugin.CfgLocalStructuresLimit).ToList();
            }

            Plugin.Log.LogInfo($"Caching {itemsToLoad.Count} local items for map {mapId}.");
            foreach (var itemData in itemsToLoad)
            {
                if (!CachedStructures.ContainsKey(itemData.MapSegment))
                {
                    CachedStructures[itemData.MapSegment] = new List<(PlacedItemData, string)>();
                }
                CachedStructures[itemData.MapSegment].Add((itemData, "You"));
            }
        }

        public static void CacheRemoteStructures(List<ServerStructureDto> items)
        {
            if (items == null || items.Count == 0)
            {
                Plugin.Log.LogInfo("No remote structures to cache.");
                return;
            }

            foreach (var item in items)
            {
                var itemData = item.ToPlacedItemData();
                if (!CachedStructures.ContainsKey(itemData.MapSegment))
                {
                    CachedStructures[itemData.MapSegment] = new List<(PlacedItemData, string)>();
                }
                CachedStructures[itemData.MapSegment].Add((itemData, item.username));
            }
        }

        public static void SpawnStructuresForSegment(int segmentIndex)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!CachedStructures.TryGetValue(segmentIndex, out var itemsToSpawn))
            {
                Plugin.Log.LogInfo($"No cached structures to spawn for segment {segmentIndex}.");
                return;
            }

            BeginRestore();
            try
            {
                Plugin.Log.LogInfo($"Spawning {itemsToSpawn.Count} structures for segment {segmentIndex}.");
                foreach (var (itemData, label) in itemsToSpawn)
                {
                    SpawnItem(itemData, label, (spawnedGo) =>
                    {
                        if (spawnedGo == null) return;
                        if (!SpawnedInstances.ContainsKey(segmentIndex))
                        {
                            SpawnedInstances[segmentIndex] = new List<GameObject>();
                        }
                        SpawnedInstances[segmentIndex].Add(spawnedGo);
                    });
                }
            }
            finally
            {
                EndRestore();
            }
        }

        public static void DespawnStructuresForSegment(int segmentIndex)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!SpawnedInstances.TryGetValue(segmentIndex, out var instancesToDespawn))
            {
                Plugin.Log.LogInfo($"No spawned instances to despawn for segment {segmentIndex}.");
                return;
            }

            Plugin.Log.LogInfo($"Despawning {instancesToDespawn.Count} structures for segment {segmentIndex}.");
            foreach (var instance in instancesToDespawn)
            {
                if (instance != null)
                {
                    var rope = instance.GetComponent<Rope>();
                    rope?.photonView.RPC("Detach_Rpc", RpcTarget.AllBuffered);

                    var magicBean = instance.GetComponent<MagicBean>();
                    if (magicBean != null)
                    {
                        MagicBeanPatch.RemoveBeanAndVine(magicBean);
                    }

                    var pv = instance.GetComponent<PhotonView>();
                    if (pv != null)
                    {
                        PhotonNetwork.Destroy(pv);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(instance);
                    }
                }
            }
            SpawnedInstances.Remove(segmentIndex);
        }

        // --- CORRECTED: Safe cleanup method ---
        public static void CleanupRunStructures()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            Plugin.Log.LogInfo("Cleaning up all structures from the run.");

            int cleanedCount = 0;

            // Iterate through all PhotonViews currently in the scene.
            foreach (var pv in PhotonNetwork.PhotonViewCollection)
            {
                if (pv == null || pv.InstantiationData == null || pv.InstantiationData.Length == 0)
                {
                    continue;
                }

                // Check if the instantiation data contains our marker.
                if (pv.InstantiationData[0] is string marker && marker == RESTORED_ITEM_MARKER)
                {
                    // This is one of our structures. It's safe to destroy.
                    // Destroying the object will also remove its buffered RPCs from the server.
                    PhotonNetwork.Destroy(pv.gameObject);
                    cleanedCount++;
                }
            }

            Plugin.Log.LogInfo($"Cleaned up {cleanedCount} spawned structures and their buffered RPCs.");

            // Clear local tracking lists for the next run
            SpawnedInstances.Clear();
            CachedStructures.Clear();
            SessionPlacedItems.Clear();
        }

        public static void AddCreditsToItem(GameObject gameObj, string label)
        {
            if (gameObj == null || string.IsNullOrEmpty(label) || !Plugin.CfgShowStructureCredits) return;
            var credits = gameObj.AddComponent<RestoredItemCredits>();
            credits.displayText = label;
        }

        // --- MODIFIED: Ensured the marker is passed as an object array ---
        public static void SpawnItem(PlacedItemData itemData, string label = "", Action<GameObject> onSpawned = null)
        {
            var prefabPath = itemData.PrefabName;
            object[] instantiationData = { RESTORED_ITEM_MARKER }; // This is the key change

            if (!prefabPath.StartsWith("PeakStranding/"))
            {
                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Plugin.Log.LogError($"Prefab not found in Resources: {prefabPath}");
                    onSpawned?.Invoke(null);
                    return;
                }
                var spawnedItem = PhotonNetwork.Instantiate(prefabPath, itemData.Position, itemData.Rotation, 0, instantiationData);
                if (spawnedItem == null)
                {
                    Plugin.Log.LogError($"Failed to instantiate prefab via Photon: {prefabPath}");
                    onSpawned?.Invoke(null);
                    return;
                }
                spawnedItem.AddComponent<RestoredItem>();
                AddCreditsToItem(spawnedItem, label);
                onSpawned?.Invoke(spawnedItem);
            }
            else if (prefabPath.StartsWith("PeakStranding/JungleVine"))
            {
                if (itemData.RopeStart == Vector3.zero)
                {
                    itemData.RopeStart = itemData.from;
                    itemData.RopeEnd = itemData.to;
                    itemData.RopeFlyingRotation = itemData.mid;
                    itemData.RopeLength = itemData.hang;
                }

                var spawnedItem = PhotonNetwork.Instantiate("ChainShootable", itemData.RopeStart, Quaternion.identity, 0, instantiationData);
                AddCreditsToItem(spawnedItem, label);
                onSpawned?.Invoke(spawnedItem);
                var vine = spawnedItem.GetComponent<JungleVine>();
                if (vine != null)
                {
                    vine.photonView.RPC("ForceBuildVine_RPC", RpcTarget.AllBuffered, itemData.RopeStart, itemData.RopeEnd, itemData.RopeLength, itemData.RopeFlyingRotation);
                }
                else
                {
                    Plugin.Log.LogError($"Failed to instantiate JungleVine prefab: vine == null");
                }
            }
            else if (prefabPath.StartsWith("PeakStranding/RopeShooter"))
            {
                string anchorPrefab = itemData.RopeAntiGrav ? "RopeAnchorForRopeShooterAnti" : "RopeAnchorForRopeShooter";
                var anchorObj = PhotonNetwork.Instantiate(anchorPrefab, itemData.RopeStart, itemData.RopeAnchorRotation, 0, instantiationData);
                AddCreditsToItem(anchorObj, label);
                onSpawned?.Invoke(anchorObj);
                var projectile = anchorObj.GetComponent<RopeAnchorProjectile>();
                if (projectile == null)
                {
                    Plugin.Log.LogError($"Failed to instantiate Rope: Prefab lacks RopeAnchorProjectile");
                    return;
                }
                float travelTime = Vector3.Distance(itemData.RopeStart, itemData.RopeEnd) * 0.01f;
                projectile.photonView.RPC("GetShot", RpcTarget.AllBuffered, itemData.RopeEnd, travelTime, itemData.RopeLength, itemData.RopeFlyingRotation);
                if (itemData.RopeAntiGrav)
                {
                    var rapw = anchorObj.GetComponent<RopeAnchorWithRope>();
                    if (rapw != null) projectile.StartCoroutine(SetAntigravWhenReady(rapw));
                }
            }
            else if (prefabPath.StartsWith("PeakStranding/RopeSpool"))
            {
                string realSpoolPrefab = itemData.RopeAntiGrav ? "0_Items/Anti-Rope Spool" : "0_Items/RopeSpool";
                string anchorPrefabName = itemData.RopeAntiGrav ? "RopeAnchorAnti" : "RopeAnchor";
                var spoolObj = PhotonNetwork.Instantiate(realSpoolPrefab, itemData.Position, itemData.Rotation, 0, instantiationData);
                spoolObj.AddComponent<RestoredItem>();
                var spool = spoolObj.GetComponent<RopeSpool>();
                var ropeObj = PhotonNetwork.Instantiate(spool.ropePrefab.name, spool.ropeBase.position, spool.ropeBase.rotation, 0, instantiationData);
                ropeObj.AddComponent<RestoredItem>();
                var rope = ropeObj.GetComponent<Rope>();
                rope.photonView.RPC("AttachToSpool_Rpc", RpcTarget.AllBuffered, spool.photonView);
                float seg = Mathf.Max(itemData.RopeLength, spool.minSegments);
                spool.Segments = seg;
                rope.Segments = seg;
                if (itemData.RopeAntiGrav) { spool.isAntiRope = true; rope.antigrav = true; }
                spool.StartCoroutine(FinaliseRope());
                IEnumerator FinaliseRope()
                {
                    while (rope.SegmentCount == 0) yield return null;
                    var anchorObj = PhotonNetwork.Instantiate(anchorPrefabName, itemData.RopeEnd, itemData.RopeAnchorRotation, 0, instantiationData);
                    anchorObj.AddComponent<RestoredItem>();
                    AddCreditsToItem(anchorObj, label);
                    anchorObj.GetComponent<RopeAnchor>().Ghost = false;
                    rope.photonView.RPC("AttachToAnchor_Rpc", RpcTarget.AllBuffered, anchorObj.GetComponent<PhotonView>());
                    float used = seg - spool.minSegments;
                    spool.RopeFuel = Mathf.Max(0f, spool.RopeFuel - used);
                    onSpawned?.Invoke(ropeObj);
                    onSpawned?.Invoke(anchorObj);
                    PhotonNetwork.Destroy(spoolObj);
                }
            }
            else if (prefabPath.StartsWith("PeakStranding/MagicBeanVine"))
            {
                var beanObj = PhotonNetwork.Instantiate("0_Items/MagicBean", itemData.Position, Quaternion.identity, 0, instantiationData);
                beanObj.AddComponent<RestoredItem>();
                beanObj.AddComponent<MagicBeanPatch.MagicBeanEventHandler>();
                //var dummyObj = new GameObject("PeakStranding/CreditsDummy");
                //dummyObj.transform.position = itemData.Position;
                var bean = beanObj.GetComponent<MagicBean>();
                if (bean == null)
                {
                    Plugin.Log.LogError("Failed to instantiate MagicBean: prefab missing MagicBean script");
                    return;
                }
                Vector3 upDir = itemData.Rotation * Vector3.forward;
                bean.photonView.RPC("GrowVineRPC", RpcTarget.AllBuffered, itemData.Position, upDir, itemData.RopeLength);
                AddCreditsToItem(beanObj, label);
                onSpawned?.Invoke(beanObj);
                //UnityEngine.Object.Destroy(beanObj);
            }
            else
            {
                Plugin.Log.LogError($"Failed to instantiate some prefab: Unhandled type: {prefabPath}");
                onSpawned?.Invoke(null);
            }
        }

        private static IEnumerator SetAntigravWhenReady(RopeAnchorWithRope rapw)
        {
            while (rapw.rope == null) yield return null;
            rapw.rope.antigrav = true;
        }
    }
}