using Photon.Pun;
using UnityEngine;

namespace PeakStranding.Patches
{
    public class RopeSleepManager : MonoBehaviourPunCallbacks
    {
        private static bool initialized = false;

        public static void Initialize()
        {
            if (!initialized)
            {
                GameObject managerObject = new GameObject("RopeSleepManager");
                managerObject.AddComponent<RopeSleepManager>();
                DontDestroyOnLoad(managerObject);
                initialized = true;
            }
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                Plugin.Log.LogError($"[RopeOptimizer] Player {newPlayer.NickName} entered. Waking up all ropes for synchronization.");
                Rope[] allRopes = FindObjectsByType<Rope>(sortMode: FindObjectsSortMode.None);
                foreach (Rope rope in allRopes)
                {
                    if (rope.GetData().IsSleeping)
                    {
                        rope.WakeUp();
                    }
                }
            }
        }
    }
}