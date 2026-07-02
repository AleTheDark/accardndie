using System;
using System.Collections.Generic;

namespace AccardND.GameCore.Pvp
{
    public sealed class PvpLoadoutRules
    {
        private readonly int[] cardCostByValue;
        private readonly Dictionary<int, int> cardCountLimitsByValue;
        private readonly Dictionary<int, int> baseDieCostBySides;
        private readonly Dictionary<int, int> bagDieCostBySides;

        public PvpLoadoutRules(
            int budget,
            int requiredCardCount,
            IReadOnlyList<int> cardCostByValue,
            IReadOnlyDictionary<int, int> cardCountLimitsByValue,
            IReadOnlyDictionary<int, int> baseDieCostBySides,
            IReadOnlyDictionary<int, int> bagDieCostBySides)
        {
            if (budget < 0)
                throw new ArgumentOutOfRangeException(nameof(budget));
            if (requiredCardCount < 1)
                throw new ArgumentOutOfRangeException(nameof(requiredCardCount));
            if (cardCostByValue == null || cardCostByValue.Count < 1)
                throw new ArgumentException("Serve un costo per ogni valore carta.", nameof(cardCostByValue));
            if (baseDieCostBySides == null || baseDieCostBySides.Count < 1)
                throw new ArgumentException("Serve almeno un dado base disponibile.", nameof(baseDieCostBySides));
            if (bagDieCostBySides == null)
                throw new ArgumentNullException(nameof(bagDieCostBySides));

            Budget = budget;
            RequiredCardCount = requiredCardCount;
            this.cardCostByValue = new int[cardCostByValue.Count];
            for (int index = 0; index < cardCostByValue.Count; index++)
            {
                if (cardCostByValue[index] < 0)
                    throw new ArgumentException("I costi carta non possono essere negativi.", nameof(cardCostByValue));
                this.cardCostByValue[index] = cardCostByValue[index];
            }
            this.cardCountLimitsByValue = CopyCosts(cardCountLimitsByValue, nameof(cardCountLimitsByValue));
            this.baseDieCostBySides = CopyCosts(baseDieCostBySides, nameof(baseDieCostBySides));
            this.bagDieCostBySides = CopyCosts(bagDieCostBySides, nameof(bagDieCostBySides));
        }

        public int Budget { get; }
        public int RequiredCardCount { get; }
        public int MaximumCardValue => cardCostByValue.Length;
        public IReadOnlyCollection<int> AllowedBaseDieSides => baseDieCostBySides.Keys;
        public IReadOnlyCollection<int> AllowedBagDieSides => bagDieCostBySides.Keys;

        public bool TryGetCardCost(int cardValue, out int cost)
        {
            if (cardValue >= 1 && cardValue <= cardCostByValue.Length)
            {
                cost = cardCostByValue[cardValue - 1];
                return true;
            }
            cost = 0;
            return false;
        }

        public bool TryGetCardCountLimit(int cardValue, out int limit) =>
            cardCountLimitsByValue.TryGetValue(cardValue, out limit);

        public bool TryGetBaseDieCost(int sides, out int cost) =>
            baseDieCostBySides.TryGetValue(sides, out cost);

        public bool TryGetBagDieCost(int sides, out int cost) =>
            bagDieCostBySides.TryGetValue(sides, out cost);

        public static PvpLoadoutRules CreateDefault() => new(
            budget: 60,
            requiredCardCount: 9,
            cardCostByValue: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            cardCountLimitsByValue: new Dictionary<int, int>
            {
                [10] = 1,
                [9] = 2,
                [8] = 3,
                [7] = 4
            },
            baseDieCostBySides: new Dictionary<int, int>
            {
                [3] = 0,
                [4] = 4,
                [6] = 8,
                [8] = 13
            },
            bagDieCostBySides: new Dictionary<int, int>
            {
                [6] = 2,
                [8] = 4,
                [10] = 7,
                [12] = 10,
                [20] = 18
            });

        private static Dictionary<int, int> CopyCosts(
            IReadOnlyDictionary<int, int> source, string parameterName)
        {
            var copy = new Dictionary<int, int>();
            if (source == null)
                return copy;
            foreach (KeyValuePair<int, int> entry in source)
            {
                if (entry.Key < 1 || entry.Value < 0)
                    throw new ArgumentException("Voce non valida.", parameterName);
                copy[entry.Key] = entry.Value;
            }
            return copy;
        }
    }
}
