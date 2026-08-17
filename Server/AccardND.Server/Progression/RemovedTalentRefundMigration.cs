using AccardND.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AccardND.Server.Progression;

/// <summary>
/// Restituisce i propoli spesi sui nodi tolti dal catalogo.
///
/// Quando un talento sparisce, la sua riga in <c>player_talents</c> smette di contare -
/// <c>TalentService.ReadRanks</c> scarta gli id che il catalogo non conosce - ma i punti
/// spesi per comprarlo restano spesi. Chi aveva investito in un nodo che abbiamo poi
/// cambiato idea di tenere si ritroverebbe con meno propoli di chi non l'aveva comprato:
/// e' una penale per aver giocato prima, e non e' una cosa che si puo' chiedere a un
/// giocatore di accettare.
///
/// Il rimborso va su <c>talent_points</c> e non su <c>talent_points_earned</c>: quei punti
/// erano gia' stati guadagnati, qui tornano solo disponibili. Alzare anche il totale
/// guadagnato li conterebbe due volte.
/// </summary>
public static class RemovedTalentRefundMigration
{
    /// <summary>
    /// I nodi ritirati e quanto costava un loro rango. I costi vivono qui e non nel catalogo
    /// perche' nel catalogo questi nodi non ci sono piu': e' il prezzo di listino al momento
    /// del ritiro, ed e' quello che va restituito.
    ///
    /// Chi ritira un nodo in futuro aggiunge una riga qui e cambia la chiave di migrazione
    /// piu' sotto, altrimenti il giro nuovo non parte.
    /// </summary>
    private static readonly (string TalentId, int CostPerRank)[] RetiredTalents =
    {
        // Ritirati riscrivendo il ramo Maestria: quattro sconti sulle soglie di livello
        // accorciavano la run invece di cambiarla.
        ("mastery-momentum", 4),
        ("mastery-veteran", 4),
        ("mastery-summit", 8),

        // "Colpo d'anticipo": vinceva le parita' d'iniziativa, che non esistono perche' i
        // tiri sono estratti unici. Comprabile e inerte.
        ("initiative-first-strike", 8),

        // "Forgia generosa": dava essenze da spendere nella forgia, che non ha piu' una
        // valuta da quando il mazzo di partenza si compone scegliendo campione e vice.
        ("purse-generous-forge", 2)
    };

    // Cambiata da -1 a -2 nel ritirare "Forgia generosa": senza cambiarla, sui server dove
    // il primo giro era gia' stato fatto la migrazione si considererebbe conclusa e i
    // propoli di quel nodo non tornerebbero a nessuno. I nodi del primo giro non vengono
    // rimborsati due volte: le loro righe in player_talents sono gia' state cancellate.
    private const string SettingKey = "migration.talents.refund-removed-2";

    /// <summary>Righe rimborsate. Zero se la migrazione era gia' stata fatta.</summary>
    public static int RunIfNeeded(AccardDatabase database, ILogger logger = null)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        if (IsAlreadyDone(connection, transaction))
            return 0;

        int refunded = Refund(connection, transaction);
        MarkDone(connection, transaction);
        transaction.Commit();

        if (refunded > 0)
            logger?.LogInformation("Propoli restituiti per {Rows} acquisti su nodi ritirati.", refunded);
        return refunded;
    }

    /// <summary>
    /// Accredita e poi cancella, dentro la stessa transazione. La cancellazione non e' un
    /// dettaglio di pulizia: e' quello che rende il rimborso non ripetibile anche se qualcuno
    /// rimettesse a mano la chiave in <c>server_settings</c>, perche' al secondo giro non c'e'
    /// piu' niente da rimborsare.
    /// </summary>
    private static int Refund(SqliteConnection connection, SqliteTransaction transaction)
    {
        int refundedRows = 0;
        string now = DateTime.UtcNow.ToString("O");

        foreach ((string talentId, int costPerRank) in RetiredTalents)
        {
            using (SqliteCommand credit = connection.CreateCommand())
            {
                credit.Transaction = transaction;
                credit.CommandText = @"
                    UPDATE single_player_progress
                    SET talent_points = talent_points + (
                            SELECT rank * $cost FROM player_talents
                            WHERE player_talents.player_id = single_player_progress.player_id
                              AND player_talents.talent_id = $talent),
                        updated_at = $now
                    WHERE player_id IN (
                        SELECT player_id FROM player_talents
                        WHERE talent_id = $talent AND rank > 0)";
                credit.Parameters.AddWithValue("$cost", costPerRank);
                credit.Parameters.AddWithValue("$talent", talentId);
                credit.Parameters.AddWithValue("$now", now);
                refundedRows += credit.ExecuteNonQuery();
            }

            using SqliteCommand drop = connection.CreateCommand();
            drop.Transaction = transaction;
            drop.CommandText = "DELETE FROM player_talents WHERE talent_id = $talent";
            drop.Parameters.AddWithValue("$talent", talentId);
            drop.ExecuteNonQuery();
        }

        return refundedRows;
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
