using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Rinumerazione una tantum dei capitoli dopo il restyling della campagna.
///
/// Gli id dei capitoli (<c>chapter-1</c>...) sono posizioni, non contenuti: nel DB c'e' solo
/// la stringa, e il restyling ha cambiato che cosa quella stringa indica. Senza questa
/// migrazione, chi aveva battuto Medusa si ritroverebbe l'Infestazione gia' completata e la
/// Luce gia' aperta - due capitoli mai giocati e ormai impossibili da giocare, perche' un
/// capitolo completato non si ripete.
///
/// La rimappatura vale su <c>chapter</c> (accesso) e <c>chapterCleared</c> (completato)
/// insieme: sono due facce dello stesso capitolo e separarle darebbe uno stato incoerente.
/// </summary>
public static class ChapterRemapMigration
{
    /// <summary>
    /// Chiave in <c>server_settings</c> che segna la migrazione fatta. Il nome porta la
    /// versione: una futura rinumerazione sara' un'altra chiave, non questa riusata.
    /// </summary>
    private const string SettingKey = "migration.chapters.restyling-7";

    /// <summary>
    /// Vecchia posizione -> nuova. Il primo e il secondo capitolo si scambiano (la campagna
    /// ora apre con Trentor), gli Specchi scendono al sesto e la Cosmica chiude al settimo.
    /// I capitoli nuovi (3, 4, 5) non compaiono: nessuno li ha mai giocati.
    /// </summary>
    private static readonly (string From, string To)[] Remap =
    {
        ("chapter-1", "chapter-2"),
        ("chapter-2", "chapter-1"),
        ("chapter-3", "chapter-6"),
        ("chapter-4", "chapter-7")
    };

    private static readonly string[] ChapterUnlockTypes = { "chapter", "chapterCleared" };

    /// <summary>
    /// Esegue la migrazione se non e' gia' stata fatta. Ritorna il numero di giocatori
    /// toccati (0 anche quando era gia' stata eseguita).
    /// </summary>
    public static int RunIfNeeded(AccardDatabase database, ILogger logger = null)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        if (IsAlreadyDone(connection, transaction))
            return 0;

        int touched = Remap_(connection, transaction);
        touched += Backfill(connection, transaction);
        int classes = GrantRewardClasses(connection, transaction);

        MarkDone(connection, transaction);
        transaction.Commit();

        logger?.LogInformation(
            "Migrazione capitoli: {Rows} righe rimappate/aggiunte, {Classes} classi premio concesse.",
            touched, classes);
        return touched;
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
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at";
        insert.Parameters.AddWithValue("$key", SettingKey);
        insert.Parameters.AddWithValue("$value", "done");
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    /// <summary>
    /// Sposta gli id alla nuova posizione. Passa da un prefisso temporaneo perche' il primo e
    /// il secondo capitolo si scambiano: un UPDATE diretto sbatterebbe contro la chiave
    /// primaria (player_id, unlock_type, unlock_id) a meta' strada.
    /// </summary>
    private static int Remap_(SqliteConnection connection, SqliteTransaction transaction)
    {
        const string stagingPrefix = "migrating:";
        int touched = 0;

        foreach ((string from, string to) in Remap)
            touched += Rename(connection, transaction, from, stagingPrefix + to);

        foreach ((_, string to) in Remap)
            Rename(connection, transaction, stagingPrefix + to, to);

        return touched;
    }

    private static int Rename(
        SqliteConnection connection, SqliteTransaction transaction, string fromId, string toId)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_unlocks SET unlock_id = $to
            WHERE unlock_id = $from AND unlock_type IN ('chapter', 'chapterCleared')";
        update.Parameters.AddWithValue("$to", toId);
        update.Parameters.AddWithValue("$from", fromId);
        return update.ExecuteNonQuery();
    }

    /// <summary>
    /// Riempie i buchi lasciati dalla rinumerazione: chi ha accesso al capitolo N deve avere
    /// accesso anche a tutti quelli prima. Senza, un giocatore rimappato al sesto capitolo si
    /// troverebbe i capitoli 2-5 chiusi sotto un capitolo aperto, cioe' una campagna che
    /// comincia dal fondo.
    ///
    /// Riguarda solo l'accesso: i completamenti restano quelli veri, perche' un capitolo
    /// segnato completato senza averlo giocato e' contenuto perso per sempre.
    /// </summary>
    private static int Backfill(SqliteConnection connection, SqliteTransaction transaction)
    {
        var highestByPlayer = new Dictionary<string, int>(StringComparer.Ordinal);
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText =
                "SELECT player_id, unlock_id FROM single_player_unlocks WHERE unlock_type = 'chapter'";
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
            {
                string playerId = reader.GetString(0);
                if (!ChapterCatalog.TryGetById(reader.GetString(1), out ChapterCatalog.Chapter chapter))
                    continue;
                if (!highestByPlayer.TryGetValue(playerId, out int highest) || chapter.Number > highest)
                    highestByPlayer[playerId] = chapter.Number;
            }
        }

        int added = 0;
        string now = DateTime.UtcNow.ToString("O");
        foreach ((string playerId, int highest) in highestByPlayer)
        {
            foreach (ChapterCatalog.Chapter chapter in ChapterCatalog.All)
            {
                if (chapter.Number > highest)
                    break;
                added += Grant(connection, transaction, playerId, "chapter", chapter.Id, now);
            }
        }
        return added;
    }

    /// <summary>
    /// Consegna le classi premio dei capitoli gia' completati. Solo di quelli davvero
    /// completati: il backfill dell'accesso non conta, altrimenti chi ha battuto un boss si
    /// ritroverebbe in mano sei classi mai guadagnate e l'altare del Santuario vuoto.
    /// </summary>
    private static int GrantRewardClasses(SqliteConnection connection, SqliteTransaction transaction)
    {
        var cleared = new List<(string PlayerId, string ChapterId)>();
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText =
                "SELECT player_id, unlock_id FROM single_player_unlocks WHERE unlock_type = 'chapterCleared'";
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
                cleared.Add((reader.GetString(0), reader.GetString(1)));
        }

        int granted = 0;
        string now = DateTime.UtcNow.ToString("O");
        foreach ((string playerId, string chapterId) in cleared)
        {
            if (!ChapterCatalog.TryGetById(chapterId, out ChapterCatalog.Chapter chapter) ||
                string.IsNullOrEmpty(chapter.RewardClassId))
                continue;
            granted += Grant(connection, transaction, playerId, "class", chapter.RewardClassId, now);
        }
        return granted;
    }

    private static int Grant(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string type, string id, string now)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT OR IGNORE INTO single_player_unlocks (player_id, unlock_type, unlock_id, unlocked_at)
            VALUES ($player, $type, $id, $now)";
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$type", type);
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$now", now);
        return insert.ExecuteNonQuery();
    }
}
