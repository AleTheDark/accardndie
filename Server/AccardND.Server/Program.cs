using AccardND.Server;
using AccardND.Server.Accounts;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;

ServerConfig config = ServerConfig.Load(
    Path.Combine(AppContext.BaseDirectory, "serverconfig.json"));

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(config.Urls);
builder.Services.AddSingleton(config);
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
