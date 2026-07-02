using System;

namespace AccardND.GameCore.Pvp
{
    public readonly struct PvpInitiativeResult
    {
        public PvpInitiativeResult(int firstPlayerRoll, int secondPlayerRoll)
        {
            FirstPlayerRoll = firstPlayerRoll;
            SecondPlayerRoll = secondPlayerRoll;
        }

        public int FirstPlayerRoll { get; }
        public int SecondPlayerRoll { get; }
        public bool FirstPlayerStarts => FirstPlayerRoll > SecondPlayerRoll;
    }

    public static class PvpInitiative
    {
        /// <summary>Tira l'iniziativa per entrambi i giocatori, ritirando i pareggi.</summary>
        public static PvpInitiativeResult RollOff(IRandomSource random, int dieSides)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (dieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(dieSides));

            int first;
            int second;
            do
            {
                first = random.NextInclusive(1, dieSides);
                second = random.NextInclusive(1, dieSides);
            }
            while (first == second);

            return new PvpInitiativeResult(first, second);
        }
    }
}
