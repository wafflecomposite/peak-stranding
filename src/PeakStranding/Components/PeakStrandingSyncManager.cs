using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using PeakStranding.Data;
using PeakStranding.Online;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakStranding.Components
{
    /// <summary>
    /// A central manager for custom network synchronization of structures.
    /// This component handles sending and receiving all non-standard structure data,
    /// such as usernames, likes, and server IDs.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PeakStrandingSyncManager : MonoBehaviourPunCallbacks
    {
        public static PeakStrandingSyncManager? Instance { get; private set; }

        // Track which remote players are ready to receive incremental updates.
        // Keys are Photon actor numbers.
        private readonly HashSet<int> _readyPlayers = new();

        private readonly Dictionary<int, int> _runLikeCounts = new();
        public const int MaxLikesPerRun = 100;

        public static void DestroyInstance()
        {
            if (Instance == null) return;
            Destroy(Instance);
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            // Clients request a full sync from the host once their manager is ready.
            if (!PhotonNetwork.IsMasterClient && photonView != null)
            {
                photonView.RPC(nameof(RequestFullSync_RPC), RpcTarget.MasterClient);
            }
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            // Drop players from ready list when they disconnect.
            _readyPlayers.Remove(otherPlayer.ActorNumber);
        }

        public void ResetRunLikes()
        {
            _runLikeCounts.Clear();
        }

        public void SyncAllStructuresToPlayer(Photon.Realtime.Player targetPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var allEntries = OverlayManager.Instance.GetAllEntriesForSync();
            var syncData = allEntries
                .Where(e => e.go != null && e.go.GetPhotonView() != null) // Ensure GO and PV exist
                .Select(e =>
                {
                    int viewId = e.go.GetPhotonView().ViewID;
                    return new StructureSyncData
                    {
                        ViewID = viewId,
                        Username = e.username,
                        Likes = e.likes,
                        ServerId = e.id,
                        UserId = e.user_id,
                        LikeEnabled = !_runLikeCounts.TryGetValue(viewId, out var count) || count < MaxLikesPerRun
                    };
                }).ToList();

            if (syncData.Count == 0)
            {
                Plugin.Log.LogInfo("No structures to sync.");
                return;
            }

            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, syncData);
                var data = ms.ToArray();
                photonView.RPC(nameof(ReceiveFullSync_RPC), targetPlayer, data);
            }
        }

        [PunRPC]
        private void ReceiveFullSync_RPC(byte[] data, PhotonMessageInfo info)
        {
            Plugin.Log.LogInfo($"Received full structure sync from host {info.Sender.NickName}.");
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream(data))
            {
                var syncDataList = (List<StructureSyncData>)bf.Deserialize(ms);

                foreach (var syncData in syncDataList)
                {
                    var view = PhotonView.Find(syncData.ViewID);
                    if (view != null)
                    {
                        OverlayManager.Register(new OverlayManager.RegisterInfo
                        {
                            target = view.gameObject,
                            username = syncData.Username ?? "",
                            likes = syncData.Likes,
                            id = syncData.ServerId,
                            user_id = syncData.UserId,
                            canLike = syncData.LikeEnabled
                        });
                    }
                }
            }
        }

        // --- Host-to-Client RPCs ---

        public void RegisterNewStructure(GameObject go, string username, int likes, ulong serverId, ulong userId)
        {
            StartCoroutine(RegisterNewStructureRoutine(go, username, likes, serverId, userId));
        }

        private System.Collections.IEnumerator RegisterNewStructureRoutine(GameObject go, string username, int likes, ulong serverId, ulong userId)
        {
            if (!PhotonNetwork.IsMasterClient) yield break;

            if (go == null)
            {
                Plugin.Log.LogWarning("Attempted to register a null GameObject. Skipping.");
                yield break;
            }

            var pv = go.GetPhotonView();
            if (pv == null)
            {
                Plugin.Log.LogWarning($"Attempted to register structure {go.name} which has no PhotonView. Skipping.");
                yield break;
            }

            float waitStartTime = Time.time;
            while (pv.ViewID == 0)
            {
                if (Time.time - waitStartTime > 5f)
                {
                    Plugin.Log.LogError($"Timed out waiting for PhotonView ID on {go.name}. Aborting registration.");
                    yield break;
                }
                yield return null;
            }

            // Send to players who have completed the initial sync handshake.
            foreach (var player in PhotonNetwork.PlayerListOthers)
            {
                if (_readyPlayers.Contains(player.ActorNumber))
                {
                    bool canLike = !_runLikeCounts.TryGetValue(pv.ViewID, out var count) || count < MaxLikesPerRun;
                    photonView.RPC(nameof(RegisterStructure_RPC), player, pv.ViewID, username, likes, (long)serverId, (long)userId, canLike);
                }
            }
        }

        [PunRPC]
        private void RegisterStructure_RPC(int viewId, string username, int likes, long serverId, long userId, bool canLike)
        {
            var view = PhotonView.Find(viewId);
            if (view != null)
            {
                OverlayManager.Register(new OverlayManager.RegisterInfo
                {
                    target = view.gameObject,
                    username = username,
                    likes = likes,
                    id = (ulong)serverId,
                    user_id = (ulong)userId,
                    canLike = canLike
                });
            }
        }

        // --- Client-to-Host RPCs ---

        [PunRPC]
        private void RequestLike_RPC(int viewId, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (info.Sender != PhotonNetwork.MasterClient && !Plugin.CfgAllowClientLike) return;
            ApplyLike(viewId, info.Sender);
        }

        public void RequestLikeFromHost(int viewId)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            ApplyLike(viewId, PhotonNetwork.MasterClient);
        }

        private void ApplyLike(int viewId, Photon.Realtime.Player sender)
        {
            var view = PhotonView.Find(viewId);
            if (view == null) return;

            var entry = OverlayManager.Instance.FindEntry(view.gameObject);
            if (entry == null) return;

            // Prevent liking one's own structure
            if (sender == PhotonNetwork.MasterClient && entry.user_id != 0 && entry.user_id == Steamworks.SteamUser.GetSteamID().m_SteamID)
            {
                Plugin.Log.LogInfo("User tried to like their own structure. Denied.");
                return;
            }

            if (!_runLikeCounts.TryGetValue(viewId, out var runCount)) runCount = 0;
            if (runCount >= MaxLikesPerRun) return;
            runCount++;
            _runLikeCounts[viewId] = runCount;

            entry.likes++;
            if (entry.id != 0)
            {
                LikeBuffer.Enqueue(entry.id);
            }

            bool canLikeMore = runCount < MaxLikesPerRun;

            photonView.RPC(nameof(LikeBroadcast_RPC), RpcTarget.All, viewId, entry.likes, canLikeMore);
        }

        [PunRPC]
        private void LikeBroadcast_RPC(int viewId, int newLikeCount, bool canLike)
        {
            var view = PhotonView.Find(viewId);
            if (view != null)
            {
                OverlayManager.Instance.ApplyLikeBroadcast(view.gameObject, newLikeCount, canLike);
            }
        }

        [PunRPC]
        private void RequestRemove_RPC(int viewId, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (info.Sender != PhotonNetwork.MasterClient && !Plugin.CfgAllowClientDelete) return;

            var view = PhotonView.Find(viewId);
            if (view == null) return;

            // The host's DeletionUtility already handles networked destruction.
            DeletionUtility.Delete(view.gameObject);
        }

        [PunRPC]
        private void RequestFullSync_RPC(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            _readyPlayers.Add(info.Sender.ActorNumber);
            Plugin.Log.LogInfo($"Player {info.Sender.NickName} requested full sync.");
            SyncAllStructuresToPlayer(info.Sender);
            photonView.RPC(nameof(SyncSettings_RPC), info.Sender, Plugin.CfgAllowClientLike, Plugin.CfgAllowClientDelete);
        }

        [PunRPC]
        private void SyncSettings_RPC(bool allowLike, bool allowDelete)
        {
            OverlayManager.ClientsCanLike = allowLike;
            OverlayManager.ClientsCanDelete = allowDelete;
        }
    }
}