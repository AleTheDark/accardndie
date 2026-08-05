using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Match;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La grazia di riconnessione. Due promesse verso il giocatore: chi rientra ritrova
/// la partita esattamente com'era, e chi stacca la rete a ripetizione non può
/// regalarsi una finestra nuova ogni volta tenendo l'avversario appeso all'infinito.
/// </summary>
public sealed class ReconnectGraceTests
{
    private const int Budget = 120;

    private sealed class TestClock
    {
        private DateTime now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public DateTime Now() => now;

        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }

    private static Room CreateRoom(TestServer server, TestClock clock, out AccountIdentity hostIdentity)
    {
        hostIdentity = server.RegisterAccount("ospitante");
        var host = new ClientConnection(new FakeWebSocket()) { Identity = hostIdentity };
        return new Room(
            "TESTAA", host, TestServer.BuildLoadout(), utcNow: clock.Now);
    }

    [Fact]
    public void ReconnectBudget_StartsFull()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);

        Assert.Equal(Budget, room.RemainingReconnectSeconds(host.PlayerId, Budget));
    }

    [Fact]
    public void SecondDisconnect_GetsWhatIsLeft_NotAFreshWindow()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);

        room.BeginReconnectWait(host.PlayerId, Budget, new CancellationTokenSource());
        clock.Advance(90);
        room.EndReconnectWait();

        Assert.Equal(30, room.RemainingReconnectSeconds(host.PlayerId, Budget));
    }

    [Fact]
    public void DuringTheWait_TheCountdownAlreadyErodesTheBudget()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);

        room.BeginReconnectWait(host.PlayerId, Budget, new CancellationTokenSource());
        clock.Advance(45);

        Assert.Equal(75, room.RemainingReconnectSeconds(host.PlayerId, Budget));
    }

    [Fact]
    public void ManyShortDisconnects_AddUpToTheSameBudget()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);

        for (int drop = 0; drop < 4; drop++)
        {
            room.BeginReconnectWait(
                host.PlayerId, room.RemainingReconnectSeconds(host.PlayerId, Budget), new CancellationTokenSource());
            clock.Advance(25);
            room.EndReconnectWait();
        }

        Assert.Equal(Budget - 100, room.RemainingReconnectSeconds(host.PlayerId, Budget));
    }

    [Fact]
    public void AStaleCountdown_CannotCloseTheWaitOpenedByTheNextDisconnect()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);

        var first = new CancellationTokenSource();
        room.BeginReconnectWait(host.PlayerId, Budget, first);
        clock.Advance(10);
        room.EndReconnectWait();

        var second = new CancellationTokenSource();
        room.BeginReconnectWait(host.PlayerId, 110, second);

        Assert.False(room.TryEndReconnectWait(first));
        Assert.True(room.IsAwaitingReconnect);
        Assert.True(room.TryEndReconnectWait(second));
        Assert.False(room.IsAwaitingReconnect);
    }

    [Fact]
    public void ABudgetSpentToTheLastSecond_LeavesNothingForTheNextDisconnect()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);

        room.BeginReconnectWait(host.PlayerId, Budget, new CancellationTokenSource());
        clock.Advance(Budget);
        room.EndReconnectWait();

        // Zero significa che il router non aprirà una nuova attesa: la prossima
        // caduta è un abbandono.
        Assert.Equal(0, room.RemainingReconnectSeconds(host.PlayerId, Budget));
    }

    [Fact]
    public void TheTimeSpentOfflineIsChargedToWhoWasOffline_NotToTheOpponent()
    {
        using var server = new TestServer();
        var clock = new TestClock();
        Room room = CreateRoom(server, clock, out AccountIdentity host);
        AccountIdentity guest = server.RegisterAccount("sfidante");
        room.TrySeatGuest(new ClientConnection(new FakeWebSocket()) { Identity = guest }, TestServer.BuildLoadout());

        room.BeginReconnectWait(host.PlayerId, Budget, new CancellationTokenSource());
        clock.Advance(100);
        room.EndReconnectWait();

        Assert.Equal(20, room.RemainingReconnectSeconds(host.PlayerId, Budget));
        Assert.Equal(Budget, room.RemainingReconnectSeconds(guest.PlayerId, Budget));
    }

    [Fact]
    public async Task Resume_HandsBackTheWholeMatch_LoadoutIncluded()
    {
        using var server = new TestServer();
        AccountIdentity hostIdentity = server.RegisterAccount("ospitante");
        AccountIdentity guestIdentity = server.RegisterAccount("sfidante");
        var host = new ClientConnection(new FakeWebSocket()) { Identity = hostIdentity };
        var guest = new ClientConnection(new FakeWebSocket()) { Identity = guestIdentity };

        var rooms = new RoomManager();
        Room room = rooms.Create(host, TestServer.BuildLoadout());
        Assert.True(rooms.TryJoin(room.Code, guest, TestServer.BuildLoadout(), out _));
        room.Session = new MatchSession(room, server.Config, server.ResultRecorder);
        await room.Session.StartAsync(CancellationToken.None);

        Assert.True(await room.Session.PauseForReconnectAsync(host, 30));
        var returningSocket = new FakeWebSocket();
        var returning = new ClientConnection(returningSocket) { Identity = hostIdentity };
        Assert.True(await room.Session.ResumeAsync(returning, 30, CancellationToken.None));

        Envelope envelope = Assert.Single(returningSocket.SentOfType(MessageTypes.MatchResume));
        var resume = ClientConnection.ParsePayload<MatchResumeState>(envelope);
        Assert.Equal(0, resume.yourPlayerIndex);
        Assert.Equal(guestIdentity.Username, resume.opponentName);
        // Il log eventi è la partita: senza, chi rientra ricostruirebbe un tavolo vuoto.
        Assert.NotEmpty(resume.events);
        // Il loadout arriva dal server perché il client, dopo un riavvio dell'app,
        // non ha più il proprio in memoria.
        Assert.Equal(
            TestServer.BuildLoadout().Cards.Count, resume.yourLoadout?.cards?.Length);
        Assert.Equal(30, resume.reconnectSecondsRemaining);
    }

    [Fact]
    public async Task AfterResume_TheOpponentIsToldTheMatchIsBackOn()
    {
        using var server = new TestServer();
        AccountIdentity hostIdentity = server.RegisterAccount("ospitante");
        AccountIdentity guestIdentity = server.RegisterAccount("sfidante");
        var host = new ClientConnection(new FakeWebSocket()) { Identity = hostIdentity };
        var guestSocket = new FakeWebSocket();
        var guest = new ClientConnection(guestSocket) { Identity = guestIdentity };

        var rooms = new RoomManager();
        Room room = rooms.Create(host, TestServer.BuildLoadout());
        Assert.True(rooms.TryJoin(room.Code, guest, TestServer.BuildLoadout(), out _));
        room.Session = new MatchSession(room, server.Config, server.ResultRecorder);
        await room.Session.StartAsync(CancellationToken.None);

        Assert.True(await room.Session.PauseForReconnectAsync(host, 30));
        var dropped = ClientConnection.ParsePayload<MatchOpponentDisconnected>(
            Assert.Single(guestSocket.SentOfType(MessageTypes.MatchOpponentDisconnected)));
        Assert.Equal(30, dropped.secondsRemaining);

        await room.Session.ResumeAsync(
            new ClientConnection(new FakeWebSocket()) { Identity = hostIdentity },
            30,
            CancellationToken.None);

        Assert.Single(guestSocket.SentOfType(MessageTypes.MatchOpponentReconnected));
    }

    [Fact]
    public async Task ALiveMatchIsFound_EvenWhenTheServerHasNotNoticedTheDropYet()
    {
        using var server = new TestServer();
        AccountIdentity hostIdentity = server.RegisterAccount("ospitante");
        AccountIdentity guestIdentity = server.RegisterAccount("sfidante");
        var host = new ClientConnection(new FakeWebSocket()) { Identity = hostIdentity };
        var guest = new ClientConnection(new FakeWebSocket()) { Identity = guestIdentity };

        var rooms = new RoomManager();
        Room room = rooms.Create(host, TestServer.BuildLoadout());
        Assert.True(rooms.TryJoin(room.Code, guest, TestServer.BuildLoadout(), out _));
        room.Session = new MatchSession(room, server.Config, server.ResultRecorder);
        await room.Session.StartAsync(CancellationToken.None);

        // Nessuna attesa aperta: il socket morto è ancora "aperto" per il server.
        Assert.Null(rooms.FindAwaitingReconnect(hostIdentity.PlayerId));
        Assert.Same(room, rooms.FindLiveMatchOf(hostIdentity.PlayerId));

        // E il rientro deve poter prendere il posto della connessione zombie.
        var returning = new ClientConnection(new FakeWebSocket()) { Identity = hostIdentity };
        Assert.True(room.TryReplaceConnection(returning, out ClientConnection stale));
        Assert.Same(host, stale);
        Assert.Same(returning, room.Host);
    }
}
