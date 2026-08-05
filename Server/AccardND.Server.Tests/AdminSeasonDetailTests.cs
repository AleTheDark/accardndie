using System.Text.Json;
using AccardND.Server.Accounts;
using AccardND.Server.Admin;
using AccardND.Server.Progression;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La classifica di stagione del pannello admin: chi ha giocato, in che ordine e
/// con quali vittorie. Il database e' vero, quindi la query SQL viene eseguita
/// davvero (CTE comprese) invece di essere solo compilata.
/// </summary>
public sealed class AdminSeasonDetailTests
{
    [Fact]
    public async Task Season_detail_ranks_players_and_reports_win_rate()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);
        var seasons = new SeasonService(
            server.Database, server.Config, ranked, new UnlockService(server.Database, server.Config));
        var admin = new AdminService(
            server.Database, new PresenceRegistry(), seasons, ranked,
            new AccountEraser(server.Database));

        AccountIdentity winner = server.RegisterAccount("vincitore");
        AccountIdentity loser = server.RegisterAccount("perdente");
        AccountIdentity casual = server.RegisterAccount("amichevole");

        // Due ranked vinte dallo stesso giocatore: la ladder si separa.
        await RecordAsync(server, winner, loser, winnerIndex: 0, isRanked: true);
        await RecordAsync(server, winner, loser, winnerIndex: 0, isRanked: true);
        // Un'amichevole: entra nel conteggio partite ma non nella classifica.
        await RecordAsync(server, casual, loser, winnerIndex: 1, isRanked: false);

        JsonElement detail = Serialize(admin.GetSeasonDetail(seasons.ActiveSeasonId));
        JsonElement players = detail.GetProperty("players");

        Assert.Equal(3, players.GetArrayLength());
        Assert.Equal(3, detail.GetProperty("summary").GetProperty("players").GetInt32());
        Assert.Equal(2, detail.GetProperty("summary").GetProperty("rankedPlayers").GetInt32());
        Assert.Equal(3, detail.GetProperty("summary").GetProperty("matches").GetInt32());
        Assert.Equal(2, detail.GetProperty("summary").GetProperty("rankedMatches").GetInt32());

        JsonElement first = players[0];
        Assert.Equal("vincitore", first.GetProperty("username").GetString());
        Assert.Equal(1, first.GetProperty("rank").GetInt32());
        Assert.True(first.GetProperty("ranked").GetBoolean());
        Assert.Equal(2, first.GetProperty("wins").GetInt32());
        Assert.Equal(0, first.GetProperty("losses").GetInt32());
        Assert.Equal(100, first.GetProperty("winRatePercent").GetInt32());

        JsonElement second = players[1];
        Assert.Equal("perdente", second.GetProperty("username").GetString());
        Assert.Equal(2, second.GetProperty("rank").GetInt32());
        // Tre partite giocate (due ranked perse, un'amichevole vinta): l'aggregato di
        // stagione conta tutto, games_played della ladder solo le ranked.
        Assert.Equal(3, second.GetProperty("matches").GetInt32());
        Assert.Equal(2, second.GetProperty("rankedGames").GetInt32());
        Assert.Equal(33, second.GetProperty("winRatePercent").GetInt32());

        // Chi ha giocato solo amichevoli chiude la lista senza posizione ne' tier.
        JsonElement last = players[2];
        Assert.Equal("amichevole", last.GetProperty("username").GetString());
        Assert.Equal(0, last.GetProperty("rank").GetInt32());
        Assert.False(last.GetProperty("ranked").GetBoolean());
        Assert.Equal(1, last.GetProperty("matches").GetInt32());
    }

    [Fact]
    public void Unknown_season_has_no_detail()
    {
        using var server = new TestServer();
        var ranked = new RankedService(server.Database, server.Config);
        var seasons = new SeasonService(
            server.Database, server.Config, ranked, new UnlockService(server.Database, server.Config));
        var admin = new AdminService(
            server.Database, new PresenceRegistry(), seasons, ranked,
            new AccountEraser(server.Database));

        Assert.Null(admin.GetSeasonDetail(9999));
    }

    private static Task RecordAsync(
        TestServer server, AccountIdentity a, AccountIdentity b, int winnerIndex, bool isRanked)
    {
        DateTime endedAt = DateTime.UtcNow;
        return server.ResultRecorder.RecordAsync(new MatchOutcome(
            a, b, winnerIndex,
            ScoreA: winnerIndex == 0 ? 3 : 1,
            ScoreB: winnerIndex == 0 ? 1 : 3,
            Ranked: isRanked,
            EndedReason: "score",
            RoomCode: "TEST",
            StartedAt: endedAt.AddMinutes(-5),
            EndedAt: endedAt));
    }

    /// <summary>
    /// Il pannello legge JSON: i tipi anonimi del servizio si ispezionano allo stesso
    /// modo in cui li vedra' la pagina.
    /// </summary>
    private static JsonElement Serialize(object payload) =>
        JsonSerializer.SerializeToElement(payload);
}
