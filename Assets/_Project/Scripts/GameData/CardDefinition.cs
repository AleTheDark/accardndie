using System;
using AccardND.GameCore;
using UnityEngine;

namespace AccardND.GameData
{
    [CreateAssetMenu(menuName = "Accard N' Die/Card Definition", fileName = "CardDefinition")]
    public sealed class CardDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CardCategory category;
        [SerializeField] private Sprite artwork;
        [SerializeField, Min(0)] private int strength;
        [SerializeField] private bool hasHeroClass;
        [SerializeField] private HeroClass heroClass;
        [SerializeField, TextArea(2, 6)] private string rulesText;

        public string Id => id;
        public string DisplayName => FormatDisplayName(displayName, id);
        public CardCategory Category => category;
        public Sprite Artwork => artwork;
        public int Strength => IsMedusaBoss ? 8 : strength;
        public bool HasHeroClass => hasHeroClass || IsMedusaBoss;
        public HeroClass HeroClass => IsMedusaBoss ? HeroClass.Mage : heroClass;
        public string RulesText => rulesText;
        private bool IsMedusaBoss => string.Equals(id, "boss-medusa", StringComparison.OrdinalIgnoreCase);

        public bool CanEnterCombat =>
            (category == CardCategory.Monster || category == CardCategory.Boss)
            && Strength > 0
            && HasHeroClass;

        public CombatCard CreateCombatCard()
        {
            if (!CanEnterCombat)
                throw new InvalidOperationException($"La carta '{displayName}' non ha dati di combattimento completi.");

            return new CombatCard(id, DisplayName, HeroClass, Strength);
        }

        private static string FormatDisplayName(string rawDisplayName, string fallbackId)
        {
            string source = !string.IsNullOrWhiteSpace(rawDisplayName)
                ? rawDisplayName
                : CreatureNameFromId(fallbackId);

            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            string[] words = source.Trim()
                .Replace("_", " ")
                .Replace("-", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].ToLowerInvariant();
                words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
            }

            return string.Join(" ", words);
        }

        private static string CreatureNameFromId(string fallbackId)
        {
            if (string.IsNullOrWhiteSpace(fallbackId))
                return string.Empty;

            string[] parts = fallbackId.Split('-');
            return parts.Length > 1 ? parts[1] : fallbackId;
        }

#if UNITY_EDITOR
        public void ApplyImportedData(
            string importedId,
            string importedDisplayName,
            CardCategory importedCategory,
            Sprite importedArtwork,
            int importedStrength,
            bool importedHasHeroClass,
            HeroClass importedHeroClass,
            bool initializeGameplayData)
        {
            id = importedId;
            category = importedCategory;
            artwork = importedArtwork;

            if (!initializeGameplayData)
                return;

            displayName = importedDisplayName;
            strength = importedStrength;
            hasHeroClass = importedHasHeroClass;
            heroClass = importedHeroClass;
            rulesText = string.Empty;
        }

        public void ApplyImportedHeroClass(bool importedHasHeroClass, HeroClass importedHeroClass)
        {
            hasHeroClass = importedHasHeroClass;
            heroClass = importedHeroClass;
        }
#endif
    }
}
