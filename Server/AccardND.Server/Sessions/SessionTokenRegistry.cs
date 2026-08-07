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

    /// <summary>
    /// Pietre tombali dei token revocati perché l'account è entrato altrove.
    /// Togliere il token non basta: il client sloggato può non aver mai letto
    /// l'avviso (app Android in pausa, scheda del browser in secondo piano: lì il
    /// loop di gioco è fermo e i messaggi restano in coda) e al risveglio prova a
    /// riagganciarsi. Trovando un token "sconosciuto" ripiegherebbe sul login
    /// Google, rientrerebbe come sessione nuova e sbatterebbe fuori il dispositivo
    /// che sta giocando adesso — che a sua volta rifarebbe lo stesso, all'infinito.
    /// Ricordandoci la revoca possiamo invece rispondergli "sei stato sostituito",
    /// e quello è un rifiuto su cui si ferma.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> superseded = new(StringComparer.Ordinal);

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

    /// <summary>
    /// Butta via un token: serve quando una sessione viene chiusa perché l'account
    /// è entrato altrove. Senza, il client sloggato potrebbe rientrare da solo con
    /// la riconnessione automatica e sbattere fuori a sua volta il dispositivo
    /// nuovo, in un rimpallo senza fine.
    /// </summary>
    public void Revoke(string token)
    {
        if (string.IsNullOrEmpty(token))
            return;
        entries.TryRemove(token, out _);
        // Il ricordo dura quanto sarebbe durato il token: oltre, un riaggancio
        // sarebbe comunque scaduto e "rifai l'accesso" è la risposta giusta.
        superseded[token] = DateTime.UtcNow.Add(Lifetime);
    }

    /// <summary>
    /// true se questo token era valido ed è stato revocato perché l'account è
    /// entrato da un altro dispositivo. Un token mai emesso o semplicemente
    /// scaduto non conta: quello merita un login nuovo, non l'avviso di
    /// sloggatura.
    /// </summary>
    public bool WasSuperseded(string token)
    {
        if (string.IsNullOrEmpty(token) || !superseded.TryGetValue(token, out DateTime rememberUntil))
            return false;
        if (rememberUntil > DateTime.UtcNow)
            return true;

        superseded.TryRemove(token, out _);
        return false;
    }

    private void Prune()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            if (pair.Value.ExpiresAtUtc <= now)
                entries.TryRemove(pair.Key, out _);
        }

        foreach (KeyValuePair<string, DateTime> pair in superseded)
        {
            if (pair.Value <= now)
                superseded.TryRemove(pair.Key, out _);
        }
    }
}
