using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AccardND.Server.Accounts;

/// <summary>
/// Broker OAuth per il login Google dei client che non hanno un browser
/// integrato (Android nativo, e in prospettiva desktop).
///
/// Perche' passa dal server: un client OAuth "Android" produce un ID token con
/// audience diversa da quella configurata sul provider Google di Unity
/// Authentication, e Google non accetta redirect loopback per quel tipo di
/// client. Facendo lo scambio qui si usa lo stesso Web Client ID del login web,
/// quindi lo stesso account Google genera lo stesso PlayerId UGS su APK e PWA:
/// e' il motivo per cui Google Play Games e' stato dismesso, sdoppiava i profili.
///
/// Il segreto OAuth non lascia mai il server. L'app dimostra di essere la stessa
/// che ha aperto il browser presentando un verifier monouso di cui, all'avvio,
/// aveva mandato solo l'hash.
/// </summary>
public sealed class GoogleOAuthBroker
{
    private sealed class PendingLogin
    {
        public string RequestId;
        public string State;
        public string ChallengeHash;
        public string CodeVerifier;
        public DateTime ExpiresAt;
        public string IdToken;
        public string Error;
        public string Purpose = PurposeApp;
    }

    /// <summary>Flusso normale: l'app apre il browser e poi ritira il token con Redeem.</summary>
    public const string PurposeApp = "app";

    /// <summary>
    /// Flusso che si esaurisce nel browser: la pagina di cancellazione account.
    /// Nessuna app sta in attesa, quindi l'ID token viene consegnato direttamente
    /// al callback e la richiesta si chiude li'.
    /// </summary>
    public const string PurposeDeletion = "deletion";

    /// <summary>Esito del callback: per il flusso app si mostra solo il messaggio,
    /// per i flussi browser il chiamante prosegue usando l'ID token.</summary>
    public sealed record CallbackOutcome(string Purpose, string Message, bool Ok, string IdToken);

    /// <summary>Oltre questo numero di login in volo si smette di accettarne: il
    /// dizionario e' in memoria e non deve poter crescere a comando.</summary>
    private const int MaxPending = 500;

    private readonly ConcurrentDictionary<string, PendingLogin> byRequestId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> requestIdByState = new(StringComparer.Ordinal);
    private readonly ServerConfig config;
    private readonly ILogger<GoogleOAuthBroker> logger;
    private readonly HttpClient httpClient = new();

    public GoogleOAuthBroker(ServerConfig config, ILogger<GoogleOAuthBroker> logger)
    {
        this.config = config;
        this.logger = logger;
    }

    public bool IsEnabled => config.GoogleOAuth.IsEnabled;

    // ---- 1. L'app chiede di iniziare -----------------------------------------

    public (string RequestId, string AuthorizeUrl, string Error) Begin(string challenge)
    {
        if (!IsEnabled)
            return (null, null, "Login Google non configurato sul server.");
        if (!IsPlausibleChallenge(challenge))
            return (null, null, "Richiesta di login non valida.");

        Prune();
        if (byRequestId.Count >= MaxPending)
        {
            logger.LogWarning("Broker Google saturo: {Count} login in attesa.", byRequestId.Count);
            return (null, null, "Troppi accessi in corso, riprova tra poco.");
        }

        var pending = new PendingLogin
        {
            RequestId = CreateUrlSafeRandom(32),
            State = CreateUrlSafeRandom(32),
            ChallengeHash = challenge,
            // PKCE della tratta server <-> Google: impedisce che un codice di
            // autorizzazione rubato altrove venga speso sul nostro callback.
            CodeVerifier = CreateUrlSafeRandom(64),
            ExpiresAt = DateTime.UtcNow.AddMinutes(config.GoogleOAuth.RequestTtlMinutes)
        };

        byRequestId[pending.RequestId] = pending;
        requestIdByState[pending.State] = pending.RequestId;

        return (pending.RequestId, AuthorizeUrl(pending), null);
    }

    /// <summary>
    /// Avvia un accesso che si conclude nel browser invece che nell'app: oggi solo
    /// la pagina di cancellazione account. Non c'e' un verifier perche' non c'e'
    /// nessuno che debba ritirare il token dopo; il callback lo consegna e chiude.
    /// Riusa lo stesso redirect_uri del login, cosi' non serve registrarne un
    /// secondo sulla console Google.
    /// </summary>
    public (string AuthorizeUrl, string Error) BeginBrowserFlow(string purpose)
    {
        if (!IsEnabled)
            return (null, "Login Google non configurato sul server.");

        Prune();
        if (byRequestId.Count >= MaxPending)
        {
            logger.LogWarning("Broker Google saturo: {Count} login in attesa.", byRequestId.Count);
            return (null, "Troppi accessi in corso, riprova tra poco.");
        }

        var pending = new PendingLogin
        {
            RequestId = CreateUrlSafeRandom(32),
            State = CreateUrlSafeRandom(32),
            ChallengeHash = null,
            CodeVerifier = CreateUrlSafeRandom(64),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(config.GoogleOAuth.RequestTtlMinutes)
        };

        byRequestId[pending.RequestId] = pending;
        requestIdByState[pending.State] = pending.RequestId;

        return (AuthorizeUrl(pending), null);
    }

    private string AuthorizeUrl(PendingLogin pending) =>
        "https://accounts.google.com/o/oauth2/v2/auth"
        + "?client_id=" + Uri.EscapeDataString(config.GoogleOAuth.ClientId)
        + "&redirect_uri=" + Uri.EscapeDataString(config.GoogleOAuth.RedirectUri)
        + "&response_type=code"
        + "&scope=" + Uri.EscapeDataString("openid email profile")
        + "&state=" + Uri.EscapeDataString(pending.State)
        + "&code_challenge=" + Uri.EscapeDataString(Sha256Base64Url(pending.CodeVerifier))
        + "&code_challenge_method=S256"
        + "&prompt=select_account";

    // ---- 2. Google rimanda l'utente qui --------------------------------------

    /// <summary>
    /// Chiude il giro nel browser. Per il flusso app ritorna solo il messaggio da
    /// mostrare (il token resta in attesa di <see cref="Redeem"/>); per i flussi
    /// browser consegna l'ID token al chiamante e dimentica la richiesta.
    /// </summary>
    public async Task<CallbackOutcome> CompleteAsync(string state, string code, string googleError)
    {
        Prune();

        if (string.IsNullOrEmpty(state) || !requestIdByState.TryGetValue(state, out string requestId) ||
            !byRequestId.TryGetValue(requestId, out PendingLogin pending))
        {
            // Anche il caso "state sconosciuto" va detto in modo neutro: potrebbe
            // essere una richiesta scaduta o un tentativo di iniezione. Il messaggio
            // non nomina il gioco perche' qui non sappiamo piu' da quale flusso venga.
            return new CallbackOutcome(
                PurposeApp, "Sessione di accesso scaduta. Riprova dall'inizio.", false, null);
        }

        bool browserFlow = pending.Purpose != PurposeApp;
        string retry = browserFlow
            ? "Puoi chiudere questa pagina e riprovare."
            : "Puoi tornare al gioco e riprovare.";

        // Il codice di autorizzazione si spende una volta sola: se il browser
        // ripassa dal callback (ricarica, back/forward, prefetch del browser)
        // il secondo scambio fallirebbe con invalid_grant e marcherebbe come
        // fallito un accesso gia' riuscito. Nei flussi browser la richiesta e'
        // gia' stata dimenticata, quindi qui ci arriva solo il flusso app.
        if (pending.IdToken != null)
            return Done(pending, "Accesso completato. Torna al gioco: la schermata di login prosegue da sola.", true, null);

        if (!string.IsNullOrEmpty(googleError))
        {
            pending.Error = "Accesso Google annullato.";
            logger.LogInformation("Login Google rifiutato dall'utente: {Error}", googleError);
            if (browserFlow)
                Forget(pending);
            return Done(pending, "Accesso annullato. " + retry, false, null);
        }

        if (string.IsNullOrEmpty(code))
        {
            pending.Error = "Google non ha restituito il codice di autorizzazione.";
            if (browserFlow)
                Forget(pending);
            return Done(pending, "Accesso non riuscito. " + retry, false, null);
        }

        try
        {
            (string idToken, string exchangeError) = await ExchangeCodeAsync(code, pending.CodeVerifier);
            if (idToken == null)
            {
                pending.Error = exchangeError;
                if (browserFlow)
                    Forget(pending);
                return Done(pending, "Accesso non riuscito. " + retry, false, null);
            }

            if (browserFlow)
            {
                // Nessuno lo ritirera' con Redeem: consegnarlo qui e liberare lo slot.
                Forget(pending);
                return Done(pending, null, true, idToken);
            }

            pending.IdToken = idToken;
            return Done(pending, "Accesso completato. Torna al gioco: la schermata di login prosegue da sola.", true, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scambio del codice OAuth Google fallito.");
            pending.Error = "Scambio del codice Google fallito.";
            if (browserFlow)
                Forget(pending);
            return Done(pending, "Accesso non riuscito. " + retry, false, null);
        }
    }

    private static CallbackOutcome Done(PendingLogin pending, string message, bool ok, string idToken) =>
        new(pending.Purpose, message, ok, idToken);

    // ---- 3. L'app ritira l'ID token ------------------------------------------

    /// <summary>status: "pending" | "ready" | "error". Il token si consegna una volta sola.</summary>
    public (string Status, string IdToken, string Error) Redeem(string requestId, string verifier)
    {
        Prune();

        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(verifier) ||
            !byRequestId.TryGetValue(requestId, out PendingLogin pending))
        {
            return ("error", null, "Sessione di accesso scaduta, riprova.");
        }

        if (!MatchesChallenge(pending.ChallengeHash, verifier))
        {
            logger.LogWarning("Verifier non valido per la richiesta di login {RequestId}.", requestId);
            return ("error", null, "Sessione di accesso non valida.");
        }

        // Il token vince sull'errore: se due callback si sono accavallati, uno
        // solo ha ottenuto l'ID token e quello vale, l'altro e' rumore.
        if (pending.IdToken != null)
        {
            Forget(pending);
            return ("ready", pending.IdToken, null);
        }

        if (pending.Error != null)
        {
            Forget(pending);
            return ("error", null, pending.Error);
        }

        return ("pending", null, null);
    }

    // ---- Interno --------------------------------------------------------------

    /// <summary>
    /// Ritorna (idToken, null) oppure (null, messaggio d'errore). Il motivo vero
    /// del rifiuto finisce nel messaggio: distinguere un secret sbagliato
    /// (invalid_client) da un redirect non autorizzato (redirect_uri_mismatch) o
    /// da un codice gia' speso (invalid_grant) senza guardare i log del server
    /// era impossibile, e tutti e tre arrivavano all'app come "manca l'ID token".
    /// </summary>
    private async Task<(string IdToken, string Error)> ExchangeCodeAsync(string code, string codeVerifier)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.GoogleOAuth.ClientId,
            ["client_secret"] = config.GoogleOAuth.ResolveClientSecret(),
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = config.GoogleOAuth.RedirectUri
        });

        using HttpResponseMessage response =
            await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
        string payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            string reason = DescribeGoogleError(payload, response.StatusCode);
            // Il payload di Google e' JSON indentato: su una riga sola, altrimenti
            // journalctl lo spezza e il motivo dell'errore sparisce dopo la graffa.
            logger.LogError("Google ha rifiutato lo scambio del codice: {Status} {Reason} {Payload}",
                (int)response.StatusCode, reason, SingleLine(payload));
            return (null, $"Google ha rifiutato l'accesso ({reason}).");
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        string idToken = document.RootElement.TryGetProperty("id_token", out JsonElement element)
            ? element.GetString()
            : null;
        if (string.IsNullOrEmpty(idToken))
        {
            logger.LogError("Risposta token Google senza id_token: {Payload}", SingleLine(payload));
            return (null, "Google non ha restituito un ID token.");
        }

        return (idToken, null);
    }

    /// <summary>Il codice d'errore OAuth dal corpo della risposta, se c'e'.</summary>
    private static string DescribeGoogleError(string payload, System.Net.HttpStatusCode status)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("error", out JsonElement error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return "HTTP " + (int)status;
    }

    private static string SingleLine(string value) =>
        value?.Replace("\r", " ").Replace("\n", " ").Trim();

    private void Forget(PendingLogin pending)
    {
        byRequestId.TryRemove(pending.RequestId, out _);
        requestIdByState.TryRemove(pending.State, out _);
    }

    private void Prune()
    {
        DateTime now = DateTime.UtcNow;
        foreach (PendingLogin pending in byRequestId.Values)
        {
            if (pending.ExpiresAt <= now)
                Forget(pending);
        }
    }

    /// <summary>L'hash arriva dal client: si accetta solo se e' plausibilmente un
    /// SHA-256 in base64url, cosi' non finisce roba arbitraria in memoria.</summary>
    private static bool IsPlausibleChallenge(string challenge) =>
        !string.IsNullOrEmpty(challenge) &&
        challenge.Length is >= 42 and <= 48 &&
        challenge.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool MatchesChallenge(string expectedHash, string verifier)
    {
        byte[] expected = Encoding.ASCII.GetBytes(expectedHash);
        byte[] actual = Encoding.ASCII.GetBytes(Sha256Base64Url(verifier));
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string CreateUrlSafeRandom(int byteCount) =>
        Base64Url(RandomNumberGenerator.GetBytes(byteCount));

    private static string Sha256Base64Url(string value) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(value)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
