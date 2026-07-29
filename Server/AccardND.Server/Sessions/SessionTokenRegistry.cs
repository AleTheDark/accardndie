using System.Collections.Concurrent;
using System.Security.Cryptography;
using AccardND.Server.Accounts;

namespace AccardND.Server.Sessions;

/// <summary>
/// Token di sessione emessi al login e rigiocabili su auth.session per riagganciare
/// una connessione caduta. Serve a non ripassare da Google/UGS a ogni blip di rete:
/// quel giro è lento e fallisce proprio quando la rete è instabile.
///
/// Vivono solo in memoria: il riavvio del server chiude comunque tutti i socket, e
/// non tenere credenziali a lungo termine su disco è un rischio in meno. Il token è
/// un bearer, quindi è casuale a 256 bit e scade.
/// </summary>
public sealed class SessionTokenRegistry
{
    private sealed record Entry(AccountIdentity Identity, DateTime ExpiresAtUtc);

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /// <summary>Emette un token per l'identità appena autenticata.</summary>
    public string Issue(AccountIdentity identity)
    {
        if (identity == null)
            return null;

        Prune();
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        entries[token] = new Entry(identity, DateTime.UtcNow.Add(Lifetime));
        return token;
    }

    /// <summary>
    /// Risolve un token. Il rinnovo è scorrevole: finché il giocatore si riconnette
    /// il token resta valido, così una sessione lunga non decade a metà partita.
    /// </summary>
    public AccountIdentity Resolve(string token)
    {
        if (string.IsNullOrEmpty(token) || !entries.TryGetValue(token, out Entry entry))
            return null;
        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            entries.TryRemove(token, out _);
            return null;
        }

        entries[token] = entry with { ExpiresAtUtc = DateTime.UtcNow.Add(Lifetime) };
        return entry.Identity;
    }

    private void Prune()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            if (pair.Value.ExpiresAtUtc <= now)
                entries.TryRemove(pair.Key, out _);
        }
    }
}
