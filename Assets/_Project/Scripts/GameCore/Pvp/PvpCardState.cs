namespace AccardND.GameCore.Pvp
{
    public enum PvpPendingBonusKind
    {
        None,
        Blessing,
        Fury
    }

    /// <summary>Stato di una carta schierata in un round PvP.
    /// Ricreato a ogni round: le vite e gli effetti non persistono tra i round.</summary>
    public sealed class PvpCardState
    {
        public PvpCardState(int owner, int slot, int loadoutIndex, CombatCard card, int lives)
        {
            Owner = owner;
            Slot = slot;
            LoadoutIndex = loadoutIndex;
            Card = card;
            Lives = lives;
        }

        public int Owner { get; }
        public int Slot { get; }
        public int LoadoutIndex { get; }
        public CombatCard Card { get; }

        public int Lives { get; internal set; }
        public bool Eliminated { get; internal set; }
        public int Initiative { get; internal set; }
        public int TieBreaker { get; internal set; }

        public bool AbilityUsed { get; internal set; }
        public bool AbilityArmed { get; internal set; }
        public int PendingAttackBonus { get; internal set; }
        public PvpPendingBonusKind PendingBonusKind { get; internal set; }
        public int PermanentCombatBonus { get; internal set; }
        public int InhibitedTurns { get; internal set; }
        public bool WasInhibited { get; internal set; }
        public int PendingVigorStepPenalty { get; internal set; }
        public bool IsSpirit { get; internal set; }
        public bool IsAttachment { get; internal set; }
        public PvpCardState MarkedTarget { get; internal set; }
        public PvpCardState ProtectedAlly { get; internal set; }

        public bool IsActive => !Eliminated;

        /// <summary>La Furia del Barbarian vale anche in difesa; la Benedizione no.</summary>
        public int PendingDefenseBonus =>
            PendingBonusKind == PvpPendingBonusKind.Fury ? PendingAttackBonus : 0;
    }
}
