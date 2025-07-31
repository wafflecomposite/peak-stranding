using System;
using UnityEngine;

namespace PeakStranding
{
    [Serializable]
    public class PlacedItemData
    {
        public string PrefabName;
        public Vector3 Position;
        public Quaternion Rotation;


        // Vine
        public Vector3 from, to, mid;
        public float hang;

        // Rope Shooter
        public Vector3 RopeStart, RopeEnd;
        public float RopeLength;
        public Vector3 RopeFlyingRotation;
        public Quaternion RopeAnchorRotation;
        public bool RopeAntiGrav;

        // Rope Spool
        public float SpoolSegments;
    }
}