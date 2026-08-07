using System.Net;
using AccardND.Server.Data;
using AccardND.Server.Web;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Accounts;

/// <summary>
/// Route del broker OAuth Google (vedi <see cref="GoogleOAuthBroker"/>):
/// /auth/google/begin -> l'app avvia, /auth/google/callback -> ci torna il
/// browser dopo il consenso, /auth/google/token -> l'app ritira l'ID token.
/// </summary>
public static class GoogleAuthEndpoints
{
    private sealed record BeginRequest(string challenge);
    private sealed record TokenRequest(string requestId, string verifier);

    public static void MapGoogleAuthEndpoints(this WebApplication app)
    {
        var broker = app.Services.GetRequiredService<GoogleOAuthBroker>();
        var deletion = app.Services.GetRequiredService<AccountDeletionService>();
        var database = app.Services.GetRequiredService<AccardDatabase>();
        var googleIdTokens = app.Services.GetRequiredService<GoogleIdTokenReader>();
        var webSessions = app.Services.GetRequiredService<WebSessionStore>();

        app.MapPost("/auth/google/begin", async (HttpContext context) =>
        {
            NoStore(context);
            BeginRequest body = await ReadBody<BeginRequest>(context);
            (string requestId, string authorizeUrl, string error) = broker.Begin(body?.challenge);
            // Sempre 200 con l'errore nel corpo: il client mostra il messaggio
            // invece di un generico "server non raggiungibile".
            return error != null
                ? Results.Ok(new { error })
                : Results.Ok(new { requestId, authorizeUrl });
        });

        app.MapPost("/auth/google/token", async (HttpContext context) =>
        {
            NoStore(context);
            TokenRequest body = await ReadBody<TokenRequest>(context);
            (string status, string idToken, string error) = broker.Redeem(body?.requestId, body?.verifier);
            return Results.Ok(new { status, idToken, error });
        });

        // I parametri si leggono a mano dalla query: Google omette "code" quando
        // l'utente annulla e "error" quando va a buon fine, e il binding
        // automatico dei minimal API su parametri mancanti risponderebbe 400.
        //
        // Il callback e' condiviso da tutti i flussi che passano dal broker (stesso
        // redirect_uri registrato su Google): il purpose dell'esito dice se qui
        // finisce un login dell'app, la cancellazione account o la pagina delle
        // statistiche. L'ID token non lascia mai il server in nessuno dei tre casi.
        app.MapGet("/auth/google/callback", async (HttpContext context) =>
        {
            NoStore(context);
            GoogleOAuthBroker.CallbackOutcome outcome = await broker.CompleteAsync(
                context.Request.Query["state"],
                context.Request.Query["code"],
                context.Request.Query["error"]);

            if (outcome.IdToken != null)
            {
                switch (outcome.Purpose)
                {
                    case GoogleOAuthBroker.PurposeDeletion:
                        return await AccountDeletionEndpoints.RenderConfirmationAsync(deletion, outcome.IdToken);
                    case GoogleOAuthBroker.PurposeStats:
                        return await StatsPageEndpoints.CompleteLoginAsync(
                            context, database, googleIdTokens, webSessions, outcome.IdToken);
                }
            }

            return Results.Content(Page(outcome.Message, outcome.Ok), "text/html; charset=utf-8");
        });
    }

    private static void NoStore(HttpContext context) =>
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

    private static async Task<T> ReadBody<T>(HttpContext context)
    {
        try { return await context.Request.ReadFromJsonAsync<T>(); }
        catch { return default; }
    }

    private static string Page(string message, bool ok) =>
        "<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\">"
        + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
        + "<title>AcCardNDie</title></head>"
        + "<body style=\"font-family:system-ui,sans-serif;text-align:center;padding:3rem 1.5rem;"
        + "background:#130d25;color:#f4efe6\">"
        + "<h1 style=\"font-size:1.6rem;margin-bottom:1rem\">AcCardNDie</h1>"
        + "<p style=\"font-size:1.05rem;line-height:1.5;color:" + (ok ? "#9fe0a8" : "#e8a0a0") + "\">"
        + WebUtility.HtmlEncode(message) + "</p>"
        + "</body></html>";
}
