using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class BragusBossTests
    {
        [Test]
        public void DamagedDefenseDoesNotCounterattack()
        {
            var bragus = new BragusBoss(new FixedRandomSource());
            var attacker = new CombatCard("hero", "Hero", HeroClass.Barbarian, 7);

            BragusDefenseResult result = bragus.ApplyResolvedDefense(18, 4, 14, attacker, 7, 6);

            Assert.That(result.Damage, Is.EqualTo(4));
            Assert.That(result.HitPointsAfter, Is.EqualTo(BragusBoss.DefaultHitPoints - 4));
            Assert.That(result.Counterattacks, Is.False);
        }

        [Test]
        public void BlockedDefenseCounterattacksImmediately()
        {
            var bragus = new BragusBoss(new FixedRandomSource(5, 3));
            var attacker = new CombatCard("hero", "Hero", HeroClass.Barbarian, 7);

            BragusDefenseResult result = bragus.ApplyResolvedDefense(12, 4, 14, attacker, 7, 6);

            Assert.That(result.Damage, Is.Zero);
            Assert.That(result.Counterattacks, Is.True);
            Assert.That(result.CounterTotal, Is.EqualTo(BragusBoss.CardStrength + 5));
            Assert.That(result.TargetDefenseTotal, Is.EqualTo(7 + 3));
            Assert.That(result.CounterDefeatsAttacker, Is.True);
        }

        [Test]
        public void CounterattackTargetCanDefendWithAdvantage()
        {
            var bragus = new BragusBoss(new FixedRandomSource(5, 2, 6));
            var attacker = new CombatCard("paladin", "Paladin", HeroClass.Paladin, 7);

            BragusDefenseResult result = bragus.ApplyResolvedDefense(12, 4, 14, attacker, 7, 6, counterTargetDefenseAdvantage: true);

            Assert.That(result.Counterattacks, Is.True);
            Assert.That(result.CounterTotal, Is.EqualTo(BragusBoss.CardStrength + 5));
            Assert.That(result.TargetDefenseTotal, Is.EqualTo(7 + 6));
            Assert.That(result.CounterDefeatsAttacker, Is.True);
        }

        [Test]
        public void CounterattackHasDisadvantageAgainstCunning()
        {
            var bragus = new BragusBoss(new FixedRandomSource(6, 2, 1));
            var attacker = new CombatCard("rogue", "Rogue", HeroClass.Rogue, 7);

            BragusDefenseResult result = bragus.ApplyResolvedDefense(12, 4, 14, attacker, 7, 6);

            Assert.That(result.Counterattacks, Is.True);
            Assert.That(result.CounterRoll, Is.EqualTo(2));
            Assert.That(result.CounterTotal, Is.EqualTo(BragusBoss.CardStrength + 2));
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
