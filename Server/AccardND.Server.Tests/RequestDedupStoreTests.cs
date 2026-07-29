using AccardND.NetProtocol;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

public sealed class RequestDedupStoreTests
{
    [Fact]
    public void SamePlayerAndRequestId_ReplaysTheStoredAck()
    {
        using var server = new TestServer();
        RequestDedupStore store = server.CreateDedupStore();
        var ack = new RequestDedupStore.CachedReply(
            MessageTypes.MatchActionAck,
            """{"accepted":true}""");

        store.Store("player-a", "move-42", ack);

        Assert.True(store.TryGet("player-a", "move-42", out var replay));
        Assert.Equal(MessageTypes.MatchActionAck, replay.Type);
        Assert.Equal(ack.Payload, replay.Payload);
    }

    [Fact]
    public void RequestIds_AreScopedPerPlayer()
    {
        using var server = new TestServer();
        RequestDedupStore store = server.CreateDedupStore();
        store.Store(
            "player-a",
            "same-id",
            new RequestDedupStore.CachedReply(MessageTypes.MatchActionAck, "{}"));

        Assert.False(store.TryGet("player-b", "same-id", out _));
    }

    [Fact]
    public void TheMemory_SurvivesAServerRestart()
    {
        // È il caso che conta: il client rigioca la sua coda persistita al riavvio,
        // magari il giorno dopo un deploy. Se il ricordo morisse col processo,
        // quell'acquisto verrebbe applicato una seconda volta.
        using var server = new TestServer();
        server.CreateDedupStore().Store(
            "player-a",
            "buy-7",
            new RequestDedupStore.CachedReply(MessageTypes.SanctuaryData, """{"honey":10}"""));

        RequestDedupStore afterDeploy = server.RestartAndCreateDedupStore();

        Assert.True(afterDeploy.TryGet("player-a", "buy-7", out var replay));
        Assert.Equal("""{"honey":10}""", replay.Payload);
    }

    [Fact]
    public void ExpiredEntries_AreIgnoredAndSweptAway()
    {
        using var server = new TestServer();
        RequestDedupStore store = server.CreateDedupStore();
        server.Execute(
            "INSERT INTO request_dedup (player_id, request_id, reply_type, reply_payload, expires_at) "
            + $"VALUES ('player-a', 'vecchia', '{MessageTypes.TavernData}', '{{}}', "
            + $"'{DateTime.UtcNow.AddDays(-1):O}')");

        Assert.False(store.TryGet("player-a", "vecchia", out _));

        // La scrittura successiva fa anche da spazzino: la riga scaduta sparisce.
        store.Store("player-b", "nuova", new RequestDedupStore.CachedReply(MessageTypes.TavernData, "{}"));

        Assert.Equal(0, server.QueryScalar<int>(
            "SELECT COUNT(*) FROM request_dedup WHERE request_id = 'vecchia'"));
    }

    [Fact]
    public void StoringTwice_KeepsTheLatestReply()
    {
        using var server = new TestServer();
        RequestDedupStore store = server.CreateDedupStore();

        store.Store("player-a", "req", new RequestDedupStore.CachedReply(MessageTypes.TavernData, """{"v":1}"""));
        store.Store("player-a", "req", new RequestDedupStore.CachedReply(MessageTypes.TavernData, """{"v":2}"""));

        Assert.True(store.TryGet("player-a", "req", out var replay));
        Assert.Equal("""{"v":2}""", replay.Payload);
        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM request_dedup"));
    }

    [Fact]
    public void RequestsWithoutAnIdentity_AreNotRemembered()
    {
        using var server = new TestServer();
        RequestDedupStore store = server.CreateDedupStore();

        store.Store(null, "req", new RequestDedupStore.CachedReply(MessageTypes.TavernData, "{}"));
        store.Store("player-a", null, new RequestDedupStore.CachedReply(MessageTypes.TavernData, "{}"));

        Assert.Equal(0, server.QueryScalar<int>("SELECT COUNT(*) FROM request_dedup"));
    }
}
