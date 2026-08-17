using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La classifica che finisce sulla Hall of Fame pubblica (/hall-of-fame).
///
/// E' una pagina di sola lettura, quindi l'unica cosa che puo' andare storta e'
/// dire un numero falso su qualcuno - e qui i numeri li legge chiunque, anche chi
/// non gioca.
/// </summary>
public sealed class HallOfFameLadderTests
{
    [Fact]
    public async Task The_ladder_is_ordered_by_rank_and_counts_only_ranked_matches()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);

        AccountIdentity winner = server.RegisterAccount("campione");
        AccountIdentity loser = server.RegisterAccount("sfidante");

        await RecordAsync(server, winner, loser, winnerIsA: true, isRanked: true);
        await RecordAsync(server, winner, loser, winnerIsA: true, isRanked: true);
        await RecordAsync(server, loser, winner, winnerIsA: true, isRanked: true);

        // Un'amichevole non deve contare: resta nello storico per il pannello admin, ma
        // in una graduatoria di classificate non c'entra niente.
        await RecordAsync(server, winner, loser, winnerIsA: true, isRanked: false);

        IReadOnlyList<LeaderboardRow> ladder = ranked.GetLeaderboard(SeasonId(server), 100);

        Assert.Equal(2, ladder.Count);
        Assert.Equal(2, ranked.CountRanked(SeasonId(server)));

        LeaderboardRow first = ladder[0];
        Assert.Equal(winner.PlayerId, first.PlayerId);
        Assert.Equal("campione", first.Username);
        Assert.Equal(2, first.Wins);
        Assert.Equal(1, first.Losses);

        LeaderboardRow second = ladder[1];
        Assert.Equal(loser.PlayerId, second.PlayerId);
        Assert.Equal(1, second.Wins);
        Assert.Equal(2, second.Losses);
    }

    /// <summary>
    /// Cancellare un account porta via anche le sue partite. Chi resta deve
    /// perdere quelle vittorie, non vedersele trasformare in sconfitte: e' il
    /// motivo per cui le sconfitte si contano, invece di ricavarle sottraendo le
    /// vittorie dalle partite giocate.
    /// </summary>
    [Fact]
    public async Task Erasing_an_opponent_does_not_invent_defeats_for_the_others()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);
        var eraser = new AccountEraser(server.Database);

        AccountIdentity survivor = server.RegisterAccount("rimasto");
        AccountIdentity leaving = server.RegisterAccount("andato");

        await RecordAsync(server, survivor, leaving, winnerIsA: true, isRanked: true);
        await RecordAsync(server, survivor, leaving, winnerIsA: true, isRanked: true);
        await RecordAsync(server, leaving, survivor, winnerIsA: true, isRanked: true);

        (bool ok, string error) = eraser.DeletePlayer(leaving.PlayerId);
        Assert.True(ok, error);

        LeaderboardRow row = Assert.Single(ranked.GetLeaderboard(SeasonId(server), 100));
        Assert.Equal(survivor.PlayerId, row.PlayerId);
        Assert.Equal(0, row.Wins);
        Assert.Equal(0, row.Losses);
    }

    /// <summary>
    /// Il soft reset di inizio stagione porta avanti una riga per ogni classificato
    /// della stagione precedente. Chi non ha ancora rigiocato non e' in classifica:
    /// altrimenti la nuova stagione nascerebbe con una graduatoria gia' fatta, e in
    /// testa ci sarebbe chi non ha ancora tirato un dado.
    /// </summary>
    [Fact]
    public async Task The_new_season_starts_empty_even_if_everyone_carries_over()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);
        var unlocks = new UnlockService(server.Database, server.Config);
        var seasons = new SeasonService(server.Database, server.Config, ranked, unlocks);

        // Registratore che condivide questa SeasonService: quello di TestServer ne ha
        // una sua, e dopo il cambio stagione scriverebbe ancora in quella vecchia.
        var recorder = new MatchResultRecorder(
            server.Database, seasons, ranked, unlocks,
            new AchievementService(server.Database, ranked, unlocks),
            NullLogger<MatchResultRecorder>.Instance);

        AccountIdentity veteran = server.RegisterAccount("veterano");
        AccountIdentity rival = server.RegisterAccount("rivale");
        await Record(recorder, veteran, rival, winnerIsA: true, isRanked: true);

        server.Execute(
            "UPDATE seasons SET ends_at='" + DateTime.UtcNow.AddDays(-1).ToString("O")
            + "' WHERE is_active=1");
        Assert.True(seasons.RolloverIfDue(DateTime.UtcNow));

        Assert.Empty(ranked.GetLeaderboard(seasons.ActiveSeasonId, 100));
        Assert.Equal(0, ranked.CountRanked(seasons.ActiveSeasonId));
        Assert.Equal((0, 0), ranked.GetGlobalStanding(veteran.PlayerId, seasons.ActiveSeasonId));

        // Una partita, e la stagione nuova ha la sua classifica.
        await Record(recorder, veteran, rival, winnerIsA: true, isRanked: true);
        Assert.Equal(2, ranked.CountRanked(seasons.ActiveSeasonId));
        Assert.Equal((1, 2), ranked.GetGlobalStanding(veteran.PlayerId, seasons.ActiveSeasonId));
    }

    [Fact]
    public void An_empty_season_gives_an_empty_ladder_and_no_players()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);

        Assert.Empty(ranked.GetLeaderboard(SeasonId(server), 100));
        Assert.Equal(0, ranked.CountRanked(SeasonId(server)));
    }

    /// <summary>La stagione attiva la apre SeasonService al primo avvio del server.</summary>
    private static int SeasonId(TestServer server) =>
        new SeasonService(
            server.Database, server.Config,
            new RankedService(server.Database, server.Config),
            new UnlockService(server.Database, server.Config)).ActiveSeasonId;

    private static Task<MatchRecordResult> RecordAsync(
        TestServer server, AccountIdentity playerA, AccountIdentity playerB,
        bool winnerIsA, bool isRanked) =>
        Record(server.ResultRecorder, playerA, playerB, winnerIsA, isRanked);

    private static Task<MatchRecordResult> Record(
        MatchResultRecorder recorder, AccountIdentity playerA, AccountIdentity playerB,
        bool winnerIsA, bool isRanked)
    {
        DateTime endedAt = DateTime.UtcNow;
        return recorder.RecordAsync(new MatchOutcome(
            playerA, playerB, Winner: winnerIsA ? 0 : 1,
            ScoreA: winnerIsA ? 2 : 0, ScoreB: winnerIsA ? 0 : 2, Ranked: isRanked,
            EndedReason: "score", RoomCode: $"HOF-{Guid.NewGuid():N}",
            StartedAt: endedAt.AddMinutes(-2), EndedAt: endedAt));
    }
}
