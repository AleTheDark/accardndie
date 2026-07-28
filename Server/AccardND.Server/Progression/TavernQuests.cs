using AccardND.NetProtocol;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Quest giornaliere della taverna. Sono l'unico rubinetto di miele del gioco: la fine run
/// non ne paga piu', quindi l'economia della bisaccia e degli sblocchi passa tutta da qui.
///
/// Non introducono un secondo sistema di tracciamento: ogni obiettivo e' "fai salire di N un
/// contatore che gia' esiste". All'assegnazione si registra il valore di partenza del
/// contatore, e il progresso e' la differenza: quello che conta per il Santuario conta anche
/// qui, senza codice nuovo nel gameplay. Le quest non si accettano, sono attive dal momento
/// in cui il giocatore apre la taverna (o comunque dal primo contatto del giorno).
///
/// Le otto quest del giorno sono le stesse per tutti e derivano dalla data: nessuna
/// estrazione da memorizzare, e due giocatori possono parlare delle prove di oggi.
/// Ne bastano cinque per il premio di giornata, ed e' quello che permette di proporre
/// anche prove d'arena o di fine capitolo senza penalizzare chi non ci arriva.
/// </summary>
public static class TavernQuests
{
    /// <summary>Ricompensa di una singola quest: volutamente magra, il grosso e' il premio di giornata.</summary>
    public const int QuestHoneyReward = 5;

    /// <summary>Premio di giornata, il grosso della paga.</summary>
    public const int AllQuestsHoneyReward = 50;

    /// <summary>Quante quest si vedono in bacheca ogni giorno.</summary>
    public const int QuestsPerDay = 8;

    /// <summary>
    /// Quante ne bastano per il premio di giornata. Che sia meno di <see cref="QuestsPerDay"/>
    /// e' quello che rende sopportabili le quest fuori portata: la bacheca puo' proporre
    /// l'arena o un boss di capitolo senza che chi non ci arriva perda i 50 vasetti, perche'
    /// le quest sicure del fondo comune sono da sole abbastanza per la soglia.
    /// </summary>
    public const int QuestsRequiredForBonus = 5;

    private const int AdvancedQuestsPerDay = 1;
    private const int PvpQuestsPerDay = 2;

    public const string PoolCore = "core";
    public const string PoolAdvanced = "advanced";
    public const string PoolPvp = "pvp";

    private sealed record Quest(string Id, string CounterKey, int Threshold, string Title, string Description);

    /// <summary>
    /// Quest sempre alla portata di chiunque abbia il primo capitolo: si completano giocando,
    /// senza dipendere da cosa il giocatore ha sbloccato.
    /// </summary>
    private static readonly Quest[] CoreCatalog =
    {
        new("kill-10", CampaignCounters.EnemiesDefeated, 10, "Battuta di caccia", "Uccidi 10 nemici in campagna."),
        new("kill-20", CampaignCounters.EnemiesDefeated, 20, "Cacciatore di mostri", "Uccidi 20 nemici in campagna."),
        new("kill-30", CampaignCounters.EnemiesDefeated, 30, "Ripulitore di corridoi", "Uccidi 30 nemici in campagna."),
        new("kill-40", CampaignCounters.EnemiesDefeated, 40, "Sterminatore", "Uccidi 40 nemici in campagna."),
        new("kill-50", CampaignCounters.EnemiesDefeated, 50, "Flagello dei mostri", "Uccidi 50 nemici in campagna."),
        new("kill-75", CampaignCounters.EnemiesDefeated, 75, "Leggenda del sottosuolo", "Uccidi 75 nemici in campagna."),

        new("rooms-3", CampaignCounters.RoomsCleared, 3, "Esploratore", "Supera 3 stanze."),
        new("rooms-5", CampaignCounters.RoomsCleared, 5, "Passo sicuro", "Supera 5 stanze."),
        new("rooms-8", CampaignCounters.RoomsCleared, 8, "Battitore di sentieri", "Supera 8 stanze."),
        new("rooms-10", CampaignCounters.RoomsCleared, 10, "Profondita' del dungeon", "Supera 10 stanze."),
        new("rooms-15", CampaignCounters.RoomsCleared, 15, "Nessuna porta chiusa", "Supera 15 stanze."),
        new("rooms-20", CampaignCounters.RoomsCleared, 20, "Fino all'ultima soglia", "Supera 20 stanze."),

        new("runs-1", CampaignCounters.RunsEnded, 1, "Fino in fondo", "Concludi una run."),
        new("runs-2", CampaignCounters.RunsEnded, 2, "Doppia discesa", "Concludi 2 run."),
        new("runs-3", CampaignCounters.RunsEnded, 3, "Instancabile", "Concludi 3 run."),
        new("runs-4", CampaignCounters.RunsEnded, 4, "Nessuna sosta", "Concludi 4 run."),

        new("dice-50", CampaignCounters.DiceRolled, 50, "Mano calda", "Tira 50 volte i dadi."),
        new("dice-100", CampaignCounters.DiceRolled, 100, "Amico della sorte", "Tira 100 volte i dadi."),
        new("dice-150", CampaignCounters.DiceRolled, 150, "Sfida al destino", "Tira 150 volte i dadi."),
        new("dice-200", CampaignCounters.DiceRolled, 200, "Il tavolo brucia", "Tira 200 volte i dadi."),
        new("dice-300", CampaignCounters.DiceRolled, 300, "Le ossa non mentono", "Tira 300 volte i dadi."),

        new("ability-5", CampaignCounters.AbilitiesUsed, 5, "Mestiere", "Usa 5 volte l'abilita' di classe."),
        new("ability-10", CampaignCounters.AbilitiesUsed, 10, "Arte della classe", "Usa 10 volte l'abilita' di classe."),
        new("ability-15", CampaignCounters.AbilitiesUsed, 15, "Scuola di guerra", "Usa 15 volte l'abilita' di classe."),
        new("ability-20", CampaignCounters.AbilitiesUsed, 20, "Maestria", "Usa 20 volte l'abilita' di classe."),
        new("ability-30", CampaignCounters.AbilitiesUsed, 30, "Virtuoso", "Usa 30 volte l'abilita' di classe."),

        new("exp-300", CampaignCounters.ExperienceEarned, 300, "Lezioni sul campo", "Guadagna 300 esperienza in campagna."),
        new("exp-600", CampaignCounters.ExperienceEarned, 600, "Veterano del giorno", "Guadagna 600 esperienza in campagna."),
        new("exp-1000", CampaignCounters.ExperienceEarned, 1000, "Scuola dura", "Guadagna 1000 esperienza in campagna."),
        new("exp-1500", CampaignCounters.ExperienceEarned, 1500, "Sapere pagato caro", "Guadagna 1500 esperienza in campagna.")
    };

    /// <summary>
    /// Quest che chiedono qualcosa in piu': arrivare a un miniboss, chiudere un capitolo,
    /// avere consumabili in bisaccia. Ne esce al massimo una al giorno.
    /// </summary>
    private static readonly Quest[] AdvancedCatalog =
    {
        new("miniboss-1", CampaignCounters.MinibossesDefeated, 1, "Il guardiano", "Uccidi un miniboss."),
        new("miniboss-2", CampaignCounters.MinibossesDefeated, 2, "Due teste cadute", "Uccidi 2 miniboss."),
        new("miniboss-3", CampaignCounters.MinibossesDefeated, 3, "Caccia grossa", "Uccidi 3 miniboss."),
        new("miniboss-4", CampaignCounters.MinibossesDefeated, 4, "Nessun guardiano in piedi", "Uccidi 4 miniboss."),

        new("boss-1", CampaignCounters.BossesDefeated, 1, "Fine del capitolo", "Sconfiggi il boss finale di un capitolo."),
        new("boss-2", CampaignCounters.BossesDefeated, 2, "Doppio trofeo", "Sconfiggi 2 boss di capitolo."),
        new("boss-3", CampaignCounters.BossesDefeated, 3, "Tre corone", "Sconfiggi 3 boss di capitolo."),

        new("items-2", CampaignCounters.ItemsUsed, 2, "Mano nella bisaccia", "Usa 2 volte gli oggetti."),
        new("items-5", CampaignCounters.ItemsUsed, 5, "Bisaccia svuotata", "Usa 5 volte gli oggetti."),
        new("items-8", CampaignCounters.ItemsUsed, 8, "Alchimista da viaggio", "Usa 8 volte gli oggetti."),
        new("items-12", CampaignCounters.ItemsUsed, 12, "Niente resta nello zaino", "Usa 12 volte gli oggetti.")
    };

    /// <summary>
    /// Quest d'arena. Dipendono dal fatto che ci sia qualcuno dall'altra parte, quindi non
    /// devono mai essere indispensabili: ne escono al massimo due su otto, e la soglia del
    /// premio e' raggiungibile senza toccarne nessuna.
    /// I contatori li scrive il server a fine partita, non il client.
    /// </summary>
    private static readonly Quest[] PvpCatalog =
    {
        new("pvp-play-1", CampaignCounters.PvpMatches, 1, "Primo sfidante", "Gioca una partita PvP."),
        new("pvp-play-2", CampaignCounters.PvpMatches, 2, "Frequentatore dell'arena", "Gioca 2 partite PvP."),
        new("pvp-play-3", CampaignCounters.PvpMatches, 3, "Giornata in arena", "Gioca 3 partite PvP."),
        new("pvp-play-5", CampaignCounters.PvpMatches, 5, "Maratona d'arena", "Gioca 5 partite PvP."),

        new("pvp-win-1", CampaignCounters.PvpWins, 1, "Vittoria onorevole", "Vinci una partita PvP."),
        new("pvp-win-2", CampaignCounters.PvpWins, 2, "Doppietta in arena", "Vinci 2 partite PvP."),
        new("pvp-win-3", CampaignCounters.PvpWins, 3, "Dominio", "Vinci 3 partite PvP."),

        new("pvp-rounds-3", CampaignCounters.PvpRoundsWon, 3, "Tre round tuoi", "Vinci 3 round in PvP."),
        new("pvp-rounds-5", CampaignCounters.PvpRoundsWon, 5, "Padrone del tavolo", "Vinci 5 round in PvP."),
        new("pvp-rounds-8", CampaignCounters.PvpRoundsWon, 8, "Nessuno ti tocca", "Vinci 8 round in PvP.")
    };

    /// <summary>
    /// Una quest vista da fuori (pannello admin, diagnostica): le stesse informazioni del
    /// catalogo, senza esporre il tipo interno ne' permettere di modificarlo.
    /// </summary>
    public sealed record QuestDefinition(
        string Id, string CounterKey, int Threshold, string Title, string Description, string Pool)
    {
        /// <summary>Quest di campagna che richiede progressione (miniboss, boss, consumabili).</summary>
        public bool Advanced => Pool == PoolAdvanced;

        /// <summary>Quest d'arena: dipende da un avversario, quindi non e' mai obbligatoria.</summary>
        public bool Pvp => Pool == PoolPvp;
    }

    /// <summary>Le quest estratte per una data, nell'ordine in cui vengono assegnate.</summary>
    public static IReadOnlyList<QuestDefinition> DefinitionsForDay(string day) =>
        SelectForDay(day).Select(Describe).ToList();

    /// <summary>Catalogo completo, nello stesso ordine di fondo usato dall'estrazione.</summary>
    public static IReadOnlyList<QuestDefinition> AllDefinitions() =>
        AdvancedCatalog.Concat(PvpCatalog).Concat(CoreCatalog).Select(Describe).ToList();

    /// <summary>
    /// Descrive una quest per id. Utile per le righe storiche: una quest tolta dal catalogo
    /// resta in <c>player_tavern_quests</c>, e chi legge deve poter dire che non c'e' piu'.
    /// </summary>
    public static bool TryDescribe(string questId, out QuestDefinition definition)
    {
        if (TryFindQuest(Normalize(questId), out Quest quest))
        {
            definition = Describe(quest);
            return true;
        }
        definition = null;
        return false;
    }

    private static QuestDefinition Describe(Quest quest) => new(
        quest.Id, quest.CounterKey, quest.Threshold, quest.Title, quest.Description, PoolOf(quest));

    private static string PoolOf(Quest quest)
    {
        if (Array.IndexOf(AdvancedCatalog, quest) >= 0)
            return PoolAdvanced;
        return Array.IndexOf(PvpCatalog, quest) >= 0 ? PoolPvp : PoolCore;
    }

    public static string TodayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    /// <summary>Secondi che mancano alla mezzanotte UTC, cioe' al cambio delle quest.</summary>
    public static int SecondsToRefresh()
    {
        DateTime now = DateTime.UtcNow;
        return (int)Math.Ceiling((now.Date.AddDays(1) - now).TotalSeconds);
    }

    /// <summary>
    /// Assegna (se mancano) le quest di oggi e restituisce la bacheca valutata.
    /// L'assegnazione avviene al primo contatto del giorno: il baseline e' il contatore in
    /// quel momento, quindi quello che il giocatore ha fatto ieri non conta per oggi.
    /// </summary>
    public static TavernData ReadOrAssign(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int honey)
    {
        string day = TodayKey();
        AssignIfMissing(connection, transaction, playerId, day);

        var quests = new List<TavernQuestData>();
        int completed = 0;
        foreach ((string questId, int baseline, bool claimed) in ReadRows(connection, transaction, playerId, day))
        {
            // Una quest che non esiste piu' nel catalogo (deploy a giornata iniziata) sparisce
            // dalla bacheca invece di bloccare il premio: il giorno dopo si riassegna comunque.
            if (!TryFindQuest(questId, out Quest quest))
                continue;

            int gained = Math.Max(0, ReadCounter(connection, transaction, playerId, quest.CounterKey) - baseline);
            bool isCompleted = gained >= quest.Threshold;
            if (isCompleted)
                completed++;

            quests.Add(new TavernQuestData
            {
                questId = quest.Id,
                title = quest.Title,
                description = quest.Description,
                // Il progresso mostrato non supera la soglia: "34/20 nemici" sarebbe rumore.
                current = Math.Min(gained, quest.Threshold),
                threshold = quest.Threshold,
                completed = isCompleted,
                claimed = claimed,
                honeyReward = QuestHoneyReward
            });
        }

        // Se il catalogo si e' ristretto a giornata iniziata la soglia non puo' restare sopra
        // il numero di quest davvero in bacheca, altrimenti il premio diventa irraggiungibile.
        int required = Math.Min(QuestsRequiredForBonus, quests.Count);
        bool bonusClaimed = IsBonusClaimed(connection, transaction, playerId, day);
        return new TavernData
        {
            honey = honey,
            quests = quests.ToArray(),
            completedCount = completed,
            questsRequiredForBonus = required,
            bonusHoneyReward = AllQuestsHoneyReward,
            bonusAvailable = required > 0 && completed >= required,
            bonusClaimed = bonusClaimed,
            secondsToRefresh = SecondsToRefresh()
        };
    }

    /// <summary>
    /// Riscuote una quest completata. Idempotente: una seconda chiamata non paga due volte.
    /// Ritorna un messaggio d'errore se non e' riscuotibile, altrimenti null.
    /// </summary>
    public static string ClaimQuest(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string questId)
    {
        string day = TodayKey();
        AssignIfMissing(connection, transaction, playerId, day);

        string normalized = Normalize(questId);
        if (!TryFindQuest(normalized, out Quest quest))
            return "Quest non valida.";

        (int baseline, bool claimed, bool assigned) = ReadRow(connection, transaction, playerId, day, normalized);
        if (!assigned)
            return "Quest non fra quelle di oggi.";
        if (claimed)
            return "Ricompensa gia' riscossa.";

        int gained = Math.Max(0, ReadCounter(connection, transaction, playerId, quest.CounterKey) - baseline);
        if (gained < quest.Threshold)
            return "Quest non ancora completata.";

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE player_tavern_quests SET claimed_at = $now
                WHERE player_id = $player AND day = $day AND quest_id = $quest AND claimed_at IS NULL";
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$player", playerId);
            update.Parameters.AddWithValue("$day", day);
            update.Parameters.AddWithValue("$quest", normalized);
            if (update.ExecuteNonQuery() == 0)
                return "Ricompensa gia' riscossa.";
        }

        GrantHoney(connection, transaction, playerId, QuestHoneyReward);
        return null;
    }

    /// <summary>
    /// Riscuote il premio di giornata. Richiede che siano complete almeno
    /// <see cref="QuestsRequiredForBonus"/> quest fra quelle di oggi (non che siano gia'
    /// state riscosse una per una). Idempotente per giornata.
    /// </summary>
    public static string ClaimBonus(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        string day = TodayKey();
        TavernData state = ReadOrAssign(connection, transaction, playerId, 0);
        if (state.bonusClaimed)
            return "Premio di giornata gia' riscosso.";
        if (!state.bonusAvailable)
            return $"Completa {state.questsRequiredForBonus} quest del giorno per il premio.";

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO player_tavern_bonus (player_id, day, claimed_at)
                VALUES ($player, $day, $now)";
            insert.Parameters.AddWithValue("$player", playerId);
            insert.Parameters.AddWithValue("$day", day);
            insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            if (insert.ExecuteNonQuery() == 0)
                return "Premio di giornata gia' riscosso.";
        }

        GrantHoney(connection, transaction, playerId, AllQuestsHoneyReward);
        CampaignCounters.Increment(connection, transaction, playerId, CampaignCounters.DailyCompleted, 1);
        return null;
    }

    /// <summary>
    /// Assegna le quest di oggi se non ci sono gia'. Idempotente, e va chiamata il prima
    /// possibile nella sessione: il baseline dei contatori si fissa qui, quindi ogni cosa
    /// fatta prima di questa chiamata non conta per la giornata.
    /// </summary>
    public static void AssignIfMissing(
        SqliteConnection connection, SqliteTransaction transaction, string playerId) =>
        AssignIfMissing(connection, transaction, playerId, TodayKey());

    private static void AssignIfMissing(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string day)
    {
        foreach (Quest quest in SelectForDay(day))
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO player_tavern_quests (player_id, day, quest_id, baseline)
                VALUES ($player, $day, $quest, $baseline)";
            insert.Parameters.AddWithValue("$player", playerId);
            insert.Parameters.AddWithValue("$day", day);
            insert.Parameters.AddWithValue("$quest", quest.Id);
            insert.Parameters.AddWithValue(
                "$baseline", ReadCounter(connection, transaction, playerId, quest.CounterKey));
            insert.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Le otto quest del giorno. Stabili per data: non usa GetHashCode perche' non e'
    /// garantito costante tra avvii e un riavvio del server cambierebbe le quest a meta'
    /// giornata. Niente due quest sullo stesso contatore, altrimenti "uccidi 10" e "uccidi 20"
    /// si completerebbero insieme e la giornata varrebbe la meta'.
    ///
    /// Le quote (1 avanzata, 2 d'arena, il resto dal fondo comune) garantiscono che le
    /// cinque quest necessarie al premio siano sempre raggiungibili giocando da solo in
    /// campagna: chi non fa PvP e non e' ancora arrivato ai boss perde tre righe su otto,
    /// non il premio.
    /// </summary>
    private static List<Quest> SelectForDay(string day)
    {
        var seed = new DailySequence(day);
        var picked = new List<Quest>();
        var usedCounters = new HashSet<string>();

        Take(AdvancedCatalog, AdvancedQuestsPerDay);
        Take(PvpCatalog, AdvancedQuestsPerDay + PvpQuestsPerDay);
        Take(CoreCatalog, QuestsPerDay);
        return picked;

        void Take(Quest[] catalog, int upTo)
        {
            foreach (int index in seed.Shuffle(catalog.Length))
            {
                if (picked.Count >= upTo)
                    return;
                Quest quest = catalog[index];
                if (usedCounters.Add(quest.CounterKey))
                    picked.Add(quest);
            }
        }
    }

    /// <summary>
    /// Estrazione riproducibile a partire dalla data: hash FNV-1a del giorno come seme,
    /// poi xorshift. Stesso giorno, stesse quest, su qualunque processo.
    /// </summary>
    private readonly struct DailySequence
    {
        private readonly uint seed;

        public DailySequence(string day)
        {
            uint hash = 2166136261u;
            foreach (char character in day ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            seed = hash == 0 ? 1u : hash;
        }

        /// <summary>Indici da 0 a count-1 in ordine mescolato (Fisher-Yates).</summary>
        public int[] Shuffle(int count)
        {
            int[] order = new int[count];
            for (int index = 0; index < count; index++)
                order[index] = index;

            uint state = seed;
            for (int index = count - 1; index > 0; index--)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                int swap = (int)(state % (uint)(index + 1));
                (order[index], order[swap]) = (order[swap], order[index]);
            }
            return order;
        }
    }

    private static bool TryFindQuest(string questId, out Quest quest)
    {
        foreach (Quest candidate in CoreCatalog)
        {
            if (candidate.Id == questId)
            {
                quest = candidate;
                return true;
            }
        }
        foreach (Quest candidate in AdvancedCatalog)
        {
            if (candidate.Id == questId)
            {
                quest = candidate;
                return true;
            }
        }
        foreach (Quest candidate in PvpCatalog)
        {
            if (candidate.Id == questId)
            {
                quest = candidate;
                return true;
            }
        }
        quest = null;
        return false;
    }

    private static List<(string QuestId, int Baseline, bool Claimed)> ReadRows(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string day)
    {
        var rows = new List<(string, int, bool)>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT quest_id, baseline, claimed_at FROM player_tavern_quests
            WHERE player_id = $player AND day = $day
            ORDER BY rowid";
        query.Parameters.AddWithValue("$player", playerId);
        query.Parameters.AddWithValue("$day", day);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            rows.Add((reader.GetString(0), reader.GetInt32(1), !reader.IsDBNull(2)));
        return rows;
    }

    private static (int Baseline, bool Claimed, bool Assigned) ReadRow(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string day, string questId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT baseline, claimed_at FROM player_tavern_quests
            WHERE player_id = $player AND day = $day AND quest_id = $quest";
        query.Parameters.AddWithValue("$player", playerId);
        query.Parameters.AddWithValue("$day", day);
        query.Parameters.AddWithValue("$quest", questId);
        using SqliteDataReader reader = query.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), !reader.IsDBNull(1), true) : (0, false, false);
    }

    private static bool IsBonusClaimed(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string day)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT 1 FROM player_tavern_bonus WHERE player_id = $player AND day = $day";
        query.Parameters.AddWithValue("$player", playerId);
        query.Parameters.AddWithValue("$day", day);
        using SqliteDataReader reader = query.ExecuteReader();
        return reader.Read();
    }

    private static void GrantHoney(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int amount)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET honey = honey + $honey, updated_at = $now
            WHERE player_id = $player";
        update.Parameters.AddWithValue("$honey", amount);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$player", playerId);
        update.ExecuteNonQuery();
    }

    private static int ReadCounter(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string counterKey)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT value FROM player_counters WHERE player_id = $player AND counter_key = $key";
        query.Parameters.AddWithValue("$player", playerId);
        query.Parameters.AddWithValue("$key", counterKey);
        object value = query.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
