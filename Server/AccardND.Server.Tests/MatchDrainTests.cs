using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Match;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Cosa succede ai match vivi quando il processo si ferma. Le stanze stanno in
/// memoria: senza il drenaggio ogni deploy farebbe sparire le partite in corso
/// senza lasciarne traccia, e il giocatore resterebbe con un tavolo muto.
/// </summary>
public sealed class MatchDrainTests
{
    private sealed record LiveMatch(
        TestServer Server, RoomManager Rooms, Room Room, FakeWebSocket HostSocket, FakeWebSocket GuestSocket);

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

        return new LiveMatch(server, rooms, room, hostSocket, guestSocket);
    }

    [Fact]
    public async Task Drain_RecordsOpenMatchesAsUnfinishedInsteadOfLosingThem()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        await match.Rooms.DrainMatchesAsync();

        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM match_history WHERE ended_reason = 'server_shutdown'"));
        // Winner -1: la partita resta nello storico ma non muove il rating di nessuno.
        Assert.Equal(-1, server.QueryScalar<int>(
            "SELECT winner FROM match_history WHERE ended_reason = 'server_shutdown'"));
    }

    [Fact]
    public async Task Drain_TellsBothPlayersHowTheMatchEnded()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        await match.Rooms.DrainMatchesAsync();

        foreach (FakeWebSocket socket in new[] { match.HostSocket, match.GuestSocket })
        {
            Envelope result = Assert.Single(socket.SentOfType(MessageTypes.MatchResult));
            var data = ClientConnection.ParsePayload<MatchResultData>(result);
            Assert.Equal("server_shutdown", data.endedReason);
            Assert.False(data.youWon);
        }
    }

    [Fact]
    public async Task Drain_EmptiesTheRoomsSoNothingIsLeftHanging()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        await match.Rooms.DrainMatchesAsync();

        Assert.Empty(match.Rooms.ListOpen(10));
        Assert.Null(match.Room.Host.CurrentRoom);
        Assert.Null(match.Room.Guest.CurrentRoom);
    }

    [Fact]
    public async Task Drain_OnAnAlreadyRecordedMatch_DoesNotWriteASecondRow()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);

        await match.Room.Session.RecordServerShutdownAsync();
        await match.Room.Session.RecordServerShutdownAsync();
        await match.Rooms.DrainMatchesAsync();

        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM match_history"));
    }

    [Fact]
    public async Task Drain_WithNoMatchStarted_ClosesTheRoomWithoutInventingAResult()
    {
        using var server = new TestServer();
        AccountIdentity identity = server.RegisterAccount("solitario");
        var socket = new FakeWebSocket();
        var host = new ClientConnection(socket) { Identity = identity };
        var rooms = new RoomManager();
        rooms.Create(host, TestServer.BuildLoadout());

        await rooms.DrainMatchesAsync();

        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM match_history"));
        Assert.Null(host.CurrentRoom);
    }

    [Fact]
    public async Task DrainService_RunsTheDrainWhenTheHostStops()
    {
        using var server = new TestServer();
        LiveMatch match = await StartMatchAsync(server);
        var drainService = new MatchDrainService(match.Rooms, NullLogger<MatchDrainService>.Instance);

        await drainService.StopAsync(CancellationToken.None);

        Assert.Equal(1, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM match_history WHERE ended_reason = 'server_shutdown'"));
    }
}
