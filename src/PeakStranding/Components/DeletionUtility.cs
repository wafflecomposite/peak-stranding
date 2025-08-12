using Photon.Pun;
using System.Reflection;
using UnityEngine;

namespace PeakStranding.Components
{
    public static class DeletionUtility
    {
        // Reflection helper to get Rope.attachedToAnchor -> GameObject
        private static GameObject? TryGetRopeAnchorObject(Rope rope)
        {
            try
            {
                var fi = typeof(Rope).GetField("attachedToAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi == null) return null;
                var anchor = fi.GetValue(rope) as RopeAnchor;
                return anchor != null ? anchor.gameObject : null;
            }
            catch
            {
                return null;
            }
        }

        // Public entrypoint: delete an object and any linked rope/anchor/vine group if applicable
        public static void Delete(GameObject go)
        {
            if (go == null) return;
            if (go.name.Contains("ChainShootable"))
            {
                Plugin.Log.LogInfo("We are NOT deleting the Chain. Like NO. Main game bugs.");
                return;
            }

            // If there's a DeletableGroup, invoke its RPC (grouped deletion)
            var group = go.GetComponentInParent<DeletableGroup>();
            if (group != null && group.photonView != null)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    group.photonView.RPC("PS_Delete", RpcTarget.AllBuffered);
                }
                else
                {
                    Plugin.Log.LogInfo("Remove requested by non-host; only host can remove globally.");
                }
                return;
            }

            // Rope anchor-with-rope root
            if (PhotonNetwork.IsMasterClient)
            {
                var rapw = go.GetComponentInChildren<RopeAnchorWithRope>();
                if (rapw != null)
                {
                    // Delete rope first, then the anchor-with-rope root
                    if (rapw.rope != null && rapw.rope.photonView != null)
                        PhotonNetwork.Destroy(rapw.rope.photonView);
                    var rapwPv = rapw.GetComponent<PhotonView>();
                    if (rapwPv != null) PhotonNetwork.Destroy(rapwPv);
                    return;
                }

                // Plain RopeAnchor (may have an attached Rope)
                var anchor = go.GetComponentInChildren<RopeAnchor>();
                if (anchor != null)
                {
                    var ropeForAnchor = FindRopeForAnchor(anchor);
                    if (ropeForAnchor != null && ropeForAnchor.photonView != null)
                        PhotonNetwork.Destroy(ropeForAnchor.photonView);
                    if (anchor.TryGetComponent<PhotonView>(out var anchorPv))
                        PhotonNetwork.Destroy(anchorPv);
                    return;
                }

                // Bare Rope (try delete its anchor PV first, then the rope)
                var rope = go.GetComponentInChildren<Rope>();
                if (rope != null)
                {
                    var aGo = TryGetRopeAnchorObject(rope);
                    if (aGo != null && aGo.TryGetComponent<PhotonView>(out var aPv))
                        PhotonNetwork.Destroy(aPv);
                    if (rope.photonView != null)
                        PhotonNetwork.Destroy(rope.photonView);
                    return;
                }
            }

            // Magic bean and vine cleanup is handled by patches reacting to PV destruction.
            // Fallback: single object
            var pv = go.GetComponent<PhotonView>();
            if (PhotonNetwork.IsMasterClient)
            {
                if (pv != null) PhotonNetwork.Destroy(pv);
                else Object.Destroy(go);
            }
            else
            {
                Plugin.Log.LogInfo("Remove requested by non-host; only host can remove globally.");
            }
        }

        // Helper: find a Rope that is attached to the given RopeAnchor
        private static Rope? FindRopeForAnchor(RopeAnchor anchor)
        {
            if (anchor == null) return null;
            try
            {
                var allRopes = GameObject.FindObjectsByType<Rope>(sortMode: FindObjectsSortMode.None);
                foreach (var r in allRopes)
                {
                    var aGo = TryGetRopeAnchorObject(r);
                    if (aGo == null) continue;
                    if (aGo == anchor.gameObject) return r;
                    var aComp = aGo.GetComponent<RopeAnchor>();
                    if (aComp == anchor) return r;
                }
            }
            catch { }
            return null;
        }
    }
}
