using System;
using UnityEngine;

namespace PeakStranding.Data
{
    [Serializable]
    public class PlacedItemData
    {
        public string PrefabName = string.Empty;
        public Vector3 Position;
        public Quaternion Rotation;

        // Rope Shooter
        public Vector3 RopeStart, RopeEnd;
        public float RopeLength;
        public Vector3 RopeFlyingRotation;
        public Quaternion RopeAnchorRotation;
        public bool RopeAntiGrav;

        public string Scene = string.Empty;
        public int MapSegment;

        public void AddCurrentRunContext()
        {
            Scene = DataHelper.GetCurrentSceneName();
            MapSegment = DataHelper.GetCurrentMapSegment();
        }
    }
}