using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AccardND.Editor
{
    public sealed class AndroidTextureBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
                AndroidTextureOptimizer.OptimizeAndroidTextures();
        }
    }

    [InitializeOnLoad]
    public static class AndroidTextureOptimizer
    {
        private const string SessionKey = "AccardND.AndroidTexturesOptimized.v1";
        private static readonly string[] TextureFolders =
        {
            "Assets/_Project/Art/Cards",
            "Assets/_Project/Art/Dice",
            "Assets/_Project/Art/Scenarios",
            "Assets/_Project/Resources/CardBorders",
            "Assets/_Project/Resources/Backgrounds",
            "Assets/_Project/Resources/BattlePreviews",
            "Assets/_Project/Resources/StatusIcons",
            "Assets/_Project/Resources/UI"
        };

        static AndroidTextureOptimizer()
        {
            EditorApplication.delayCall += OptimizeOnce;
        }

        [MenuItem("Accard N' Die/Optimize Android Textures", priority = 50)]
        public static void OptimizeAndroidTextures()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", TextureFolders))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Accard N' Die] Ottimizzate {count} texture per Android (ASTC).");
        }

        private static void OptimizeOnce()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);
            OptimizeAndroidTextures();
        }
    }

    public static class TextureCompressionSettings
    {
        public static void ApplyAndroid(
            TextureImporter importer,
            int maximumSize,
            TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Android");
            settings.name = "Android";
            settings.overridden = true;
            settings.maxTextureSize = maximumSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 60;
            importer.SetPlatformTextureSettings(settings);
        }

        public static void ApplyBuildTargets(TextureImporter importer, int maximumSize)
        {
            ApplyCompressedPlatform(importer, "Standalone", maximumSize, true);
            ApplyCompressedPlatform(importer, "iOS", maximumSize, false);
        }

        private static void ApplyCompressedPlatform(
            TextureImporter importer,
            string buildTarget,
            int maximumSize,
            bool crunchedCompression)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(buildTarget);
            settings.name = buildTarget;
            settings.overridden = true;
            settings.maxTextureSize = maximumSize;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 60;
            settings.crunchedCompression = crunchedCompression;
            importer.SetPlatformTextureSettings(settings);
        }
    }

    public static class AndroidTextureCompression
    {
        public static void Apply(
            TextureImporter importer,
            int maximumSize,
            TextureImporterFormat format)
        {
            TextureCompressionSettings.ApplyAndroid(importer, maximumSize, format);
        }
    }

    [InitializeOnLoad]
    public static class CardBorderImportInitializer
    {
        private const string BorderFolder = "Assets/_Project/Resources/CardBorders";

        static CardBorderImportInitializer()
        {
            EditorApplication.delayCall += ReimportBorders;
        }

        [MenuItem("Accard N' Die/Reimport Card Holders", priority = 22)]
        public static void ReimportBorders()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BorderFolder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }

    public sealed class CardTextureImporter : AssetPostprocessor
    {
        private const string CardArtPath = "Assets/_Project/Art/Cards/";
        private const string CardBorderPath = "Assets/_Project/Resources/CardBorders/";
        private const string BattlePreviewPath = "Assets/_Project/Resources/BattlePreviews/";
        private const string StatusIconPath = "Assets/_Project/Resources/StatusIcons/";
        private const string UiResourcePath = "Assets/_Project/Resources/UI/";

        private void OnPreprocessTexture()
        {
            bool isCardArt = assetPath.StartsWith(CardArtPath, System.StringComparison.Ordinal);
            bool isCardBorder = assetPath.StartsWith(CardBorderPath, System.StringComparison.Ordinal);
            bool isBattlePreview = assetPath.StartsWith(BattlePreviewPath, System.StringComparison.Ordinal);
            bool isStatusIcon = assetPath.StartsWith(StatusIconPath, System.StringComparison.Ordinal);
            bool isUiResource = assetPath.StartsWith(UiResourcePath, System.StringComparison.Ordinal);
            if (!isCardArt && !isCardBorder && !isBattlePreview && !isStatusIcon && !isUiResource)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = true;
            importer.compressionQuality = 60;

            if (isCardArt)
            {
                importer.maxTextureSize = 1024;
                TextureCompressionSettings.ApplyBuildTargets(importer, 1024);
                TextureCompressionSettings.ApplyAndroid(importer, 1024, TextureImporterFormat.ASTC_6x6);
                return;
            }

            if (isBattlePreview)
            {
                importer.maxTextureSize = 768;
                TextureCompressionSettings.ApplyBuildTargets(importer, 768);
                TextureCompressionSettings.ApplyAndroid(importer, 768, TextureImporterFormat.ASTC_6x6);
                return;
            }

            if (isStatusIcon)
            {
                importer.maxTextureSize = 256;
                TextureCompressionSettings.ApplyBuildTargets(importer, 256);
                TextureCompressionSettings.ApplyAndroid(importer, 256, TextureImporterFormat.ASTC_6x6);
                return;
            }

            if (isUiResource)
            {
                int maximumSize = IsLargeUiResource(assetPath) ? 1024 : 512;
                importer.maxTextureSize = maximumSize;
                TextureCompressionSettings.ApplyBuildTargets(importer, maximumSize);
                TextureCompressionSettings.ApplyAndroid(importer, maximumSize, TextureImporterFormat.ASTC_6x6);
                return;
            }

            importer.maxTextureSize = 1024;
            TextureCompressionSettings.ApplyBuildTargets(importer, 1024);
            TextureCompressionSettings.ApplyAndroid(importer, 1024, TextureImporterFormat.ASTC_6x6);
        }

        private static bool IsLargeUiResource(string path)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            return fileName.StartsWith("background_", System.StringComparison.Ordinal)
                || fileName.EndsWith("_background", System.StringComparison.Ordinal)
                || fileName.EndsWith("_background_hud", System.StringComparison.Ordinal)
                || fileName.StartsWith("selection_mode_screen", System.StringComparison.Ordinal)
                || fileName.StartsWith("tutorial-", System.StringComparison.Ordinal)
                || fileName.StartsWith("card_inspection", System.StringComparison.Ordinal)
                || fileName == "card_inspection_book";
        }
    }
}
