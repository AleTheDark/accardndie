using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class CpuDecisionServiceTests
    {
        [Test]
        public void HardCpu_PrefersTargetWithClassAdvantage()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("mage", "Mago", HeroClass.Mage, 5);
            var targets = new List<CombatCard>
            {
                new("tank", "Tank", HeroClass.Paladin, 5),
                new("assassin", "Assassino", HeroClass.Assassin, 5)
            };
            var unavailable = new[] { false, false };
            var weights = new CpuDecisionWeights(1000, 100, 8, 0);

            CpuTargetDecision decision = service.ChooseTarget(
                attacker,
                targets,
                unavailable,
                6,
                CpuDifficulty.Hard,
                weights);

            Assert.That(decision.TargetIndex, Is.EqualTo(0));
            Assert.That(decision.Matchup, Is.EqualTo(MatchupResult.Advantage));
        }

        [Test]
        public void HardCpu_IgnoresImpossibleTargetWhenAnotherTargetCanBeDefeated()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("champion", "Champion", HeroClass.Mage, 10);
            var targets = new List<CombatCard>
            {
                new("giant", "Giant", HeroClass.Paladin, 13),
                new("paladin", "Paladin", HeroClass.Warrior, 8)
            };
            var unavailable = new[] { false, false };
            var weights = new CpuDecisionWeights(1000, 100, 8, 0);

            CpuTargetDecision decision = service.ChooseTarget(
                attacker,
                targets,
                unavailable,
                4,
                4,
                CpuDifficulty.Hard,
                weights,
                _ => CombatModifiers.None);

            Assert.That(decision.TargetIndex, Is.EqualTo(1));
            Assert.That(decision.DefeatProbability, Is.GreaterThan(0d));
        }

        [Test]
        public void Probability_RespectsDefenderWinningTies()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 5);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, 5);

            double probability = service.EstimateDefeatProbability(attacker, defender, 6);

            Assert.That(probability, Is.EqualTo(15d / 36d).Within(0.0001d));
        }

        [Test]
        public void Probability_ReturnsOneWhenKillIsMathematicallyCertain()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 20);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, 1);

            double probability = service.EstimateDefeatProbability(
                attacker, defender, 4, 12, CombatModifiers.None);

            Assert.That(probability, Is.EqualTo(1d));
        }

        [Test]
        public void Probability_ReturnsZeroWhenKillIsMathematicallyImpossible()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 1);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, 20);

            double probability = service.EstimateDefeatProbability(
                attacker, defender, 12, 4, CombatModifiers.None);

            Assert.That(probability, Is.Zero);
        }

        [Test]
        public void Certainty_FiveWithD6AgainstEightWithD6RequiresRoll()
        {
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 5);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, 8);

            CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(
                attacker, defender, 6, 6, CombatModifiers.None);

            Assert.That(certainty, Is.EqualTo(CombatCertainty.RollRequired));
        }

        [Test]
        public void Probability_FiveWithD6AgainstEightWithD6IsNotZero()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 5);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, 8);

            double probability = service.EstimateDefeatProbability(attacker, defender, 6);

            Assert.That(probability, Is.EqualTo(3d / 36d).Within(0.0001d));
        }

        [Test]
        public void Probability_AppliesRogueRerollOnesOnlyWhenAbilityIsActive()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 8);

            double probability = service.EstimateDefeatProbability(
                attacker,
                defender,
                6,
                6,
                new CombatModifiers(false, false, rerollAttackerOnes: true));

            Assert.That(probability, Is.EqualTo(21d / 216d).Within(0.0001d));
        }

        [Test]
        public void Certainty_FiveWithD3AgainstEightWithD3IsImpossible()
        {
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 5);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, 8);

            CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(
                attacker, defender, 3, 3, CombatModifiers.None);

            Assert.That(certainty, Is.EqualTo(CombatCertainty.Impossible));
        }

        /// <summary>
        /// La somma del Guerriero e' dado Vigore + dado di uno step inferiore. Con un D6 il
        /// massimo e' 10, non 12: qui la differenza e' tutta fra "impossibile" e "ci provo".
        /// </summary>
        [Test]
        public void Probability_WarriorSumUsesALowerSecondDie()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("warrior", "Guerriero", HeroClass.Warrior, 1);
            var defender = new CombatCard("wall", "Muro", HeroClass.Warrior, 11);

            double probability = service.EstimateDefeatProbability(
                attacker,
                defender,
                6,
                2,
                new CombatModifiers(sumAttackerVigor: true, defenderAdvantage: false));

            Assert.That(probability, Is.Zero);
        }

        [Test]
        public void Probability_ConditionalRerollHelpsTheAttacker()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 7);

            double plain = service.EstimateDefeatProbability(
                attacker, defender, 6, 6, CombatModifiers.None);
            double withReroll = service.EstimateDefeatProbability(
                attacker, defender, 6, 6, new CombatModifiers(false, false, attackerConditionalRerollMax: 2));

            Assert.That(withReroll, Is.GreaterThan(plain));
        }

        [Test]
        public void Probability_ConditionalRerollHelpsTheDefender()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 7);
            var defender = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);

            double plain = service.EstimateDefeatProbability(
                attacker, defender, 6, 6, CombatModifiers.None);
            double withReroll = service.EstimateDefeatProbability(
                attacker, defender, 6, 6, new CombatModifiers(false, false, defenderConditionalRerollMax: 2));

            Assert.That(withReroll, Is.LessThan(plain));
        }

        /// <summary>
        /// Il modello della CPU e il tavolo devono dire la stessa cosa: qualunque scostamento
        /// qui e' una decisione presa su regole che il gioco non applica.
        /// </summary>
        [TestCase(5, 5, 6, 6, false, 0, 0, 0, 0)]
        [TestCase(5, 8, 6, 6, false, 0, 0, 0, 0)]
        [TestCase(7, 6, 12, 8, false, 2, 1, 0, 0)]
        [TestCase(4, 4, 8, 8, true, 0, 0, 0, 0)]
        [TestCase(6, 6, 10, 10, true, 3, 0, 0, 0)]
        [TestCase(5, 6, 6, 6, false, 0, 0, 2, 0)]
        [TestCase(5, 5, 8, 8, false, 0, 0, 3, 3)]
        [TestCase(9, 7, 20, 12, false, 0, 2, 6, 0)]
        [TestCase(5, 5, 6, 6, true, 0, 0, 2, 2)]
        public void Probability_MatchesTheCombatResolver(
            int attackerStrength,
            int defenderStrength,
            int attackerDieSides,
            int defenderDieSides,
            bool sumAttackerVigor,
            int attackerFlatBonus,
            int defenderFlatBonus,
            int attackerConditionalRerollMax,
            int defenderConditionalRerollMax)
        {
            const int trials = 200_000;
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, attackerStrength);
            var defender = new CombatCard("d", "D", HeroClass.Paladin, defenderStrength);
            var modifiers = new CombatModifiers(
                sumAttackerVigor,
                defenderAdvantage: false,
                rerollAttackerOnes: false,
                rerollAttackerTwos: false,
                attackerFlatBonus: attackerFlatBonus,
                defenderFlatBonus: defenderFlatBonus,
                attackerConditionalRerollMax: attackerConditionalRerollMax,
                defenderConditionalRerollMax: defenderConditionalRerollMax);

            double expected = new CpuDecisionService(new FixedRandomSource())
                .EstimateDefeatProbability(attacker, defender, attackerDieSides, defenderDieSides, modifiers);

            var resolver = new CombatResolver(new SeededRandomSource(20260811));
            int wins = 0;
            for (int trial = 0; trial < trials; trial++)
            {
                CombatResult result = resolver.ResolveAttack(
                    attacker, defender, attackerDieSides, defenderDieSides, modifiers);
                if (result.DefenderIsDefeated)
                    wins++;
            }

            Assert.That(wins / (double)trials, Is.EqualTo(expected).Within(0.01d));
        }

        /// <summary>
        /// Il dado di difesa e' una proprieta' del singolo bersaglio: e' cosi' che la CPU
        /// vede il malus che il suo Mago ha appena messo addosso a qualcuno.
        /// </summary>
        [Test]
        public void ChooseTarget_PrefersTheTargetWithTheWeakerDefenceDie()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 5);
            var targets = new List<CombatCard>
            {
                new("solid", "Solido", HeroClass.Paladin, 5),
                new("weakened", "Indebolito", HeroClass.Paladin, 5)
            };
            var weights = new CpuDecisionWeights(1000, 100, 8, 0);

            CpuTargetDecision decision = service.ChooseTarget(
                attacker,
                targets,
                new[] { false, false },
                6,
                targetIndex => targetIndex == 1 ? 4 : 12,
                CpuDifficulty.Hard,
                weights,
                _ => CombatModifiers.None);

            Assert.That(decision.TargetIndex, Is.EqualTo(1));
        }

        /// <summary>
        /// La minaccia si misura sulla Potenza effettiva: equipaggiamenti e benedizioni
        /// arrivano dentro DefenderFlatBonus, non dalla carta base.
        /// </summary>
        [Test]
        public void ChooseTarget_ThreatWeightCountsTheDefenderFlatBonus()
        {
            var service = new CpuDecisionService(new FixedRandomSource());
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 9);
            var targets = new List<CombatCard>
            {
                new("plain", "Semplice", HeroClass.Paladin, 3),
                new("buffed", "Potenziato", HeroClass.Paladin, 3)
            };
            var weights = new CpuDecisionWeights(0, 0, 10, 0);

            CpuTargetDecision decision = service.ChooseTarget(
                attacker,
                targets,
                new[] { false, false },
                6,
                _ => 6,
                CpuDifficulty.Hard,
                weights,
                targetIndex => targetIndex == 1
                    ? new CombatModifiers(false, false, defenderFlatBonus: 4)
                    : CombatModifiers.None);

            Assert.That(decision.TargetIndex, Is.EqualTo(1));
        }

        /// <summary>
        /// Senza rumore, Normale ragiona esattamente come Diabolica: la difficolta' e' un
        /// asse solo (quanto sbaglia), non pesi diversi che le cambiano i gusti.
        /// </summary>
        [Test]
        public void NormalCpu_WithoutNoiseChoosesLikeHardCpu()
        {
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 6);
            var targets = new List<CombatCard>
            {
                new("small", "Piccolo", HeroClass.Paladin, 4),
                new("big", "Grosso", HeroClass.Paladin, 7)
            };
            var weights = new CpuDecisionWeights(1000, 100, 8, 0);

            CpuTargetDecision hard = new CpuDecisionService(new FixedRandomSource()).ChooseTarget(
                attacker, targets, new[] { false, false }, 8, 8, CpuDifficulty.Hard, weights, _ => CombatModifiers.None);
            CpuTargetDecision normal = new CpuDecisionService(new FixedRandomSource()).ChooseTarget(
                attacker, targets, new[] { false, false }, 8, 8, CpuDifficulty.Normal, weights, _ => CombatModifiers.None);

            Assert.That(normal.TargetIndex, Is.EqualTo(hard.TargetIndex));
            Assert.That(normal.Score, Is.EqualTo(hard.Score));
        }

        [Test]
        public void NormalCpu_NoiseScalesWithTheKillProbabilityWeight()
        {
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 6);
            var targets = new List<CombatCard> { new("only", "Unico", HeroClass.Paladin, 6) };
            // Il tie breaker vale punti percentuali di probabilita': 20 su un peso di 1000
            // sono al massimo 200 punti di scarto.
            var weights = new CpuDecisionWeights(1000, 100, 8, 20);

            CpuTargetDecision quiet = new CpuDecisionService(new FixedRandomSource()).ChooseTarget(
                attacker, targets, new[] { false }, 8, 8, CpuDifficulty.Normal, weights, _ => CombatModifiers.None);
            CpuTargetDecision loud = new CpuDecisionService(new MaximumRandomSource()).ChooseTarget(
                attacker, targets, new[] { false }, 8, 8, CpuDifficulty.Normal, weights, _ => CombatModifiers.None);

            Assert.That(loud.Score - quiet.Score, Is.EqualTo(200));
        }

        [Test]
        public void HardCpu_BreaksScoreTiesWithoutAlwaysPickingTheFirstTarget()
        {
            var attacker = new CombatCard("a", "A", HeroClass.Paladin, 5);
            var targets = new List<CombatCard>
            {
                new("left", "Sinistra", HeroClass.Paladin, 5),
                new("right", "Destra", HeroClass.Paladin, 5)
            };
            var weights = new CpuDecisionWeights(1000, 100, 8, 0);

            CpuTargetDecision first = new CpuDecisionService(new FixedRandomSource()).ChooseTarget(
                attacker, targets, new[] { false, false }, 6, 6, CpuDifficulty.Hard, weights, _ => CombatModifiers.None);
            CpuTargetDecision last = new CpuDecisionService(new MaximumRandomSource()).ChooseTarget(
                attacker, targets, new[] { false, false }, 6, 6, CpuDifficulty.Hard, weights, _ => CombatModifiers.None);

            Assert.That(first.Score, Is.EqualTo(last.Score));
            Assert.That(first.TargetIndex, Is.EqualTo(0));
            Assert.That(last.TargetIndex, Is.EqualTo(1));
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }

        private sealed class MaximumRandomSource : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => maximum;
        }
    }
}
