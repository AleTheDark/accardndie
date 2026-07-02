using System.Security.Cryptography;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;

namespace AccardND.Server.Match;

/// <summary>
/// Pilota un PvpMatchEngine per una stanza: traduce le azioni dei client in
/// chiamate al motore e ne inoltra gli eventi. Il motore è l'unica autorità;
/// qui vive solo l'adattamento di rete.
/// </summary>
public sealed class MatchSession
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly PvpMatchEngine engine;
    private readonly ClientConnection[] connections;
    private readonly PvpLoadout[] loadouts;
    private readonly int turnTimerSeconds;
    private readonly int forfeitAfterTimeouts;
    private readonly int[] consecutiveTimeouts = new int[2];
    private CancellationTokenSource turnTimer;

    public MatchSession(Room room, ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(config);
        connections = new[] { room.Host, room.Guest };
        loadouts = new[] { room.HostLoadout, room.GuestLoadout };
        turnTimerSeconds = config.TurnTimerSeconds;
        forfeitAfterTimeouts = config.ForfeitAfterConsecutiveTimeouts;
        engine = new PvpMatchEngine(
            ToCombatCards(room.HostLoadout),
            ToCombatCards(room.GuestLoadout),
            config.ToMatchRules(),
            new SeededRandomSource(RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue)));
    }

    public bool IsFinished => engine.Phase == PvpMatchPhase.Finished;

    public async Task StartAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            for (int player = 0; player < 2; player++)
            {
                await connections[player].SendAsync(MessageTypes.MatchStart, new MatchStart
                {
                    opponentName = connections[1 - player].Identity.Username,
                    yourPlayerIndex = player
                }, cancellation);
            }
            await DispatchAsync(engine.Start(), cancellation);
            ScheduleTurnTimer();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task HandleActionAsync(
        ClientConnection sender, MatchActionDto action, CancellationToken cancellation)
    {
        int player = PlayerIndexOf(sender);
        if (player < 0 || action == null)
        {
            await sender.SendErrorAsync(ErrorCodes.InvalidAction, "Azione non valida.", cancellation);
            return;
        }

        await gate.WaitAsync(cancellation);
        try
        {
            IReadOnlyList<PvpEvent> events;
            try
            {
                events = Execute(player, action);
            }
            catch (PvpActionException exception)
            {
                await sender.SendErrorAsync(ErrorCodes.InvalidAction, exception.Message, cancellation);
                return;
            }
            consecutiveTimeouts[player] = 0;
            await DispatchAsync(events, cancellation);
            ScheduleTurnTimer();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Ferma i timer quando la stanza viene smantellata.</summary>
    public void Shutdown() => turnTimer?.Cancel();

    // --- Timer per mossa ---

    private void ScheduleTurnTimer()
    {
        turnTimer?.Cancel();
        if (turnTimerSeconds <= 0 || engine.Phase == PvpMatchPhase.Finished)
            return;
        var timer = new CancellationTokenSource();
        turnTimer = timer;
        _ = RunTurnTimerAsync(timer.Token);
    }

    private async Task RunTurnTimerAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(turnTimerSeconds), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await gate.WaitAsync(CancellationToken.None);
        try
        {
            if (token.IsCancellationRequested || engine.Phase == PvpMatchPhase.Finished)
                return;
            await ExecuteTimeoutAsync();
            ScheduleTurnTimer();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ExecuteTimeoutAsync()
    {
        var timedOut = new List<int>();
        var events = new List<PvpEvent>();

        switch (engine.Phase)
        {
            case PvpMatchPhase.Deployment:
            {
                int player = engine.ActivePlayer;
                events.AddRange(engine.Deploy(player, 0));
                timedOut.Add(player);
                break;
            }
            case PvpMatchPhase.Battle:
            {
                int player = engine.ActivePlayer;
                events.AddRange(engine.Pass(player));
                timedOut.Add(player);
                break;
            }
            case PvpMatchPhase.DecisiveSelection:
            {
                for (int player = 0; player < 2; player++)
                {
                    if (engine.HasDecisiveChoice(player))
                        continue;
                    events.AddRange(engine.SubmitDecisiveSelection(player, new[] { 0, 1, 2 }));
                    timedOut.Add(player);
                }
                break;
            }
            default:
                return;
        }

        foreach (int player in timedOut)
        {
            consecutiveTimeouts[player]++;
            await BroadcastAsync(new MatchEventDto
            {
                type = "ActionTimeout",
                player = player,
                amount = consecutiveTimeouts[player]
            }, CancellationToken.None);
        }
        await DispatchAsync(events, CancellationToken.None);

        foreach (int player in timedOut)
        {
            if (engine.Phase == PvpMatchPhase.Finished
                || forfeitAfterTimeouts <= 0
                || consecutiveTimeouts[player] < forfeitAfterTimeouts)
                continue;
            await BroadcastAsync(new MatchEventDto
            {
                type = "MatchForfeited",
                player = player,
                winner = 1 - player,
                reason = "timeout"
            }, CancellationToken.None);
            await DispatchAsync(engine.Forfeit(player), CancellationToken.None);
            break;
        }
    }

    private IReadOnlyList<PvpEvent> Execute(int player, MatchActionDto action)
    {
        int targetPlayer = action.targetIsEnemy ? 1 - player : player;
        return action.action switch
        {
            MatchActionDto.Deploy => engine.Deploy(player, action.handIndex),
            MatchActionDto.Ability => engine.UseAbility(player, targetPlayer, action.targetSlot),
            MatchActionDto.Attack => engine.Attack(player, action.targetSlot),
            MatchActionDto.Attach => engine.Attach(player, action.targetSlot),
            MatchActionDto.Pass => engine.Pass(player),
            MatchActionDto.Decisive => engine.SubmitDecisiveSelection(player, action.decisiveIndices),
            _ => throw new PvpActionException($"Azione sconosciuta: {action.action}")
        };
    }

    private async Task DispatchAsync(IReadOnlyList<PvpEvent> events, CancellationToken cancellation)
    {
        foreach (PvpEvent gameEvent in events)
        {
            // La mano è privata: al posto dell'evento broadcast si invia il contenuto al solo proprietario.
            if (gameEvent is HandReadyEvent handReady)
            {
                await connections[handReady.Player].SendAsync(
                    MessageTypes.MatchHand, BuildHand(handReady.Player), cancellation);
                continue;
            }

            await BroadcastAsync(PvpEventMapper.ToDto(gameEvent), cancellation);
        }
        if (engine.Phase == PvpMatchPhase.Finished)
            turnTimer?.Cancel();
    }

    private async Task BroadcastAsync(MatchEventDto dto, CancellationToken cancellation)
    {
        foreach (ClientConnection connection in connections)
        {
            if (connection.IsOpen)
                await connection.SendAsync(MessageTypes.MatchEvent, dto, cancellation);
        }
    }

    private MatchHand BuildHand(int player)
    {
        IReadOnlyList<int> hand = engine.HandOf(player);
        var indices = new int[hand.Count];
        var definitionIds = new string[hand.Count];
        for (int position = 0; position < hand.Count; position++)
        {
            indices[position] = hand[position];
            definitionIds[position] = engine.LoadoutCard(player, hand[position]).Id;
        }
        return new MatchHand
        {
            roundNumber = engine.MatchRound,
            handIndices = indices,
            handDefinitionIds = definitionIds
        };
    }

    private int PlayerIndexOf(ClientConnection connection)
    {
        if (connection == connections[0])
            return 0;
        if (connection == connections[1])
            return 1;
        return -1;
    }

    private static List<CombatCard> ToCombatCards(PvpLoadout loadout)
    {
        var cards = new List<CombatCard>(loadout.Cards.Count);
        foreach (PvpLoadoutCard card in loadout.Cards)
        {
            // NOTA anti-cheat: valore e classe sono dichiarati dal client.
            // Serve un catalogo carte server-side per verificarli (vedi roadmap).
            cards.Add(card.ToCombatCard());
        }
        return cards;
    }
}
