using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using AccardND.Server.Progression;
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
    public void Old_database_keeps_its_runs_and_accepts_open_ones()
    {
        // Il database vero arriva dalla versione in cui ended_at era NOT NULL: la
        // migrazione ricostruisce la tabella, e la prova che serve e' che lo storico
        // gia' scritto sopravviva e che da subito si possa aprire una run senza fine.
        string path = Path.Combine(Path.GetTempPath(), $"accardnd-migration-{Guid.NewGuid():N}.db");
        try
        {
            using (var legacy = new SqliteConnection($"Data Source={path}"))
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
            SqliteConnection.ClearAllPools();

            var config = new ServerConfig { DatabaseFilePath = path };
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
            SqliteConnection.ClearAllPools();
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    private static SinglePlayerRunStartRequest StartRun(string runId) => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = "chapter-2",
        stageId = "climbing"
    };

    private static SinglePlayerDeathRewardRequest DeathRun(string runId) => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = "chapter-2",
        stageId = "climbing",
        roomsCleared = 4,
        enemiesDefeated = 7,
        matchExperience = 250
    };
}
