using AccardND.NetProtocol;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Contatori cumulativi di campagna. Sono la base su cui i requisiti del Santuario (e le
/// quest) vengono valutati: leggere un contatore costa una riga, ricavare lo stesso numero
/// da <c>campaign_runs</c> costerebbe una scansione dello storico.
///
/// Ogni contatore ha una sola via di scrittura, per non doversi chiedere quale evento
/// l'abbia incrementato: i boss di capitolo passano da ClearChapter (che scatta alla morte
/// del boss), tutto il resto dal sommario di fine run.
///
/// Le chiavi sono canoniche e non coincidono con gli id carta del client: <c>trentor</c>
/// non ha il prefisso <c>boss-</c> che hanno gli altri, e non vogliamo quella stranezza
/// dentro il catalogo dei requisiti.
/// </summary>
public static class CampaignCounters
{
    public const string EnemiesDefeated = "enemies_defeated";
    public const string RoomsCleared = "rooms_cleared";
    public const string RunsEnded = "runs_ended";

    /// <summary>Boss di capitolo sconfitti in totale, senza distinguere quale.</summary>
    public const string BossesDefeated = "bosses_defeated";

    /// <summary>
    /// Miniboss sconfitti in totale, senza distinguere la forma. I contatori per-miniboss
    /// (<c>miniboss_golem</c> e simili) restano quelli dei requisiti mirati del Santuario.
    /// </summary>
    public const string MinibossesDefeated = "minibosses_defeated";

    /// <summary>Dadi tirati: ogni faccia lanciata conta uno, anche nei tiri doppi.</summary>
    public const string DiceRolled = "dice_rolled";

    /// <summary>Abilita' di classe attivate dalle pedine del giocatore.</summary>
    public const string AbilitiesUsed = "abilities_used";

    /// <summary>Consumabili della bisaccia davvero usati in run.</summary>
    public const string ItemsUsed = "items_used";

    /// <summary>Esperienza guadagnata nelle run, al lordo di quella spesa dal mercante.</summary>
    public const string ExperienceEarned = "experience_earned";

    /// <summary>Giornate di taverna chiuse in pieno: e' la prova del Sacerdote.</summary>
    public const string DailyCompleted = "daily_completed";

    /// <summary>
    /// Partite PvP concluse. A differenza dei contatori di campagna questi non arrivano da
    /// un sommario del client: il PvP e' autoritativo sul server, quindi non hanno bisogno
    /// di cap e non sono falsificabili.
    /// </summary>
    public const string PvpMatches = "pvp_matches";

    /// <summary>Partite PvP vinte.</summary>
    public const string PvpWins = "pvp_wins";

    /// <summary>Round vinti in PvP: avanza anche in una partita persa.</summary>
    public const string PvpRoundsWon = "pvp_rounds_won";

    /// <summary>
    /// Aggiorna i contatori PvP di un giocatore a fine partita. Chi ha abbandonato non
    /// prende niente (nemmeno la partita giocata): senza questa condizione due account
    /// complici chiuderebbero le quest d'arena con una manciata di resa immediata, e da
    /// quando le quest sono l'unica fonte di miele quella sarebbe una zecca.
    /// L'avversario invece prende credito pieno: la sua partita l'ha giocata davvero.
    /// </summary>
    public static void RecordPvpMatch(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        bool won,
        bool forfeitedByThisPlayer,
        int roundsWon)
    {
        if (forfeitedByThisPlayer)
            return;

        Increment(connection, transaction, playerId, PvpMatches, 1);
        if (won)
            Increment(connection, transaction, playerId, PvpWins, 1);
        Increment(connection, transaction, playerId, PvpRoundsWon, Math.Max(0, roundsWon));
    }

    /// <summary>Boss di capitolo: id carta client -> chiave contatore.</summary>
    private static readonly Dictionary<string, string> BossCounterByCardId = new()
    {
        ["boss-bragus"] = "boss_bragus",
        ["trentor"] = "boss_trentor",
        ["boss-medusa"] = "boss_medusa",
        ["boss-palatir"] = "boss_palatir"
    };

    /// <summary>Miniboss: id carta client -> chiave contatore.</summary>
    private static readonly Dictionary<string, string> MinibossCounterByCardId = new()
    {
        ["miniboss-composable-golem"] = "miniboss_golem"
    };

    public static bool TryGetBossCounterKey(string bossCardId, out string counterKey) =>
        BossCounterByCardId.TryGetValue(Normalize(bossCardId), out counterKey);

    /// <summary>
    /// Tetto per run di ogni contatore. Da quando le quest della taverna sono l'unica fonte
    /// di miele, questi numeri sono moneta: il combattimento e' client-side, quindi un
    /// sommario gonfiato comprerebbe sblocchi. Il cap non toglie niente a chi gioca davvero
    /// (sono valori fuori portata per una run onesta) e limita il danno di un client
    /// manipolato a una giornata di quest.
    /// </summary>
    private static readonly Dictionary<string, int> PerRunCap = new()
    {
        [EnemiesDefeated] = 300,
        [RoomsCleared] = 100,
        [MinibossesDefeated] = 20,
        [DiceRolled] = 3000,
        [AbilitiesUsed] = 600,
        [ItemsUsed] = 20,
        [ExperienceEarned] = 5000
    };

    /// <summary>
    /// Aggrega il sommario di una run conclusa. Non tocca i contatori dei boss di capitolo:
    /// quelli li possiede ClearChapter. Gli id sconosciuti vengono ignorati, cosi' un client
    /// manipolato non puo' creare chiavi arbitrarie; la lista grezza resta comunque
    /// archiviata in <c>campaign_runs.defeated_boss_ids</c>.
    /// </summary>
    public static void RecordRunSummary(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        SinglePlayerDeathRewardRequest request)
    {
        if (request == null)
            return;

        IncrementCapped(connection, transaction, playerId, EnemiesDefeated, request.enemiesDefeated);
        IncrementCapped(connection, transaction, playerId, RoomsCleared, request.roomsCleared);
        IncrementCapped(connection, transaction, playerId, MinibossesDefeated, request.minibossesDefeated);
        IncrementCapped(connection, transaction, playerId, DiceRolled, request.diceRolled);
        IncrementCapped(connection, transaction, playerId, AbilitiesUsed, request.abilitiesUsed);
        IncrementCapped(connection, transaction, playerId, ItemsUsed, request.itemsUsed);
        IncrementCapped(connection, transaction, playerId, ExperienceEarned, request.experienceEarned);
        Increment(connection, transaction, playerId, RunsEnded, 1);

        if (request.defeatedBossIds == null)
            return;

        foreach (string cardId in request.defeatedBossIds)
        {
            if (MinibossCounterByCardId.TryGetValue(Normalize(cardId), out string counterKey))
                Increment(connection, transaction, playerId, counterKey, 1);
        }
    }

    private static void IncrementCapped(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        string counterKey,
        int amount)
    {
        int cap = PerRunCap.TryGetValue(counterKey, out int limit) ? limit : int.MaxValue;
        Increment(connection, transaction, playerId, counterKey, Math.Clamp(amount, 0, cap));
    }

    public static void Increment(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        string counterKey,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(counterKey) || amount <= 0)
            return;

        using SqliteCommand upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = @"
            INSERT INTO player_counters (player_id, counter_key, value, updated_at)
            VALUES ($player, $key, $amount, $now)
            ON CONFLICT(player_id, counter_key) DO UPDATE SET
                value = player_counters.value + $amount,
                updated_at = $now";
        upsert.Parameters.AddWithValue("$player", playerId);
        upsert.Parameters.AddWithValue("$key", counterKey);
        upsert.Parameters.AddWithValue("$amount", amount);
        upsert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        upsert.ExecuteNonQuery();
    }

    public static PlayerCounterData[] Read(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId)
    {
        var counters = new List<PlayerCounterData>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT counter_key, value FROM player_counters WHERE player_id = $player ORDER BY counter_key";
        query.Parameters.AddWithValue("$player", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            counters.Add(new PlayerCounterData { key = reader.GetString(0), value = reader.GetInt32(1) });
        return counters.ToArray();
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
