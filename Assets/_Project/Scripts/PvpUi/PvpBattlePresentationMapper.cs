using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.NetProtocol;

namespace AccardND.PvpUi
{
    internal static class PvpBattlePresentationMapper
    {
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
                // Assassin e' il valore zero di HeroClass: il controllo numerico
                // precedente scartava proprio la sua AbilityUsed, impedendo alla
                // regia di riprodurre callout, SFX e fumo.
                HasAbilityClass = IsAbilityPresentationEvent(source.type)
                    && System.Enum.IsDefined(typeof(HeroClass), source.ability),
                AbilityClass = (HeroClass)source.ability,
                AbilityMagnitude = source.magnitude,
				Amount = source.amount,
				ManaCurrent = source.mana,
				ManaDelta = source.manaDelta,
				ManaReason = source.reason,
                Certainty = ParseCombatCertainty(source.certainty),
                AttackerDieSides = source.attackerDieSides,
                DefenderDieSides = source.defenderDieSides,
                AttackerRollFirst = source.attackerRollFirst,
                AttackerRollSecond = source.attackerRollSecond,
                AttackerRollHasSecond = source.attackerRollHasSecond,
                AttackerRollSelected = source.attackerRollSelected,
                AttackerRollSelectionMode = (VigorSelectionMode)source.attackerRollSelectionMode,
                AttackerRollFirstBeforeReroll = source.attackerRollFirstBeforeReroll,
                AttackerRollSecondBeforeReroll = source.attackerRollSecondBeforeReroll,
                AttackerTotal = source.attackerTotal,
                DefenderRollFirst = source.defenderRollFirst,
                DefenderRollSecond = source.defenderRollSecond,
                DefenderRollHasSecond = source.defenderRollHasSecond,
                DefenderRollSelected = source.defenderRollSelected,
                DefenderRollSelectionMode = (VigorSelectionMode)source.defenderRollSelectionMode,
                DefenderRollFirstBeforeReroll = source.defenderRollFirstBeforeReroll,
                DefenderRollSecondBeforeReroll = source.defenderRollSecondBeforeReroll,
                DefenderTotal = source.defenderTotal,
                DefenderLostLife = source.defenderLostLife,
                DefenderRemainingLives = source.defenderRemainingLives,
                DefenderEliminated = source.defenderEliminated,
                BecameSpirit = source.becameSpirit,
                Overkill = source.overkill,
                Redirected = source.redirected,
                IsCounter = source.isCounter,
                InterceptedByNecromancerMinion = source.interceptedByNecromancerMinion,
                Winner = source.winner,
                WinsPlayer0 = source.winsPlayer0,
                WinsPlayer1 = source.winsPlayer1
            };

            if (string.Equals(source.type, "AttackResolved", System.StringComparison.Ordinal)
                && !target.HasHeroClass
                && TryFindHeroClass(state, source.player, source.slot, out HeroClass attackerClass))
            {
                target.HasHeroClass = true;
                target.HeroClass = attackerClass;
            }

            return target;
        }

        private static bool IsAbilityPresentationEvent(string eventType) =>
            string.Equals(eventType, "AbilityUsed", System.StringComparison.Ordinal)
            || string.Equals(eventType, "SupremeUsed", System.StringComparison.Ordinal);

        private static CombatCertainty ParseCombatCertainty(string value)
        {
            return System.Enum.TryParse(value, out CombatCertainty certainty)
                ? certainty
                : CombatCertainty.RollRequired;
        }

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
