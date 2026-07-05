namespace AccardND.GameCore.Pvp
{
    /// <summary>Eventi emessi dal motore: il server li inoltra ai client,
    /// che li riproducono senza rieseguire la logica.</summary>
    public abstract class PvpEvent
    {
    }

    public sealed class RoundStartedEvent : PvpEvent
    {
        public RoundStartedEvent(int matchRound, int vigorDieSides)
        {
            MatchRound = matchRound;
            VigorDieSides = vigorDieSides;
        }

        public int MatchRound { get; }
        public int VigorDieSides { get; }
    }

    public sealed class DecisiveSelectionStartedEvent : PvpEvent
    {
        public DecisiveSelectionStartedEvent(int requiredCount)
        {
            RequiredCount = requiredCount;
        }

        public int RequiredCount { get; }
    }

    public sealed class HandReadyEvent : PvpEvent
    {
        public HandReadyEvent(int player)
        {
            Player = player;
        }

        public int Player { get; }
    }

    public sealed class DeploymentStartedEvent : PvpEvent
    {
        public DeploymentStartedEvent(int firstPlayer, int rollPlayer0, int rollPlayer1)
        {
            FirstPlayer = firstPlayer;
            RollPlayer0 = rollPlayer0;
            RollPlayer1 = rollPlayer1;
        }

        public int FirstPlayer { get; }
        public int RollPlayer0 { get; }
        public int RollPlayer1 { get; }
    }

    public sealed class DeploymentInitiativeEvent : PvpEvent
    {
        public DeploymentInitiativeEvent(int order, int player, int initiative)
        {
            Order = order;
            Player = player;
            Initiative = initiative;
        }

        public int Order { get; }
        public int Player { get; }
        public int Initiative { get; }
    }

    public sealed class DeployTurnEvent : PvpEvent
    {
        public DeployTurnEvent(int player)
        {
            Player = player;
        }

        public int Player { get; }
    }

    public sealed class CardDeployedEvent : PvpEvent
    {
        public CardDeployedEvent(
            int player,
            int slot,
            string cardId,
            string cardName,
            HeroClass heroClass,
            int strength,
            int lives,
            int initiative)
        {
            Player = player;
            Slot = slot;
            CardId = cardId;
            CardName = cardName;
            HeroClass = heroClass;
            Strength = strength;
            Lives = lives;
            Initiative = initiative;
        }

        public int Player { get; }
        public int Slot { get; }
        public string CardId { get; }
        public string CardName { get; }
        public HeroClass HeroClass { get; }
        public int Strength { get; }
        public int Lives { get; }
        public int Initiative { get; }
    }

    public sealed class BattleStartedEvent : PvpEvent
    {
        public BattleStartedEvent(PvpAuraType auraPlayer0, PvpAuraType auraPlayer1)
        {
            AuraPlayer0 = auraPlayer0;
            AuraPlayer1 = auraPlayer1;
        }

        public PvpAuraType AuraPlayer0 { get; }
        public PvpAuraType AuraPlayer1 { get; }
    }

    public sealed class CardInitiativeEvent : PvpEvent
    {
        public CardInitiativeEvent(int player, int slot, int initiative)
        {
            Player = player;
            Slot = slot;
            Initiative = initiative;
        }

        public int Player { get; }
        public int Slot { get; }
        public int Initiative { get; }
    }

    public sealed class TurnStartedEvent : PvpEvent
    {
        public TurnStartedEvent(int player, int slot, int cycle)
        {
            Player = player;
            Slot = slot;
            Cycle = cycle;
        }

        public int Player { get; }
        public int Slot { get; }
        public int Cycle { get; }
    }

    public sealed class TurnSkippedEvent : PvpEvent
    {
        public TurnSkippedEvent(int player, int slot, string reason)
        {
            Player = player;
            Slot = slot;
            Reason = reason;
        }

        public int Player { get; }
        public int Slot { get; }
        public string Reason { get; }
    }

    public sealed class AbilityUsedEvent : PvpEvent
    {
        public AbilityUsedEvent(int player, int slot, HeroClass ability, int targetPlayer, int targetSlot, int magnitude)
        {
            Player = player;
            Slot = slot;
            Ability = ability;
            TargetPlayer = targetPlayer;
            TargetSlot = targetSlot;
            Magnitude = magnitude;
        }

        public int Player { get; }
        public int Slot { get; }
        public HeroClass Ability { get; }
        public int TargetPlayer { get; }
        public int TargetSlot { get; }
        public int Magnitude { get; }
    }

    public sealed class CardRevivedEvent : PvpEvent
    {
        public CardRevivedEvent(int player, int slot, int lives)
        {
            Player = player;
            Slot = slot;
            Lives = lives;
        }

        public int Player { get; }
        public int Slot { get; }
        public int Lives { get; }
    }

    public sealed class ProtectionTriggeredEvent : PvpEvent
    {
        public ProtectionTriggeredEvent(int paladinPlayer, int paladinSlot, bool redirected)
        {
            PaladinPlayer = paladinPlayer;
            PaladinSlot = paladinSlot;
            Redirected = redirected;
        }

        public int PaladinPlayer { get; }
        public int PaladinSlot { get; }
        public bool Redirected { get; }
    }

    public sealed class AttackResolvedEvent : PvpEvent
    {
        public AttackResolvedEvent(
            int attackerPlayer,
            int attackerSlot,
            int defenderPlayer,
            int defenderSlot,
            CombatCertainty certainty,
            int attackerDieSides,
            int defenderDieSides,
            VigorRollResult attackerRoll,
            VigorRollResult defenderRoll,
            int attackerTotal,
            int defenderTotal,
            bool defenderLostLife,
            int defenderRemainingLives,
            bool defenderEliminated,
            bool becameSpirit,
            bool isCounter)
        {
            AttackerPlayer = attackerPlayer;
            AttackerSlot = attackerSlot;
            DefenderPlayer = defenderPlayer;
            DefenderSlot = defenderSlot;
            Certainty = certainty;
            AttackerDieSides = attackerDieSides;
            DefenderDieSides = defenderDieSides;
            AttackerRoll = attackerRoll;
            DefenderRoll = defenderRoll;
            AttackerTotal = attackerTotal;
            DefenderTotal = defenderTotal;
            DefenderLostLife = defenderLostLife;
            DefenderRemainingLives = defenderRemainingLives;
            DefenderEliminated = defenderEliminated;
            BecameSpirit = becameSpirit;
            IsCounter = isCounter;
        }

        public int AttackerPlayer { get; }
        public int AttackerSlot { get; }
        public int DefenderPlayer { get; }
        public int DefenderSlot { get; }
        public CombatCertainty Certainty { get; }
        public int AttackerDieSides { get; }
        public int DefenderDieSides { get; }
        public VigorRollResult AttackerRoll { get; }
        public VigorRollResult DefenderRoll { get; }
        public int AttackerTotal { get; }
        public int DefenderTotal { get; }
        public bool DefenderLostLife { get; }
        public int DefenderRemainingLives { get; }
        public bool DefenderEliminated { get; }
        public bool BecameSpirit { get; }
        public bool IsCounter { get; }
    }

    public sealed class AttachmentAppliedEvent : PvpEvent
    {
        public AttachmentAppliedEvent(int player, int sourceSlot, int targetSlot, int bonus)
        {
            Player = player;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
            Bonus = bonus;
        }

        public int Player { get; }
        public int SourceSlot { get; }
        public int TargetSlot { get; }
        public int Bonus { get; }
    }

    public sealed class FuryGainedEvent : PvpEvent
    {
        public FuryGainedEvent(int player, int slot, int amount)
        {
            Player = player;
            Slot = slot;
            Amount = amount;
        }

        public int Player { get; }
        public int Slot { get; }
        public int Amount { get; }
    }

    public sealed class MightAuraBonusEvent : PvpEvent
    {
        public MightAuraBonusEvent(int player, int slot)
        {
            Player = player;
            Slot = slot;
        }

        public int Player { get; }
        public int Slot { get; }
    }

    public sealed class SpiritExpiredEvent : PvpEvent
    {
        public SpiritExpiredEvent(int player, int slot)
        {
            Player = player;
            Slot = slot;
        }

        public int Player { get; }
        public int Slot { get; }
    }

    public sealed class RoundEndedEvent : PvpEvent
    {
        public RoundEndedEvent(int matchRound, int winner, int winsPlayer0, int winsPlayer1)
        {
            MatchRound = matchRound;
            Winner = winner;
            WinsPlayer0 = winsPlayer0;
            WinsPlayer1 = winsPlayer1;
        }

        public int MatchRound { get; }
        public int Winner { get; }
        public int WinsPlayer0 { get; }
        public int WinsPlayer1 { get; }
    }

    public sealed class MatchEndedEvent : PvpEvent
    {
        public MatchEndedEvent(int winner, int winsPlayer0, int winsPlayer1)
        {
            Winner = winner;
            WinsPlayer0 = winsPlayer0;
            WinsPlayer1 = winsPlayer1;
        }

        public int Winner { get; }
        public int WinsPlayer0 { get; }
        public int WinsPlayer1 { get; }
    }
}
