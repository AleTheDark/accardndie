using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Xunit;

namespace AccardND.Server.Tests;

public sealed class RankedAccountExperienceTests
{
    [Fact]
    public async Task Ranked_match_grants_five_account_experience_per_round_won()
    {
        using var server = new TestServer();
        AccountIdentity playerA = server.RegisterAccount("ranked-xp-a");
        AccountIdentity playerB = server.RegisterAccount("ranked-xp-b");

        MatchRecordResult result = await RecordAsync(server, playerA, playerB, 2, 1, ranked: true);

        Assert.Equal(10, result.ExperienceA.Experience);
        Assert.Equal(5, result.ExperienceB.Experience);
        Assert.Equal(10L, TotalExperience(server, playerA));
        Assert.Equal(5L, TotalExperience(server, playerB));
    }

    [Fact]
    public async Task Friendly_match_does_not_grant_account_experience()
    {
        using var server = new TestServer();
        AccountIdentity playerA = server.RegisterAccount("friendly-xp-a");
        AccountIdentity playerB = server.RegisterAccount("friendly-xp-b");

        MatchRecordResult result = await RecordAsync(server, playerA, playerB, 2, 1, ranked: false);

        Assert.Null(result.ExperienceA);
        Assert.Null(result.ExperienceB);
        Assert.Equal(0L, TotalExperience(server, playerA));
        Assert.Equal(0L, TotalExperience(server, playerB));
    }

    [Fact]
    public async Task Ranked_reward_can_be_tripled_by_ad_only_once()
    {
        using var server = new TestServer();
        AccountIdentity playerA = server.RegisterAccount("ranked-adv-a");
        AccountIdentity playerB = server.RegisterAccount("ranked-adv-b");
        MatchRecordResult match = await RecordAsync(server, playerA, playerB, 2, 1, ranked: true);
        var progress = new SinglePlayerProgressService(server.Database);

        (SinglePlayerRewardResult first, string firstCode, _) = progress.ClaimAdMultiplier(
            playerA,
            new SinglePlayerAdMultiplierRequest
            {
                rewardClaimId = match.ExperienceA.ClaimId,
                adImpressionId = "ranked-ad-1"
            });
        (SinglePlayerRewardResult second, string secondCode, _) = progress.ClaimAdMultiplier(
            playerA,
            new SinglePlayerAdMultiplierRequest
            {
                rewardClaimId = match.ExperienceA.ClaimId,
                adImpressionId = "ranked-ad-2"
            });

        Assert.Null(firstCode);
        Assert.Equal(20, first.grantedAccountExperience);
        Assert.Equal(30L, TotalExperience(server, playerA));
        Assert.Null(secondCode);
        Assert.Equal(0, second.grantedAccountExperience);
        Assert.Equal(30L, TotalExperience(server, playerA));
    }

    private static long TotalExperience(TestServer server, AccountIdentity identity) =>
        server.QueryScalar<long>($"SELECT COALESCE(account_total_experience, 0) FROM single_player_progress WHERE player_id='{identity.PlayerId}'");

    private static Task<MatchRecordResult> RecordAsync(
        TestServer server, AccountIdentity playerA, AccountIdentity playerB,
        int scoreA, int scoreB, bool ranked)
    {
        DateTime endedAt = DateTime.UtcNow;
        return server.ResultRecorder.RecordAsync(new MatchOutcome(
            playerA, playerB, Winner: scoreA > scoreB ? 0 : 1,
            ScoreA: scoreA, ScoreB: scoreB, Ranked: ranked,
            EndedReason: "score", RoomCode: $"XP-{Guid.NewGuid():N}",
            StartedAt: endedAt.AddMinutes(-2), EndedAt: endedAt));
    }
}
