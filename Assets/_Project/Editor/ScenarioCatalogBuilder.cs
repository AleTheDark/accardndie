using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccardND.GameData;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    [InitializeOnLoad]
    public static class ScenarioCatalogBuilder
    {
        private const string ArtFolder = "Assets/_Project/Art/Scenarios";
        private const string DataFolder = "Assets/_Project/Data/Scenarios";
        private const string CatalogPath = "Assets/_Project/Resources/ScenarioCatalog.asset";

        static ScenarioCatalogBuilder()
        {
            EditorApplication.delayCall += ReimportAndRebuild;
        }

        [MenuItem("Accard N' Die/Reimport Scenario Backgrounds", priority = 39)]
        public static void ReimportAndRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
            foreach (string guid in textureGuids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            Rebuild();
        }

        [MenuItem("Accard N' Die/Rebuild Scenario Catalog", priority = 40)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureFolder(DataFolder);
            var definitions = new List<ScenarioDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { ArtFolder });

            foreach (string guid in guids)
            {
                string artPath = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
                if (sprite == null)
                    continue;

                ScenarioImport imported = Parse(Path.GetFileNameWithoutExtension(artPath));
                string definitionPath = $"{DataFolder}/{imported.Id}.asset";
                ScenarioDefinition definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(definitionPath);
                bool isNew = definition == null;
                if (isNew)
                {
                    definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
                    AssetDatabase.CreateAsset(definition, definitionPath);
                }

                definition.ApplyImportedData(
                    imported.Id,
                    imported.DisplayName,
                    sprite,
                    imported.RoomType,
                    imported.Difficulty,
                    imported.BossId,
                    isNew);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            ScenarioCatalog catalog = AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ScenarioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetScenarios(definitions.OrderBy(definition => definition.Id).ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Accard N' Die] Catalogo scenari aggiornato: {definitions.Count} scenari.");
        }

        [MenuItem("Accard N' Die/Open Scenario Catalog", priority = 41)]
        private static void OpenCatalog()
        {
            ScenarioCatalog catalog = AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(CatalogPath);
            if (catalog == null)
            {
                Rebuild();
                catalog = AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(CatalogPath);
            }

            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        private static ScenarioImport Parse(string rawName)
        {
            string id = rawName.StartsWith("bg_", StringComparison.OrdinalIgnoreCase)
                ? rawName.Substring(3)
                : rawName;

            return id switch
            {
                "default" => new(id, "Dungeon", RoomType.Any, RoomDifficulty.Any, string.Empty),
                "monster_2" => new(id, "Mostri - Facile", RoomType.Monster, RoomDifficulty.Easy, string.Empty),
                "monster_3" => new(id, "Mostri - Normale", RoomType.Monster, RoomDifficulty.Normal, string.Empty),
                "monster_4" => new(id, "Mostri - Difficile", RoomType.Monster, RoomDifficulty.Hard, string.Empty),
                "low_merchant" => new(id, "Mercante", RoomType.Merchant, RoomDifficulty.Easy, string.Empty),
                "god_merchant" => new(id, "Mercante Divino", RoomType.Merchant, RoomDifficulty.Hard, string.Empty),
                "loot" => new(id, "Ricompensa", RoomType.Loot, RoomDifficulty.Any, string.Empty),
                "unexpected_opportunity" => new(id, "Imprevisto o Opportunità", RoomType.UnexpectedOpportunity, RoomDifficulty.Any, string.Empty),
                "climbing" => new(id, "Rampicanti", RoomType.Boss, RoomDifficulty.Any, "salazar"),
                "toxic" => new(id, "Esalazioni Tossiche", RoomType.Boss, RoomDifficulty.Any, "kronn"),
                "infested" => new(id, "Infestata", RoomType.Boss, RoomDifficulty.Any, "draktharr"),
                "lux" => new(id, "Illuminata", RoomType.Boss, RoomDifficulty.Any, "zakhar"),
                "fog" => new(id, "Nebbia", RoomType.Boss, RoomDifficulty.Any, string.Empty),
                "ghost" => new(id, "Spettrale", RoomType.Boss, RoomDifficulty.Any, string.Empty),
                "mirror" => new(id, "Specchi", RoomType.Boss, RoomDifficulty.Any, string.Empty),
                "rainbow" => new(id, "Arcobaleno", RoomType.Boss, RoomDifficulty.Any, string.Empty),
                _ => new(id, id.Replace('_', ' '), RoomType.Any, RoomDifficulty.Any, string.Empty)
            };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct ScenarioImport
        {
            public ScenarioImport(
                string id,
                string displayName,
                RoomType roomType,
                RoomDifficulty difficulty,
                string bossId)
            {
                Id = id;
                DisplayName = displayName;
                RoomType = roomType;
                Difficulty = difficulty;
                BossId = bossId;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public RoomType RoomType { get; }
            public RoomDifficulty Difficulty { get; }
            public string BossId { get; }
        }
    }

    public sealed class ScenarioCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Concat(movedAssets)
                .Any(path => path.StartsWith("Assets/_Project/Art/Scenarios/", StringComparison.Ordinal)))
            {
                EditorApplication.delayCall += ScenarioCatalogBuilder.Rebuild;
            }
        }
    }
}
