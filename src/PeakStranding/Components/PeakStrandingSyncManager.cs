using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using PeakStranding.Data;
using PeakStranding.Online;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

namespace PeakStranding.Components
{
    /// <summary>
    /// A central manager for custom network synchronization of structures.
    /// This component handles sending and receiving all non-standard structure data,
    /// such as usernames, likes, and server IDs.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PeakStrandingSyncManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        const byte RegisterStructureEvent = 1;
        const byte RequestLikeEvent = 2;
        const byte RequestRemoveEvent = 3;
        public static PeakStrandingSyncManager? Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Event code used when PhotonView on the manager cannot be relied on for RPCs.

        public override void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        // Handle incoming RaiseEvent calls for structure registration and client->host requests
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null) return;

            try
            {
                var code = photonEvent.Code;
                var data = photonEvent.CustomData as object[];

                if (code == RegisterStructureEvent)
                {
                    if (data == null) return;
                    int viewId = (int)data[0];
                    string username = (string)data[1];
                    int likes = (int)data[2];
                    string serverIdStr = (string)data[3];
                    string userIdStr = (string)data[4];
                    // Reuse existing RPC handler logic locally
                    RegisterStructure_RPC(viewId, username, likes, serverIdStr, userIdStr);
                    return;
                }

                if (code == RequestLikeEvent)
                {
                    if (!PhotonNetwork.IsMasterClient) return;
                    if (data == null || data.Length == 0) return;
                    int viewId = (int)data[0];
                    var view = PhotonView.Find(viewId);
                    if (view == null) return;
                    var entry = OverlayManager.Instance.FindEntry(view.gameObject);
                    if (entry != null)
                    {
                        entry.likes++;
                        if (entry.id != 0)
                        {
                            LikeBuffer.Enqueue(entry.id);
                        }

                        // Broadcast like update to all clients
                        if (photonView != null && photonView.ViewID != 0)
                        {
                            photonView.RPC(nameof(UpdateLikes_RPC), RpcTarget.All, viewId, entry.likes);
                        }
                        else
                        {
                            // Fallback: locally update overlay; others will eventually sync via full sync or other mechanisms
                            OverlayManager.Instance.UpdateLikes(view.gameObject, entry.likes);
                        }
                    }
                    return;
                }

                if (code == RequestRemoveEvent)
                {
                    if (!PhotonNetwork.IsMasterClient) return;
                    if (data == null || data.Length == 0) return;
                    int viewId = (int)data[0];
                    var view = PhotonView.Find(viewId);
                    if (view == null) return;
                    DeletionUtility.Delete(view.gameObject);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Failed to handle Photon event: {ex.Message}");
            }
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            Plugin.Log.LogInfo($"New player {newPlayer.NickName} entered. Synchronizing structures.");
            SyncAllStructuresToPlayer(newPlayer);
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
                photonView.RPC(nameof(ReceiveFullSync_RPC), targetPlayer, new object[] { data });
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

            // Do not block forever for the manager's PhotonView. We always use RaiseEvent for registration,
            // so there's no need to log a warning here.

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

            // Wait for the spawned object's view to be ready
            float waitStartTime = Time.time;
            while (pv.ViewID == 0)
            {
                if (Time.time - waitStartTime > 5f)
                {
                    Plugin.Log.LogError($"Timed out waiting for PhotonView ID on {go.name}. Aborting registration.");
                    yield break;
                }
                // Plugin.Log.LogInfo($"Waiting for ViewID on spawned object {go.name}...");
                yield return null;
            }

            // Always use a RaiseEvent for structure registration to avoid depending on the manager's PhotonView.
            // This avoids Illegal view ID errors during early initialization.
            var payload = new object[] { pv.ViewID, username, likes, serverId.ToString(), userId.ToString() };
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            var sendOptions = new ExitGames.Client.Photon.SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(RegisterStructureEvent, payload, options, sendOptions);
        }

        [PunRPC]
        private void RegisterStructure_RPC(int viewId, string username, int likes, string serverIdStr, string userIdStr)
        {
            if (!ulong.TryParse(serverIdStr, out var serverId) || !ulong.TryParse(userIdStr, out var userId))
            {
                Plugin.Log.LogError($"Failed to parse serverId '{serverIdStr}' or userId '{userIdStr}' from RPC.");
                return;
            }
            var view = PhotonView.Find(viewId);
            if (view != null)
            {
                OverlayManager.Register(new OverlayManager.RegisterInfo
                {
                    target = view.gameObject,
                    username = username,
                    likes = likes,
                    id = serverId,
                    user_id = userId
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

                // Broadcast the update to all clients
                photonView.RPC(nameof(UpdateLikes_RPC), RpcTarget.All, viewId, entry.likes);
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
    }
}