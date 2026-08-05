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

    /// <summary>
    /// Orologio della stanza. Esiste perché la grazia di riconnessione è un budget
    /// che si consuma nel tempo: senza poterlo far scorrere a comando, verificarla
    /// significherebbe scrivere test che dormono per minuti.
    /// </summary>
    private readonly Func<DateTime> utcNow;

    public Room(
        string code,
        ClientConnection host,
        PvpLoadout hostLoadout,
        string name = null,
        string mode = null,
        RoomVisibility visibility = RoomVisibility.Private,
        Func<DateTime> utcNow = null)
    {
        Code = code;
        Host = host;
        HostLoadout = hostLoadout;
        Mode = RoomModes.Normalize(mode);
        Visibility = visibility;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        Name = string.IsNullOrWhiteSpace(name)
            ? $"Stanza di {host?.Identity?.Username ?? "sfidante"}"
            : name;
        CreatedAtUtc = this.utcNow();
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
    // La stanza resta in piedi finché il giocatore ha grazia residua e la partita si
    // mette in pausa; solo all'esaurirsi diventa una sconfitta.
    //
    // La grazia è un budget per l'intera partita, non per singola caduta: chi si
    // scollega dieci volte di fila non può regalarsi dieci finestre piene, o
    // basterebbe staccare la rete a ripetizione per tenere l'avversario appeso
    // all'infinito.

    /// <summary>Giocatore la cui connessione è caduta, null se sono entrambi collegati.</summary>
    public string DisconnectedPlayerId { get; private set; }

    public DateTime ReconnectDeadlineUtc { get; private set; }

    public bool IsAwaitingReconnect => DisconnectedPlayerId != null;

    private CancellationTokenSource reconnectTimer;
    private DateTime reconnectStartedUtc;
    private readonly Dictionary<string, double> spentReconnectSeconds = new(StringComparer.Ordinal);

    /// <summary>
    /// Secondi di grazia che restano a un giocatore per il resto della partita:
    /// il budget totale meno tutto il tempo già passato offline (compreso quello
    /// dell'attesa in corso, se è la sua).
    /// </summary>
    public int RemainingReconnectSeconds(string playerId, int budgetSeconds)
    {
        if (budgetSeconds <= 0 || string.IsNullOrEmpty(playerId))
            return 0;
        lock (gate)
        {
            double spent = SpentSecondsLocked(playerId);
            if (DisconnectedPlayerId == playerId)
                spent += ElapsedWaitSecondsLocked();
            return (int)Math.Floor(Math.Max(0d, budgetSeconds - spent));
        }
    }

    public void BeginReconnectWait(string playerId, int seconds, CancellationTokenSource timer)
    {
        lock (gate)
        {
            DisconnectedPlayerId = playerId;
            reconnectStartedUtc = utcNow();
            ReconnectDeadlineUtc = reconnectStartedUtc.AddSeconds(seconds);
            reconnectTimer = timer;
        }
    }

    /// <summary>
    /// Chiude l'attesa e restituisce il timer a chi ha vinto la corsa. Il rientro del
    /// giocatore e la scadenza del countdown possono arrivare insieme: solo uno dei due
    /// riceve un valore non null, l'altro deve fermarsi.
    ///
    /// Il tempo passato offline viene addebitato qui: è l'unico punto attraversato da
    /// tutti i modi in cui un'attesa può finire.
    /// </summary>
    public CancellationTokenSource EndReconnectWait()
    {
        lock (gate)
        {
            CancellationTokenSource timer = reconnectTimer;
            CloseWaitLocked();
            return timer;
        }
    }

    /// <summary>
    /// Chiusura riservata al countdown: vale solo se l'attesa è ancora la sua. Un
    /// timer scaduto in ritardo non deve poter dichiarare persa la caduta successiva,
    /// che ha una finestra tutta sua ancora aperta.
    /// </summary>
    public bool TryEndReconnectWait(CancellationTokenSource expected)
    {
        lock (gate)
        {
            if (expected == null || !ReferenceEquals(reconnectTimer, expected))
                return false;
            CloseWaitLocked();
            return true;
        }
    }

    private void CloseWaitLocked()
    {
        if (DisconnectedPlayerId != null)
        {
            spentReconnectSeconds[DisconnectedPlayerId] =
                SpentSecondsLocked(DisconnectedPlayerId) + ElapsedWaitSecondsLocked();
        }
        reconnectTimer = null;
        DisconnectedPlayerId = null;
    }

    private double SpentSecondsLocked(string playerId) =>
        spentReconnectSeconds.TryGetValue(playerId, out double spent) ? spent : 0d;

    private double ElapsedWaitSecondsLocked() =>
        Math.Max(0d, (utcNow() - reconnectStartedUtc).TotalSeconds);

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
