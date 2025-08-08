using HarmonyLib;
using Photon.Pun;

namespace PeakStranding.Patches
{
    [HarmonyPatch(typeof(MapHandler), nameof(MapHandler.GoToSegment))]
    public static class MapHandlerGoToSegmentPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapHandler __instance, Segment s)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int newSegment = (int)s;
            int previousSegment = newSegment - 1; // Segments are advanced sequentially

            Plugin.Log.LogInfo($"Map segment changing to {newSegment}. Updating structures.");

            // Despawn structures from the segment we just left
            SaveManager.DespawnStructuresForSegment(previousSegment);

            // Spawn structures for the new segment we are entering
            SaveManager.SpawnStructuresForSegment(newSegment);
        }
    }
}