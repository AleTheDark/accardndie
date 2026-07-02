using System.Collections.Generic;
using AccardND.GameData;
using NUnit.Framework;
using UnityEngine;

namespace AccardND.GameCore.Tests
{
    public sealed class CampaignDeckStateTests
    {
        [Test]
        public void CompleteCombat_MovesDefeatedToGraveyardAndSurvivorsToCooldown()
        {
            List<CardDefinition> definitions = CreateCards(6);
            var state = new CampaignDeckState(definitions);
            List<CampaignCardInstance> hand = state.DrawCombatHand(new MinimumRandomSource(), 3);
            foreach (CampaignCardInstance card in hand)
                Assert.That(state.Deploy(card), Is.True);

            state.CompleteCombat(new[] { hand[0] });

            Assert.That(state.GraveyardCount, Is.EqualTo(1));
            Assert.That(state.CooldownCount, Is.EqualTo(2));
            Assert.That(state.AvailableCount, Is.EqualTo(3));
            DestroyCards(definitions);
        }

        [Test]
        public void CompleteFollowingCombat_ReleasesPreviousCooldownOnly()
        {
            List<CardDefinition> definitions = CreateCards(8);
            var state = new CampaignDeckState(definitions);
            List<CampaignCardInstance> firstHand = state.DrawCombatHand(new MinimumRandomSource(), 3);
            foreach (CampaignCardInstance card in firstHand)
                state.Deploy(card);
            state.CompleteCombat(new CampaignCardInstance[0]);

            List<CampaignCardInstance> secondHand = state.DrawCombatHand(new MinimumRandomSource(), 3);
            foreach (CampaignCardInstance card in secondHand)
                state.Deploy(card);
            state.CompleteCombat(new CampaignCardInstance[0]);

            Assert.That(state.CooldownCount, Is.EqualTo(3));
            Assert.That(state.AvailableCount, Is.EqualTo(5));
            DestroyCards(definitions);
        }

        [Test]
        public void ReleaseCooldown_MakesCooldownCardsAvailable()
        {
            List<CardDefinition> definitions = CreateCards(6);
            var state = new CampaignDeckState(definitions);
            List<CampaignCardInstance> hand = state.DrawCombatHand(new MinimumRandomSource(), 3);
            foreach (CampaignCardInstance card in hand)
                state.Deploy(card);
            state.CompleteCombat(new[] { hand[0] });

            int released = state.ReleaseCooldown();

            Assert.That(released, Is.EqualTo(2));
            Assert.That(state.GraveyardCount, Is.EqualTo(1));
            Assert.That(state.CooldownCount, Is.Zero);
            Assert.That(state.AvailableCount, Is.EqualTo(5));
            DestroyCards(definitions);
        }

        [Test]
        public void CombatReadyCount_ExcludesCooldownAndGraveyardCards()
        {
            List<CardDefinition> definitions = CreateCards(6);
            var state = new CampaignDeckState(definitions);
            List<CampaignCardInstance> hand = state.DrawCombatHand(new MinimumRandomSource(), 3);
            foreach (CampaignCardInstance card in hand)
                state.Deploy(card);

            state.CompleteCombat(new[] { hand[0] });

            Assert.That(state.CombatReadyCount, Is.EqualTo(3));
            Assert.That(state.GraveyardCount, Is.EqualTo(1));
            Assert.That(state.CooldownCount, Is.EqualTo(2));
            DestroyCards(definitions);
        }

        [Test]
        public void RemoveCard_RemovesAvailableCardFromDeck()
        {
            List<CardDefinition> definitions = CreateCards(4);
            var state = new CampaignDeckState(definitions);
            CampaignCardInstance card = state.Cards[0];

            bool removed = state.RemoveCard(card);

            Assert.That(removed, Is.True);
            Assert.That(state.Cards, Has.Count.EqualTo(3));
            Assert.That(state.ContainsDefinition(card.Definition.Id), Is.False);
            DestroyCards(definitions);
        }

        [Test]
        public void RemoveCard_DoesNotRemoveGraveyardCardUntilRecovered()
        {
            List<CardDefinition> definitions = CreateCards(4);
            var state = new CampaignDeckState(definitions);
            List<CampaignCardInstance> hand = state.DrawCombatHand(new MinimumRandomSource(), 3);
            foreach (CampaignCardInstance card in hand)
                state.Deploy(card);
            CampaignCardInstance defeated = hand[0];
            state.CompleteCombat(new[] { defeated });

            bool removedFromGraveyard = state.RemoveCard(defeated);
            bool recovered = state.RecoverFromGraveyard(defeated);
            bool removedAfterRecovery = state.RemoveCard(defeated);

            Assert.That(removedFromGraveyard, Is.False);
            Assert.That(recovered, Is.True);
            Assert.That(removedAfterRecovery, Is.True);
            Assert.That(state.ContainsDefinition(defeated.Definition.Id), Is.False);
            DestroyCards(definitions);
        }

        [Test]
        public void AddCard_RejectsEquivalentCardWithDifferentId()
        {
            CardDefinition first = CreateCard("faceless-paladin", "Faceless", 9, HeroClass.Paladin);
            CardDefinition second = CreateCard("faceless-warrior", "Faceless", 9, HeroClass.Warrior);
            var state = new CampaignDeckState(new[] { first });

            CampaignCardInstance added = state.AddCard(second);

            Assert.That(added, Is.Null);
            Assert.That(state.Cards, Has.Count.EqualTo(1));
            Assert.That(state.ContainsEquivalentDefinition(second), Is.True);
            DestroyCards(new[] { first, second });
        }

        private static List<CardDefinition> CreateCards(int count)
        {
            var result = new List<CardDefinition>();
            for (int index = 0; index < count; index++)
            {
                result.Add(CreateCard($"card-{index}", $"Card {index}", index + 2, HeroClass.Warrior));
            }
            return result;
        }

        private static CardDefinition CreateCard(
            string id,
            string displayName,
            int strength,
            HeroClass heroClass)
        {
            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ApplyImportedData(id, displayName, CardCategory.Monster,
                null, strength, true, heroClass, true);
            return card;
        }

        private static void DestroyCards(IEnumerable<CardDefinition> cards)
        {
            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        private sealed class MinimumRandomSource : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }
    }
}
