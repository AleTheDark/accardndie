using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Accounts;

/// <summary>
/// Pagina pubblica di cancellazione account su /account/delete.
///
/// Google Play la pretende da ogni app che permette di registrarsi, e la
/// pretende raggiungibile dal web senza installare il gioco: l'URL va indicato
/// nella dichiarazione "Sicurezza dei dati". Da qui l'utente si riconosce con lo
/// stesso account Google con cui gioca, vede cosa sta per perdere e conferma.
///
/// Il giro OAuth passa dal broker gia' in uso per il login (stesso redirect_uri,
/// quindi nessuna configurazione nuova sulla console Google): il callback
/// riconosce il flusso dal purpose e atterra qui invece che sulla pagina
/// "torna al gioco".
/// </summary>
public static class AccountDeletionEndpoints
{
    public const string PagePath = "/account/delete";

    public static void MapAccountDeletionEndpoints(this WebApplication app)
    {
        var broker = app.Services.GetRequiredService<GoogleOAuthBroker>();
        var deletion = app.Services.GetRequiredService<AccountDeletionService>();

        app.MapGet(PagePath, (HttpContext context) =>
        {
            NoStore(context);
            return Html(IntroPage(broker.IsEnabled));
        });

        // POST e non link: un GET che apre una sessione OAuth verrebbe innescato
        // anche dal prefetch del browser, riempiendo il broker di richieste morte.
        app.MapPost(PagePath + "/start", (HttpContext context) =>
        {
            NoStore(context);
            (string authorizeUrl, string error) = broker.BeginBrowserFlow(GoogleOAuthBroker.PurposeDeletion);
            return error != null
                ? Html(MessagePage("Non si puo' procedere", error, false))
                : Results.Redirect(authorizeUrl);
        });

        app.MapPost(PagePath + "/confirm", async (HttpContext context) =>
        {
            NoStore(context);
            string ticket = await ReadFormValue(context, "ticket");
            (bool ok, string error, int deleted) = deletion.Confirm(ticket);
            return ok
                ? Html(DeletedPage(deleted))
                : Html(MessagePage("Cancellazione non eseguita", error, false));
        });
    }

    /// <summary>
    /// Secondo tempo del flusso: il callback OAuth ha in mano l'ID token verificato.
    /// Da qui in poi la pagina mostra gli account trovati e chiede conferma.
    /// </summary>
    public static async Task<IResult> RenderConfirmationAsync(
        AccountDeletionService deletion, string idToken)
    {
        PreparedDeletion prepared = await deletion.PrepareAsync(idToken);

        if (prepared.Error != null)
            return Html(MessagePage("Non si puo' procedere", prepared.Error, false));

        if (prepared.Accounts.Count == 0)
        {
            return Html(MessagePage(
                "Nessun account da cancellare",
                "Non risulta nessun account AcCardNDie collegato a " + prepared.Email
                + ". Se giochi con un altro indirizzo Google, ripeti l'accesso scegliendo quello giusto.",
                true));
        }

        return Html(ConfirmPage(prepared));
    }

    // ---- Pagine ---------------------------------------------------------------

    private static string IntroPage(bool googleEnabled)
    {
        var body = new StringBuilder();
        body.Append("<h1>Cancellazione dell'account</h1>");
        body.Append("<p>Da questa pagina puoi chiedere l'eliminazione del tuo account AcCardNDie "
            + "e di tutti i dati collegati. Non serve avere il gioco installato.</p>");
        body.Append("<p>Ti verra' chiesto di accedere con Google: serve a dimostrare che l'account "
            + "e' tuo. Prima di cancellare qualsiasi cosa ti mostreremo cosa sta per sparire.</p>");
        body.Append(WhatGetsDeleted());
        body.Append("<p class=\"warn\">L'operazione e' immediata e definitiva: i dati non sono "
            + "recuperabili e non esiste un periodo di ripensamento.</p>");

        if (!googleEnabled)
        {
            body.Append("<p class=\"warn\">Il servizio di accesso non e' al momento disponibile. "
                + "Riprova piu' tardi.</p>");
            return body.ToString();
        }

        body.Append("<form method=\"post\" action=\"" + PagePath + "/start\">"
            + "<button type=\"submit\" class=\"primary\">Accedi con Google e continua</button></form>");
        return body.ToString();
    }

    private static string ConfirmPage(PreparedDeletion prepared)
    {
        var body = new StringBuilder();
        body.Append("<h1>Conferma la cancellazione</h1>");
        body.Append("<p>Accesso verificato come <strong>" + Encode(prepared.Email) + "</strong>.</p>");
        body.Append(prepared.Accounts.Count == 1
            ? "<p>A questo indirizzo risulta collegato un account:</p>"
            : "<p>A questo indirizzo risultano collegati " + prepared.Accounts.Count + " account:</p>");

        body.Append("<ul class=\"accounts\">");
        foreach (DeletableAccount account in prepared.Accounts)
        {
            body.Append("<li><strong>" + Encode(account.Nickname ?? "(senza nickname)") + "</strong>");
            string created = ShortDate(account.CreatedAt);
            string seen = ShortDate(account.LastLoginAt);
            if (created != null)
                body.Append("<span>Creato il " + Encode(created) + "</span>");
            if (seen != null)
                body.Append("<span>Ultimo accesso il " + Encode(seen) + "</span>");
            body.Append("</li>");
        }
        body.Append("</ul>");

        body.Append(WhatGetsDeleted());
        body.Append("<p class=\"warn\">Premendo il pulsante i dati vengono eliminati subito e in modo "
            + "definitivo. Non e' possibile annullare.</p>");
        body.Append("<form method=\"post\" action=\"" + PagePath + "/confirm\">"
            + "<input type=\"hidden\" name=\"ticket\" value=\"" + Encode(prepared.Ticket) + "\">"
            + "<button type=\"submit\" class=\"danger\">Elimina definitivamente</button></form>");
        body.Append("<p class=\"muted\">Se hai cambiato idea, chiudi semplicemente questa pagina: "
            + "senza conferma non viene cancellato nulla. La richiesta scade da sola dopo 15 minuti.</p>");
        return body.ToString();
    }

    private static string DeletedPage(int deleted)
    {
        var body = new StringBuilder();
        body.Append("<h1>Account eliminato</h1>");
        body.Append(deleted == 1
            ? "<p class=\"ok\">Il tuo account e tutti i dati collegati sono stati eliminati.</p>"
            : "<p class=\"ok\">" + deleted + " account e tutti i dati collegati sono stati eliminati.</p>");
        body.Append("<p>Se hai ancora il gioco installato puoi disinstallarlo: al prossimo avvio "
            + "ripartiresti comunque da un profilo nuovo.</p>");
        body.Append("<p class=\"muted\">I log tecnici del server (diagnostica, senza dati di profilo) "
            + "possono conservare tracce per un breve periodo prima della rotazione automatica.</p>");
        return body.ToString();
    }

    private static string MessagePage(string title, string message, bool neutral)
    {
        var body = new StringBuilder();
        body.Append("<h1>" + Encode(title) + "</h1>");
        body.Append("<p class=\"" + (neutral ? "" : "warn") + "\">" + Encode(message) + "</p>");
        body.Append("<p><a href=\"" + PagePath + "\">Torna all'inizio</a></p>");
        return body.ToString();
    }

    private static string WhatGetsDeleted() =>
        "<p>Vengono eliminati:</p><ul>"
        + "<li>l'account e il collegamento con il tuo profilo Google, email compresa;</li>"
        + "<li>nickname, icona e biografia del profilo;</li>"
        + "<li>progressi di campagna, miele, sblocchi, oggetti e quest della taverna;</li>"
        + "<li>statistiche, punteggio classificato, storico delle partite e amicizie;</li>"
        + "<li>la presenza nella Hall of Fame delle stagioni passate.</li></ul>";

    /// <summary>Le date in archivio sono ISO 8601 UTC: all'utente basta il giorno.</summary>
    private static string ShortDate(string isoDate) =>
        DateTime.TryParse(isoDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed.ToUniversalTime().ToString("dd/MM/yyyy")
            : null;

    // ---- Infrastruttura -------------------------------------------------------

    private static IResult Html(string body) =>
        Results.Content(Layout(body), "text/html; charset=utf-8");

    private static string Layout(string body) =>
        "<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\">"
        + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
        + "<meta name=\"robots\" content=\"noindex\">"
        + "<title>AcCardNDie - Cancellazione account</title><style>"
        + "body{font-family:system-ui,sans-serif;background:#130d25;color:#f4efe6;margin:0;"
        + "padding:2.5rem 1.25rem;line-height:1.55}"
        + "main{max-width:34rem;margin:0 auto}"
        + "h1{font-size:1.5rem;margin:0 0 1.25rem}"
        + "p{margin:0 0 1rem}"
        + "ul{margin:0 0 1rem;padding-left:1.25rem}"
        + "li{margin-bottom:.35rem}"
        + "ul.accounts{list-style:none;padding:0}"
        + "ul.accounts li{background:#1e1636;border-radius:.5rem;padding:.75rem .9rem;margin-bottom:.6rem}"
        + "ul.accounts span{display:block;font-size:.85rem;color:#b6abd0}"
        + ".warn{color:#f0c674}.ok{color:#9fe0a8}.muted{font-size:.85rem;color:#b6abd0}"
        + "button{font:inherit;border:0;border-radius:.5rem;padding:.75rem 1.25rem;cursor:pointer;"
        + "width:100%;margin:.5rem 0 1rem}"
        + "button.primary{background:#f4efe6;color:#130d25}"
        + "button.danger{background:#a3242c;color:#fff;font-weight:600}"
        + "a{color:#c9b6ff}"
        + "</style></head><body><main>" + body + "</main></body></html>";

    private static void NoStore(HttpContext context) =>
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

    private static async Task<string> ReadFormValue(HttpContext context, string key)
    {
        // Lettura manuale invece del binding [FromForm]: il binding attiverebbe la
        // validazione antiforgery dei minimal API, che qui non serve perche' il
        // ticket e' gia' un segreto monouso e non c'e' nessuna sessione da abusare.
        try
        {
            if (!context.Request.HasFormContentType)
                return null;
            IFormCollection form = await context.Request.ReadFormAsync();
            return form[key];
        }
        catch
        {
            return null;
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
