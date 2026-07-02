using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class RunProgressStateTests
    {
        [Test]
        public void CompleteMonsterRoom_AwardsFlatAndDefeatedMonsterStrengths()
        {
            var progress = CreateProgress();

            RoomReward reward = progress.CompleteMonsterRoom(new[] { 5, 8, 3 });

            Assert.That(reward.RoomExperience, Is.EqualTo(10));
            Assert.That(reward.DefeatedMonsterExperience, Is.EqualTo(16));
            Assert.That(progress.CurrentExperience, Is.EqualTo(26));
            Assert.That(progress.RoomsCleared, Is.EqualTo(1));
        }

        [Test]
        public void ExperienceLevelsUpAndChangesVigorDie()
        {
            var progress = CreateProgress();

            progress.CompleteMonsterRoom(new[] { 90 });

            Assert.That(progress.PlayerLevel, Is.EqualTo(2));
            Assert.That(progress.CurrentExperience, Is.Zero);
            Assert.That(progress.PlayerVigorDieSides, Is.EqualTo(6));
        }

        [Test]
        public void MasterLevelsEveryFiveRoomsOrWithPlayer()
        {
            var progress = CreateProgress();
            for (int room = 0; room < 5; room++)
                progress.CompleteMonsterRoom(System.Array.Empty<int>());

            Assert.That(progress.MasterLevel, Is.EqualTo(2));
            Assert.That(progress.MasterVigorDieSides, Is.EqualTo(6));
        }

        [Test]
        public void SpendableExperienceCanBeSpentWithoutReducingLevelProgress()
        {
            var progress = CreateProgress();
            progress.CompleteNonCombatRoom(50);

            bool spent = progress.TrySpendExperience(15);

            Assert.That(spent, Is.True);
            Assert.That(progress.AvailableExperience, Is.EqualTo(35));
            Assert.That(progress.CurrentExperience, Is.EqualTo(50));
        }

        [Test]
        public void SpendableExperienceCanBeAddedWithoutReducingLevelProgress()
        {
            var progress = CreateProgress();
            progress.CompleteNonCombatRoom(50);

            progress.AddSpendableExperience(12);

            Assert.That(progress.AvailableExperience, Is.EqualTo(62));
            Assert.That(progress.CurrentExperience, Is.EqualTo(50));
            Assert.That(progress.PlayerLevel, Is.EqualTo(1));
        }

        [Test]
        public void AddedExperienceIncreasesSpendableAndCanLevelUpWithoutClearingRoom()
        {
            var progress = CreateProgress();
            progress.CompleteNonCombatRoom(90);

            int levelsGained = progress.AddExperience(12);

            Assert.That(levelsGained, Is.EqualTo(1));
            Assert.That(progress.PlayerLevel, Is.EqualTo(2));
            Assert.That(progress.CurrentExperience, Is.EqualTo(2));
            Assert.That(progress.AvailableExperience, Is.EqualTo(102));
            Assert.That(progress.RoomsCleared, Is.EqualTo(1));
        }

        private static RunProgressState CreateProgress()
        {
            return new RunProgressState(100, 10, 6, 5, new[] { 4, 6, 8, 10, 12, 20 });
        }
    }
}
