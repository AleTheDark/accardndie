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
    }

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

        string authorizeUrl =
            "https://accounts.google.com/o/oauth2/v2/auth"
            + "?client_id=" + Uri.EscapeDataString(config.GoogleOAuth.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(config.GoogleOAuth.RedirectUri)
            + "&response_type=code"
            + "&scope=" + Uri.EscapeDataString("openid email profile")
            + "&state=" + Uri.EscapeDataString(pending.State)
            + "&code_challenge=" + Uri.EscapeDataString(Sha256Base64Url(pending.CodeVerifier))
            + "&code_challenge_method=S256"
            + "&prompt=select_account";

        return (pending.RequestId, authorizeUrl, null);
    }

    // ---- 2. Google rimanda l'utente qui --------------------------------------

    /// <summary>Ritorna il messaggio da mostrare nella pagina del browser.</summary>
    public async Task<(string Message, bool Ok)> CompleteAsync(string state, string code, string googleError)
    {
        Prune();

        if (string.IsNullOrEmpty(state) || !requestIdByState.TryGetValue(state, out string requestId) ||
            !byRequestId.TryGetValue(requestId, out PendingLogin pending))
        {
            // Anche il caso "state sconosciuto" va detto in modo neutro: potrebbe
            // essere una richiesta scaduta o un tentativo di iniezione.
            return ("Sessione di accesso scaduta. Riapri il login dal gioco.", false);
        }

        if (!string.IsNullOrEmpty(googleError))
        {
            pending.Error = "Accesso Google annullato.";
            logger.LogInformation("Login Google rifiutato dall'utente: {Error}", googleError);
            return ("Accesso annullato. Puoi tornare al gioco e riprovare.", false);
        }

        if (string.IsNullOrEmpty(code))
        {
            pending.Error = "Google non ha restituito il codice di autorizzazione.";
            return ("Accesso non riuscito. Puoi tornare al gioco e riprovare.", false);
        }

        try
        {
            string idToken = await ExchangeCodeAsync(code, pending.CodeVerifier);
            if (string.IsNullOrEmpty(idToken))
            {
                pending.Error = "Google non ha restituito un ID token.";
                return ("Accesso non riuscito. Puoi tornare al gioco e riprovare.", false);
            }

            pending.IdToken = idToken;
            return ("Accesso completato. Torna al gioco: la schermata di login prosegue da sola.", true);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scambio del codice OAuth Google fallito.");
            pending.Error = "Scambio del codice Google fallito.";
            return ("Accesso non riuscito. Puoi tornare al gioco e riprovare.", false);
        }
    }

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

        if (pending.Error != null)
        {
            Forget(pending);
            return ("error", null, pending.Error);
        }

        if (pending.IdToken == null)
            return ("pending", null, null);

        Forget(pending);
        return ("ready", pending.IdToken, null);
    }

    // ---- Interno --------------------------------------------------------------

    private async Task<string> ExchangeCodeAsync(string code, string codeVerifier)
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
            logger.LogError("Google ha rifiutato lo scambio del codice: {Status} {Payload}",
                (int)response.StatusCode, payload);
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("id_token", out JsonElement idToken)
            ? idToken.GetString()
            : null;
    }

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
