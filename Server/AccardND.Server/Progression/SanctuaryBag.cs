using AccardND.NetProtocol;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Scorta di consumabili e bisaccia. Due concetti distinti di proposito:
/// la scorta e' quanti pezzi possiedi, la bisaccia e' quali porti nella prossima run.
/// La bisaccia non trasferisce niente: e' una selezione. Un oggetto lascia la scorta solo
/// quando viene davvero usato, cosi' portarselo dietro senza usarlo non costa nulla.
///
/// La regola "un solo pezzo per tipo" non e' un controllo scritto a mano: e' la chiave
/// primaria di player_bag. Da li' discende quasi tutto il bilanciamento, perche' rende
/// impossibile accumulare tre copie dell'oggetto piu' forte.
/// </summary>
public static class SanctuaryBag
{
    public static int ReadSlots(SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        int purchased = 0;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText =
                "SELECT COUNT(*) FROM single_player_unlocks WHERE player_id = $player AND unlock_type = 'slot'";
            query.Parameters.AddWithValue("$player", playerId);
            purchased = Convert.ToInt32(query.ExecuteScalar() ?? 0);
        }
        return Math.Min(SanctuaryCatalog.MaxBagSlots, SanctuaryCatalog.BaseBagSlots + purchased);
    }

    public static SanctuaryStashData[] ReadStash(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        var stash = new List<SanctuaryStashData>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT item_id, count FROM player_consumables WHERE player_id = $player AND count > 0 ORDER BY item_id";
        query.Parameters.AddWithValue("$player", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            stash.Add(new SanctuaryStashData { itemId = reader.GetString(0), count = reader.GetInt32(1) });
        return stash.ToArray();
    }

    public static string[] ReadBag(SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        var bag = new List<string>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT item_id FROM player_bag WHERE player_id = $player ORDER BY item_id";
        query.Parameters.AddWithValue("$player", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            bag.Add(reader.GetString(0));
        return bag.ToArray();
    }

    public static void AddToStash(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string itemId, int amount)
    {
        if (amount <= 0)
            return;

        using SqliteCommand upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = @"
            INSERT INTO player_consumables (player_id, item_id, count)
            VALUES ($player, $item, $amount)
            ON CONFLICT(player_id, item_id) DO UPDATE SET count = player_consumables.count + $amount";
        upsert.Parameters.AddWithValue("$player", playerId);
        upsert.Parameters.AddWithValue("$item", itemId);
        upsert.Parameters.AddWithValue("$amount", amount);
        upsert.ExecuteNonQuery();
    }

    /// <summary>
    /// Scala dalla scorta i consumabili usati in una run. Un oggetto esaurito esce anche
    /// dalla bisaccia: lasciarcelo mostrerebbe uno slot pieno che alla run successiva parte
    /// vuoto senza spiegazioni.
    /// </summary>
    public static void ConsumeFromStash(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, IEnumerable<string> itemIds)
    {
        if (itemIds == null)
            return;

        foreach (string rawId in itemIds)
        {
            string itemId = Normalize(rawId);
            if (string.IsNullOrEmpty(itemId) || !SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeItem, itemId, out _))
                continue;

            using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = @"
                    UPDATE player_consumables SET count = MAX(0, count - 1)
                    WHERE player_id = $player AND item_id = $item";
                update.Parameters.AddWithValue("$player", playerId);
                update.Parameters.AddWithValue("$item", itemId);
                update.ExecuteNonQuery();
            }

            using SqliteCommand prune = connection.CreateCommand();
            prune.Transaction = transaction;
            prune.CommandText = @"
                DELETE FROM player_bag
                WHERE player_id = $player AND item_id = $item
                  AND NOT EXISTS (
                      SELECT 1 FROM player_consumables
                      WHERE player_id = $player AND item_id = $item AND count > 0)";
            prune.Parameters.AddWithValue("$player", playerId);
            prune.Parameters.AddWithValue("$item", itemId);
            prune.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Sostituisce l'intera bisaccia. Ritorna un messaggio d'errore se la selezione non e'
    /// valida, altrimenti null.
    /// </summary>
    public static string ReplaceBag(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        IEnumerable<string> requestedItemIds)
    {
        int slots = ReadSlots(connection, transaction, playerId);
        var owned = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SanctuaryStashData entry in ReadStash(connection, transaction, playerId))
            owned[entry.itemId] = entry.count;

        var selection = new List<string>();
        foreach (string rawId in requestedItemIds ?? Enumerable.Empty<string>())
        {
            string itemId = Normalize(rawId);
            if (string.IsNullOrEmpty(itemId))
                continue;
            if (!SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeItem, itemId, out _))
                return "Oggetto non valido.";
            if (selection.Contains(itemId))
                return "Un solo pezzo per tipo nella bisaccia.";
            if (!owned.TryGetValue(itemId, out int count) || count <= 0)
                return "Oggetto non posseduto.";
            selection.Add(itemId);
        }

        if (selection.Count > slots)
            return $"La bisaccia ha {slots} slot.";

        using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM player_bag WHERE player_id = $player";
            clear.Parameters.AddWithValue("$player", playerId);
            clear.ExecuteNonQuery();
        }

        foreach (string itemId in selection)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT OR IGNORE INTO player_bag (player_id, item_id) VALUES ($player, $item)";
            insert.Parameters.AddWithValue("$player", playerId);
            insert.Parameters.AddWithValue("$item", itemId);
            insert.ExecuteNonQuery();
        }
        return null;
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
