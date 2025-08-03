using UnityEngine;

namespace PeakStranding.Data
{
    public class RestoredItemCredits : MonoBehaviour
    {
        private Camera _mainCam;

        public string displayText = "";

        private const float kDistanceSqr = 10f * 10f;   // compare squared distances
        private const int kLazyInterval = 30;         // frames between checks when far

        private bool _isNear;
        private int _nextCheckFrame;

        private void Awake()
        {
            _mainCam = Camera.main;
            if (_mainCam == null)
                Plugin.Log.LogError("Main camera not found.");
            _nextCheckFrame = kLazyInterval;            // first check after 30 frames
        }

        private void Update()
        {
            if (_mainCam == null || string.IsNullOrEmpty(displayText)) return;

            // When far, only test every kLazyInterval frames.
            // When near, test every frame for snappy exit.
            if (!_isNear && Time.frameCount < _nextCheckFrame) return;

            bool nowNear = (_mainCam.transform.position - transform.position).sqrMagnitude <= kDistanceSqr;

            if (_isNear != nowNear)
            {
                _isNear = nowNear;

                // just left range → resume lazy polling
                if (!_isNear) _nextCheckFrame = Time.frameCount + kLazyInterval;
            }
            else if (!_isNear) // still far, schedule next lazy check
            {
                _nextCheckFrame = Time.frameCount + kLazyInterval;
            }
        }

        private void OnGUI()
        {
            if (!_isNear || _mainCam == null || string.IsNullOrEmpty(displayText)) return;

            // Convert world → screen
            Vector3 sp = _mainCam.WorldToScreenPoint(transform.position);
            if (sp.z < 0f) return;          // behind camera

            sp.y = Screen.height - sp.y;    // flip Y for GUI

            GUIContent content = new GUIContent(displayText);
            GUIStyle style = GUI.skin.label;
            style.fontSize = 18;
            style.alignment = TextAnchor.MiddleCenter;  // Center align the text
            style.wordWrap = true;
            style.normal.textColor = Color.white;  // Make text white for better contrast

            // Make the rect wider to accommodate longer text
            float maxWidth = 200f;
            float height = style.CalcHeight(content, maxWidth);
            float padding = 2f;

            Rect r = new Rect(sp.x - maxWidth * 0.5f,
                            sp.y - height * 0.5f - padding,
                            maxWidth,
                            height + padding * 2);  // Add padding to height

            // Draw semi-transparent black background
            GUI.color = new Color(0, 0, 0, 0.15f);  // Last value is alpha (0.7 = 70% opacity)
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;  // Reset color for text

            // Draw text
            GUI.Label(r, content, style);
        }
    }
}
