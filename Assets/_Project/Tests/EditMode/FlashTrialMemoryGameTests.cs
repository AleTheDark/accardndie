using AccardND.GameCore;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class FlashTrialMemoryGameTests
    {
        [Test]
        public void CorrectSequence_AddsOneSymbolEveryRound()
        {
            var game = new FlashTrialMemoryGame(1234);

            game.BeginNextRound();
            Assert.That(game.Submit(game.Sequence[0]), Is.EqualTo(FlashTrialMemoryInputResult.RoundCompleted));

            game.BeginNextRound();
            Assert.That(game.Sequence.Count, Is.EqualTo(2));
            Assert.That(game.Submit(game.Sequence[0]), Is.EqualTo(FlashTrialMemoryInputResult.AwaitingInput));
            Assert.That(game.Submit(game.Sequence[1]), Is.EqualTo(FlashTrialMemoryInputResult.RoundCompleted));
            Assert.That(game.CompletedLevels, Is.EqualTo(2));
        }

        [Test]
        public void WrongSymbol_EndsAttemptAndKeepsLongestCompletedSequence()
        {
            var game = new FlashTrialMemoryGame(7, classes: new[] { HeroClass.Mage });
            game.BeginNextRound();
            game.Submit(HeroClass.Mage);
            game.BeginNextRound();

            FlashTrialMemoryInputResult result = game.Submit(HeroClass.Warrior);

            Assert.That(result, Is.EqualTo(FlashTrialMemoryInputResult.Failed));
            Assert.That(game.CompletedLevels, Is.EqualTo(1));
            Assert.That(game.IsFinished, Is.True);
        }

        [TestCase(0, FlashTrialResult.Failed)]
        [TestCase(1, FlashTrialResult.Failed)]
        [TestCase(2, FlashTrialResult.Completed)]
        [TestCase(4, FlashTrialResult.Good)]
        [TestCase(6, FlashTrialResult.Excellent)]
        [TestCase(8, FlashTrialResult.Perfect)]
        public void Evaluate_MapsLongestSequenceToRewardTier(int levels, FlashTrialResult expected)
        {
            Assert.That(FlashTrialMemoryGame.Evaluate(levels), Is.EqualTo(expected));
        }
    }
}
