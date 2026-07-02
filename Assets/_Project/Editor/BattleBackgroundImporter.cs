using System;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    public sealed class BattleBackgroundImporter : AssetPostprocessor
    {
        private const string BackgroundPath = "Assets/_Project/Resources/Backgrounds/";
        private const string ScenarioPath = "Assets/_Project/Art/Scenarios/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(BackgroundPath, StringComparison.Ordinal)
                && !assetPath.StartsWith(ScenarioPath, StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            AndroidTextureCompression.Apply(importer, 1024, TextureImporterFormat.ASTC_6x6);
        }
    }
}
