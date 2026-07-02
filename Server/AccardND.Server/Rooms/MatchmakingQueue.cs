using AccardND.GameCore.Pvp;
using AccardND.Server.Sessions;

namespace AccardND.Server.Rooms;

public sealed record QueueEntry(ClientConnection Connection, PvpLoadout Loadout);

public sealed class MatchmakingQueue
{
    private readonly object gate = new();
    private readonly List<QueueEntry> waiting = new();

    /// <summary>
    /// Accoda il giocatore; se c'è già qualcuno in attesa ritorna la coppia da avviare.
    /// </summary>
    public (QueueEntry First, QueueEntry Second)? Enqueue(ClientConnection connection, PvpLoadout loadout)
    {
        lock (gate)
        {
            waiting.RemoveAll(entry => entry.Connection == connection || !entry.Connection.IsOpen);
            var candidate = new QueueEntry(connection, loadout);
            if (waiting.Count > 0)
            {
                QueueEntry opponent = waiting[0];
                waiting.RemoveAt(0);
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
