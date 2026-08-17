using UnityEditor;

/// <summary>
/// Preserva i dettagli fini dei frame della Quick Challenge. Sono elementi UI grandi,
/// visualizzati vicino alla risoluzione sorgente, quindi non devono usare la compressione
/// predefinita né mipmap pensate per le texture del mondo 3D.
/// </summary>
internal sealed class QuickChallengeFrameImporter : AssetPostprocessor
{
    private const string Folder = "Assets/_Project/Resources/UI/QuickChallenge/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;

        TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.overridden = true;
        defaults.maxTextureSize = 4096;
        defaults.format = TextureImporterFormat.RGBA32;
        defaults.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(defaults);
    }

    [MenuItem("AccardND/Debug/Quick Challenge/Reimporta frame alta qualità")]
    private static void ReimportFrames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') });
        foreach (string guid in guids)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }
}
