using System.Collections.Generic;
using AccardND.GameCore;

namespace AccardND.Battlefield
{
    public enum BattlePresentationPhase
    {
        Waiting,
        Deployment,
        Battle,
        DecisiveSelection,
        Finished
    }

    public sealed class BattlePresentationState
    {
        public int LocalPlayerIndex { get; set; } = -1;
        public string OpponentName { get; set; } = string.Empty;
        public BattlePresentationPhase Phase { get; set; } = BattlePresentationPhase.Waiting;
        public int MatchRound { get; set; }
        public int VigorDieSides { get; set; }
        public int LocalVigorDieSides { get; set; }
        public int OpponentVigorDieSides { get; set; }
        public int DeployPlayer { get; set; } = -1;
        public int ActivePlayer { get; set; } = -1;
        public int ActiveSlot { get; set; } = -1;
        public int Winner { get; set; } = -1;
        public int DecisiveRequiredCount { get; set; }
        public int[] Wins { get; } = new int[2];
        public string[] Auras { get; } = { string.Empty, string.Empty };
        public List<BattlePresentationCard>[] Boards { get; } = { new(), new() };
        public List<BattlePresentationHandCard> Hand { get; } = new();
        public List<BattlePresentationDeploymentToken> DeploymentOrder { get; } = new();
        public List<string> Log { get; } = new();

        public bool IsLocalDeployTurn => Phase == BattlePresentationPhase.Deployment && DeployPlayer == LocalPlayerIndex;
        public bool IsLocalBattleTurn => Phase == BattlePresentationPhase.Battle && ActivePlayer == LocalPlayerIndex;
    }

    public sealed class BattlePresentationCard
    {
        public int Slot { get; set; }
        public string CardId { get; set; }
        public string CardName { get; set; }
        public HeroClass HeroClass { get; set; }
        public int Strength { get; set; }
        public int Lives { get; set; }
        public int MaximumLives { get; set; } = 2;
        public int Initiative { get; set; }
        public bool Eliminated { get; set; }
        public bool IsSpirit { get; set; }
        public bool Inhibited { get; set; }
        public bool Marked { get; set; }
        public bool Protecting { get; set; }
        public int PermanentBonus { get; set; }
        public int PendingBonus { get; set; }
        public int DiePenaltySteps { get; set; }
    }

    public sealed class BattlePresentationHandCard
    {
        public int LoadoutIndex { get; set; }
        public string DefinitionId { get; set; }
    }

    public sealed class BattlePresentationLoadoutCard
    {
        public string DefinitionId { get; set; }
        public int Value { get; set; }
        public HeroClass HeroClass { get; set; }
    }

    public sealed class BattlePresentationDeploymentToken
    {
        public int Order { get; set; }
        public int Player { get; set; }
        public int Initiative { get; set; }
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
        public int Initiative { get; set; }
        public int AttackerDieSides { get; set; }
        public int DefenderDieSides { get; set; }
        public int AttackerRollFirst { get; set; }
        public int AttackerRollSecond { get; set; }
        public bool AttackerRollHasSecond { get; set; }
        public int AttackerRollSelected { get; set; }
        public int DefenderRollFirst { get; set; }
        public int DefenderRollSecond { get; set; }
        public bool DefenderRollHasSecond { get; set; }
        public int DefenderRollSelected { get; set; }
        public bool DefenderLostLife { get; set; }
        public bool DefenderEliminated { get; set; }
        public bool BecameSpirit { get; set; }
    }
}
