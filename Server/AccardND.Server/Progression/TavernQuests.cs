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
/// Le dieci quest del giorno sono le stesse per tutti e derivano dalla data: nessuna
/// estrazione da memorizzare, e due giocatori possono parlare delle prove di oggi.
/// Il premio usa punti pesati per difficolta', cosi' una prova impegnativa accelera davvero
/// il traguardo senza rendere obbligatorie arena o fine capitolo.
/// </summary>
public static class TavernQuests
{
    /// <summary>Ricompensa base di una quest facile. Le difficolta' superiori pagano di piu'.</summary>
    public const int QuestHoneyReward = 1;

    /// <summary>Premio di giornata, il grosso della paga.</summary>
    public const int AllQuestsHoneyReward = 50;

    /// <summary>Quante quest si vedono in bacheca ogni giorno.</summary>
    public const int QuestsPerDay = 10;

    /// <summary>
    /// Punti necessari al premio. Le 5 facili e le 3 intermedie valgono insieme 11 punti:
    /// le 2 avanzate accelerano il traguardo, ma non sono obbligatorie.
    /// </summary>
    public const int BonusPointsRequired = 10;

    private const int EasyQuestsPerDay = 5;
    private const int IntermediateQuestsPerDay = 3;
    private const int AdvancedQuestsPerDay = 2;

    /// <summary>
    /// Quante quest d'arena possono uscire nello stesso giorno. Con due il resto della
    /// bacheca vale sempre almeno <see cref="BonusPointsRequired"/> punti, quindi il premio
    /// di giornata resta raggiungibile da chi non entra mai in coda. Senza questo tetto tre
    /// quest PvP pesanti si prenderebbero 8 dei 17 punti e il premio diventerebbe d'arena.
    /// </summary>
    private const int PvpQuestsPerDay = 2;

    public const string PoolCore = "core";
    public const string PoolAdvanced = "advanced";
    public const string PoolPvp = "pvp";

    // Scorciatoie: il catalogo e' una tabella, e con i nomi lunghi dell'enum ogni riga
    // andrebbe a capo.
    private const TavernQuestDifficulty Easy = TavernQuestDifficulty.Easy;
    private const TavernQuestDifficulty Medium = TavernQuestDifficulty.Intermediate;
    private const TavernQuestDifficulty Hard = TavernQuestDifficulty.Advanced;

    /// <summary>Il miele base segue la difficolta': verde 1, arancione 2, rossa 3.</summary>
    public static int HoneyRewardFor(TavernQuestDifficulty difficulty) => difficulty switch
    {
        TavernQuestDifficulty.Intermediate => 2,
        TavernQuestDifficulty.Advanced => 3,
        _ => QuestHoneyReward
    };

    private sealed record Quest(
        string Id, string CounterKey, int Threshold, TavernQuestDifficulty Difficulty,
        string Title, string Description);

    /// <summary>
    /// Quest sempre alla portata di chiunque abbia il primo capitolo: si completano giocando,
    /// senza dipendere da cosa il giocatore ha sbloccato.
    /// </summary>
    private static readonly Quest[] CoreCatalog =
    {
        new("kill-10", CampaignCounters.EnemiesDefeated, 10, Easy, "Battuta di caccia", "Uccidi 10 nemici in campagna."),
        new("kill-15", CampaignCounters.EnemiesDefeated, 15, Easy, "Sentieri ripuliti", "Uccidi 15 nemici in campagna."),
        new("kill-20", CampaignCounters.EnemiesDefeated, 20, Easy, "Cacciatore di mostri", "Uccidi 20 nemici in campagna."),
        new("kill-30", CampaignCounters.EnemiesDefeated, 30, Medium, "Ripulitore di corridoi", "Uccidi 30 nemici in campagna."),
        new("kill-40", CampaignCounters.EnemiesDefeated, 40, Medium, "Sterminatore", "Uccidi 40 nemici in campagna."),
        new("kill-50", CampaignCounters.EnemiesDefeated, 50, Hard, "Flagello dei mostri", "Uccidi 50 nemici in campagna."),
        new("kill-75", CampaignCounters.EnemiesDefeated, 75, Hard, "Leggenda del sottosuolo", "Uccidi 75 nemici in campagna."),
        new("kill-100", CampaignCounters.EnemiesDefeated, 100, Hard, "Marea di ferro", "Uccidi 100 nemici in campagna."),

        new("rooms-3", CampaignCounters.RoomsCleared, 3, Easy, "Esploratore", "Supera 3 stanze."),
        new("rooms-5", CampaignCounters.RoomsCleared, 5, Easy, "Passo sicuro", "Supera 5 stanze."),
        new("rooms-6", CampaignCounters.RoomsCleared, 6, Easy, "Sempre piu' giu'", "Supera 6 stanze."),
        new("rooms-8", CampaignCounters.RoomsCleared, 8, Medium, "Battitore di sentieri", "Supera 8 stanze."),
        new("rooms-10", CampaignCounters.RoomsCleared, 10, Medium, "Profondita' del dungeon", "Supera 10 stanze."),
        new("rooms-12", CampaignCounters.RoomsCleared, 12, Medium, "Corridoi senza fine", "Supera 12 stanze."),
        new("rooms-15", CampaignCounters.RoomsCleared, 15, Hard, "Nessuna porta chiusa", "Supera 15 stanze."),
        new("rooms-20", CampaignCounters.RoomsCleared, 20, Hard, "Fino all'ultima soglia", "Supera 20 stanze."),
        new("rooms-25", CampaignCounters.RoomsCleared, 25, Hard, "Il fondo del pozzo", "Supera 25 stanze."),

        new("runs-1", CampaignCounters.RunsEnded, 1, Easy, "Fino in fondo", "Concludi una run."),
        new("runs-2", CampaignCounters.RunsEnded, 2, Medium, "Doppia discesa", "Concludi 2 run."),
        new("runs-3", CampaignCounters.RunsEnded, 3, Medium, "Instancabile", "Concludi 3 run."),
        new("runs-4", CampaignCounters.RunsEnded, 4, Hard, "Nessuna sosta", "Concludi 4 run."),
        new("runs-5", CampaignCounters.RunsEnded, 5, Hard, "Giornata al tavolo", "Concludi 5 run."),

        new("dice-50", CampaignCounters.DiceRolled, 50, Easy, "Mano calda", "Tira 50 volte i dadi."),
        new("dice-75", CampaignCounters.DiceRolled, 75, Easy, "Le dita sporche di gesso", "Tira 75 volte i dadi."),
        new("dice-100", CampaignCounters.DiceRolled, 100, Medium, "Amico della sorte", "Tira 100 volte i dadi."),
        new("dice-150", CampaignCounters.DiceRolled, 150, Medium, "Sfida al destino", "Tira 150 volte i dadi."),
        new("dice-200", CampaignCounters.DiceRolled, 200, Hard, "Il tavolo brucia", "Tira 200 volte i dadi."),
        new("dice-300", CampaignCounters.DiceRolled, 300, Hard, "Le ossa non mentono", "Tira 300 volte i dadi."),
        new("dice-400", CampaignCounters.DiceRolled, 400, Hard, "Tempesta d'avorio", "Tira 400 volte i dadi."),

        new("ability-5", CampaignCounters.AbilitiesUsed, 5, Easy, "Mestiere", "Usa 5 volte l'abilita' di classe."),
        new("ability-10", CampaignCounters.AbilitiesUsed, 10, Easy, "Arte della classe", "Usa 10 volte l'abilita' di classe."),
        new("ability-15", CampaignCounters.AbilitiesUsed, 15, Medium, "Scuola di guerra", "Usa 15 volte l'abilita' di classe."),
        new("ability-20", CampaignCounters.AbilitiesUsed, 20, Medium, "Maestria", "Usa 20 volte l'abilita' di classe."),
        new("ability-30", CampaignCounters.AbilitiesUsed, 30, Hard, "Virtuoso", "Usa 30 volte l'abilita' di classe."),
        new("ability-40", CampaignCounters.AbilitiesUsed, 40, Hard, "Nessun gesto sprecato", "Usa 40 volte l'abilita' di classe."),

        new("exp-300", CampaignCounters.ExperienceEarned, 300, Easy, "Lezioni sul campo", "Guadagna 300 esperienza in campagna."),
        new("exp-450", CampaignCounters.ExperienceEarned, 450, Easy, "Pratica quotidiana", "Guadagna 450 esperienza in campagna."),
        new("exp-600", CampaignCounters.ExperienceEarned, 600, Medium, "Veterano del giorno", "Guadagna 600 esperienza in campagna."),
        new("exp-800", CampaignCounters.ExperienceEarned, 800, Medium, "Mestiere pagato", "Guadagna 800 esperienza in campagna."),
        new("exp-1000", CampaignCounters.ExperienceEarned, 1000, Hard, "Scuola dura", "Guadagna 1000 esperienza in campagna."),
        new("exp-1500", CampaignCounters.ExperienceEarned, 1500, Hard, "Sapere pagato caro", "Guadagna 1500 esperienza in campagna."),
        new("exp-2000", CampaignCounters.ExperienceEarned, 2000, Hard, "Il prezzo della saggezza", "Guadagna 2000 esperienza in campagna."),

        new("supreme-1", CampaignCounters.SupremesUsed, 1, Easy, "Colpo supremo", "Attiva una suprema in campagna."),
        new("supreme-3", CampaignCounters.SupremesUsed, 3, Easy, "Il tavolo trema", "Attiva 3 supreme in campagna."),
        new("supreme-5", CampaignCounters.SupremesUsed, 5, Medium, "Arsenale aperto", "Attiva 5 supreme in campagna."),
        new("supreme-8", CampaignCounters.SupremesUsed, 8, Medium, "Spettacolo di potere", "Attiva 8 supreme in campagna."),
        new("supreme-12", CampaignCounters.SupremesUsed, 12, Hard, "Furia incontenibile", "Attiva 12 supreme in campagna."),

        new("flash-1", CampaignCounters.QuickChallenges, 1, Easy, "Prova lampo", "Completa una stanza Sfida veloce."),
        new("flash-2", CampaignCounters.QuickChallenges, 2, Easy, "Riflessi pronti", "Completa 2 stanze Sfida veloce."),
        new("flash-3", CampaignCounters.QuickChallenges, 3, Medium, "Occhio allenato", "Completa 3 stanze Sfida veloce."),
        new("flash-5", CampaignCounters.QuickChallenges, 5, Hard, "Mente fulminea", "Completa 5 stanze Sfida veloce."),

        new("market-1", CampaignCounters.MerchantPurchases, 1, Easy, "Cliente del mercante", "Concludi un affare col mercante."),
        new("market-2", CampaignCounters.MerchantPurchases, 2, Easy, "Giro dei banchi", "Concludi 2 affari col mercante."),
        new("market-4", CampaignCounters.MerchantPurchases, 4, Medium, "Borsa leggera", "Concludi 4 affari col mercante."),
        new("market-6", CampaignCounters.MerchantPurchases, 6, Medium, "Mercante compiaciuto", "Concludi 6 affari col mercante."),
        new("market-9", CampaignCounters.MerchantPurchases, 9, Hard, "Il miglior cliente", "Concludi 9 affari col mercante."),

        new("gold-100", CampaignCounters.GoldEarned, 100, Easy, "Prime monete", "Guadagna 100 oro in campagna."),
        new("gold-200", CampaignCounters.GoldEarned, 200, Easy, "Borsa che tintinna", "Guadagna 200 oro in campagna."),
        new("gold-400", CampaignCounters.GoldEarned, 400, Medium, "Sacchetto pieno", "Guadagna 400 oro in campagna."),
        new("gold-700", CampaignCounters.GoldEarned, 700, Medium, "Bottino di giornata", "Guadagna 700 oro in campagna."),
        new("gold-1200", CampaignCounters.GoldEarned, 1200, Hard, "Tesoriere del dungeon", "Guadagna 1200 oro in campagna."),

        new("level-2", CampaignCounters.LevelsGained, 2, Easy, "Passo avanti", "Sali 2 volte di livello in campagna."),
        new("level-3", CampaignCounters.LevelsGained, 3, Easy, "Si impara scendendo", "Sali 3 volte di livello in campagna."),
        new("level-5", CampaignCounters.LevelsGained, 5, Medium, "Scalata", "Sali 5 volte di livello in campagna."),
        new("level-8", CampaignCounters.LevelsGained, 8, Hard, "Ascesa", "Sali 8 volte di livello in campagna.")
    };

    /// <summary>
    /// Quest che chiedono qualcosa in piu': arrivare a un miniboss, chiudere un capitolo,
    /// avere consumabili in bisaccia. Il pool non e' una quota d'estrazione (quella la
    /// decide la difficolta'): e' l'etichetta che dice a chi legge la bacheca - noi e il
    /// pannello admin - perche' una quest puo' restare ferma anche giocando.
    /// </summary>
    private static readonly Quest[] AdvancedCatalog =
    {
        new("miniboss-1", CampaignCounters.MinibossesDefeated, 1, Easy, "Il guardiano", "Uccidi un miniboss."),
        new("miniboss-2", CampaignCounters.MinibossesDefeated, 2, Medium, "Due teste cadute", "Uccidi 2 miniboss."),
        new("miniboss-3", CampaignCounters.MinibossesDefeated, 3, Hard, "Caccia grossa", "Uccidi 3 miniboss."),
        new("miniboss-4", CampaignCounters.MinibossesDefeated, 4, Hard, "Nessun guardiano in piedi", "Uccidi 4 miniboss."),

        new("boss-1", CampaignCounters.BossesDefeated, 1, Medium, "Fine del capitolo", "Sconfiggi il boss finale di un capitolo."),
        new("boss-2", CampaignCounters.BossesDefeated, 2, Hard, "Doppio trofeo", "Sconfiggi 2 boss di capitolo."),
        new("boss-3", CampaignCounters.BossesDefeated, 3, Hard, "Tre corone", "Sconfiggi 3 boss di capitolo."),

        // Contano tutti gli usi, non solo quelli della bisaccia: anche un oggetto trovato in
        // una stanza o comprato al mercante e usato sul posto. I titoli non nominano la
        // bisaccia per non far credere il contrario.
        new("items-2", CampaignCounters.ItemsUsed, 2, Easy, "Mano lesta", "Usa 2 volte gli oggetti."),
        new("items-3", CampaignCounters.ItemsUsed, 3, Easy, "Scorte in viaggio", "Usa 3 volte gli oggetti."),
        new("items-5", CampaignCounters.ItemsUsed, 5, Medium, "Niente si spreca", "Usa 5 volte gli oggetti."),
        new("items-8", CampaignCounters.ItemsUsed, 8, Hard, "Alchimista da viaggio", "Usa 8 volte gli oggetti."),
        new("items-12", CampaignCounters.ItemsUsed, 12, Hard, "Tutto quello che trovo", "Usa 12 volte gli oggetti.")
    };

    /// <summary>
    /// Quest d'arena. Dipendono dal fatto che ci sia qualcuno dall'altra parte, quindi non
    /// devono mai essere indispensabili: ne escono al massimo <see cref="PvpQuestsPerDay"/>
    /// al giorno, e con quel tetto la soglia del premio resta raggiungibile senza toccarne
    /// nessuna.
    /// I contatori li scrive il server a fine partita, non il client, e solo per le partite
    /// classificate: le amichevoli in stanza non contano, altrimenti due account complici
    /// chiuderebbero la giornata (cioe' il miele) senza mai entrare in coda. Le descrizioni
    /// lo dicono, perche' una quest che non avanza mentre il giocatore sta giocando davvero
    /// sembra rotta.
    /// </summary>
    private static readonly Quest[] PvpCatalog =
    {
        new("pvp-play-1", CampaignCounters.PvpMatches, 1, Easy, "Primo sfidante", "Gioca una partita PvP classificata."),
        new("pvp-play-2", CampaignCounters.PvpMatches, 2, Medium, "Frequentatore dell'arena", "Gioca 2 partite PvP classificate."),
        new("pvp-play-3", CampaignCounters.PvpMatches, 3, Medium, "Giornata in arena", "Gioca 3 partite PvP classificate."),
        new("pvp-play-5", CampaignCounters.PvpMatches, 5, Hard, "Maratona d'arena", "Gioca 5 partite PvP classificate."),

        new("pvp-win-1", CampaignCounters.PvpWins, 1, Easy, "Vittoria onorevole", "Vinci una partita PvP classificata."),
        new("pvp-win-2", CampaignCounters.PvpWins, 2, Medium, "Doppietta in arena", "Vinci 2 partite PvP classificate."),
        new("pvp-win-3", CampaignCounters.PvpWins, 3, Hard, "Dominio", "Vinci 3 partite PvP classificate."),
        new("pvp-win-4", CampaignCounters.PvpWins, 4, Hard, "Serie aperta", "Vinci 4 partite PvP classificate."),

        new("pvp-rounds-3", CampaignCounters.PvpRoundsWon, 3, Easy, "Tre round tuoi", "Vinci 3 round in PvP classificato."),
        new("pvp-rounds-5", CampaignCounters.PvpRoundsWon, 5, Medium, "Padrone del tavolo", "Vinci 5 round in PvP classificato."),
        new("pvp-rounds-8", CampaignCounters.PvpRoundsWon, 8, Hard, "Nessuno ti tocca", "Vinci 8 round in PvP classificato."),
        new("pvp-rounds-12", CampaignCounters.PvpRoundsWon, 12, Hard, "Il tavolo e' tuo", "Vinci 12 round in PvP classificato.")
    };

    /// <summary>
    /// Una quest vista da fuori (pannello admin, diagnostica): le stesse informazioni del
    /// catalogo, senza esporre il tipo interno ne' permettere di modificarlo.
    /// </summary>
    public sealed record QuestDefinition(
        string Id, string CounterKey, int Threshold, string Title, string Description, string Pool,
        TavernQuestDifficulty Difficulty, int BonusPoints)
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

    private static QuestDefinition Describe(Quest quest) =>
        new(quest.Id, quest.CounterKey, quest.Threshold, quest.Title, quest.Description,
            PoolOf(quest), quest.Difficulty, BonusPointsFor(quest.Difficulty));

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

        // La bacheca e' l'estrazione del giorno, non l'elenco delle righe a database: le righe
        // dicono solo da che punto contare e cosa e' gia' stato riscosso. Leggerle tutte
        // significherebbe che un deploy a giornata iniziata somma la vecchia estrazione alla
        // nuova e mostra fino a venti quest, perche' AssignIfMissing inserisce quelle nuove
        // accanto a quelle vecchie senza poterle togliere (toglierle brucerebbe il progresso
        // di chi ha gia' giocato). Cosi' invece le righe orfane restano a database, innocue,
        // e la bacheca ne mostra dieci comunque.
        var rows = ReadRows(connection, transaction, playerId, day);
        var quests = new List<TavernQuestData>();
        int completed = 0;
        int completedPoints = 0;
        foreach (Quest quest in SelectForDay(day))
        {
            if (!rows.TryGetValue(quest.Id, out (int Baseline, bool Claimed) row))
                continue;

            int baseline = row.Baseline;
            bool claimed = row.Claimed;
            int gained = Math.Max(0, ReadCounter(connection, transaction, playerId, quest.CounterKey) - baseline);
            bool isCompleted = gained >= quest.Threshold;
            TavernQuestDifficulty difficulty = quest.Difficulty;
            if (isCompleted)
            {
                completed++;
                completedPoints += BonusPointsFor(difficulty);
            }

            quests.Add(new TavernQuestData
            {
                questId = quest.Id,
                titleKey = $"tavern.quest.{quest.Id}.title",
                descriptionKey = $"tavern.quest.{quest.Id}.description",
                title = quest.Title,
                description = quest.Description,
                // Il progresso mostrato non supera la soglia: "34/20 nemici" sarebbe rumore.
                current = Math.Min(gained, quest.Threshold),
                threshold = quest.Threshold,
                completed = isCompleted,
                difficulty = difficulty,
                bonusPoints = BonusPointsFor(difficulty),
                claimed = claimed,
                honeyReward = HoneyRewardFor(difficulty)
            });
        }

        // Se il catalogo si e' ristretto a giornata iniziata la soglia non puo' restare sopra
        // il numero di quest davvero in bacheca, altrimenti il premio diventa irraggiungibile.
        int availablePoints = quests.Sum(quest => quest.bonusPoints);
        int requiredPoints = Math.Min(BonusPointsRequired, availablePoints);
        bool bonusClaimed = IsBonusClaimed(connection, transaction, playerId, day);
        return new TavernData
        {
            honey = honey,
            quests = quests.ToArray(),
            completedCount = completed,
            completedBonusPoints = completedPoints,
            // Campi legacy: restano valorizzati durante la transizione dei client/admin.
            questsRequiredForBonus = Math.Min(5, quests.Count),
            bonusPointsRequired = requiredPoints,
            bonusHoneyReward = AllQuestsHoneyReward,
            bonusAvailable = requiredPoints > 0 && completedPoints >= requiredPoints,
            bonusClaimed = bonusClaimed,
            secondsToRefresh = SecondsToRefresh()
        };
    }

    /// <summary>
    /// Riscuote una quest completata. Idempotente: una seconda chiamata non paga due volte.
    /// Ritorna un messaggio d'errore se non e' riscuotibile, altrimenti null.
    /// </summary>
    public static string ClaimQuest(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string questId,
        int rewardMultiplier = 1)
    {
        string day = TodayKey();
        AssignIfMissing(connection, transaction, playerId, day);

        string normalized = Normalize(questId);
        if (!TryFindQuest(normalized, out Quest quest))
            return "Quest non valida.";

        // Basta la riga, non serve che la quest sia ancora nell'estrazione di oggi: dopo un
        // deploy a giornata iniziata la bacheca mostra la nuova, ma chi aveva gia' finito una
        // quest della vecchia l'aveva finita davvero e il suo miele lo ha guadagnato. Non
        // apre buchi: il premio di giornata conta solo le dieci in bacheca.
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

        GrantHoney(connection, transaction, playerId, HoneyRewardFor(quest.Difficulty) * rewardMultiplier);
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
            return $"Ottieni {state.bonusPointsRequired} punti quest per il premio.";

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
    /// Le dieci quest del giorno. Stabili per data: non usa GetHashCode perche' non e'
    /// garantito costante tra avvii e un riavvio del server cambierebbe le quest a meta'
    /// giornata. Niente due quest sullo stesso contatore, altrimenti "uccidi 10" e "uccidi 20"
    /// si completerebbero insieme e la giornata varrebbe la meta'.
    ///
    /// Le quote sono 5 facili, 3 intermedie e 2 avanzate. Un contatore compare una sola
    /// volta, quindi due soglie dello stesso obiettivo non avanzano insieme, e le quest
    /// d'arena hanno un tetto proprio (<see cref="PvpQuestsPerDay"/>) perche' dipendono da
    /// un avversario e non devono poter monopolizzare i punti del premio.
    /// </summary>
    private static List<Quest> SelectForDay(string day)
    {
        var seed = new DailySequence(day);
        var picked = new List<Quest>();
        var usedCounters = new HashSet<string>();
        int pvpPicked = 0;

        Quest[] all = CoreCatalog.Concat(AdvancedCatalog).Concat(PvpCatalog).ToArray();
        Take(TavernQuestDifficulty.Easy, EasyQuestsPerDay);
        Take(TavernQuestDifficulty.Intermediate, EasyQuestsPerDay + IntermediateQuestsPerDay);
        Take(TavernQuestDifficulty.Advanced, QuestsPerDay);
        return picked;

        void Take(TavernQuestDifficulty difficulty, int upTo)
        {
            Quest[] catalog = all.Where(quest => quest.Difficulty == difficulty).ToArray();
            foreach (int index in seed.Shuffle(catalog.Length))
            {
                if (picked.Count >= upTo)
                    return;
                Quest quest = catalog[index];
                bool pvp = PoolOf(quest) == PoolPvp;
                if (pvp && pvpPicked >= PvpQuestsPerDay)
                    continue;
                if (!usedCounters.Add(quest.CounterKey))
                    continue;
                picked.Add(quest);
                if (pvp)
                    pvpPicked++;
            }
        }
    }

    private static int BonusPointsFor(TavernQuestDifficulty difficulty) => difficulty switch
    {
        TavernQuestDifficulty.Intermediate => 2,
        TavernQuestDifficulty.Advanced => 3,
        _ => 1
    };

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

    /// <summary>
    /// Baseline e riscossione di ogni quest assegnata al giocatore per la giornata, per id.
    /// Puo' contenerne piu' di dieci: un deploy a giornata iniziata lascia a database anche
    /// l'estrazione precedente. E' <see cref="ReadOrAssign"/> a scegliere quali mostrare.
    /// </summary>
    private static Dictionary<string, (int Baseline, bool Claimed)> ReadRows(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string day)
    {
        var rows = new Dictionary<string, (int, bool)>(StringComparer.Ordinal);
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
            rows[reader.GetString(0)] = (reader.GetInt32(1), !reader.IsDBNull(2));
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
