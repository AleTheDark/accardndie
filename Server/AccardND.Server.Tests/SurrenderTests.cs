using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Match;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La resa. Chi molla perde davvero — riga a storico e rating come una sconfitta
/// qualsiasi — e l'avversario deve capire perché ha vinto senza aver giocato.
/// </summary>
public sealed class SurrenderTests
{
    private sealed record LiveMatch(
        Room Room,
        ClientConnection Host,
        ClientConnection Guest,
        FakeWebSocket HostSocket,
        FakeWebSocket GuestSocket,
        AccountIdentity HostIdentity,
        AccountIdentity GuestIdentity);

    private static async Task<LiveMatch> StartMatchAsync(TestServer server)
    {
        AccountIdentity hostIdentity = server.RegisterAccount("ospitante");
        AccountIdentity guestIdentity = server.RegisterAccount("sfidante");
        var hostSocket = new FakeWebSocket();
        var guestSocket = new FakeWebSocket();
        var host = new ClientConnection(hostSocket) { Identity = hostIdentity };
        var guest = new ClientConnection(guestSocket) { Identity = guestIdentity };

        var rooms = new RoomManager();
        Room room = rooms.Create(host, TestServer.BuildLoadout());
        Assert.True(rooms.TryJoin(room.Code, guest, TestServer.BuildLoadout(), out _));
        room.Session = new MatchSession(room, server.Config, server.ResultRecorder);
        await room.Session.StartAsync(CancellationToken.None);

        return new LiveMatch(room, host, guest, hostSocket, guestSocket, hostIdentity, guestIdentity);
    }

    [Fact]
    public async Task Surrender_EndsTheMatchWithTheOpponentAsWinner()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        Assert.True(await match.Room.Session.ForfeitAsync(match.Host, CancellationToken.None));

        Assert.True(match.Room.Session.IsFinished);
        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM match_history WHERE ended_reason = 'surrender'"));
        // Winner 1 = l'ospite: chi si arrende è il giocatore 0.
        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT winner FROM match_history WHERE ended_reason = 'surrender'"));
    }

    [Fact]
    public async Task Surrender_TellsBothPlayersHowItEnded()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        await match.Room.Session.ForfeitAsync(match.Host, CancellationToken.None);

        var loser = ClientConnection.ParsePayload<MatchResultData>(
            Assert.Single(match.HostSocket.SentOfType(MessageTypes.MatchResult)));
        var winner = ClientConnection.ParsePayload<MatchResultData>(
            Assert.Single(match.GuestSocket.SentOfType(MessageTypes.MatchResult)));

        Assert.False(loser.youWon);
        Assert.True(winner.youWon);
        // È il motivo a far scrivere "il tuo avversario si è arreso" nel riepilogo.
        Assert.Equal("surrender", loser.endedReason);
        Assert.Equal("surrender", winner.endedReason);
    }

    [Fact]
    public async Task Surrender_IsAnnouncedOnTheBoardBeforeTheMatchCloses()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        await match.Room.Session.ForfeitAsync(match.Host, CancellationToken.None);

        MatchEventDto forfeited = match.GuestSocket.SentOfType(MessageTypes.MatchEvent)
            .Select(ClientConnection.ParsePayload<MatchEventDto>)
            .FirstOrDefault(dto => dto.type == "MatchForfeited");
        Assert.NotNull(forfeited);
        Assert.Equal(0, forfeited.player);
        Assert.Equal(1, forfeited.winner);
        Assert.Equal("surrender", forfeited.reason);
    }

    [Fact]
    public async Task Surrender_WorksWhileTheOpponentIsReconnecting()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        // Partita in pausa perché l'ospite è caduto: chi resta non deve restare
        // inchiodato al tavolo fino allo scadere della grazia.
        Assert.True(await match.Room.Session.PauseForReconnectAsync(match.Guest, 60));
        Assert.True(await match.Room.Session.ForfeitAsync(match.Host, CancellationToken.None));

        Assert.True(match.Room.Session.IsFinished);
        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT winner FROM match_history WHERE ended_reason = 'surrender'"));
    }

    [Fact]
    public async Task ASecondSurrender_ChangesNothing()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        Assert.True(await match.Room.Session.ForfeitAsync(match.Host, CancellationToken.None));
        Assert.False(await match.Room.Session.ForfeitAsync(match.Guest, CancellationToken.None));

        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM match_history"));
        Assert.Equal(1, server.QueryScalar<int>("SELECT winner FROM match_history"));
    }

    [Fact]
    public async Task ASurrenderFromSomeoneElse_IsIgnored()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);
        var intruder = new ClientConnection(new FakeWebSocket())
        {
            Identity = server.RegisterAccount("passante")
        };

        Assert.False(await match.Room.Session.ForfeitAsync(intruder, CancellationToken.None));

        Assert.False(match.Room.Session.IsFinished);
        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM match_history"));
    }
}
