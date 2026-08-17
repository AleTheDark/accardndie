using System.Text.Json;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Admin;
using AccardND.Server.Data;
using AccardND.Server.Progression;
using AccardND.Server.Sessions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Lo storico delle run esiste per rispondere a "ha giocato o no?". Prima la riga nasceva
/// solo con la ricompensa di fine run, quindi chi mollava a meta' non compariva da nessuna
/// parte. Questi test difendono le due meta' del racconto: l'avvio da solo lascia una run
/// aperta, e la fine chiude quella stessa riga invece di aprirne una seconda.
/// </summary>
public sealed class CampaignRunHistoryTests
{
    [Fact]
    public void Started_run_is_recorded_as_open()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-history-1");
        var progress = new SinglePlayerProgressService(server.Database);

        SinglePlayerRunStartAck ack = progress.RecordRunStart(player, StartRun("run-1"));

        Assert.Equal("run-1", ack.runId);
        Assert.False(string.IsNullOrWhiteSpace(ack.startedAt));
        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at IS NULL AND started_at IS NOT NULL"));
        Assert.Equal("chapter-2", server.QueryScalar<string>("SELECT chapter_id FROM campaign_runs"));
    }

    [Fact]
    public void Death_reward_closes_the_started_run()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-history-2");
        var progress = new SinglePlayerProgressService(server.Database);
        progress.RecordRunStart(player, StartRun("run-2"));

        progress.ClaimDeathReward(player, DeathRun("run-2"));

        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM campaign_runs"));
        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM campaign_runs WHERE started_at IS NOT NULL AND ended_at IS NOT NULL"));
        Assert.Equal(4, server.QueryScalar<int>("SELECT rooms_cleared FROM campaign_runs"));
    }

    [Fact]
    public void Repeated_start_does_not_duplicate_the_run()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-history-3");
        var progress = new SinglePlayerProgressService(server.Database);

        progress.RecordRunStart(player, StartRun("run-3"));
        progress.RecordRunStart(player, StartRun("run-3"));

        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM campaign_runs"));
    }

    [Fact]
    public void Run_ended_without_a_recorded_start_still_lands_in_the_history()
    {
        // Client vecchio, o avvio perso perche' offline: la fine deve valere lo stesso.
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-history-4");
        var progress = new SinglePlayerProgressService(server.Database);

        progress.ClaimDeathReward(player, DeathRun("run-4"));

        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM campaign_runs WHERE started_at IS NULL AND ended_at IS NOT NULL"));
    }

    [Fact]
    public void Abandoned_run_stays_open_when_the_next_one_starts()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-history-5");
        var progress = new SinglePlayerProgressService(server.Database);

        progress.RecordRunStart(player, StartRun("run-5a"));
        progress.RecordRunStart(player, StartRun("run-5b"));
        progress.ClaimDeathReward(player, DeathRun("run-5b"));

        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at IS NULL"));
        Assert.Equal("run-5a", server.QueryScalar<string>(
            "SELECT client_run_ref FROM campaign_runs WHERE ended_at IS NULL"));
    }

    [Fact]
    public void Admin_panel_lists_open_and_ended_runs()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-history-6");
        var progress = new SinglePlayerProgressService(server.Database);
        AdminService admin = CreateAdmin(server);

        progress.RecordRunStart(player, StartRun("run-6a"));
        progress.RecordRunStart(player, StartRun("run-6b"));
        progress.ClaimDeathReward(player, DeathRun("run-6b"));

        JsonElement all = Serialize(admin.GetRuns("all", 50, 0));
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal(1, all.GetProperty("open").GetInt32());
        Assert.Equal(1, all.GetProperty("ended").GetInt32());
        Assert.Equal("run-history-6", all.GetProperty("runs")[0].GetProperty("username").GetString());

        JsonElement open = Serialize(admin.GetRuns("open", 50, 0));
        Assert.Equal(1, open.GetProperty("runs").GetArrayLength());
        Assert.Equal(
            JsonValueKind.Null, open.GetProperty("runs")[0].GetProperty("endedAt").ValueKind);

        JsonElement overview = Serialize(admin.GetOverview());
        Assert.Equal(2, overview.GetProperty("startedRuns24h").GetInt32());
        Assert.Equal(1, overview.GetProperty("openRuns24h").GetInt32());
        Assert.Equal(1, overview.GetProperty("totalCampaignRuns").GetInt32());

        JsonElement detail = Serialize(admin.GetPlayerDetail(player.PlayerId));
        Assert.Equal(2, detail.GetProperty("recentRuns").GetArrayLength());
        Assert.Equal(2, detail.GetProperty("campaignTotals").GetProperty("startedRuns").GetInt32());
        Assert.Equal(1, detail.GetProperty("campaignTotals").GetProperty("openRuns").GetInt32());
        Assert.Equal(1, detail.GetProperty("campaignTotals").GetProperty("runs").GetInt32());

        JsonElement series = Serialize(admin.GetTimeseries(7)).GetProperty("points");
        JsonElement today = series[series.GetArrayLength() - 1];
        Assert.Equal(2, today.GetProperty("campaignStarted").GetInt32());
        Assert.Equal(1, today.GetProperty("campaign").GetInt32());
    }

    [Fact]
    public void Admin_campaign_leaderboard_keeps_each_players_personal_record()
    {
        using var server = new TestServer();
        AccountIdentity first = server.RegisterAccount("campaign-record-a");
        AccountIdentity second = server.RegisterAccount("campaign-record-b");
        var progress = new SinglePlayerProgressService(server.Database);

        progress.ClaimDeathReward(first, DeathRun("record-a-1", 3));
        progress.ClaimDeathReward(first, DeathRun("record-a-2", 8));
        progress.ClaimDeathReward(second, DeathRun("record-b-1", 5));

        JsonElement players = Serialize(CreateAdmin(server).GetCampaignLeaderboard(100))
            .GetProperty("players");

        Assert.Equal(2, players.GetArrayLength());
        Assert.Equal("campaign-record-a", players[0].GetProperty("username").GetString());
        Assert.Equal(8, players[0].GetProperty("personalRecord").GetInt32());
        Assert.Equal(2, players[0].GetProperty("runs").GetInt32());
        Assert.Equal(1, players[0].GetProperty("position").GetInt32());
        Assert.Equal(5, players[1].GetProperty("personalRecord").GetInt32());
        Assert.Equal(2, players[1].GetProperty("position").GetInt32());
    }

    [Fact]
    public void Admin_can_delete_one_campaign_run_without_touching_the_others()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("run-delete-one");
        var progress = new SinglePlayerProgressService(server.Database);
        AdminService admin = CreateAdmin(server);

        progress.ClaimDeathReward(player, DeathRun("keep-this", 3));
        progress.ClaimDeathReward(player, DeathRun("delete-this", 8));
        JsonElement runs = Serialize(admin.GetRuns("all", 50, 0)).GetProperty("runs");
        long runId = runs[0].GetProperty("runId").GetInt64();

        Assert.True(admin.DeleteCampaignRun(runId).ok);
        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM campaign_runs"));
        Assert.Equal("keep-this", server.QueryScalar<string>(
            "SELECT client_run_ref FROM campaign_runs"));
        Assert.False(admin.DeleteCampaignRun(runId).ok);
    }

    [Fact]
    public void Game_adventure_leaderboard_returns_records_and_profile_icons()
    {
        using var server = new TestServer();
        AccountIdentity first = server.RegisterAccount("adventure-ladder-a");
        AccountIdentity second = server.RegisterAccount("adventure-ladder-b");
        var progress = new SinglePlayerProgressService(server.Database);
        progress.ClaimDeathReward(first, DeathRun("ladder-a", 9));
        progress.ClaimDeathReward(second, DeathRun("ladder-b", 4));

        using (SqliteConnection connection = server.Database.Open())
        using (SqliteCommand icon = connection.CreateCommand())
        {
            icon.CommandText = @"
                INSERT INTO profiles (player_id, selected_icon_id, updated_at)
                VALUES ($id, 'tier-gold', '2026-01-01T00:00:00Z')
                ON CONFLICT(player_id) DO UPDATE SET selected_icon_id=excluded.selected_icon_id";
            icon.Parameters.AddWithValue("$id", first.PlayerId);
            icon.ExecuteNonQuery();
        }

        AdventureLeaderboardData data =
            new RankedService(server.Database, server.Config).GetAdventureLeaderboard(50);

        Assert.Equal(2, data.entries.Length);
        Assert.Equal(1, data.entries[0].rank);
        Assert.Equal(9, data.entries[0].roomsCleared);
        Assert.Equal("tier-gold", data.entries[0].selectedIconId);
        Assert.Equal(2, data.entries[1].rank);
    }

    [Fact]
    public void Adventure_personal_record_prioritizes_chapter_before_room()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("chapter-before-room");
        var progress = new SinglePlayerProgressService(server.Database);

        progress.ClaimDeathReward(player, DeathRun("chapter-one-far", 15, "chapter-1"));
        progress.ClaimDeathReward(player, DeathRun("chapter-two-near", 5, "chapter-2"));

        AdventureLeaderboardEntry entry =
            new RankedService(server.Database, server.Config).GetAdventureLeaderboard(50).entries[0];

        Assert.Equal(2, entry.chapterNumber);
        Assert.Equal(5, entry.roomsCleared);
    }

    [Fact]
    public void Admin_campaign_leaderboard_prioritizes_chapter_before_room()
    {
        using var server = new TestServer();
        AccountIdentity chapterOne = server.RegisterAccount("chapter-one-far");
        AccountIdentity chapterTwo = server.RegisterAccount("chapter-two-near");
        var progress = new SinglePlayerProgressService(server.Database);

        progress.ClaimDeathReward(
            chapterOne, DeathRun("chapter-one-far", 15, "chapter-1"));
        progress.ClaimDeathReward(
            chapterTwo, DeathRun("chapter-two-near", 4, "chapter-2"));

        JsonElement players = Serialize(CreateAdmin(server).GetCampaignLeaderboard(100))
            .GetProperty("players");

        Assert.Equal(chapterTwo.PlayerId, players[0].GetProperty("playerId").GetString());
        Assert.Equal(2, players[0].GetProperty("chapterNumber").GetInt32());
        Assert.Equal(4, players[0].GetProperty("personalRecord").GetInt32());
        Assert.Equal(1, players[0].GetProperty("position").GetInt32());
        Assert.Equal(chapterOne.PlayerId, players[1].GetProperty("playerId").GetString());
        Assert.Equal(2, players[1].GetProperty("position").GetInt32());
    }

    [Fact]
    public void Old_database_keeps_its_runs_and_accepts_open_ones()
    {
        // Il database vero arriva dalla versione in cui ended_at era NOT NULL: la
        // migrazione ricostruisce la tabella, e la prova che serve e' che lo storico
        // gia' scritto sopravviva e che da subito si possa aprire una run senza fine.
        string path = Path.Combine(Path.GetTempPath(), $"accardnd-migration-{Guid.NewGuid():N}.db");
        try
        {
            // Niente pool nemmeno sulla connessione "legacy": ClearAllPools e' globale al
            // processo e strapperebbe le connessioni ai test in parallelo.
            using (var legacy = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                legacy.Open();
                using SqliteCommand create = legacy.CreateCommand();
                create.CommandText = @"
                    CREATE TABLE campaign_runs (
                        run_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        player_id        TEXT NOT NULL,
                        client_run_ref   TEXT,
                        mode             TEXT,
                        chapter_id       TEXT,
                        stage_id         TEXT,
                        rooms_cleared    INTEGER NOT NULL DEFAULT 0,
                        enemies_defeated INTEGER NOT NULL DEFAULT 0,
                        bosses_defeated  INTEGER NOT NULL DEFAULT 0,
                        honey_reward     INTEGER NOT NULL DEFAULT 0,
                        ended_at         TEXT NOT NULL
                    );
                    INSERT INTO campaign_runs (player_id, client_run_ref, rooms_cleared, ended_at)
                    VALUES ('vecchio', 'run-storica', 6, '2026-01-01T00:00:00.0000000Z');";
                create.ExecuteNonQuery();
            }
            var config = new ServerConfig { DatabaseFilePath = path, DatabasePooling = false };
            var database = new AccardDatabase(config);

            using SqliteConnection connection = database.Open();
            using SqliteCommand check = connection.CreateCommand();
            check.CommandText = @"
                INSERT INTO campaign_runs (player_id, client_run_ref, started_at)
                VALUES ('nuovo', 'run-aperta', '2026-02-01T00:00:00.0000000Z');
                SELECT COUNT(*) FROM campaign_runs";
            Assert.Equal(2, Convert.ToInt32(check.ExecuteScalar()));

            using SqliteCommand survived = connection.CreateCommand();
            survived.CommandText =
                "SELECT rooms_cleared FROM campaign_runs WHERE client_run_ref = 'run-storica'";
            Assert.Equal(6, Convert.ToInt32(survived.ExecuteScalar()));
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    private static AdminService CreateAdmin(TestServer server)
    {
        var ranked = new RankedService(server.Database, server.Config);
        var seasons = new SeasonService(
            server.Database, server.Config, ranked, new UnlockService(server.Database, server.Config));
        return new AdminService(
            server.Database, new PresenceRegistry(), seasons, ranked,
            new AccountEraser(server.Database));
    }

    /// <summary>Il pannello legge JSON: si ispeziona quello che vedra' davvero la pagina.</summary>
    private static JsonElement Serialize(object payload) => JsonSerializer.SerializeToElement(payload);

    private static SinglePlayerRunStartRequest StartRun(string runId) => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = "chapter-2",
        stageId = "climbing"
    };

    private static SinglePlayerDeathRewardRequest DeathRun(
        string runId, int roomsCleared = 4, string chapterId = "chapter-2") => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = chapterId,
        stageId = "climbing",
        roomsCleared = roomsCleared,
        enemiesDefeated = 7,
        matchExperience = 250
    };
}
