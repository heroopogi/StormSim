using TMPro;
using UnityEngine;

namespace StormSim.UI
{
    [CreateAssetMenu(menuName = "StormSim/Stormglass theme")]
    public sealed class StormglassTheme : ScriptableObject
    {
        public TMP_FontAsset headingFont, bodyFont;
        public Sprite roundedPanel;
        public static Color Color(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        public static readonly Color Background = Color("091721"), Surface = Color("122A38"), Subtle = Color("1C3B4B"),
            Ink = Color("F0F8FB"), Muted = Color("B0C8D4"), Accent = Color("80E2E1"), OnAccent = Color("09252F"), Warning = Color("FFD09B");
    }
}
