using System;
using UnityEngine;

namespace PeakStranding.Data
{
    [Serializable]
    public class PlacedItemData
    {
        public string PrefabName;
        public Vector3 Position;
        public Quaternion Rotation;

        // Vine
        public Vector3 from, to, mid; // obsolete, use RopeStart as from, RopeEnd as to, RopeFlyingRotation as mid for vine instead
        public float hang; // obsolete, use RopeLength as hang for vine instead

        // Rope Shooter
        public Vector3 RopeStart, RopeEnd;
        public float RopeLength;
        public Vector3 RopeFlyingRotation;
        public Quaternion RopeAnchorRotation;
        public bool RopeAntiGrav;
    }
}