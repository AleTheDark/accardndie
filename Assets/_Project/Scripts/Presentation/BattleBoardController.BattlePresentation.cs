using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private BattlePresentationState BuildCampaignBattlePresentationState()
    {
        var state = new BattlePresentationState
        {
            LocalPlayerIndex = 0,
            OpponentName = "CPU MASTER",
            Phase = CurrentBattlePresentationPhase(),
            MatchRound = roundNumber,
            VigorDieSides = runProgress != null ? runProgress.PlayerVigorDieSides : 0,
            LocalVigorDieSides = runProgress != null ? runProgress.PlayerVigorDieSides : 0,
            OpponentVigorDieSides = runProgress != null ? runProgress.MasterVigorDieSides : 0,
            ActivePlayer = -1,
            ActiveSlot = -1,
            DeployPlayer = deploymentDraftActive ? DeploymentOwner() : -1,
            Winner = gameFinished ? CampaignWinner() : -1
        };

        state.Auras[0] = AuraLabel(playerAura);
        state.Auras[1] = AuraLabel(cpuAura);
        CopyCampaignBoard(playerCards, state.Boards[0]);
        CopyCampaignBoard(cpuCards, state.Boards[1]);
        CopyCampaignDeploymentOrder(state.DeploymentOrder);
        CopyCampaignHand(state.Hand);
        CopyRecentCampaignLog(state.Log);
        FillActiveTurn(state);
        return state;
    }

    private BattlePresentationPhase CurrentBattlePresentationPhase()
    {
        if (gameFinished)
            return BattlePresentationPhase.Finished;
        if (deploymentDraftActive || draftActive)
            return BattlePresentationPhase.Deployment;
        if (roundNumber > 0 && turnOrder.Count > 0)
            return BattlePresentationPhase.Battle;
        return BattlePresentationPhase.Waiting;
    }

    private void FillActiveTurn(BattlePresentationState state)
    {
        if (state.Phase != BattlePresentationPhase.Battle
            || currentTurnIndex < 0
            || currentTurnIndex >= turnOrder.Count)
        {
            return;
        }

        BattleCardState active = turnOrder[currentTurnIndex];
        if (active == null)
            return;

        state.ActivePlayer = active.BelongsToPlayer ? 0 : 1;
        state.ActiveSlot = active.BelongsToPlayer ? playerCards.IndexOf(active) : cpuCards.IndexOf(active);
    }

    private void CopyCampaignBoard(IReadOnlyList<BattleCardState> source, List<BattlePresentationCard> target)
    {
        for (int slot = 0; slot < source.Count; slot++)
        {
            BattleCardState card = source[slot];
            if (card == null)
                continue;

            int lives = card.Eliminated ? 0 : 2;
            target.Add(new BattlePresentationCard
            {
                Slot = slot,
                CardId = card.Card.Id,
                CardName = card.Card.Name,
                HeroClass = card.Card.HeroClass,
                Strength = card.Card.Strength,
                Lives = lives,
                MaximumLives = 2,
                Initiative = card.Initiative,
                Eliminated = card.Eliminated,
                IsSpirit = card.IsSpirit,
                Inhibited = card.InhibitedTurns > 0,
                Marked = HunterMarkBonusForTarget(card) > 0,
                Protecting = card.AbilityArmed && card.Card.HeroClass == HeroClass.Paladin,
                PermanentBonus = card.PermanentCombatBonus,
                PendingBonus = card.PendingAttackBonus,
                DiePenaltySteps = card.PendingVigorStepPenalty
            });
        }
    }

    private void CopyCampaignDeploymentOrder(List<BattlePresentationDeploymentToken> target)
    {
        for (int index = 0; index < deploymentOrder.Count; index++)
        {
            DeploymentToken token = deploymentOrder[index];
            if (token == null)
                continue;

            target.Add(new BattlePresentationDeploymentToken
            {
                Order = index,
                Player = token.BelongsToPlayer ? 0 : 1,
                Initiative = token.Initiative
            });
        }
    }

    private void CopyCampaignHand(List<BattlePresentationHandCard> target)
    {
        for (int index = 0; index < draftCandidates.Count; index++)
        {
            if (selectedDraftCards.Contains(index))
                continue;

            target.Add(new BattlePresentationHandCard
            {
                LoadoutIndex = index,
                DefinitionId = draftCandidates[index].Id
            });
        }
    }

    private void CopyRecentCampaignLog(List<string> target)
    {
        foreach (string entry in gameLogEntries.TakeLast(20))
            target.Add(entry);

        if (target.Count == 0 && messageText != null && !string.IsNullOrWhiteSpace(messageText.text))
            target.Add(messageText.text);
    }

    private int DeploymentOwner()
    {
        if (deploymentOrder.Count == 0)
            return -1;

        int deployedCount = playerCards.Count + cpuCards.Count;
        if (deployedCount < 0 || deployedCount >= deploymentOrder.Count)
            return -1;

        return deploymentOrder[deployedCount].BelongsToPlayer ? 0 : 1;
    }

    private int CampaignWinner()
    {
        bool playerAlive = playerCards.Any(card => card != null && !card.Eliminated);
        bool cpuAlive = cpuCards.Any(card => card != null && !card.Eliminated);
        if (playerAlive == cpuAlive)
            return -1;
        return playerAlive ? 0 : 1;
    }

    private static string AuraLabel(BattleAuraType aura) =>
        aura == BattleAuraType.None ? string.Empty : AuraDisplayName(aura);
}
}
