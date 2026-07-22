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

        /// <summary>Indice assoluto (0/1) del destinatario nel match: serve a
        /// interpretare i campi player degli eventi.</summary>
        public int yourPlayerIndex;
    }

    /// <summary>Mano privata del round: solo indici e id del proprio loadout.</summary>
    [Serializable]
    public sealed class MatchHand
    {
        public int roundNumber;
        public int[] handIndices;
        public string[] handDefinitionIds;
    }

    /// <summary>Azione inviata dal client durante il match.</summary>
    [Serializable]
    public sealed class MatchActionDto
    {
        public const string Deploy = "deploy";
        public const string Ability = "ability";
        public const string Attack = "attack";
        public const string Attach = "attach";
        public const string Pass = "pass";
        public const string Decisive = "decisive";

        public string action;
        public int handIndex;
        public bool targetIsEnemy;
        public int targetSlot;
        public int[] decisiveIndices;
    }

    /// <summary>Evento di match, forma appiattita per JsonUtility: i campi non
    /// pertinenti al tipo restano a zero. "type" replica il nome dell'evento
    /// del motore (es. "AttackResolved", "RoundStarted").</summary>
    [Serializable]
    public sealed class MatchEventDto
    {
        public string type;
        public int player;
        public int slot;
        public int targetPlayer;
        public int targetSlot;
        public int matchRound;
        public int vigorDieSides;
        public int cycle;
        public int firstPlayer;
        public int rollPlayer0;
        public int rollPlayer1;
        public string cardId;
        public string cardName;
        public int heroClass;
        public int strength;
        public int lives;
        public int initiative;
        public int auraPlayer0;
        public int auraPlayer1;
        public int ability;
        public int magnitude;
        public bool redirected;
        public string certainty;
        public int attackerDieSides;
        public int defenderDieSides;
        public int attackerRollFirst;
        public int attackerRollSecond;
        public bool attackerRollHasSecond;
        public int attackerRollSelected;
        public int attackerRollSelectionMode;
        public int attackerRollFirstBeforeReroll;
        public int attackerRollSecondBeforeReroll;
        public int defenderRollFirst;
        public int defenderRollSecond;
        public bool defenderRollHasSecond;
        public int defenderRollSelected;
        public int defenderRollSelectionMode;
        public int defenderRollFirstBeforeReroll;
        public int defenderRollSecondBeforeReroll;
        public int attackerTotal;
        public int defenderTotal;
        public bool defenderLostLife;
        public int defenderRemainingLives;
        public bool defenderEliminated;
        public bool becameSpirit;
        public bool overkill;
        public bool isCounter;
        public int bonus;
        public int amount;
        public int winner;
        public int winsPlayer0;
        public int winsPlayer1;
        public int requiredCount;
        public string reason;
    }

    [Serializable]
    public sealed class MatchEventBatch
    {
        public MatchEventDto[] events;
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
