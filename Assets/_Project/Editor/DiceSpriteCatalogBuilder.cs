using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccardND.Presentation;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    [InitializeOnLoad]
    public static class DiceSpriteCatalogBuilder
    {
        private const string DiceSpritesFolder = "Assets/DiceUI/DiceSprites";
        private const string DiceResultsFolder = "Assets/DiceUI/DiceSpritesResult";
        private const string CatalogPath = "Assets/_Project/Resources/DiceSpriteCatalog.asset";
        private static readonly int[] SupportedDiceSides = { 4, 6, 8, 10, 12, 20 };

        static DiceSpriteCatalogBuilder()
        {
            EditorApplication.delayCall += ReimportAndRebuild;
        }

        [MenuItem("Accard N' Die/Reimport Dice Sprites", priority = 29)]
        public static void ReimportAndRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string[] diceGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { DiceSpritesFolder, DiceResultsFolder });
            foreach (string guid in diceGuids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            Rebuild();
        }

        [MenuItem("Accard N' Die/Rebuild Dice Catalog", priority = 30)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!Directory.Exists(DiceSpritesFolder) || !Directory.Exists(DiceResultsFolder))
            {
                Debug.LogWarning("[Accard N' Die] Impossibile aggiornare il catalogo dadi: cartelle DiceUI mancanti.");
                return;
            }

            Sprite[] frames = Directory.GetFiles(DiceSpritesFolder, "Purple_Dice_Roll*.png")
                .Select(LoadSpriteAtPath)
                .Where(sprite => sprite != null)
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();

            var sets = new List<DiceAnimationSet>();
            foreach (int sides in SupportedDiceSides)
            {
                Sprite[] results = Enumerable.Range(1, sides)
                    .Select(result => LoadSpriteAtPath($"{DiceResultsFolder}/DicePu_R_{result}.png"))
                    .ToArray();

                if (frames.Length > 0 && results.All(sprite => sprite != null))
                    sets.Add(new DiceAnimationSet(sides, frames, results));
            }

            DiceSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<DiceSpriteCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DiceSpriteCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetDice(sets.OrderBy(set => set.Sides).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Accard N' Die] Catalogo dadi aggiornato: {sets.Count} dadi, "
                + $"{sets.Sum(set => set.Frames.Length)} frame e risultati completi.");
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            string unityPath = path.Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<Sprite>(unityPath);
        }
    }

    public sealed class DiceCatalogAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool diceChanged = importedAssets.Concat(movedAssets)
                .Any(path => path.StartsWith("Assets/DiceUI/DiceSprites/", StringComparison.Ordinal)
                    || path.StartsWith("Assets/DiceUI/DiceSpritesResult/", StringComparison.Ordinal));
            if (diceChanged)
                EditorApplication.delayCall += DiceSpriteCatalogBuilder.Rebuild;
        }
    }
}
