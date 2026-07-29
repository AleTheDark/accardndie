using System.Security.Cryptography;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
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

    /// <summary>
    /// Tutti gli eventi già trasmessi, in ordine. Chi rientra dopo una disconnessione
    /// li riapplica dall'inizio e ricostruisce la partita esatta: il client sa già
    /// farlo, è lo stesso codice che usa in diretta. Una partita intera sta in
    /// qualche centinaio di voci, quindi non serve di meglio.
    /// </summary>
    private readonly List<MatchEventDto> eventLog = new();
    private readonly ClientConnection[] connections;
    private readonly PvpLoadout[] loadouts;
    private readonly int turnTimerSeconds;
    private readonly int forfeitAfterTimeouts;
    private readonly int[] consecutiveTimeouts = new int[2];
    private readonly MatchResultRecorder resultRecorder;
    private readonly AccountIdentity[] identities;
    private readonly string roomCode;
    private readonly bool ranked;
    private CancellationTokenSource turnTimer;
    private DateTime startedAt;
    private bool resultRecorded;
    private bool paused;
    private string endedReason = "normal";

    public MatchSession(Room room, ServerConfig config, MatchResultRecorder resultRecorder)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(config);
        this.resultRecorder = resultRecorder;
        connections = new[] { room.Host, room.Guest };
        loadouts = new[] { room.HostLoadout, room.GuestLoadout };
        identities = new[] { room.Host.Identity, room.Guest.Identity };
        roomCode = room.Code;
        ranked = room.Ranked;
        turnTimerSeconds = config.ResolveTurnTimerSeconds(room.Mode);
        forfeitAfterTimeouts = config.ForfeitAfterConsecutiveTimeouts;
        engine = new PvpMatchEngine(
            ToCombatCards(room.HostLoadout),
            ToCombatCards(room.GuestLoadout),
            config.ToMatchRules(room.Mode),
            new SeededRandomSource(RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue)));
    }

    public bool IsFinished => engine.Phase == PvpMatchPhase.Finished;

    public async Task StartAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            startedAt = DateTime.UtcNow;
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
            if (paused)
            {
                // Mentre si aspetta il rientro dell'avversario la partita è ferma per
                // entrambi: chi è rimasto non deve poter avanzare da solo, o l'altro
                // tornerebbe su un tavolo diverso da quello che ha lasciato.
                await sender.SendErrorAsync(
                    ErrorCodes.MatchPaused,
                    "Partita in pausa: l'avversario si sta riconnettendo.",
                    cancellation);
                return;
            }

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
            // L'ACK deve essere la prima risposta nell'ambito requestId: così il
            // router può memorizzarlo e una copia rinviata non riesegue la mossa.
            await sender.SendAsync(
                MessageTypes.MatchActionAck,
                new MatchActionAck { accepted = true },
                cancellation);
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

    /// <summary>
    /// Chiude senza vincitore un match ancora aperto durante lo shutdown. La riga
    /// resta nello storico, ma non modifica il rating perché Winner è -1.
    /// </summary>
    public async Task RecordServerShutdownAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (resultRecorded)
                return;
            turnTimer?.Cancel();
            paused = true;
            await RecordResultAsync(-1, "server_shutdown");
        }
        finally
        {
            gate.Release();
        }
    }

    // --- Disconnessione e rientro ---

    /// <summary>
    /// Mette la partita in pausa in attesa che <paramref name="leaver"/> torni: ferma il
    /// timer di mossa (che altrimenti lo farebbe perdere per timeout mentre è offline)
    /// e avvisa l'avversario con il countdown. false se il match è già chiuso.
    /// </summary>
    public async Task<bool> PauseForReconnectAsync(ClientConnection leaver, int secondsRemaining)
    {
        await gate.WaitAsync();
        try
        {
            if (resultRecorded || engine.Phase == PvpMatchPhase.Finished)
                return false;
            int player = PlayerIndexOf(leaver);
            if (player < 0)
                return false;

            paused = true;
            turnTimer?.Cancel();

            ClientConnection opponent = connections[1 - player];
            if (opponent.IsOpen)
            {
                await opponent.SendAsync(MessageTypes.MatchOpponentDisconnected, new MatchOpponentDisconnected
                {
                    opponentName = identities[player].Username,
                    secondsRemaining = secondsRemaining
                }, CancellationToken.None);
            }
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Riaggancia il giocatore rientrato: gli spedisce lo stato completo, avvisa
    /// l'avversario e fa ripartire il timer di mossa.
    /// </summary>
    public async Task<bool> ResumeAsync(ClientConnection replacement, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            int player = IndexOfPlayerId(replacement?.Identity?.PlayerId);
            if (player < 0 || resultRecorded || engine.Phase == PvpMatchPhase.Finished)
                return false;

            connections[player] = replacement;
            paused = false;

            await replacement.SendAsync(MessageTypes.MatchResume, new MatchResumeState
            {
                opponentName = identities[1 - player].Username,
                yourPlayerIndex = player,
                events = eventLog.ToArray(),
                hand = BuildHand(player)
            }, cancellation);

            ClientConnection opponent = connections[1 - player];
            if (opponent.IsOpen)
            {
                await opponent.SendAsync(MessageTypes.MatchOpponentReconnected, new MatchOpponentReconnected
                {
                    opponentName = identities[player].Username
                }, cancellation);
            }

            ScheduleTurnTimer();
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Sconfitta per disconnessione scaduta: il giocatore non è più rappresentato da una connessione viva.</summary>
    public async Task RecordDisconnectByPlayerIdAsync(string playerId)
    {
        await gate.WaitAsync();
        try
        {
            if (resultRecorded)
                return;
            if (engine.Phase == PvpMatchPhase.Finished)
            {
                await RecordResultAsync(engine.MatchWinner, endedReason);
                return;
            }
            int loser = IndexOfPlayerId(playerId);
            if (loser < 0)
                return;
            await RecordResultAsync(1 - loser, "disconnect");
        }
        finally
        {
            gate.Release();
        }
    }

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
            endedReason = "timeout";
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
        {
            turnTimer?.Cancel();
            await RecordResultAsync(engine.MatchWinner, endedReason);
        }
    }

    /// <summary>Registra l'esito una sola volta. Chiamato a fine motore o alla disconnessione.</summary>
    private async Task RecordResultAsync(int winner, string reason)
    {
        if (resultRecorded || resultRecorder == null || startedAt == default)
            return;
        MatchRecordResult result;
        try
        {
            result = await resultRecorder.RecordAsync(new MatchOutcome(
                identities[0], identities[1], winner,
                engine.WinsOf(0), engine.WinsOf(1),
                ranked, reason, roomCode, startedAt, DateTime.UtcNow));
        }
        catch (Exception)
        {
            return;
        }
        resultRecorded = true;

        for (int player = 0; player < 2; player++)
        {
            if (!connections[player].IsOpen)
                continue;
            await connections[player].SendAsync(
                MessageTypes.MatchResult, BuildResultData(player, winner, reason, result), CancellationToken.None);
        }
    }

    private MatchResultData BuildResultData(int player, int winner, string reason, MatchRecordResult result)
    {
        PlayerRankedDelta delta = player == 0 ? result.A : result.B;
        IReadOnlyList<string> unlocked = player == 0 ? result.AchievementsA : result.AchievementsB;
        var data = new MatchResultData
        {
            youWon = winner == player,
            ranked = result.Ranked && delta != null,
            endedReason = reason,
            scoreYou = engine.WinsOf(player),
            scoreOpponent = engine.WinsOf(1 - player),
            unlockedAchievements = unlocked?.ToArray() ?? Array.Empty<string>()
        };
        if (data.ranked)
        {
            data.tier = delta.After.TierName;
            data.division = delta.After.Division;
            data.leaguePoints = delta.After.LeaguePoints;
            data.lpDelta = delta.LpDelta;
            data.promoted = delta.Promoted;
            data.demoted = delta.Demoted;
            data.placement = delta.Placement;
            data.placementRemaining = delta.PlacementRemaining;
        }
        return data;
    }

    /// <summary>
    /// Un giocatore ha lasciato prima della fine: assegna la vittoria all'avversario.
    /// No-op se il match è già concluso o già registrato.
    /// </summary>
    public async Task RecordDisconnectAsync(ClientConnection leaver)
    {
        await gate.WaitAsync();
        try
        {
            if (resultRecorded)
                return;
            if (engine.Phase == PvpMatchPhase.Finished)
            {
                await RecordResultAsync(engine.MatchWinner, endedReason);
                return;
            }
            int loser = PlayerIndexOf(leaver);
            if (loser < 0)
                return;
            await RecordResultAsync(1 - loser, "disconnect");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task BroadcastAsync(MatchEventDto dto, CancellationToken cancellation)
    {
        eventLog.Add(dto);
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

    /// <summary>
    /// Indice del giocatore per PlayerId: dopo una riconnessione l'oggetto connessione
    /// è nuovo, l'identità no.
    /// </summary>
    private int IndexOfPlayerId(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return -1;
        if (identities[0]?.PlayerId == playerId)
            return 0;
        if (identities[1]?.PlayerId == playerId)
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
