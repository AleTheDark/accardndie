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
                HasAbilityClass = source.ability > 0,
                AbilityClass = (HeroClass)source.ability,
                Certainty = ParseCombatCertainty(source.certainty),
                AttackerDieSides = source.attackerDieSides,
                DefenderDieSides = source.defenderDieSides,
                AttackerRollFirst = source.attackerRollFirst,
                AttackerRollSecond = source.attackerRollSecond,
                AttackerRollHasSecond = source.attackerRollHasSecond,
                AttackerRollSelected = source.attackerRollSelected,
                AttackerRollSelectionMode = (VigorSelectionMode)source.attackerRollSelectionMode,
                AttackerTotal = source.attackerTotal,
                DefenderRollFirst = source.defenderRollFirst,
                DefenderRollSecond = source.defenderRollSecond,
                DefenderRollHasSecond = source.defenderRollHasSecond,
                DefenderRollSelected = source.defenderRollSelected,
                DefenderRollSelectionMode = (VigorSelectionMode)source.defenderRollSelectionMode,
                DefenderTotal = source.defenderTotal,
                DefenderLostLife = source.defenderLostLife,
                DefenderEliminated = source.defenderEliminated,
                BecameSpirit = source.becameSpirit,
                Overkill = source.overkill
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
