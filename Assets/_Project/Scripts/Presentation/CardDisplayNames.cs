using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;

namespace AccardND.Presentation
{
    internal static class CardDisplayNames
    {
        private static readonly Dictionary<string, string> KnownNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["whitealien"] = "White Alien",
            ["white alien"] = "White Alien",
            ["darkelf"] = "Dark Elf",
            ["dark elf"] = "Dark Elf",
            ["spirit"] = "Spirit"
        };

        public static string MarketName(CardDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string name = HarmonizeCreatureName(definition);
            return definition.HasHeroClass
                ? $"{definition.HeroClass} {name}"
                : name;
        }

        private static string HarmonizeCreatureName(CardDefinition definition)
        {
            string source = !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : CreatureNameFromId(definition.Id);
            string normalized = NormalizeKey(source);

            if (KnownNames.TryGetValue(normalized, out string knownName))
                return knownName;

            return TitleCase(source);
        }

        private static string CreatureNameFromId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            string[] parts = id.Split('-');
            return parts.Length > 1 ? parts[1] : id;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("_", string.Empty).Replace("-", string.Empty);
        }

        private static string TitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] words = value.Trim().Replace("_", " ").Replace("-", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].ToLowerInvariant();
                words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
            }

            return string.Join(" ", words);
        }
    }
}
