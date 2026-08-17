using AccardND.GameData;
using NUnit.Framework;

namespace AccardND.Tests.EditMode
{
    public sealed class RoomDifficultyRulesTests
    {
        [TestCase(RoomDifficulty.Easy, "Accessibile", 6, 20, 8, 5, true, false, 1)]
        [TestCase(RoomDifficulty.Normal, "Normale", 6, 25, 9, 10, true, false, 2)]
        [TestCase(RoomDifficulty.Hard, "Diabolica", 24, 30, 10, 15, true, true, 4)]
        public void RulesMatchCampaignDesign(RoomDifficulty difficulty, string name, int min, int max, int maxCardStrength, int experience, bool advanced, bool supremes, int startingMana)
        {
            RoomDifficultyRules rules = RoomDifficultyRules.For(difficulty);
            Assert.That(rules.DisplayName, Is.EqualTo(name));
            Assert.That(rules.MinimumFormationPower, Is.EqualTo(min));
            Assert.That(rules.MaximumFormationPower, Is.EqualTo(max));
            Assert.That(rules.MaximumMonsterCardStrength, Is.EqualTo(maxCardStrength));
            Assert.That(rules.BaseExperience, Is.EqualTo(experience));
            Assert.That(rules.CpuUsesAbilities, Is.EqualTo(advanced));
            Assert.That(rules.CpuUsesMana, Is.EqualTo(advanced));
            Assert.That(rules.CpuUsesAttachments, Is.EqualTo(advanced));
            Assert.That(rules.CpuUsesSupremes, Is.EqualTo(supremes));
            Assert.That(rules.CpuStartingMana, Is.EqualTo(startingMana));
        }

        [TestCase(1, 1, 60, 40, 0)]
        [TestCase(2, 11, 25, 50, 30)]
        [TestCase(4, 9, 30, 65, 5)]
        [TestCase(7, 19, 0, 20, 80)]
        [TestCase(9, 1, 0, 70, 30)]
        [TestCase(9, 11, 0, 0, 100)]
        public void ScenarioWeightsMatchDesign(int scenario, int room, int accessible, int normal, int diabolic)
        {
            ScenarioMonsterDifficultyWeights weights = ScenarioMonsterDifficultyWeights.For(scenario, room);
            Assert.That(weights.Accessible, Is.EqualTo(accessible));
            Assert.That(weights.Normal, Is.EqualTo(normal));
            Assert.That(weights.Diabolic, Is.EqualTo(diabolic));
        }
    }
}
