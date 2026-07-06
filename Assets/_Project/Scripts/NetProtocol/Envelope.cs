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
        public const string AuthUgs = "auth.ugs";
        public const string AuthResponse = "auth.response";

        public const string RulesGet = "rules.get";
        public const string RulesData = "rules.data";

        public const string RoomCreate = "room.create";
        public const string RoomCreated = "room.created";
        public const string RoomJoin = "room.join";
        public const string RoomLeave = "room.leave";

        public const string QueueJoin = "queue.join";
        public const string QueueStatus = "queue.status";
        public const string QueueLeave = "queue.leave";

        public const string MatchFound = "match.found";
        public const string MatchStart = "match.start";
        public const string MatchHand = "match.hand";
        public const string MatchAction = "match.action";
        public const string MatchEvent = "match.event";
        public const string MatchOpponentLeft = "match.opponent_left";

        public const string StatsGet = "stats.get";
        public const string StatsData = "stats.data";

        public const string RankedGet = "ranked.get";
        public const string RankedData = "ranked.data";
        public const string LeaderboardGet = "leaderboard.get";
        public const string LeaderboardData = "leaderboard.data";
        public const string MatchResult = "match.result";

        public const string ProfileGet = "profile.get";
        public const string ProfileData = "profile.data";
        public const string ProfileSetIcon = "profile.set_icon";
        public const string IconsList = "icons.list";
        public const string IconsData = "icons.data";
        public const string CampaignReportKills = "campaign.report_kills";
        public const string CampaignKillsResult = "campaign.kills_result";

        public const string HallOfFameSeasonsGet = "halloffame.seasons.get";
        public const string HallOfFameSeasonsData = "halloffame.seasons.data";
        public const string HallOfFameGet = "halloffame.get";
        public const string HallOfFameData = "halloffame.data";

        public const string FriendsList = "friends.list";
        public const string FriendsData = "friends.data";
        public const string FriendAdd = "friends.add";
        public const string FriendRespond = "friends.respond";
        public const string FriendRemove = "friends.remove";
        public const string FriendBlock = "friends.block";
        public const string FriendPresence = "friends.presence";
        public const string FriendChallenge = "friends.challenge";
        public const string FriendChallengeReceived = "friends.challenge_received";

        public const string AchievementsGet = "achievements.get";
        public const string AchievementsData = "achievements.data";
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
