using System.Collections.Generic;
using System.Linq;
using AccardND.GameData;
using NUnit.Framework;
using UnityEngine;

namespace AccardND.GameCore.Tests
{
    public sealed class FormationDraftServiceTests
    {
        [Test]
        public void DrawCandidates_ReturnsRequestedUniqueCombatCards()
        {
            var cards = new List<CardDefinition>();
            for (int index = 0; index < 10; index++)
            {
                CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
                card.ApplyImportedData(
                    $"card-{index}",
                    $"Carta {index}",
                    CardCategory.Monster,
                    null,
                    index + 1,
                    true,
                    HeroClass.Assassin,
                    true);
                cards.Add(card);
            }

            var service = new FormationDraftService(new MinimumRandomSource());
            List<CardDefinition> result = service.DrawCandidates(cards, 5);

            Assert.That(result, Has.Count.EqualTo(5));
            Assert.That(result.Select(card => card.Id).Distinct().Count(), Is.EqualTo(5));

            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        [Test]
        public void DrawCandidates_DoesNotReturnDuplicateCardIds()
        {
            var firstSkeleton = CreateMonster("skeleton", "Scheletro A", 3);
            var secondSkeleton = CreateMonster("skeleton", "Scheletro B", 3);
            var brute = CreateMonster("brute", "Bruto", 7);
            var cards = new List<CardDefinition> { firstSkeleton, secondSkeleton, brute };

            var service = new FormationDraftService(new MinimumRandomSource());
            List<CardDefinition> result = service.DrawCandidates(cards, 2);

            Assert.That(result.Select(card => card.Id).Distinct().Count(), Is.EqualTo(result.Count));

            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        [Test]
        public void DrawCandidates_DoesNotReturnEquivalentCardsWithDifferentIds()
        {
            var firstFaceless = CreateMonster("faceless-paladin", "Faceless", 9, HeroClass.Paladin);
            var secondFaceless = CreateMonster("faceless-warrior", "Faceless", 9, HeroClass.Warrior);
            var brute = CreateMonster("brute", "Bruto", 7, HeroClass.Warrior);
            var cards = new List<CardDefinition> { firstFaceless, secondFaceless, brute };

            var service = new FormationDraftService(new MinimumRandomSource());
            List<CardDefinition> result = service.DrawCandidates(cards, 2);

            Assert.That(result.Count(card => card.DisplayName == "Faceless" && card.Strength == 9), Is.EqualTo(1));

            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        [Test]
        public void DrawBossCandidates_UsesOnlyBossCards()
        {
            var cards = new List<CardDefinition>();
            foreach (CardCategory category in new[] { CardCategory.Monster, CardCategory.Boss })
            {
                CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
                card.ApplyImportedData(category.ToString(), category.ToString(), category, null, 10,
                    true, HeroClass.Paladin, true);
                cards.Add(card);
            }

            var service = new FormationDraftService(new MinimumRandomSource());
            List<CardDefinition> result = service.DrawBossCandidates(cards, 1);

            Assert.That(result.Single().Category, Is.EqualTo(CardCategory.Boss));
            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        [Test]
        public void DrawBossCandidates_FallsBackToMonsterWhenBossPoolIsEmpty()
        {
            CardDefinition monster = ScriptableObject.CreateInstance<CardDefinition>();
            monster.ApplyImportedData("monster", "Monster", CardCategory.Monster, null, 8,
                true, HeroClass.Barbarian, true);

            var service = new FormationDraftService(new MinimumRandomSource());
            List<CardDefinition> result = service.DrawBossCandidates(
                new List<CardDefinition> { monster }, 1);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Category, Is.EqualTo(CardCategory.Monster));
            Object.DestroyImmediate(monster);
        }

        private sealed class MinimumRandomSource : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }

        private static CardDefinition CreateMonster(string id, string name, int strength)
        {
            return CreateMonster(id, name, strength, HeroClass.Assassin);
        }

        private static CardDefinition CreateMonster(string id, string name, int strength, HeroClass heroClass)
        {
            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ApplyImportedData(id, name, CardCategory.Monster, null, strength, true, heroClass, true);
            return card;
        }
    }
}
