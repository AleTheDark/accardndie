using System.Text.Json;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Admin;
using AccardND.Server.Progression;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Le partite giocate in una stanza (pubblica, protetta o privata) sono amichevoli:
/// esistono per giocare con chi si vuole, non per costruire un curriculum. Devono
/// restare nello storico - il pannello admin e' l'unico posto da cui si vede cosa
/// succede sul server - e non lasciare traccia in niente di quello che i giocatori
/// vedono o guadagnano.
///
/// Il confine e' anche una difesa: statistiche e quest sono la vitrina e la moneta
/// del gioco, e una stanza privata fra due complici e' la partita piu' facile da
/// produrre in serie che ci sia.
/// </summary>
public sealed class FriendlyMatchTests
{
    [Fact]
    public async Task A_friendly_match_is_recorded_in_history_and_nowhere_else()
    {
        using var server = new TestServer();
        AccountIdentity host = server.RegisterAccount("padrone-di-casa");
        AccountIdentity guest = server.RegisterAccount("ospite");

        MatchRecordResult result = await RecordAsync(server, host, guest, ranked: false);

        Assert.False(result.Ranked);
        Assert.Null(result.A);
        Assert.Null(result.B);
        Assert.Null(result.ExperienceA);
        Assert.Null(result.ExperienceB);
        Assert.Empty(result.AchievementsA);
        Assert.Empty(result.AchievementsB);

        // Lo storico e' l'unica traccia: da qui il pannello admin conta le amichevoli.
        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM match_history WHERE ranked = 0"));

        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM player_stats"));
        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM ranked_state"));
        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM player_counters"));
        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM single_player_reward_claims"));
        Assert.Equal(0, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM player_achievements WHERE unlocked_at IS NOT NULL"));
    }

    /// <summary>
    /// Un'amichevole in mezzo alle classificate non deve nemmeno spostare i numeri di
    /// quelle: e' il caso vero, perche' chi gioca in coda gioca anche con gli amici.
    /// </summary>
    [Fact]
    public async Task Friendlies_do_not_move_the_numbers_of_the_ranked_matches()
    {
        using var server = new TestServer();
        var stats = new StatsService(
            server.Database,
            new SeasonService(
                server.Database, server.Config,
                new RankedService(server.Database, server.Config),
                new UnlockService(server.Database, server.Config)));

        AccountIdentity player = server.RegisterAccount("giocatore");
        AccountIdentity rival = server.RegisterAccount("rivale");

        await RecordAsync(server, player, rival, ranked: true);   // classificata vinta
        await RecordAsync(server, rival, player, ranked: true);   // classificata persa
        await RecordAsync(server, player, rival, ranked: false);  // amichevole vinta
        await RecordAsync(server, player, rival, ranked: false);  // amichevole vinta

        PlayerStatsDto lifetime = stats.GetStats(player).lifetime;
        Assert.Equal(2, lifetime.matches);
        Assert.Equal(1, lifetime.wins);
        Assert.Equal(1, lifetime.losses);
        Assert.Equal(2, lifetime.roundsWon);
        Assert.Equal(50, lifetime.winRatePercent);
        // La striscia si e' fermata sulla sconfitta classificata: le due amichevoli vinte
        // dopo non la fanno ripartire.
        Assert.Equal(0, lifetime.currentStreak);
        Assert.Equal(1, lifetime.bestStreak);

        // Le quest della taverna contano due partite, non quattro.
        Assert.Equal(2, server.QueryScalar<int>(
            $"SELECT value FROM player_counters WHERE player_id = '{player.PlayerId}' AND counter_key = 'pvp_matches'"));

        Assert.Equal(4, server.QueryScalar<int>("SELECT COUNT(*) FROM match_history"));
    }

    /// <summary>
    /// La scheda giocatore del pannello admin e' il posto da cui si vedono: se le
    /// amichevoli non comparissero nemmeno qui, sul server sarebbero invisibili.
    /// </summary>
    [Fact]
    public async Task The_admin_dossier_counts_the_friendlies_apart_from_the_statistics()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);
        var seasons = new SeasonService(
            server.Database, server.Config, ranked, new UnlockService(server.Database, server.Config));
        var admin = new AdminService(
            server.Database, new PresenceRegistry(), seasons, ranked, new AccountEraser(server.Database));

        AccountIdentity player = server.RegisterAccount("giocatore");
        AccountIdentity rival = server.RegisterAccount("rivale");

        await RecordAsync(server, player, rival, ranked: true);
        await RecordAsync(server, player, rival, ranked: false);
        await RecordAsync(server, rival, player, ranked: false);

        JsonElement dossier = JsonSerializer.SerializeToElement(admin.GetPlayerDetail(player.PlayerId));

        JsonElement friendly = dossier.GetProperty("friendly");
        Assert.Equal(2, friendly.GetProperty("matches").GetInt32());
        Assert.Equal(1, friendly.GetProperty("wins").GetInt32());
        Assert.Equal(1, friendly.GetProperty("losses").GetInt32());

        // Gli aggregati restano quelli della sola classificata.
        Assert.Equal(1, dossier.GetProperty("lifetime").GetProperty("matches").GetInt32());
    }

    /// <summary>
    /// Le amichevoli registrate prima della regola sono ancora dentro player_stats: la
    /// migrazione le toglie. Il database di partenza si costruisce facendo scrivere al
    /// recorder anche le amichevoli, come faceva prima.
    /// </summary>
    [Fact]
    public async Task The_migration_takes_the_old_friendlies_out_of_the_statistics()
    {
        using var server = new TestServer();
        var stats = new StatsService(
            server.Database,
            new SeasonService(
                server.Database, server.Config,
                new RankedService(server.Database, server.Config),
                new UnlockService(server.Database, server.Config)));

        AccountIdentity player = server.RegisterAccount("veterano");
        AccountIdentity rival = server.RegisterAccount("rivale");

        await RecordAsync(server, player, rival, ranked: true);    // vinta
        await RecordAsync(server, rival, player, ranked: true);    // persa
        await RecordAsync(server, player, rival, ranked: false);
        await RecordAsync(server, player, rival, ranked: false);
        // Aggregati come li scriveva il recorder di prima: amichevoli comprese.
        AddOldFriendlyToStats(server, player.PlayerId, matches: 2, wins: 2, roundsWon: 4);

        PlayerStatsDto before = stats.GetStats(player).lifetime;
        Assert.Equal(4, before.matches);
        Assert.Equal(3, before.wins);

        Assert.True(FriendlyStatsCleanupMigration.RunIfNeeded(server.Database) > 0);

        PlayerStatsDto after = stats.GetStats(player).lifetime;
        Assert.Equal(2, after.matches);
        Assert.Equal(1, after.wins);
        Assert.Equal(1, after.losses);
        Assert.Equal(2, after.roundsWon);
        Assert.Equal(50, after.winRatePercent);
        // Strisce rifatte sulle sole classificate: vinta poi persa.
        Assert.Equal(0, after.currentStreak);
        Assert.Equal(1, after.bestStreak);

        // Girata una volta: al riavvio del server non ritocca niente.
        Assert.Equal(0, FriendlyStatsCleanupMigration.RunIfNeeded(server.Database));
        Assert.Equal(2, stats.GetStats(player).lifetime.matches);
    }

    /// <summary>
    /// Rimette negli aggregati il contributo che le amichevoli avevano prima della regola,
    /// in tutti gli scope come faceva il recorder (lifetime e stagione attiva).
    /// </summary>
    private static void AddOldFriendlyToStats(
        TestServer server, string playerId, int matches, int wins, int roundsWon)
    {
        int seasonId = server.QueryScalar<int>("SELECT season_id FROM seasons WHERE is_active = 1");
        foreach (string scope in new[] { "lifetime", $"season:{seasonId}" })
            server.Execute($@"
                UPDATE player_stats SET
                    matches = matches + {matches},
                    wins = wins + {wins},
                    rounds_won = rounds_won + {roundsWon},
                    current_streak = {wins},
                    best_streak = {wins}
                WHERE player_id = '{playerId}' AND scope = '{scope}'");
    }

    /// <summary>Vince sempre il giocatore A, 2-0.</summary>
    private static Task<MatchRecordResult> RecordAsync(
        TestServer server, AccountIdentity playerA, AccountIdentity playerB, bool ranked)
    {
        DateTime endedAt = DateTime.UtcNow;
        return server.ResultRecorder.RecordAsync(new MatchOutcome(
            playerA, playerB, Winner: 0, ScoreA: 2, ScoreB: 0, Ranked: ranked,
            EndedReason: "score", RoomCode: $"FRIEND-{Guid.NewGuid():N}",
            StartedAt: endedAt.AddMinutes(-3), EndedAt: endedAt));
    }
}
