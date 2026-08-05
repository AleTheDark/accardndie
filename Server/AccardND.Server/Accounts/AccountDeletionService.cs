using System.Collections.Concurrent;
using System.Security.Cryptography;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Accounts;

/// <summary>Un account trovato per l'email verificata, come lo vede l'utente prima di confermare.</summary>
public sealed record DeletableAccount(string PlayerId, string Nickname, string CreatedAt, string LastLoginAt);

/// <summary>
/// Esito della fase di riconoscimento. Ticket e' il permesso monouso da spendere
/// nella conferma: senza, la POST di cancellazione non vale nulla.
/// </summary>
public sealed record PreparedDeletion(
    string Ticket, string Email, IReadOnlyList<DeletableAccount> Accounts, string Error);

/// <summary>
/// Cancellazione account richiesta dal diretto interessato tramite la pagina
/// pubblica /account/delete.
///
/// Due tempi di proposito: prima si riconosce chi sta chiedendo (login Google,
/// stessa identita' con cui gioca) e gli si mostra cosa sta per sparire, poi si
/// cancella solo su conferma esplicita. Il ticket che lega le due fasi vive in
/// memoria, e' monouso e scade: un link di conferma non deve poter essere
/// rigiocato ne' indovinato.
/// </summary>
public sealed class AccountDeletionService
{
    private const int MaxPendingTickets = 200;
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(15);

    private sealed record Ticket(string Email, string[] PlayerIds, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Ticket> tickets = new(StringComparer.Ordinal);
    private readonly AccardDatabase database;
    private readonly GoogleIdTokenReader googleIdTokens;
    private readonly AccountEraser eraser;
    private readonly ILogger<AccountDeletionService> logger;

    public AccountDeletionService(
        AccardDatabase database,
        GoogleIdTokenReader googleIdTokens,
        AccountEraser eraser,
        ILogger<AccountDeletionService> logger)
    {
        this.database = database;
        this.googleIdTokens = googleIdTokens;
        this.eraser = eraser;
        this.logger = logger;
    }

    /// <summary>
    /// Verifica l'ID token, cerca gli account legati a quell'email ed emette il
    /// ticket di conferma. Un'email senza account non e' un errore: la pagina lo
    /// dice e finisce li'.
    /// </summary>
    public async Task<PreparedDeletion> PrepareAsync(string idToken)
    {
        string email = await googleIdTokens.TryReadVerifiedEmailAsync(idToken);
        if (string.IsNullOrEmpty(email))
            return new PreparedDeletion(null, null, Array.Empty<DeletableAccount>(),
                "Non e' stato possibile verificare il tuo account Google. Riprova.");

        return PrepareForVerifiedEmail(email);
    }

    /// <summary>
    /// Il seguito di <see cref="PrepareAsync"/> una volta che l'email e' provata.
    /// Separato perche' e' la parte che si puo' verificare senza chiamare Google.
    /// Chiamarlo con un'email non verificata significherebbe cancellare l'account
    /// di chiunque la si sappia scrivere: l'unico chiamante legittimo e' sopra.
    /// </summary>
    public PreparedDeletion PrepareForVerifiedEmail(string email)
    {
        IReadOnlyList<DeletableAccount> accounts = FindAccounts(email);
        if (accounts.Count == 0)
            return new PreparedDeletion(null, email, accounts, null);

        Prune();
        if (tickets.Count >= MaxPendingTickets)
        {
            logger.LogWarning("Troppe richieste di cancellazione in attesa: {Count}.", tickets.Count);
            return new PreparedDeletion(null, email, accounts,
                "Troppe richieste in corso, riprova tra qualche minuto.");
        }

        string ticket = CreateUrlSafeRandom(32);
        tickets[ticket] = new Ticket(
            email,
            accounts.Select(account => account.PlayerId).ToArray(),
            DateTime.UtcNow.Add(TicketLifetime));

        return new PreparedDeletion(ticket, email, accounts, null);
    }

    /// <summary>Spende il ticket e cancella. Il ticket sparisce comunque: niente secondi tentativi.</summary>
    public (bool ok, string error, int deleted) Confirm(string ticket)
    {
        Prune();

        if (string.IsNullOrEmpty(ticket) || !tickets.TryRemove(ticket, out Ticket pending))
            return (false, "Richiesta scaduta o gia' completata. Ricomincia dall'accesso con Google.", 0);

        if (pending.ExpiresAt <= DateTime.UtcNow)
            return (false, "Richiesta scaduta. Ricomincia dall'accesso con Google.", 0);

        int deleted = 0;
        foreach (string playerId in pending.PlayerIds)
        {
            (bool ok, string error) = eraser.DeletePlayer(playerId);
            if (ok)
            {
                deleted++;
                continue;
            }

            // Un account gia' sparito nel frattempo non e' un fallimento: lo e'
            // qualsiasi altro errore, perche' vorrebbe dire dati rimasti indietro.
            if (error == "Giocatore inesistente.")
                continue;

            logger.LogError("Cancellazione account {PlayerId} fallita: {Error}", playerId, error);
            return (false, "Cancellazione non riuscita. Riprova piu' tardi.", deleted);
        }

        logger.LogInformation(
            "Cancellazione account su richiesta dell'utente: {Deleted} account rimossi.", deleted);
        return (true, null, deleted);
    }

    private IReadOnlyList<DeletableAccount> FindAccounts(string email)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        // L'email sta su external_identities perche' e' un dato del provider, non
        // dell'account: e' la sola colonna che lega un login Google a un player_id.
        command.CommandText = @"
            SELECT DISTINCT identities.player_id,
                   COALESCE(nicknames.nickname, account.username),
                   account.created_at,
                   account.last_login_at
            FROM external_identities identities
            JOIN accounts account ON account.player_id = identities.player_id
            LEFT JOIN account_nicknames nicknames ON nicknames.player_id = identities.player_id
            WHERE identities.email IS NOT NULL
              AND lower(identities.email) = lower($email)";
        command.Parameters.AddWithValue("$email", email);

        var found = new List<DeletableAccount>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            found.Add(new DeletableAccount(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return found;
    }

    private void Prune()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, Ticket> entry in tickets)
        {
            if (entry.Value.ExpiresAt <= now)
                tickets.TryRemove(entry.Key, out _);
        }
    }

    private static string CreateUrlSafeRandom(int byteCount) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
