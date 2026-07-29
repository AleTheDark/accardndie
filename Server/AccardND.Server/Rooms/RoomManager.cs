using System.Collections.Concurrent;
using System.Security.Cryptography;
using AccardND.GameCore.Pvp;
using AccardND.Server.Sessions;

namespace AccardND.Server.Rooms;

public sealed class RoomManager
{
    // Alfabeto senza caratteri ambigui (0/O, 1/I/L).
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    private readonly ConcurrentDictionary<string, Room> roomsByCode = new();

    public Room Create(
        ClientConnection host,
        PvpLoadout hostLoadout,
        string name = null,
        string mode = null,
        RoomVisibility visibility = RoomVisibility.Private)
    {
        while (true)
        {
            string code = GenerateCode();
            var room = new Room(code, host, hostLoadout, name, mode, visibility);
            if (roomsByCode.TryAdd(code, room))
            {
                host.CurrentRoom = room;
                return room;
            }
        }
    }

    /// <summary>
    /// Stanze da mostrare nell'elenco: in attesa di un avversario, non private,
    /// dalla più recente alla più vecchia.
    /// </summary>
    public IReadOnlyList<Room> ListOpen(int limit)
    {
        return roomsByCode.Values
            .Where(room => room.IsListed)
            .OrderByDescending(room => room.CreatedAtUtc)
            .Take(limit)
            .ToList();
    }

    public bool TryJoin(string code, ClientConnection guest, PvpLoadout guestLoadout, out Room room)
    {
        room = null;
        if (string.IsNullOrWhiteSpace(code))
            return false;
        if (!roomsByCode.TryGetValue(code.Trim().ToUpperInvariant(), out Room found))
            return false;
        if (!found.TrySeatGuest(guest, guestLoadout))
            return false;
        guest.CurrentRoom = found;
        room = found;
        return true;
    }

    /// <summary>
    /// Stanza in cui un giocatore è atteso dopo una caduta di rete, se la finestra
    /// non è ancora scaduta. Le stanze vive sono poche: la scansione lineare basta.
    /// </summary>
    public Room FindAwaitingReconnect(string playerId) =>
        string.IsNullOrEmpty(playerId)
            ? null
            : roomsByCode.Values.FirstOrDefault(
                room => room.IsAwaitingReconnect && room.DisconnectedPlayerId == playerId);

    public void Remove(Room room)
    {
        if (room == null)
            return;
        roomsByCode.TryRemove(room.Code, out _);
        if (room.Host != null)
            room.Host.CurrentRoom = null;
        if (room.Guest != null)
            room.Guest.CurrentRoom = null;
    }

    public async Task DrainMatchesAsync()
    {
        Room[] snapshot = roomsByCode.Values.ToArray();
        foreach (Room room in snapshot)
        {
            room.EndReconnectWait()?.Cancel();
            if (room.Session != null)
                await room.Session.RecordServerShutdownAsync();
            Remove(room);
        }
    }

    private static string GenerateCode()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (int index = 0; index < CodeLength; index++)
            code[index] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(code);
    }
}
