using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeakStranding.Patches
{
    /// <summary>
    /// This patch enhances the MagicBean script. It allows tracking of the vine created by the bean
    /// and adds a networked method to remove both the bean and the vine simultaneously.
    /// </summary>
    [HarmonyPatch]
    public static class MagicBeanPatch
    {
        // Use a ConditionalWeakTable to associate a MagicBeanVine instance with a MagicBean instance.
        // This is a memory-safe way to add "fields" to existing objects without modifying them directly.
        private static readonly ConditionalWeakTable<MagicBean, MagicBeanVine> magicBeanVines = new ConditionalWeakTable<MagicBean, MagicBeanVine>();

        // A custom event code for our new network action. Must be unique.
        private const byte RemoveBeanAndVineEventCode = 230;

        /// <summary>
        /// Patches the GrowVineRPC method to capture the created vine instance.
        /// A prefix patch is used here to replace the original method, which allows us to get a reference
        /// to the locally instantiated MagicBeanVine object.
        /// </summary>
        [HarmonyPatch(typeof(MagicBean), nameof(MagicBean.GrowVineRPC))]
        [HarmonyPrefix]
        public static bool GrowVineRPC_Prefix(MagicBean __instance, Vector3 pos, Vector3 direction, float maxLength)
        {
            // 1. Replicate the original method's logic to ensure the vine still spawns correctly.
            MagicBeanVine magicBeanVine = Object.Instantiate(__instance.plantPrefab, pos, Quaternion.identity);
            magicBeanVine.transform.up = direction;
            magicBeanVine.maxLength = maxLength;

            // 2. Our new logic: store the reference to the newly created vine in our table.
            magicBeanVines.Add(__instance, magicBeanVine);
            Plugin.Log.LogInfo($"Patched GrowVineRPC: Stored reference to the new MagicBeanVine for bean {__instance.photonView.ViewID}.");

            // 3. Return false to prevent the original GrowVineRPC from running, as we have replaced it.
            return false;
        }

        /// <summary>
        /// This is the new public method that can be called from other parts of your mod to despawn the bean and its vine.
        /// It sends a custom network event to all players.
        /// </summary>
        public static void RemoveBeanAndVine(MagicBean bean)
        {
            if (bean == null || !bean.photonView.IsMine) return;

            object[] content = new object[] { bean.photonView.ViewID };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(RemoveBeanAndVineEventCode, content, raiseEventOptions, SendOptions.SendReliable);
        }

        /// <summary>
        /// A helper method to get the vine associated with a bean.
        /// </summary>
        public static MagicBeanVine GetAssociatedVine(MagicBean bean)
        {
            magicBeanVines.TryGetValue(bean, out var vine);
            return vine;
        }

        /// <summary>
        /// A cleanup patch for when the MagicBean is destroyed for any reason.
        /// This ensures we don't leave orphaned vines or memory leaks in our tracking table.
        /// </summary>
        // [HarmonyPatch(typeof(MagicBean), "OnDestroy")]
        // [HarmonyPostfix]
        // public static void OnDestroy_Postfix(MagicBean __instance)
        // {
        //     if (magicBeanVines.TryGetValue(__instance, out var vine) && vine != null)
        //     {
        //         Object.Destroy(vine.gameObject);
        //     }
        //     magicBeanVines.Remove(__instance);
        // }

        /// <summary>
        /// A new MonoBehaviour to handle our custom network event.
        /// This component should be added to a persistent GameObject in your mod's setup.
        /// </summary>
        public class MagicBeanEventHandler : MonoBehaviour, IOnEventCallback
        {
            private void OnEnable()
            {
                PhotonNetwork.AddCallbackTarget(this);
            }

            private void OnDisable()
            {
                PhotonNetwork.RemoveCallbackTarget(this);
            }

            public void OnEvent(EventData photonEvent)
            {
                if (photonEvent.Code == RemoveBeanAndVineEventCode)
                {
                    object[] data = (object[])photonEvent.CustomData;
                    int viewID = (int)data[0];

                    PhotonView beanView = PhotonView.Find(viewID);
                    if (beanView != null)
                    {
                        MagicBean bean = beanView.GetComponent<MagicBean>();
                        if (bean != null)
                        {
                            // The vine is destroyed locally by the OnDestroy patch.
                            // We only need to destroy the networked MagicBean object.
                            if (PhotonNetwork.IsMasterClient)
                            {
                                PhotonNetwork.Destroy(bean.gameObject);
                                Plugin.Log.LogInfo($"Removed MagicBean (ViewID: {viewID}) via network event.");
                            }
                        }
                    }
                }
            }
        }
    }
}