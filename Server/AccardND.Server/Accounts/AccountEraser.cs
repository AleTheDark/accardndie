using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Accounts;

/// <summary>
/// Cancellazione definitiva di un account e di tutto cio' che vi e' collegato.
///
/// Sta qui e non nel pannello admin perche' i chiamanti sono due: l'admin che
/// ripulisce un account di prova e la pagina pubblica /account/delete, che
/// Google Play pretende dalle app con registrazione. La lista delle tabelle
/// deve restare in un posto solo: due copie divergono al primo giro di schema
/// e lasciano righe orfane che fanno fallire la DELETE finale su accounts.
/// </summary>
public sealed class AccountEraser
{
    private readonly AccardDatabase database;

    public AccountEraser(AccardDatabase database) => this.database = database;

    public (bool ok, string error) DeletePlayer(string playerId)
    {
        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        // Ordine: prima le tabelle figlie, poi accounts. match_history/campaign_runs/
        // login_events sono storici e vengono rimossi con l'account.
        // Le connessioni girano con foreign_keys=ON: se anche una sola tabella figlia con
        // FK verso accounts resta piena, l'ultima DELETE fallisce e l'account non si
        // cancella. Quando si aggiunge una tabella per-giocatore va aggiunta anche qui.
        string[] byPlayer =
        {
            "DELETE FROM single_player_unlocks WHERE player_id = $id",
            "DELETE FROM single_player_reward_claims WHERE player_id = $id",
            "DELETE FROM single_player_progress WHERE player_id = $id",
            "DELETE FROM player_counters WHERE player_id = $id",
            "DELETE FROM player_consumables WHERE player_id = $id",
            "DELETE FROM player_bag WHERE player_id = $id",
            "DELETE FROM player_tavern_quests WHERE player_id = $id",
            "DELETE FROM player_tavern_bonus WHERE player_id = $id",
            "DELETE FROM campaign_runs WHERE player_id = $id",
            "DELETE FROM login_events WHERE player_id = $id",
            "DELETE FROM player_stats WHERE player_id = $id",
            "DELETE FROM ranked_state WHERE player_id = $id",
            "DELETE FROM player_achievements WHERE player_id = $id",
            "DELETE FROM player_icons WHERE player_id = $id",
            "DELETE FROM campaign_kills WHERE player_id = $id",
            "DELETE FROM profiles WHERE player_id = $id",
            "DELETE FROM account_nicknames WHERE player_id = $id",
            "DELETE FROM external_identities WHERE player_id = $id",
            "DELETE FROM friends WHERE owner_id = $id OR other_id = $id",
            "DELETE FROM match_history WHERE player_a = $id OR player_b = $id",
            "DELETE FROM hall_of_fame WHERE player_id = $id",
            "DELETE FROM accounts WHERE player_id = $id"
        };

        using SqliteTransaction transaction = connection.BeginTransaction();
        foreach (string sql in byPlayer)
            Execute(connection, transaction, sql, ("$id", playerId));
        transaction.Commit();
        return (true, null);
    }

    private static bool PlayerExists(SqliteConnection connection, string playerId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM accounts WHERE player_id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", playerId);
        return command.ExecuteScalar() != null;
    }

    private static void Execute(
        SqliteConnection connection, SqliteTransaction transaction,
        string sql, params (string, object)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
}
