using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using NUnit.Framework;
using UnityEngine;

namespace AccardND.GameCore.Tests
{
    public sealed class InitialDeckBuilderTests
    {
        [Test]
        public void TryBuyStrength_DoesNotBuyEquivalentCardsWithDifferentIdsInSameClass()
        {
            CardDefinition first = CreateMonster("faceless-paladin", "Faceless", 9, HeroClass.Paladin);
            CardDefinition second = CreateMonster("faceless-paladin-alt", "Faceless", 9, HeroClass.Paladin);
            var rules = new DeckBuildingRules(
                startingEssence: 99,
                deckSize: 2,
                combatHandSize: 2,
                formationSize: 2,
                blindRandomCost: 1,
                chosenClassCost: 1,
                chosenStrengthBaseCost: 1,
                maximumCopiesPerCard: 1,
                strengthWeights: new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            var builder = new InitialDeckBuilder(
                new List<CardDefinition> { first, second },
                new MinimumRandomSource(),
                rules);

            bool firstPurchase = builder.TryBuyStrength(9, out CardDefinition purchased);
            bool secondPurchase = builder.TryBuyStrength(9, out _);

            Assert.That(firstPurchase, Is.True);
            Assert.That(purchased, Is.SameAs(first));
            Assert.That(secondPurchase, Is.False);
            Assert.That(builder.Deck, Has.Count.EqualTo(1));
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void TryBuyStrength_AllowsSameStrengthAndNameInDifferentClasses()
        {
            CardDefinition first = CreateMonster("10-champion-warrior", "Champion", 10, HeroClass.Warrior);
            CardDefinition second = CreateMonster("10-champion-mage", "Champion", 10, HeroClass.Mage);
            var rules = new DeckBuildingRules(
                startingEssence: 99,
                deckSize: 2,
                combatHandSize: 2,
                formationSize: 2,
                blindRandomCost: 1,
                chosenClassCost: 1,
                chosenStrengthBaseCost: 1,
                maximumCopiesPerCard: 1,
                strengthWeights: new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            var builder = new InitialDeckBuilder(
                new List<CardDefinition> { first, second },
                new MinimumRandomSource(),
                rules);

            bool firstPurchase = builder.TryBuyStrength(10, out CardDefinition firstPurchased);
            bool secondPurchase = builder.TryBuyStrength(10, out CardDefinition secondPurchased);

            Assert.That(firstPurchase, Is.True);
            Assert.That(secondPurchase, Is.True);
            Assert.That(firstPurchased, Is.SameAs(first));
            Assert.That(secondPurchased, Is.SameAs(second));
            Assert.That(builder.Deck, Has.Count.EqualTo(2));
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private static CardDefinition CreateMonster(
            string id,
            string displayName,
            int strength,
            HeroClass heroClass)
        {
            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ApplyImportedData(id, displayName, CardCategory.Monster, null, strength, true, heroClass, true);
            return card;
        }

        private sealed class MinimumRandomSource : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }
    }
}
