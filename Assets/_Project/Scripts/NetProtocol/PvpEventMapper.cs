using System;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;

namespace AccardND.NetProtocol
{
    public static class PvpEventMapper
    {
        public static MatchEventDto ToDto(PvpEvent gameEvent)
        {
            switch (gameEvent)
            {
                case RoundStartedEvent e:
                    return new MatchEventDto
                    {
                        type = "RoundStarted",
                        matchRound = e.MatchRound,
                        vigorDieSides = e.VigorDieSides
                    };
                case DecisiveSelectionStartedEvent e:
                    return new MatchEventDto { type = "DecisiveSelectionStarted", requiredCount = e.RequiredCount };
                case HandReadyEvent e:
                    return new MatchEventDto { type = "HandReady", player = e.Player };
                case DeploymentStartedEvent e:
                    return new MatchEventDto
                    {
                        type = "DeploymentStarted",
                        firstPlayer = e.FirstPlayer,
                        rollPlayer0 = e.RollPlayer0,
                        rollPlayer1 = e.RollPlayer1
                    };
                case DeployTurnEvent e:
                    return new MatchEventDto { type = "DeployTurn", player = e.Player };
                case CardDeployedEvent e:
                    return new MatchEventDto
                    {
                        type = "CardDeployed",
                        player = e.Player,
                        slot = e.Slot,
                        cardId = e.CardId,
                        cardName = e.CardName,
                        heroClass = (int)e.HeroClass,
                        strength = e.Strength,
                        lives = e.Lives
                    };
                case BattleStartedEvent e:
                    return new MatchEventDto
                    {
                        type = "BattleStarted",
                        auraPlayer0 = (int)e.AuraPlayer0,
                        auraPlayer1 = (int)e.AuraPlayer1
                    };
                case CardInitiativeEvent e:
                    return new MatchEventDto
                    {
                        type = "CardInitiative",
                        player = e.Player,
                        slot = e.Slot,
                        initiative = e.Initiative
                    };
                case TurnStartedEvent e:
                    return new MatchEventDto { type = "TurnStarted", player = e.Player, slot = e.Slot, cycle = e.Cycle };
                case TurnSkippedEvent e:
                    return new MatchEventDto { type = "TurnSkipped", player = e.Player, slot = e.Slot, reason = e.Reason };
                case AbilityUsedEvent e:
                    return new MatchEventDto
                    {
                        type = "AbilityUsed",
                        player = e.Player,
                        slot = e.Slot,
                        ability = (int)e.Ability,
                        targetPlayer = e.TargetPlayer,
                        targetSlot = e.TargetSlot,
                        magnitude = e.Magnitude
                    };
                case CardRevivedEvent e:
                    return new MatchEventDto { type = "CardRevived", player = e.Player, slot = e.Slot, lives = e.Lives };
                case ProtectionTriggeredEvent e:
                    return new MatchEventDto
                    {
                        type = "ProtectionTriggered",
                        player = e.PaladinPlayer,
                        slot = e.PaladinSlot,
                        redirected = e.Redirected
                    };
                case AttackResolvedEvent e:
                    return new MatchEventDto
                    {
                        type = "AttackResolved",
                        player = e.AttackerPlayer,
                        slot = e.AttackerSlot,
                        targetPlayer = e.DefenderPlayer,
                        targetSlot = e.DefenderSlot,
                        certainty = e.Certainty.ToString(),
                        attackerDieSides = e.AttackerDieSides,
                        defenderDieSides = e.DefenderDieSides,
                        attackerRollFirst = e.AttackerRoll.FirstRoll,
                        attackerRollSecond = e.AttackerRoll.SecondRoll,
                        attackerRollHasSecond = e.AttackerRoll.HasSecondRoll,
                        attackerRollSelected = e.AttackerRoll.SelectedRoll,
                        defenderRollFirst = e.DefenderRoll.FirstRoll,
                        defenderRollSecond = e.DefenderRoll.SecondRoll,
                        defenderRollHasSecond = e.DefenderRoll.HasSecondRoll,
                        defenderRollSelected = e.DefenderRoll.SelectedRoll,
                        attackerTotal = e.AttackerTotal,
                        defenderTotal = e.DefenderTotal,
                        defenderLostLife = e.DefenderLostLife,
                        defenderRemainingLives = e.DefenderRemainingLives,
                        defenderEliminated = e.DefenderEliminated,
                        becameSpirit = e.BecameSpirit,
                        isCounter = e.IsCounter
                    };
                case AttachmentAppliedEvent e:
                    return new MatchEventDto
                    {
                        type = "AttachmentApplied",
                        player = e.Player,
                        slot = e.SourceSlot,
                        targetSlot = e.TargetSlot,
                        bonus = e.Bonus
                    };
                case FuryGainedEvent e:
                    return new MatchEventDto { type = "FuryGained", player = e.Player, slot = e.Slot, amount = e.Amount };
                case MightAuraBonusEvent e:
                    return new MatchEventDto { type = "MightAuraBonus", player = e.Player, slot = e.Slot };
                case SpiritExpiredEvent e:
                    return new MatchEventDto { type = "SpiritExpired", player = e.Player, slot = e.Slot };
                case RoundEndedEvent e:
                    return new MatchEventDto
                    {
                        type = "RoundEnded",
                        matchRound = e.MatchRound,
                        winner = e.Winner,
                        winsPlayer0 = e.WinsPlayer0,
                        winsPlayer1 = e.WinsPlayer1
                    };
                case MatchEndedEvent e:
                    return new MatchEventDto
                    {
                        type = "MatchEnded",
                        winner = e.Winner,
                        winsPlayer0 = e.WinsPlayer0,
                        winsPlayer1 = e.WinsPlayer1
                    };
                default:
                    throw new ArgumentException($"Evento non mappato: {gameEvent.GetType().Name}");
            }
        }
    }
}
