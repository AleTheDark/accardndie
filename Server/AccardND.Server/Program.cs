using AccardND.Server;
using AccardND.Server.Accounts;
using AccardND.Server.Admin;
using AccardND.Server.Data;
using AccardND.Server.Progression;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;

// Config alternativa passabile come primo argomento (utile per test e ambienti diversi).
string configPath = args.Length > 0 && File.Exists(args[0])
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "serverconfig.json");
ServerConfig config = ServerConfig.Load(configPath);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(config.Urls);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(provider =>
{
    string catalogPath = Path.IsPathRooted(config.CardCatalogPath)
        ? config.CardCatalogPath
        : Path.Combine(AppContext.BaseDirectory, config.CardCatalogPath);
    return AccardND.Server.Match.PvpCardCatalog.Load(
        catalogPath, provider.GetRequiredService<ILogger<AccardND.Server.Match.PvpCardCatalog>>());
});
builder.Services.AddSingleton<AccardDatabase>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<GoogleIdTokenReader>();
builder.Services.AddSingleton<UgsAuthService>();
builder.Services.AddSingleton<GoogleOAuthBroker>();
builder.Services.AddSingleton<AccountEraser>();
builder.Services.AddSingleton<AccountDeletionService>();
builder.Services.AddSingleton<SeasonService>();
builder.Services.AddSingleton<StatsService>();
builder.Services.AddSingleton<RankedService>();
builder.Services.AddSingleton<UnlockService>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<HallOfFameService>();
builder.Services.AddSingleton<AchievementService>();
builder.Services.AddSingleton<SinglePlayerProgressService>();
builder.Services.AddSingleton<PresenceRegistry>();
builder.Services.AddSingleton<SessionTokenRegistry>();
builder.Services.AddSingleton<RequestDedupStore>();
builder.Services.AddSingleton<FriendService>();
builder.Services.AddSingleton<MatchResultRecorder>();
builder.Services.AddHostedService<SeasonRolloverService>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService<MatchDrainService>();
builder.Services.AddSingleton<MatchmakingQueue>();
builder.Services.AddSingleton<ClientVersionGate>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<AdminAuth>();
builder.Services.AddSingleton<AdminService>();

WebApplication app = builder.Build();
// Ping di keep-alive ogni 30s: tiene vive le connessioni idle (turni lunghi)
// sotto il timeout dei proxy davanti al server, es. Cloudflare. Il browser
// risponde automaticamente al PING, quindi vale anche per i client WebGL/PWA.
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Pannello admin (pagina + API su /admin/*). Attivo solo se e configurata una
// password admin (env var ACCARDND_ADMIN_PASSWORD o serverconfig Admin.Password).
app.MapAdminEndpoints();

// Broker OAuth Google per i client senza browser integrato (APK Android):
// scambia il codice col Web Client ID del login web, cosi' lo stesso account
// Google resta lo stesso PlayerId UGS su APK e PWA.
app.MapGoogleAuthEndpoints();

// Pagina pubblica di cancellazione account: Google Play la richiede raggiungibile
// dal web, senza installare il gioco. L'URL da dichiarare in "Sicurezza dei dati"
// e' https://<dominio>/account/delete.
app.MapAccountDeletionEndpoints();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var router = context.RequestServices.GetRequiredService<MessageRouter>();
    await router.HandleConnectionAsync(new ClientConnection(socket), context.RequestAborted);
});

app.Run();
