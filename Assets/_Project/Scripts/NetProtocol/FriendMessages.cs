using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class FriendAddRequest
    {
        public string username;
    }

    [Serializable]
    public sealed class FriendRespondRequest
    {
        public string playerId;
        public bool accept;
    }

    /// <summary>Bersaglio di un'azione amico (rimuovi / blocca).</summary>
    [Serializable]
    public sealed class FriendTargetRequest
    {
        public string playerId;
    }

    [Serializable]
    public sealed class FriendDto
    {
        public string playerId;
        public string username;
        public string status;    // requested | incoming | accepted | blocked
        public string presence;  // online | in_match | offline
    }

    /// <summary>Lista amici (risposta a friends.list).</summary>
    [Serializable]
    public sealed class FriendsData
    {
        public FriendDto[] friends;
    }

    /// <summary>Aggiornamento di presenza di un amico (push).</summary>
    [Serializable]
    public sealed class FriendPresenceUpdate
    {
        public string playerId;
        public string presence;
    }

    /// <summary>Sfida a un amico: il mittente allega il proprio loadout ed è host della stanza.</summary>
    [Serializable]
    public sealed class FriendChallengeRequest
    {
        public string playerId;
        public PvpLoadoutDto loadout;
    }

    /// <summary>Invito ricevuto: il destinatario si unisce con room.join usando roomCode.</summary>
    [Serializable]
    public sealed class FriendChallengeReceived
    {
        public string roomCode;
        public string challengerId;
        public string challengerName;
    }
}
