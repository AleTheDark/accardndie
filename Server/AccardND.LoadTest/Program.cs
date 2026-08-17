using System.Diagnostics;
using AccardND.LoadTest;

Options options = Options.Parse(args, out string parseError);
if (parseError != null)
{
    if (parseError != "help")
        Console.Error.WriteLine($"Errore: {parseError}\n");
    Console.WriteLine(Options.Usage);
    return parseError == "help" ? 0 : 2;
}

if (!Guard.CheckTarget(options))
    return 2;

var metrics = new Metrics();
using var run = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    Console.WriteLine("\nInterruzione richiesta: chiudo i bot e stampo il riepilogo.");
    run.Cancel();
};

int pvpBots = options.Profile switch
{
    Profile.Pvp => options.Clients - options.Clients % 2,
    Profile.Mixed => (int)(options.Clients * Math.Clamp(options.PvpShare, 0, 1)) / 2 * 2,
    _ => 0
};
int singleBots = options.Profile switch
{
    Profile.Connect or Profile.Web => 0,
    Profile.SinglePlayer => options.Clients,
    _ => options.Clients - pvpBots
};
int idleBots = options.Profile == Profile.Connect ? options.Clients : 0;

Console.WriteLine($"""
    Prova di carico AccardND
      bersaglio ws   {options.Url}
      bersaglio http {options.WebUrl ?? "(nessuno)"}
      profilo        {options.Profile}  (pvp {pvpBots}, singolo {singleBots}, inattivi {idleBots}, web {options.WebClients})
      ingresso       {options.Clients} bot in {options.RampSeconds}s, poi {options.DurationSeconds}s a regime
      account        {options.LoginMode} con prefisso '{options.Prefix}', versione client {options.ClientVersion}
    """);
Console.WriteLine();

var pairing = new PvpScenario.Pairing(Math.Max(pvpBots / 2, 1));
var workers = new List<Task>();
var stopwatch = Stopwatch.StartNew();
int accountIndex = 0;

for (int index = 0; index < idleBots; index++)
    workers.Add(StartBot(accountIndex++, BotKind.Idle, 0, false));
for (int index = 0; index < singleBots; index++)
    workers.Add(StartBot(accountIndex++, BotKind.SinglePlayer, 0, false));
for (int index = 0; index < pvpBots; index++)
    workers.Add(StartBot(accountIndex++, BotKind.Pvp, index / 2, index % 2 == 0));

if (options.WebClients > 0 && !string.IsNullOrWhiteSpace(options.WebUrl))
{
    for (int index = 0; index < options.WebClients; index++)
    {
        int seed = index;
        workers.Add(Task.Run(async () =>
        {
            await RampDelay(seed, options.WebClients);
            var scenario = new WebScenario(options.WebUrl, metrics, new Random(9000 + seed));
            try
            {
                await scenario.RunAsync(run.Token);
            }
            catch (OperationCanceledException)
            {
                // Fine corsa.
            }
        }));
    }
}

Task reporter = Task.Run(ReportAsync);

// La prova dura la rampa piu' il tempo a regime: i bot entrati per primi lavorano piu' a
// lungo, ma la finestra che conta e' quella in cui ci sono tutti.
try
{
    await Task.Delay(
        TimeSpan.FromSeconds(options.RampSeconds + options.DurationSeconds), run.Token);
}
catch (OperationCanceledException)
{
    // Interrotta a mano.
}

run.Cancel();
await Task.WhenAny(Task.WhenAll(workers), Task.Delay(TimeSpan.FromSeconds(15)));
await Task.WhenAny(reporter, Task.Delay(TimeSpan.FromSeconds(2)));

Metrics.Snapshot final = metrics.Take();
Console.WriteLine();
Console.WriteLine($"=== Riepilogo dopo {stopwatch.Elapsed.TotalSeconds:F0}s ===");
Console.WriteLine($"connessioni fallite {final.ConnectionsFailed}, cadute {final.ConnectionsDropped}");
Console.WriteLine($"richieste {final.TotalRequests}, con errore {final.TotalErrors} " +
                  $"({(final.TotalRequests == 0 ? 0 : 100.0 * final.TotalErrors / final.TotalRequests):F2}%)");
Console.WriteLine();
Console.WriteLine(final.ToTable());

if (!string.IsNullOrWhiteSpace(options.JsonOut))
{
    await File.WriteAllTextAsync(options.JsonOut, final.ToJson(new
    {
        url = options.Url,
        webUrl = options.WebUrl,
        profile = options.Profile.ToString(),
        clients = options.Clients,
        webClients = options.WebClients,
        pvpBots,
        singleBots,
        idleBots,
        rampSeconds = options.RampSeconds,
        durationSeconds = options.DurationSeconds,
        elapsedSeconds = stopwatch.Elapsed.TotalSeconds,
        startedAtUtc = DateTime.UtcNow.ToString("O")
    }));
    Console.WriteLine($"Riepilogo salvato in {options.JsonOut}");
}

return final.ConnectionsFailed > 0 || final.TotalErrors > 0 ? 1 : 0;

Task StartBot(int index, BotKind kind, int pair, bool isHost)
{
    int total = Math.Max(options.Clients, 1);
    return Task.Run(async () =>
    {
        await RampDelay(index, total);
        var random = new Random(1000 + index);
        var singlePlayer = new SinglePlayerScenario(options, random);
        var pvp = new PvpScenario(options, random);
        int failures = 0;

        while (!run.IsCancellationRequested)
        {
            await using var bot = new BotConnection(
                options.Url, metrics, TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
            try
            {
                await bot.ConnectAsync(run.Token);
                await bot.AuthenticateAsync(
                    options.Nickname(index),
                    options.Password,
                    options.ClientVersion,
                    options.LoginMode == LoginMode.Register,
                    run.Token);

                if (kind == BotKind.Idle)
                {
                    // Nessun traffico: interessa solo quanto costa tenere aperta la sessione.
                    await Task.Delay(Timeout.InfiniteTimeSpan, run.Token);
                }

                await SinglePlayerScenario.OpenAppAsync(bot, run.Token);
                if (kind == BotKind.Pvp && options.PvpRanked)
                    await SinglePlayerScenario.UnlockWarriorAsync(bot, run.Token);

                failures = 0;
                while (!run.IsCancellationRequested && bot.IsOpen)
                {
                    await Task.Delay(singlePlayer.ThinkDelay(), run.Token);
                    if (kind == BotKind.Pvp)
                        await pvp.PlayMatchAsync(bot, pairing, pair, isHost, run.Token);
                    else
                        await singlePlayer.StepAsync(bot, run.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (BotFailure failure)
            {
                metrics.BotError(Shorten(failure.Message));
                if (options.Verbose && failures < 3)
                    Console.Error.WriteLine($"[{options.Nickname(index)}] {failure.Message}");
            }
            catch (Exception exception)
            {
                metrics.BotError(exception.GetType().Name);
                if (options.Verbose && failures < 3)
                    Console.Error.WriteLine($"[{options.Nickname(index)}] {exception.Message}");
            }

            // Rientro dopo una caduta, come farebbe il client: attesa crescente, con un tetto.
            failures++;
            TimeSpan backoff = TimeSpan.FromSeconds(Math.Min(2 * failures, 20) + random.NextDouble());
            try
            {
                await Task.Delay(backoff, run.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    });
}

async Task RampDelay(int index, int total)
{
    if (options.RampSeconds <= 0)
        return;
    double offset = options.RampSeconds * (double)index / total;
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(offset), run.Token);
    }
    catch (OperationCanceledException)
    {
        // Fine corsa prima ancora di entrare.
    }
}

async Task ReportAsync()
{
    long previousRequests = 0;
    long previousErrors = 0;
    var interval = TimeSpan.FromSeconds(Math.Max(options.ReportSeconds, 1));
    Console.WriteLine($"{"t",6}{"aperte",9}{"req/s",9}{"err/s",9}{"p95 ms",9}  errori totali");
    while (!run.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(interval, run.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Metrics.Snapshot snapshot = metrics.Take();
        double seconds = interval.TotalSeconds;
        double requestsPerSecond = (snapshot.TotalRequests - previousRequests) / seconds;
        double errorsPerSecond = (snapshot.TotalErrors - previousErrors) / seconds;
        previousRequests = snapshot.TotalRequests;
        previousErrors = snapshot.TotalErrors;

        Console.WriteLine(
            $"{stopwatch.Elapsed.TotalSeconds,6:F0}{snapshot.ConnectionsOpen,9}" +
            $"{requestsPerSecond,9:F1}{errorsPerSecond,9:F1}{snapshot.WeightedP95,9:F0}  " +
            $"{snapshot.TotalErrors} app / {snapshot.BotErrors} trasporto");
    }
}

static string Shorten(string message) =>
    message.Length <= 60 ? message : message[..60];

internal enum BotKind
{
    Idle,
    SinglePlayer,
    Pvp
}

internal static class Guard
{
    /// <summary>
    /// I bot registrano account, giocano partite classificate e scrivono progressione:
    /// puntati al server vero sporcano il database dei giocatori. Il flag esiste perche'
    /// sia una scelta e non una distrazione.
    /// </summary>
    public static bool CheckTarget(Options options)
    {
        var uri = new Uri(options.Url);
        bool isLocal = uri.IsLoopback;
        if (isLocal || options.AllowProduction || uri.AbsolutePath != "/ws")
            return true;

        Console.Error.WriteLine($"""
            L'indirizzo {options.Url} e' l'endpoint di gioco su un host remoto.
            I bot creano account, scrivono progressione e chiudono partite classificate:
            sul database di produzione restano.

            Se e' un'istanza di prova, esponila su un percorso diverso (es. /wstest).
            Se vuoi davvero colpire la produzione, ripeti il comando con --allow-production.
            """);
        return false;
    }
}
