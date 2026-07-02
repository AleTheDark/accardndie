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
        private const string DiceArtFolder = "Assets/_Project/Art/Dice";
        private const string CatalogPath = "Assets/_Project/Resources/DiceSpriteCatalog.asset";

        static DiceSpriteCatalogBuilder()
        {
            EditorApplication.delayCall += ReimportAndRebuild;
        }

        [MenuItem("Accard N' Die/Reimport Dice Sprites", priority = 29)]
        public static void ReimportAndRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string[] diceGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { DiceArtFolder });
            foreach (string guid in diceGuids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            Rebuild();
        }

        [MenuItem("Accard N' Die/Rebuild Dice Catalog", priority = 30)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var sets = new List<DiceAnimationSet>();
            foreach (string path in Directory.GetFiles(DiceArtFolder, "D*.png"))
            {
                string unityPath = path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(unityPath);
                if (!int.TryParse(name.Substring(1), out int sides))
                    continue;

                Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(unityPath).OfType<Sprite>().ToArray();
                Sprite[] frames = allSprites
                    .Where(sprite => sprite.name.StartsWith(name + "_roll_", StringComparison.Ordinal))
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                    .ToArray();

                Sprite[] results = Enumerable.Range(1, sides)
                    .Select(result => allSprites.FirstOrDefault(
                        sprite => sprite.name.Equals($"{name}_result_{result:00}", StringComparison.Ordinal)))
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
                .Any(path => path.StartsWith("Assets/_Project/Art/Dice/", StringComparison.Ordinal));
            if (diceChanged)
                EditorApplication.delayCall += DiceSpriteCatalogBuilder.Rebuild;
        }
    }
}
