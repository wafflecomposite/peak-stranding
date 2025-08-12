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

        public static void DestroyInstance()
        {
            Plugin.Log.LogInfo("DestroyInstance called.");
            if (Instance == null)
            {
                Plugin.Log.LogWarning("DestroyInstance called but Instance is already null.");
                return;
            }
            Plugin.Log.LogInfo("Destroying PeakStrandingSyncManager instance.");
            Destroy(Instance);
            Instance = null;
            Plugin.Log.LogInfo("PeakStrandingSyncManager instance destroyed and set to null.");
        }

        private void Awake()
        {
            Plugin.Log.LogInfo("Awake called for PeakStrandingSyncManager.");
            if (Instance != null && Instance != this)
            {
                Plugin.Log.LogWarning("Another instance of PeakStrandingSyncManager already exists. Destroying this instance.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Plugin.Log.LogInfo("PeakStrandingSyncManager instance initialized.");
        }

        private void OnDestroy()
        {
            Plugin.Log.LogInfo("OnDestroy called for PeakStrandingSyncManager.");
            if (Instance == this)
            {
                Plugin.Log.LogInfo("Setting PeakStrandingSyncManager instance to null.");
                Instance = null;
            }
            else
            {
                Plugin.Log.LogInfo("OnDestroy called but this is not the current instance.");
            }
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Plugin.Log.LogInfo($"OnPlayerEnteredRoom called for player: {newPlayer.NickName} (ID: {newPlayer.ActorNumber})");
            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogInfo("Not the master client, skipping structure synchronization.");
                return;
            }

            Plugin.Log.LogInfo($"New player {newPlayer.NickName} entered. Synchronizing structures.");
            SyncAllStructuresToPlayer(newPlayer);
            Plugin.Log.LogInfo($"Structure synchronization completed for player {newPlayer.NickName}.");
        }

        public void SyncAllStructuresToPlayer(Photon.Realtime.Player targetPlayer)
        {
            Plugin.Log.LogInfo($"SyncAllStructuresToPlayer called for player: {targetPlayer.NickName} (ID: {targetPlayer.ActorNumber})");
            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogWarning("SyncAllStructuresToPlayer called but not master client. Skipping.");
                return;
            }

            Plugin.Log.LogInfo("Getting all entries for sync from OverlayManager.");
            var allEntries = OverlayManager.Instance.GetAllEntriesForSync();
            Plugin.Log.LogInfo($"Found {allEntries.Count} entries to sync.");

            Plugin.Log.LogInfo("Filtering entries and creating StructureSyncData objects.");
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

            Plugin.Log.LogInfo($"Filtered to {syncData.Count} valid structures for sync.");
            if (syncData.Count == 0)
            {
                Plugin.Log.LogInfo("No structures to sync.");
                return;
            }

            Plugin.Log.LogInfo("Serializing structure sync data.");
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, syncData);
                var data = ms.ToArray();
                Plugin.Log.LogInfo($"Serialized {syncData.Count} structures. Sending RPC to player {targetPlayer.NickName}.");
                photonView.RPC(nameof(ReceiveFullSync_RPC), targetPlayer, data);
                Plugin.Log.LogInfo($"RPC sent to player {targetPlayer.NickName}.");
            }
        }

        [PunRPC]
        private void ReceiveFullSync_RPC(byte[] data, PhotonMessageInfo info)
        {
            Plugin.Log.LogInfo($"ReceiveFullSync_RPC called from host {info.Sender.NickName} (ID: {info.Sender.ActorNumber}).");
            Plugin.Log.LogInfo($"Received {data.Length} bytes of structure sync data.");

            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream(data))
            {
                Plugin.Log.LogInfo("Deserializing structure sync data.");
                var syncDataList = (List<StructureSyncData>)bf.Deserialize(ms);
                Plugin.Log.LogInfo($"Deserialized {syncDataList.Count} structures.");

                foreach (var syncData in syncDataList)
                {
                    Plugin.Log.LogInfo($"Processing structure with ViewID: {syncData.ViewID}, Username: {syncData.Username}, Likes: {syncData.Likes}, ServerId: {syncData.ServerId}, UserId: {syncData.UserId}");
                    var view = PhotonView.Find(syncData.ViewID);
                    if (view != null)
                    {
                        Plugin.Log.LogInfo($"Found PhotonView for ViewID: {syncData.ViewID}. Registering structure.");
                        OverlayManager.Register(new OverlayManager.RegisterInfo
                        {
                            target = view.gameObject,
                            username = syncData.Username ?? "",
                            likes = syncData.Likes,
                            id = syncData.ServerId,
                            user_id = syncData.UserId
                        });
                        Plugin.Log.LogInfo($"Structure registered successfully for ViewID: {syncData.ViewID}.");
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Could not find PhotonView for ViewID: {syncData.ViewID}. Skipping structure registration.");
                    }
                }
                Plugin.Log.LogInfo($"Finished processing {syncDataList.Count} structures.");
            }
        }

        // --- Host-to-Client RPCs ---

        public void RegisterNewStructure(GameObject go, string username, int likes, ulong serverId, ulong userId)
        {
            Plugin.Log.LogInfo($"RegisterNewStructure called for GameObject: {go?.name ?? "null"}, Username: {username}, Likes: {likes}, ServerId: {serverId}, UserId: {userId}");
            if (go != null)
            {
                StartCoroutine(RegisterNewStructureRoutine(go, username, likes, serverId, userId));
                Plugin.Log.LogInfo($"Started RegisterNewStructureRoutine coroutine.");
            }
            else
            {
                Plugin.Log.LogInfo($"No calling RegisterNewStructureRoutine, GO is null.");
            }
        }

        private System.Collections.IEnumerator RegisterNewStructureRoutine(GameObject go, string username, int likes, ulong serverId, ulong userId)
        {
            Plugin.Log.LogInfo($"RegisterNewStructureRoutine started for GameObject: {go?.name ?? "null"}, Username: {username}, Likes: {likes}, ServerId: {serverId}, UserId: {userId}");

            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogWarning("RegisterNewStructureRoutine called but not master client. Exiting coroutine.");
                yield break;
            }

            if (go == null)
            {
                Plugin.Log.LogWarning("Attempted to register a null GameObject. Skipping.");
                yield break;
            }

            Plugin.Log.LogInfo($"Getting PhotonView for GameObject: {go.name}");
            var pv = go.GetPhotonView();
            if (pv == null)
            {
                Plugin.Log.LogWarning($"Attempted to register structure {go.name} which has no PhotonView. Skipping.");
                yield break;
            }

            Plugin.Log.LogInfo($"Waiting for PhotonView ID to be assigned to {go.name}");
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

            Plugin.Log.LogInfo($"PhotonView ID assigned: {pv.ViewID}. Sending RPC to register structure.");
            photonView.RPC(nameof(RegisterStructure_RPC), RpcTarget.Others, pv.ViewID, username, likes, (long)serverId, (long)userId);
            Plugin.Log.LogInfo($"RPC sent to register structure with ViewID: {pv.ViewID}.");
        }

        [PunRPC]
        private void RegisterStructure_RPC(int viewId, string username, int likes, long serverId, long userId)
        {
            Plugin.Log.LogInfo($"RegisterStructure_RPC called for ViewID: {viewId}, Username: {username}, Likes: {likes}, ServerId: {serverId}, UserId: {userId}");

            var view = PhotonView.Find(viewId);
            if (view != null)
            {
                Plugin.Log.LogInfo($"Found PhotonView for ViewID: {viewId}. Registering structure with OverlayManager.");
                OverlayManager.Register(new OverlayManager.RegisterInfo
                {
                    target = view.gameObject,
                    username = username,
                    likes = likes,
                    id = (ulong)serverId,
                    user_id = (ulong)userId
                });
                Plugin.Log.LogInfo($"Structure registered successfully for ViewID: {viewId}.");
            }
            else
            {
                Plugin.Log.LogWarning($"Could not find PhotonView for ViewID: {viewId}. Skipping structure registration.");
            }
        }

        [PunRPC]
        private void UpdateLikes_RPC(int viewId, int newLikeCount)
        {
            Plugin.Log.LogInfo($"UpdateLikes_RPC called for ViewID: {viewId}, NewLikeCount: {newLikeCount}");

            var view = PhotonView.Find(viewId);
            if (view != null)
            {
                Plugin.Log.LogInfo($"Found PhotonView for ViewID: {viewId}. Updating likes to {newLikeCount}.");
                OverlayManager.Instance.UpdateLikes(view.gameObject, newLikeCount);
                Plugin.Log.LogInfo($"Likes updated successfully for ViewID: {viewId}.");
            }
            else
            {
                Plugin.Log.LogWarning($"Could not find PhotonView for ViewID: {viewId}. Skipping like update.");
            }
        }

        // --- Client-to-Host RPCs ---

        [PunRPC]
        private void RequestLike_RPC(int viewId, PhotonMessageInfo info)
        {
            Plugin.Log.LogInfo($"RequestLike_RPC called for ViewID: {viewId} from player {info.Sender.NickName} (ID: {info.Sender.ActorNumber})");

            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogWarning("RequestLike_RPC called but not master client. Ignoring request.");
                return;
            }

            Plugin.Log.LogInfo($"Finding PhotonView for ViewID: {viewId}");
            var view = PhotonView.Find(viewId);
            if (view == null)
            {
                Plugin.Log.LogWarning($"Could not find PhotonView for ViewID: {viewId}. Ignoring like request.");
                return;
            }

            Plugin.Log.LogInfo($"Finding OverlayManager entry for GameObject: {view.gameObject.name}");
            // Additional validation can be added here (e.g., prevent like spam)

            var entry = OverlayManager.Instance.FindEntry(view.gameObject);
            if (entry != null)
            {
                Plugin.Log.LogInfo($"Found entry for GameObject: {view.gameObject.name}. Incrementing likes from {entry.likes} to {entry.likes + 1}.");
                entry.likes++;
                if (entry.id != 0)
                {
                    Plugin.Log.LogInfo($"Enqueuing like for structure ID: {entry.id}");
                    LikeBuffer.Enqueue(entry.id);
                }
                else
                {
                    Plugin.Log.LogInfo($"Structure ID is 0 (local-only), not enqueuing like.");
                }

                Plugin.Log.LogInfo($"Broadcasting like update for ViewID: {viewId} with new like count: {entry.likes}");
                // Broadcast the update to all clients
                photonView.RPC(nameof(UpdateLikes_RPC), RpcTarget.All, viewId, entry.likes);
                Plugin.Log.LogInfo($"Like update broadcast completed for ViewID: {viewId}.");
            }
            else
            {
                Plugin.Log.LogWarning($"Could not find OverlayManager entry for GameObject: {view.gameObject.name}. Ignoring like request.");
            }
        }

        [PunRPC]
        private void RequestRemove_RPC(int viewId, PhotonMessageInfo info)
        {
            Plugin.Log.LogInfo($"RequestRemove_RPC called for ViewID: {viewId} from player {info.Sender.NickName} (ID: {info.Sender.ActorNumber})");

            if (!PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogWarning("RequestRemove_RPC called but not master client. Ignoring request.");
                return;
            }

            Plugin.Log.LogInfo($"Finding PhotonView for ViewID: {viewId}");
            var view = PhotonView.Find(viewId);
            if (view == null)
            {
                Plugin.Log.LogWarning($"Could not find PhotonView for ViewID: {viewId}. Ignoring remove request.");
                return;
            }

            Plugin.Log.LogInfo($"Deleting GameObject: {view.gameObject.name} using DeletionUtility.");
            // The host's DeletionUtility already handles networked destruction.
            DeletionUtility.Delete(view.gameObject);
            Plugin.Log.LogInfo($"Deletion request completed for GameObject: {view.gameObject.name}.");
        }
    }
}