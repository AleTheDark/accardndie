using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public enum FlashTrialCurrencyReward
    {
        Experience,
        Gold
    }

    public readonly struct FlashTrialSlotOutcome
    {
        public FlashTrialSlotOutcome(HeroClass heroClass, int strength,
            FlashTrialCurrencyReward currency, int amount)
        {
            HeroClass = heroClass;
            Strength = strength;
            Currency = currency;
            Amount = amount;
            CardId = null;
            FirstConsumableName = null;
            FirstConsumableResource = null;
            SecondConsumableName = null;
            SecondConsumableResource = null;
        }

        public HeroClass HeroClass { get; }
        public int Strength { get; }
        public FlashTrialCurrencyReward Currency { get; }
        public int Amount { get; }
        public string CardId { get; }
        public string FirstConsumableName { get; }
        public string FirstConsumableResource { get; }
        public string SecondConsumableName { get; }
        public string SecondConsumableResource { get; }
        public bool HasConsumableRewards => !string.IsNullOrEmpty(FirstConsumableName);

        public FlashTrialSlotOutcome(string cardId, HeroClass heroClass, int strength,
            FlashTrialCurrencyReward currency, int amount)
            : this(heroClass, strength, currency, amount)
        {
            CardId = cardId;
        }

        public FlashTrialSlotOutcome WithConsumables(string firstName, string firstResource,
            string secondName = null, string secondResource = null)
        {
            return new FlashTrialSlotOutcome(HeroClass, Strength, Currency, Amount,
                firstName, firstResource, secondName, secondResource);
        }

        private FlashTrialSlotOutcome(HeroClass heroClass, int strength,
            FlashTrialCurrencyReward currency, int amount, string firstName,
            string firstResource, string secondName, string secondResource)
            : this(heroClass, strength, currency, amount)
        {
            FirstConsumableName = firstName;
            FirstConsumableResource = firstResource;
            SecondConsumableName = secondName;
            SecondConsumableResource = secondResource;
        }
    }

    public readonly struct FlashTrialCardCandidate
    {
        public FlashTrialCardCandidate(string id, HeroClass heroClass, int strength)
        {
            Id = id;
            HeroClass = heroClass;
            Strength = strength;
        }

        public string Id { get; }
        public HeroClass HeroClass { get; }
        public int Strength { get; }
    }

    /// <summary>Genera il premio della slot che segue una Prova Lampo completata.</summary>
    public sealed class FlashTrialSlotMachine
    {
        private static readonly HeroClass[] Classes =
        {
            HeroClass.Assassin, HeroClass.Warrior, HeroClass.Mage,
            HeroClass.Paladin, HeroClass.Rogue, HeroClass.Hunter,
            HeroClass.Barbarian, HeroClass.Necromancer, HeroClass.Priest
        };

        private readonly Random random;

        public FlashTrialSlotMachine(int seed)
        {
            random = new Random(seed);
        }

        public FlashTrialSlotOutcome Roll(FlashTrialResult performance, int completedMemoryLevels)
        {
            HeroClass heroClass = Classes[random.Next(Classes.Length)];
            int strength = Math.Max(2, Math.Min(10, completedMemoryLevels + 2));
            FlashTrialCurrencyReward currency = random.Next(2) == 0
                ? FlashTrialCurrencyReward.Experience
                : FlashTrialCurrencyReward.Gold;
            int amount = RewardAmount(performance, currency);
            return new FlashTrialSlotOutcome(heroClass, strength, currency, amount);
        }

        /// <summary>
        /// Estrae soltanto fra carte realmente disponibili e gia' filtrate dal chiamante.
        /// Preferisce la potenza obiettivo; se manca, usa la piu' vicina e a parita' quella
        /// inferiore. Il rullo classe mostra la classe della carta scelta, non un risultato
        /// indipendente che potrebbe non esistere nel catalogo.
        /// </summary>
        public FlashTrialSlotOutcome Roll(FlashTrialResult performance, int completedMemoryLevels,
            IReadOnlyList<FlashTrialCardCandidate> eligibleCards)
        {
            if (eligibleCards == null) throw new ArgumentNullException(nameof(eligibleCards));
            if (eligibleCards.Count == 0) throw new InvalidOperationException("Nessuna carta nuova disponibile.");

            int targetStrength = Math.Max(2, Math.Min(10, completedMemoryLevels + 2));
            int bestDistance = int.MaxValue;
            bool bestIsAbove = true;
            var best = new List<FlashTrialCardCandidate>();
            foreach (FlashTrialCardCandidate candidate in eligibleCards)
            {
                int distance = Math.Abs(candidate.Strength - targetStrength);
                bool isAbove = candidate.Strength > targetStrength;
                if (distance < bestDistance || (distance == bestDistance && bestIsAbove && !isAbove))
                {
                    bestDistance = distance;
                    bestIsAbove = isAbove;
                    best.Clear();
                    best.Add(candidate);
                }
                else if (distance == bestDistance && isAbove == bestIsAbove)
                {
                    best.Add(candidate);
                }
            }

            FlashTrialCardCandidate selected = best[random.Next(best.Count)];
            FlashTrialCurrencyReward currency = random.Next(2) == 0
                ? FlashTrialCurrencyReward.Experience
                : FlashTrialCurrencyReward.Gold;
            return new FlashTrialSlotOutcome(selected.Id, selected.HeroClass, selected.Strength,
                currency, RewardAmount(performance, currency));
        }

        private static int RewardAmount(FlashTrialResult performance, FlashTrialCurrencyReward currency)
        {
            if (currency == FlashTrialCurrencyReward.Experience)
            {
                return performance switch
                {
                    FlashTrialResult.Perfect => 50,
                    FlashTrialResult.Excellent => 30,
                    FlashTrialResult.Good => 20,
                    FlashTrialResult.Completed => 10,
                    _ => 5
                };
            }

            return performance switch
            {
                FlashTrialResult.Perfect => 38,
                FlashTrialResult.Excellent => 27,
                FlashTrialResult.Good => 18,
                FlashTrialResult.Completed => 12,
                _ => 6
            };
        }
    }
}
