using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class ComposableGolemTests
    {
        [Test]
        public void StartsWithSharedHitPointPool()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource());

            Assert.That(golem.MaxHitPoints, Is.EqualTo(30));
            Assert.That(golem.HitPoints, Is.EqualTo(30));
            Assert.That(golem.Forms.Count, Is.EqualTo(3));
        }

        [Test]
        public void RollsInitiativeOnlyOnce()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource(12, 4));

            int first = golem.RollInitiative(20);
            int second = golem.RollInitiative(20);

            Assert.That(first, Is.EqualTo(12));
            Assert.That(second, Is.EqualTo(12));
        }

        [Test]
        public void RotatesFormEveryTwoEndRounds()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource());

            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Iron));
            Assert.That(golem.ActiveForm.Power, Is.EqualTo(8));
            Assert.That(golem.EndRound(), Is.False);
            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Iron));

            Assert.That(golem.EndRound(), Is.True);
            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Crystal));
            Assert.That(golem.ActiveForm.Power, Is.EqualTo(6));
            Assert.That(golem.ActiveForm.PowerBonus, Is.Zero);
        }

        [Test]
        public void DamageSubtractsDifferenceFromSharedHitPoints()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource(2));

            ComposableGolemDefenseResult result = golem.DefendAgainst(10);

            Assert.That(result.DefenseTotal, Is.EqualTo(10));
            Assert.That(result.Damage, Is.Zero);
            Assert.That(result.Healing, Is.Zero);
            Assert.That(golem.HitPoints, Is.EqualTo(30));
        }

        [Test]
        public void GlassHealsWhenDefenseBeatsAttack()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource(2, 4));
            golem.DefendAgainst(12);
            golem.EndRound();
            golem.EndRound();
            golem.EndRound();
            golem.EndRound();

            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Glass));

            ComposableGolemDefenseResult result = golem.DefendAgainst(7);

            Assert.That(result.DefenseTotal, Is.EqualTo(10));
            Assert.That(result.Healing, Is.EqualTo(3));
            Assert.That(golem.HitPoints, Is.EqualTo(30));
        }

        [Test]
        public void ReturningToAFormStacksItsPowerBonus()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource());

            for (int index = 0; index < 6; index++)
                golem.EndRound();

            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Iron));
            Assert.That(golem.ActiveForm.PowerBonus, Is.EqualTo(1));
            Assert.That(golem.ActiveForm.Power, Is.EqualTo(9));
            Assert.That(golem.Forms[1].PowerBonus, Is.EqualTo(1));
            Assert.That(golem.Forms[2].PowerBonus, Is.EqualTo(1));
        }

        [Test]
        public void PowerBonusIncreasesOnlyAfterEachFullFormCycle()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource());

            for (int index = 0; index < 4; index++)
                golem.EndRound();

            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Glass));
            Assert.That(golem.Forms[0].PowerBonus, Is.Zero);
            Assert.That(golem.Forms[1].PowerBonus, Is.Zero);
            Assert.That(golem.Forms[2].PowerBonus, Is.Zero);

            golem.EndRound();
            golem.EndRound();

            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Iron));
            Assert.That(golem.Forms[0].PowerBonus, Is.EqualTo(1));
            Assert.That(golem.Forms[1].PowerBonus, Is.EqualTo(1));
            Assert.That(golem.Forms[2].PowerBonus, Is.EqualTo(1));

            for (int index = 0; index < 6; index++)
                golem.EndRound();

            Assert.That(golem.ActiveForm.Form, Is.EqualTo(ComposableGolemForm.Iron));
            Assert.That(golem.Forms[0].PowerBonus, Is.EqualTo(2));
            Assert.That(golem.Forms[1].PowerBonus, Is.EqualTo(2));
            Assert.That(golem.Forms[2].PowerBonus, Is.EqualTo(2));
        }

        [Test]
        public void HealingCannotExceedMaximumHitPoints()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource(4));
            golem.EndRound();
            golem.EndRound();
            golem.EndRound();
            golem.EndRound();

            golem.DefendAgainst(7);

            Assert.That(golem.HitPoints, Is.EqualTo(30));
        }

        [Test]
        public void RestoredStateKeepsCurrentHitPointsAndFlatPowerBonuses()
        {
            var forms = new[]
            {
                ComposableGolem.CreateIronStats().AddPowerBonus(2),
                ComposableGolem.CreateCrystalStats().AddPowerBonus(1),
                ComposableGolem.CreateGlassStats()
            };

            var golem = new ComposableGolem(
                new FixedRandomSource(),
                maxHitPoints: 30,
                currentHitPoints: 17,
                roundsPerForm: 2,
                forms);

            Assert.That(golem.HitPoints, Is.EqualTo(17));
            Assert.That(golem.Forms[0].PowerBonus, Is.EqualTo(2));
            Assert.That(golem.Forms[1].PowerBonus, Is.EqualTo(1));
        }

        [Test]
        public void CustomVigorDieAppliesToDefenseAndAttack()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource(4, 4, 2));

            ComposableGolemDefenseResult defense = golem.DefendAgainst(10, vigorDieSides: 4);
            var target = new CombatCard("target", "Target", HeroClass.Warrior, 6);
            ComposableGolemAttackResult attack = golem.Attack(target, targetVigorDieSides: 6, vigorDieSides: 4);

            Assert.That(defense.VigorDieSides, Is.EqualTo(4));
            Assert.That(defense.DefenseTotal, Is.EqualTo(12));
            Assert.That(attack.VigorDieSides, Is.EqualTo(4));
            Assert.That(attack.AttackTotal, Is.EqualTo(12));
        }

        [Test]
        public void FlatPowerModifierAppliesToDefenseAndAttack()
        {
            var golem = CreateOrderedGolem(new FixedRandomSource(4, 4, 2));

            ComposableGolemDefenseResult defense = golem.DefendAgainst(
                attackerTotal: 10,
                vigorDieSides: 6,
                powerModifier: -2);
            var target = new CombatCard("target", "Target", HeroClass.Warrior, 6);
            ComposableGolemAttackResult attack = golem.Attack(
                target,
                targetVigorDieSides: 6,
                vigorDieSides: 6,
                powerModifier: -2);

            Assert.That(defense.DefenseTotal, Is.EqualTo(10));
            Assert.That(attack.AttackTotal, Is.EqualTo(10));
        }

        [Test]
        public void SelectsHighestStrengthTargetBreakingTiesByInitiative()
        {
            var targets = new[]
            {
                new CombatCard("a", "A", HeroClass.Warrior, 6),
                new CombatCard("b", "B", HeroClass.Mage, 8),
                new CombatCard("c", "C", HeroClass.Rogue, 8)
            };
            int[] initiatives = { 15, 4, 11 };

            int selected = ComposableGolem.SelectHighestStrengthTarget(targets, initiatives);

            Assert.That(selected, Is.EqualTo(2));
        }

        private static ComposableGolem CreateOrderedGolem(IRandomSource random)
        {
            return new ComposableGolem(
                random,
                maxHitPoints: 30,
                roundsPerForm: 2,
                new[]
                {
                    ComposableGolem.CreateIronStats(),
                    ComposableGolem.CreateCrystalStats(),
                    ComposableGolem.CreateGlassStats()
                });
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
