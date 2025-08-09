/*
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakStranding.Components
{
    // Handles cross-client deletion of non-networked MagicBeanVine by broadcasting a position-based event
    public class VineEvents : MonoBehaviour
    {
        public static VineEvents Instance { get; private set; }
        public const byte PS_VINE_DELETE = 161; // custom event code
        public const float MatchRadius = 2.5f;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("PeakStranding_VineEvents");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VineEvents>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
        }

        private void OnDestroy()
        {
            if (PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.NetworkingClient.EventReceived -= OnEventReceived;
            }
        }

        public void SendDeleteVine(Vector3 position)
        {
            var content = new object[] { position.x, position.y, position.z };
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(PS_VINE_DELETE, content, options, SendOptions.SendReliable);
        }

        private void OnEventReceived(EventData evt)
        {
            if (evt == null || evt.Code != PS_VINE_DELETE) return;
            if (evt.CustomData is not object[] arr || arr.Length < 3) return;
            Vector3 pos = new Vector3(
                (float)arr[0],
                (float)arr[1],
                (float)arr[2]
            );

            // Find nearest MagicBeanVine and destroy it if within radius
            var vines = FindObjectsByType<MagicBeanVine>(sortMode: FindObjectsSortMode.None);
            if (vines == null || vines.Length == 0) return;
            var nearest = vines
                .Select(v => (v, d2: (v.transform.position - pos).sqrMagnitude))
                .OrderBy(x => x.d2)
                .FirstOrDefault();
            if (nearest.v == null) return;
            if (nearest.d2 <= MatchRadius * MatchRadius)
            {
                Destroy(nearest.v.gameObject);
            }
        }
    }
}
*/