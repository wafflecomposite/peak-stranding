using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace PeakStranding.Components
{
    // Attach to a networked root (must have PhotonView). Holds parts that should be deleted together.
    public class DeletableGroup : MonoBehaviourPun
    {
        [SerializeField] private List<PhotonView> _pvs = new();
        [SerializeField] private List<GameObject> _gos = new();

        public void Add(PhotonView pv)
        {
            if (pv == null) return;
            if (_pvs.Contains(pv)) return;
            _pvs.Add(pv);
        }

        public void Add(GameObject go)
        {
            if (go == null) return;
            if (_gos.Contains(go)) return;
            _gos.Add(go);
        }

        [PunRPC]
        public void PS_Delete()
        {
            // Destroy PV-backed objects via Photon to sync to everyone
            for (int i = _pvs.Count - 1; i >= 0; i--)
            {
                var pv = _pvs[i];
                if (pv == null) { _pvs.RemoveAt(i); continue; }
                if (pv != null)
                {
                    PhotonNetwork.Destroy(pv);
                }
            }
            // Destroy any loose GameObjects (local only)
            for (int i = _gos.Count - 1; i >= 0; i--)
            {
                var go = _gos[i];
                if (go == null) { _gos.RemoveAt(i); continue; }
                Destroy(go);
            }
        }
    }
}
