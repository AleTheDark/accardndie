using System;
using System.Collections.Generic;

namespace AccardND.GameCore.Pvp
{
    /// <summary>Parametri configurabili del match PvP. Il dado vigore è unico
    /// per entrambi i giocatori e scala col numero del round.</summary>
    public sealed class PvpMatchRules
    {
        private readonly int[] vigorDieByRound;

        public PvpMatchRules(
            int handSize,
            int formationSize,
            int decisiveHandSize,
            int roundsToWin,
            int cardLives,
            IReadOnlyList<int> vigorDieByRound,
            int initiativeDieSides,
            bool rogueRerollsOnes,
            int barbarianRageBonus,
            int hunterMarkBonus,
            int priestBlessingBonus)
        {
            if (handSize < 1)
                throw new ArgumentOutOfRangeException(nameof(handSize));
            if (formationSize < 1 || formationSize > handSize)
                throw new ArgumentOutOfRangeException(nameof(formationSize));
            if (decisiveHandSize < 1)
                throw new ArgumentOutOfRangeException(nameof(decisiveHandSize));
            if (roundsToWin < 1)
                throw new ArgumentOutOfRangeException(nameof(roundsToWin));
            if (cardLives < 1)
                throw new ArgumentOutOfRangeException(nameof(cardLives));
            if (vigorDieByRound == null || vigorDieByRound.Count < 1)
                throw new ArgumentException("Serve un dado vigore per ogni round.", nameof(vigorDieByRound));
            if (initiativeDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(initiativeDieSides));

            HandSize = handSize;
            FormationSize = formationSize;
            DecisiveHandSize = decisiveHandSize;
            RoundsToWin = roundsToWin;
            CardLives = cardLives;
            this.vigorDieByRound = new int[vigorDieByRound.Count];
            for (int index = 0; index < vigorDieByRound.Count; index++)
            {
                if (vigorDieByRound[index] < 2)
                    throw new ArgumentException("Dado vigore non valido.", nameof(vigorDieByRound));
                this.vigorDieByRound[index] = vigorDieByRound[index];
            }
            InitiativeDieSides = initiativeDieSides;
            RogueRerollsOnes = rogueRerollsOnes;
            BarbarianRageBonus = barbarianRageBonus;
            HunterMarkBonus = hunterMarkBonus;
            PriestBlessingBonus = priestBlessingBonus;
        }

        public int HandSize { get; }
        public int FormationSize { get; }
        public int DecisiveHandSize { get; }
        public int RoundsToWin { get; }
        public int CardLives { get; }
        public int InitiativeDieSides { get; }
        public bool RogueRerollsOnes { get; }
        public int BarbarianRageBonus { get; }
        public int HunterMarkBonus { get; }
        public int PriestBlessingBonus { get; }

        public int VigorDieForRound(int matchRound)
        {
            int index = Math.Clamp(matchRound - 1, 0, vigorDieByRound.Length - 1);
            return vigorDieByRound[index];
        }

        public static PvpMatchRules CreateDefault() => new(
            handSize: 6,
            formationSize: 3,
            decisiveHandSize: 3,
            roundsToWin: 2,
            cardLives: 2,
            vigorDieByRound: new[] { 4, 6, 8 },
            initiativeDieSides: 20,
            rogueRerollsOnes: true,
            barbarianRageBonus: 2,
            hunterMarkBonus: 2,
            priestBlessingBonus: 2);
    }
}
