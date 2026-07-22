using System.Collections.Generic;
using AccardND.GameData;
using NUnit.Framework;
using UnityEngine;

namespace AccardND.GameCore.Tests
{
    public sealed class CampaignRunSaveTests
    {
        [Test]
        public void RestoreProgress_RoundTripsCounters()
        {
            RunProgressState original = CreateProgress();
            original.CompleteMonsterRoom(new[] { 3, 4 });
            original.CompleteMonsterRoom(new[] { 5 });
            original.TrySpendExperience(2);

            var save = new CampaignRunSave();
            CampaignRunMapper.WriteProgress(save, original);

            RunProgressState restored = CreateProgress();
            CampaignRunMapper.ReadProgress(save, restored);

            Assert.That(restored.PlayerLevel, Is.EqualTo(original.PlayerLevel));
            Assert.That(restored.CurrentExperience, Is.EqualTo(original.CurrentExperience));
            Assert.That(restored.TotalExperience, Is.EqualTo(original.TotalExperience));
            Assert.That(restored.AvailableExperience, Is.EqualTo(original.AvailableExperience));
            Assert.That(restored.RoomsCleared, Is.EqualTo(original.RoomsCleared));
        }

        [Test]
        public void RestoreDeck_PreservesZonesAndCount()
        {
            List<CardDefinition> definitions = CreateCards(6);
            var deck = new CampaignDeckState(definitions);
            List<CampaignCardInstance> hand = deck.DrawCombatHand(new FixedRandom(), 3);
            foreach (CampaignCardInstance card in hand)
                deck.Deploy(card);
            deck.CompleteCombat(new[] { hand[0] }); // 1 cimitero, 2 cooldown, 3 mazzo

            var save = new CampaignRunSave();
            CampaignRunMapper.WriteDeck(save, deck);

            var restored = new CampaignDeckState(new List<CardDefinition>());
            CampaignRunMapper.ReadDeck(save, restored, id => Resolve(definitions, id));

            Assert.That(restored.Cards, Has.Count.EqualTo(deck.Cards.Count));
            Assert.That(restored.GraveyardCount, Is.EqualTo(deck.GraveyardCount));
            Assert.That(restored.CooldownCount, Is.EqualTo(deck.CooldownCount));
            Assert.That(restored.AvailableCount, Is.EqualTo(deck.AvailableCount));
            Assert.That(restored.NextInstanceId, Is.EqualTo(deck.NextInstanceId));
            DestroyCards(definitions);
        }

        [Test]
        public void ReadDeck_SkipsCardsMissingFromDatabase()
        {
            List<CardDefinition> definitions = CreateCards(3);
            var deck = new CampaignDeckState(definitions);
            var save = new CampaignRunSave();
            CampaignRunMapper.WriteDeck(save, deck);

            // Il database "aggiornato" non contiene più la prima carta.
            var reduced = new List<CardDefinition> { definitions[1], definitions[2] };
            var restored = new CampaignDeckState(new List<CardDefinition>());
            CampaignRunMapper.ReadDeck(save, restored, id => Resolve(reduced, id));

            Assert.That(restored.Cards, Has.Count.EqualTo(2));
            Assert.That(restored.ContainsDefinition(definitions[0].Id), Is.False);
            DestroyCards(definitions);
        }

        [Test]
        public void Service_SaveLoadClear_RoundTripsThroughStore()
        {
            var store = new InMemoryStore();
            var service = new CampaignRunSaveService(store);
            Assert.That(service.HasSave, Is.False);

            var save = new CampaignRunSave
            {
                playerLevel = 3,
                roomsCleared = 7,
                campaignScenarioId = "mirror",
                merchantRoomsBlockedUntilMonster = true,
                nextMonsterTierBonus = 2
            };
            save.deck.Add(new CampaignCardSave
            {
                definitionId = "card-1",
                zone = (int)CampaignCardZone.Cooldown,
                instanceId = 4
            });
            save.nextInstanceId = 5;

            service.Save(save);
            Assert.That(service.HasSave, Is.True);
            Assert.That(service.TryLoad(out CampaignRunSave loaded), Is.True);

            Assert.That(loaded.playerLevel, Is.EqualTo(3));
            Assert.That(loaded.roomsCleared, Is.EqualTo(7));
            Assert.That(loaded.campaignScenarioId, Is.EqualTo("mirror"));
            Assert.That(loaded.merchantRoomsBlockedUntilMonster, Is.True);
            Assert.That(loaded.nextMonsterTierBonus, Is.EqualTo(2));
            Assert.That(loaded.deck, Has.Count.EqualTo(1));
            Assert.That(loaded.deck[0].definitionId, Is.EqualTo("card-1"));
            Assert.That(loaded.deck[0].zone, Is.EqualTo((int)CampaignCardZone.Cooldown));
            Assert.That(loaded.nextInstanceId, Is.EqualTo(5));

            service.Clear();
            Assert.That(service.HasSave, Is.False);
        }

        private static RunProgressState CreateProgress()
        {
            return new RunProgressState(
                experiencePerLevel: 5,
                roomClearExperience: 2,
                maximumLevel: 5,
                roomsPerMasterLevel: 3,
                vigorDiceByLevel: new[] { 6, 8, 10, 12, 20 });
        }

        private static CardDefinition Resolve(List<CardDefinition> definitions, string id)
        {
            foreach (CardDefinition definition in definitions)
                if (definition.Id == id)
                    return definition;
            return null;
        }

        private static List<CardDefinition> CreateCards(int count)
        {
            var result = new List<CardDefinition>();
            for (int index = 0; index < count; index++)
                result.Add(CreateCard($"card-{index}", $"Card {index}", index + 2, HeroClass.Warrior));
            return result;
        }

        private static CardDefinition CreateCard(string id, string displayName, int strength, HeroClass heroClass)
        {
            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ApplyImportedData(id, displayName, CardCategory.Monster, null, strength, true, heroClass, true);
            return card;
        }

        private static void DestroyCards(IEnumerable<CardDefinition> cards)
        {
            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        private sealed class FixedRandom : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }

        private sealed class InMemoryStore : ICampaignRunStore
        {
            private string json;

            public void Save(string value) => json = value;
            public bool TryLoad(out string value)
            {
                value = json;
                return !string.IsNullOrEmpty(value);
            }
            public bool Exists() => !string.IsNullOrEmpty(json);
            public void Delete() => json = null;
        }
    }
}
