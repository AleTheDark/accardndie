using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Il margine di una classificata pesa sull'MMR: chi vince 2-0 sale piu' di chi
/// vince 2-1, e chi perde 1-2 scende meno di chi perde 0-2. Lo scambio resta a
/// somma zero fra i due giocatori.
/// </summary>
public sealed class RankedMarginTests
{
    [Fact]
    public async Task Clean_sweep_moves_more_than_a_close_match()
    {
        using var server = new TestServer();

        MatchRecordResult sweep = await RecordAsync(server, "margin-sweep", 2, 0);
        MatchRecordResult close = await RecordAsync(server, "margin-close", 2, 1);

        Assert.True(sweep.A.LpDelta > close.A.LpDelta,
            $"2-0 dovrebbe dare piu' LP di 2-1 ({sweep.A.LpDelta} vs {close.A.LpDelta}).");
        Assert.True(close.B.LpDelta > sweep.B.LpDelta,
            $"1-2 dovrebbe togliere meno LP di 0-2 ({close.B.LpDelta} vs {sweep.B.LpDelta}).");
        Assert.True(close.A.LpDelta > 0);
        Assert.True(close.B.LpDelta < 0);
    }

    [Fact]
    public async Task Margin_keeps_the_exchange_symmetric()
    {
        using var server = new TestServer();

        MatchRecordResult sweep = await RecordAsync(server, "margin-sym-sweep", 2, 0);
        MatchRecordResult close = await RecordAsync(server, "margin-sym-close", 2, 1);

        // Stesso MMR di partenza per tutti: quello che il vincitore prende e' quello
        // che il perdente lascia, margine compreso.
        Assert.Equal(sweep.A.LpDelta, -sweep.B.LpDelta);
        Assert.Equal(close.A.LpDelta, -close.B.LpDelta);
    }

    [Fact]
    public async Task Forfeit_without_rounds_played_counts_full()
    {
        using var server = new TestServer();

        MatchRecordResult sweep = await RecordAsync(server, "margin-sweep-ref", 2, 0);
        MatchRecordResult forfeit = await RecordAsync(server, "margin-forfeit", 0, 0, winner: 0);

        Assert.Equal(sweep.A.LpDelta, forfeit.A.LpDelta);
        Assert.Equal(sweep.B.LpDelta, forfeit.B.LpDelta);
    }

    private static Task<MatchRecordResult> RecordAsync(
        TestServer server, string prefix, int scoreA, int scoreB, int? winner = null)
    {
        AccountIdentity playerA = server.RegisterAccount($"{prefix}-a");
        AccountIdentity playerB = server.RegisterAccount($"{prefix}-b");
        DateTime endedAt = DateTime.UtcNow;
        return server.ResultRecorder.RecordAsync(new MatchOutcome(
            playerA, playerB, Winner: winner ?? (scoreA > scoreB ? 0 : 1),
            ScoreA: scoreA, ScoreB: scoreB, Ranked: true,
            EndedReason: winner.HasValue ? "forfeit" : "score",
            RoomCode: $"MARGIN-{Guid.NewGuid():N}",
            StartedAt: endedAt.AddMinutes(-2), EndedAt: endedAt));
    }
}
