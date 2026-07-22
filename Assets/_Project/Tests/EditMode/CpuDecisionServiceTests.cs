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

        private sealed class FixedRandomSource : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }
    }
}
