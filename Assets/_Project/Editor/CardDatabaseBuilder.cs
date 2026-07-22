using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    public static class CardDatabaseBuilder
    {
        private const string ArtRoot = "Assets/_Project/Art/Cards";
        private const string DataRoot = "Assets/_Project/Data/Cards";
        private const string ResourcesRoot = "Assets/_Project/Resources";
        private const string DatabasePath = ResourcesRoot + "/CardDatabase.asset";

        [MenuItem("Accard N' Die/Rebuild Card Database", priority = 20)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureFolder(DataRoot);
            EnsureFolder(ResourcesRoot);

            var definitions = new List<CardDefinition>();
            string[] artworkGuids = AssetDatabase.FindAssets("t:Sprite", new[] { ArtRoot });

            foreach (string guid in artworkGuids)
            {
                string artworkPath = AssetDatabase.GUIDToAssetPath(guid);
                Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(artworkPath);
                if (artwork == null)
                    continue;

                ImportedCard imported = Parse(artworkPath, artwork);
                string categoryFolder = DataRoot + "/" + imported.Category;
                EnsureFolder(categoryFolder);

                string definitionPath = categoryFolder + "/" + imported.Id + ".asset";
                CardDefinition definition = AssetDatabase.LoadAssetAtPath<CardDefinition>(definitionPath);
                bool isNew = definition == null;

                if (isNew)
                {
                    definition = ScriptableObject.CreateInstance<CardDefinition>();
                    AssetDatabase.CreateAsset(definition, definitionPath);
                }

                definition.ApplyImportedData(
                    imported.Id,
                    imported.DisplayName,
                    imported.Category,
                    artwork,
                    imported.Strength,
                    imported.HasHeroClass,
                    imported.HeroClass,
                    isNew);
                definition.ApplyImportedHeroClass(imported.HasHeroClass, imported.HeroClass);

                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:CardDefinition", new[] { DataRoot }))
            {
                string definitionPath = AssetDatabase.GUIDToAssetPath(guid);
                CardDefinition definition = AssetDatabase.LoadAssetAtPath<CardDefinition>(definitionPath);
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                if (definitions.Any(card => card != null && string.Equals(card.Id, definition.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                definitions.Add(definition);
            }

            definitions = definitions
                .OrderBy(card => card.Category)
                .ThenBy(card => card.Strength)
                .ThenBy(card => card.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.SetCards(definitions.ToArray());
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Validate(database, false);
            Debug.Log($"[Accard N' Die] Database aggiornato: {definitions.Count} carte.");
        }

        [MenuItem("Accard N' Die/Open Card Database", priority = 21)]
        private static void OpenDatabase()
        {
            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (database == null)
            {
                Rebuild();
                database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            }

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        [MenuItem("Accard N' Die/Validate Card Database", priority = 22)]
        private static void ValidateFromMenu()
        {
            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("[Accard N' Die] CardDatabase non trovato. Esegui prima Rebuild Card Database.");
                return;
            }

            Validate(database, true);
        }

        private static void Validate(CardDatabase database, bool verbose)
        {
            int problems = 0;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CardDefinition card in database.Cards)
            {
                if (card == null)
                {
                    problems++;
                    continue;
                }

                if (!ids.Add(card.Id))
                {
                    problems++;
                    Debug.LogError($"[Accard N' Die] ID duplicato: {card.Id}", card);
                }

                if (card.Artwork == null)
                {
                    problems++;
                    Debug.LogError($"[Accard N' Die] Artwork mancante: {card.Id}", card);
                }

                if (card.Category == CardCategory.Monster && !card.CanEnterCombat)
                {
                    problems++;
                    Debug.LogError($"[Accard N' Die] Dati di combattimento incompleti: {card.Id}", card);
                }
            }

            if (verbose || problems > 0)
                Debug.Log($"[Accard N' Die] Validazione completata: {database.Cards.Count} carte, {problems} problemi.", database);
        }

        private static ImportedCard Parse(string artworkPath, Sprite artwork)
        {
            CardCategory category = CategoryFromPath(artworkPath);
            string rawName = Path.GetFileNameWithoutExtension(artworkPath);
            string id = NormalizeId(rawName);
            string[] tokens = rawName.Split('-');

            int strength = 0;
            int firstNameToken = 0;
            if (tokens.Length > 0 && int.TryParse(tokens[0], out int parsedStrength))
            {
                strength = parsedStrength;
                firstNameToken = 1;
            }

            bool hasClass = TryParseHeroClass(tokens.LastOrDefault(), out HeroClass heroClass);
            bool classComesFromToken = hasClass;
            if (string.Equals(id, "boss-medusa", StringComparison.OrdinalIgnoreCase))
            {
                hasClass = true;
                classComesFromToken = false;
                heroClass = HeroClass.Mage;
            }
            int lastNameToken = classComesFromToken ? tokens.Length - 1 : tokens.Length;
            string displayName = BuildDisplayName(tokens, firstNameToken, lastNameToken, rawName, category);

            return new ImportedCard(id, displayName, category, artwork, strength, hasClass, heroClass);
        }

        private static CardCategory CategoryFromPath(string path)
        {
            if (path.Contains("/Bosses/", StringComparison.OrdinalIgnoreCase))
                return CardCategory.Boss;
            if (path.Contains("/Items/", StringComparison.OrdinalIgnoreCase))
                return CardCategory.Item;
            if (path.Contains("/Backs/", StringComparison.OrdinalIgnoreCase))
                return CardCategory.CardBack;
            return CardCategory.Monster;
        }

        private static string BuildDisplayName(
            string[] tokens,
            int first,
            int last,
            string rawName,
            CardCategory category)
        {
            if (category == CardCategory.CardBack)
                return "Retro carta";

            if (category == CardCategory.Item)
            {
                return tokens[0].ToUpperInvariant() switch
                {
                    "A" => "Asso",
                    "J" => "Jack",
                    "Q" => "Donna",
                    "K" => "Re",
                    _ => rawName.StartsWith("Jolly", StringComparison.OrdinalIgnoreCase) ? "Jolly" : Humanize(rawName)
                };
            }

            var nameTokens = tokens.Skip(first).Take(Math.Max(0, last - first)).ToArray();
            if (category == CardCategory.Boss && nameTokens.FirstOrDefault()?.Equals("Boss", StringComparison.OrdinalIgnoreCase) == true)
                nameTokens = nameTokens.Skip(1).ToArray();

            if (nameTokens.Length == 0 && last > 0)
                nameTokens = new[] { tokens[last - 1] };

            string name = string.Join(" ", nameTokens.Select(Humanize));
            return string.IsNullOrWhiteSpace(name) ? Humanize(rawName) : name;
        }

        private static bool TryParseHeroClass(string token, out HeroClass heroClass)
        {
            switch (token?.ToLowerInvariant())
            {
                case "assassin":
                case "assassino":
                    heroClass = HeroClass.Assassin;
                    return true;
                case "warrior":
                case "guerriero":
                    heroClass = HeroClass.Warrior;
                    return true;
                case "mage":
                case "mago":
                    heroClass = HeroClass.Mage;
                    return true;
                case "tank":
                    heroClass = HeroClass.Paladin;
                    return true;
                case "paladin":
                case "paladino":
                    heroClass = HeroClass.Paladin;
                    return true;
                case "rogue":
                case "ladro":
                    heroClass = HeroClass.Rogue;
                    return true;
                case "hunter":
                case "cacciatore":
                    heroClass = HeroClass.Hunter;
                    return true;
                case "barbarian":
                case "barbaro":
                    heroClass = HeroClass.Barbarian;
                    return true;
                case "necromancer":
                case "negromante":
                    heroClass = HeroClass.Necromancer;
                    return true;
                case "priest":
                case "sacerdote":
                    heroClass = HeroClass.Priest;
                    return true;
                default:
                    heroClass = default;
                    return false;
            }
        }

        private static string NormalizeId(string value)
        {
            string normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-");
            return normalized.Trim('-');
        }

        private static string Humanize(string value)
        {
            string spaced = Regex.Replace(value.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
            return spaced.Trim();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct ImportedCard
        {
            public ImportedCard(
                string id,
                string displayName,
                CardCategory category,
                Sprite artwork,
                int strength,
                bool hasHeroClass,
                HeroClass heroClass)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                Artwork = artwork;
                Strength = strength;
                HasHeroClass = hasHeroClass;
                HeroClass = heroClass;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public CardCategory Category { get; }
            public Sprite Artwork { get; }
            public int Strength { get; }
            public bool HasHeroClass { get; }
            public HeroClass HeroClass { get; }
        }
    }
}
