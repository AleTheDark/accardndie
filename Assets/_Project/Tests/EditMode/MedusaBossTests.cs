using System.Collections.Generic;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class MedusaBossTests
    {
        [Test]
        public void UnpetrifyRequiresRollAboveHalfVigorDie()
        {
            var medusa = new MedusaBoss(new FixedRandomSource(3, 4));

            MedusaUnpetrifyResult failed = medusa.RollUnpetrify(6);
            MedusaUnpetrifyResult freed = medusa.RollUnpetrify(6);

            Assert.That(failed.RequiredRoll, Is.EqualTo(3));
            Assert.That(failed.Freed, Is.False);
            Assert.That(freed.RequiredRoll, Is.EqualTo(3));
            Assert.That(freed.Freed, Is.True);
        }

        [Test]
        public void PetrifyingGazeUsesOneAttackRollAndResolvesEachTargetSeparately()
        {
            var medusa = new MedusaBoss(new FixedRandomSource(4, 2, 3, 1));
            var targets = new[]
            {
                new CombatCard("mage-a", "Mage A", HeroClass.Mage, 4),
                new CombatCard("mage-b", "Mage B", HeroClass.Mage, 4),
                new CombatCard("mage-c", "Mage C", HeroClass.Mage, 4)
            };

            MedusaPetrifyingGazeResult result = medusa.PetrifyingGaze(targets, new[] { 6, 6, 6 }, 6);

            Assert.That(result.MedusaRolls.Count, Is.EqualTo(1));
            Assert.That(result.TargetRolls.Count, Is.EqualTo(3));
            Assert.That(result.MedusaTotal, Is.EqualTo(MedusaBoss.CardStrength + 4));
            Assert.That(result.AlliesTotal, Is.EqualTo(2 + 3 + 1));
            Assert.That(result.TargetTotals, Is.EqualTo(new[] { 6, 7, 5 }));
            Assert.That(result.PetrifiesTarget(0), Is.True);
            Assert.That(result.PetrifiesTarget(1), Is.True);
            Assert.That(result.PetrifiesTarget(2), Is.True);
        }

        [Test]
        public void PetrifyingGazePetrifiesOnlyTargetsWhoseTotalItExceeds()
        {
            var medusa = new MedusaBoss(new FixedRandomSource(2, 6, 1));
            var targets = new[]
            {
                new CombatCard("warrior", "Warrior", HeroClass.Warrior, 5),
                new CombatCard("mage", "Mage", HeroClass.Mage, 4)
            };

            MedusaPetrifyingGazeResult result = medusa.PetrifyingGaze(targets, new[] { 6, 6 }, 6);

            Assert.That(result.MedusaTotal, Is.EqualTo(10));
            Assert.That(result.TargetTotals, Is.EqualTo(new[] { 11, 5 }));
            Assert.That(result.PetrifiesTarget(0), Is.False);
            Assert.That(result.PetrifiesTarget(1), Is.True);
            Assert.That(result.PetrifiesTargets, Is.True);
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
