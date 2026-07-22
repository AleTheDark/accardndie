using NUnit.Framework;
using AccardND.GameData;

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
        public void CompleteMinibossRoom_AwardsOnlyTheFlatReward()
        {
            var progress = CreateProgress();

            RoomReward reward = progress.CompleteMinibossRoom(50);

            Assert.That(reward.RoomExperience, Is.EqualTo(50));
            Assert.That(reward.DefeatedMonsterExperience, Is.Zero);
            Assert.That(reward.TotalExperience, Is.EqualTo(50));
            Assert.That(progress.CurrentExperience, Is.EqualTo(50));
            Assert.That(progress.RoomsCleared, Is.EqualTo(1));
        }

        [Test]
        public void ExperienceLevelsUpAndChangesVigorDie()
        {
            var progress = CreateProgress();

            progress.CompleteMonsterRoom(new[] { 40 });

            Assert.That(progress.PlayerLevel, Is.EqualTo(2));
            Assert.That(progress.CurrentExperience, Is.Zero);
            Assert.That(progress.PlayerVigorDieSides, Is.EqualTo(6));
            Assert.That(progress.ExperiencePerLevel, Is.EqualTo(75));
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
            progress.CompleteNonCombatRoom(40);

            progress.AddSpendableExperience(12);

            Assert.That(progress.AvailableExperience, Is.EqualTo(52));
            Assert.That(progress.CurrentExperience, Is.EqualTo(40));
            Assert.That(progress.PlayerLevel, Is.EqualTo(1));
        }

        [Test]
        public void AddedExperienceIncreasesSpendableAndCanLevelUpWithoutClearingRoom()
        {
            var progress = CreateProgress();
            progress.CompleteNonCombatRoom(40);

            int levelsGained = progress.AddExperience(12);

            Assert.That(levelsGained, Is.EqualTo(1));
            Assert.That(progress.PlayerLevel, Is.EqualTo(2));
            Assert.That(progress.CurrentExperience, Is.EqualTo(2));
            Assert.That(progress.AvailableExperience, Is.EqualTo(52));
            Assert.That(progress.RoomsCleared, Is.EqualTo(1));
        }

        [Test]
        public void VariableExperienceThresholdsLevelUpInOrder()
        {
            var progress = CreateProgress();

            int levelsGained = progress.AddExperience(127);

            Assert.That(levelsGained, Is.EqualTo(2));
            Assert.That(progress.PlayerLevel, Is.EqualTo(3));
            Assert.That(progress.CurrentExperience, Is.EqualTo(2));
            Assert.That(progress.ExperiencePerLevel, Is.EqualTo(100));
            Assert.That(progress.PlayerVigorDieSides, Is.EqualTo(8));
        }

        [Test]
        public void ProgressionConfiguration_UsesGameplayVigorDieAsStartingDie()
        {
            var progression = new ProgressionConfiguration();

            int[] dice = progression.BuildVigorDiceByLevel(4);

            Assert.That(dice[0], Is.EqualTo(4));
            Assert.That(dice[1], Is.EqualTo(6));
            Assert.That(dice[2], Is.EqualTo(8));
        }

        [Test]
        public void ProgressionConfiguration_DebugStartingDieDoesNotDowngradeOnLevelUp()
        {
            var progression = new ProgressionConfiguration();

            int[] dice = progression.BuildVigorDiceByLevel(8);

            Assert.That(dice[0], Is.EqualTo(8));
            Assert.That(dice[1], Is.EqualTo(8));
            Assert.That(dice[2], Is.EqualTo(8));
            Assert.That(dice[3], Is.EqualTo(10));
        }

        private static RunProgressState CreateProgress()
        {
            return new RunProgressState(new[] { 50, 75, 100, 125, 150 }, 10, 6, 5, new[] { 4, 6, 8, 10, 12, 20 });
        }
    }
}
