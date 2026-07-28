using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AccardND.Server.Admin;

/// <summary>
/// Autenticazione del pannello admin: verifica username/password (constant-time)
/// ed emette token bearer opachi tenuti in memoria con scadenza. I token non
/// sopravvivono al riavvio del processo: e voluto, l'admin si riautentica.
/// </summary>
public sealed class AdminAuth
{
    private readonly AdminConfig config;
    private readonly ConcurrentDictionary<string, DateTime> tokens = new();

    public AdminAuth(ServerConfig serverConfig)
    {
        config = serverConfig.Admin;
    }

    public bool IsEnabled => config.IsEnabled;

    /// <summary>Verifica le credenziali ed emette un token, oppure null se non valide.</summary>
    public string TryLogin(string username, string password)
    {
        if (!config.IsEnabled)
            return null;

        bool userOk = FixedTimeEquals(username ?? string.Empty, config.Username);
        bool passOk = FixedTimeEquals(password ?? string.Empty, config.ResolvePassword());
        // Valuta sempre entrambe per non rivelare quale campo e sbagliato dai tempi.
        if (!(userOk && passOk))
            return null;

        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        tokens[token] = DateTime.UtcNow.AddHours(Math.Max(1, config.SessionTtlHours));
        return token;
    }

    /// <summary>True se il token e valido e non scaduto. Ripulisce i token scaduti incontrati.</summary>
    public bool IsValid(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;
        if (!tokens.TryGetValue(token, out DateTime expiry))
            return false;
        if (expiry <= DateTime.UtcNow)
        {
            tokens.TryRemove(token, out _);
            return false;
        }
        return true;
    }

    public void Logout(string token)
    {
        if (!string.IsNullOrEmpty(token))
            tokens.TryRemove(token, out _);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        byte[] ba = Encoding.UTF8.GetBytes(a);
        byte[] bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(ba), SHA256.HashData(bb));
    }
}
