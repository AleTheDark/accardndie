using System;
using System.Collections.Generic;
using AccardND.GameCore.Pvp;
using UnityEngine;

namespace AccardND.GameData
{
    [Serializable]
    public struct PvpDieCostEntry
    {
        [SerializeField, Min(2)] private int sides;
        [SerializeField, Min(0)] private int cost;

        public PvpDieCostEntry(int sides, int cost)
        {
            this.sides = sides;
            this.cost = cost;
        }

        public int Sides => sides;
        public int Cost => cost;
    }

    [Serializable]
    public struct PvpCardValueLimitEntry
    {
        [SerializeField, Min(1)] private int cardValue;
        [SerializeField, Min(0)] private int maximumCopies;

        public PvpCardValueLimitEntry(int cardValue, int maximumCopies)
        {
            this.cardValue = cardValue;
            this.maximumCopies = maximumCopies;
        }

        public int CardValue => cardValue;
        public int MaximumCopies => maximumCopies;
    }

    [Serializable]
    public sealed class PvpConfiguration
    {
        [Header("Loadout")]
        [SerializeField, Min(0)] private int loadoutBudget = 60;
        [SerializeField, Range(1, 30)] private int loadoutCardCount = 9;
        [SerializeField, Tooltip("Costo in punti per ogni valore carta: indice 0 = valore 1.")]
        private int[] cardCostByValue = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        [SerializeField] private PvpCardValueLimitEntry[] cardValueLimits =
        {
            new(10, 1),
            new(9, 2),
            new(8, 3),
            new(7, 4)
        };

        [Header("Dadi")]
        [SerializeField] private PvpDieCostEntry[] baseDieCosts =
        {
            new(3, 0),
            new(4, 4),
            new(6, 8),
            new(8, 13)
        };
        [SerializeField] private PvpDieCostEntry[] bagDieCosts =
        {
            new(6, 2),
            new(8, 4),
            new(10, 7),
            new(12, 10),
            new(20, 18)
        };

        [Header("Match")]
        [SerializeField, Min(1)] private int roundsToWinMatch = 2;
        [SerializeField, Min(1)] private int deployedCardLives = 2;
        [SerializeField, Min(0), Tooltip("Secondi per mossa; 0 = nessun timer.")]
        private int turnTimerSeconds = 60;
        [SerializeField, Min(1)] private int disconnectTimeoutSeconds = 120;

        public int LoadoutBudget => loadoutBudget;
        public int LoadoutCardCount => loadoutCardCount;
        public int RoundsToWinMatch => roundsToWinMatch;
        public int DeployedCardLives => deployedCardLives;
        public int TurnTimerSeconds => turnTimerSeconds;
        public int DisconnectTimeoutSeconds => disconnectTimeoutSeconds;

        public PvpLoadoutRules ToLoadoutRules() => new(
            loadoutBudget,
            loadoutCardCount,
            cardCostByValue,
            ToDictionary(cardValueLimits),
            ToDictionary(baseDieCosts),
            ToDictionary(bagDieCosts));

        private static Dictionary<int, int> ToDictionary(PvpCardValueLimitEntry[] entries)
        {
            var result = new Dictionary<int, int>();
            if (entries == null)
                return result;
            foreach (PvpCardValueLimitEntry entry in entries)
                result[entry.CardValue] = entry.MaximumCopies;
            return result;
        }

        private static Dictionary<int, int> ToDictionary(PvpDieCostEntry[] entries)
        {
            var result = new Dictionary<int, int>();
            if (entries == null)
                return result;
            foreach (PvpDieCostEntry entry in entries)
                result[entry.Sides] = entry.Cost;
            return result;
        }
    }
}
