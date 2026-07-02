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

    public Room Create(ClientConnection host, PvpLoadout hostLoadout)
    {
        while (true)
        {
            string code = GenerateCode();
            var room = new Room(code, host, hostLoadout);
            if (roomsByCode.TryAdd(code, room))
            {
                host.CurrentRoom = room;
                return room;
            }
        }
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

    private static string GenerateCode()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (int index = 0; index < CodeLength; index++)
            code[index] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(code);
    }
}
