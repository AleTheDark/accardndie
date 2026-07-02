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
        public void ResolveAttack_WarriorAbilitySumsTwoAttackerDice()
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
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
        }

        [Test]
        public void ResolveAttack_RogueRerollsAllAttackerOnesWithAdvantage()
        {
            var random = new FixedRandomSource(1, 4, 1, 5, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("mage", "Mago", HeroClass.Mage, 5);

            CombatResult result = resolver.ResolveAttack(attacker, defender, 6);

            Assert.That(result.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Advantage));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(4));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(5));
            Assert.That(result.AttackerVigor, Is.EqualTo(5));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
        }

        [Test]
        public void ResolveAttack_RogueRerollsAllAttackerOnesWithDisadvantage()
        {
            var random = new FixedRandomSource(1, 4, 1, 5, 2);
            var resolver = new CombatResolver(random);
            var attacker = new CombatCard("rogue", "Ladro", HeroClass.Rogue, 5);
            var defender = new CombatCard("paladin", "Paladino", HeroClass.Paladin, 5);

            CombatResult result = resolver.ResolveAttack(attacker, defender, 6);

            Assert.That(result.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Disadvantage));
            Assert.That(result.AttackerRoll.FirstRoll, Is.EqualTo(4));
            Assert.That(result.AttackerRoll.SecondRoll, Is.EqualTo(5));
            Assert.That(result.AttackerVigor, Is.EqualTo(4));
            Assert.That(result.DefenderRoll.FirstRoll, Is.EqualTo(2));
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
    }
}
