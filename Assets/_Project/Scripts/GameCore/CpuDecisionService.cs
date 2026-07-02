using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public sealed class CpuDecisionService
    {
        private readonly IRandomSource random;

        public CpuDecisionService(IRandomSource random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public CpuTargetDecision ChooseTarget(
            CombatCard attacker,
            IReadOnlyList<CombatCard> targets,
            IReadOnlyList<bool> unavailableTargets,
            int vigorDieSides,
            CpuDifficulty difficulty,
            CpuDecisionWeights weights)
        {
            return ChooseTarget(
                attacker,
                targets,
                unavailableTargets,
                vigorDieSides,
                vigorDieSides,
                difficulty,
                weights,
                _ => CombatModifiers.None);
        }

        public CpuTargetDecision ChooseTarget(
            CombatCard attacker,
            IReadOnlyList<CombatCard> targets,
            IReadOnlyList<bool> unavailableTargets,
            int attackerVigorDieSides,
            int defenderVigorDieSides,
            CpuDifficulty difficulty,
            CpuDecisionWeights weights,
            Func<int, CombatModifiers> modifiersForTarget)
        {
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            if (unavailableTargets == null || unavailableTargets.Count != targets.Count)
                throw new ArgumentException("Availability must match the target list.", nameof(unavailableTargets));
            if (modifiersForTarget == null)
                throw new ArgumentNullException(nameof(modifiersForTarget));

            var availableIndices = new List<int>();
            for (int index = 0; index < targets.Count; index++)
            {
                if (!unavailableTargets[index])
                    availableIndices.Add(index);
            }

            if (availableIndices.Count == 0)
                throw new InvalidOperationException("The CPU has no available target.");

            if (difficulty == CpuDifficulty.Easy)
            {
                int randomIndex = availableIndices[random.NextInclusive(0, availableIndices.Count - 1)];
                return Evaluate(
                    attacker,
                    targets[randomIndex],
                    randomIndex,
                    attackerVigorDieSides,
                    defenderVigorDieSides,
                    difficulty,
                    weights,
                    modifiersForTarget(randomIndex));
            }

            var candidates = new List<CpuTargetDecision>();
            foreach (int index in availableIndices)
            {
                candidates.Add(Evaluate(
                    attacker,
                    targets[index],
                    index,
                    attackerVigorDieSides,
                    defenderVigorDieSides,
                    difficulty,
                    weights,
                    modifiersForTarget(index)));
            }

            bool hasPossibleKill = false;
            foreach (CpuTargetDecision candidate in candidates)
            {
                if (candidate.DefeatProbability > 0d)
                    hasPossibleKill = true;
            }

            CpuTargetDecision best = default;
            bool hasBest = false;
            foreach (CpuTargetDecision candidate in candidates)
            {
                if (hasPossibleKill && candidate.DefeatProbability <= 0d)
                    continue;

                if (!hasBest || candidate.Score > best.Score)
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            return best;
        }

        public double EstimateDefeatProbability(CombatCard attacker, CombatCard defender, int vigorDieSides)
        {
            return EstimateDefeatProbability(
                attacker,
                defender,
                vigorDieSides,
                vigorDieSides,
                CombatModifiers.None);
        }

        public double EstimateDefeatProbability(
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
            List<int> attackerOutcomes = modifiers.SumAttackerVigor
                ? EnumerateSumOutcomes(attackerVigorDieSides, attackerRerollsOnes, modifiers.RerollAttackerTwos)
                : EnumerateVigorOutcomes(attackerVigorDieSides, attackerMatchup, attackerRerollsOnes, modifiers.RerollAttackerTwos);
            List<int> defenderOutcomes = EnumerateVigorOutcomes(
                defenderVigorDieSides,
                modifiers.DefenderAdvantage ? MatchupResult.Advantage : MatchupResult.Neutral,
                false);

            long victories = 0;
            long combinations = (long)attackerOutcomes.Count * defenderOutcomes.Count;
            foreach (int attackerVigor in attackerOutcomes)
            {
                foreach (int defenderVigor in defenderOutcomes)
                {
                    if (attacker.Strength + attackerVigor + modifiers.AttackerFlatBonus
                        > defender.Strength + defenderVigor + modifiers.DefenderFlatBonus)
                        victories++;
                }
            }

            return victories / (double)combinations;
        }

        private static List<int> EnumerateSumOutcomes(int dieSides, bool rerollOnes, bool rerollTwos)
        {
            List<int> rolls = EnumerateSingleDieOutcomes(dieSides, rerollOnes, rerollTwos);
            var outcomes = new List<int>(rolls.Count * rolls.Count);
            foreach (int first in rolls)
            {
                foreach (int second in rolls)
                    outcomes.Add(first + second);
            }
            return outcomes;
        }

        private CpuTargetDecision Evaluate(
            CombatCard attacker,
            CombatCard target,
            int targetIndex,
            int attackerVigorDieSides,
            int defenderVigorDieSides,
            CpuDifficulty difficulty,
            CpuDecisionWeights weights,
            CombatModifiers modifiers)
        {
            double probability = EstimateDefeatProbability(
                attacker,
                target,
                attackerVigorDieSides,
                defenderVigorDieSides,
                modifiers);
            MatchupResult matchup = modifiers.ForceAttackerAdvantage
                ? MatchupResult.Advantage
                : modifiers.NeutralizeAttackerMatchup
                    ? MatchupResult.Neutral
                    : ClassMatchup.Compare(attacker.HeroClass, target.HeroClass);
            int probabilityWeight = difficulty == CpuDifficulty.Hard
                ? weights.KillProbabilityWeight
                : weights.KillProbabilityWeight / 2;
            int variation = difficulty == CpuDifficulty.Hard || weights.RandomTieBreaker <= 0
                ? 0
                : random.NextInclusive(0, weights.RandomTieBreaker);
            int score = (int)Math.Round(probability * probabilityWeight)
                + (int)matchup * weights.ClassAdvantageWeight
                + target.Strength * weights.WeakerTargetWeight
                + variation;

            return new CpuTargetDecision(targetIndex, score, probability, matchup);
        }

        private static List<int> EnumerateVigorOutcomes(
            int dieSides,
            MatchupResult matchup,
            bool rerollOnes,
            bool rerollTwos = false)
        {
            List<int> rolls = EnumerateSingleDieOutcomes(dieSides, rerollOnes, rerollTwos);
            var outcomes = new List<int>();
            if (matchup == MatchupResult.Neutral)
            {
                return rolls;
            }

            foreach (int first in rolls)
            {
                foreach (int second in rolls)
                {
                    outcomes.Add(matchup == MatchupResult.Advantage
                        ? Math.Max(first, second)
                        : Math.Min(first, second));
                }
            }

            return outcomes;
        }

        private static List<int> EnumerateSingleDieOutcomes(int dieSides, bool rerollOnes, bool rerollTwos)
        {
            var outcomes = new List<int>();
            if (!rerollOnes && !rerollTwos)
            {
                for (int roll = 1; roll <= dieSides; roll++)
                    outcomes.Add(roll);
                return outcomes;
            }

            for (int first = 1; first <= dieSides; first++)
            {
                bool shouldReroll = (rerollOnes && first == 1) || (rerollTwos && first == 2);
                if (!shouldReroll)
                {
                    for (int weight = 0; weight < dieSides; weight++)
                        outcomes.Add(first);
                    continue;
                }
                for (int reroll = 1; reroll <= dieSides; reroll++)
                    outcomes.Add(reroll);
            }
            return outcomes;
        }
    }
}
