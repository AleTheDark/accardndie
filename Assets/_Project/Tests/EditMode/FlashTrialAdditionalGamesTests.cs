using AccardND.GameCore;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class FlashTrialAdditionalGamesTests
    {
        [TestCase(0, FlashTrialResult.Failed)]
        [TestCase(1, FlashTrialResult.Failed)]
        [TestCase(2, FlashTrialResult.Good)]
        [TestCase(3, FlashTrialResult.Excellent)]
        public void QuizSession_ThreeAnswersDetermineFinalTier(int correctAnswers, FlashTrialResult expected)
        {
            var session = new FlashTrialQuizSession();
            for (int index = 0; index < FlashTrialQuizSession.TotalQuestions; index++)
                session.RecordAnswer(index < correctAnswers);

            Assert.That(session.Result, Is.EqualTo(expected));
        }

        [TestCase(2f, FlashTrialResult.Perfect)]
        [TestCase(5f, FlashTrialResult.Excellent)]
        [TestCase(9f, FlashTrialResult.Good)]
        [TestCase(14f, FlashTrialResult.Completed)]
        public void Quiz_CorrectAnswerUsesElapsedTime(float seconds, FlashTrialResult expected)
        {
            Assert.That(new FlashTrialQuizGame(1).Submit(1, seconds), Is.EqualTo(expected));
        }

        [Test]
        public void Puzzle_ShuffleIsReversibleThroughValidMoves()
        {
            var puzzle = new FlashTrialSlidingPuzzle(123);
            puzzle.Shuffle(50);

            Assert.That(puzzle.IsSolved, Is.False);
            Assert.That(puzzle.Cells.Count, Is.EqualTo(9));
            Assert.That(puzzle.Cells[puzzle.EmptyIndex], Is.EqualTo(-1));
        }

        [Test]
        public void Puzzle_RejectsNonAdjacentTile()
        {
            var puzzle = new FlashTrialSlidingPuzzle(1);

            Assert.That(puzzle.TryMove(0), Is.False);
            Assert.That(puzzle.Moves, Is.Zero);
        }

        [TestCase(40f, 55, FlashTrialResult.Perfect)]
        [TestCase(40f, 90, FlashTrialResult.Excellent)]
        [TestCase(100f, 55, FlashTrialResult.Good)]
        [TestCase(130f, 170, FlashTrialResult.Completed)]
        public void Puzzle_EvaluationUsesWorseOfTimeAndMoves(float seconds, int moves, FlashTrialResult expected)
        {
            Assert.That(FlashTrialSlidingPuzzle.Evaluate(seconds, moves), Is.EqualTo(expected));
        }
    }
}
