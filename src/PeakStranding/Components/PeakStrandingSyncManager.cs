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

        public void SyncAllStructuresToPlayer(Photon.Realtime.Player targetPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var allEntries = OverlayManager.Instance.GetAllEntriesForSync();
            var syncData = allEntries
                .Where(e => e.go != null && e.go.GetPhotonView() != null) // Ensure GO and PV exist
                .Select(e => new StructureSyncData
                {
                    ViewID = e.go.GetPhotonView().ViewID,
                    Username = e.username,
                    Likes = e.likes,
                    ServerId = e.id,
                    UserId = e.user_id
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
                            user_id = syncData.UserId
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
                    photonView.RPC(nameof(RegisterStructure_RPC), player, pv.ViewID, username, likes, (long)serverId, (long)userId);
                }
            }
        }

        [PunRPC]
        private void RegisterStructure_RPC(int viewId, string username, int likes, long serverId, long userId)
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
                    user_id = (ulong)userId
                });
            }
        }

        [PunRPC]
        private void UpdateLikes_RPC(int viewId, int newLikeCount)
        {
            var view = PhotonView.Find(viewId);
            if (view != null)
            {
                OverlayManager.Instance.UpdateLikes(view.gameObject, newLikeCount);
            }
        }

        // --- Client-to-Host RPCs ---

        [PunRPC]
        private void RequestLike_RPC(int viewId, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var view = PhotonView.Find(viewId);
            if (view == null) return;

            // Additional validation can be added here (e.g., prevent like spam)

            var entry = OverlayManager.Instance.FindEntry(view.gameObject);
            if (entry != null)
            {
                entry.likes++;
                if (entry.id != 0)
                {
                    LikeBuffer.Enqueue(entry.id);
                }

                // Update the host locally
                OverlayManager.Instance.UpdateLikes(view.gameObject, entry.likes);

                // Broadcast the update to ready clients
                foreach (var player in PhotonNetwork.PlayerListOthers)
                {
                    if (_readyPlayers.Contains(player.ActorNumber))
                    {
                        photonView.RPC(nameof(UpdateLikes_RPC), player, viewId, entry.likes);
                    }
                }
            }
        }

        [PunRPC]
        private void RequestRemove_RPC(int viewId, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

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
        }
    }
}