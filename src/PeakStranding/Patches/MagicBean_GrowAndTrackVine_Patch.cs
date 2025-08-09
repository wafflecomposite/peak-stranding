using HarmonyLib;
using UnityEngine;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(MagicBean), "GrowVineRPC")]
    public static class MagicBean_GrowAndTrackVine_Patch
    {
        // Replace original entirely: spawn vine, configure, and store ref on bean
        private static bool Prefix(MagicBean __instance, Vector3 pos, Vector3 direction, float maxLength)
        {
            var link = __instance.GetComponent<PeakStranding.Components.MagicBeanLink>();
            if (link != null && link.vine != null)
            {
                // Already spawned (likely due to both direct call and RPC); skip duplicate
                return false;
            }
            if (link == null) link = __instance.gameObject.AddComponent<PeakStranding.Components.MagicBeanLink>();

            var vine = Object.Instantiate(__instance.plantPrefab, pos, Quaternion.identity);
            vine.transform.up = direction;
            vine.maxLength = maxLength;
            link.vine = vine;
            return false; // skip original
        }
    }
}
