using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace AccardND.Server.Accounts;

/// <summary>
/// Legge la mail dall'ID token Google, verificandone la firma contro le chiavi
/// pubbliche di Google.
///
/// Serve solo a mostrare nel pannello admin con quale account Google e' entrato
/// un giocatore: il login vero resta quello di Unity Authentication. La mail si
/// verifica comunque invece di fidarsi di quello che dichiara il client, perche'
/// una mail sbagliata in pannello porterebbe a fondere o cancellare l'account
/// sbagliato.
///
/// Il token arriva solo dai login Google interattivi: sui resume di sessione non
/// esiste, quindi la mail resta quella gia' salvata.
/// </summary>
public sealed class GoogleIdTokenReader
{
    private const string JwksUrl = "https://www.googleapis.com/oauth2/v3/certs";

    private static readonly string[] ValidIssuers =
    {
        "https://accounts.google.com", "accounts.google.com"
    };

    private readonly ServerConfig config;
    private readonly ILogger<GoogleIdTokenReader> logger;
    private readonly HttpClient httpClient = new();
    private readonly SemaphoreSlim keysLock = new(1, 1);
    private IList<SecurityKey> cachedKeys;
    private DateTime keysExpireAt = DateTime.MinValue;

    public GoogleIdTokenReader(ServerConfig config, ILogger<GoogleIdTokenReader> logger)
    {
        this.config = config;
        this.logger = logger;
    }

    /// <summary>Mail verificata, oppure null se il token manca, non e' valido o
    /// Google non garantisce la mail.</summary>
    public async Task<string> TryReadVerifiedEmailAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(config.GoogleOAuth.ClientId))
            return null;

        IList<SecurityKey> keys;
        try
        {
            keys = await GetSigningKeysAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "JWKS di Google non disponibile: mail non registrata.");
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuers = ValidIssuers,
            ValidateIssuer = true,
            IssuerSigningKeys = keys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateAudience = true,
            ValidAudience = config.GoogleOAuth.ClientId
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            System.Security.Claims.ClaimsPrincipal principal =
                handler.ValidateToken(idToken, parameters, out _);

            // Una mail non verificata da Google non e' un identificativo: meglio
            // niente che un dato su cui poi si prendono decisioni sugli account.
            if (!string.Equals(principal.FindFirst("email_verified")?.Value, "true",
                    StringComparison.OrdinalIgnoreCase))
                return null;

            string email = principal.FindFirst("email")?.Value?.Trim();
            return string.IsNullOrEmpty(email) || email.Length > 254 ? null : email;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            logger.LogInformation("ID token Google rifiutato: {Reason}", exception.Message);
            return null;
        }
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

            string json = await httpClient.GetStringAsync(JwksUrl);
            cachedKeys = new JsonWebKeySet(json).GetSigningKeys();
            keysExpireAt = DateTime.UtcNow.AddHours(6);
            return cachedKeys;
        }
        finally
        {
            keysLock.Release();
        }
    }
}
