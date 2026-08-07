using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace AccardND.Server.Web;

/// <summary>
/// Una sessione del sito: chi ha dimostrato di possedere quell'email e quali
/// account puo' quindi guardare. PlayerIds e' la lista chiusa dei permessi, non
/// un suggerimento: una pagina che accetta un player_id dalla query string deve
/// controllarlo qui dentro, altrimenti diventa un lettore di statistiche altrui.
/// </summary>
public sealed record WebSession(string Email, string[] PlayerIds, DateTime ExpiresAt)
{
    /// <summary>
    /// Il permesso di leggere i dati di un profilo. E' un metodo e non un controllo
    /// sparso nelle pagine perche' e' l'unica cosa che separa "guardo le mie
    /// statistiche" da "guardo quelle di chiunque sappia scrivere un player_id
    /// nell'indirizzo": deve avere un posto solo, e un test suo.
    /// </summary>
    public bool CanRead(string playerId) =>
        !string.IsNullOrEmpty(playerId) &&
        PlayerIds != null &&
        PlayerIds.Contains(playerId, StringComparer.Ordinal);
}

/// <summary>
/// Sessioni delle pagine pubbliche del sito (oggi solo /statistiche).
///
/// Deliberatamente in memoria e non nel database: valgono poche ore, non
/// sopravvivono a un riavvio del server e non sono un dato da conservare. Chi
/// aveva la pagina aperta rifa' l'accesso con Google, che dura tre secondi.
///
/// Non e' il registro delle sessioni di gioco (<c>SessionTokenRegistry</c>):
/// quelle autenticano un client che gioca e possono scrivere. Questa autorizza
/// solo la lettura di pagine HTML, e tenerle separate significa che un buco qui
/// non puo' diventare una partita giocata da qualcun altro.
/// </summary>
public sealed class WebSessionStore
{
    /// <summary>Nome del cookie. Il prefisso __Host- vincola il browser a servirlo
    /// solo su HTTPS, senza dominio, con Path=/: se qualcuno prova a impostarlo da
    /// un sottodominio, il browser lo rifiuta da solo.</summary>
    public const string CookieName = "__Host-acnd_web";

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(4);

    /// <summary>Come per il broker OAuth: un dizionario in memoria non deve poter
    /// crescere su comando di chi passa dal sito.</summary>
    private const int MaxSessions = 2000;

    private readonly ConcurrentDictionary<string, WebSession> sessions = new(StringComparer.Ordinal);

    /// <summary>Apre una sessione e ritorna il token da mettere nel cookie.
    /// Null se non c'e' spazio: il chiamante lo dice all'utente e finisce li'.</summary>
    public string Create(string email, IEnumerable<string> playerIds)
    {
        Prune();
        if (sessions.Count >= MaxSessions)
            return null;

        string token = CreateUrlSafeRandom(32);
        sessions[token] = new WebSession(
            email,
            playerIds?.ToArray() ?? Array.Empty<string>(),
            DateTime.UtcNow.Add(Lifetime));
        return token;
    }

    public WebSession TryGet(string token)
    {
        if (string.IsNullOrEmpty(token) || !sessions.TryGetValue(token, out WebSession session))
            return null;

        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            sessions.TryRemove(token, out _);
            return null;
        }

        return session;
    }

    public void Remove(string token)
    {
        if (!string.IsNullOrEmpty(token))
            sessions.TryRemove(token, out _);
    }

    // ---- Cookie ---------------------------------------------------------------

    public static string ReadToken(HttpContext context) =>
        context.Request.Cookies[CookieName];

    public static void WriteCookie(HttpContext context, string token)
    {
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,          // niente JavaScript: non serve, e cosi' non e' rubabile
            Secure = true,            // richiesto dal prefisso __Host-
            SameSite = SameSiteMode.Lax, // Lax e non Strict: il ritorno da Google e' cross-site
            Path = "/",               // richiesto dal prefisso __Host-
            IsEssential = true        // sessione tecnica: non e' un cookie da consenso
            // Niente Expires/MaxAge: cookie di sessione, muore con il browser.
        });
    }

    public static void ClearCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    // ---- Interno --------------------------------------------------------------

    private void Prune()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, WebSession> entry in sessions)
        {
            if (entry.Value.ExpiresAt <= now)
                sessions.TryRemove(entry.Key, out _);
        }
    }

    private static string CreateUrlSafeRandom(int byteCount) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
