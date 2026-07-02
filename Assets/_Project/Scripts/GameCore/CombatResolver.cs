using System;

namespace AccardND.GameCore
{
    public sealed class CombatResolver
    {
        private readonly IRandomSource random;

        public CombatResolver(IRandomSource random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public CombatResult ResolveAttack(CombatCard attacker, CombatCard defender, int vigorDieSides)
        {
            return ResolveAttack(attacker, defender, vigorDieSides, CombatModifiers.None);
        }

        public CombatResult ResolveAttack(
            CombatCard attacker,
            CombatCard defender,
            int vigorDieSides,
            CombatModifiers modifiers)
        {
            return ResolveAttack(attacker, defender, vigorDieSides, vigorDieSides, modifiers);
        }

        public CombatResult ResolveAttack(
            CombatCard attacker,
            CombatCard defender,
            int attackerVigorDieSides,
            int defenderVigorDieSides,
            CombatModifiers modifiers)
        {
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (defender == null)
                throw new ArgumentNullException(nameof(defender));
            if (attackerVigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(attackerVigorDieSides));
            if (defenderVigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(defenderVigorDieSides));

            MatchupResult attackerMatchup = modifiers.ForceAttackerAdvantage
                ? MatchupResult.Advantage
                : modifiers.NeutralizeAttackerMatchup
                    ? MatchupResult.Neutral
                    : ClassMatchup.Compare(attacker.HeroClass, defender.HeroClass);

            bool attackerRerollsOnes = modifiers.RerollAttackerOnes || attacker.HeroClass == HeroClass.Rogue;
            VigorRollResult attackerRoll = modifiers.SumAttackerVigor
                ? RollTwoAndSum(attackerVigorDieSides, attackerRerollsOnes, modifiers.RerollAttackerTwos)
                : RollVigor(attackerVigorDieSides, attackerMatchup, attackerRerollsOnes, modifiers.RerollAttackerTwos);
            VigorRollResult defenderRoll = modifiers.DefenderAdvantage
                ? RollTwoAndTakeHighest(defenderVigorDieSides)
                : RollVigor(defenderVigorDieSides, MatchupResult.Neutral, false, false);

            return new CombatResult(
                attackerRoll,
                defenderRoll,
                attacker.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus,
                defender.Strength + defenderRoll.SelectedRoll + modifiers.DefenderFlatBonus);
        }

        private VigorRollResult RollVigor(int dieSides, MatchupResult matchup, bool rerollOnes, bool rerollTwos)
        {
            int firstRoll = RollSingle(dieSides, rerollOnes, rerollTwos);
            if (matchup == MatchupResult.Neutral)
                return new VigorRollResult(
                    dieSides,
                    firstRoll,
                    0,
                    false,
                    firstRoll,
                    matchup,
                    VigorSelectionMode.Single);

            int secondRoll = RollSingle(dieSides, rerollOnes, rerollTwos);
            int selectedRoll = matchup == MatchupResult.Advantage
                ? Math.Max(firstRoll, secondRoll)
                : Math.Min(firstRoll, secondRoll);
            return new VigorRollResult(
                dieSides,
                firstRoll,
                secondRoll,
                true,
                selectedRoll,
                matchup,
                matchup == MatchupResult.Advantage
                    ? VigorSelectionMode.Highest
                    : VigorSelectionMode.Lowest);
        }

        private VigorRollResult RollTwoAndSum(int dieSides, bool rerollOnes, bool rerollTwos)
        {
            int first = RollSingle(dieSides, rerollOnes, rerollTwos);
            int second = RollSingle(dieSides, rerollOnes, rerollTwos);
            return new VigorRollResult(
                dieSides,
                first,
                second,
                true,
                first + second,
                MatchupResult.Neutral,
                VigorSelectionMode.Sum);
        }

        private int RollSingle(int dieSides, bool rerollOnes, bool rerollTwos)
        {
            int result = random.NextInclusive(1, dieSides);
            return (rerollOnes && result == 1) || (rerollTwos && result == 2)
                ? random.NextInclusive(1, dieSides)
                : result;
        }

        private VigorRollResult RollTwoAndTakeHighest(int dieSides)
        {
            int first = random.NextInclusive(1, dieSides);
            int second = random.NextInclusive(1, dieSides);
            return new VigorRollResult(
                dieSides,
                first,
                second,
                true,
                Math.Max(first, second),
                MatchupResult.Advantage,
                VigorSelectionMode.Highest);
        }
    }
}
