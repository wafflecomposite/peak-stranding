using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
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
        public static bool DebugShowAll = false;

        private readonly List<Entry> _entries = new();
        private readonly List<Entry> _visible = new();
        private Camera _cam = null!;
        private int _nextCullFrame;

        private GUIStyle _labelStyle = null!;
        private Texture2D _bgTex = Texture2D.whiteTexture;

        // UI scale for overlay sizing
        public static float UiScale = 1.2f;
        public static int MaxNameChars = 20;

        // Keybinds (configurable via plugin config)
        public static KeyCode LikeShortcut = KeyCode.L;
        public static KeyCode RemoveShortcut = KeyCode.Delete;
        public static float RemoveHoldSeconds = 0.6f;

        // Permissions pushed from the host
        public static bool ClientsCanLike = true;
        public static bool ClientsCanDelete = true;

        // Focus and hold state
        private Entry? _focused;
        private float _removeHeldTime;
        // removed global visual scale; using per-entry Entry.removeS
        private Entry? _lastFocused;

        // Panel layout constants (compact)
        private const float ScreenMargin = 6f;
        private const float Pad = 6f;
        private const float RowGap = 3f;
        private const float MaxPanelWidth = 480f;   // grows as needed but clamped to screen
        private const float MinPanelWidth = 200f;

        // Styles dedicated for measured layout
        private GUIStyle _titleStyle;   // wraps
        private GUIStyle _likesStyle;   // no wrap, right aligned
        private GUIStyle _promptStyle;  // wraps (base); we'll use a no-wrap clone for manual rows
        private GUIStyle _floaterStyle; // no wrap, overflow


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
            if (_titleStyle != null) return;

            // one step smaller than before
            int titleSize = Mathf.RoundToInt(13 * UiScale); // was 14
            int promptSize = Mathf.RoundToInt(11 * UiScale); // was 12
            int floaterSize = Mathf.RoundToInt(15 * UiScale); // was 16

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };

            _likesStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSize,
                alignment = TextAnchor.UpperRight,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = promptSize,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,  // base style is wrap; we clone as no-wrap for manual rows
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
            };

            _floaterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = floaterSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };

            _bgTex = Texture2D.whiteTexture;
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
                if (entry.floaters == null) entry.floaters = new List<Floater>(4);
                var f = new Floater
                {
                    t = 0f,
                    duration = 0.5f,
                    xJitter = Random.Range(-8f * UiScale, 8f * UiScale),
                    scale = Random.Range(0.9f, 1.1f)
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

            if (!Plugin.CfgShowStructureOverlay)
            {
                _visible.Clear();
                _focused = null;
                _removeHeldTime = 0f;
                return;
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
            if (canLike && Input.GetKeyDown(LikeShortcut))
            {
                TryLike(_focused);
            }

            // Remove on hold
            if (canDelete && Input.GetKey(RemoveShortcut))
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
            if (!Plugin.CfgShowStructureOverlay) return;
            if (_cam == null || _visible.Count == 0) return;

            EnsureGui();

            // If focus changed, clear transient visuals for the previous entry (except while removing)
            if (_lastFocused != _focused)
            {
                if (_lastFocused != null && !_lastFocused.removing)
                {
                    _lastFocused.likeTickT = 0f;
                    _lastFocused.floaters?.Clear();
                }
                _lastFocused = _focused;
            }

            foreach (var e in _visible)
            {
                if (e.t == null) continue;

                // World to screen
                var wp = e.t.position + Vector3.up * 0.1f;
                var sp = _cam.WorldToScreenPoint(wp);
                if (sp.z < 0f) continue;
                sp.y = Screen.height - sp.y;

                // Visibility (edge fade)
                float visibility = 1f;
                if (!DebugShowAll)
                {
                    float dx = Mathf.Min(sp.x, Screen.width - sp.x) / Mathf.Max(1f, 0.5f * Screen.width);
                    float dy = Mathf.Min(sp.y, Screen.height - sp.y) / Mathf.Max(1f, 0.5f * Screen.height);
                    float centerRatio = Mathf.Clamp01(Mathf.Min(dx, dy));
                    visibility = 0f;
                    if (centerRatio > 0.5f && centerRatio < 0.7f) visibility = Mathf.Clamp01((centerRatio - 0.5f) / 0.2f);
                    else if (centerRatio >= 0.7f) visibility = 1f;
                    if (visibility <= 0f) continue;
                }

                bool isFocused = (e == _focused);

                // ---- Strings & contents (no truncation) ----
                string displayName = string.IsNullOrEmpty(e.username) ? "" : e.username; // full string, no cutting
                string likesStr = $"❤ {e.likes}";

                bool canLike = e.canLike && (PhotonNetwork.IsMasterClient || ClientsCanLike);
                bool canDelete = PhotonNetwork.IsMasterClient || ClientsCanDelete;

                // ---- Measure everything first ----
                float panelMaxW = Mathf.Min(MaxPanelWidth * UiScale, Screen.width - 2f * ScreenMargin);
                float panelMinW = Mathf.Min(panelMaxW, MinPanelWidth * UiScale);

                var nameContent = new GUIContent(displayName);
                var likesContent = new GUIContent(likesStr);

                // Measure likes size (no wrap)
                Vector2 likesSize = _likesStyle.CalcSize(likesContent);

                // Start with a width that comfortably fits likes and some name space (narrower default)
                float desiredW = Mathf.Clamp(likesSize.x + 150f * UiScale + 2f * Pad, panelMinW, panelMaxW);

                // We give name the remaining width beside likes; if too small, wrapping will increase height
                float nameW = Mathf.Max(40f * UiScale, desiredW - 2f * Pad - likesSize.x - 6f * UiScale);
                float nameH = string.IsNullOrEmpty(displayName) ? 0f : _titleStyle.CalcHeight(nameContent, nameW);

                // ---- Build prompts with controlled break (only between phrases) ----
                bool showPrompts = isFocused && !e.removing && (canLike || canDelete);

                string likePhrase   = canLike   ? $"[{LikeShortcut}]\u00A0Like" : "";                      // NBSP
                string removePhrase = canDelete ? $"Hold\u00A0[{RemoveShortcut}]\u00A0Remove" : "";        // NBSPs
                string sep          = (canLike && canDelete) ? "    •    " : "";

                // Measure with no-wrap prompt style
                var promptNoWrap = new GUIStyle(_promptStyle) { wordWrap = false, clipping = TextClipping.Overflow };
                Vector2 likeSizeP   = string.IsNullOrEmpty(likePhrase)   ? Vector2.zero : promptNoWrap.CalcSize(new GUIContent(likePhrase));
                Vector2 removeSizeP = string.IsNullOrEmpty(removePhrase) ? Vector2.zero : promptNoWrap.CalcSize(new GUIContent(removePhrase));
                Vector2 sepSizeP    = string.IsNullOrEmpty(sep)          ? Vector2.zero : promptNoWrap.CalcSize(new GUIContent(sep));

                float promptsWInner = desiredW - 2f * Pad;

                // Decide single line vs two lines; only break BETWEEN phrases
                bool singleLine = showPrompts &&
                                  (likeSizeP.x + sepSizeP.x + removeSizeP.x <= promptsWInner ||
                                   string.IsNullOrEmpty(removePhrase) || string.IsNullOrEmpty(likePhrase));

                float promptsH = 0f;
                if (showPrompts)
                {
                    if (singleLine)
                        promptsH = Mathf.Max(likeSizeP.y, removeSizeP.y);
                    else
                        promptsH = likeSizeP.y + RowGap + removeSizeP.y;
                }

                // Top row height is max(name, likes)
                float topRowH = Mathf.Max(nameH, likesSize.y);

                // Total panel height with padding
                float height = Pad + topRowH + (promptsH > 0f ? RowGap + promptsH : 0f) + Pad;

                // Position, clamped to screen with margins
                float x = sp.x - desiredW * 0.5f;
                float y = sp.y - height - 18f * UiScale;
                x = Mathf.Clamp(x, ScreenMargin, Screen.width - ScreenMargin - desiredW);
                y = Mathf.Clamp(y, ScreenMargin, Screen.height - ScreenMargin - height);

                var rect = new Rect(x, y, desiredW, height);

                // ---- Background (slight tint on focus) ----
                var old = GUI.color;
                float fadeMul = 1f;
                var bgCol = Color.Lerp(new Color(0f, 0f, 0f, 0.35f * visibility * fadeMul),
                                    new Color(0.3f, 0f, 0f, 0.4f * visibility),
                                    isFocused ? 0.25f : 0f);
                GUI.color = bgCol;
                GUI.DrawTexture(rect, _bgTex);
                GUI.color = old;

                // ---- Content (no clipping because group rect == measured size) ----
                GUI.BeginGroup(rect);
                try
                {
                    // Dynamic alpha per label
                    var titleStyle = new GUIStyle(_titleStyle);
                    titleStyle.normal.textColor = new Color(1f, 1f, 1f, visibility);

                    var likesStyle = new GUIStyle(_likesStyle);
                    likesStyle.normal.textColor = new Color(1f, 1f, 1f, visibility);

                    // Top row layout
                    var nameRect = new Rect(Pad, Pad, nameW, nameH);
                    var likesRectLocal = new Rect(rect.width - Pad - likesSize.x, Pad, likesSize.x, likesSize.y);

                    if (!string.IsNullOrEmpty(displayName))
                        GUI.Label(nameRect, nameContent, titleStyle);

                    // Like tick pulse (scales around right edge but won't affect layout)
                    float tickT = Mathf.Clamp01(e.likeTickT);
                    float tickEase = 1f - Mathf.Pow(1f - tickT, 3f);
                    float likeScale = 1f + 0.2f * tickEase;

                    var oldM = GUI.matrix;
                    var pivot = new Vector2(likesRectLocal.xMax, likesRectLocal.y + likesRectLocal.height * 0.5f);
                    GUIUtility.ScaleAroundPivot(new Vector2(likeScale, likeScale), pivot);
                    GUI.Label(likesRectLocal, likesContent, likesStyle);
                    GUI.matrix = oldM;

                    if (e.likeTickT > 0f)
                        e.likeTickT = Mathf.Max(0f, e.likeTickT - Time.unscaledDeltaTime * 4.5f);

                    // ----- Prompts with controlled break -----
                    if (showPrompts)
                    {
                        var promptStyle = new GUIStyle(promptNoWrap);
                        promptStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f * visibility);

                        var promptsRect = new Rect(Pad, nameRect.yMax + RowGap, rect.width - 2f * Pad, promptsH);

                        if (singleLine)
                        {
                            float lineW = likeSizeP.x + sepSizeP.x + removeSizeP.x;
                            float sx = promptsRect.x + Mathf.Max(0f, (promptsRect.width - lineW) * 0.5f);

                            var r = new Rect(sx, promptsRect.y, likeSizeP.x, likeSizeP.y);
                            if (!string.IsNullOrEmpty(likePhrase)) { GUI.Label(r, likePhrase, promptStyle); r.x += likeSizeP.x; }

                            if (!string.IsNullOrEmpty(sep)) { r.width = sepSizeP.x; GUI.Label(r, sep, promptStyle); r.x += sepSizeP.x; }

                            if (!string.IsNullOrEmpty(removePhrase)) { r.width = removeSizeP.x; GUI.Label(r, removePhrase, promptStyle); }
                        }
                        else
                        {
                            // Two lines: break strictly between the phrases
                            if (!string.IsNullOrEmpty(likePhrase))
                            {
                                var r1 = new Rect(
                                    promptsRect.x + (promptsRect.width - likeSizeP.x) * 0.5f,
                                    promptsRect.y,
                                    likeSizeP.x, likeSizeP.y);
                                GUI.Label(r1, likePhrase, promptStyle);
                            }
                            if (!string.IsNullOrEmpty(removePhrase))
                            {
                                var r2 = new Rect(
                                    promptsRect.x + (promptsRect.width - removeSizeP.x) * 0.5f,
                                    promptsRect.y + likeSizeP.y + RowGap,
                                    removeSizeP.x, removeSizeP.y);
                                GUI.Label(r2, removePhrase, promptStyle);
                            }
                        }
                    }

                    // Bottom delete-hold progress bar (stable size)
                    if (isFocused && canDelete && _removeHeldTime > 0f)
                    {
                        float pct = Mathf.Clamp01(_removeHeldTime / RemoveHoldSeconds);
                        float barH = Mathf.Max(3f * UiScale, 2f);
                        var barBg = new Rect(Pad, rect.height - Pad * 0.5f - barH, rect.width - 2f * Pad, barH);
                        var barFg = new Rect(barBg.x, barBg.y, barBg.width * pct, barBg.height);

                        var c = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.15f * visibility);
                        GUI.DrawTexture(barBg, _bgTex);
                        GUI.color = new Color(0.9f, 0.2f, 0.2f, 0.9f * visibility);
                        GUI.DrawTexture(barFg, _bgTex);
                        GUI.color = c;
                    }
                }
                finally
                {
                    GUI.EndGroup();
                }

                // ---- +1 floaters (outside group so they can float) ----
                if (e.floaters != null && e.floaters.Count > 0)
                {
                    // Likes rect in SCREEN space
                    Vector2 likesSizeScreen = _likesStyle.CalcSize(new GUIContent(likesStr));
                    var likesRectScreen = new Rect(rect.x + rect.width - Pad - likesSizeScreen.x, rect.y + Pad, likesSizeScreen.x, likesSizeScreen.y);

                    for (int i = e.floaters.Count - 1; i >= 0; i--)
                    {
                        var f = e.floaters[i];
                        f.t += Time.unscaledDeltaTime;
                        float t01 = Mathf.Clamp01(f.t / Mathf.Max(0.0001f, f.duration));
                        float yOff = Mathf.Lerp(0f, -26f * UiScale, t01);
                        float alpha = visibility * (1f - t01);

                        var col = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, alpha);

                        var fx = likesRectScreen.x + likesRectScreen.width * 0.5f + f.xJitter;
                        var fy = likesRectScreen.y + yOff;

                        var content = new GUIContent("+1");
                        var sz = _floaterStyle.CalcSize(content);
                        var fRect = new Rect(fx - sz.x * 0.5f, fy - sz.y * 0.5f, sz.x, sz.y);

                        var oldM2 = GUI.matrix;
                        GUIUtility.ScaleAroundPivot(new Vector2(f.scale * 1.2f, f.scale * 1.2f), new Vector2(fRect.x + sz.x * 0.5f, fRect.y + sz.y * 0.5f));
                        GUI.Label(fRect, content, _floaterStyle);
                        GUI.matrix = oldM2;

                        GUI.color = col;

                        if (t01 >= 1f) e.floaters.RemoveAt(i);
                        else e.floaters[i] = f;
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
