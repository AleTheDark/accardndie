using AccardND.GameCore;
using AccardND.GameCore.Pvp;

namespace AccardND.Battlefield
{
    public interface IBattlePresentationActions
    {
        void Deploy(int handIndex);
        void Attack(int enemySlot);
        void UseAbility(bool targetIsEnemy, int targetSlot);
        void Attach(int allySlot);
        void Pass();
        void SubmitDecisive(int[] loadoutIndices);
        void LeaveToLobby();
    }

    public sealed class BattlePresentationCard
    {
        public int Initiative { get; set; }
        public bool IsSpirit { get; set; }
        public bool Inhibited { get; set; }
        public bool Marked { get; set; }
        public bool Protecting { get; set; }
        public bool AbilityUsed { get; set; }
        public int PendingBonus { get; set; }
        public PvpPendingBonusKind PendingBonusKind { get; set; }
        public int DiePenaltySteps { get; set; }
    }

    public sealed class BattlePresentationEvent
    {
        public string Type { get; set; }
        public int Player { get; set; }
        public int Slot { get; set; }
        public int TargetPlayer { get; set; }
        public int TargetSlot { get; set; }
        public string CardId { get; set; }
        public bool HasHeroClass { get; set; }
        public HeroClass HeroClass { get; set; }
        public bool HasAbilityClass { get; set; }
        public HeroClass AbilityClass { get; set; }
        public int AbilityMagnitude { get; set; }
        public int Initiative { get; set; }
        public CombatCertainty Certainty { get; set; }
        public int AttackerDieSides { get; set; }
        public int DefenderDieSides { get; set; }
        public int AttackerRollFirst { get; set; }
        public int AttackerRollSecond { get; set; }
        public bool AttackerRollHasSecond { get; set; }
        public int AttackerRollSelected { get; set; }
        public VigorSelectionMode AttackerRollSelectionMode { get; set; }
        public int AttackerRollFirstBeforeReroll { get; set; }
        public int AttackerRollSecondBeforeReroll { get; set; }
        public int AttackerTotal { get; set; }
        public int DefenderRollFirst { get; set; }
        public int DefenderRollSecond { get; set; }
        public bool DefenderRollHasSecond { get; set; }
        public int DefenderRollSelected { get; set; }
        public VigorSelectionMode DefenderRollSelectionMode { get; set; }
        public int DefenderRollFirstBeforeReroll { get; set; }
        public int DefenderRollSecondBeforeReroll { get; set; }
        public int DefenderTotal { get; set; }
        public bool DefenderLostLife { get; set; }
        public bool DefenderEliminated { get; set; }
        public bool BecameSpirit { get; set; }
        public bool Overkill { get; set; }
        public bool Redirected { get; set; }
    }
}
