using System.Globalization;
using System.Text;
using AccardND.NetProtocol;
using AccardND.Server.Progression;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Web;

/// <summary>
/// Hall of Fame pubblica su /hall-of-fame: la classifica ranked di tutti i
/// giocatori, senza accesso e senza avere il gioco aperto.
///
/// Non e' la pagina delle statistiche con un altro nome: li' si guarda il proprio
/// profilo e serve dimostrare di esserne il proprietario, qui si guarda una
/// graduatoria che nel gioco vedono gia' tutti. Percio' niente login e niente
/// cookie, e per lo stesso motivo si mostrano solo nickname e grado: non
/// player_id (che e' una chiave, non un nome), non email, non l'MMR, che il
/// client non ha mai visto e non deve vedere da qui.
///
/// La stagione in corso e' viva e si legge da ranked_state; le stagioni chiuse
/// sono le istantanee di fine stagione (tabella hall_of_fame), le stesse che
/// assegnano le icone dei primi classificati.
/// </summary>
public static class HallOfFamePageEndpoints
{
    public const string PagePath = "/hall-of-fame";

    /// <summary>Quanti posti si mostrano. Cento e' una pagina lunga ma leggibile,
    /// ed e' il doppio della leaderboard in gioco: qui c'e' spazio.</summary>
    private const int TopCount = 100;

    /// <summary>
    /// Un minuto di cache. La classifica cambia solo a fine partita e la pagina
    /// e' pubblica: senza questa riga ogni visita, e ogni crawler, sarebbero cento
    /// righe rilette dal database. Un minuto di ritardo su una graduatoria non lo
    /// nota nessuno.
    /// </summary>
    private const string CacheHeader = "public, max-age=60";

    public static void MapHallOfFamePageEndpoints(this WebApplication app)
    {
        var ranked = app.Services.GetRequiredService<RankedService>();
        var seasons = app.Services.GetRequiredService<SeasonService>();
        var hallOfFame = app.Services.GetRequiredService<HallOfFameService>();

        app.MapGet(PagePath, (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = CacheHeader;

            HallOfFameSeasonDto[] past = hallOfFame.GetSeasons().seasons ?? Array.Empty<HallOfFameSeasonDto>();
            int requested = ReadSeasonId(context.Request.Query["stagione"]);

            // Una stagione chiusa si mostra solo se ha davvero un'istantanea: un id
            // inventato nell'indirizzo torna alla stagione in corso invece di dare
            // una pagina vuota.
            HallOfFameSeasonDto chosen = past.FirstOrDefault(season => season.seasonId == requested);
            return chosen != null
                ? PastSeasonPage(hallOfFame, past, chosen)
                : CurrentSeasonPage(ranked, seasons, past);
        });
    }

    // ---- Stagione in corso ----------------------------------------------------

    private static IResult CurrentSeasonPage(
        RankedService ranked, SeasonService seasons, HallOfFameSeasonDto[] past)
    {
        int seasonId = seasons.ActiveSeasonId;
        IReadOnlyList<LeaderboardRow> rows = ranked.GetLeaderboard(seasonId, TopCount);
        int players = ranked.CountRanked(seasonId);

        var body = new StringBuilder();
        body.Append(SeasonPicker(past, 0));
        body.Append("<h2>" + SiteLayout.Encode(seasons.ActiveSeasonName ?? "Stagione " + seasonId)
            + "</h2>");
        body.Append("<p class=\"lead\">" + PlayersLabel(players)
            + RemainingLabel(seasons.ActiveSeasonSecondsRemaining) + "</p>");

        if (rows.Count == 0)
        {
            body.Append("<p class=\"box\">La classifica di questa stagione e' ancora vuota: "
                + "si apre con la prima partita classificata. "
                + "<a href=\"/game/\">Tocca a te.</a></p>");
        }
        else
        {
            body.Append(CurrentSeasonTable(rows));
            if (players > rows.Count)
            {
                body.Append("<p class=\"muted\">Sono mostrati i primi " + rows.Count
                    + " su " + players + " classificati. La tua posizione esatta, anche fuori "
                    + "da questa pagina, si legge nelle <a href=\"/statistiche\">tue statistiche</a>.</p>");
            }
        }

        body.Append(Closing());
        return Render("La classifica in corso", CurrentSeasonLead(), body.ToString());
    }

    private static string CurrentSeasonTable(IReadOnlyList<LeaderboardRow> rows)
    {
        var table = new StringBuilder();
        table.Append("<div class=\"scroll\"><table class=\"ladder\">");
        table.Append("<thead><tr>"
            + "<th class=\"pos\">#</th><th>Giocatore</th><th>Grado</th>"
            + "<th class=\"num\">Punti lega</th><th class=\"num\">Partite</th>"
            + "<th class=\"num\">V / S</th></tr></thead><tbody>");

        int position = 0;
        foreach (LeaderboardRow row in rows)
        {
            position++;
            table.Append("<tr" + PodiumClass(position) + ">");
            table.Append("<td class=\"pos\">" + position + "</td>");
            table.Append("<td class=\"who\">" + PlayerName(row.Username) + "</td>");

            // Durante il piazzamento il grado non esiste ancora: mostrarne uno
            // sarebbe un'informazione inventata, e in gioco resta nascosto.
            if (!row.PlacementDone)
            {
                table.Append("<td class=\"muted\">In piazzamento</td>");
                table.Append("<td class=\"num muted\">—</td>");
            }
            else
            {
                table.Append("<td class=\"grade\">" + Tier(row.Tier.TierName, row.Tier.Division) + "</td>");
                table.Append("<td class=\"num\">" + row.Tier.LeaguePoints + "</td>");
            }

            // Le partite sono la somma di vinte e perse, non games_played: vengono
            // dallo stesso conteggio, quindi le tre cifre della riga tornano sempre.
            table.Append("<td class=\"num\">" + (row.Wins + row.Losses) + "</td>");
            table.Append("<td class=\"num\">" + row.Wins + " / " + row.Losses + "</td>");
            table.Append("</tr>");
        }

        table.Append("</tbody></table></div>");
        return table.ToString();
    }

    // ---- Stagioni concluse ----------------------------------------------------

    private static IResult PastSeasonPage(
        HallOfFameService hallOfFame, HallOfFameSeasonDto[] past, HallOfFameSeasonDto chosen)
    {
        // requester null: questa pagina non sa chi la sta guardando, quindi non c'e'
        // nessuna riga personale da aggiungere in fondo alla classifica.
        HallOfFameData data = hallOfFame.GetHallOfFame(chosen.seasonId, requester: null, TopCount);
        HallOfFameEntry[] entries = data.entries ?? Array.Empty<HallOfFameEntry>();

        var body = new StringBuilder();
        body.Append(SeasonPicker(past, chosen.seasonId));
        body.Append("<h2>" + SiteLayout.Encode(SeasonName(chosen)) + "</h2>");
        body.Append("<p class=\"lead\">Classifica definitiva, congelata alla chiusura della stagione"
            + EndedLabel(chosen.endedAt) + " " + PlayersLabel(chosen.participants) + "</p>");

        if (entries.Length == 0)
        {
            body.Append("<p class=\"box\">Questa stagione si e' chiusa senza nessun classificato.</p>");
        }
        else
        {
            body.Append(PastSeasonTable(entries));
            if (chosen.participants > entries.Length)
            {
                body.Append("<p class=\"muted\">Sono mostrati i primi " + entries.Length
                    + " su " + chosen.participants + " classificati.</p>");
            }
        }

        body.Append(Closing());
        return Render(SeasonName(chosen), PastSeasonLead(), body.ToString());
    }

    private static string PastSeasonTable(HallOfFameEntry[] entries)
    {
        var table = new StringBuilder();
        table.Append("<div class=\"scroll\"><table class=\"ladder\">");
        table.Append("<thead><tr>"
            + "<th class=\"pos\">#</th><th>Giocatore</th><th>Grado finale</th>"
            + "<th class=\"num\">V / S</th></tr></thead><tbody>");

        foreach (HallOfFameEntry entry in entries)
        {
            table.Append("<tr" + PodiumClass(entry.rank) + ">");
            table.Append("<td class=\"pos\">" + entry.rank + "</td>");
            table.Append("<td class=\"who\">" + PlayerName(entry.username) + "</td>");
            table.Append("<td class=\"grade\">" + Tier(entry.tier, entry.division) + "</td>");
            table.Append("<td class=\"num\">" + entry.wins + " / " + entry.losses + "</td>");
            table.Append("</tr>");
        }

        table.Append("</tbody></table></div>");
        return table.ToString();
    }

    // ---- Pezzi comuni ---------------------------------------------------------

    /// <summary>
    /// Le stagioni sono link e non un menu a tendina: cosi' ognuna e' un indirizzo
    /// che si puo' mandare a qualcuno, e la pagina continua a funzionare senza
    /// JavaScript.
    /// </summary>
    private static string SeasonPicker(HallOfFameSeasonDto[] past, int currentSeasonId)
    {
        if (past.Length == 0)
            return string.Empty;

        var picker = new StringBuilder();
        picker.Append("<ul class=\"chips seasons\">");
        picker.Append(SeasonChip(PagePath, "Stagione in corso", currentSeasonId == 0));
        foreach (HallOfFameSeasonDto season in past)
        {
            picker.Append(SeasonChip(
                PagePath + "?stagione=" + season.seasonId,
                SeasonName(season),
                currentSeasonId == season.seasonId));
        }
        picker.Append("</ul>");
        return picker.ToString();
    }

    private static string SeasonChip(string href, string label, bool current) =>
        current
            ? "<li class=\"on\" aria-current=\"page\">" + SiteLayout.Encode(label) + "</li>"
            : "<li><a href=\"" + href + "\">" + SiteLayout.Encode(label) + "</a></li>";

    private static string Closing() =>
        "<p class=\"box\">Si sale una partita alla volta: il grado viene assegnato dopo le "
        + "partite di piazzamento e le stagioni si chiudono ogni tre mesi, congelando la "
        + "classifica qui dentro per sempre. I tuoi numeri, posizione compresa, stanno "
        + "nelle <a href=\"/statistiche\">tue statistiche</a>.</p>";

    private static string CurrentSeasonLead() =>
        "<h1>Hall of Fame</h1>"
        + "<p class=\"lead\">La classifica dei duelli online: chi comanda la stagione in corso "
        + "e chi ha chiuso in testa quelle passate.</p>";

    private static string PastSeasonLead() =>
        "<h1>Hall of Fame</h1>"
        + "<p class=\"lead\">Una stagione conclusa: la classifica com'era all'ultimo minuto, "
        + "e com'e' rimasta.</p>";

    private static IResult Render(string subtitle, string heading, string body) =>
        SiteLayout.Page(
            "Hall of Fame — " + subtitle + " — AcCard N' Die",
            heading,
            body,
            PagePath,
            noIndex: false,
            description: "La classifica dei duelli online di AcCard N' Die: stagione in corso "
                + "e albo d'oro delle stagioni concluse.",
            canonical: "https://accardndie.com" + PagePath);

    /// <summary>Un nickname mancante e' un account cancellato o mai nominato: il
    /// posto in classifica resta, il nome no.</summary>
    private static string PlayerName(string username) =>
        string.IsNullOrWhiteSpace(username)
            ? "<span class=\"muted\">(senza nome)</span>"
            : SiteLayout.Encode(username);

    private static string Tier(string tier, string division) =>
        SiteLayout.RankBadge(tier, division);

    /// <summary>Oro, argento e bronzo: sul podio il numero non basta.</summary>
    private static string PodiumClass(int rank) => rank switch
    {
        1 => " class=\"gold\"",
        2 => " class=\"silver\"",
        3 => " class=\"bronze\"",
        _ => string.Empty
    };

    private static string PlayersLabel(int players) => players switch
    {
        0 => "Nessun giocatore in classifica.",
        1 => "Un giocatore in classifica.",
        _ => players + " giocatori in classifica."
    };

    private static string RemainingLabel(int secondsRemaining)
    {
        if (secondsRemaining <= 0)
            return string.Empty;

        var remaining = TimeSpan.FromSeconds(secondsRemaining);
        if (remaining.TotalDays >= 1)
            return " Finisce fra " + (int)remaining.TotalDays + " giorni.";
        if (remaining.TotalHours >= 1)
            return " Finisce fra " + (int)remaining.TotalHours + " ore.";
        return " Finisce oggi.";
    }

    private static string EndedLabel(string endedAt) =>
        DateTime.TryParse(endedAt, null, DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? ", il " + parsed.ToUniversalTime().ToString("dd/MM/yyyy") + "."
            : ".";

    private static string SeasonName(HallOfFameSeasonDto season) =>
        string.IsNullOrEmpty(season.name) ? "Stagione " + season.seasonId : season.name;

    private static int ReadSeasonId(string raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
}
