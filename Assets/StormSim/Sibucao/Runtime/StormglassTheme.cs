using TMPro;
using UnityEngine;

namespace StormSim.UI
{
    [CreateAssetMenu(menuName = "StormSim/Stormglass theme")]
    public sealed class StormglassTheme : ScriptableObject
    {
        public TMP_FontAsset headingFont, bodyFont;
        public Sprite roundedPanel;
        [System.NonSerialized] Sprite coastalPanel;
        [System.NonSerialized] Texture2D coastalTexture;

        // A small, nine-sliced chamfer keeps the low-poly silhouette at every
        // panel size. Flat edge facets replace soft glass and rounded corners.
        public Sprite PanelSprite
        {
            get
            {
                if (coastalPanel) return coastalPanel;
                const int size = 64, cut = 10;
                coastalTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Coastal Signal panel", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int edgeX = Mathf.Min(x, size - 1 - x), edgeY = Mathf.Min(y, size - 1 - y);
                    bool inside = edgeX + edgeY >= cut;
                    float shade = y < 2 || x >= size - 2 ? .78f : 1f;
                    pixels[y * size + x] = inside ? new Color(shade, shade, shade, 1) : UnityEngine.Color.clear;
                }
                coastalTexture.SetPixels(pixels); coastalTexture.Apply(false, true);
                coastalPanel = Sprite.Create(coastalTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
                coastalPanel.name = "Coastal Signal chamfer"; coastalPanel.hideFlags = HideFlags.HideAndDontSave;
                return coastalPanel;
            }
        }
        void OnDisable()
        {
            if (Application.isPlaying)
            {
                if (coastalPanel) Destroy(coastalPanel);
                if (coastalTexture) Destroy(coastalTexture);
            }
            else
            {
                if (coastalPanel) DestroyImmediate(coastalPanel);
                if (coastalTexture) DestroyImmediate(coastalTexture);
            }
            coastalPanel = null; coastalTexture = null;
        }
        public static Color Color(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        public static readonly Color Background = Color("0B1720"), Surface = Color("142B35"), Subtle = Color("24414B"),
            Ink = Color("F2F4ED"), Muted = Color("B8CBCB"), Accent = Color("65DAC2"), OnAccent = Color("09252F"),
            Warning = Color("EAB45D"), Danger = Color("FF9B9B"), Success = Color("9FE3B1");
    }
}
