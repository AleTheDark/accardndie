using AccardND.GameCore;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La progressione dell'account: curva di esperienza, moltiplicatore di capitolo e punti
/// talento. Sono le tre cose che prima erano piatte - 100 exp a livello per sempre, ogni
/// capitolo pagato uguale, cinque vasetti di miele a livello - e i test qui sotto servono
/// a impedire che tornino piatte per distrazione.
/// </summary>
public sealed class AccountProgressionTests
{
    [Fact]
    public void The_cost_of_a_level_grows_with_the_level()
    {
        Assert.Equal(100, AccountLevelCurve.ExperienceToNext(1));
        Assert.Equal(200, AccountLevelCurve.ExperienceToNext(5));
        Assert.Equal(325, AccountLevelCurve.ExperienceToNext(10));
        Assert.Equal(1325, AccountLevelCurve.ExperienceToNext(50));
    }

    [Fact]
    public void Crossing_several_levels_pays_each_threshold_in_turn()
    {
        // 100 per il primo livello, 125 per il secondo: 225 in tutto portano a livello 3
        // esatto, con la barra a zero. Con la vecchia soglia fissa sarebbero stati due
        // livelli e 25 di avanzo, che e' precisamente la differenza che la curva introduce.
        AccountLevelProgress progress = AccountLevelCurve.Apply(1, 0, 0, 225);

        Assert.Equal(3, progress.Level);
        Assert.Equal(0, progress.Experience);
        Assert.Equal(225, progress.TotalExperience);
        Assert.Equal(2, progress.LevelsGained);
        Assert.Equal(150, progress.ExperienceToNextLevel);
    }

    [Fact]
    public void Experience_below_the_threshold_only_fills_the_bar()
    {
        AccountLevelProgress progress = AccountLevelCurve.Apply(4, 40, 500, 30);

        Assert.Equal(4, progress.Level);
        Assert.Equal(70, progress.Experience);
        Assert.Equal(530, progress.TotalExperience);
        Assert.Equal(0, progress.LevelsGained);
    }

    [Fact]
    public void A_later_chapter_pays_more_account_experience_for_the_same_run()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity novizio = server.RegisterAccount("capitolo-primo");
        AccountIdentity veterano = server.RegisterAccount("capitolo-settimo");

        (SinglePlayerRewardResult first, _, _) =
            progress.ClaimDeathReward(novizio, DeathRun("run-c1", "chapter-1", 1000));
        (SinglePlayerRewardResult last, _, _) =
            progress.ClaimDeathReward(veterano, DeathRun("run-c7", "chapter-7", 1000));

        Assert.Equal(100, first.grantedAccountExperience);
        Assert.Equal(250, last.grantedAccountExperience);
    }

    [Fact]
    public void A_run_outside_the_campaign_is_worth_the_base_rate()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("fuori-campagna");

        (SinglePlayerRewardResult reward, _, _) =
            progress.ClaimDeathReward(player, DeathRun("run-free", "free-run", 1000));

        Assert.Equal(100, reward.grantedAccountExperience);
    }

    [Fact]
    public void The_ceiling_applies_to_run_experience_so_the_multiplier_can_exceed_it()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("tetto");

        // 9000 di run vengono tagliate a 5000, cioe' 500 di base. Il 250% del settimo
        // capitolo si applica dopo il taglio, quindi supera le 500: se il tetto valesse sul
        // risultato, il moltiplicatore non esisterebbe oltre le 5000.
        (SinglePlayerRewardResult reward, _, _) =
            progress.ClaimDeathReward(player, DeathRun("run-tetto", "chapter-7", 9000));

        Assert.Equal(1250, reward.grantedAccountExperience);
    }

    [Fact]
    public void Levels_pay_talent_points_and_round_levels_pay_more()
    {
        // Dal livello 1 al 4: tre livelli, nessun livello tondo.
        Assert.Equal(3, SinglePlayerProgressService.TalentPointsForLevels(1, 4));
        // Il quinto livello aggiunge il suo punto piu' il bonus.
        Assert.Equal(6, SinglePlayerProgressService.TalentPointsForLevels(1, 5));
        // Fino al decimo: nove livelli piu' due bonus.
        Assert.Equal(13, SinglePlayerProgressService.TalentPointsForLevels(1, 10));
    }

    [Fact]
    public void Claiming_levels_pays_talent_points_instead_of_honey()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("livellatore");

        // 1000 di run al capitolo 1 fanno 100 di exp account: esattamente un livello.
        progress.ClaimDeathReward(player, DeathRun("run-livello", "chapter-1", 1000));
        (SinglePlayerRewardResult claimed, _, _) = progress.ClaimLevelRewards(player);

        Assert.Equal(1, claimed.levelsGained);
        Assert.Equal(1, claimed.grantedTalentPoints);
        Assert.Equal(0, claimed.grantedHoney);
        Assert.Equal(1, claimed.progress.talentPoints);
        Assert.Equal(2, claimed.progress.accountLevel);
        // Il miele resta tutto delle quest giornaliere.
        Assert.Equal(0, claimed.progress.honey);
    }

    [Fact]
    public void The_first_kill_of_a_chapter_boss_pays_talent_points()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("cacciatore-boss");
        progress.ClaimTutorialReward(player, new SinglePlayerTutorialRewardRequest());

        (SinglePlayerProgressData first, _, _) = progress.ClearChapter(
            player, new SinglePlayerClearChapterRequest { bossId = "trentor" });

        Assert.Equal(3, first.talentPoints);
        Assert.Contains("chapter-1", first.clearedChapters);
    }

    [Fact]
    public void Replaying_a_chapter_does_not_pay_the_boss_points_again()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("ripetitore");
        progress.ClaimTutorialReward(player, new SinglePlayerTutorialRewardRequest());

        progress.ClearChapter(player, new SinglePlayerClearChapterRequest { bossId = "trentor" });
        (SinglePlayerProgressData again, _, _) = progress.ClearChapter(
            player, new SinglePlayerClearChapterRequest { bossId = "trentor" });

        // Rigiocare un capitolo continua a dare esperienza e contatori, ma non i punti: se
        // li ripagasse, il modo piu' veloce di riempire l'albero sarebbe ribattere per
        // sempre il boss piu' facile.
        Assert.Equal(3, again.talentPoints);
    }

    [Fact]
    public void Every_chapter_boss_pays_its_own_first_kill()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("scalatore");
        progress.ClaimTutorialReward(player, new SinglePlayerTutorialRewardRequest());

        progress.ClearChapter(player, new SinglePlayerClearChapterRequest { bossId = "trentor" });
        (SinglePlayerProgressData after, _, _) = progress.ClearChapter(
            player, new SinglePlayerClearChapterRequest { bossId = "boss-bragus" });

        Assert.Equal(6, after.talentPoints);
    }

    [Fact]
    public void Claiming_nothing_pays_nothing()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("nulla-da-riscuotere");

        (SinglePlayerRewardResult claimed, _, _) = progress.ClaimLevelRewards(player);

        Assert.Equal(0, claimed.grantedTalentPoints);
        Assert.Equal(0, claimed.progress.talentPoints);
    }

    [Fact]
    public void The_progress_bar_denominator_follows_the_curve_even_on_old_rows()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("riga-vecchia");
        progress.GetProgress(player);

        // Una riga scritta prima della curva: livello alto ma soglia ferma a 100.
        server.Execute(
            "UPDATE single_player_progress SET account_level = 12, " +
            $"account_experience_to_next_level = 100 WHERE player_id = '{player.PlayerId}'");

        SinglePlayerProgressData data = progress.GetProgress(player);

        Assert.Equal(12, data.accountLevel);
        Assert.Equal(375, data.accountExperienceToNextLevel);
    }

    [Fact]
    public void Existing_levels_are_backfilled_into_talent_points_once()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("veterano-senza-punti");
        progress.GetProgress(player);
        server.Execute(
            $"UPDATE single_player_progress SET account_level = 10 WHERE player_id = '{player.PlayerId}'");

        Assert.Equal(1, TalentPointsBackfillMigration.RunIfNeeded(server.Database));
        Assert.Equal(13, progress.GetProgress(player).talentPoints);

        // Idempotente: al riavvio successivo non paga una seconda volta.
        Assert.Equal(0, TalentPointsBackfillMigration.RunIfNeeded(server.Database));
        Assert.Equal(13, progress.GetProgress(player).talentPoints);
    }

    private static SinglePlayerDeathRewardRequest DeathRun(
        string runId, string chapterId, int matchExperience) => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = chapterId,
        stageId = "stage-1",
        roomsCleared = 6,
        enemiesDefeated = 12,
        bossesDefeated = 1,
        matchExperience = matchExperience
    };
}
