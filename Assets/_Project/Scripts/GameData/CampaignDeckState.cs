using System;
using System.Collections.Generic;
using AccardND.GameCore;

namespace AccardND.GameData
{
    public enum CampaignCardZone
    {
        Deck,
        Hand,
        Battlefield,
        Cooldown,
        Graveyard
    }

    public sealed class CampaignCardInstance
    {
        internal CampaignCardInstance(int instanceId, CardDefinition definition)
        {
            InstanceId = instanceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Zone = CampaignCardZone.Deck;
        }

        public int InstanceId { get; }
        public CardDefinition Definition { get; }
        public CampaignCardZone Zone { get; internal set; }
    }

    /// <summary>Voce di uno snapshot del mazzo, per il save/resume della campagna.</summary>
    public readonly struct CampaignCardRestoreEntry
    {
        public CampaignCardRestoreEntry(CardDefinition definition, CampaignCardZone zone, int instanceId)
        {
            Definition = definition;
            Zone = zone;
            InstanceId = instanceId;
        }

        public CardDefinition Definition { get; }
        public CampaignCardZone Zone { get; }
        public int InstanceId { get; }
    }

    public sealed class CampaignDeckState
    {
        private readonly List<CampaignCardInstance> cards = new();
        private int nextInstanceId = 1;

        public CampaignDeckState(IReadOnlyList<CardDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            for (int index = 0; index < definitions.Count; index++)
                AddCard(definitions[index]);
        }

        public IReadOnlyList<CampaignCardInstance> Cards => cards;
        public int NextInstanceId => nextInstanceId;
        public int GraveyardCount => CountZone(CampaignCardZone.Graveyard);
        public int CooldownCount => CountZone(CampaignCardZone.Cooldown);
        public int AvailableCount => CountZone(CampaignCardZone.Deck);
        public int CombatReadyCount => CountCombatReadyCards();

        /// <summary>
        /// Ricostruisce il mazzo da uno snapshot salvato: svuota lo stato attuale, ricrea
        /// le istanze con la loro zona e reimposta il contatore degli instanceId. Bypassa il
        /// dedup di AddCard perché lo snapshot rappresenta uno stato già valido.
        /// </summary>
        public void RestoreFrom(IReadOnlyList<CampaignCardRestoreEntry> entries, int nextInstanceId)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            cards.Clear();
            int maxInstanceId = 0;
            foreach (CampaignCardRestoreEntry entry in entries)
            {
                if (entry.Definition == null)
                    throw new ArgumentException("Snapshot con definizione nulla.", nameof(entries));
                cards.Add(new CampaignCardInstance(entry.InstanceId, entry.Definition)
                {
                    Zone = entry.Zone
                });
                if (entry.InstanceId > maxInstanceId)
                    maxInstanceId = entry.InstanceId;
            }
            this.nextInstanceId = Math.Max(nextInstanceId, maxInstanceId + 1);
        }

        public bool ContainsDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return false;
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Definition.Id == definitionId)
                    return true;
            }
            return false;
        }

        public bool ContainsEquivalentDefinition(CardDefinition definition)
        {
            if (definition == null)
                return false;
            foreach (CampaignCardInstance card in cards)
            {
                if (CardPurchaseUniqueness.AreEquivalent(card.Definition, definition))
                    return true;
            }
            return false;
        }

        public CampaignCardInstance AddCard(CardDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (ContainsEquivalentDefinition(definition))
                return null;
            var instance = new CampaignCardInstance(nextInstanceId++, definition);
            cards.Add(instance);
            return instance;
        }

        public bool RemoveCard(CampaignCardInstance card)
        {
            if (card == null || !cards.Contains(card))
                return false;
            if (card.Zone == CampaignCardZone.Hand
                || card.Zone == CampaignCardZone.Battlefield
                || card.Zone == CampaignCardZone.Graveyard)
                return false;
            return cards.Remove(card);
        }

        public List<CampaignCardInstance> DrawCombatHand(IRandomSource random, int handSize)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (handSize < 1)
                throw new ArgumentOutOfRangeException(nameof(handSize));

            ReturnUnusedHandToDeck();
            var available = FindZone(CampaignCardZone.Deck);
            Shuffle(available, random);
            int drawCount = Math.Min(handSize, available.Count);
            var hand = available.GetRange(0, drawCount);
            foreach (CampaignCardInstance card in hand)
                card.Zone = CampaignCardZone.Hand;
            return hand;
        }

        public bool Deploy(CampaignCardInstance card)
        {
            if (card == null || card.Zone != CampaignCardZone.Hand || !cards.Contains(card))
                return false;
            card.Zone = CampaignCardZone.Battlefield;
            return true;
        }

        public int ReturnHandToDeck()
        {
            int returned = 0;
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Zone != CampaignCardZone.Hand)
                    continue;
                card.Zone = CampaignCardZone.Deck;
                returned++;
            }
            return returned;
        }

        public int ReleaseCooldown()
        {
            int released = 0;
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Zone != CampaignCardZone.Cooldown)
                    continue;
                card.Zone = CampaignCardZone.Deck;
                released++;
            }
            return released;
        }

        public void CompleteCombat(IReadOnlyCollection<CampaignCardInstance> defeatedCards, bool skipCooldown = false)
        {
            var defeated = defeatedCards != null
                ? new HashSet<CampaignCardInstance>(defeatedCards)
                : new HashSet<CampaignCardInstance>();

            // Le carte che hanno saltato questo combattimento tornano disponibili.
            ReleaseCooldown();

            // Le carte appena schierate vanno in cooldown o al cimitero.
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Zone != CampaignCardZone.Battlefield)
                    continue;
                card.Zone = defeated.Contains(card)
                    ? CampaignCardZone.Graveyard
                    : skipCooldown ? CampaignCardZone.Deck : CampaignCardZone.Cooldown;
            }
            ReturnUnusedHandToDeck();
        }

        public bool RecoverFromGraveyard(CampaignCardInstance card)
        {
            if (card == null || card.Zone != CampaignCardZone.Graveyard || !cards.Contains(card))
                return false;
            card.Zone = CampaignCardZone.Deck;
            return true;
        }

        private void ReturnUnusedHandToDeck()
        {
            ReturnHandToDeck();
        }

        private List<CampaignCardInstance> FindZone(CampaignCardZone zone)
        {
            var result = new List<CampaignCardInstance>();
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Zone == zone)
                    result.Add(card);
            }
            return result;
        }

        private int CountZone(CampaignCardZone zone)
        {
            int count = 0;
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Zone == zone)
                    count++;
            }
            return count;
        }

        private int CountCombatReadyCards()
        {
            int count = 0;
            foreach (CampaignCardInstance card in cards)
            {
                if (card.Zone == CampaignCardZone.Deck || card.Zone == CampaignCardZone.Hand)
                    count++;
            }
            return count;
        }

        private static void Shuffle(List<CampaignCardInstance> cards, IRandomSource random)
        {
            for (int index = cards.Count - 1; index > 0; index--)
            {
                int other = random.NextInclusive(0, index);
                (cards[index], cards[other]) = (cards[other], cards[index]);
            }
        }
    }
}
