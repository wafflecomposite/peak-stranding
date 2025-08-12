using System;

namespace PeakStranding.Data
{
    /// <summary>
    /// Data Transfer Object for synchronizing essential structure metadata
    /// from the host to clients.
    /// </summary>
    [Serializable]
    public class StructureSyncData
    {
        public int ViewID { get; set; }
        public string? Username { get; set; }
        public int Likes { get; set; }
        public ulong ServerId { get; set; }
        public ulong UserId { get; set; }
    }
}