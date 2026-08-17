using System.Threading.Channels;
using AccardND.NetProtocol;

namespace AccardND.LoadTest;

/// <summary>
/// Partite PvP vere fra bot appaiati. Il bot gioca come gioca il server quando scade il
/// timer di turno (schiera la prima carta, in battaglia attacca o passa, al round decisivo
/// prende le prime tre): non e' un giocatore bravo, ma produce esattamente il traffico di
/// una partita vera - eventi in broadcast, timer, e a fine partita la scrittura del
/// risultato con MMR, statistiche e stagione.
/// </summary>
public sealed class PvpScenario
{
    private readonly Options options;
    private readonly Random random;

    public PvpScenario(Options options, Random random)
    {
        this.options = options;
        this.random = random;
    }

    /// <summary>
    /// Punto d'incontro fra i due bot di una coppia: l'host pubblica il codice della
    /// stanza, l'ospite lo aspetta. Con la coda ranked non serve, li appaia il server.
    /// </summary>
    public sealed class Pairing
    {
        private readonly Channel<string>[] codes;

        public Pairing(int pairs)
        {
            codes = new Channel<string>[Math.Max(pairs, 1)];
            for (int index = 0; index < codes.Length; index++)
                codes[index] = Channel.CreateBounded<string>(1);
        }

        public ValueTask PublishAsync(int pair, string code, CancellationToken cancellation) =>
            codes[pair].Writer.WriteAsync(code, cancellation);

        public async Task<string> WaitAsync(int pair, TimeSpan timeout, CancellationToken cancellation)
        {
            using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            window.CancelAfter(timeout);
            try
            {
                return await codes[pair].Reader.ReadAsync(window.Token);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new BotFailure("l'host della coppia non ha aperto la stanza in tempo");
            }
        }
    }

    /// <summary>Apre la stanza (host) o ci entra (ospite), poi gioca la partita fino alla fine.</summary>
    public async Task PlayMatchAsync(
        BotConnection bot, Pairing pairing, int pair, bool isHost, CancellationToken cancellation)
    {
        PvpLoadoutDto loadout = Loadouts.Warrior();

        if (options.PvpRanked)
        {
            Envelope queued = await bot.RequestAsync("queue.join", MessageTypes.QueueJoin,
                new QueueJoinRequest { loadout = loadout }, cancellation);
            if (queued.type == MessageTypes.Error)
                throw new BotFailure($"coda rifiutata: {BotConnection.Parse<ErrorMessage>(queued)?.message}");
        }
        else if (isHost)
        {
            Envelope created = await bot.RequestAsync("room.create", MessageTypes.RoomCreate,
                new CreateRoomRequest { loadout = loadout, roomName = "carico", isPublic = false },
                cancellation);
            var room = BotConnection.Parse<RoomCreated>(created);
            if (created.type == MessageTypes.Error || string.IsNullOrEmpty(room?.code))
                throw new BotFailure($"stanza rifiutata: {BotConnection.Parse<ErrorMessage>(created)?.message}");
            await pairing.PublishAsync(pair, room.code, cancellation);
        }
        else
        {
            string code = await pairing.WaitAsync(pair, TimeSpan.FromSeconds(60), cancellation);
            Envelope joined = await bot.RequestAsync("room.join", MessageTypes.RoomJoin,
                new JoinRoomRequest { code = code, loadout = loadout }, cancellation);
            if (joined.type == MessageTypes.Error)
                throw new BotFailure($"ingresso rifiutato: {BotConnection.Parse<ErrorMessage>(joined)?.message}");
        }

        await PlayUntilEndAsync(bot, cancellation);
    }

    private async Task PlayUntilEndAsync(BotConnection bot, CancellationToken cancellation)
    {
        int me = -1;

        // Gli slot avversari ancora in piedi, ricostruiti dagli eventi come fa il client.
        // Senza, il bot attaccherebbe a caso e meta' delle mosse sarebbe un rifiuto.
        var enemySlots = new HashSet<int>();

        // Il tetto e' una rete di sicurezza: una partita che non finisce piu' non deve
        // tenere occupato un bot per tutta la prova.
        using var matchWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        matchWindow.CancelAfter(TimeSpan.FromMinutes(12));

        try
        {
            await foreach (Envelope envelope in bot.Events.ReadAllAsync(matchWindow.Token))
            {
                switch (envelope.type)
                {
                    case MessageTypes.MatchStart:
                        me = BotConnection.Parse<MatchStart>(envelope)?.yourPlayerIndex ?? -1;
                        break;

                    case MessageTypes.MatchOpponentLeft:
                    case MessageTypes.SessionKicked:
                        return;

                    case MessageTypes.MatchEvent:
                    {
                        var move = BotConnection.Parse<MatchEventDto>(envelope);
                        if (move == null)
                            break;
                        if (move.type == "MatchEnded")
                            return;
                        Track(move, me, enemySlots);
                        await ReactAsync(bot, move, me, enemySlots, matchWindow.Token);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new BotFailure("partita non conclusa entro 12 minuti");
        }
    }

    /// <summary>Tiene aggiornato lo schieramento avversario leggendo gli eventi.</summary>
    private static void Track(MatchEventDto move, int me, HashSet<int> enemySlots)
    {
        switch (move.type)
        {
            case "RoundStarted":
                enemySlots.Clear();
                break;
            case "CardDeployed" when move.player != me && me >= 0:
                enemySlots.Add(move.slot);
                break;
            case "AttackResolved" when move.defenderEliminated && move.targetPlayer != me:
                enemySlots.Remove(move.targetSlot);
                break;
        }
    }

    /// <summary>Le tre situazioni in cui tocca a noi. Fuori da queste il bot resta a guardare.</summary>
    private async Task ReactAsync(
        BotConnection bot, MatchEventDto move, int me, HashSet<int> enemySlots, CancellationToken cancellation)
    {
        switch (move.type)
        {
            case "DeployTurn" when move.player == me:
                await ThinkAsync(cancellation);
                await bot.RequestAsync("match.action.deploy", MessageTypes.MatchAction,
                    new MatchActionDto { action = MatchActionDto.Deploy, handIndex = 0 }, cancellation);
                break;

            case "TurnStarted" when move.player == me:
            {
                await ThinkAsync(cancellation);
                if (enemySlots.Count > 0)
                {
                    int target = enemySlots.ElementAt(random.Next(enemySlots.Count));
                    Envelope reply = await bot.RequestAsync("match.action.attack", MessageTypes.MatchAction,
                        new MatchActionDto { action = MatchActionDto.Attack, targetSlot = target },
                        cancellation,
                        tolerateErrors: true);

                    // Lo slot puo' essere caduto fra l'evento e la mossa (spiriti, contrattacchi):
                    // se il server dice di no il turno si chiude passando, come fa il timeout.
                    if (reply.type != MessageTypes.Error)
                        break;
                    enemySlots.Remove(target);
                }

                await bot.RequestAsync("match.action.pass", MessageTypes.MatchAction,
                    new MatchActionDto { action = MatchActionDto.Pass }, cancellation);
                break;
            }

            case "DecisiveSelectionStarted":
                await ThinkAsync(cancellation);
                await bot.RequestAsync("match.action.decisive", MessageTypes.MatchAction,
                    new MatchActionDto
                    {
                        action = MatchActionDto.Decisive,
                        decisiveIndices = new[] { 0, 1, 2 }
                    }, cancellation);
                break;
        }
    }

    /// <summary>Un secondo o due prima di muovere: senza, la partita non somiglia a niente.</summary>
    private Task ThinkAsync(CancellationToken cancellation) =>
        Task.Delay(TimeSpan.FromSeconds(0.8 + random.NextDouble() * 2.5), cancellation);
}
