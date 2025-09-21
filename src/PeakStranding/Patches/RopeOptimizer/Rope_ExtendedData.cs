using Photon.Pun;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeakStranding.Patches
{
    public class RopeData
    {
        public bool IsSleeping;
        public float SleepCountdown = -1f;
        public bool WasBeingClimbed;

        public float NextSleepSyncTime = 0f;
    }

    public static class Rope_ExtendedData
    {
        private static readonly ConditionalWeakTable<Rope, RopeData> AllRopeData = new();
        public const float SleepDelay = 15.0f;

        public static RopeData GetData(this Rope rope)
        {
            return AllRopeData.GetValue(rope, _ => new RopeData());
        }

        public static void GoToSleep(this Rope rope)
        {
            var data = rope.GetData();
            if (data.IsSleeping) return;

            // Update local state immediately to avoid duplicate RPCs before the
            // network message is processed. The RPC will mirror this state on
            // all other clients.
            data.IsSleeping = true;
            data.SleepCountdown = -1f;
            data.WasBeingClimbed = false;
            data.NextSleepSyncTime = Time.unscaledTime + 1.0f;

            rope.GetComponent<PhotonView>().RPC("EnterSleepState_RPC", RpcTarget.All);
        }

        public static void WakeUp(this Rope rope)
        {
            var data = rope.GetData();
            if (!data.IsSleeping) return;

            // Initialize sleep tracking state locally. The RPC will propagate
            // the same values to the rest of the clients.
            data.IsSleeping = false;
            data.SleepCountdown = SleepDelay;
            data.WasBeingClimbed = false;
            data.NextSleepSyncTime = 0f;

            rope.GetComponent<PhotonView>().RPC("ExitSleepState_RPC", RpcTarget.All);
        }

        public static void UpdateSleepLogic(Rope rope)
        {
            var data = rope.GetData();
            if (data.IsSleeping) return;

            bool isBeingClimbed = rope.charactersClimbing.Count > 0;

            // If a player was climbing but now isn't, start the countdown.
            if (data.WasBeingClimbed && !isBeingClimbed)
            {
                data.SleepCountdown = SleepDelay;
            }

            data.WasBeingClimbed = isBeingClimbed;

            // If a countdown is active and no one is climbing, tick it down.
            if (data.SleepCountdown > 0 && !isBeingClimbed)
            {
                data.SleepCountdown -= Time.deltaTime;
                if (data.SleepCountdown <= 0)
                {
                    rope.GoToSleep();
                    data.SleepCountdown = -1f; // Deactivate timer
                    //data.SleepCountdown = 10f; // Reset to 10 seconds for next sleep
                }
            }
        }
    }
}
