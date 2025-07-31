using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
// using Steamworks;
using UnityEngine;
using Photon.Pun;
using System.Collections;

namespace ItemPersistenceMod
{
    public static class SaveManager
    {
        public const string RESTORED_ITEM_MARKER = "ITEM_PERSISTENCE_MOD_RESTORED";

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
            var folderPath = Path.Combine(Paths.ConfigPath, "ItemPersistenceMod");
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, $"PlacedItems_{mapId}.json");
        }

        public static void ClearSessionItems()
        {
            SessionPlacedItems.Clear();
        }

        public static void AddItemToSave(PlacedItemData data)
        {
            if (data == null) return;

            SessionPlacedItems.Add(data);

            var mapId = GameHandler.GetService<NextLevelService>().Data.Value.CurrentLevelIndex;
            var filePath = GetSaveFilePath(mapId);

            var existingItems = LoadItems(mapId);
            existingItems.Add(data);

            var json = JsonConvert.SerializeObject(existingItems, Formatting.Indented, JsonSettings);
            File.WriteAllText(filePath, json);
            Debug.Log($"[ItemPersistence] Saved item {data.PrefabName}. Total items for map {mapId}: {existingItems.Count}");
        }

        private static List<PlacedItemData> LoadItems(int mapId)
        {
            var filePath = GetSaveFilePath(mapId);
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<List<PlacedItemData>>(json, JsonSettings) ?? new List<PlacedItemData>();
            }
            return new List<PlacedItemData>();
        }

        public static void LoadAndSpawnItems()
        {
            BeginRestore();
            try { LoadItems(); }
            finally { EndRestore(); }
        }

        private static void LoadItems()
        {
            var mapId = GameHandler.GetService<NextLevelService>().Data.Value.CurrentLevelIndex;
            var itemsToLoad = LoadItems(mapId);

            Debug.Log($"[ItemPersistence] Loading {itemsToLoad.Count} items for map {mapId}.");

            foreach (var itemData in itemsToLoad)
            {
                var prefabPath = itemData.PrefabName;
                // easy case, just instantiate
                if (!prefabPath.StartsWith("PeakStranding/"))
                {
                    var prefab = Resources.Load<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        Debug.LogError($"[ItemPersistence] Prefab not found in Resources: {prefabPath}");
                        continue;
                    }
                    var spawnedItem = PhotonNetwork.Instantiate(prefabPath, itemData.Position, itemData.Rotation, 0, [RESTORED_ITEM_MARKER]);
                    if (spawnedItem == null)
                    {
                        Debug.LogError($"[ItemPersistence] Failed to instantiate prefab via Photon: {prefabPath}");
                        continue;
                    }
                    spawnedItem.AddComponent<RestoredItem>();
                }
                // oh no, not an easy case
                else if (prefabPath.StartsWith("PeakStranding/JungleVine"))
                {
                    var vine = PhotonNetwork.Instantiate("ChainShootable", itemData.from, Quaternion.identity, 0, [RESTORED_ITEM_MARKER]).GetComponent<JungleVine>();
                    if (vine != null)
                    {
                        vine.ForceBuildVine_RPC(itemData.from, itemData.to, itemData.hang, itemData.mid);
                    }
                    else
                    {
                        Debug.LogError($"[ItemPersistence] Failed to instantiate JungleVine prefab: {prefabPath}");
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

                    var projectile = anchorObj.GetComponent<RopeAnchorProjectile>();
                    if (projectile == null)
                    {
                        Debug.LogError($"[ItemPersistence] Rope prefab lacks RopeAnchorProjectile: {prefabPath}");
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
                    float seg = Mathf.Max(itemData.SpoolSegments, spool.minSegments);
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

                    var bean = beanObj.GetComponent<MagicBean>();
                    if (bean == null)
                    {
                        Debug.LogError("[ItemPersistence] MagicBean prefab missing MagicBean script");
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
                    Object.Destroy(beanObj);
                }
                else
                {
                    Debug.LogWarning($"[ItemPersistence] Unhandled prefab type: {prefabPath}");
                }
            }
        }

        private static IEnumerator SetAntigravWhenReady(RopeAnchorWithRope rapw)
        {
            while (rapw.rope == null) yield return null;   // wait until SpawnRope() is done
            rapw.rope.antigrav = true;
        }
    }
}