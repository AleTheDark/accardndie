using System;
using System.Linq;
using AccardND.Presentation;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    [InitializeOnLoad]
    public static class CardArtCatalogBuilder
    {
        private const string CatalogDirectory = "Assets/_Project/Resources";
        private const string CatalogPath = CatalogDirectory + "/CardArtCatalog.asset";

        private static readonly string[] CardFolders =
        {
            "Assets/_Project/Art/Cards"
        };

        static CardArtCatalogBuilder()
        {
            EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("Accard N' Die/Rebuild Card Art Catalog")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", CardFolders);
            Sprite[] sprites = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!AssetDatabase.IsValidFolder(CatalogDirectory))
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");

            CardArtCatalog catalog = AssetDatabase.LoadAssetAtPath<CardArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetCards(sprites);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Accard N' Die] Catalogo aggiornato: {sprites.Length} carte.");
        }
    }

    public sealed class CardCatalogAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool cardsChanged = importedAssets.Concat(deletedAssets).Concat(movedAssets)
                .Any(path => path.StartsWith("Assets/_Project/Art/Cards/", StringComparison.Ordinal));

            if (cardsChanged)
                EditorApplication.delayCall += CardArtCatalogBuilder.Rebuild;
        }
    }
}
