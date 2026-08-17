using AccardND.GameCore;
using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
	internal sealed class SeraphelFixedRandomSource : IRandomSource
	{
		private readonly Queue<int> values;
		public SeraphelFixedRandomSource(params int[] values) => this.values = new Queue<int>(values);
		public int NextInclusive(int minimum, int maximum)
		{
			if (values.Count == 0) return minimum;
			return System.Math.Clamp(values.Dequeue(), minimum, maximum);
		}
	}

    public sealed class SeraphelBossTests
    {
        [Test]
        public void StartsInPhaseOneWithEightyHp()
        {
            var boss = new SeraphelBoss(new SeraphelFixedRandomSource());
            Assert.That(boss.HitPoints, Is.EqualTo(80));
            Assert.That(boss.Strength, Is.EqualTo(8));
            Assert.That(boss.VigorDieSides, Is.EqualTo(10));
            Assert.That(boss.SealsPerHit, Is.EqualTo(1));
            Assert.That(boss.IsImmuneToDebuffs, Is.False);
        }

        [Test]
        public void CrossingHalfHealthActivatesPhaseTwo()
        {
            var boss = new SeraphelBoss(new SeraphelFixedRandomSource());
            SeraphelDefenseResult result = boss.ApplyResolvedDefense(50, 10);
            Assert.That(result.PhaseChanged, Is.True);
            Assert.That(boss.Strength, Is.EqualTo(10));
            Assert.That(boss.VigorDieSides, Is.EqualTo(12));
            Assert.That(boss.SealsPerHit, Is.EqualTo(2));
            Assert.That(boss.IsImmuneToDebuffs, Is.True);
        }

        [Test]
        public void SuccessfulAttackAppliesSealsAndUsesTwoDamagePerExistingSeal()
        {
            var boss = new SeraphelBoss(new SeraphelFixedRandomSource(10, 1));
            var target = new CombatCard("hero", "Hero", HeroClass.Warrior, 8);
            SeraphelAttackResult result = boss.Attack(target, 6, 2);
            Assert.That(result.AttackTotal, Is.EqualTo(22));
            Assert.That(result.SealDamageBonus, Is.EqualTo(4));
            Assert.That(result.SealsApplied, Is.EqualTo(1));
        }

    }
}
