using AccardND.GameCore;
using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.Tests.EditMode
{
    public sealed class PalatirBossTests
    {
        [Test]
        public void ApplyResolvedDefense_BreaksOnlyShieldWeakToAttackerFamily()
        {
            var palatir = new PalatirBoss(new FixedRandomSource());
            var attacker = new CombatCard("mage", "Mage", HeroClass.Mage, 7);

            PalatirDefenseResult result = palatir.ApplyResolvedDefense(attacker, 18, 3, 12);

            Assert.That(result.ShieldWasBroken, Is.True);
            Assert.That(result.TargetedShield, Is.EqualTo(ClassFamily.Might));
            Assert.That(palatir.ActiveShields, Has.No.Member(ClassFamily.Might));
            Assert.That(palatir.HitPoints, Is.EqualTo(PalatirBoss.DefaultHitPoints));
        }

        [Test]
        public void ApplyResolvedDefense_DoesNotDamageHpUntilAllShieldsAreBroken()
        {
            var palatir = new PalatirBoss(new FixedRandomSource());

            palatir.ApplyResolvedDefense(new CombatCard("mage", "Mage", HeroClass.Mage, 7), 18, 3, 12);
            palatir.ApplyResolvedDefense(new CombatCard("warrior", "Warrior", HeroClass.Warrior, 7), 18, 3, 12);

            PalatirDefenseResult result = palatir.ApplyResolvedDefense(
                new CombatCard("mage-2", "Mage 2", HeroClass.Mage, 7),
                22,
                2,
                10);

            Assert.That(result.Damage, Is.Zero);
            Assert.That(result.ShieldWasBroken, Is.False);
            Assert.That(palatir.HitPoints, Is.EqualTo(PalatirBoss.DefaultHitPoints));
        }

        [Test]
        public void ApplyResolvedDefense_DamagesHpAfterFinalShieldBreaks()
        {
            var palatir = new PalatirBoss(new FixedRandomSource());

            palatir.ApplyResolvedDefense(new CombatCard("mage", "Mage", HeroClass.Mage, 7), 18, 3, 12);
            palatir.ApplyResolvedDefense(new CombatCard("warrior", "Warrior", HeroClass.Warrior, 7), 18, 3, 12);
            palatir.ApplyResolvedDefense(new CombatCard("rogue", "Rogue", HeroClass.Rogue, 7), 18, 3, 12);

            PalatirDefenseResult result = palatir.ApplyResolvedDefense(
                new CombatCard("warrior-2", "Warrior 2", HeroClass.Warrior, 7),
                20,
                4,
                13);

            Assert.That(palatir.HasActiveShields, Is.False);
            Assert.That(result.Damage, Is.EqualTo(7));
            Assert.That(palatir.HitPoints, Is.EqualTo(PalatirBoss.DefaultHitPoints - 7));
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
                return values.Count > 0 ? values.Dequeue() : minimum;
            }
        }
    }
}
