namespace AccardND.Server.Progression;

/// <summary>
/// Controlla ogni ora (e all'avvio) se la stagione è scaduta e in tal caso la ruota.
/// </summary>
public sealed class SeasonRolloverService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly SeasonService seasons;
    private readonly ILogger<SeasonRolloverService> logger;

    public SeasonRolloverService(SeasonService seasons, ILogger<SeasonRolloverService> logger)
    {
        this.seasons = seasons;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (seasons.RolloverIfDue(DateTime.UtcNow))
                    logger.LogInformation(
                        "Rollover stagione completato: ora attiva '{Name}' ({Id}).",
                        seasons.ActiveSeasonName, seasons.ActiveSeasonId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Rollover stagione fallito.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
