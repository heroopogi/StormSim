using UnityEditor;
using UnityEngine;

namespace StormSim.Sibucao.Editor
{
    public sealed class SignalStationImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/StormSim/Sibucao/Resources/SignalStation/")) return;
            var importer = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            int border = name.StartsWith("panel_bg") ? 16 : name.StartsWith("btn_") ? 8 :
                name.StartsWith("badge_") || name.StartsWith("role_icon_frame") ? 6 :
                name == "portrait_frame" ? 2 : name.StartsWith("inv_slot") || name == "icon_frame" || name == "card_bg" ? 1 : 0;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
