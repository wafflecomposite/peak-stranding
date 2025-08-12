using System;
using System.Collections.Generic;
using System.Linq;
using PeakStranding.Online;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Reflection;

namespace PeakStranding.Components
{
    public class OverlayManager : MonoBehaviour
    {
        private static OverlayManager? _instance;
        public static OverlayManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PeakStranding_OverlayManager");
                    _instance = go.AddComponent<OverlayManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        // Reflection helper to get Rope.attachedToAnchor -> GameObject
        private static GameObject? TryGetRopeAnchorObject(Rope rope)
        {
            try
            {
                var fi = typeof(Rope).GetField("attachedToAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi == null) return null;
                var anchor = fi.GetValue(rope) as RopeAnchor;
                return anchor != null ? anchor.gameObject : null;
            }
            catch
            {
                return null;
            }
        }

        public const int MaxOverlays = 10;
        public const float MaxDistance = 10f; // meters
        private const float MaxDistanceSqr = MaxDistance * MaxDistance;
        private const int ThrottleFrames = 10; // recompute culling every N frames

        // Debug: show all overlays always (no limits, no edge fade). Toggle before build.
        public static bool DebugShowAll = true;

        private readonly List<Entry> _entries = new();
        private readonly List<Entry> _visible = new();
        private Camera _cam = null!;
        private int _nextCullFrame;

        private GUIStyle _labelStyle = null!;
        private Texture2D _bgTex = Texture2D.whiteTexture;

        // UI scale for overlay sizing
        public static float UiScale = 1.2f;
        public static int MaxNameChars = 20;

        // Keybinds (configurable later via Plugin config if desired)
        public static KeyCode LikeKey = KeyCode.L;
        public static KeyCode RemoveKey = KeyCode.Delete;
        public static float RemoveHoldSeconds = 0.6f;

        // Permissions pushed from the host
        public static bool ClientsCanLike = true;
        public static bool ClientsCanDelete = true;

        // Focus and hold state
        private Entry? _focused;
        private float _removeHeldTime;
        // removed global visual scale; using per-entry Entry.removeS
        private Entry? _lastFocused;

        public struct RegisterInfo
        {
            public GameObject target;
            public string username;
            public int likes;
            public ulong id; // server id (0 for local-only)
            public ulong user_id;
            public bool canLike;
        }

        // Made public to be accessible by the sync manager
        public class Entry
        {
            public Transform t = null!;
            public string username = string.Empty;
            public int likes;
            public ulong id;
            public ulong user_id;
            public bool canLike = true;
            public GameObject go = null!;
            // Like feedback
            public System.Collections.Generic.List<Floater> floaters = new();
            public float likeTickT; // 0..1 animation timer
            // Delete-hold visual state (per-entry)
            public float removeS; // 0..1 width scale
            public bool removing; // hard lock to deletion visuals to avoid 1-frame pop
        }

        public struct Floater
        {
            public float t;           // elapsed time
            public float duration;    // total lifetime
            public float xJitter;     // horizontal offset
            public float scale;       // initial scale
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _cam = Camera.main;
            _bgTex = Texture2D.whiteTexture;
        }

        private void EnsureGui()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * UiScale),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
        }

        public static void Register(RegisterInfo info)
        {
            if (info.target == null) return;
            var inst = Instance;

            // Normalize to group root if present
            var group = info.target.GetComponentInParent<DeletableGroup>();
            if (group != null)
            {
                var root = group.gameObject;
                if (inst._entries.Any(e => e.go == root)) return;
                inst._entries.Add(new Entry
                {
                    go = root,
                    t = root.transform,
                    username = info.username ?? "",
                    likes = Mathf.Max(0, info.likes),
                    id = info.id,
                    user_id = info.user_id,
                    canLike = info.canLike
                });
                return;
            }

            // Rope fallback: if target is a Rope, try to find its attached anchor's group via reflection
            var rope = info.target.GetComponent<Rope>();
            if (rope != null)
            {
                var anchorGo = TryGetRopeAnchorObject(rope);
                if (anchorGo != null)
                {
                    var ag = anchorGo.GetComponent<DeletableGroup>();
                    if (ag != null)
                    {
                        var root = ag.gameObject;
                        if (!inst._entries.Any(e => e.go == root))
                        {
                            inst._entries.Add(new Entry
                            {
                                go = root,
                                t = root.transform,
                                username = info.username ?? "",
                                likes = Mathf.Max(0, info.likes),
                                id = info.id,
                                user_id = info.user_id,
                                canLike = info.canLike
                            });
                        }
                        return;
                    }
                }
            }

            // Fallback: register the provided target
            if (inst._entries.Any(e => e.go == info.target)) return;
            inst._entries.Add(new Entry
            {
                go = info.target,
                t = info.target.transform,
                username = info.username ?? "",
                likes = Mathf.Max(0, info.likes),
                id = info.id,
                user_id = info.user_id,
                canLike = info.canLike
            });
        }

        public static void Unregister(GameObject target)
        {
            if (target == null || _instance == null) return;
            _instance._entries.RemoveAll(e => e.go == target);
            _instance._visible.RemoveAll(e => e.go == target);
        }

        public List<Entry> GetAllEntriesForSync()
        {
            // Return a copy to prevent external modification
            return new List<Entry>(_entries);
        }

        public void ApplyLikeBroadcast(GameObject target, int newLikeCount, bool canLike)
        {
            var entry = _entries.FirstOrDefault(e => e.go == target);
            if (entry != null)
            {
                entry.likes = newLikeCount;
                entry.canLike = canLike;
                if (entry.floaters == null) entry.floaters = new System.Collections.Generic.List<Floater>(4);
                var f = new Floater
                {
                    t = 0f,
                    duration = 0.5f,
                    xJitter = UnityEngine.Random.Range(-8f * UiScale, 8f * UiScale),
                    scale = UnityEngine.Random.Range(0.9f, 1.1f)
                };
                entry.floaters.Add(f);
                entry.likeTickT = 1f;
            }
        }

        public Entry FindEntry(GameObject target)
        {
            return _entries.FirstOrDefault(e => e.go == target);
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Remove destroyed targets
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].go == null)
                {
                    _entries.RemoveAt(i);
                }
            }

            // Normalize entries to group roots if groups were added after registration
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.go == null) continue;
                var group = e.go.GetComponentInParent<DeletableGroup>();
                if (group != null && group.gameObject != e.go)
                {
                    var root = group.gameObject;
                    // If an entry for the root already exists, drop this one
                    if (_entries.Any(x => x.go == root))
                    {
                        _entries.RemoveAt(i);
                        i--;
                        continue;
                    }
                    // Remap to root
                    e.go = root;
                    e.t = root.transform;
                    _entries[i] = e;
                }
                else
                {
                    // If this is a Rope without a parent group, try to map to its anchor's group via reflection
                    var rope = e.go.GetComponent<Rope>();
                    if (rope != null)
                    {
                        var anchorGo = TryGetRopeAnchorObject(rope);
                        if (anchorGo != null)
                        {
                            var ag = anchorGo.GetComponent<DeletableGroup>();
                            if (ag != null)
                            {
                                var root = ag.gameObject;
                                if (_entries.Any(x => x.go == root))
                                {
                                    _entries.RemoveAt(i);
                                    i--;
                                    continue;
                                }
                                e.go = root;
                                e.t = root.transform;
                                _entries[i] = e;
                            }
                        }
                    }
                }
            }

            if (Time.frameCount >= _nextCullFrame)
            {
                _nextCullFrame = Time.frameCount + ThrottleFrames;
                RebuildVisible();
            }

            UpdateFocus();
            HandleInput();
        }

        private void RebuildVisible()
        {
            _visible.Clear();
            if (_entries.Count == 0) return;

            if (DebugShowAll)
            {
                // Render everything, no distance or count limit
                _visible.AddRange(_entries);
                return;
            }

            var camPos = _cam.transform.position;
            // pre-filter by distance
            var filtered = _entries
                .Select(e => (e, d2: (e.t.position - camPos).sqrMagnitude))
                .Where(x => x.d2 <= MaxDistanceSqr)
                .OrderBy(x => x.d2)
                .Take(MaxOverlays)
                .Select(x => x.e);
            _visible.AddRange(filtered);
        }

        private void UpdateFocus()
        {
            _focused = null;
            if (_visible.Count == 0 || _cam == null) return;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float bestDist = float.MaxValue;
            foreach (var e in _visible)
            {
                if (e.t == null) continue;
                Vector3 sp = _cam.WorldToScreenPoint(e.t.position + Vector3.up * 0.6f);
                if (sp.z < 0f) continue;
                var p = new Vector2(sp.x, Screen.height - sp.y);
                float d = Vector2.SqrMagnitude(p - screenCenter);
                if (d < bestDist)
                {
                    bestDist = d;
                    _focused = e;
                }
            }
            // Reset hold progress if focus changed
            if (_focused == null)
                _removeHeldTime = 0f;
        }

        private void HandleInput()
        {
            if (_focused == null) { _removeHeldTime = 0f; return; }

            bool canLike = _focused.canLike && (PhotonNetwork.IsMasterClient || ClientsCanLike);
            bool canDelete = PhotonNetwork.IsMasterClient || ClientsCanDelete;

            // Like on key down
            if (canLike && Input.GetKeyDown(LikeKey))
            {
                TryLike(_focused);
            }

            // Remove on hold
            if (canDelete && Input.GetKey(RemoveKey))
            {
                _removeHeldTime += Time.unscaledDeltaTime;
                if (_removeHeldTime >= RemoveHoldSeconds)
                {
                    TryRemove(_focused);
                    _removeHeldTime = 0f; // reset after action
                }
            }
            else
            {
                _removeHeldTime = 0f;
            }
        }

        private void OnGUI()
        {
            if (_cam == null || _visible.Count == 0) return;
            EnsureGui();

            // Reset visual state when focus changes to avoid spillover
            if (_lastFocused != _focused)
            {
                if (_lastFocused != null)
                {
                    // Do not reset visuals if the last focused overlay is in removing state
                    if (!_lastFocused.removing)
                    {
                        _lastFocused.removeS = 1f;
                        _lastFocused.likeTickT = 0f;
                        _lastFocused.floaters?.Clear();
                    }
                }
                _lastFocused = _focused;
            }

            foreach (var e in _visible)
            {
                if (e.t == null) continue;
                var wp = e.t.position + Vector3.up * 0.1f; // slight offset up
                var sp = _cam.WorldToScreenPoint(wp);
                if (sp.z < 0f) continue;
                sp.y = Screen.height - sp.y;

                // Visibility based on distance to nearest screen edge (edge=0, center=1)
                float visibility;
                if (DebugShowAll)
                {
                    visibility = 1f; // no edge fade in debug mode
                }
                else
                {
                    // Normalize per-axis to its own half-dimension to avoid widescreen bias
                    float dx = Mathf.Min(sp.x, Screen.width - sp.x) / Mathf.Max(1f, 0.5f * Screen.width);
                    float dy = Mathf.Min(sp.y, Screen.height - sp.y) / Mathf.Max(1f, 0.5f * Screen.height);
                    float centerRatio = Mathf.Clamp01(Mathf.Min(dx, dy));
                    // Invisible 0-70%, fade 70-90%, 90-100% fully visible
                    visibility = 0f;
                    if (centerRatio > 0.5f && centerRatio < 0.7f)
                    {
                        visibility = Mathf.Clamp01((centerRatio - 0.5f) / 0.2f);
                    }
                    else if (centerRatio >= 0.7f)
                    {
                        visibility = 1f;
                    }
                    if (visibility <= 0f) continue;
                }

                // Prepare content strings
                string displayName = e.username;
                if (!string.IsNullOrEmpty(displayName) && displayName.Length > MaxNameChars)
                {
                    displayName = displayName.Substring(0, MaxNameChars) + "…";
                }
                var content = new GUIContent(displayName);
                float maxWidth = 220f * UiScale;
                // Stabilize single-line height to avoid reflow/glitches when rect shrinks
                float lineH = _labelStyle.lineHeight > 0 ? _labelStyle.lineHeight : _labelStyle.CalcHeight(new GUIContent("A"), 1000f);
                float height = lineH;
                bool isFocused = (e == _focused);
                float extra = isFocused ? 20f * UiScale : 10f * UiScale; // non-focused is slimmer
                var rect = new Rect(sp.x - maxWidth * 0.5f, sp.y - height - 18f * UiScale, maxWidth, height + extra);

                // Delete-hold visual: center-pivot horizontal shrink with ease-out and tint/fade
                float targetS = 1f;
                if (isFocused)
                {
                    float pct = Mathf.Clamp01(_removeHeldTime / RemoveHoldSeconds);
                    float ease = 1f - Mathf.Pow(1f - pct, 3f); // ease-out cubic
                    targetS = 1f - ease;
                }
                if (e.removing)
                {
                    targetS = 0f; // lock to fully shrunk while removal is in-flight
                }
                // Smooth towards target per-entry
                if (e.removeS <= 0f) e.removeS = 1f; // default init
                e.removeS = Mathf.MoveTowards(e.removeS, targetS, Time.unscaledDeltaTime * 8f);
                float s = (isFocused || e.removing) ? e.removeS : 1f;
                // Always let non-focused relax back to full (unless it's in removing state)
                if (!isFocused && !e.removing)
                {
                    if (e.removeS < 1f) e.removeS = Mathf.MoveTowards(e.removeS, 1f, Time.unscaledDeltaTime * 12f);
                }
                if (s < 1f - 0.0001f && isFocused)
                {
                    float scaledW = rect.width * Mathf.Clamp01(s);
                    float cx = rect.x + rect.width * 0.5f;
                    rect = new Rect(cx - scaledW * 0.5f, rect.y, scaledW, rect.height);
                }

                // no like pulse scaling

                // background (apply slight red tint and fade as s goes to 0 when focused)
                var old = GUI.color;
                float tintT = (isFocused ? (1f - Mathf.Clamp01(s)) : 0f);
                // fade content a bit when shrinking
                float fadeMul = isFocused ? Mathf.Lerp(1f, 0.6f, tintT) : 1f;
                var bgCol = Color.Lerp(new Color(0f, 0f, 0f, 0.35f * visibility * fadeMul), new Color(0.3f, 0f, 0f, 0.4f * visibility), tintT * 0.8f);
                GUI.color = bgCol;
                GUI.DrawTexture(rect, _bgTex);
                GUI.color = old;

                // Skip group if rect is too small to render safely
                if (rect.width < 2f || rect.height < 2f)
                {
                    continue;
                }
                // Begin a clipping group so inner content never spills out of the shrinking rect
                GUI.BeginGroup(rect);
                try
                {

                    // label with visibility alpha (independent from background alpha)
                    var dynLabelStyle = new GUIStyle(_labelStyle);
                    dynLabelStyle.normal.textColor = new Color(1f, 1f, 1f, visibility * fadeMul);
                    dynLabelStyle.clipping = TextClipping.Clip;
                    dynLabelStyle.alignment = TextAnchor.MiddleLeft;
                    dynLabelStyle.wordWrap = false; // prevent multi-line reflow during shrink

                    // Hard bounds: left area for name, right area for likes
                    float pad = 6f * UiScale;
                    float likesW = 80f * UiScale;
                    // Local-space rects (0,0 is top-left of group)
                    var nameRect = new Rect(pad, 2f, Mathf.Max(0f, rect.width - likesW - pad * 2f), height);
                    var likesRect = new Rect(rect.width - likesW - pad, 2f, likesW, height);

                    // Draw name (clipped in its rect)
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        GUI.Label(nameRect, displayName, dynLabelStyle);
                    }

                    // Likes tick scale (ease-out on likeTickT)
                    float tickT = Mathf.Clamp01(e.likeTickT);
                    float tickEase = 1f - Mathf.Pow(1f - tickT, 3f);
                    float likeScale = 1f + 0.2f * tickEase;
                    string likesPart = $"❤ {e.likes}";
                    var likesContent = new GUIContent(likesPart);
                    var oldMatrix = GUI.matrix;
                    // Align likes right within its rect while scaling around its right-center
                    var likesSize = dynLabelStyle.CalcSize(likesContent);
                    float lx = Mathf.Max(likesRect.x, likesRect.xMax - likesSize.x);
                    float ly = likesRect.y;
                    var pivot = new Vector2(lx + likesSize.x, ly + likesSize.y * 0.5f);
                    GUIUtility.ScaleAroundPivot(new Vector2(likeScale, likeScale), pivot);
                    // Draw likes into an exact rect to avoid overlap
                    GUI.Label(new Rect(lx, ly, likesSize.x, likesSize.y), likesContent, dynLabelStyle);
                    GUI.matrix = oldMatrix;
                    // Decay animation
                    if (e.likeTickT > 0f) e.likeTickT = Mathf.Max(0f, e.likeTickT - Time.unscaledDeltaTime * 4.5f);

                    // prompts only for focused (skip while removing)
                    if (isFocused && !e.removing)
                    {
                        bool canLike = e.canLike && (PhotonNetwork.IsMasterClient || ClientsCanLike);
                        bool canDelete = PhotonNetwork.IsMasterClient || ClientsCanDelete;
                        var prompts = new System.Collections.Generic.List<string>();
                        if (canLike) prompts.Add($"[{LikeKey}] Like");
                        if (canDelete) prompts.Add($"Hold [{RemoveKey}] Remove");
                        if (prompts.Count > 0)
                        {
                            var promptStyle = new GUIStyle(GUI.skin.label)
                            {
                                fontSize = Mathf.RoundToInt(12 * UiScale),
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = new Color(1f, 1f, 1f, 0.85f * visibility) }
                            };

                            float rowH = 16f * UiScale;
                            var promptsRect = new Rect(0f, rect.height - rowH - 2f * UiScale, rect.width, rowH);
                            GUI.Label(promptsRect, string.Join("    •    ", prompts), promptStyle);

                            // no separate progress bar; shrink animation communicates delete hold
                        }
                    }

                }
                finally
                {
                    GUI.EndGroup();
                }

                // Draw +1 floaters OUTSIDE the group so they can float beyond bounds
                if (e.floaters != null && e.floaters.Count > 0)
                {
                    float pad = 6f * UiScale;
                    float likesW = 80f * UiScale;
                    // likes rect in screen space
                    var likesRectScreen = new Rect(rect.x + rect.width - likesW - pad, rect.y + 2f, likesW, height);

                    var floaterStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(16 * UiScale), // bigger floaters
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };
                    for (int i = e.floaters.Count - 1; i >= 0; i--)
                    {
                        var f = e.floaters[i];
                        f.t += Time.unscaledDeltaTime;
                        float t01 = Mathf.Clamp01(f.t / Mathf.Max(0.0001f, f.duration));
                        float yOff = Mathf.Lerp(0f, -26f * UiScale, t01); // higher travel
                        float alpha = visibility * (1f - t01);
                        var col = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, alpha);
                        var fx = likesRectScreen.x + likesRectScreen.width * 0.5f + f.xJitter;
                        var fy = likesRectScreen.y + yOff;
                        var sz = floaterStyle.CalcSize(new GUIContent("+1"));
                        var fRect = new Rect(fx - sz.x * 0.5f, fy - sz.y * 0.5f, sz.x, sz.y);
                        var oldM = GUI.matrix;
                        GUIUtility.ScaleAroundPivot(new Vector2(f.scale * 1.2f, f.scale * 1.2f), new Vector2(fRect.x + sz.x * 0.5f, fRect.y + sz.y * 0.5f));
                        GUI.Label(fRect, "+1", floaterStyle);
                        GUI.matrix = oldM;
                        GUI.color = col;
                        if (t01 >= 1f)
                            e.floaters.RemoveAt(i);
                        else
                            e.floaters[i] = f;
                    }
                }
            }
        }

        private void TryLike(Entry e)
        {
            if (e.go == null) return;
            var pv = e.go.GetPhotonView();
            if (pv == null || PeakStrandingSyncManager.Instance == null) return;

            var manager = PeakStrandingSyncManager.Instance;
            int viewId = pv.ViewID;

            if (PhotonNetwork.IsMasterClient)
            {
                manager.RequestLikeFromHost(viewId);
            }
            else
            {
                manager.photonView.RPC("RequestLike_RPC", RpcTarget.MasterClient, viewId);
            }
        }

        private void TryRemove(Entry e)
        {
            if (e.go == null) return;

            // Lock deletion visuals to avoid last-frame pop
            e.removing = true;
            e.removeS = 0f;

            if (PhotonNetwork.IsMasterClient)
            {
                // Delegate deletion to utility for consistent behavior
                DeletionUtility.Delete(e.go);
            }
            else
            {
                if (e.go.GetPhotonView() != null && PeakStrandingSyncManager.Instance != null)
                {
                    var manager = PeakStrandingSyncManager.Instance;
                    var viewId = e.go.GetPhotonView().ViewID;
                    if (manager.photonView != null && manager.photonView.ViewID != 0)
                    {
                        manager.photonView.RPC("RequestRemove_RPC", RpcTarget.MasterClient, viewId);
                    }
                }
            }
        }
    }
}
