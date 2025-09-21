using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakStranding.UI
{
    public class ToastController : MonoBehaviour
    {
        public static ToastController Instance;

        private TextMeshProUGUI textMesh;
        private Coroutine clearCoroutine;
        private Coroutine fontCoroutine;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }

        private void Start()
        {
            Plugin.Log.LogInfo("Initializing Toast Controller...");

            var canvas = new GameObject("canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(transform);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvas.gameObject.AddComponent<CanvasScaler>();

            var textGO = new GameObject("text");
            textGO.transform.SetParent(canvas.transform, false);

            var rectTransform = textGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(1000, 100);

            // Anchor & pivot at TOP-RIGHT
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot     = new Vector2(1f, 1f);

            // Offset in from the top-right corner (x negative to move left)
            rectTransform.anchoredPosition = new Vector2(-5f, -20f);

            textMesh = textGO.AddComponent<TextMeshProUGUI>();
            textMesh.fontSize    = 26;
            textMesh.fontSizeMin = 26;
            textMesh.fontSizeMax = 26;

            // Align text inside the rect to top-right so it grows leftwards/downwards
            textMesh.alignment = TMPro.TextAlignmentOptions.TopRight;
            // (optional) keep long strings from running out of the rect
            // textMesh.enableWordWrapping = true;
            // textMesh.overflowMode = TMPro.TextOverflowModes.Ellipsis;

            Plugin.Log.LogInfo("Toast Controller Initialized!");

            // Poll for a usable TMP font (without UniTask)
            fontCoroutine = StartCoroutine(FindFontEventually());
        }

        public void Toast(string text, Color color, float duration = 2f, float fadeTime = 1f)
        {
            if (!Plugin.CfgShowToasts) return;
            if (textMesh == null) return;

            // Cancel/stop any in-flight fade
            if (clearCoroutine != null)
            {
                StopCoroutine(clearCoroutine);
                clearCoroutine = null;
            }

            textMesh.text = text;
            textMesh.color = color;
            textMesh.alpha = 1f;

            clearCoroutine = StartCoroutine(ClearToast(duration, fadeTime));
        }

        private IEnumerator ClearToast(float duration, float fadeTime = 1f)
        {
            if (textMesh == null) yield break;

            // Delay before starting fade
            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            float timeElapsed = 0f;

            // Per-frame fade
            while (timeElapsed < fadeTime)
            {
                timeElapsed += Time.deltaTime;
                float progress = fadeTime <= 0f ? 1f : Mathf.Clamp01(timeElapsed / fadeTime);
                textMesh.alpha = Mathf.Lerp(1f, 0f, progress);
                yield return null; // wait a frame
            }

            textMesh.alpha = 0f;
            clearCoroutine = null;
        }

        private IEnumerator FindFontEventually()
        {
            // Poll each frame until a font is available
            TMP_FontAsset font;
            while (!FontUtility.TryGetFont(out font))
                yield return null; // could use new WaitForSeconds(0.1f) if you prefer throttling

            if (textMesh != null)
                textMesh.font = font;

            fontCoroutine = null;
        }
    }
}
