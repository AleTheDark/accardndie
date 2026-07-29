using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace AccardND.Server.Accounts;

public sealed record VerifiedExternalIdentity(
    string Provider, string ExternalId, string DisplayName, string AuthMethod, string Email);

/// <summary>
/// Valida gli access token di Unity Authentication contro le chiavi pubbliche
/// (JWKS) di Unity. Per i test la sorgente JWKS può essere un file locale.
/// </summary>
public sealed class UgsAuthService
{
    private readonly ServerConfig config;
    private readonly GoogleIdTokenReader googleIdTokens;
    private readonly ILogger<UgsAuthService> logger;
    private readonly HttpClient httpClient = new();
    private readonly SemaphoreSlim keysLock = new(1, 1);
    private IList<SecurityKey> cachedKeys;
    private DateTime keysExpireAt = DateTime.MinValue;

    public UgsAuthService(
        ServerConfig config, GoogleIdTokenReader googleIdTokens, ILogger<UgsAuthService> logger)
    {
        this.config = config;
        this.googleIdTokens = googleIdTokens;
        this.logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(config.UgsProjectId);

    /// <summary>Metodi di accesso noti; il client ne dichiara uno, il token ha
    /// la precedenza se porta il claim del provider.</summary>
    public const string AuthMethodUnknown = "unknown";

    private static readonly string[] ProviderClaimNames =
    {
        "sign_in_provider", "identity_provider", "idp", "provider", "id_provider"
    };

    private bool loggedClaimNames;

    /// <param name="googleIdToken">ID token Google del login appena fatto, se c'e':
    /// serve solo a registrare la mail dell'account. Sui resume di sessione manca.</param>
    public async Task<(VerifiedExternalIdentity Identity, string Error)> ValidateAsync(
        string accessToken, string displayName, string declaredAuthMethod, string googleIdToken = null)
    {
        if (!IsEnabled)
            return (null, "Unity Authentication non configurato sul server.");
        if (string.IsNullOrWhiteSpace(accessToken))
            return (null, "Token mancante.");

        IList<SecurityKey> keys;
        try
        {
            keys = await GetSigningKeysAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Impossibile caricare il JWKS da {Source}", config.UgsJwksSource);
            return (null, "Chiavi di firma non disponibili, riprova.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = config.UgsIssuer,
            ValidateIssuer = true,
            IssuerSigningKeys = keys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateAudience = true,
            // Unity mette il project id tra le audience: basta che una lo contenga.
            AudienceValidator = (audiences, _, _) =>
                audiences != null && audiences.Any(a =>
                    a != null && a.Contains(config.UgsProjectId, StringComparison.OrdinalIgnoreCase))
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            System.Security.Claims.ClaimsPrincipal principal =
                handler.ValidateToken(accessToken, parameters, out _);
            string playerId = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(playerId))
                return (null, "Token senza identità giocatore.");

            LogClaimNamesOnce(principal);

            string name = SanitizeName(displayName, playerId);
            string method = ResolveAuthMethod(principal, declaredAuthMethod);
            string email = await googleIdTokens.TryReadVerifiedEmailAsync(googleIdToken);
            return (new VerifiedExternalIdentity("ugs", playerId, name, method, email), null);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            logger.LogInformation("Token UGS rifiutato: {Reason}", exception.Message);
            return (null, "Token non valido o scaduto.");
        }
    }

    /// <summary>
    /// Come si e' autenticato il giocatore dietro al token UGS. Se Unity mette il
    /// provider tra i claim quello vince (e' firmato); altrimenti si usa il valore
    /// dichiarato dal client, che serve solo a etichettare gli accessi nel pannello
    /// admin e non decide nulla di sensibile.
    /// </summary>
    private static string ResolveAuthMethod(
        System.Security.Claims.ClaimsPrincipal principal, string declared)
    {
        foreach (string claimName in ProviderClaimNames)
        {
            string value = principal.FindFirst(claimName)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return NormalizeAuthMethod(value);
        }

        return NormalizeAuthMethod(declared);
    }

    private static string NormalizeAuthMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return AuthMethodUnknown;

        string normalized = new string(method.Trim().ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character)
                || character is '-' or '.' or '_')
            .ToArray());
        if (normalized.Length == 0)
            return AuthMethodUnknown;
        return normalized.Length > 32 ? normalized[..32] : normalized;
    }

    /// <summary>
    /// Traccia una volta sola i claim del token: serve a scoprire se UGS espone
    /// gia' il provider di accesso, cosi' da poterlo leggere dal token firmato
    /// invece che dal valore dichiarato dal client.
    /// </summary>
    private void LogClaimNamesOnce(System.Security.Claims.ClaimsPrincipal principal)
    {
        if (loggedClaimNames)
            return;
        loggedClaimNames = true;
        logger.LogInformation(
            "Claim presenti nel token UGS: {Claims}",
            string.Join(", ", principal.Claims.Select(claim => claim.Type).Distinct()));
    }

    private static string SanitizeName(string displayName, string playerId)
    {
        string name = (displayName ?? string.Empty).Trim();
        if (name.Length > 20)
            name = name[..20];
        if (name.Length < 3)
            name = $"player-{playerId[..Math.Min(6, playerId.Length)]}";
        return name;
    }

    private async Task<IList<SecurityKey>> GetSigningKeysAsync()
    {
        if (cachedKeys != null && DateTime.UtcNow < keysExpireAt)
            return cachedKeys;

        await keysLock.WaitAsync();
        try
        {
            if (cachedKeys != null && DateTime.UtcNow < keysExpireAt)
                return cachedKeys;

            string json = config.UgsJwksSource.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? await httpClient.GetStringAsync(config.UgsJwksSource)
                : await File.ReadAllTextAsync(config.UgsJwksSource);
            cachedKeys = new JsonWebKeySet(json).GetSigningKeys();
            keysExpireAt = DateTime.UtcNow.AddHours(6);
            logger.LogInformation("JWKS caricato: {Count} chiavi di firma.", cachedKeys.Count);
            return cachedKeys;
        }
        finally
        {
            keysLock.Release();
        }
    }
}
