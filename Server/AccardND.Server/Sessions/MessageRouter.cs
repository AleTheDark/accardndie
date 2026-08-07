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
    private readonly SessionTokenRegistry sessionTokens;
    private readonly RequestDedupStore requestDedup;
    private readonly ClientVersionGate clientVersions;
    private readonly ILogger<MessageRouter> logger;

    /// <summary>
    /// Messaggi che cambiano lo stato del giocatore e che quindi non possono essere
    /// eseguiti due volte. Un client che rinvia dopo una riconnessione ripete lo
    /// stesso requestId: qui la seconda copia riceve la risposta memorizzata invece
    /// di ricomprare l'oggetto o riscuotere di nuovo la quest.
    /// </summary>
    private static readonly HashSet<string> DedupedMessageTypes = new(StringComparer.Ordinal)
    {
        MessageTypes.SinglePlayerPurchaseUnlock,
        MessageTypes.SinglePlayerClaimTutorialReward,
        MessageTypes.SinglePlayerClaimDeathReward,
        MessageTypes.SinglePlayerClaimAdMultiplier,
        MessageTypes.SinglePlayerClearChapter,
        MessageTypes.SanctuaryBuyItem,
        MessageTypes.SanctuarySetBag,
        MessageTypes.TavernClaimQuest,
        MessageTypes.TavernClaimBonus,
        MessageTypes.NicknameSet,
        MessageTypes.ProfileSetIcon,
        MessageTypes.CampaignReportKills,
        MessageTypes.MatchAction,
        MessageTypes.FriendAdd,
        MessageTypes.FriendRespond,
        MessageTypes.FriendRemove,
        MessageTypes.FriendBlock
    };

    /// <summary>
    /// Un account, una sessione: il testo che legge chi resta fuori. Lo usano sia la
    /// sloggatura in diretta sia il rifiuto di un riaggancio già sostituito.
    /// </summary>
    private const string SessionUsedElsewhereMessage = "Il tuo account è stato usato su un altro dispositivo.";

    private const int LeaderboardLimit = 50;
    private const int HallOfFameLimit = 50;
    private const int RoomListLimit = 30;

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
        SessionTokenRegistry sessionTokens,
        RequestDedupStore requestDedup,
        ClientVersionGate clientVersions,
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
        this.sessionTokens = sessionTokens;
        this.requestDedup = requestDedup;
        this.clientVersions = clientVersions;
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

    /// <summary>
    /// Esegue il messaggio dentro l'ambito di risposta, così la risposta esce correlata
    /// al requestId del client. Per i messaggi che mutano lo stato tiene anche memoria
    /// dell'esito: un rinvio dopo una riconnessione va servito, non rieseguito.
    /// </summary>
    private async Task DispatchAsync(ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        string requestId = envelope.requestId;
        if (string.IsNullOrEmpty(requestId))
        {
            await RouteAsync(connection, envelope, cancellation);
            return;
        }

        bool deduped = DedupedMessageTypes.Contains(envelope.type ?? string.Empty);
        string playerId = connection.Identity?.PlayerId;
        if (deduped
            && playerId != null
            && requestDedup.TryGet(playerId, requestId, out RequestDedupStore.CachedReply cached))
        {
            logger.LogInformation(
                "Richiesta {Type} ripetuta da '{User}': risposta rigiocata senza rieseguire.",
                envelope.type, connection.Identity.Username);
            connection.BeginReplyScope(requestId);
            await connection.SendRawAsync(cached.Type, cached.Payload, cancellation);
            connection.EndReplyScope(out _);
            return;
        }

        connection.BeginReplyScope(requestId);
        try
        {
            await RouteAsync(connection, envelope, cancellation);
        }
        finally
        {
            if (connection.EndReplyScope(out RequestDedupStore.CachedReply reply) && deduped)
            {
                // L'id account può nascere proprio da questo messaggio (login): rileggilo.
                requestDedup.Store(connection.Identity?.PlayerId ?? playerId, requestId, reply);
            }
        }
    }

    private async Task RouteAsync(ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        switch (envelope.type)
        {
            case MessageTypes.AuthRegister:
            case MessageTypes.AuthLogin:
            case MessageTypes.AuthUgs:
            case MessageTypes.AuthSession:
                if (await RejectOutdatedClientAsync(connection, envelope, cancellation))
                    return;
                break;
        }

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
            case MessageTypes.AuthSession:
                await HandleSessionResumeAsync(connection, envelope, cancellation);
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
            case MessageTypes.RoomsList:
                await connection.SendAsync(MessageTypes.RoomsData, BuildRoomsData(), cancellation);
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
            case MessageTypes.SanctuaryGet:
                await connection.SendAsync(
                    MessageTypes.SanctuaryData,
                    singlePlayerProgress.GetSanctuary(connection.Identity),
                    cancellation);
                break;
            case MessageTypes.TavernGet:
                await connection.SendAsync(
                    MessageTypes.TavernData,
                    singlePlayerProgress.GetTavern(connection.Identity),
                    cancellation);
                break;
            case MessageTypes.TavernClaimQuest:
            {
                var request = ClientConnection.ParsePayload<TavernClaimQuestRequest>(envelope);
                await SendTavernResultAsync(
                    connection, singlePlayerProgress.ClaimTavernQuest(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.TavernClaimBonus:
                await SendTavernResultAsync(
                    connection, singlePlayerProgress.ClaimTavernBonus(connection.Identity), cancellation);
                break;
            case MessageTypes.SanctuaryBuyItem:
            {
                var request = ClientConnection.ParsePayload<SanctuaryBuyItemRequest>(envelope);
                await SendSanctuaryResultAsync(
                    connection, singlePlayerProgress.BuyItem(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SanctuarySetBag:
            {
                var request = ClientConnection.ParsePayload<SanctuarySetBagRequest>(envelope);
                await SendSanctuaryResultAsync(
                    connection, singlePlayerProgress.SetBag(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SinglePlayerPurchaseUnlock:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerPurchaseUnlockRequest>(envelope);
                await SendProgressResultAsync(
                    connection, singlePlayerProgress.PurchaseUnlock(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SinglePlayerClearChapter:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerClearChapterRequest>(envelope);
                await SendProgressResultAsync(
                    connection, singlePlayerProgress.ClearChapter(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SinglePlayerClaimTutorialReward:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerTutorialRewardRequest>(envelope);
                await SendRewardResultAsync(
                    connection, singlePlayerProgress.ClaimTutorialReward(connection.Identity, request), cancellation);
                break;
            }
            case MessageTypes.SinglePlayerRunStarted:
            {
                var request = ClientConnection.ParsePayload<SinglePlayerRunStartRequest>(envelope);
                await connection.SendAsync(
                    MessageTypes.SinglePlayerRunStartedAck,
                    singlePlayerProgress.RecordRunStart(connection.Identity, request),
                    cancellation);
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
            case MessageTypes.SinglePlayerPendingAdRewardsGet:
                await connection.SendAsync(
                    MessageTypes.SinglePlayerPendingAdRewardsData,
                    singlePlayerProgress.GetPendingAdRewards(connection.Identity),
                    cancellation);
                break;
            case MessageTypes.SinglePlayerClaimLevelRewards:
            {
                await SendRewardResultAsync(
                    connection, singlePlayerProgress.ClaimLevelRewards(connection.Identity), cancellation);
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
            case MessageTypes.MatchForfeit:
            {
                MatchSession session = connection.CurrentRoom?.Session;
                if (session == null)
                {
                    await connection.SendErrorAsync(
                        ErrorCodes.NotInMatch, "Nessun match in corso.", cancellation);
                    break;
                }
                if (await session.ForfeitAsync(connection, cancellation))
                {
                    logger.LogInformation(
                        "'{User}' si è arreso nella stanza {Code}.",
                        connection.Identity.Username, connection.CurrentRoom.Code);
                }
                break;
            }
            default:
                await connection.SendErrorAsync(
                    ErrorCodes.InvalidMessage, $"Tipo messaggio sconosciuto: {envelope.type}", cancellation);
                break;
        }
    }

    private static async Task SendSanctuaryResultAsync(
        ClientConnection connection,
        (SanctuaryData Data, string ErrorCode, string Error) outcome,
        CancellationToken cancellation)
    {
        if (outcome.Data == null)
        {
            await connection.SendErrorAsync(outcome.ErrorCode, outcome.Error, cancellation);
            return;
        }

        await connection.SendAsync(MessageTypes.SanctuaryData, outcome.Data, cancellation);
    }

    private static async Task SendTavernResultAsync(
        ClientConnection connection,
        (TavernData Data, string ErrorCode, string Error) outcome,
        CancellationToken cancellation)
    {
        if (outcome.Data == null)
        {
            await connection.SendErrorAsync(outcome.ErrorCode, outcome.Error, cancellation);
            return;
        }

        await connection.SendAsync(MessageTypes.TavernData, outcome.Data, cancellation);
    }

    private static async Task SendProgressResultAsync(
        ClientConnection connection,
        (SinglePlayerProgressData Progress, string ErrorCode, string Error) outcome,
        CancellationToken cancellation)
    {
        if (outcome.Progress == null)
        {
            await connection.SendErrorAsync(outcome.ErrorCode, outcome.Error, cancellation);
            return;
        }

        await connection.SendAsync(MessageTypes.SinglePlayerProgressData, outcome.Progress, cancellation);
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

    /// <summary>
    /// Cancello di versione: una build vecchia non entra, qualunque sia il modo in
    /// cui prova ad autenticarsi. La connessione resta aperta ma senza identità, e
    /// il client riceve un AuthResponse che dice esplicitamente "aggiorna" invece di
    /// un errore generico da riprovare all'infinito.
    /// </summary>
    private async Task<bool> RejectOutdatedClientAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        string clientVersion = ClientConnection.ParsePayload<ClientVersionInfo>(envelope)?.clientVersion;
        if (clientVersions.IsAccepted(clientVersion))
            return false;

        string target = clientVersions.Target;
        logger.LogInformation(
            "Accesso rifiutato su {ConnectionId}: client '{Client}' invece di '{Target}'.",
            connection.ConnectionId,
            string.IsNullOrWhiteSpace(clientVersion) ? "(non dichiarata)" : clientVersion,
            target);

        await connection.SendAsync(MessageTypes.AuthResponse, new AuthResponse
        {
            ok = false,
            error = $"Versione del gioco non aggiornata: serve la {target}. Aggiorna per continuare.",
            requiresUpdate = true,
            requiredVersion = target,
            updateUrl = clientVersions.UpdateUrl
        }, cancellation);
        return true;
    }

    private async Task HandleUgsAuthAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<UgsLoginRequest>(envelope);
        (VerifiedExternalIdentity externalIdentity, string error) =
            await ugsAuth.ValidateAsync(
                request?.accessToken, request?.displayName, request?.authMethod, request?.googleIdToken);

        ExternalAccountResult resolved = externalIdentity == null
            ? new ExternalAccountResult(null, false, false, error)
            : accounts.ResolveExternalAccount(externalIdentity);
        AccountIdentity identity = resolved.Identity;
        error = resolved.Error ?? error;

        // Il token va emesso prima dell'aggancio: serve a distinguere questa sessione
        // da quella già collegata, che altrimenti verrebbe sloggata anche quando è
        // solo il rientro dello stesso client.
        string sessionToken = sessionTokens.Issue(identity);
        if (identity != null)
        {
            connection.Identity = identity;
            connection.SessionToken = sessionToken;
            logger.LogInformation(
                "Autenticato via {Provider}/{Method} '{Username}' ({PlayerId}) su {ConnectionId}",
                externalIdentity.Provider, externalIdentity.AuthMethod,
                identity.Username, identity.PlayerId, connection.ConnectionId);
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
            requiresNickname = resolved.RequiresNickname,
            sessionToken = sessionToken
        }, cancellation);

        if (identity != null)
            await TryResumeMatchAsync(connection, cancellation);
    }

    /// <summary>
    /// Riaggancio di una sessione caduta: nessun giro da Google/UGS, solo il token
    /// emesso all'ultimo login. È la strada veloce, quella che deve funzionare
    /// mentre la rete è ancora ballerina.
    /// </summary>
    private async Task HandleSessionResumeAsync(
        ClientConnection connection, Envelope envelope, CancellationToken cancellation)
    {
        var request = ClientConnection.ParsePayload<SessionResumeRequest>(envelope);
        AccountIdentity issued = sessionTokens.Resolve(request?.sessionToken);

        // L'account può essere cambiato (nickname) dopo l'emissione del token.
        (AccountIdentity identity, bool requiresNickname) = accounts.Reload(issued);

        // Il token non c'è più perché l'account è entrato da un'altra parte, e questo
        // client non ha mai letto l'avviso: glielo diamo adesso. Rispondere solo
        // "sessione scaduta" lo farebbe ripiegare sul login completo, rientrando come
        // sessione nuova e sloggando il dispositivo su cui si sta giocando ora.
        bool wasSuperseded = identity == null && sessionTokens.WasSuperseded(request?.sessionToken);
        if (wasSuperseded)
        {
            logger.LogInformation(
                "Riaggancio rifiutato su {ConnectionId}: quella sessione era già stata chiusa da un altro dispositivo.",
                connection.ConnectionId);
            await connection.SendAsync(
                MessageTypes.SessionKicked,
                new SessionKickedMessage { message = SessionUsedElsewhereMessage },
                cancellation);
        }

        if (identity != null)
        {
            connection.Identity = identity;
            // Stesso token della connessione caduta: è lo stesso client che rientra,
            // non un secondo dispositivo.
            connection.SessionToken = request.sessionToken;
            logger.LogInformation(
                "Sessione ripresa da '{Username}' ({PlayerId}) su {ConnectionId}",
                identity.Username, identity.PlayerId, connection.ConnectionId);
            await OnAuthenticatedAsync(connection);
        }

        await connection.SendAsync(MessageTypes.AuthResponse, new AuthResponse
        {
            ok = identity != null,
            error = identity != null
                ? null
                : wasSuperseded
                    ? SessionUsedElsewhereMessage
                    : "Sessione scaduta: rifai l'accesso.",
            playerId = identity?.PlayerId,
            username = identity?.Username,
            requiresNickname = requiresNickname,
            sessionToken = identity != null ? request.sessionToken : null
        }, cancellation);

        if (identity != null)
            await TryResumeMatchAsync(connection, cancellation);
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

        string sessionToken = sessionTokens.Issue(identity);
        if (identity != null)
        {
            connection.Identity = identity;
            connection.SessionToken = sessionToken;
            logger.LogInformation("Autenticato '{Username}' su {ConnectionId}", identity.Username, connection.ConnectionId);
            await OnAuthenticatedAsync(connection);
        }

        await connection.SendAsync(MessageTypes.AuthResponse, new AuthResponse
        {
            ok = identity != null,
            error = error,
            token = token,
            playerId = identity?.PlayerId,
            username = identity?.Username,
            sessionToken = sessionToken
        }, cancellation);

        if (identity != null)
            await TryResumeMatchAsync(connection, cancellation);
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

        Room room = rooms.Create(
            connection,
            loadout,
            SanitizeRoomName(request?.roomName),
            request?.mode,
            request?.isPublic == true ? RoomVisibility.Public : RoomVisibility.Protected);

        RankedProgress hostProgress = ranked.GetProgress(connection.Identity.PlayerId, seasons.ActiveSeasonId);
        if (hostProgress.Ranked && hostProgress.PlacementDone)
        {
            room.HostTier = hostProgress.Tier.TierName;
            room.HostDivision = hostProgress.Tier.Division;
        }

        logger.LogInformation(
            "Stanza {Code} '{Name}' ({Mode}, {Visibility}) creata da '{Username}'",
            room.Code, room.Name, room.Mode, room.Visibility, connection.Identity.Username);
        await connection.SendAsync(MessageTypes.RoomCreated, new RoomCreated
        {
            code = room.Code,
            roomName = room.Name,
            mode = room.Mode,
            isPublic = room.Visibility == RoomVisibility.Public
        }, cancellation);
    }

    /// <summary>Ripulisce il nome scelto dall'host: niente caratteri di controllo, lunghezza limitata.</summary>
    private static string SanitizeRoomName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var builder = new System.Text.StringBuilder(RoomModes.NameMaxLength);
        foreach (char character in raw.Trim())
        {
            if (builder.Length >= RoomModes.NameMaxLength)
                break;
            if (!char.IsControl(character))
                builder.Append(character);
        }
        string cleaned = builder.ToString().Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private RoomsData BuildRoomsData()
    {
        IReadOnlyList<Room> open = rooms.ListOpen(RoomListLimit);
        var summaries = new RoomSummary[open.Count];
        for (int index = 0; index < open.Count; index++)
        {
            Room room = open[index];
            bool isPublic = room.Visibility == RoomVisibility.Public;
            summaries[index] = new RoomSummary
            {
                // Il codice viaggia solo per le stanze aperte: quelle protette restano da indovinare.
                code = isPublic ? room.Code : null,
                roomName = room.Name,
                mode = room.Mode,
                hostUsername = room.Host.Identity.Username,
                hostTier = room.HostTier,
                hostDivision = room.HostDivision,
                isPublic = isPublic,
                players = room.IsFull ? 2 : 1,
                capacity = 2
            };
        }
        return new RoomsData { rooms = summaries };
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
                selectedIconId = row.SelectedIconId,
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
        await CloseOtherSessionAsync(connection);
        presence.Register(connection);
        await NotifyFriendsPresenceAsync(connection.Identity.PlayerId, PresenceRegistry.Online);
    }

    /// <summary>
    /// Un account, una sessione. Se il giocatore era già collegato, la connessione
    /// vecchia viene chiusa qui — prima di registrare la nuova, altrimenti la
    /// cercheremmo nel registro e troveremmo se stessi.
    ///
    /// Due casi diversi: stesso token di sessione significa che è lo stesso client
    /// che rientra dopo una caduta di rete e il socket vecchio è solo uno zombie da
    /// chiudere in silenzio; token diverso significa un altro dispositivo, e allora
    /// chi era dentro va avvisato prima di essere sloggato.
    /// </summary>
    private async Task CloseOtherSessionAsync(ClientConnection connection)
    {
        ClientConnection previous = presence.GetConnection(connection.Identity.PlayerId);
        if (previous == null || ReferenceEquals(previous, connection))
            return;

        bool sameSession = !string.IsNullOrEmpty(previous.SessionToken)
            && string.Equals(previous.SessionToken, connection.SessionToken, StringComparison.Ordinal);

        try
        {
            if (!sameSession)
            {
                logger.LogInformation(
                    "'{Username}' è entrato da un'altra parte: chiudo la sessione {Old}.",
                    connection.Identity.Username, previous.ConnectionId);
                await previous.SendAsync(MessageTypes.SessionKicked, new SessionKickedMessage
                {
                    message = SessionUsedElsewhereMessage
                });
                // Senza revoca il client sloggato rientrerebbe da solo con la
                // riconnessione automatica, sbattendo fuori a sua volta il nuovo.
                sessionTokens.Revoke(previous.SessionToken);
            }

            await previous.CloseAsync();
        }
        catch (Exception exception)
        {
            // Il socket vecchio può essere già morto: non deve far fallire il login
            // di chi sta entrando adesso.
            logger.LogDebug("Chiusura della sessione precedente non riuscita: {Message}", exception.Message);
        }
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

        Room room = rooms.Create(connection, loadout, $"Sfida a {target.Identity.Username}");
        await connection.SendAsync(MessageTypes.RoomCreated, new RoomCreated
        {
            code = room.Code,
            roomName = room.Name,
            mode = room.Mode,
            isPublic = false
        }, cancellation);
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
            // Il giocatore può essere già rientrato da un altro socket: la chiusura di
            // quello vecchio può arrivare con parecchio ritardo (su mobile la rete
            // sparita se ne accorge tardi) e non deve dirlo offline agli amici.
            if (!presence.IsOnline(connection.Identity.PlayerId))
                await NotifyFriendsPresenceAsync(connection.Identity.PlayerId, PresenceRegistry.Offline);
        }

        Room room = connection.CurrentRoom;
        if (room == null)
            return;

        // Stessa storia per la stanza: se questa connessione è già stata sostituita da
        // un rientro, la partita è viva e non va toccata.
        if (room.Host != connection && room.Guest != connection)
            return;

        // Un socket che cade non è un abbandono: prima si aspetta il rientro.
        if (await TryBeginReconnectWaitAsync(room, connection))
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

    // --- Grazia di riconnessione ---

    /// <summary>
    /// Mette la partita in pausa e apre la finestra di rientro, invece di trasformare
    /// la caduta del socket in una sconfitta immediata. false quando non c'è niente da
    /// salvare: partita già finita, nessun avversario in attesa, grazia disattivata o
    /// budget di riconnessione esaurito.
    /// </summary>
    private async Task<bool> TryBeginReconnectWaitAsync(Room room, ClientConnection connection)
    {
        string playerId = connection.Identity?.PlayerId;
        if (playerId == null)
            return false;
        if (room.Session == null || room.Session.IsFinished)
            return false;
        // Se anche l'altro è già fuori non c'è nessuno per cui tenere in piedi il tavolo.
        if (room.IsAwaitingReconnect || room.OpponentOf(connection) is not { IsOpen: true })
            return false;

        // Quello che resta del budget di partita, non una finestra nuova di zecca:
        // il tempo delle cadute precedenti è già stato speso.
        int seconds = room.RemainingReconnectSeconds(playerId, config.DisconnectTimeoutSeconds);
        if (seconds <= 0)
            return false;

        var timer = new CancellationTokenSource();
        room.BeginReconnectWait(playerId, seconds, timer);
        if (!await room.Session.PauseForReconnectAsync(connection, seconds))
        {
            room.EndReconnectWait();
            return false;
        }

        logger.LogInformation(
            "Disconnessione di '{User}' nella stanza {Code}: {Seconds}s di grazia residua per rientrare.",
            connection.Identity.Username, room.Code, seconds);
        _ = ExpireReconnectWaitAsync(room, playerId, seconds, timer);
        return true;
    }

    /// <summary>Scaduta la finestra: la partita si chiude con la sconfitta di chi non è tornato.</summary>
    private async Task ExpireReconnectWaitAsync(
        Room room, string playerId, int seconds, CancellationTokenSource timer)
    {
        try
        {
            // Il CancellationTokenSource non viene liberato di proposito: il rientro
            // può chiamare Cancel() esattamente mentre questo metodo finisce, e una
            // Dispose() in mezzo trasformerebbe la corsa in un'eccezione.
            await Task.Delay(TimeSpan.FromSeconds(seconds), timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Il rientro può arrivare nello stesso istante: vince chi prende il timer.
        // E se nel frattempo è già cominciata un'altra attesa, questa scadenza è
        // vecchia e non ha più voce in capitolo.
        if (!room.TryEndReconnectWait(timer))
            return;

        try
        {
            bool finished = room.Session?.IsFinished ?? false;
            if (room.Session != null)
                await room.Session.RecordDisconnectByPlayerIdAsync(playerId);
            room.Session?.Shutdown();

            ClientConnection remaining =
                room.Host?.Identity?.PlayerId == playerId ? room.Guest : room.Host;
            rooms.Remove(room);

            if (!finished && remaining is { IsOpen: true })
            {
                await remaining.SendAsync(MessageTypes.MatchOpponentLeft, new ErrorMessage
                {
                    code = "opponent_left",
                    message = "L'avversario non è rientrato in tempo."
                });
            }
            logger.LogInformation(
                "Riconnessione scaduta nella stanza {Code}: vittoria a tavolino.", room.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Chiusura della stanza {Code} dopo la grazia fallita.", room.Code);
        }
    }

    /// <summary>
    /// Dopo l'autenticazione: se il giocatore ha una partita ancora viva, la riprende
    /// da dove l'aveva lasciata invece di ritrovarsi in lobby. Vale sia per il rientro
    /// atteso (finestra di grazia aperta) sia per quello che arriva prima che il server
    /// si accorga della caduta: chi riapre l'app torna comunque al suo tavolo.
    /// </summary>
    private async Task TryResumeMatchAsync(ClientConnection connection, CancellationToken cancellation)
    {
        string playerId = connection.Identity?.PlayerId;
        bool awaited = true;
        Room room = rooms.FindAwaitingReconnect(playerId);
        if (room == null)
        {
            awaited = false;
            room = rooms.FindLiveMatchOf(playerId);
        }
        if (room?.Session == null || room.Session.IsFinished)
            return;

        // Ferma il countdown e addebita il tempo passato offline: se era già scaduto
        // qui torna null e la stanza è persa.
        CancellationTokenSource timer = room.EndReconnectWait();
        if (awaited && timer == null)
            return;
        timer?.Cancel();

        if (!room.TryReplaceConnection(connection, out ClientConnection stale))
            return;
        if (stale != null && !ReferenceEquals(stale, connection))
            stale.CurrentRoom = null;
        connection.CurrentRoom = room;

        int graceLeft = room.RemainingReconnectSeconds(playerId, config.DisconnectTimeoutSeconds);
        if (!await room.Session.ResumeAsync(connection, graceLeft, cancellation))
            return;

        await NotifyFriendsPresenceAsync(playerId, PresenceRegistry.InMatch);
        logger.LogInformation(
            "'{User}' è rientrato nella stanza {Code}: {Seconds}s di grazia ancora disponibili.",
            connection.Identity.Username, room.Code, graceLeft);
    }
}
