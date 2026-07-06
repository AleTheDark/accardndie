using AccardND.GameCore.Pvp;
using AccardND.Server.Match;
using AccardND.Server.Sessions;

namespace AccardND.Server.Rooms;

public sealed class Room
{
    private readonly object gate = new();

    public Room(string code, ClientConnection host, PvpLoadout hostLoadout)
    {
        Code = code;
        Host = host;
        HostLoadout = hostLoadout;
    }

    public string Code { get; }
    public ClientConnection Host { get; }
    public PvpLoadout HostLoadout { get; }
    public ClientConnection Guest { get; private set; }
    public PvpLoadout GuestLoadout { get; private set; }
    public MatchSession Session { get; set; }
    public bool IsFull => Guest != null;

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
}
