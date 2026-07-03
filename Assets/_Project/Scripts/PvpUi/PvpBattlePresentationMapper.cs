using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;

namespace AccardND.PvpUi
{
    internal static class PvpBattlePresentationMapper
    {
        public static BattlePresentationState ToPresentationState(PvpClientMatchState source)
        {
            var target = new BattlePresentationState();
            if (source == null)
                return target;

            target.LocalPlayerIndex = source.MyIndex;
            target.OpponentName = source.OpponentName ?? string.Empty;
            target.Phase = ToPresentationPhase(source.Phase);
            target.MatchRound = source.MatchRound;
            target.VigorDieSides = source.VigorDieSides;
            target.LocalVigorDieSides = source.VigorDieSides;
            target.OpponentVigorDieSides = source.VigorDieSides;
            target.DeployPlayer = source.DeployPlayer;
            target.ActivePlayer = source.ActivePlayer;
            target.ActiveSlot = source.ActiveSlot;
            target.Winner = source.Winner;
            target.DecisiveRequiredCount = source.DecisiveRequiredCount;
            target.Wins[0] = source.Wins[0];
            target.Wins[1] = source.Wins[1];
            target.Auras[0] = AuraLabel(source.Auras[0]);
            target.Auras[1] = AuraLabel(source.Auras[1]);

            CopyBoard(source.Boards[0], target.Boards[0]);
            CopyBoard(source.Boards[1], target.Boards[1]);

            foreach ((int loadoutIndex, string definitionId) in source.Hand)
                target.Hand.Add(new BattlePresentationHandCard
                {
                    LoadoutIndex = loadoutIndex,
                    DefinitionId = definitionId
                });

            foreach (PvpClientDeploymentToken token in source.DeploymentOrder)
                target.DeploymentOrder.Add(new BattlePresentationDeploymentToken
                {
                    Order = token.Order,
                    Player = token.Player,
                    Initiative = token.Initiative
                });

            foreach (string logLine in source.Log)
                target.Log.Add(logLine);

            return target;
        }

        public static List<BattlePresentationLoadoutCard> ToPresentationLoadout(List<LoadoutCardDto> source)
        {
            var target = new List<BattlePresentationLoadoutCard>();
            if (source == null)
                return target;

            foreach (LoadoutCardDto card in source)
            {
                if (card == null)
                    continue;

                target.Add(new BattlePresentationLoadoutCard
                {
                    DefinitionId = card.definitionId,
                    Value = card.value,
                    HeroClass = (HeroClass)card.heroClass
                });
            }
            return target;
        }

        public static BattlePresentationEvent ToPresentationEvent(MatchEventDto source, PvpClientMatchState state = null)
        {
            if (source == null)
                return null;

            var target = new BattlePresentationEvent
            {
                Type = source.type,
                Player = source.player,
                Slot = source.slot,
                TargetPlayer = source.targetPlayer,
                TargetSlot = source.targetSlot,
                CardId = source.cardId,
                Initiative = source.initiative,
                HasHeroClass = source.heroClass > 0,
                HeroClass = (HeroClass)source.heroClass,
                HasAbilityClass = source.ability > 0,
                AbilityClass = (HeroClass)source.ability,
                AttackerDieSides = source.attackerDieSides,
                DefenderDieSides = source.defenderDieSides,
                AttackerRollFirst = source.attackerRollFirst,
                AttackerRollSecond = source.attackerRollSecond,
                AttackerRollHasSecond = source.attackerRollHasSecond,
                AttackerRollSelected = source.attackerRollSelected,
                DefenderRollFirst = source.defenderRollFirst,
                DefenderRollSecond = source.defenderRollSecond,
                DefenderRollHasSecond = source.defenderRollHasSecond,
                DefenderRollSelected = source.defenderRollSelected,
                DefenderLostLife = source.defenderLostLife,
                DefenderEliminated = source.defenderEliminated,
                BecameSpirit = source.becameSpirit
            };

            if (!target.HasHeroClass
                && string.Equals(source.type, "AttackResolved", System.StringComparison.Ordinal)
                && TryFindHeroClass(state, source.player, source.slot, out HeroClass attackerClass))
            {
                target.HasHeroClass = true;
                target.HeroClass = attackerClass;
            }

            return target;
        }

        private static void CopyBoard(List<PvpClientCard> source, List<BattlePresentationCard> target)
        {
            foreach (PvpClientCard card in source)
                target.Add(new BattlePresentationCard
                {
                    Slot = card.Slot,
                    CardId = card.CardId,
                    CardName = card.CardName,
                    HeroClass = card.HeroClass,
                    Strength = card.Strength,
                    Lives = card.Lives,
                    MaximumLives = 2,
                    Initiative = card.Initiative,
                    Eliminated = card.Eliminated,
                    IsSpirit = card.IsSpirit,
                    Inhibited = card.Inhibited,
                    Marked = card.Marked,
                    Protecting = card.Protecting,
                    PermanentBonus = card.PermanentBonus,
                    PendingBonus = card.PendingBonus,
                    DiePenaltySteps = card.DiePenaltySteps
                });
        }

        private static BattlePresentationPhase ToPresentationPhase(PvpClientPhase phase) =>
            phase switch
            {
                PvpClientPhase.Deployment => BattlePresentationPhase.Deployment,
                PvpClientPhase.Battle => BattlePresentationPhase.Battle,
                PvpClientPhase.DecisiveSelection => BattlePresentationPhase.DecisiveSelection,
                PvpClientPhase.Finished => BattlePresentationPhase.Finished,
                _ => BattlePresentationPhase.Waiting
            };

        private static string AuraLabel(PvpAuraType aura) =>
            aura == PvpAuraType.None ? string.Empty : aura.ToString();

        private static bool TryFindHeroClass(
            PvpClientMatchState state,
            int player,
            int slot,
            out HeroClass heroClass)
        {
            heroClass = default;
            if (state == null || player is < 0 or > 1)
                return false;

            foreach (PvpClientCard card in state.Boards[player])
            {
                if (card.Slot == slot)
                {
                    heroClass = card.HeroClass;
                    return true;
                }
            }
            return false;
        }
    }
}
