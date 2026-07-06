using AccardND.GameCore.Pvp;
using AccardND.Server.Sessions;

namespace AccardND.Server.Rooms;

public sealed record QueueEntry(ClientConnection Connection, PvpLoadout Loadout, int Mmr);

public sealed class MatchmakingQueue
{
    private readonly object gate = new();
    private readonly List<QueueEntry> waiting = new();

    /// <summary>
    /// Accoda il giocatore; se c'è già qualcuno in attesa ritorna la coppia da avviare,
    /// scegliendo l'avversario con l'MMR più vicino.
    /// </summary>
    public (QueueEntry First, QueueEntry Second)? Enqueue(
        ClientConnection connection, PvpLoadout loadout, int mmr)
    {
        lock (gate)
        {
            string playerId = connection.Identity?.PlayerId;
            waiting.RemoveAll(entry =>
                entry.Connection == connection
                || !entry.Connection.IsOpen
                || (!string.IsNullOrEmpty(playerId)
                    && entry.Connection.Identity?.PlayerId == playerId));
            var candidate = new QueueEntry(connection, loadout, mmr);
            if (waiting.Count > 0)
            {
                QueueEntry opponent = waiting
                    .OrderBy(entry => Math.Abs(entry.Mmr - mmr))
                    .First();
                waiting.Remove(opponent);
                return (opponent, candidate);
            }
            waiting.Add(candidate);
            return null;
        }
    }

    public int PositionOf(ClientConnection connection)
    {
        lock (gate)
            return waiting.FindIndex(entry => entry.Connection == connection) + 1;
    }

    public void Remove(ClientConnection connection)
    {
        lock (gate)
            waiting.RemoveAll(entry => entry.Connection == connection);
    }
}
