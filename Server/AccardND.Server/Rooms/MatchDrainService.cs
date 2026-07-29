namespace AccardND.Server.Rooms;

/// <summary>Registra e chiude i match vivi prima che il processo termini.</summary>
public sealed class MatchDrainService : IHostedService
{
    private readonly RoomManager rooms;
    private readonly ILogger<MatchDrainService> logger;

    public MatchDrainService(RoomManager rooms, ILogger<MatchDrainService> logger)
    {
        this.rooms = rooms;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Shutdown: chiusura dei match ancora aperti.");
        await rooms.DrainMatchesAsync();
    }
}
