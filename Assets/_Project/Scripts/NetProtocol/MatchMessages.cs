using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class MatchFound
    {
        public string opponentName;
        public string roomCode;
    }

    [Serializable]
    public sealed class MatchStart
    {
        public string opponentName;
        public int yourInitiative;
        public int opponentInitiative;
        public bool youDeployFirst;
        public int roundNumber;
    }

    /// <summary>Mano privata del round: solo indici e id del proprio loadout.</summary>
    [Serializable]
    public sealed class MatchHand
    {
        public int roundNumber;
        public int[] handIndices;
        public string[] handDefinitionIds;
    }

    [Serializable]
    public sealed class DieCostDto
    {
        public int sides;
        public int cost;
    }

    [Serializable]
    public sealed class CardValueLimitDto
    {
        public int value;
        public int maximumCopies;
    }

    /// <summary>Regole inviate dal server: il client le usa per la UI, l'autorità resta server-side.</summary>
    [Serializable]
    public sealed class RulesData
    {
        public int budget;
        public int requiredCardCount;
        public int[] cardCostByValue;
        public CardValueLimitDto[] cardValueLimits;
        public DieCostDto[] baseDieCosts;
        public DieCostDto[] bagDieCosts;
        public int roundsToWinMatch;
        public int deployedCardLives;
        public int turnTimerSeconds;
        public int disconnectTimeoutSeconds;
        public int initiativeDieSides;
    }
}
