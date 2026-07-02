using AccardND.Server;
using AccardND.Server.Accounts;
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
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<MatchmakingQueue>();
builder.Services.AddSingleton<MessageRouter>();

WebApplication app = builder.Build();
app.UseWebSockets();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
