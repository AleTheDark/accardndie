using System.Net;
using System.Text;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using AccardND.Server.Progression;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Web;

/// <summary>
/// Pagina pubblica delle statistiche su /statistiche.
///
/// E' la sola pagina del sito che mostra dati di una persona, quindi e' l'unica
/// dietro un accesso: ci si riconosce con lo stesso account Google con cui si
/// gioca, e da li' si vede tutto quello che il server sa di quel profilo.
/// Sola lettura: da qui non si cambia niente, niente si cancella (per quello
/// c'e' /account/delete) e non si guadagna nulla.
///
/// E' generata dal server e non e' un file statico perche' i numeri arrivano dal
/// database. Prende pero' il CSS del sito (/site.css): stesso dominio, stessa
/// faccia, e la palette resta in un posto solo.
///
/// Il giro OAuth passa dal broker gia' in uso per il login e per la
/// cancellazione account (stesso redirect_uri, nessuna configurazione nuova
/// sulla console Google): il callback riconosce il flusso dal purpose.
/// </summary>
public static class StatsPageEndpoints
{
    public const string PagePath = "/statistiche";

    public static void MapStatsPageEndpoints(this WebApplication app)
    {
        var broker = app.Services.GetRequiredService<GoogleOAuthBroker>();
        var sessions = app.Services.GetRequiredService<WebSessionStore>();
        var dossiers = app.Services.GetRequiredService<PlayerDossierService>();
        var database = app.Services.GetRequiredService<AccardDatabase>();

        app.MapGet(PagePath, (HttpContext context) =>
        {
            NoStore(context);

            WebSession session = sessions.TryGet(WebSessionStore.ReadToken(context));
            if (session == null)
                return Html(IntroHeading, IntroPage(broker.IsEnabled));

            IReadOnlyList<LinkedAccount> linked = LinkedAccounts.FindByVerifiedEmail(database, session.Email);
            if (linked.Count == 0)
                return NoAccounts(session.Email);

            // L'account richiesto vale solo se e' fra quelli della sessione: il
            // controllo e' su session.PlayerIds e non sulla lista appena riletta,
            // perche' i permessi sono quelli fotografati al momento dell'accesso.
            string requested = context.Request.Query["account"];
            LinkedAccount chosen = ChooseAccount(linked, session, requested);
            if (chosen == null)
                return NoAccounts(session.Email);

            PlayerDossier dossier = dossiers.Build(chosen.PlayerId, chosen.Nickname);
            return dossier == null
                ? NoAccounts(session.Email)
                : Html("<h1>Statistiche di " + Encode(dossier.DisplayName) + "</h1>",
                       DossierPage(session, linked, chosen, dossier));
        });

        // POST e non link: un GET che apre una sessione OAuth verrebbe innescato
        // anche dal prefetch del browser, riempiendo il broker di richieste morte.
        app.MapPost(PagePath + "/accedi", (HttpContext context) =>
        {
            NoStore(context);
            (string authorizeUrl, string error) = broker.BeginBrowserFlow(GoogleOAuthBroker.PurposeStats);
            return error != null
                ? MessagePage("Non si puo' procedere", error)
                : Results.Redirect(authorizeUrl);
        });

        app.MapPost(PagePath + "/esci", (HttpContext context) =>
        {
            NoStore(context);
            sessions.Remove(WebSessionStore.ReadToken(context));
            WebSessionStore.ClearCookie(context);
            return Results.Redirect(PagePath);
        });
    }

    /// <summary>
    /// Secondo tempo dell'accesso: il callback OAuth ha in mano l'ID token
    /// verificato. Apre la sessione, la mette nel cookie e rimanda alla pagina.
    /// Il redirect non e' cosmetico: senza, l'indirizzo nella barra resterebbe
    /// quello del callback, con dentro il codice di autorizzazione, e un
    /// aggiornamento della pagina proverebbe a rigiocarlo.
    /// </summary>
    public static async Task<IResult> CompleteLoginAsync(
        HttpContext context,
        AccardDatabase database,
        GoogleIdTokenReader googleIdTokens,
        WebSessionStore sessions,
        string idToken)
    {
        NoStore(context);

        string email = await googleIdTokens.TryReadVerifiedEmailAsync(idToken);
        if (string.IsNullOrEmpty(email))
        {
            return MessagePage("Accesso non riuscito",
                "Non e' stato possibile verificare il tuo account Google. Riprova.");
        }

        IReadOnlyList<LinkedAccount> linked = LinkedAccounts.FindByVerifiedEmail(database, email);
        if (linked.Count == 0)
            return NoAccounts(email);

        string token = sessions.Create(email, linked.Select(account => account.PlayerId));
        if (token == null)
        {
            return MessagePage("Troppi accessi in corso",
                "Riprova fra qualche minuto.");
        }

        WebSessionStore.WriteCookie(context, token);
        return Results.Redirect(PagePath);
    }

    private static LinkedAccount ChooseAccount(
        IReadOnlyList<LinkedAccount> linked, WebSession session, string requested)
    {
        IEnumerable<LinkedAccount> allowed =
            linked.Where(account => session.CanRead(account.PlayerId));

        if (!string.IsNullOrEmpty(requested))
        {
            LinkedAccount match = allowed.FirstOrDefault(account =>
                string.Equals(account.PlayerId, requested, StringComparison.Ordinal));
            if (match != null)
                return match;
        }

        return allowed.FirstOrDefault();
    }

    // ---- Pagine ---------------------------------------------------------------

    private const string IntroHeading =
        "<h1>Le tue statistiche</h1>"
        + "<p class=\"lead\">Partite, vittorie, grado della stagione, progressi di campagna e "
        + "obiettivi: tutto quello che il server sa del tuo profilo, in una pagina sola. "
        + "Non serve avere il gioco aperto.</p>";

    private static string IntroPage(bool googleEnabled)
    {
        var body = new StringBuilder();
        body.Append("<p>Accedi con lo stesso account Google che usi per giocare. Serve solo a "
            + "riconoscerti: da questa pagina non si modifica e non si cancella niente.</p>");

        if (!googleEnabled)
        {
            body.Append("<p class=\"box\">Il servizio di accesso non e' al momento disponibile. "
                + "Riprova piu' tardi.</p>");
            return body.ToString();
        }

        body.Append("<form method=\"post\" action=\"" + PagePath + "/accedi\">"
            + "<button type=\"submit\" class=\"primary\">Accedi con Google</button></form>");
        body.Append("<p class=\"box\">Se giochi senza aver mai fatto l'accesso con Google, i tuoi "
            + "progressi vivono solo su quel dispositivo e qui non c'e' niente da mostrare. "
            + "Collegando l'account dal gioco cominciano a seguirti fra telefono e browser.</p>");
        return body.ToString();
    }

    private static IResult NoAccounts(string email) =>
        Html("<h1>Nessun profilo collegato</h1>",
            "<p>Non risulta nessun profilo Accard N' Die collegato a <strong>"
            + Encode(email) + "</strong>.</p>"
            + "<p>Se giochi con un altro indirizzo Google, esci e ripeti l'accesso "
            + "scegliendo quello giusto.</p>"
            + LogoutForm("Esci"));

    private static IResult MessagePage(string title, string message) =>
        Html("<h1>" + Encode(title) + "</h1>",
            "<p>" + Encode(message) + "</p>"
            + "<p><a href=\"" + PagePath + "\">Torna all'inizio</a></p>");

    private static string DossierPage(
        WebSession session,
        IReadOnlyList<LinkedAccount> linked,
        LinkedAccount chosen,
        PlayerDossier dossier)
    {
        var body = new StringBuilder();
        body.Append(SessionBar(session, linked, chosen));
        body.Append(AccountSection(dossier));
        body.Append(RankedSection(dossier.Profile));
        body.Append(PvpSection(dossier.Stats));
        body.Append(CampaignSection(dossier.Progress));
        body.Append(AchievementsSection(dossier.Achievements));
        return body.ToString();
    }

    private static string SessionBar(
        WebSession session, IReadOnlyList<LinkedAccount> linked, LinkedAccount chosen)
    {
        var bar = new StringBuilder();
        bar.Append("<div class=\"session\">");
        bar.Append("<span class=\"who\">Accesso verificato come <strong>"
            + Encode(session.Email) + "</strong></span>");

        // Un solo account e' il caso normale: il selettore comparirebbe vuoto di
        // significato. Con piu' account (capita a chi ha giocato prima da ospite e
        // poi ha collegato Google) servono link, non un menu a tendina, cosi'
        // ognuno resta un indirizzo condivisibile.
        if (linked.Count > 1)
        {
            bar.Append("<span class=\"who\">Profili:</span>");
            foreach (LinkedAccount account in linked)
            {
                string name = Encode(account.Nickname ?? "(senza nome)");
                bar.Append(string.Equals(account.PlayerId, chosen.PlayerId, StringComparison.Ordinal)
                    ? "<strong>" + name + "</strong>"
                    : "<a href=\"" + PagePath + "?account=" + Uri.EscapeDataString(account.PlayerId)
                      + "\">" + name + "</a>");
            }
        }

        bar.Append(LogoutForm("Esci"));
        bar.Append("</div>");
        return bar.ToString();
    }

    private static string LogoutForm(string label) =>
        "<form method=\"post\" action=\"" + PagePath + "/esci\">"
        + "<button type=\"submit\" class=\"quiet\">" + Encode(label) + "</button></form>";

    private static string AccountSection(PlayerDossier dossier)
    {
        ProfileData profile = dossier.Profile;
        SinglePlayerProgressData progress = dossier.Progress;

        var section = new StringBuilder();
        section.Append("<h2>Account</h2>");
        section.Append("<div class=\"statgrid\">");
        section.Append(Stat(progress.accountLevel, "Livello"));
        section.Append(Stat(progress.accountTotalExperience, "Esperienza totale"));
        section.Append(Stat(progress.honey, "Miele"));
        section.Append(Stat(profile.iconsUnlocked + "/" + profile.iconsTotal, "Icone sbloccate"));
        section.Append("</div>");

        // Barra del livello: accountExperienceToNextLevel e' quanta ne serve in tutto per
        // salire, quindi e' gia' il denominatore. Qui ci si sommava sopra l'esperienza gia'
        // fatta, e la barra mostrava "45 / 145" su un livello che ne chiedeva 100.
        int toNext = Math.Max(0, progress.accountExperienceToNextLevel);
        if (toNext > 0)
        {
            section.Append("<p class=\"lead\">Livello " + progress.accountLevel + " — "
                + progress.accountExperience + " / " + toNext + " esperienza verso il livello successivo."
                + Meter(progress.accountExperience, toNext, false) + "</p>");
        }

        if (progress.pendingLevelRewards > 0)
        {
            section.Append("<p>Hai <strong>" + progress.pendingLevelRewards
                + "</strong> ricompense di livello ancora da riscuotere nel gioco.</p>");
        }

        return section.ToString();
    }

    private static string RankedSection(ProfileData profile)
    {
        var section = new StringBuilder();
        section.Append("<h2>Grado della stagione</h2>");
        section.Append("<div class=\"rank\">");

        if (!profile.ranked)
        {
            section.Append("<div class=\"tier\">Non classificato</div>");
            section.Append("<p>Non hai ancora giocato partite classificate in "
                + Encode(SeasonLabel(profile)) + ".</p>");
        }
        else if (profile.placement)
        {
            section.Append("<div class=\"tier\">Piazzamento in corso</div>");
            section.Append("<p>Mancano <strong>" + profile.placementRemaining
                + "</strong> partite prima che il grado venga assegnato.</p>");
        }
        else
        {
            section.Append("<div class=\"tier\">"
                + SiteLayout.RankBadge(profile.tier, profile.division) + "</div>");
            section.Append("<p>" + profile.leaguePoints + " punti lega"
                + (profile.globalRank > 0
                    ? " — posizione " + profile.globalRank + " su " + profile.globalPlayers
                      + " giocatori in classifica"
                    : "")
                + ".</p>");
        }

        section.Append("<p>" + Encode(SeasonLabel(profile))
            + RemainingLabel(profile.seasonSecondsRemaining) + "</p>");
        section.Append("</div>");
        return section.ToString();
    }

    private static string PvpSection(StatsData stats)
    {
        var section = new StringBuilder();
        section.Append("<h2>Duelli online</h2>");
        section.Append("<h3>Da sempre</h3>");
        section.Append(StatsGrid(stats.lifetime));
        section.Append("<h3>Stagione in corso</h3>");
        section.Append(StatsGrid(stats.season));
        return section.ToString();
    }

    private static string StatsGrid(PlayerStatsDto stats)
    {
        stats ??= new PlayerStatsDto();
        var grid = new StringBuilder();
        grid.Append("<div class=\"statgrid\">");
        grid.Append(Stat(stats.matches, "Partite"));
        grid.Append(Stat(stats.wins, "Vittorie"));
        grid.Append(Stat(stats.losses, "Sconfitte"));
        grid.Append(Stat(stats.winRatePercent + "%", "Percentuale"));
        grid.Append(Stat(stats.roundsWon, "Round vinti"));
        grid.Append(Stat(stats.roundsLost, "Round persi"));
        grid.Append(Stat(stats.currentStreak, "Serie attuale"));
        grid.Append(Stat(stats.bestStreak, "Serie migliore"));
        grid.Append(Stat(stats.forfeits, "Abbandoni"));
        grid.Append("</div>");
        return grid.ToString();
    }

    private static string CampaignSection(SinglePlayerProgressData progress)
    {
        var section = new StringBuilder();
        section.Append("<h2>Campagna</h2>");

        section.Append("<h3>Capitoli</h3>");
        section.Append("<ul class=\"chips\">");
        foreach (ChapterCatalog.Chapter chapter in ChapterCatalog.All)
        {
            bool cleared = Contains(progress.clearedChapters, chapter.Id);
            bool unlocked = Contains(progress.unlockedChapters, chapter.Id);
            string state = cleared ? "on" : unlocked ? "" : "off";
            string suffix = cleared ? " ✔" : unlocked ? "" : " (bloccato)";
            section.Append("<li class=\"" + state + "\">"
                + Encode(chapter.Name) + suffix + "</li>");
        }
        section.Append("</ul>");

        section.Append("<h3>Classi sbloccate</h3>");
        section.Append("<ul class=\"chips\">");
        foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All
                     .Where(candidate => candidate.Type == SanctuaryCatalog.TypeClass))
        {
            bool owned = Contains(progress.unlockedClasses, entry.Id);
            section.Append("<li class=\"" + (owned ? "on" : "off") + "\">"
                + Encode(entry.Name) + "</li>");
        }
        section.Append("</ul>");

        section.Append("<h3>Contatori</h3>");
        if (progress.counters == null || progress.counters.Length == 0)
        {
            section.Append("<p>Nessuna run conclusa: i contatori si riempiono a fine partita.</p>");
        }
        else
        {
            section.Append("<div class=\"statgrid\">");
            foreach (PlayerCounterData counter in progress.counters)
                section.Append(Stat(counter.value, CounterLabel(counter.key)));
            section.Append("</div>");
        }

        if (progress.hardcoreUnlocked)
            section.Append("<p>Modalita <strong>hardcore</strong> sbloccata.</p>");

        return section.ToString();
    }

    private static string AchievementsSection(AchievementsData achievements)
    {
        AchievementDto[] list = achievements?.achievements ?? Array.Empty<AchievementDto>();
        int done = list.Count(achievement => achievement.unlocked);

        var section = new StringBuilder();
        section.Append("<h2>Obiettivi</h2>");
        section.Append("<p class=\"lead\">" + done + " su " + list.Length + " completati.</p>");
        section.Append("<ul class=\"ach\">");
        foreach (AchievementDto achievement in list)
        {
            int threshold = Math.Max(1, achievement.threshold);
            int progress = Math.Min(achievement.progress, threshold);
            section.Append("<li>");
            section.Append("<span class=\"count\">" + progress + " / " + threshold + "</span>");
            section.Append("<div class=\"name" + (achievement.unlocked ? " done" : "") + "\">"
                + Encode(achievement.name) + (achievement.unlocked ? " ✔" : "") + "</div>");
            section.Append("<div class=\"desc\">" + Encode(achievement.description) + "</div>");
            section.Append(Meter(progress, threshold, achievement.unlocked));
            section.Append("</li>");
        }
        section.Append("</ul>");
        return section.ToString();
    }

    // ---- Pezzi di markup ------------------------------------------------------

    private static string Stat(int value, string label) => Stat(value.ToString(), label);

    private static string Stat(string value, string label) =>
        "<div class=\"stat\"><b>" + Encode(value) + "</b><span>" + Encode(label) + "</span></div>";

    private static string Meter(int value, int total, bool done)
    {
        int percent = total <= 0 ? 0 : (int)Math.Round(Math.Clamp(value * 100.0 / total, 0, 100));
        return "<span class=\"meter" + (done ? " done" : "") + "\">"
            + "<i style=\"width:" + percent + "%\"></i></span>";
    }

    private static string SeasonLabel(ProfileData profile) =>
        string.IsNullOrEmpty(profile.seasonName)
            ? "Stagione " + profile.seasonId
            : profile.seasonName;

    /// <summary>Alla fine della stagione mancano giorni, non secondi: quello che
    /// interessa e' se c'e' tempo per risalire, non il conto alla rovescia.</summary>
    private static string RemainingLabel(int secondsRemaining)
    {
        if (secondsRemaining <= 0)
            return ".";

        var remaining = TimeSpan.FromSeconds(secondsRemaining);
        if (remaining.TotalDays >= 1)
            return " — finisce fra " + (int)remaining.TotalDays + " giorni.";
        if (remaining.TotalHours >= 1)
            return " — finisce fra " + (int)remaining.TotalHours + " ore.";
        return " — finisce oggi.";
    }

    private static readonly Dictionary<string, string> CounterLabels = new(StringComparer.Ordinal)
    {
        [CampaignCounters.EnemiesDefeated] = "Nemici sconfitti",
        [CampaignCounters.RoomsCleared] = "Stanze ripulite",
        [CampaignCounters.RunsEnded] = "Run concluse",
        [CampaignCounters.BossesDefeated] = "Boss sconfitti",
        [CampaignCounters.MinibossesDefeated] = "Miniboss sconfitti",
        [CampaignCounters.DiceRolled] = "Dadi tirati",
        [CampaignCounters.AbilitiesUsed] = "Abilita usate",
        [CampaignCounters.ItemsUsed] = "Oggetti usati",
        [CampaignCounters.ExperienceEarned] = "Esperienza in run",
        [CampaignCounters.DailyCompleted] = "Giornate di taverna",
        [CampaignCounters.PvpMatches] = "Partite online",
        [CampaignCounters.PvpWins] = "Vittorie online",
        [CampaignCounters.PvpRoundsWon] = "Round vinti online"
    };

    /// <summary>
    /// I contatori mirati (boss_bragus, miniboss_golem, ...) non stanno nella
    /// tabella: sono tanti e nascono man mano che si aggiungono avversari. Per
    /// quelli basta rendere leggibile la chiave, che e' gia' parlante, invece di
    /// nasconderli o di dover ricordarsi di aggiungerli qui a ogni boss nuovo.
    /// </summary>
    private static string CounterLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "—";
        if (CounterLabels.TryGetValue(key, out string label))
            return label;

        string readable = key.Replace('_', ' ');
        return char.ToUpperInvariant(readable[0]) + readable.Substring(1);
    }

    private static bool Contains(string[] values, string needle) =>
        values != null && values.Any(value =>
            string.Equals(value, needle, StringComparison.OrdinalIgnoreCase));

    // ---- Infrastruttura -------------------------------------------------------

    /// <summary>
    /// Stessa impalcatura delle pagine statiche (nav, main, footer) e stesso
    /// foglio di stile: da fuori questa deve sembrare una pagina del sito come le
    /// altre, non un pannello di servizio. noindex perche' e' dietro un accesso e
    /// non ha niente da far indicizzare.
    ///
    /// Testata e corpo viaggiano separati perche' finiscono in due posti diversi:
    /// il titolo (e la riga che lo accompagna) sta nella fascia illustrata a tutta
    /// larghezza, il resto nella colonna di testo.
    /// </summary>
    private static IResult Html(string heading, string body) =>
        SiteLayout.Page(
            "Le tue statistiche — AcCard N' Die", heading, body, PagePath, noIndex: true);

    private static void NoStore(HttpContext context) =>
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
