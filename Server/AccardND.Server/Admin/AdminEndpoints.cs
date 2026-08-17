using AccardND.Server.Rooms;
using AccardND.Server.Sessions;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Admin;

/// <summary>
/// Espone il pannello admin: la pagina HTML su GET /admin e le API JSON su
/// /admin/api/*. Se il pannello e disattivato (nessuna password configurata)
/// tutte le route rispondono 404, cosi da non rivelarne l'esistenza.
/// </summary>
public static class AdminEndpoints
{
    private sealed record LoginRequest(string username, string password);
    private sealed record RenameRequest(string name);
    private sealed record HoneyRequest(int honey);
    private sealed record UnlockRequest(string type, string id, bool granted);
    private sealed record UnlockAllRequest(bool granted);
    private sealed record StashRequest(string itemId, int count);
    private sealed record StashAllRequest(int count);
    private sealed record ClientVersionRequest(string target, bool enforce, string updateUrl);
    private sealed record MaintenanceRequest(bool enabled, string message);

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var auth = app.Services.GetRequiredService<AdminAuth>();
        var service = app.Services.GetRequiredService<AdminService>();
        var clientVersions = app.Services.GetRequiredService<ClientVersionGate>();
        var maintenance = app.Services.GetRequiredService<MaintenanceGate>();
        var rooms = app.Services.GetRequiredService<RoomManager>();
        var presence = app.Services.GetRequiredService<PresenceRegistry>();

        // Pagina del pannello.
        app.MapGet("/admin", (HttpContext context) =>
        {
            NoStore(context);
            return auth.IsEnabled
                ? Results.Content(AdminPage.Html, "text/html; charset=utf-8")
                : Results.NotFound();
        });

        // Login: emette un token bearer.
        app.MapPost("/admin/api/login", async (HttpContext context) =>
        {
            if (!auth.IsEnabled)
                return Results.NotFound();
            LoginRequest body;
            try { body = await context.Request.ReadFromJsonAsync<LoginRequest>(); }
            catch { return Results.BadRequest(new { error = "Richiesta non valida." }); }

            string token = auth.TryLogin(body?.username, body?.password);
            return token == null
                ? Results.Json(new { error = "Credenziali non valide." }, statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(new { token });
        });

        app.MapPost("/admin/api/logout", (HttpContext context) =>
        {
            auth.Logout(ExtractToken(context));
            return Results.Ok(new { ok = true });
        });

        // --- API protette --------------------------------------------------

        app.MapGet("/admin/api/overview", (HttpContext context) =>
            Guard(context, auth, () => Results.Ok(service.GetOverview())));

        app.MapGet("/admin/api/timeseries", (HttpContext context, int? days) =>
            Guard(context, auth, () => Results.Ok(service.GetTimeseries(days ?? 30))));

        // 60 giorni di default: con 30 il D30 avrebbe una sola coorte matura.
        app.MapGet("/admin/api/retention", (HttpContext context, int? days) =>
            Guard(context, auth, () => Results.Ok(service.GetRetention(days ?? 60))));

        // sort/desc si leggono dalla query invece di farli legare: i minimal API
        // risponderebbero 400 se un client vecchio non li mandasse.
        app.MapGet("/admin/api/players", (HttpContext context, string search, int? limit, int? offset) =>
            Guard(context, auth, () => Results.Ok(service.GetPlayers(
                search,
                limit ?? 50,
                offset ?? 0,
                context.Request.Query["sort"],
                context.Request.Query["desc"] != "false"))));

        app.MapGet("/admin/api/players/{id}", (HttpContext context, string id) =>
            Guard(context, auth, () =>
            {
                object detail = service.GetPlayerDetail(id);
                return detail == null ? Results.NotFound() : Results.Ok(detail);
            }));

        app.MapGet("/admin/api/matches", (HttpContext context, int? limit, int? offset) =>
            Guard(context, auth, () => Results.Ok(service.GetMatches(limit ?? 50, offset ?? 0))));

        // status si legge dalla query e non dalla firma: un parametro mancante non deve
        // diventare un 400 per un pannello aperto da prima.
        app.MapGet("/admin/api/runs", (HttpContext context, int? limit, int? offset) =>
            Guard(context, auth, () => Results.Ok(service.GetRuns(
                context.Request.Query["status"], limit ?? 100, offset ?? 0))));

        app.MapGet("/admin/api/campaign-leaderboard", (HttpContext context, int? limit) =>
            Guard(context, auth, () => Results.Ok(service.GetCampaignLeaderboard(limit ?? 100))));

        app.MapGet("/admin/api/seasons", (HttpContext context) =>
            Guard(context, auth, () => Results.Ok(service.GetSeasons())));

        app.MapGet("/admin/api/seasons/{id:int}", (HttpContext context, int id) =>
            Guard(context, auth, () =>
            {
                object detail = service.GetSeasonDetail(id);
                return detail == null ? Results.NotFound() : Results.Ok(detail);
            }));

        app.MapGet("/admin/api/quests", (HttpContext context, int? days) =>
            Guard(context, auth, () => Results.Ok(service.GetTavernQuests(days ?? 14))));

        // --- Azioni di scrittura -------------------------------------------

        app.MapPost("/admin/api/players/{id}/rename", async (HttpContext context, string id) =>
            await GuardAsync(context, auth, async () =>
            {
                RenameRequest body = await ReadBody<RenameRequest>(context);
                return ToResult(service.RenamePlayer(id, body?.name));
            }));

        app.MapPost("/admin/api/players/{id}/honey", async (HttpContext context, string id) =>
            await GuardAsync(context, auth, async () =>
            {
                HoneyRequest body = await ReadBody<HoneyRequest>(context);
                return ToResult(service.SetHoney(id, body?.honey ?? 0));
            }));

        // Sblocchi a mano per gli account di prova. Le POST rispondono col catalogo
        // aggiornato: il pannello ridisegna la griglia senza una seconda chiamata.
        app.MapGet("/admin/api/players/{id}/unlocks", (HttpContext context, string id) =>
            Guard(context, auth, () =>
            {
                object state = service.GetPlayerUnlocks(id);
                return state == null ? Results.NotFound() : Results.Ok(state);
            }));

        app.MapPost("/admin/api/players/{id}/unlocks", async (HttpContext context, string id) =>
            await GuardAsync(context, auth, async () =>
            {
                UnlockRequest body = await ReadBody<UnlockRequest>(context);
                if (body == null)
                    return Results.BadRequest(new { error = "Richiesta non valida." });
                (bool ok, string error) = service.SetUnlock(id, body.type, body.id, body.granted);
                return ok ? Results.Ok(service.GetPlayerUnlocks(id)) : Results.BadRequest(new { error });
            }));

        app.MapPost("/admin/api/players/{id}/unlocks/all", async (HttpContext context, string id) =>
            await GuardAsync(context, auth, async () =>
            {
                UnlockAllRequest body = await ReadBody<UnlockAllRequest>(context);
                (bool ok, string error) = service.SetAllUnlocks(id, body?.granted ?? true);
                return ok ? Results.Ok(service.GetPlayerUnlocks(id)) : Results.BadRequest(new { error });
            }));

        // Scorta consumabili: come gli sblocchi, la POST risponde con lo stato aggiornato
        // cosi' il pannello ridisegna il riquadro senza una seconda chiamata.
        app.MapGet("/admin/api/players/{id}/stash", (HttpContext context, string id) =>
            Guard(context, auth, () =>
            {
                object state = service.GetPlayerStash(id);
                return state == null ? Results.NotFound() : Results.Ok(state);
            }));

        app.MapPost("/admin/api/players/{id}/stash", async (HttpContext context, string id) =>
            await GuardAsync(context, auth, async () =>
            {
                StashRequest body = await ReadBody<StashRequest>(context);
                if (body == null)
                    return Results.BadRequest(new { error = "Richiesta non valida." });
                (bool ok, string error) = service.SetStashItem(id, body.itemId, body.count);
                return ok ? Results.Ok(service.GetPlayerStash(id)) : Results.BadRequest(new { error });
            }));

        app.MapPost("/admin/api/players/{id}/stash/all", async (HttpContext context, string id) =>
            await GuardAsync(context, auth, async () =>
            {
                StashAllRequest body = await ReadBody<StashAllRequest>(context);
                if (body == null)
                    return Results.BadRequest(new { error = "Richiesta non valida." });
                (bool ok, string error) = service.SetAllStash(id, body.count);
                return ok ? Results.Ok(service.GetPlayerStash(id)) : Results.BadRequest(new { error });
            }));

        app.MapPost("/admin/api/players/{id}/reset", (HttpContext context, string id) =>
            Guard(context, auth, () => ToResult(service.ResetProgress(id))));

        app.MapPost("/admin/api/players/{id}/delete", (HttpContext context, string id) =>
            Guard(context, auth, () => ToResult(service.DeletePlayer(id))));

        app.MapPost("/admin/api/runs/{id:long}/delete", (HttpContext context, long id) =>
            Guard(context, auth, () => ToResult(service.DeleteCampaignRun(id))));

        // --- Versione client richiesta --------------------------------------

        app.MapGet("/admin/api/client-version", (HttpContext context) =>
            Guard(context, auth, () => Results.Ok(Describe(clientVersions))));

        // Il cast a Delegate non e' decorativo: un handler asincrono con il solo
        // HttpContext combacia con l'overload RequestDelegate di MapPost, che scarta
        // il valore restituito e risponde 200 con corpo vuoto (analizzatore ASP0016).
        // Gli altri POST qui sopra si salvano solo perche' hanno anche {id}.
        app.MapPost("/admin/api/client-version", (Delegate)(async (HttpContext context) =>
            await GuardAsync(context, auth, () => SaveClientVersionAsync(context, clientVersions))));

        app.MapPost("/admin/api/client-version/reset", (HttpContext context) =>
            Guard(context, auth, () =>
            {
                clientVersions.ResetToConfiguration();
                return Results.Ok(Describe(clientVersions));
            }));

        // --- Manutenzione ----------------------------------------------------
        //
        // La GET la interroga anche il polling del pannello: porta con sé i numeri
        // che dicono se si può riavviare, non solo lo stato dell'interruttore.

        app.MapGet("/admin/api/maintenance", (HttpContext context) =>
            Guard(context, auth, () => Results.Ok(Describe(maintenance, rooms, presence))));

        // Stesso cast a Delegate del client-version, e per la stessa ragione: senza
        // {id} nella rotta un handler asincrono col solo HttpContext finirebbe
        // sull'overload RequestDelegate, che scarta il risultato (ASP0016).
        app.MapPost("/admin/api/maintenance", (Delegate)(async (HttpContext context) =>
            await GuardAsync(context, auth, () =>
                SaveMaintenanceAsync(context, maintenance, rooms, presence))));
    }

    /// <summary>
    /// Tipo di ritorno dichiarato di proposito, come per
    /// <see cref="SaveClientVersionAsync"/>: dentro la lambda annidata i rami
    /// Ok/BadRequest non venivano inferiti come <see cref="IResult"/>.
    /// </summary>
    private static async Task<IResult> SaveMaintenanceAsync(
        HttpContext context, MaintenanceGate gate, RoomManager rooms, PresenceRegistry presence)
    {
        MaintenanceRequest body = await ReadBody<MaintenanceRequest>(context);
        if (body == null)
            return Results.BadRequest(new { error = "Richiesta non valida." });

        (bool ok, string error) = gate.Update(body.enabled, body.message);
        return ok
            ? Results.Ok(Describe(gate, rooms, presence))
            : Results.BadRequest(new { error });
    }

    private static object Describe(MaintenanceGate gate, RoomManager rooms, PresenceRegistry presence) => new
    {
        enabled = gate.IsActive,
        message = gate.Message,
        since = gate.SinceUtc?.ToString("O"),
        maxMessageLength = MaintenanceGate.MaxMessageLength,
        // Quanto resta da aspettare prima di poter riavviare senza far danni.
        liveMatches = rooms.LiveMatchCount,
        waitingRooms = rooms.WaitingRoomCount,
        onlineNow = presence.OnlineCount
    };

    /// <summary>
    /// Tipo di ritorno dichiarato di proposito: dentro una lambda annidata i rami
    /// con risultati di tipo diverso (Ok/BadRequest) non venivano inferiti come
    /// <see cref="IResult"/>, e la risposta usciva 200 con corpo vuoto.
    /// </summary>
    private static async Task<IResult> SaveClientVersionAsync(HttpContext context, ClientVersionGate gate)
    {
        ClientVersionRequest body = await ReadBody<ClientVersionRequest>(context);
        if (body == null)
            return Results.BadRequest(new { error = "Richiesta non valida." });

        (bool ok, string error) = gate.Update(body.target, body.enforce, body.updateUrl);
        return ok
            ? Results.Ok(Describe(gate))
            : Results.BadRequest(new { error });
    }

    private static object Describe(ClientVersionGate gate) => new
    {
        target = gate.Target,
        enforce = gate.Enforce,
        updateUrl = gate.UpdateUrl,
        source = gate.Source,
        configuredTarget = gate.ConfiguredTarget,
        blocking = gate.IsEnforced
    };

    /// <summary>
    /// Il pannello mostra dati vivi: nessuna risposta deve essere memorizzata da
    /// browser, Service Worker o proxy intermedi.
    /// </summary>
    private static void NoStore(HttpContext context) =>
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

    private static IResult Guard(HttpContext context, AdminAuth auth, Func<IResult> action)
    {
        NoStore(context);
        if (!auth.IsEnabled)
            return Results.NotFound();
        if (!auth.IsValid(ExtractToken(context)))
            return Results.Json(new { error = "Non autorizzato." }, statusCode: StatusCodes.Status401Unauthorized);
        return action();
    }

    private static async Task<IResult> GuardAsync(HttpContext context, AdminAuth auth, Func<Task<IResult>> action)
    {
        NoStore(context);
        if (!auth.IsEnabled)
            return Results.NotFound();
        if (!auth.IsValid(ExtractToken(context)))
            return Results.Json(new { error = "Non autorizzato." }, statusCode: StatusCodes.Status401Unauthorized);
        return await action();
    }

    private static IResult ToResult((bool ok, string error) outcome) =>
        outcome.ok
            ? Results.Ok(new { ok = true })
            : Results.BadRequest(new { error = outcome.error });

    private static async Task<T> ReadBody<T>(HttpContext context)
    {
        try { return await context.Request.ReadFromJsonAsync<T>(); }
        catch { return default; }
    }

    private static string ExtractToken(HttpContext context)
    {
        string header = context.Request.Headers.Authorization;
        if (string.IsNullOrEmpty(header))
            return null;
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : header.Trim();
    }
}
