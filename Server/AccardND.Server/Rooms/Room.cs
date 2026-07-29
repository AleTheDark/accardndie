using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Match;
using AccardND.Server.Sessions;

namespace AccardND.Server.Rooms;

/// <summary>Visibilità di una stanza nell'elenco pubblico (rooms.list).</summary>
public enum RoomVisibility
{
    /// <summary>Elencata e aperta a chiunque.</summary>
    Public,

    /// <summary>Elencata ma col lucchetto: per entrare serve il codice.</summary>
    Protected,

    /// <summary>Fuori dall'elenco: matchmaking e sfide dirette fra amici.</summary>
    Private
}

public sealed class Room
{
    private readonly object gate = new();

    public Room(
        string code,
        ClientConnection host,
        PvpLoadout hostLoadout,
        string name = null,
        string mode = null,
        RoomVisibility visibility = RoomVisibility.Private)
    {
        Code = code;
        Host = host;
        HostLoadout = hostLoadout;
        Mode = RoomModes.Normalize(mode);
        Visibility = visibility;
        Name = string.IsNullOrWhiteSpace(name)
            ? $"Stanza di {host?.Identity?.Username ?? "sfidante"}"
            : name;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Code { get; }
    public ClientConnection Host { get; private set; }
    public PvpLoadout HostLoadout { get; }
    public ClientConnection Guest { get; private set; }
    public PvpLoadout GuestLoadout { get; private set; }
    public MatchSession Session { get; set; }
    public bool IsFull => Guest != null;

    /// <summary>Nome mostrato nell'elenco stanze.</summary>
    public string Name { get; }

    /// <summary>Modalità della stanza: cambia davvero le regole del match (vedi <see cref="RoomModes"/>).</summary>
    public string Mode { get; }

    public RoomVisibility Visibility { get; }

    public DateTime CreatedAtUtc { get; }

    /// <summary>
    /// Lega dell'host fotografata alla creazione: così l'elenco stanze non
    /// interroga il database per ogni riga ad ogni refresh. null = non classificato.
    /// </summary>
    public string HostTier { get; set; }

    public string HostDivision { get; set; }

    /// <summary>true se la stanza va mostrata nell'elenco: in attesa dell'avversario e non privata.</summary>
    public bool IsListed =>
        Visibility != RoomVisibility.Private
        && Session == null
        && Guest == null
        && Host is { IsOpen: true };

    /// <summary>true se la partita è di matchmaking (conta per l'MMR); false per le stanze con codice.</summary>
    public bool Ranked { get; set; }

    public bool TrySeatGuest(ClientConnection guest, PvpLoadout loadout)
    {
        lock (gate)
        {
            if (Guest != null)
                return false;
            Guest = guest;
            GuestLoadout = loadout;
            return true;
        }
    }

    public ClientConnection OpponentOf(ClientConnection connection) =>
        connection == Host ? Guest : Host;

    // --- Attesa di riconnessione ---
    //
    // Un socket che cade non è un abbandono: su mobile basta un cambio di cella.
    // La stanza resta in piedi per DisconnectTimeoutSeconds e la partita si mette
    // in pausa; solo allo scadere diventa una sconfitta.

    /// <summary>Giocatore la cui connessione è caduta, null se sono entrambi collegati.</summary>
    public string DisconnectedPlayerId { get; private set; }

    public DateTime ReconnectDeadlineUtc { get; private set; }

    public bool IsAwaitingReconnect => DisconnectedPlayerId != null;

    private CancellationTokenSource reconnectTimer;

    public void BeginReconnectWait(string playerId, int seconds, CancellationTokenSource timer)
    {
        lock (gate)
        {
            DisconnectedPlayerId = playerId;
            ReconnectDeadlineUtc = DateTime.UtcNow.AddSeconds(seconds);
            reconnectTimer = timer;
        }
    }

    /// <summary>
    /// Chiude l'attesa e restituisce il timer a chi ha vinto la corsa. Il rientro del
    /// giocatore e la scadenza del countdown possono arrivare insieme: solo uno dei due
    /// riceve un valore non null, l'altro deve fermarsi.
    /// </summary>
    public CancellationTokenSource EndReconnectWait()
    {
        lock (gate)
        {
            CancellationTokenSource timer = reconnectTimer;
            reconnectTimer = null;
            DisconnectedPlayerId = null;
            return timer;
        }
    }

    /// <summary>
    /// Sostituisce la connessione morta di un giocatore con quella appena riautenticata,
    /// riconosciuta dal PlayerId. Restituisce false se il giocatore non è di questa stanza.
    /// </summary>
    public bool TryReplaceConnection(ClientConnection replacement, out ClientConnection replaced)
    {
        replaced = null;
        string playerId = replacement?.Identity?.PlayerId;
        if (string.IsNullOrEmpty(playerId))
            return false;

        lock (gate)
        {
            if (Host?.Identity?.PlayerId == playerId)
            {
                replaced = Host;
                Host = replacement;
                return true;
            }
            if (Guest?.Identity?.PlayerId == playerId)
            {
                replaced = Guest;
                Guest = replacement;
                return true;
            }
        }
        return false;
    }
}
