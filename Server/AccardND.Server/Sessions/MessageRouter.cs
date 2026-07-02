using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Match;
using AccardND.Server.Rooms;

namespace AccardND.Server.Sessions;

public sealed class MessageRouter
{
    private readonly ServerConfig config;
    private readonly AccountService accounts;
    private readonly RoomManager rooms;
    private readonly MatchmakingQueue queue;
    private readonly PvpLoadoutRules loadoutRules;
    private readonly ILogger<MessageRouter> logger;

    public MessageRouter(
        ServerConfig config,
        AccountService accounts,
        RoomManager rooms,
        MatchmakingQueue queue,
        ILogger<MessageRouter> logger)
    {
        this.config = config;
        this.accounts = accounts;
        this.rooms = rooms;
        this.queue = queue;
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
            case MessageTypes.RoomCreate:
                await HandleRoomCreateAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.RoomJoin:
                await HandleRoomJoinAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.QueueJoin:
                await HandleQueueJoinAsync(connection, envelope, cancellation);
                break;
            case MessageTypes.QueueLeave:
                queue.Remove(connection);
                await connection.SendAsync(
                    MessageTypes.QueueStatus, new QueueStatus { queued = false, position = 0 }, cancellation);
                break;
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

    private async Task HandleAuthAsync(
        ClientConnection connection, Envelope envelope, bool isRegistration, CancellationToken cancellation)
    {
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

        (QueueEntry first, QueueEntry second)? pair = queue.Enqueue(connection, loadout);
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
        room.TrySeatGuest(pair.Value.second.Connection, pair.Value.second.Loadout);
        pair.Value.second.Connection.CurrentRoom = room;
        logger.LogInformation(
            "Matchmaking: '{A}' vs '{B}' nella stanza {Code}",
            room.Host.Identity.Username, room.Guest.Identity.Username, room.Code);
        await StartMatchAsync(room, cancellation);
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

        room.Session = new MatchSession(room, config);
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

        return loadout;
    }

    private async Task OnDisconnectedAsync(ClientConnection connection)
    {
        logger.LogInformation("Connessione chiusa {ConnectionId}", connection.ConnectionId);
        queue.Remove(connection);

        Room room = connection.CurrentRoom;
        if (room == null)
            return;

        ClientConnection opponent = room.OpponentOf(connection);
        rooms.Remove(room);
        if (opponent is { IsOpen: true })
            await opponent.SendAsync(MessageTypes.MatchOpponentLeft, new ErrorMessage
            {
                code = "opponent_left",
                message = "L'avversario ha lasciato la partita."
            });
    }
}
