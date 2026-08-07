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

        /// <summary>
        /// Correlazione richiesta/risposta. Il client lo genera per ogni richiesta, il
        /// server lo ricopia sulla risposta diretta: senza, due richieste in volo con la
        /// stessa risposta attesa si scambierebbero l'esito. Sui messaggi spontanei del
        /// server (eventi di match, presenza amici...) resta vuoto.
        /// Vale anche da chiave di idempotenza: un rinvio dopo una riconnessione porta
        /// lo stesso requestId e il server risponde senza rieseguire la mutazione.
        /// </summary>
        public string requestId;
    }

    public static class MessageTypes
    {
        public const string Error = "error";

        public const string AuthRegister = "auth.register";
        public const string AuthLogin = "auth.login";
        public const string AuthUgs = "auth.ugs";

        /// <summary>Riaggancio di una sessione già autenticata dopo una caduta di rete.</summary>
        public const string AuthSession = "auth.session";
        public const string AuthResponse = "auth.response";

        /// <summary>
        /// Il server chiude questa sessione perché l'account è entrato altrove.
        /// Messaggio spontaneo: arriva senza che il client abbia chiesto nulla.
        /// </summary>
        public const string SessionKicked = "session.kicked";
        public const string NicknameSet = "account.nickname.set";
        public const string NicknameResponse = "account.nickname.response";

        public const string RulesGet = "rules.get";
        public const string RulesData = "rules.data";

        public const string RoomCreate = "room.create";
        public const string RoomCreated = "room.created";
        public const string RoomJoin = "room.join";
        public const string RoomLeave = "room.leave";
        public const string RoomsList = "rooms.list";
        public const string RoomsData = "rooms.data";

        public const string QueueJoin = "queue.join";
        public const string QueueStatus = "queue.status";
        public const string QueueLeave = "queue.leave";

        public const string MatchFound = "match.found";
        public const string MatchStart = "match.start";
        public const string MatchHand = "match.hand";
        public const string MatchAction = "match.action";
        public const string MatchActionAck = "match.action.ack";
        public const string MatchEvent = "match.event";
        public const string MatchOpponentLeft = "match.opponent_left";

        /// <summary>
        /// Resa volontaria: il giocatore si dichiara sconfitto e la vittoria va
        /// all'avversario. Non è un'azione di gioco come le altre — vale anche a
        /// partita in pausa — quindi ha un messaggio suo.
        /// </summary>
        public const string MatchForfeit = "match.forfeit";

        // Riconnessione: la caduta del socket mette il match in pausa invece di
        // assegnare subito la sconfitta (vedi ServerConfig.DisconnectTimeoutSeconds).
        public const string MatchOpponentDisconnected = "match.opponent_disconnected";
        public const string MatchOpponentReconnected = "match.opponent_reconnected";
        public const string MatchResume = "match.resume";

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

        public const string SinglePlayerProgressGet = "singleplayer.progress.get";
        public const string SinglePlayerProgressData = "singleplayer.progress.data";
        public const string SinglePlayerPurchaseUnlock = "singleplayer.unlock.purchase";
        public const string SinglePlayerClaimTutorialReward = "singleplayer.reward.tutorial";
        public const string SinglePlayerClaimDeathReward = "singleplayer.reward.death";
        public const string SinglePlayerClaimAdMultiplier = "singleplayer.reward.ad_multiplier";
        public const string SinglePlayerClaimLevelRewards = "singleplayer.reward.levels";
        public const string SinglePlayerRewardResult = "singleplayer.reward.result";
        public const string SinglePlayerPendingAdRewardsGet = "singleplayer.reward.pending_ads.get";
        public const string SinglePlayerPendingAdRewardsData = "singleplayer.reward.pending_ads.data";
        public const string SinglePlayerClearChapter = "singleplayer.chapter.cleared";
        public const string SinglePlayerChooseClass = "singleplayer.class.choose";

        /// <summary>
        /// Inizio di una run di campagna. Serve solo allo storico: la reward di fine run
        /// arriva alla morte, quindi senza questo messaggio una run abbandonata a meta'
        /// non lascerebbe alcuna traccia sul server.
        /// </summary>
        public const string SinglePlayerRunStarted = "singleplayer.run.started";
        public const string SinglePlayerRunStartedAck = "singleplayer.run.started_ack";

        public const string SanctuaryGet = "sanctuary.get";
        public const string SanctuaryData = "sanctuary.data";
        public const string SanctuaryBuyItem = "sanctuary.item.buy";
        public const string SanctuarySetBag = "sanctuary.bag.set";

        public const string TavernGet = "tavern.get";
        public const string TavernData = "tavern.data";
        public const string TavernClaimQuest = "tavern.quest.claim";
        public const string TavernClaimBonus = "tavern.bonus.claim";
    }

    public static class ErrorCodes
    {
        public const string NotAuthenticated = "not_authenticated";
        public const string InvalidMessage = "invalid_message";
        public const string InvalidCredentials = "invalid_credentials";

        /// <summary>Build più vecchia della versione target: si aggiorna, non si riprova.</summary>
        public const string ClientOutdated = "client_outdated";
        public const string UsernameTaken = "username_taken";
        public const string RoomNotFound = "room_not_found";
        public const string RoomFull = "room_full";
        public const string AlreadyInRoom = "already_in_room";
        public const string InvalidLoadout = "invalid_loadout";
        public const string InvalidAction = "invalid_action";
        public const string NotInMatch = "not_in_match";
        public const string MatchPaused = "match_paused";
        public const string InvalidProgressionRequest = "invalid_progression_request";
        public const string InsufficientHoney = "insufficient_honey";
        public const string RewardClaimNotFound = "reward_claim_not_found";
        public const string AdAlreadyUsed = "ad_already_used";
        public const string RequirementsNotMet = "requirements_not_met";
    }
}
