using AccardND.Server;
using AccardND.Server.Accounts;
using AccardND.Server.Admin;
using AccardND.Server.Data;
using AccardND.Server.Progression;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;
using AccardND.Server.Web;

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
builder.Services.AddSingleton<TalentService>();
builder.Services.AddSingleton(provider =>
    new GooglePlayReceiptVerifier(provider.GetRequiredService<ServerConfig>().GooglePlay));
builder.Services.AddSingleton<IapPurchaseService>();
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
builder.Services.AddSingleton<MaintenanceGate>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<AdminAuth>();
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<WebSessionStore>();
builder.Services.AddSingleton<PlayerDossierService>();

WebApplication app = builder.Build();

// Rinumerazione dei capitoli dopo il restyling della campagna. Prima di accettare
// connessioni: un client che leggesse la progressione a meta' migrazione vedrebbe capitoli
// completati che non ha giocato. E' idempotente, quindi ai riavvii successivi non fa nulla.
ChapterRemapMigration.RunIfNeeded(
    app.Services.GetRequiredService<AccardDatabase>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ChapterRemap"));

// Le amichevoli non contano piu' nelle statistiche: quelle che ci sono finite prima della
// regola vanno tolte, o i profili resterebbero gonfiati per sempre. Anche questa e'
// idempotente e gira una volta sola.
FriendlyStatsCleanupMigration.RunIfNeeded(
    app.Services.GetRequiredService<AccardDatabase>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FriendlyStatsCleanup"));

// I livelli account gia' raggiunti valgono punti talento: chi ha giocato prima dell'albero
// deve trovarci dentro qualcosa da spendere. Anche questa gira una volta sola.
TalentPointsBackfillMigration.RunIfNeeded(
    app.Services.GetRequiredService<AccardDatabase>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TalentPointsBackfill"));

// I nodi tolti dal catalogo smettono di contare, ma i propoli spesi per comprarli no:
// vanno restituiti, o chi aveva investito in un nodo che abbiamo ritirato si ritrova con
// meno di chi non l'aveva comprato. Gira una volta sola.
RemovedTalentRefundMigration.RunIfNeeded(
    app.Services.GetRequiredService<AccardDatabase>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RemovedTalentRefund"));

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

// Pagina delle statistiche su https://<dominio>/statistiche: si accede con lo
// stesso account Google del gioco e si legge, in sola lettura, tutto quello che
// il server sa di quel profilo. Il resto del sito e' HTML statico servito da
// nginx; questa deve stare qui perche' i numeri vengono dal database.
app.MapStatsPageEndpoints();

// Hall of Fame su https://<dominio>/hall-of-fame: la classifica ranked di tutti,
// stagione in corso e stagioni chiuse. Pubblica e senza accesso - i gradi in
// classifica li vedono gia' tutti in gioco - ma sta qui e non fra i file statici
// per la stessa ragione delle statistiche: i numeri sono nel database.
app.MapHallOfFamePageEndpoints();

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
