using UnityEditor;
using UnityEngine;
using TMPro;
using StormSim.UI;

namespace StormSim.Editor
{
    public static class StormglassInstaller
    {
        const string Folder = "Assets/StormSim/Sibucao/Resources/Stormglass";
        [MenuItem("StormSim/Stormglass/Install theme")]
        public static void Install()
        {
            System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var theme = AssetDatabase.LoadAssetAtPath<StormglassTheme>(Folder + "/Theme.asset");
            if (!theme) { theme = ScriptableObject.CreateInstance<StormglassTheme>(); AssetDatabase.CreateAsset(theme, Folder + "/Theme.asset"); }
            theme.headingFont = Font("Sora"); theme.bodyFont = Font("Inter");
            string path = Folder + "/RoundedPanel.png";
            if (!System.IO.File.Exists(path))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    var q = new Vector2(Mathf.Abs(x - 31.5f), Mathf.Abs(y - 31.5f)) - Vector2.one * 3.5f;
                    float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude - 28;
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(.5f - distance)));
                }
                tex.Apply(); System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * 28; importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            theme.roundedPanel = AssetDatabase.LoadAssetAtPath<Sprite>(path); EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
            Debug.Log("Stormglass theme installed: Sora, Inter, rounded panels.");
        }
        static TMP_FontAsset Font(string name)
        {
            string path = Folder + "/" + name + " SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (asset) return asset;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/StormSim/Sibucao/UI/MainMenu/" + name + ".ttf");
            asset = TMP_FontAsset.CreateFontAsset(source); asset.name = name + " SDF"; AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures) if (atlas) AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;!?/()[]+−↑↓&•×—–’% ");
            EditorUtility.SetDirty(asset); return asset;
        }
    }
}
