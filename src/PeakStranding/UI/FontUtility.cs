using TMPro;

namespace PeakStranding.UI
{
    public static class FontUtility
    {
        private static TMP_FontAsset _font;

        /// <summary>
        /// Attempts to retrieve and cache a TMP font from GUIManager when available.
        /// Returns true and sets the out param when a font is ready; otherwise false.
        /// </summary>
        public static bool TryGetFont(out TMP_FontAsset font)
        {
            if (_font == null)
            {
                if (GUIManager.instance != null &&
                    GUIManager.instance.heroDayText != null &&
                    GUIManager.instance.heroDayText.font != null)
                {
                    _font = GUIManager.instance.heroDayText.font;
                }
            }

            font = _font;
            return font != null;
        }
    }
}
