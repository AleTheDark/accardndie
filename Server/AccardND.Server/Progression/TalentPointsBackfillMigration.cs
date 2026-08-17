using AccardND.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AccardND.Server.Progression;

/// <summary>
/// Consegna a chi ha gia' un livello account i punti talento che quei livelli varranno da
/// adesso in poi. Serve una volta sola, all'introduzione dell'albero.
///
/// Senza, un giocatore di livello 40 aprirebbe l'albero e lo troverebbe tutto grigio con
/// zero punti in mano: i suoi livelli sono stati pagati in miele quando il miele era la
/// ricompensa, e quel miele e' gia' stato speso. Chiedergli di rifare da capo quaranta
/// livelli per toccare la funzione nuova sarebbe punirlo di aver giocato prima.
///
/// Non tocca il livello ne' l'esperienza: la curva nuova vale da qui in avanti, e
/// ricalcolare all'indietro degraderebbe account esistenti, che e' il modo peggiore di
/// introdurre una progressione.
/// </summary>
public static class TalentPointsBackfillMigration
{
    private const string SettingKey = "migration.talents.backfill-1";

    /// <summary>Righe accreditate. Zero se la migrazione era gia' stata fatta.</summary>
    public static int RunIfNeeded(AccardDatabase database, ILogger logger = null)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        if (IsAlreadyDone(connection, transaction))
            return 0;

        int credited = Backfill(connection, transaction);
        MarkDone(connection, transaction);
        transaction.Commit();

        if (credited > 0)
            logger?.LogInformation("Punti talento retroattivi accreditati a {Rows} account.", credited);
        return credited;
    }

    /// <summary>
    /// Un punto per livello oltre il primo, piu' due per ogni livello tondo attraversato:
    /// la forma chiusa di <c>SinglePlayerProgressService.TalentPointsForLevels(1, livello)</c>,
    /// scritta in SQL per non dover leggere e riscrivere una riga per volta.
    ///
    /// La guardia su <c>talent_points_earned = 0</c> e' la seconda rete oltre alla chiave in
    /// <c>server_settings</c>: se qualcuno rimettesse la chiave a mano, chi ha gia' guadagnato
    /// punti non verrebbe pagato due volte.
    /// </summary>
    private static int Backfill(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET talent_points = talent_points
                    + (account_level - 1) + 2 * (account_level / 5),
                talent_points_earned = talent_points_earned
                    + (account_level - 1) + 2 * (account_level / 5),
                updated_at = $now
            WHERE account_level > 1 AND talent_points_earned = 0";
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        return update.ExecuteNonQuery();
    }

    private static bool IsAlreadyDone(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT 1 FROM server_settings WHERE key = $key LIMIT 1";
        query.Parameters.AddWithValue("$key", SettingKey);
        return query.ExecuteScalar() != null;
    }

    private static void MarkDone(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT INTO server_settings (key, value, updated_at) VALUES ($key, $value, $now)
            ON CONFLICT(key) DO UPDATE SET value = $value, updated_at = $now";
        insert.Parameters.AddWithValue("$key", SettingKey);
        insert.Parameters.AddWithValue("$value", "done");
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }
}
