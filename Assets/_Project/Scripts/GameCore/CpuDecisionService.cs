using System;
using System.Collections.Generic;
using AccardND.GameCore.Pvp;

namespace AccardND.GameCore
{
    /// <summary>
    /// Il ragionamento della CPU sul bersaglio da attaccare.
    /// La probabilita' di uccisione e' esatta e modella le stesse regole di dado del
    /// <see cref="CombatResolver"/>: vantaggio/svantaggio di classe, somma del Guerriero
    /// (dado Vigore piu' un dado di uno step inferiore), reroll incondizionati e reroll
    /// condizionale del Ladro. Si lavora su distribuzioni di probabilita', non su liste
    /// di esiti: il costo resta lineare nella faccia del dado anche con i reroll attivi.
    /// </summary>
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
                _ => vigorDieSides,
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
            return ChooseTarget(
                attacker,
                targets,
                unavailableTargets,
                attackerVigorDieSides,
                _ => defenderVigorDieSides,
                difficulty,
                weights,
                modifiersForTarget);
        }

        /// <summary>
        /// Ogni bersaglio porta con se' il proprio dado di difesa: il malus del Mago e le
        /// aure che alzano o abbassano il dado cambiano da pedina a pedina, e una faccia
        /// unica per tutti renderebbe cieca la CPU proprio verso i debuff che ha appena
        /// applicato.
        /// </summary>
        public CpuTargetDecision ChooseTarget(
            CombatCard attacker,
            IReadOnlyList<CombatCard> targets,
            IReadOnlyList<bool> unavailableTargets,
            int attackerVigorDieSides,
            Func<int, int> defenderVigorDieSidesForTarget,
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
            if (defenderVigorDieSidesForTarget == null)
                throw new ArgumentNullException(nameof(defenderVigorDieSidesForTarget));
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
                    defenderVigorDieSidesForTarget(randomIndex),
                    weights,
                    modifiersForTarget(randomIndex),
                    variation: 0);
            }

            // Fuori da Diabolica la CPU non e' "meno brava a contare": ragiona con gli stessi
            // pesi e sbaglia per rumore. Cosi' la difficolta' ha un asse solo e alzare
            // RandomTieBreaker la rende davvero piu' distratta invece di spostarne i gusti.
            int noiseCeiling = difficulty == CpuDifficulty.Hard
                ? 0
                : Math.Max(0, weights.RandomTieBreaker) * Math.Max(0, weights.KillProbabilityWeight) / 100;

            var candidates = new List<CpuTargetDecision>(availableIndices.Count);
            bool hasPossibleKill = false;
            foreach (int index in availableIndices)
            {
                CpuTargetDecision candidate = Evaluate(
                    attacker,
                    targets[index],
                    index,
                    attackerVigorDieSides,
                    defenderVigorDieSidesForTarget(index),
                    weights,
                    modifiersForTarget(index),
                    noiseCeiling > 0 ? random.NextInclusive(0, noiseCeiling) : 0);
                candidates.Add(candidate);
                if (candidate.DefeatProbability > 0d)
                    hasPossibleKill = true;
            }

            // A parita' di punteggio si sorteggia: prendere sempre il primo indice rendeva la
            // CPU prevedibile (colpiva sistematicamente la pedina piu' a sinistra).
            var best = new List<CpuTargetDecision>();
            foreach (CpuTargetDecision candidate in candidates)
            {
                if (hasPossibleKill && candidate.DefeatProbability <= 0d)
                    continue;

                if (best.Count == 0 || candidate.Score > best[0].Score)
                {
                    best.Clear();
                    best.Add(candidate);
                }
                else if (candidate.Score == best[0].Score)
                {
                    best.Add(candidate);
                }
            }

            return best.Count == 1 ? best[0] : best[random.NextInclusive(0, best.Count - 1)];
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

            MatchupResult attackerMatchup = AttackerMatchup(attacker, defender, modifiers);
            bool needsStates = modifiers.AttackerConditionalRerollMax > 0
                || modifiers.DefenderConditionalRerollMax > 0;

            RollModel attackerRoll = BuildRollModel(
                attackerVigorDieSides,
                attackerMatchup,
                modifiers.SumAttackerVigor,
                modifiers.RerollAttackerOnes,
                modifiers.RerollAttackerTwos,
                modifiers.AttackerConditionalRerollMax,
                needsStates);
            RollModel defenderRoll = BuildRollModel(
                defenderVigorDieSides,
                modifiers.DefenderAdvantage ? MatchupResult.Advantage : MatchupResult.Neutral,
                sumMode: false,
                modifiers.RerollDefenderOnes,
                modifiers.RerollDefenderTwos,
                modifiers.DefenderConditionalRerollMax,
                needsStates);

            int attackerBase = attacker.Strength + modifiers.AttackerFlatBonus;
            int defenderBase = defender.Strength + modifiers.DefenderFlatBonus;

            if (!needsStates)
                return WinProbability(attackerRoll.Selected, attackerBase, defenderRoll.SelectedCumulative, defenderBase);

            // Il reroll condizionale scatta solo guardando il totale avversario, quindi le due
            // distribuzioni non sono piu' indipendenti e vanno incrociate stato per stato.
            double win = 0d;
            foreach (RollState attackerState in attackerRoll.States)
            {
                int attackerTotal = attackerBase + attackerState.Selected;
                foreach (RollState defenderState in defenderRoll.States)
                {
                    double pairProbability = attackerState.Probability * defenderState.Probability;
                    if (pairProbability <= 0d)
                        continue;

                    if (attackerState.Reroll != null && attackerTotal <= defenderBase + defenderState.Selected)
                    {
                        double[] afterReroll = attackerState.Reroll;
                        for (int value = 1; value < afterReroll.Length; value++)
                        {
                            if (afterReroll[value] > 0d)
                            {
                                win += pairProbability
                                    * afterReroll[value]
                                    * DefeatChance(attackerBase + value, defenderState, defenderBase);
                            }
                        }
                    }
                    else
                    {
                        win += pairProbability * DefeatChance(attackerTotal, defenderState, defenderBase);
                    }
                }
            }

            return win;
        }

        private static MatchupResult AttackerMatchup(CombatCard attacker, CombatCard defender, CombatModifiers modifiers)
        {
            if (modifiers.ForceAttackerAdvantage)
                return MatchupResult.Advantage;
            if (modifiers.NeutralizeAttackerMatchup)
                return MatchupResult.Neutral;
            return ClassMatchup.Compare(attacker.HeroClass, defender.HeroClass);
        }

        private CpuTargetDecision Evaluate(
            CombatCard attacker,
            CombatCard target,
            int targetIndex,
            int attackerVigorDieSides,
            int defenderVigorDieSides,
            CpuDecisionWeights weights,
            CombatModifiers modifiers,
            int variation)
        {
            double probability = EstimateDefeatProbability(
                attacker,
                target,
                attackerVigorDieSides,
                defenderVigorDieSides,
                modifiers);
            MatchupResult matchup = AttackerMatchup(attacker, target, modifiers);
            // La minaccia e' la Potenza che la pedina mostra sul campo, equipaggiamenti e
            // benedizioni comprese: sono gia' dentro DefenderFlatBonus.
            int effectiveTargetStrength = target.Strength + modifiers.DefenderFlatBonus;
            int score = (int)Math.Round(probability * weights.KillProbabilityWeight)
                + (int)matchup * weights.ClassAdvantageWeight
                + effectiveTargetStrength * weights.ThreatWeight
                + variation;

            return new CpuTargetDecision(targetIndex, score, probability, matchup);
        }

        private static double DefeatChance(int attackerTotal, RollState defenderState, int defenderBase)
        {
            if (attackerTotal <= defenderBase + defenderState.Selected)
                return 0d;
            if (defenderState.RerollCumulative == null)
                return 1d;
            return ProbabilityUpTo(defenderState.RerollCumulative, attackerTotal - defenderBase - 1);
        }

        private static double WinProbability(
            double[] attackerDistribution,
            int attackerBase,
            double[] defenderCumulative,
            int defenderBase)
        {
            double win = 0d;
            for (int value = 1; value < attackerDistribution.Length; value++)
            {
                double probability = attackerDistribution[value];
                if (probability > 0d)
                    win += probability * ProbabilityUpTo(defenderCumulative, attackerBase + value - defenderBase - 1);
            }
            return win;
        }

        private static double ProbabilityUpTo(double[] cumulative, int value)
        {
            if (value < 0)
                return 0d;
            return value >= cumulative.Length ? cumulative[cumulative.Length - 1] : cumulative[value];
        }

        private static RollModel BuildRollModel(
            int dieSides,
            MatchupResult matchup,
            bool sumMode,
            bool rerollOnes,
            bool rerollTwos,
            int conditionalRerollMax,
            bool buildStates)
        {
            // Il Guerriero somma il dado Vigore e un dado di uno step inferiore: la stessa
            // regola di CombatResolver.RollTwoAndSum, non due dadi uguali.
            int secondSides = sumMode ? PvpVigorScale.Lower(dieSides) : dieSides;
            bool twoDice = sumMode || matchup != MatchupResult.Neutral;
            int maxSelected = sumMode ? dieSides + secondSides : dieSides;

            double[] firstWeights = SingleDieWeights(dieSides, rerollOnes, rerollTwos);
            double[] secondWeights = twoDice ? SingleDieWeights(secondSides, rerollOnes, rerollTwos) : null;

            var selected = new double[maxSelected + 1];
            List<RollState> states = buildStates ? new List<RollState>() : null;
            Dictionary<long, int> stateByKey = buildStates ? new Dictionary<long, int>() : null;

            int secondUpperBound = twoDice ? secondSides : 1;
            for (int first = 1; first <= dieSides; first++)
            {
                for (int second = 1; second <= secondUpperBound; second++)
                {
                    double probability = firstWeights[first] * (twoDice ? secondWeights[second] : 1d);
                    if (probability <= 0d)
                        continue;

                    int value = SelectVigor(first, second, twoDice, sumMode, matchup);
                    selected[value] += probability;
                    if (!buildStates)
                        continue;

                    bool firstEligible = conditionalRerollMax > 0 && first <= conditionalRerollMax;
                    bool secondEligible = twoDice && conditionalRerollMax > 0 && second <= conditionalRerollMax;
                    long key = StateKey(value, first, second, firstEligible, secondEligible);
                    if (stateByKey.TryGetValue(key, out int existing))
                    {
                        RollState merged = states[existing];
                        merged.Probability += probability;
                        states[existing] = merged;
                        continue;
                    }

                    double[] reroll = firstEligible || secondEligible
                        ? BuildRerollDistribution(
                            first, second, firstEligible, secondEligible,
                            dieSides, secondSides, twoDice, sumMode, matchup, maxSelected)
                        : null;
                    stateByKey.Add(key, states.Count);
                    states.Add(new RollState
                    {
                        Probability = probability,
                        Selected = value,
                        Reroll = reroll,
                        RerollCumulative = reroll == null ? null : BuildCumulative(reroll)
                    });
                }
            }

            return new RollModel
            {
                Selected = selected,
                SelectedCumulative = BuildCumulative(selected),
                States = states
            };
        }

        /// <summary>
        /// Due combinazioni si fondono solo se producono lo stesso valore scelto e lo stesso
        /// esito di reroll, cioe' se hanno la stessa maschera di dadi rilanciabili e lo stesso
        /// valore sul dado che sopravvive.
        /// </summary>
        private static long StateKey(int selected, int first, int second, bool firstEligible, bool secondEligible)
        {
            if (!firstEligible && !secondEligible)
                return selected;

            int mask = (firstEligible ? 2 : 0) + (secondEligible ? 1 : 0);
            int survivor = firstEligible ? (secondEligible ? 0 : second) : first;
            return 1024L + (((long)selected * 4 + mask) * 64) + survivor;
        }

        private static double[] BuildRerollDistribution(
            int first,
            int second,
            bool firstEligible,
            bool secondEligible,
            int firstSides,
            int secondSides,
            bool twoDice,
            bool sumMode,
            MatchupResult matchup,
            int maxSelected)
        {
            // Il rilancio pesca uniformemente sulla faccia piena del dado: la regola di
            // reroll incondizionato non viene riapplicata (vedi CombatResolver.RerollEligibleDice).
            int firstLower = firstEligible ? 1 : first;
            int firstUpper = firstEligible ? firstSides : first;
            int secondLower = secondEligible ? 1 : second;
            int secondUpper = secondEligible ? secondSides : second;

            int combinations = (firstUpper - firstLower + 1) * (twoDice ? secondUpper - secondLower + 1 : 1);
            double weight = 1d / combinations;

            var distribution = new double[maxSelected + 1];
            for (int rolledFirst = firstLower; rolledFirst <= firstUpper; rolledFirst++)
            {
                if (!twoDice)
                {
                    distribution[rolledFirst] += weight;
                    continue;
                }
                for (int rolledSecond = secondLower; rolledSecond <= secondUpper; rolledSecond++)
                    distribution[SelectVigor(rolledFirst, rolledSecond, true, sumMode, matchup)] += weight;
            }
            return distribution;
        }

        private static int SelectVigor(int first, int second, bool twoDice, bool sumMode, MatchupResult matchup)
        {
            if (!twoDice)
                return first;
            if (sumMode)
                return first + second;
            return matchup == MatchupResult.Advantage
                ? Math.Max(first, second)
                : Math.Min(first, second);
        }

        private static double[] SingleDieWeights(int sides, bool rerollOnes, bool rerollTwos)
        {
            var weights = new double[sides + 1];
            double uniform = 1d / sides;
            for (int value = 1; value <= sides; value++)
            {
                if ((rerollOnes && value == 1) || (rerollTwos && value == 2))
                {
                    for (int rerolled = 1; rerolled <= sides; rerolled++)
                        weights[rerolled] += uniform * uniform;
                    continue;
                }
                weights[value] += uniform;
            }
            return weights;
        }

        private static double[] BuildCumulative(double[] distribution)
        {
            var cumulative = new double[distribution.Length];
            double running = 0d;
            for (int value = 0; value < distribution.Length; value++)
            {
                running += distribution[value];
                cumulative[value] = running;
            }
            return cumulative;
        }

        private sealed class RollModel
        {
            public double[] Selected;
            public double[] SelectedCumulative;
            public List<RollState> States;
        }

        private struct RollState
        {
            public double Probability;
            public int Selected;
            public double[] Reroll;
            public double[] RerollCumulative;
        }
    }
}
