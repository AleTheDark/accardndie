using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class ClassMatchupTests
    {
        [TestCase(HeroClass.Warrior, HeroClass.Assassin)]
        [TestCase(HeroClass.Barbarian, HeroClass.Hunter)]
        [TestCase(HeroClass.Rogue, HeroClass.Mage)]
        [TestCase(HeroClass.Assassin, HeroClass.Priest)]
        [TestCase(HeroClass.Necromancer, HeroClass.Paladin)]
        [TestCase(HeroClass.Priest, HeroClass.Warrior)]
        public void Compare_ReturnsAdvantage_ForWinningClass(HeroClass winner, HeroClass loser)
        {
            Assert.That(ClassMatchup.Compare(winner, loser), Is.EqualTo(MatchupResult.Advantage));
            Assert.That(ClassMatchup.Compare(loser, winner), Is.EqualTo(MatchupResult.Disadvantage));
        }

        [Test]
        public void Compare_ReturnsNeutral_ForSameClass()
        {
            Assert.That(
                ClassMatchup.Compare(HeroClass.Assassin, HeroClass.Assassin),
                Is.EqualTo(MatchupResult.Neutral));
        }

        [Test]
        public void Compare_ReturnsNeutral_ForClassesInSameFamily()
        {
            Assert.That(
                ClassMatchup.Compare(HeroClass.Warrior, HeroClass.Barbarian),
                Is.EqualTo(MatchupResult.Neutral));
        }
    }
}
