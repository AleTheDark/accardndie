using System;

namespace AccardND.NetProtocol
{
    /// <summary>
    /// Busta di trasporto: il payload è il JSON del messaggio tipizzato.
    /// La doppia codifica mantiene compatibili JsonUtility (client) e System.Text.Json (server).
    /// </summary>
    [Serializable]
    public sealed class Envelope
    {
        public string type;
        public string payload;
    }

    public static class MessageTypes
    {
        public const string Error = "error";

        public const string AuthRegister = "auth.register";
        public const string AuthLogin = "auth.login";
        public const string AuthResponse = "auth.response";

        public const string RulesGet = "rules.get";
        public const string RulesData = "rules.data";

        public const string RoomCreate = "room.create";
        public const string RoomCreated = "room.created";
        public const string RoomJoin = "room.join";

        public const string QueueJoin = "queue.join";
        public const string QueueStatus = "queue.status";
        public const string QueueLeave = "queue.leave";

        public const string MatchFound = "match.found";
        public const string MatchStart = "match.start";
        public const string MatchHand = "match.hand";
        public const string MatchAction = "match.action";
        public const string MatchEvent = "match.event";
        public const string MatchOpponentLeft = "match.opponent_left";
    }

    public static class ErrorCodes
    {
        public const string NotAuthenticated = "not_authenticated";
        public const string InvalidMessage = "invalid_message";
        public const string InvalidCredentials = "invalid_credentials";
        public const string UsernameTaken = "username_taken";
        public const string RoomNotFound = "room_not_found";
        public const string RoomFull = "room_full";
        public const string AlreadyInRoom = "already_in_room";
        public const string InvalidLoadout = "invalid_loadout";
        public const string InvalidAction = "invalid_action";
        public const string NotInMatch = "not_in_match";
    }
}
