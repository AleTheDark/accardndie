using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Accounts;

/// <summary>Un account raggiunto partendo da un'email Google verificata.</summary>
public sealed record LinkedAccount(string PlayerId, string Nickname, string CreatedAt, string LastLoginAt);

/// <summary>
/// "Quali account appartengono a chi ha appena provato di possedere questa email?"
///
/// La domanda se la fanno tutte le pagine pubbliche che partono da un accesso Google
/// e non da una sessione di gioco: la cancellazione account e la pagina delle
/// statistiche. Sta qui, da sola, perche' e' una domanda delicata - la risposta
/// decide di quali dati si sta per parlare - e averne due copie leggermente diverse
/// sarebbe il modo classico per farne divergere una.
/// </summary>
public static class LinkedAccounts
{
    /// <summary>
    /// L'email DEVE essere gia' verificata da Google. Chiamare questo metodo con
    /// un'email scritta a mano significa aprire i dati di chiunque la sappia digitare.
    /// </summary>
    public static IReadOnlyList<LinkedAccount> FindByVerifiedEmail(AccardDatabase database, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Array.Empty<LinkedAccount>();

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

        var found = new List<LinkedAccount>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            found.Add(new LinkedAccount(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return found;
    }
}
