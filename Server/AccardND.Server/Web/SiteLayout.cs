using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Web;

/// <summary>
/// Impalcatura delle pagine del sito generate dal server (/statistiche,
/// /hall-of-fame): stessa barra, stesso piede e stesso foglio di stile delle
/// pagine statiche che nginx serve da /var/www/html.
///
/// Sta in un posto solo di proposito. Prima la nav era copiata dentro la pagina
/// delle statistiche, con tanto di commento che avvisava di aggiornare anche
/// quella: alla seconda pagina generata dal server le copie sarebbero diventate
/// tre, e una barra che cambia solo in due posti su tre e' una barra sbagliata.
/// Resta comunque una copia di quella nelle pagine .html statiche - quelle non
/// passano di qui - quindi <see cref="Sections"/> e il menu di Docs/web/*.html
/// vanno tenuti allineati a mano.
/// </summary>
public static class SiteLayout
{
    /// <summary>Le voci del menu, nell'ordine in cui compaiono nella barra.</summary>
    private static readonly (string Href, string Label)[] Sections =
    {
        ("/guida", "Come si gioca"),
        ("/strategia", "Strategia"),
        ("/classi", "Le nove classi"),
        ("/carte", "Database carte"),
        ("/campagna", "Campagna"),
        ("/duelli", "Duelli"),
        ("/hall-of-fame", "Hall of Fame")
    };

    /// <summary>
    /// Una pagina intera pronta da restituire.
    ///
    /// Testata e corpo viaggiano separati perche' finiscono in due posti diversi:
    /// il titolo (e la riga che lo accompagna) sta nella fascia illustrata a tutta
    /// larghezza, il resto nella colonna di testo.
    /// </summary>
    /// <param name="title">Titolo della finestra.</param>
    /// <param name="heading">Markup della fascia in cima (di solito un h1 e una riga).</param>
    /// <param name="body">Markup della colonna di testo.</param>
    /// <param name="currentPath">Voce di menu da segnare come pagina corrente.</param>
    /// <param name="noIndex">Vero per le pagine dietro accesso: non c'e' niente da indicizzare.</param>
    /// <param name="description">Meta description; solo per le pagine indicizzabili.</param>
    /// <param name="canonical">URL canonico assoluto; solo per le pagine indicizzabili.</param>
    public static IResult Page(
        string title,
        string heading,
        string body,
        string currentPath,
        bool noIndex,
        string description = null,
        string canonical = null) =>
        Results.Content(
            Render(title, heading, body, currentPath, noIndex, description, canonical),
            "text/html; charset=utf-8");

    private static string Render(
        string title, string heading, string body, string currentPath,
        bool noIndex, string description, string canonical)
    {
        var page = new StringBuilder();
        page.Append("<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\">");
        page.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");

        if (noIndex)
            page.Append("<meta name=\"robots\" content=\"noindex\">");
        if (!string.IsNullOrEmpty(description))
            page.Append("<meta name=\"description\" content=\"" + Encode(description) + "\">");
        if (!string.IsNullOrEmpty(canonical))
            page.Append("<link rel=\"canonical\" href=\"" + Encode(canonical) + "\">");

        page.Append("<title>" + Encode(title) + "</title>");
        page.Append("<link rel=\"stylesheet\" href=\"/site.css\">");
        page.Append("<link rel=\"apple-touch-icon\" href=\"/media/apple-touch-icon.png\">");
        page.Append("<meta name=\"theme-color\" content=\"#14100c\">");
        page.Append("<script src=\"/site.js\" defer></script>");
        page.Append("</head><body>");
        page.Append(Nav(currentPath));
        page.Append("<header class=\"band band-testata\"><div class=\"wrap\">" + heading + "</div></header>");
        page.Append("<main class=\"wrap\">" + body + "</main>");
        page.Append(Footer());
        page.Append("</body></html>");
        return page.ToString();
    }

    /// <summary>
    /// La barra, pulsante "Menu" compreso: sul telefono le sezioni si aprono da li'
    /// invece di occupare tre righe. Il pulsante parte hidden e lo accende site.js;
    /// senza JavaScript restano i link in chiaro.
    /// </summary>
    private static string Nav(string currentPath)
    {
        var nav = new StringBuilder();
        nav.Append("<nav class=\"nav\"><div class=\"wrap\">");
        nav.Append("<a class=\"brand\" href=\"/\">AcCard N' Die</a>");
        nav.Append("<button class=\"menu-toggle\" type=\"button\" aria-expanded=\"false\""
            + " aria-controls=\"menu-links\" hidden>Menu</button>");
        nav.Append("<div class=\"menu-links\" id=\"menu-links\">");
        foreach ((string href, string label) in Sections)
        {
            bool current = string.Equals(href, currentPath, StringComparison.Ordinal);
            nav.Append("<a href=\"" + href + "\""
                + (current ? " aria-current=\"page\"" : "") + ">" + label + "</a>");
        }
        nav.Append("</div>");
        nav.Append("<a class=\"nav-play\" data-play data-play-label=\"Scarica\" href=\"/game/\">Gioca</a>");
        nav.Append("</div></nav>");
        return nav.ToString();
    }

    /// <summary>
    /// Il grado con il suo emblema davanti, come si vede in gioco: prima lo
    /// scudo della lega, poi "Apprendista II" scritto per esteso.
    ///
    /// Il nome resta scritto e l'immagine e' decorativa (alt vuoto): cinque
    /// emblemi a due centimetri si somigliano tutti, e chi non carica le
    /// immagini deve continuare a leggere la classifica.
    ///
    /// Il file si sceglie dal nome del tier perche' e' l'unica cosa che il
    /// server manda: i nomi stanno in <see cref="RankedConfig.Tiers"/> e si
    /// possono rinominare da configurazione. Un tier senza emblema esce come
    /// solo testo, che e' meglio di una riga con l'immagine rotta.
    /// </summary>
    public static string RankBadge(string tier, string division)
    {
        string label = string.IsNullOrEmpty(division)
            ? tier ?? string.Empty
            : tier + " " + division;
        if (string.IsNullOrWhiteSpace(label))
            return string.Empty;

        string emblem = RankEmblem(tier);
        return "<span class=\"rankbadge\">"
            + (emblem == null
                ? string.Empty
                : "<img src=\"" + emblem + "\" alt=\"\" width=\"128\" height=\"128\" loading=\"lazy\">")
            + "<span>" + Encode(label) + "</span></span>";
    }

    /// <summary>Le immagini stanno in Docs/web/media/ranks, ridotte dagli
    /// originali di Assets/_Project/Resources/UI/MultiplayerRestyle/Ranks.</summary>
    private static string RankEmblem(string tier) =>
        (tier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "nabbo" => "/media/ranks/nabbo.png",
            "apprendista" => "/media/ranks/apprendista.png",
            "esperto" => "/media/ranks/esperto.png",
            "divino" => "/media/ranks/divino.png",
            "onnipotente" => "/media/ranks/onnipotente.png",
            _ => null
        };

    /// <summary>
    /// Il piede, in tre righe: prima le pagine del gioco, poi quelle sul progetto
    /// (chi siamo, contatti, privacy, cancellazione), poi i canali.
    ///
    /// Le stesse tre righe stanno a mano in fondo a ogni file di Docs/web/*.html:
    /// se cambia una voce qui, va cambiata anche li'.
    /// </summary>
    private static string Footer() =>
        "<footer class=\"site\"><div class=\"wrap\">"
        + "<p><a href=\"/game/\">Gioca</a> · "
        + "<a href=\"/guida\">Come si gioca</a> · "
        + "<a href=\"/strategia\">Strategia</a> · "
        + "<a href=\"/classi\">Le nove classi</a> · "
        + "<a href=\"/carte\">Database carte</a> · "
        + "<a href=\"/campagna\">Campagna</a> · "
        + "<a href=\"/duelli\">Duelli</a> · "
        + "<a href=\"/rifugio\">Il rifugio</a> · "
        + "<a href=\"/faq\">Domande frequenti</a> · "
        + "<a href=\"/hall-of-fame\">Hall of Fame</a> · "
        + "<a href=\"/statistiche\">Statistiche</a></p>"
        + "<p><a href=\"/chi-siamo\">Chi siamo</a> · "
        + "<a href=\"/contatti\">Contatti</a> · "
        + "<a href=\"/privacy\">Privacy</a> · "
        + "<a href=\"/account/delete\">Cancellazione account</a></p>"
        + "<p><a href=\"https://www.youtube.com/@accardndie\" rel=\"noopener\">YouTube</a> · "
        + "<a href=\"https://www.instagram.com/accardndie/\" rel=\"noopener\">Instagram</a> · "
        + "<a href=\"https://www.tiktok.com/@accardndie\" rel=\"noopener\">TikTok</a></p>"
        + "<p>AcCard N' Die</p>"
        + "</div></footer>";

    public static string Encode(string value) => WebUtility.HtmlEncode(value);
}
