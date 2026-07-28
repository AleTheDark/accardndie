using System;

namespace AccardND.NetProtocol
{
    /// <summary>
    /// Modalità di una stanza privata. Non è solo un'etichetta: il server applica
    /// regole diverse al match (vedi ServerConfig.ToMatchRules).
    /// </summary>
    public static class RoomModes
    {
        public const string Standard = "standard";
        public const string Hardcore = "hardcore";

        /// <summary>Lunghezza massima del nome scelto da chi crea la stanza.</summary>
        public const int NameMaxLength = 24;

        public static bool IsHardcore(string mode) =>
            string.Equals(mode, Hardcore, StringComparison.OrdinalIgnoreCase);

        public static string Normalize(string mode) => IsHardcore(mode) ? Hardcore : Standard;

        public static string DisplayName(string mode) => IsHardcore(mode) ? "Hardcore" : "Standard";
    }

    [Serializable]
    public sealed class CreateRoomRequest
    {
        public PvpLoadoutDto loadout;

        /// <summary>Nome mostrato nell'elenco stanze; vuoto = nome generato dal server.</summary>
        public string roomName;

        /// <summary>Vedi <see cref="RoomModes"/>.</summary>
        public string mode;

        /// <summary>true = visibile e aperta a tutti, false = protetta dal codice.</summary>
        public bool isPublic;
    }

    [Serializable]
    public sealed class RoomCreated
    {
        public string code;
        public string roomName;
        public string mode;
        public bool isPublic;
    }

    [Serializable]
    public sealed class JoinRoomRequest
    {
        public string code;
        public PvpLoadoutDto loadout;
    }

    /// <summary>Riga dell'elenco stanze (rooms.data).</summary>
    [Serializable]
    public sealed class RoomSummary
    {
        public string code;              // valorizzato solo per le stanze pubbliche
        public string roomName;
        public string mode;
        public string hostUsername;
        public string hostTier;          // vuoto se l'host non è ancora classificato
        public string hostDivision;
        public bool isPublic;            // false = serve il codice per entrare
        public int players;
        public int capacity;
    }

    [Serializable]
    public sealed class RoomsData
    {
        public RoomSummary[] rooms;
    }

    [Serializable]
    public sealed class QueueJoinRequest
    {
        public PvpLoadoutDto loadout;
    }

    [Serializable]
    public sealed class QueueStatus
    {
        public bool queued;
        public int position;
    }
}
