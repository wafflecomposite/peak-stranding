using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PeakStranding.Online
{
    // Buffers likes per structure. When a structure hasn't received a like for 2s,
    // it's scheduled for dispatch. Dispatches are globally throttled to 1 req / 2s.
    public class LikeBuffer : MonoBehaviour
    {
        private struct Entry
        {
            public int Count;
            public float LastLikeAt; // realtimeSinceStartup
        }

        private static LikeBuffer _instance;
        private readonly Dictionary<ulong, Entry> _buffer = new();
        private readonly Queue<(ulong id, int count)> _readyToSend = new();
        private float _lastSentAt = -999f;

        private const float InactivitySeconds = 2f; // per-structure inactivity before batching is sealed
        private const float GlobalCooldown = 2f;    // min delay between outbound requests

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PeakStranding_LikeBuffer");
            _instance = go.AddComponent<LikeBuffer>();
            DontDestroyOnLoad(go);
        }

        public static void Enqueue(ulong structureId)
        {
            // if (structureId == 0) return; // local-only, nothing to send
            Ensure();
            _instance.EnqueueInternal(structureId);
        }

        private void EnqueueInternal(ulong id)
        {
            if (_buffer.TryGetValue(id, out var e))
            {
                e.Count += 1;
                e.LastLikeAt = Time.realtimeSinceStartup;
                _buffer[id] = e;
            }
            else
            {
                _buffer[id] = new Entry { Count = 1, LastLikeAt = Time.realtimeSinceStartup };
            }
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;

            // Move entries that have been inactive long enough into the ready queue
            if (_buffer.Count > 0)
            {
                s_tempIds.Clear();
                foreach (var kv in _buffer)
                {
                    if (now - kv.Value.LastLikeAt >= InactivitySeconds)
                    {
                        s_tempIds.Add(kv.Key);
                    }
                }
                foreach (var id in s_tempIds)
                {
                    var e = _buffer[id];
                    _buffer.Remove(id);
                    if (e.Count > 0)
                    {
                        _readyToSend.Enqueue((id, e.Count));
                    }
                }
            }

            // Respect global cooldown; send at most one request every GlobalCooldown seconds
            if (_readyToSend.Count > 0 && now - _lastSentAt >= GlobalCooldown)
            {
                var (id, count) = _readyToSend.Dequeue();
                _lastSentAt = now;
                _ = SendAsync(id, count);
            }
        }

        private async Task SendAsync(ulong id, int count)
        {
            try
            {
                await RemoteApi.LikeStructureAsync(id, count).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"Batched like request failed for {id} x{count}: {ex.Message}");
                // Optionally requeue on failure after a delay? For now, drop to prevent infinite retry loops.
            }
        }

        // temp list to avoid allocations during buffer scan
        private static readonly List<ulong> s_tempIds = new(16);
    }
}
