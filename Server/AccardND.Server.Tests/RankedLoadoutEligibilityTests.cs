using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Match;
using Xunit;

namespace AccardND.Server.Tests;

public sealed class RankedLoadoutEligibilityTests
{
    [Fact]
    public void RejectsLockedClassWithExplicitReason()
    {
        var loadout = Loadout(HeroClass.Assassin);
        var progress = new SinglePlayerProgressData
        {
            unlockedClasses = Array.Empty<string>(),
            unlockedSecondAbilities = Array.Empty<string>()
        };

        string failure = Assert.Single(RankedLoadoutEligibility.GetFailures(loadout, progress));

        Assert.Contains("Assassino", failure);
        Assert.Contains("classe non sbloccata", failure);
    }

    [Fact]
    public void AcceptsOwnedClassWithoutSupreme()
    {
        var progress = new SinglePlayerProgressData
        {
            unlockedClasses = new[] { "mage" },
            unlockedSecondAbilities = Array.Empty<string>()
        };

        Assert.Empty(RankedLoadoutEligibility.GetFailures(Loadout(HeroClass.Mage), progress));
    }

    private static PvpLoadout Loadout(HeroClass heroClass) => new(
        new[] { new PvpLoadoutCard("test-card", 1, heroClass) }, 6, Array.Empty<int>());
}
