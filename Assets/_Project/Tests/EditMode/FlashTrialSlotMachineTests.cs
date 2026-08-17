using AccardND.GameCore;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class FlashTrialSlotMachineTests
    {
        [TestCase(0, 2)]
        [TestCase(1, 3)]
        [TestCase(6, 8)]
        [TestCase(7, 9)]
        [TestCase(8, 10)]
        public void Roll_StrengthEqualsCompletedSequencePlusTwo(int completedLevels, int expectedStrength)
        {
            var slot = new FlashTrialSlotMachine(123);

            FlashTrialSlotOutcome outcome = slot.Roll(
                FlashTrialMemoryGame.Evaluate(completedLevels), completedLevels);

            Assert.That(outcome.Strength, Is.EqualTo(expectedStrength));
            Assert.That(outcome.Amount, Is.GreaterThan(0));
        }

        [Test]
        public void BetterPerformance_ProducesLargerCurrencyReward()
        {
            var failed = new FlashTrialSlotMachine(42).Roll(FlashTrialResult.Failed, 0);
            var perfect = new FlashTrialSlotMachine(42).Roll(FlashTrialResult.Perfect, 8);

            Assert.That(perfect.Currency, Is.EqualTo(failed.Currency));
            Assert.That(perfect.Amount, Is.GreaterThan(failed.Amount));
        }

        [Test]
        public void Roll_WithEligiblePool_CannotReturnOwnedCardRemovedByCaller()
        {
            var eligible = new[]
            {
                new FlashTrialCardCandidate("mage-10", HeroClass.Mage, 10),
                new FlashTrialCardCandidate("paladin-9", HeroClass.Paladin, 9)
            };

            // warrior-10 e' gia' posseduta e quindi non compare nel pool eleggibile.
            FlashTrialSlotOutcome outcome = new FlashTrialSlotMachine(4)
                .Roll(FlashTrialResult.Perfect, 8, eligible);

            Assert.That(outcome.CardId, Is.EqualTo("mage-10"));
            Assert.That(outcome.HeroClass, Is.EqualTo(HeroClass.Mage));
            Assert.That(outcome.Strength, Is.EqualTo(10));
        }

        [Test]
        public void Roll_WhenExactPowerIsUnavailable_UsesClosestLowerCard()
        {
            var eligible = new[]
            {
                new FlashTrialCardCandidate("warrior-8", HeroClass.Warrior, 8),
                new FlashTrialCardCandidate("mage-10", HeroClass.Mage, 10)
            };

            FlashTrialSlotOutcome outcome = new FlashTrialSlotMachine(8)
                .Roll(FlashTrialResult.Excellent, 7, eligible);

            Assert.That(outcome.CardId, Is.EqualTo("warrior-8"));
        }
    }
}
