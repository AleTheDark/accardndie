using System;
using System.Collections.Generic;
using AccardND.GameCore;

namespace AccardND.GameData
{
    public enum DeckPurchaseMode
    {
        BlindRandom,
        ChosenClass,
        ChosenStrength
    }

    public readonly struct DeckBuildingRules
    {
        public DeckBuildingRules(
            int startingEssence,
            int deckSize,
            int combatHandSize,
            int formationSize,
            int blindRandomCost,
            int chosenClassCost,
            int chosenStrengthBaseCost,
            int maximumCopiesPerCard,
            IReadOnlyList<int> strengthWeights)
        {
            StartingEssence = startingEssence;
            DeckSize = deckSize;
            CombatHandSize = combatHandSize;
            FormationSize = formationSize;
            BlindRandomCost = blindRandomCost;
            ChosenClassCost = chosenClassCost;
            ChosenStrengthBaseCost = chosenStrengthBaseCost;
            MaximumCopiesPerCard = maximumCopiesPerCard;
            StrengthWeights = strengthWeights ?? throw new ArgumentNullException(nameof(strengthWeights));
        }

        public int StartingEssence { get; }
        public int DeckSize { get; }
        public int CombatHandSize { get; }
        public int FormationSize { get; }
        public int BlindRandomCost { get; }
        public int ChosenClassCost { get; }
        public int ChosenStrengthBaseCost { get; }
        public int MaximumCopiesPerCard { get; }
        public IReadOnlyList<int> StrengthWeights { get; }

        public int CostFor(DeckPurchaseMode mode, int strength = 0) => mode switch
        {
            DeckPurchaseMode.BlindRandom => BlindRandomCost,
            DeckPurchaseMode.ChosenClass => ChosenClassCost,
            DeckPurchaseMode.ChosenStrength => ChosenStrengthBaseCost + Math.Max(1, strength),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    public sealed class InitialDeckBuilder
    {
        private readonly IReadOnlyList<CardDefinition> catalog;
        private readonly IRandomSource random;
        private readonly DeckBuildingRules rules;
        private readonly List<CardDefinition> deck = new();

        public InitialDeckBuilder(
            IReadOnlyList<CardDefinition> catalog,
            IRandomSource random,
            DeckBuildingRules rules)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.rules = rules;
            EssenceRemaining = rules.StartingEssence;
        }

        public int EssenceRemaining { get; private set; }
        public IReadOnlyList<CardDefinition> Deck => deck;
        public bool CanStartCampaign => deck.Count == rules.DeckSize;

        public bool TryBuyRandom(out CardDefinition purchased) =>
            TryPurchase(DeckPurchaseMode.BlindRandom, null, 0, out purchased);

        public bool TryBuyClass(HeroClass heroClass, out CardDefinition purchased) =>
            TryPurchase(DeckPurchaseMode.ChosenClass, heroClass, 0, out purchased);

        public bool TryBuyStrength(int strength, out CardDefinition purchased) =>
            TryPurchase(DeckPurchaseMode.ChosenStrength, null, strength, out purchased);

        private bool TryPurchase(
            DeckPurchaseMode mode,
            HeroClass? heroClass,
            int strength,
            out CardDefinition purchased)
        {
            purchased = null;
            if (deck.Count >= rules.DeckSize)
                return false;

            int cost = rules.CostFor(mode, strength);
            int slotsAfterPurchase = rules.DeckSize - deck.Count - 1;
            int minimumEssenceNeeded = slotsAfterPurchase * rules.BlindRandomCost;
            if (EssenceRemaining - cost < minimumEssenceNeeded)
                return false;

            List<CardDefinition> eligible = BuildEligiblePool(heroClass, strength, mode);
            if (eligible.Count == 0)
                return false;

            purchased = mode == DeckPurchaseMode.ChosenStrength
                ? eligible[random.NextInclusive(0, eligible.Count - 1)]
                : DrawWeightedByStrength(eligible);
            deck.Add(purchased);
            EssenceRemaining -= cost;
            return true;
        }

        private List<CardDefinition> BuildEligiblePool(
            HeroClass? heroClass,
            int strength,
            DeckPurchaseMode mode)
        {
            var eligible = new List<CardDefinition>();
            foreach (CardDefinition card in catalog)
            {
                if (card == null || card.Category != CardCategory.Monster || !card.CanEnterCombat)
                    continue;
                if (heroClass.HasValue && card.HeroClass != heroClass.Value)
                    continue;
                if (mode == DeckPurchaseMode.ChosenStrength && card.Strength != strength)
                    continue;
                // Durante la costruzione iniziale ogni carta identificata dal suo ID è unica.
                if (CardPurchaseUniqueness.ContainsEquivalent(card, deck))
                    continue;
                eligible.Add(card);
            }
            return eligible;
        }

        private CardDefinition DrawWeightedByStrength(List<CardDefinition> eligible)
        {
            int totalWeight = 0;
            foreach (CardDefinition card in eligible)
                totalWeight += WeightForStrength(card.Strength);

            int roll = random.NextInclusive(1, totalWeight);
            foreach (CardDefinition card in eligible)
            {
                roll -= WeightForStrength(card.Strength);
                if (roll <= 0)
                    return card;
            }
            return eligible[eligible.Count - 1];
        }

        private int WeightForStrength(int strength)
        {
            int index = strength - 2;
            return index >= 0 && index < rules.StrengthWeights.Count
                ? Math.Max(1, rules.StrengthWeights[index])
                : 1;
        }

    }
}
