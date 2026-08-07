using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Il triplicatore saltato a fine run non e' perso: la reward resta a moltiplicatore 1 e il
/// server la ripropone finche' qualcuno non guarda il video. Questi test difendono proprio
/// il caso per cui la vetrina esiste - la connessione che cade mentre il popup e' a schermo -
/// e i due modi in cui l'offerta deve sparire: riscossa, oppure scaduta.
/// </summary>
public sealed class PendingAdRewardTests
{
    [Fact]
    public void Death_reward_without_ad_stays_offered()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("pending-adv-1");
        var progress = new SinglePlayerProgressService(server.Database);
        progress.ClaimDeathReward(player, DeathRun("run-1", 250));

        SinglePlayerPendingAdRewardsData pending = progress.GetPendingAdRewards(player);

        Assert.Single(pending.rewards);
        Assert.Equal("death", pending.rewards[0].rewardType);
        Assert.Equal(25, pending.rewards[0].baseAccountExperience);
        Assert.Equal(50, pending.rewards[0].extraAccountExperience);
        Assert.Equal("chapter-2", pending.rewards[0].chapterId);
        Assert.Equal(4, pending.rewards[0].roomsCleared);
        Assert.True(pending.rewards[0].hoursLeft > 0);
    }

    [Fact]
    public void Tripled_reward_leaves_the_list()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("pending-adv-2");
        var progress = new SinglePlayerProgressService(server.Database);
        (SinglePlayerRewardResult reward, _, _) = progress.ClaimDeathReward(player, DeathRun("run-2", 250));

        (_, string errorCode, _) = progress.ClaimAdMultiplier(player, new SinglePlayerAdMultiplierRequest
        {
            rewardClaimId = reward.rewardClaimId,
            adImpressionId = "pending-ad-1"
        });

        Assert.Null(errorCode);
        Assert.Empty(progress.GetPendingAdRewards(player).rewards);
    }

    [Fact]
    public void Offer_expires_after_the_window()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("pending-adv-3");
        var progress = new SinglePlayerProgressService(server.Database);
        progress.ClaimDeathReward(player, DeathRun("run-3", 250));

        string old = DateTime.UtcNow.AddDays(-8).ToString("O");
        server.Execute(
            $"UPDATE single_player_reward_claims SET created_at='{old}' WHERE player_id='{player.PlayerId}'");

        Assert.Empty(progress.GetPendingAdRewards(player).rewards);
    }

    [Fact]
    public void Ranked_claims_stay_out_of_the_campaign_mailbox()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("pending-adv-4");
        var progress = new SinglePlayerProgressService(server.Database);
        server.Execute(
            "INSERT INTO single_player_reward_claims " +
            "(claim_id, player_id, reward_type, base_honey, base_account_experience, multiplier, created_at) " +
            $"VALUES ('pvp-claim', '{player.PlayerId}', 'pvp_ranked', 0, 10, 1, '{DateTime.UtcNow:O}')");

        Assert.Empty(progress.GetPendingAdRewards(player).rewards);
    }

    private static SinglePlayerDeathRewardRequest DeathRun(string runId, int matchExperience) => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = "chapter-2",
        stageId = "stage-1",
        roomsCleared = 4,
        enemiesDefeated = 9,
        bossesDefeated = 1,
        matchExperience = matchExperience
    };
}
