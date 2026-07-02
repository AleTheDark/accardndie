using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class CreateRoomRequest
    {
        public PvpLoadoutDto loadout;
    }

    [Serializable]
    public sealed class RoomCreated
    {
        public string code;
    }

    [Serializable]
    public sealed class JoinRoomRequest
    {
        public string code;
        public PvpLoadoutDto loadout;
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
