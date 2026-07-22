using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Match;
using AccardND.Server.Progression;
using AccardND.Server.Rooms;

namespace AccardND.Server.Sessions;

public sealed class MessageRouter
{
    private readonly ServerConfig config;
    private readonly AccountService accounts;
    private readonly UgsAuthService ugsAuth;
    private readonly RoomManager rooms;
    private readonly MatchmakingQueue queue;
    private readonly StatsService stats;
    private readonly RankedService ranked;
    private readonly SeasonService seasons;
    private readonly ProfileService profiles;
    private readonly HallOfFameService hallOfFame;
    private readonly AchievementService achievements;
    private readonly SinglePlayerProgressService singlePlayerProgress;
    private readonly PresenceRegistry presence;
    private readonly FriendService friends;
    private readonly MatchResultRecorder resultRecorder;
    private readonly PvpLoadoutRules loadoutRules;
    private readonly PvpCardCatalog cardCatalog;
    private readonly ILogger<MessageRouter> logger;

    private const int LeaderboardLimit = 50;
    private const int HallOfFameLimit = 50;

    public MessageRouter(
        ServerConfig config,
        AccountService accounts,
        UgsAuthService ugsAuth,
        RoomManager rooms,
        MatchmakingQueue queue,
        StatsService stats,
        RankedService ranked,
        SeasonService seasons,
        ProfileService profiles,
        HallOfFameService hallOfFame,
        AchievementService achievements,
        SinglePlayerProgressService singlePlayerProgress,
        PresenceRegistry presence,
        FriendService friends,
        MatchResultRecorder resultRecorder,
        PvpCardCatalog cardCatalog,
        ILogger<MessageRouter> logger)
    {
        this.config = config;
        this.accounts = accounts;
        this.ugsAuth = ugsAuth;
        this.rooms = rooms;
        this.queue = queue;
        this.stats = stats;
        this.ranked = ranked;
        this.seasons = seasons;
        this.profiles = profiles;
        this.hallOfFame = hallOfFame;
        this.achievements = achievements;
        this.singlePlayerProgress = singlePlayerProgress;
        this.presence = presence;
        this.friends = friends;
        this.resultRecorder = resultRecorder;
        this.cardCatalog = cardCatalog;
        this.logger = logger;
        loadoutRules = config.ToLoadoutRules();
    }

    public async Task HandleConnectionAsync(ClientConnection connection, CancellationToken cancellation)
    {
        logger.LogInformation("Connessione aperta {ConnectionId}", connection.ConnectionId);
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                Envelope envelope = await connection.ReceiveAsync(cancellation);
                if (envelope == null)
                    break;
                await DispatchAsync(connection, envelope, cancellation);
            }
        }
        catch (OperationCanceledException)
        {
            // Arresto del server o disconnessione forzata.
        }
        finally
        {
            await OnDisconnectedAsync(connection);
        }
    }

    private async Task DispatchAsync(ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        switch (envelope.type)
        {
            case MessageTypes.AuthRegister:
                await HandleAuthAsync(connection, envelope, isRegistration: true, cancellation);
                return;
            case MessageTypes.AuthLogin:
                await HandleAuthAsync(connection, envelope, isRegistration: false, cancellation);
                return;
            case MessageTypes.AuthUgs:
                await HandleUgsAuthAsync(connection, envelope, cancellation);
                return;
            case MessageTypes.RulesGet:
                await connection.SendAsync(MessageTypes.RulesData, config.ToRulesData(), cancellation);
                return;
        }

        if (!connection.IsAuthenticated)
        {
            await connection.SendErrorAsync(
                ErrorCodes.NotAuthenticated, "Effettua il login prima.", cancellation);
            return;
        }

        switch (envelope.type)
        {
            case MessageTypes.NicknameSet:
                await HandleSetNicknameAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.RoomCreate:
                await HandleRoomCreateAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.RoomJoin:
                await HandleRoomJoinAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.RoomLeave:
                await HandleRoomLeaveAsync(connection, cancellation);
                break;
            case MessageTypes.QueueJoin:
                await HandleQueueJoinAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.QueueLeave:
                queue.Remove(connection);
                await connection.SendAsync(
                    MessageTypes.QueueStatus, new QueueStatus { queued = false, position = 0 }, cancellation);
                break;
            case MessageTypes.StatsGet:
                await connection.SendAsync(
                    MessageTypes.StatsData, stats.GetStats(connection.Identity), cancellation);
                break;
            case MessageTypes.RankedGet:
                await connection.SendAsync(
                    MessageTypes.RankedData, BuildRankedData(connection), cancellation);
                break;
            case MessageTypes.LeaderboardGet:
                await connection.SendAsync(
                    MessageTypes.LeaderboardData, BuildLeaderboardData(), cancellation);
                break;
            case MessageTypes.ProfileGet:
                await connection.SendAsync(
                    MessageTypes.ProfileData, profiles.GetProfile(connection.Identity), cancellation);
                break;
            case MessageTypes.IconsList:
                await connection.SendAsync(
                    MessageTypes.IconsData, profiles.GetIcons(connection.Identity), cancellation);
                break;
            case MessageTypes.ProfileSetIcon:
                await HandleSetIconAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.CampaignReportKills:
                await HandleCampaignKillsAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.HallOfFameSeasonsGet:
                await connection.SendAsync(
                    MessageTypes.HallOfFameSeasonsData, hallOfFame.GetSeasons(), cancellation);
                break;
            case MessageTypes.HallOfFameGet:
            {
                var request = ClientConnection.ParsePayload<HallOfFameGetRequest>(envelope);
                await connection.SendAsync(MessageTypes.HallOfFameData,
                    hallOfFame.GetHallOfFame(request?.seasonId ?? 0, connection.Identity, HallOfFameLimit),
                    cancellation);
                break;
            }
            case MessageTypes.FriendsList:
                await connection.SendAsync(
                    MessageTypes.FriendsData, friends.GetFriends(connection.Identity), cancellation);
                break;
            case MessageTypes.FriendAdd:
            {
                var request = ClientConnection.ParsePayload<FriendAddRequest>(envelope);
                await ApplyFriendActionAsync(
                    connection, friends.SendRequest(connection.Identity, request?.username), cancellation);
                break;
            }
            case MessageTypes.FriendRespond:
            {
                var request = ClientConnection.ParsePayload<FriendRespondRequest>(envelope);
                await ApplyFriendActionAsync(connection,
                    friends.Respond(connection.Identity, request?.playerId, request?.accept ?? false), cancellation);
                break;
            }
            case MessageTypes.FriendRemove:
            {
                var request = ClientConnection.ParsePayload<FriendTargetRequest>(envelope);
                await ApplyFriendActionAsync(
                    connection, friends.Remove(connection.Identity, request?.playerId), cancellation);
                break;
            }
            case MessageTypes.FriendBlock:
            {
                var request = ClientConnection.ParsePayload<FriendTargetRequest>(envelope);
                await ApplyFriendActionAsync(
                    connection, friends.Block(connection.Identity, request?.playerId), cancellation);
                break;
            }
            case MessageTypes.FriendChallenge:
                await HandleFriendChallengeAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.AchievementsGet:
                await connection.SendAsync(
                    MessageTypes.AchievementsData, achievements.GetAchievements(connection.Identity), cancellation);
                break;
            case MessageTypes.SinglePlayerProgressGet:
                await connection.SendAsync(
                    MessageTypes.SinglePlayerProgressData,
                    singlePlayerProgress.GetProgress(connection.Identity),
                    cancellation);
                break;
            case MessageTypes.SinglePlayerPurchaseUnlock:
                await HandleSinglePlayerPurchaseUnlockAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.SinglePlayerClaimTutorialReward:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerTutorialRewardRequest>(envelope);
                await SendRewardResultAsync(
                    connection, singlePlayerProgress.ClaimTutorialReward(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SinglePlayerClaimDeathReward:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerDeathRewardRequest>(envelope);
                await SendRewardResultAsync(
                    connection, singlePlayerProgress.ClaimDeathReward(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SinglePlayerClaimAdMultiplier:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerAdMultiplierRequest>(envelope);
                await SendRewardResultAsync(
                    connection, singlePlayerProgress.ClaimAdMultiplier(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.MatchAction:
            {
                MatchSession session = connection.CurrentRoom?.Session;
                if (session == null)
                {
                    await connection.SendErrorAsync(
                        ErrorCodes.NotInMatch, "Nessun match in corso.", cancellation);
                    break;
                }
                var action = ClientConnection.ParsePayload<MatchActionDto>(envelope);
                await session.HandleActionAsync(connection, action, cancellation);
                break;
            }
            default:
                await connection.SendErrorAsync(
                    ErrorCodes.InvalidMessage, $"Tipo messaggio sconosciuto: {envelope.type}", cancellation);
                break;
        }
    }

    private async Task HandleSinglePlayerPurchaseUnlockAsync(
        ClientConnection connection,
        Envelope envelope,
        CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<SinglePlayerPurchaseUnlockRequest>(envelope);
        (SinglePlayerProgressData progress, string errorCode, string error) =
            singlePlayerProgress.PurchaseUnlock(connection.Identity, request);

        if (progress == null)
        {
            await connection.SendErrorAsync(errorCode, error, cancellation);
            return;
        }

        await connection.SendAsync(MessageTypes.SinglePlayerProgressData, progress, cancellation);
    }

    private static async Task SendRewardResultAsync(
        ClientConnection connection,
        (SinglePlayerRewardResult Result, string ErrorCode, string Error) outcome,
        CancellationToken cancellation)
    {
        if (outcome.Result == null)
        {
            await connection.SendErrorAsync(outcome.ErrorCode, outcome.Error, cancellation);
            return;
        }

        await connection.SendAsync(MessageTypes.SinglePlayerRewardResult, outcome.Result, cancellation);
    }

    private async Task HandleUgsAuthAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<UgsLoginRequest>(envelope);
        (VerifiedExternalIdentity externalIdentity, string error) =
            await ugsAuth.ValidateAsync(request?.accessToken, request?.displayName);

        ExternalAccountResult resolved = externalIdentity == null
            ? new ExternalAccountResult(null, false, false, error)
            : accounts.ResolveExternalAccount(externalIdentity);
        AccountIdentity identity = resolved.Identity;
        error = resolved.Error ?? error;

        if (identity != null)
        {
            connection.Identity = identity;
            logger.LogInformation(
                "Autenticato via {Provider} '{Username}' ({PlayerId}) su {ConnectionId}",
                externalIdentity.Provider, identity.Username, identity.PlayerId, connection.ConnectionId);
            await OnAuthenticatedAsync(connection);
        }

        await connection.SendAsync(MessageTypes.AuthResponse, new AuthResponse
        {
            ok = identity != null,
            error = error,
            token = null,
            playerId = identity?.PlayerId,
            username = identity?.Username,
            isNewAccount = resolved.IsNewAccount,
            authProvider = externalIdentity?.Provider,
            requiresNickname = resolved.RequiresNickname
        }, cancellation);
    }

    private async Task HandleSetNicknameAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<SetNicknameRequest>(envelope);
        NicknameChangeResult result = accounts.SetNickname(connection.Identity, request?.nickname);
        if (result.Identity != null)
            connection.Identity = result.Identity;

        await connection.SendAsync(MessageTypes.NicknameResponse, new NicknameResponse
        {
            ok = result.Identity != null,
            error = result.Error,
            nickname = result.Identity?.Username
        }, cancellation);
    }

    private async Task HandleAuthAsync(
        ClientConnection connection, Envelope envelope, bool isRegistration, CancellationToken cancellation)
    {
        if (!config.AllowPasswordAuth)
        {
            await connection.SendAsync(MessageTypes.AuthResponse, new AuthResponse
            {
                ok = false,
                error = "Login con password disattivato: usa Unity Authentication."
            }, cancellation);
            return;
        }
        string username;
        string password;
        if (isRegistration)
        {
            var request = ClientConnection.ParsePayload<RegisterRequest>(envelope);
            (username, password) = (request?.username, request?.password);
        }
        else
        {
            var request = ClientConnection.ParsePayload<LoginRequest>(envelope);
            (username, password) = (request?.username, request?.password);
        }

        (AccountIdentity identity, string token, string error) = isRegistration
            ? accounts.Register(username, password)
            : accounts.Login(username, password);

        if (identity != null)
        {
            connection.Identity = identity;
            logger.LogInformation("Autenticato '{Username}' su {ConnectionId}", identity.Username, connection.ConnectionId);
            await OnAuthenticatedAsync(connection);
        }

        await connection.SendAsync(MessageTypes.AuthResponse, new AuthResponse
        {
            ok = identity != null,
            error = error,
            token = token,
            playerId = identity?.PlayerId,
            username = identity?.Username
        }, cancellation);
    }

    private async Task HandleRoomCreateAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        if (connection.CurrentRoom != null)
        {
            await connection.SendErrorAsync(ErrorCodes.AlreadyInRoom, "Sei già in una stanza.", cancellation);
            return;
        }

        var request = ClientConnection.ParsePayload<CreateRoomRequest>(envelope);
        PvpLoadout loadout = await ValidateLoadoutAsync(connection, request?.loadout, cancellation);
        if (loadout == null)
            return;

        Room room = rooms.Create(connection, loadout);
        logger.LogInformation("Stanza {Code} creata da '{Username}'", room.Code, connection.Identity.Username);
        await connection.SendAsync(MessageTypes.RoomCreated, new RoomCreated { code = room.Code }, cancellation);
    }

    private async Task HandleRoomJoinAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        if (connection.CurrentRoom != null)
        {
            await connection.SendErrorAsync(ErrorCodes.AlreadyInRoom, "Sei già in una stanza.", cancellation);
            return;
        }

        var request = ClientConnection.ParsePayload<JoinRoomRequest>(envelope);
        PvpLoadout loadout = await ValidateLoadoutAsync(connection, request?.loadout, cancellation);
        if (loadout == null)
            return;

        if (!rooms.TryJoin(request.code, connection, loadout, out Room room))
        {
            await connection.SendErrorAsync(
                ErrorCodes.RoomNotFound, "Stanza inesistente o già piena.", cancellation);
            return;
        }

        logger.LogInformation(
            "'{Guest}' è entrato nella stanza {Code} di '{Host}'",
            connection.Identity.Username, room.Code, room.Host.Identity.Username);
        await StartMatchAsync(room, cancellation);
    }

    private RankedData BuildRankedData(ClientConnection connection)
    {
        RankedProgress progress = ranked.GetProgress(connection.Identity.PlayerId, seasons.ActiveSeasonId);
        return new RankedData
        {
            ranked = progress.Ranked,
            placement = progress.Ranked && !progress.PlacementDone,
            placementRemaining = progress.PlacementRemaining,
            tier = progress.Tier.TierName,
            division = progress.Tier.Division,
            leaguePoints = progress.Tier.LeaguePoints,
            seasonId = seasons.ActiveSeasonId,
            seasonName = seasons.ActiveSeasonName
        };
    }

    private LeaderboardData BuildLeaderboardData()
    {
        IReadOnlyList<LeaderboardRow> rows = ranked.GetLeaderboard(seasons.ActiveSeasonId, LeaderboardLimit);
        var entries = new LeaderboardEntry[rows.Count];
        for (int index = 0; index < rows.Count; index++)
        {
            LeaderboardRow row = rows[index];
            bool inPlacement = !row.PlacementDone;
            entries[index] = new LeaderboardEntry
            {
                rank = index + 1,
                playerId = row.PlayerId,
                username = row.Username,
                tier = row.Tier.TierName,
                division = row.Tier.Division,
                leaguePoints = row.Tier.LeaguePoints,
                placement = inPlacement
            };
        }
        return new LeaderboardData
        {
            seasonId = seasons.ActiveSeasonId,
            seasonName = seasons.ActiveSeasonName,
            entries = entries
        };
    }

    private async Task OnAuthenticatedAsync(ClientConnection connection)
    {
        presence.Register(connection);
        await NotifyFriendsPresenceAsync(connection.Identity.PlayerId, PresenceRegistry.Online);
    }

    private async Task NotifyFriendsPresenceAsync(string playerId, string presenceStatus)
    {
        var update = new FriendPresenceUpdate { playerId = playerId, presence = presenceStatus };
        foreach (string friendId in friends.GetAcceptedFriendIds(playerId))
        {
            ClientConnection friendConnection = presence.GetConnection(friendId);
            if (friendConnection != null)
                await friendConnection.SendAsync(MessageTypes.FriendPresence, update);
        }
    }

    private async Task ApplyFriendActionAsync(
        ClientConnection connection, FriendActionResult result, CancellationToken cancellation)
    {
        if (!result.Ok)
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidAction, result.Error, cancellation);
            return;
        }

        await connection.SendAsync(
            MessageTypes.FriendsData, friends.GetFriends(connection.Identity), cancellation);

        // Aggiorna anche la lista dell'altro giocatore se è online.
        ClientConnection other = presence.GetConnection(result.OtherPlayerId);
        if (other != null)
            await other.SendAsync(MessageTypes.FriendsData, friends.GetFriends(other.Identity), cancellation);
    }

    private async Task HandleFriendChallengeAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        if (connection.CurrentRoom != null)
        {
            await connection.SendErrorAsync(ErrorCodes.AlreadyInRoom, "Sei già in una stanza.", cancellation);
            return;
        }

        var request = ClientConnection.ParsePayload<FriendChallengeRequest>(envelope);
        if (string.IsNullOrEmpty(request?.playerId))
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidAction, "Amico non valido.", cancellation);
            return;
        }
        if (!friends.AreFriends(connection.Identity.PlayerId, request.playerId))
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidAction, "Non è tra i tuoi amici.", cancellation);
            return;
        }

        ClientConnection target = presence.GetConnection(request.playerId);
        if (target == null)
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidAction, "L'amico non è online.", cancellation);
            return;
        }
        if (target.CurrentRoom != null)
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidAction, "L'amico è già occupato.", cancellation);
            return;
        }

        PvpLoadout loadout = await ValidateLoadoutAsync(connection, request.loadout, cancellation);
        if (loadout == null)
            return;

        Room room = rooms.Create(connection, loadout);
        await connection.SendAsync(MessageTypes.RoomCreated, new RoomCreated { code = room.Code }, cancellation);
        await target.SendAsync(MessageTypes.FriendChallengeReceived, new FriendChallengeReceived
        {
            roomCode = room.Code,
            challengerId = connection.Identity.PlayerId,
            challengerName = connection.Identity.Username
        }, cancellation);
        logger.LogInformation(
            "'{Challenger}' sfida '{Target}' nella stanza {Code}",
            connection.Identity.Username, target.Identity.Username, room.Code);
    }

    private async Task HandleSetIconAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<SetIconRequest>(envelope);
        if (profiles.TrySetIcon(connection.Identity, request?.iconId, out string error))
            await connection.SendAsync(
                MessageTypes.ProfileData, profiles.GetProfile(connection.Identity), cancellation);
        else
            await connection.SendErrorAsync(ErrorCodes.InvalidAction, error, cancellation);
    }

    private async Task HandleCampaignKillsAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<CampaignKillsRequest>(envelope);
        achievements.UnlockCampaignBossVictory(connection.Identity, request?.bosses);
        IReadOnlyList<string> unlocked =
            profiles.ReportCampaignKills(connection.Identity, request?.monsters, request?.bosses);
        await connection.SendAsync(MessageTypes.CampaignKillsResult, new CampaignKillsResult
        {
            newlyUnlocked = unlocked.ToArray()
        }, cancellation);
    }

    private async Task HandleQueueJoinAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        if (connection.CurrentRoom != null)
        {
            await connection.SendErrorAsync(ErrorCodes.AlreadyInRoom, "Sei già in una stanza.", cancellation);
            return;
        }

        var request = ClientConnection.ParsePayload<QueueJoinRequest>(envelope);
        PvpLoadout loadout = await ValidateLoadoutAsync(connection, request?.loadout, cancellation);
        if (loadout == null)
            return;

        int mmr = ranked.GetProgress(connection.Identity.PlayerId, seasons.ActiveSeasonId).Mmr;
        (QueueEntry first, QueueEntry second)? pair = queue.Enqueue(connection, loadout, mmr);
        if (pair == null)
        {
            await connection.SendAsync(MessageTypes.QueueStatus, new QueueStatus
            {
                queued = true,
                position = queue.PositionOf(connection)
            }, cancellation);
            return;
        }

        Room room = rooms.Create(pair.Value.first.Connection, pair.Value.first.Loadout);
        room.Ranked = true;
        room.TrySeatGuest(pair.Value.second.Connection, pair.Value.second.Loadout);
        pair.Value.second.Connection.CurrentRoom = room;
        logger.LogInformation(
            "Matchmaking: '{A}' vs '{B}' nella stanza {Code}",
            room.Host.Identity.Username, room.Guest.Identity.Username, room.Code);
        await StartMatchAsync(room, cancellation);
    }

    private async Task HandleRoomLeaveAsync(ClientConnection connection, CancellationToken cancellation)
    {
        Room room = connection.CurrentRoom;
        if (room == null)
            return;

        ClientConnection opponent = room.OpponentOf(connection);
        bool finished = room.Session?.IsFinished ?? false;
        if (room.Session != null)
            await room.Session.RecordDisconnectAsync(connection);
        room.Session?.Shutdown();
        rooms.Remove(room);

        // Chi lascia torna al menu: notifica agli amici il ritorno "online".
        if (connection.IsOpen)
            await NotifyFriendsPresenceAsync(connection.Identity.PlayerId, PresenceRegistry.Online);

        if (!finished && opponent is { IsOpen: true })
            await opponent.SendAsync(MessageTypes.MatchOpponentLeft, new ErrorMessage
            {
                code = "opponent_left",
                message = "L'avversario ha lasciato la partita."
            }, cancellation);
    }

    private async Task StartMatchAsync(Room room, CancellationToken cancellation)
    {
        foreach (ClientConnection player in new[] { room.Host, room.Guest })
        {
            await player.SendAsync(MessageTypes.MatchFound, new MatchFound
            {
                opponentName = room.OpponentOf(player).Identity.Username,
                roomCode = room.Code
            }, cancellation);
        }

        room.Session = new MatchSession(room, config, resultRecorder);
        await NotifyFriendsPresenceAsync(room.Host.Identity.PlayerId, PresenceRegistry.InMatch);
        await NotifyFriendsPresenceAsync(room.Guest.Identity.PlayerId, PresenceRegistry.InMatch);
        await room.Session.StartAsync(cancellation);
    }

    private async Task<PvpLoadout> ValidateLoadoutAsync(
        ClientConnection connection, PvpLoadoutDto dto, CancellationToken cancellation)
    {
        if (dto == null)
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidLoadout, "Loadout mancante.", cancellation);
            return null;
        }

        PvpLoadout loadout;
        try
        {
            loadout = dto.ToLoadout();
        }
        catch (ArgumentException exception)
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidLoadout, exception.Message, cancellation);
            return null;
        }

        PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, loadoutRules);
        if (!result.IsValid)
        {
            string details = string.Join(" ", result.Errors.Select(error => error.Message));
            await connection.SendErrorAsync(ErrorCodes.InvalidLoadout, details, cancellation);
            return null;
        }

        if (!cardCatalog.TryValidate(loadout, out string catalogError))
        {
            await connection.SendErrorAsync(ErrorCodes.InvalidLoadout, catalogError, cancellation);
            return null;
        }

        return loadout;
    }

    private async Task OnDisconnectedAsync(ClientConnection connection)
    {
        logger.LogInformation("Connessione chiusa {ConnectionId}", connection.ConnectionId);
        queue.Remove(connection);

        if (connection.Identity != null)
        {
            presence.Unregister(connection);
            await NotifyFriendsPresenceAsync(connection.Identity.PlayerId, PresenceRegistry.Offline);
        }

        Room room = connection.CurrentRoom;
        if (room == null)
            return;

        ClientConnection opponent = room.OpponentOf(connection);
        bool finished = room.Session?.IsFinished ?? false;
        if (room.Session != null)
            await room.Session.RecordDisconnectAsync(connection);
        room.Session?.Shutdown();
        rooms.Remove(room);
        if (!finished && opponent is { IsOpen: true })
            await opponent.SendAsync(MessageTypes.MatchOpponentLeft, new ErrorMessage
            {
                code = "opponent_left",
                message = "L'avversario ha lasciato la partita."
            });
    }
}
