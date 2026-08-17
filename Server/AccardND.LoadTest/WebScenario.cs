using System.Diagnostics;

namespace AccardND.LoadTest;

/// <summary>
/// Carico HTTP sulle pagine servite dal server .NET dietro nginx. Non sono il grosso del
/// traffico, ma la Hall of Fame e le statistiche leggono dal database dentro la stessa
/// richiesta: se il DB e' in affanno, si vede prima qui che in gioco.
/// </summary>
public sealed class WebScenario
{
    private readonly HttpClient client;
    private readonly Metrics metrics;
    private readonly Random random;
    private readonly (string Path, string Operation)[] pages;

    public WebScenario(string baseUrl, Metrics metrics, Random random)
    {
        this.metrics = metrics;
        this.random = random;
        client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 256,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        pages = new[]
        {
            ("health", "http.health"),
            ("hall-of-fame", "http.hall-of-fame"),
            ("statistiche", "http.statistiche"),
            ("", "http.home")
        };
    }

    public async Task RunAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            (string path, string operation) = pages[random.Next(pages.Length)];
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using HttpResponseMessage response = await client.GetAsync(
                    path, HttpCompletionOption.ResponseContentRead, cancellation);
                // Il corpo va letto per intero: e' quello che paga il server, non l'header.
                await response.Content.ReadAsByteArrayAsync(cancellation);
                metrics.Record(
                    operation,
                    stopwatch.Elapsed.TotalMilliseconds,
                    response.IsSuccessStatusCode ? null : $"http_{(int)response.StatusCode}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                metrics.Record(operation, stopwatch.Elapsed.TotalMilliseconds, "http_exception");
                metrics.BotError($"http_{exception.GetType().Name}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1 + random.NextDouble() * 4), cancellation);
        }
    }
}
