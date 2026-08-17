using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class CombatResolverTests
    {
        [Test]
        public void ResolveAttack_UsesBestRollForAdvantageAndWorstForDisadvantage()
        {
            var random = new FixedRandomSource(2, 6, 5, 1);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("warrior-5", "Guerriero", HeroClass.Warrior, 5);
            var defender = new CombatCard("assassin-5", "Assassino", HeroClass.Assassin, 5);

            CombatResult result = resolver.ResolveAttack(attacker, defender, 6);

            Assert.That(result.AttackerVigor, Is.EqualTo(6));
            Assert.That(result.DefenderVigor, Is.EqualTo(5));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(2));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(6));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(5));
            Assert.That(result.DefenderRoll.HasSecondRoll, Is.False);
            Assert.That(result.DefenderIsDefeated, Is.True);
        }

        [Test]
        public void ResolveAttack_DefenderWinsTies()
        {
            var random = new FixedRandomSource(3, 3);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("a", "Attaccante", HeroClass.Paladin, 5);
            var defender = new CombatCard("d", "Difensore", HeroClass.Paladin, 5);

            CombatResult result = resolver.ResolveAttack(attacker, defender, 6);

            Assert.That(result.AttackerTotal, Is.EqualTo(result.DefenderTotal));
            Assert.That(result.DefenderIsDefeated, Is.False);
        }

        [Test]
        public void ResolveAttack_WarriorAbilitySumsVigorDieAndOneStepLowerDie()
        {
            var random = new FixedRandomSource(3, 4, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("warrior", "Guerriero", HeroClass.Warrior, 5);
            var defender = new CombatCard("tank", "Tank", HeroClass.Paladin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(sumAttackerVigor: true, defenderAdvantage: false));

            Assert.That(result.AttackerRoll.SelectionMode, Is.EqualTo(VigorSelectionMode.Sum));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(3));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(4));
            Assert.That(result.AttackerVigor, Is.EqualTo(7));
            Assert.That(result.DefenderRoll.HasSecondRoll, Is.False);
        }

        [TestCase(4, 2)]
        [TestCase(6, 4)]
        [TestCase(8, 6)]
        [TestCase(10, 8)]
        [TestCase(12, 10)]
        [TestCase(20, 12)]
        public void ResolveAttack_WarriorAbilityRollsSecondDieOneStepLower(int vigorDieSides, int expectedSecondDieSides)
        {
            var random = new RecordingRandomSource(1, 1, 1);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("warrior", "Guerriero", HeroClass.Warrior, 5);
            var defender = new CombatCard("tank", "Tank", HeroClass.Paladin, 5);

            resolver.ResolveAttack(
                attacker,
                defender,
                vigorDieSides,
                new CombatModifiers(sumAttackerVigor: true, defenderAdvantage: false));

            Assert.That(random.Maximums[0], Is.EqualTo(vigorDieSides));
            Assert.That(random.Maximums[1], Is.EqualTo(expectedSecondDieSides));
        }

        [Test]
        public void ResolveAttack_TankProtectionRollsTwoDefenseDiceAndKeepsHighest()
        {
            var random = new FixedRandomSource(3, 2, 6);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("attacker", "Attaccante", HeroClass.Assassin, 5);
            var defender = new CombatCard("protected", "Protetto", HeroClass.Assassin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(sumAttackerVigor: false, defenderAdvantage: true));

            Assert.That(result.AttackerRoll.HasSecondRoll, Is.False);
            Assert.That(result.DefenderRoll.SelectionMode, Is.EqualTo(VigorSelectionMode.Highest));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
            Assert.That(result.DefenderRoll.SecondRoll, Is.EqualTo(6));
            Assert.That(result.DefenderVigor, Is.EqualTo(6));
        }

        [Test]
        public void ResolveAttack_PaladinProtectionNeutralizesAttackerMatchupAndGrantsDefenseAdvantage()
        {
            var random = new FixedRandomSource(2, 1, 6);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("mage", "Mago", HeroClass.Mage, 5);
            var defender = new CombatCard("paladin", "Paladino", HeroClass.Paladin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(
                    sumAttackerVigor: false,
                    defenderAdvantage: true,
                    neutralizeAttackerMatchup: true));

            Assert.That(result.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Neutral));
            Assert.That(result.AttackerRoll.HasSecondRoll, Is.False);
            Assert.That(result.AttackerVigor, Is.EqualTo(2));
            Assert.That(result.DefenderRoll.SelectionMode, Is.EqualTo(VigorSelectionMode.Highest));
            Assert.That(result.DefenderVigor, Is.EqualTo(6));
        }

        [Test]
        public void ResolveAttack_RogueRerollsAnAttackerOneOnce()
        {
            var random = new FixedRandomSource(1, 5, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, rerollAttackerOnes: true));

            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(5));
            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
        }

        [Test]
        public void ResolveAttack_RogueDoesNotRerollOneWithoutAbility()
        {
            var random = new FixedRandomSource(1, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 5);

            CombatResult result = resolver.ResolveAttack(attacker, defender, 6);

            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(1));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
        }

        [Test]
        public void ResolveAttack_RogueConditionalRerollTriggersOnlyWhenNeededToWin()
        {
            var random = new FixedRandomSource(2, 4, 6);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, attackerConditionalRerollMax: 2));

            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.EqualTo(2));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(6));
            Assert.That(result.DefenderIsDefeated, Is.True);
        }

        [Test]
        public void ResolveAttack_RogueConditionalRerollKeepsAnAlreadyWinningRoll()
        {
            var random = new FixedRandomSource(3, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                8,
                new CombatModifiers(false, false, attackerConditionalRerollMax: 3));

            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(3));
            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.Zero);
            Assert.That(result.DefenderIsDefeated, Is.True);
        }

        [Test]
        public void ResolveAttack_RogueConditionalRerollRespectsTheLevelThreshold()
        {
            var random = new FixedRandomSource(4, 8);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 8);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                8,
                new CombatModifiers(false, false, attackerConditionalRerollMax: 3));

            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(4));
            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.Zero);
            Assert.That(result.DefenderIsDefeated, Is.False);
        }

        [Test]
        public void ResolveAttack_RogueAuraRerollsDefenseOnlyWhenNeededToResist()
        {
            var random = new FixedRandomSource(5, 2, 6);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 5);
            var defender = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, defenderConditionalRerollMax: 2));

            Assert.That(result.DefenderRoll.FirstRollBeforeReroll, Is.EqualTo(2));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(6));
            Assert.That(result.DefenderIsDefeated, Is.False);
        }

        [Test]
        public void ResolveAttack_RogueAuraKeepsAnAlreadySuccessfulDefense()
        {
            var random = new FixedRandomSource(2, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("assassin", "Assassino", HeroClass.Assassin, 5);
            var defender = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, defenderConditionalRerollMax: 2));

            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
            Assert.That(result.DefenderRoll.FirstRollBeforeReroll, Is.Zero);
            Assert.That(result.DefenderIsDefeated, Is.False);
        }

        [Test]
        public void ResolveAttack_RogueRerollsOneOncePerAttackerDieWithAdvantage()
        {
            var random = new FixedRandomSource(1, 4, 1, 5, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("mage", "Mago", HeroClass.Mage, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, rerollAttackerOnes: true));

            Assert.That(result.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Advantage));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(4));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(5));
            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.AttackerRoll.SecondRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.AttackerVigor, Is.EqualTo(5));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
        }

        [Test]
        public void ResolveAttack_RogueRerollsOneOncePerAttackerDieWithDisadvantage()
        {
            var random = new FixedRandomSource(1, 4, 1, 5, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("paladin", "Paladino", HeroClass.Paladin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, rerollAttackerOnes: true));

            Assert.That(result.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Disadvantage));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(4));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(5));
            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.AttackerRoll.SecondRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.AttackerVigor, Is.EqualTo(4));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
        }

        [Test]
        public void ResolveAttack_RogueAuraRerollsFirstOneOrTwoOncePerAttackerDie()
        {
            var random = new FixedRandomSource(2, 6, 1, 5, 3);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("mage", "Mago", HeroClass.Mage, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, rerollAttackerOnes: true, rerollAttackerTwos: true));

            Assert.That(result.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Advantage));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(6));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(5));
            Assert.That(result.AttackerRoll.FirstRollBeforeReroll, Is.EqualTo(2));
            Assert.That(result.AttackerRoll.SecondRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.AttackerVigor, Is.EqualTo(6));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(3));
        }

        [Test]
        public void ResolveAttack_RogueAuraRerollsFirstOneOrTwoOncePerDefenderDieWithAdvantage()
        {
            // Forza batte Astuzia: l'attaccante tira due dadi (3 e 2) prima che tocchi
            // al difensore, che ritira sia il 2 sia l'1 una volta ciascuno.
            var random = new FixedRandomSource(3, 2, 2, 6, 1, 5);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("warrior", "Guerriero", HeroClass.Warrior, 5);
            var defender = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(
                    sumAttackerVigor: false,
                    defenderAdvantage: true,
                    rerollDefenderOnes: true,
                    rerollDefenderTwos: true));

            Assert.That(result.DefenderRoll.SelectionMode, Is.EqualTo(VigorSelectionMode.Highest));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(6));
            Assert.That(result.DefenderRoll.SecondRoll, Is.EqualTo(5));
            Assert.That(result.DefenderRoll.FirstRollBeforeReroll, Is.EqualTo(2));
            Assert.That(result.DefenderRoll.SecondRollBeforeReroll, Is.EqualTo(1));
            Assert.That(result.DefenderVigor, Is.EqualTo(6));
        }

        [Test]
        public void ResolveAttack_AppliesFlatClassBonus()
        {
            var random = new FixedRandomSource(3, 3);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("hunter", "Cacciatore", HeroClass.Hunter, 5);
            var defender = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, attackerFlatBonus: 2));

            Assert.That(result.AttackerTotal, Is.EqualTo(10));
            Assert.That(result.DefenderTotal, Is.EqualTo(8));
        }

        [Test]
        public void ResolveAttack_AppliesFlatDefenseBonus()
        {
            var random = new FixedRandomSource(3, 3);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("hunter", "Cacciatore", HeroClass.Hunter, 5);
            var defender = new CombatCard("barbarian", "Barbaro", HeroClass.Barbarian, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                new CombatModifiers(false, false, defenderFlatBonus: 2));

            Assert.That(result.AttackerTotal, Is.EqualTo(8));
            Assert.That(result.DefenderTotal, Is.EqualTo(10));
        }

        [Test]
        public void CombatDiceRoller_WithoutBias_DoesNotConsumeAProbabilityRoll()
        {
            var random = new FixedRandomSource(4, 6);
            var dice = new CombatDiceRoller(random);

            Assert.That(dice.Roll(6), Is.EqualTo(4));
            Assert.That(dice.Roll(6), Is.EqualTo(6));
        }

        [Test]
        public void CombatDiceRoller_WhenBiasTriggers_KeepsTheHigherHiddenRoll()
        {
            var dice = new CombatDiceRoller(new FixedRandomSource(2, 30, 5));

            Assert.That(dice.Roll(6, 30), Is.EqualTo(5));
        }

        [Test]
        public void ResolveAttack_AppliesBiasOnlyToTheConfiguredSide()
        {
            var resolver = new CombatResolver(new FixedRandomSource(2, 10, 6, 3));
            var attacker = new CombatCard("a", "Attaccante", HeroClass.Paladin, 5);
            var defender = new CombatCard("d", "Difensore", HeroClass.Paladin, 5);

            CombatResult result = resolver.ResolveAttack(
                attacker,
                defender,
                6,
                6,
                CombatModifiers.None,
                new CombatRollBiases(attackerHighRollChancePercent: 15, defenderHighRollChancePercent: 0));

            Assert.That(result.AttackerRoll.SelectedRoll, Is.EqualTo(6));
            Assert.That(result.DefenderRoll.SelectedRoll, Is.EqualTo(3));
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            private readonly Queue<int> values;

            public FixedRandomSource(params int[] values)
            {
                this.values = new Queue<int>(values);
            }

            public int NextInclusive(int minimum, int maximum)
            {
                return values.Dequeue();
            }
        }

        private sealed class RecordingRandomSource : IRandomSource
        {
            private readonly Queue<int> values;

            public RecordingRandomSource(params int[] values)
            {
                this.values = new Queue<int>(values);
            }

            public List<int> Maximums { get; } = new List<int>();

            public int NextInclusive(int minimum, int maximum)
            {
                Maximums.Add(maximum);
                return values.Dequeue();
            }
        }
    }
}
