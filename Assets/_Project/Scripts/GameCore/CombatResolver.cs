using System;
using AccardND.GameCore.Pvp;

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

            var attackerReroll = new RerollRule(modifiers.RerollAttackerOnes, modifiers.RerollAttackerTwos);
            var defenderReroll = new RerollRule(modifiers.RerollDefenderOnes, modifiers.RerollDefenderTwos);
            VigorRollResult attackerRoll = modifiers.SumAttackerVigor
                ? RollTwoAndSum(attackerVigorDieSides, attackerReroll)
                : RollVigor(attackerVigorDieSides, attackerMatchup, attackerReroll);
            VigorRollResult defenderRoll = modifiers.DefenderAdvantage
                ? RollTwoAndTakeHighest(defenderVigorDieSides, defenderReroll)
                : RollVigor(defenderVigorDieSides, MatchupResult.Neutral, defenderReroll);

            int defenderTotal = defender.Strength + defenderRoll.SelectedRoll + modifiers.DefenderFlatBonus;
            int initialAttackerTotal = attacker.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus;
            if (modifiers.AttackerConditionalRerollMax > 0 && initialAttackerTotal <= defenderTotal)
                attackerRoll = RerollEligibleAttackerDice(attackerRoll, modifiers.AttackerConditionalRerollMax);

            int attackerTotal = attacker.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus;
            if (modifiers.DefenderConditionalRerollMax > 0 && attackerTotal > defenderTotal)
            {
                defenderRoll = RerollEligibleDice(defenderRoll, modifiers.DefenderConditionalRerollMax);
                defenderTotal = defender.Strength + defenderRoll.SelectedRoll + modifiers.DefenderFlatBonus;
            }

            return new CombatResult(
                attackerRoll,
                defenderRoll,
                attackerTotal,
                defenderTotal);
        }

        private VigorRollResult RerollEligibleAttackerDice(VigorRollResult roll, int maximumResult)
        {
            return RerollEligibleDice(roll, maximumResult);
        }

        private VigorRollResult RerollEligibleDice(VigorRollResult roll, int maximumResult)
        {
            int first = roll.FirstRoll;
            int second = roll.SecondRoll;
            int firstBeforeReroll = roll.FirstRollBeforeReroll;
            int secondBeforeReroll = roll.SecondRollBeforeReroll;

            if (first <= maximumResult)
            {
                firstBeforeReroll = first;
                first = random.NextInclusive(1, roll.DieSides);
            }

            if (roll.HasSecondRoll && second <= maximumResult)
            {
                secondBeforeReroll = second;
                int secondDieSides = roll.SelectionMode == VigorSelectionMode.Sum
                    ? PvpVigorScale.Lower(roll.DieSides)
                    : roll.DieSides;
                second = random.NextInclusive(1, secondDieSides);
            }

            int selected = roll.SelectionMode switch
            {
                VigorSelectionMode.Highest => Math.Max(first, second),
                VigorSelectionMode.Lowest => Math.Min(first, second),
                VigorSelectionMode.Sum => first + second,
                _ => first
            };

            return new VigorRollResult(
                roll.DieSides,
                first,
                second,
                roll.HasSecondRoll,
                selected,
                roll.Matchup,
                roll.SelectionMode,
                firstBeforeReroll,
                secondBeforeReroll);
        }

        private VigorRollResult RollVigor(int dieSides, MatchupResult matchup, RerollRule reroll)
        {
            RollOutcome first = RollSingle(dieSides, reroll);
            if (matchup == MatchupResult.Neutral)
                return new VigorRollResult(
                    dieSides,
                    first.Result,
                    0,
                    false,
                    first.Result,
                    matchup,
                    VigorSelectionMode.Single,
                    first.BeforeReroll);

            RollOutcome second = RollSingle(dieSides, reroll);
            int selectedRoll = matchup == MatchupResult.Advantage
                ? Math.Max(first.Result, second.Result)
                : Math.Min(first.Result, second.Result);
            return new VigorRollResult(
                dieSides,
                first.Result,
                second.Result,
                true,
                selectedRoll,
                matchup,
                matchup == MatchupResult.Advantage
                    ? VigorSelectionMode.Highest
                    : VigorSelectionMode.Lowest,
                first.BeforeReroll,
                second.BeforeReroll);
        }

        private VigorRollResult RollTwoAndSum(int dieSides, RerollRule reroll)
        {
            RollOutcome first = RollSingle(dieSides, reroll);
            RollOutcome second = RollSingle(PvpVigorScale.Lower(dieSides), reroll);
            return new VigorRollResult(
                dieSides,
                first.Result,
                second.Result,
                true,
                first.Result + second.Result,
                MatchupResult.Neutral,
                VigorSelectionMode.Sum,
                first.BeforeReroll,
                second.BeforeReroll);
        }

        private RollOutcome RollSingle(int dieSides, RerollRule reroll)
        {
            int result = random.NextInclusive(1, dieSides);
            return reroll.ShouldReroll(result)
                ? new RollOutcome(random.NextInclusive(1, dieSides), result)
                : new RollOutcome(result, 0);
        }

        private VigorRollResult RollTwoAndTakeHighest(int dieSides, RerollRule reroll)
        {
            RollOutcome first = RollSingle(dieSides, reroll);
            RollOutcome second = RollSingle(dieSides, reroll);
            return new VigorRollResult(
                dieSides,
                first.Result,
                second.Result,
                true,
                Math.Max(first.Result, second.Result),
                MatchupResult.Advantage,
                VigorSelectionMode.Highest,
                first.BeforeReroll,
                second.BeforeReroll);
        }

        private readonly struct RollOutcome
        {
            public RollOutcome(int result, int beforeReroll)
            {
                Result = result;
                BeforeReroll = beforeReroll;
            }

            public int Result { get; }
            public int BeforeReroll { get; }
        }

        private readonly struct RerollRule
        {
            private readonly bool ones;
            private readonly bool twos;

            public RerollRule(bool ones, bool twos)
            {
                this.ones = ones;
                this.twos = twos;
            }

            public bool ShouldReroll(int roll) =>
                (ones && roll == 1) || (twos && roll == 2);
        }
    }
}
