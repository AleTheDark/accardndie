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

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var auth = app.Services.GetRequiredService<AdminAuth>();
        var service = app.Services.GetRequiredService<AdminService>();

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

        app.MapGet("/admin/api/seasons", (HttpContext context) =>
            Guard(context, auth, () => Results.Ok(service.GetSeasons())));

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

        app.MapPost("/admin/api/players/{id}/reset", (HttpContext context, string id) =>
            Guard(context, auth, () => ToResult(service.ResetProgress(id))));

        app.MapPost("/admin/api/players/{id}/delete", (HttpContext context, string id) =>
            Guard(context, auth, () => ToResult(service.DeletePlayer(id))));
    }

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
