using AccardND.NetProtocol;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// L'ambito di risposta è quello che rende rigiocabile una richiesta: memorizza
/// la *prima* cosa spedita dentro l'ambito, non l'ultima. Se questa regola si
/// rompesse, un rinvio della stessa mossa riceverebbe un evento di partita al
/// posto del suo ACK - e il client resterebbe ad aspettare.
/// </summary>
public sealed class ReplyScopeTests
{
    [Fact]
    public async Task TheFirstReplyCarriesTheRequestId_AndIsTheOneRemembered()
    {
        var socket = new FakeWebSocket();
        var connection = new ClientConnection(socket);

        connection.BeginReplyScope("move-1");
        await connection.SendAsync(MessageTypes.MatchActionAck, new MatchActionAck { accepted = true });
        await connection.SendAsync(MessageTypes.MatchEvent, new MatchEventDto());

        Assert.True(connection.EndReplyScope(out RequestDedupStore.CachedReply reply));
        Assert.Equal(MessageTypes.MatchActionAck, reply.Type);

        Assert.Equal("move-1", socket.Sent[0].requestId);
        Assert.Null(socket.Sent[1].requestId);
    }

    [Fact]
    public async Task OutsideAScope_NothingIsCorrelatedNorRemembered()
    {
        var socket = new FakeWebSocket();
        var connection = new ClientConnection(socket);

        await connection.SendAsync(MessageTypes.MatchEvent, new MatchEventDto());

        Assert.False(connection.EndReplyScope(out _));
        Assert.Null(socket.Sent[0].requestId);
    }

    [Fact]
    public async Task AStoredReply_CanBeSentBackVerbatim()
    {
        var socket = new FakeWebSocket();
        var connection = new ClientConnection(socket);
        connection.BeginReplyScope("buy-1");
        await connection.SendAsync(MessageTypes.SanctuaryData, new SanctuaryData());
        connection.EndReplyScope(out RequestDedupStore.CachedReply reply);

        // È esattamente quello che fa il router quando riconosce un rinvio.
        connection.BeginReplyScope("buy-1");
        await connection.SendRawAsync(reply.Type, reply.Payload);
        connection.EndReplyScope(out _);

        Assert.Equal(2, socket.Sent.Count);
        Assert.Equal(socket.Sent[0].type, socket.Sent[1].type);
        Assert.Equal(socket.Sent[0].payload, socket.Sent[1].payload);
        Assert.Equal("buy-1", socket.Sent[1].requestId);
    }
}
