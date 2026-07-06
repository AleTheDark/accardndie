using System.Collections.Concurrent;

namespace AccardND.Server.Sessions;

/// <summary>
/// Traccia i giocatori connessi per calcolare la presenza degli amici.
/// Stati: "online" (connesso), "in_match" (in una partita in corso), "offline".
/// </summary>
public sealed class PresenceRegistry
{
    public const string Online = "online";
    public const string InMatch = "in_match";
    public const string Offline = "offline";

    private readonly ConcurrentDictionary<string, ClientConnection> byPlayer = new();

    public void Register(ClientConnection connection)
    {
        if (connection.Identity != null)
            byPlayer[connection.Identity.PlayerId] = connection;
    }

    /// <summary>Rimuove la connessione solo se è ancora quella registrata per il giocatore.</summary>
    public void Unregister(ClientConnection connection)
    {
        if (connection.Identity == null)
            return;
        var entry = new KeyValuePair<string, ClientConnection>(connection.Identity.PlayerId, connection);
        ((ICollection<KeyValuePair<string, ClientConnection>>)byPlayer).Remove(entry);
    }

    public bool IsOnline(string playerId) =>
        byPlayer.TryGetValue(playerId, out ClientConnection connection) && connection.IsOpen;

    public ClientConnection GetConnection(string playerId) =>
        byPlayer.TryGetValue(playerId, out ClientConnection connection) && connection.IsOpen ? connection : null;

    public string GetPresence(string playerId)
    {
        ClientConnection connection = GetConnection(playerId);
        if (connection == null)
            return Offline;
        return connection.CurrentRoom?.Session is { IsFinished: false } ? InMatch : Online;
    }
}
