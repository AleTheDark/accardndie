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
        public string DisplayName => displayName;
        public CardCategory Category => category;
        public Sprite Artwork => artwork;
        public int Strength => strength;
        public bool HasHeroClass => hasHeroClass;
        public HeroClass HeroClass => heroClass;
        public string RulesText => rulesText;

        public bool CanEnterCombat =>
            (category == CardCategory.Monster || category == CardCategory.Boss)
            && strength > 0
            && hasHeroClass;

        public CombatCard CreateCombatCard()
        {
            if (!CanEnterCombat)
                throw new InvalidOperationException($"La carta '{displayName}' non ha dati di combattimento completi.");

            return new CombatCard(id, displayName, heroClass, strength);
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
