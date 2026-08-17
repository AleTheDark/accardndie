using AccardND.Server.Accounts;
using AccardND.Server.Data;
using AccardND.Server.Progression;
using AccardND.Server.Sessions;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Admin;

/// <summary>
/// Strato dati del pannello admin: query di sola lettura per le dashboard e un
/// insieme ristretto di azioni di scrittura "curate" (rinomina, elimina,
/// imposta miele, reset progressi). Tutte le date sono ISO 8601 UTC: il confronto
/// lessicografico sulle stringhe coincide con l'ordine cronologico.
/// </summary>
public sealed class AdminService
{
    private readonly AccardDatabase database;
    private readonly PresenceRegistry presence;
    private readonly SeasonService seasons;
    private readonly RankedService ranked;
    private readonly AccountEraser eraser;

    public AdminService(
        AccardDatabase database, PresenceRegistry presence, SeasonService seasons, RankedService ranked,
        AccountEraser eraser)
    {
        this.database = database;
        this.presence = presence;
        this.seasons = seasons;
        this.ranked = ranked;
        this.eraser = eraser;
    }

    /// <summary>
    /// Etichette leggibili dei contatori di campagna. Le chiavi sconosciute (contatori nuovi
    /// non ancora mappati) vengono mostrate cosi' come sono invece di sparire dal pannello.
    /// </summary>
    private static readonly Dictionary<string, string> CounterLabels = new()
    {
        [CampaignCounters.EnemiesDefeated] = "Nemici sconfitti",
        [CampaignCounters.RoomsCleared] = "Stanze superate",
        [CampaignCounters.RunsEnded] = "Run concluse",
        [CampaignCounters.BossesDefeated] = "Boss di capitolo",
        [CampaignCounters.MinibossesDefeated] = "Miniboss sconfitti",
        [CampaignCounters.DiceRolled] = "Dadi tirati",
        [CampaignCounters.AbilitiesUsed] = "Abilità di classe usate",
        [CampaignCounters.ItemsUsed] = "Oggetti usati",
        [CampaignCounters.ExperienceEarned] = "Esperienza guadagnata",
        [CampaignCounters.SupremesUsed] = "Supreme attivate",
        [CampaignCounters.QuickChallenges] = "Sfide veloci completate",
        [CampaignCounters.MerchantPurchases] = "Affari col mercante",
        [CampaignCounters.GoldEarned] = "Oro guadagnato",
        [CampaignCounters.LevelsGained] = "Livelli di run guadagnati",
        [CampaignCounters.DailyCompleted] = "Giornate di taverna complete",
        ["boss_bragus"] = "Bragus sconfitto",
        ["boss_trentor"] = "Trentor sconfitto",
        ["boss_medusa"] = "Medusa sconfitta",
        ["boss_palatir"] = "Palatir sconfitto",
        ["miniboss_golem"] = "Golem sconfitto"
    };

    // ---- Overview -----------------------------------------------------------

    public object GetOverview()
    {
        using SqliteConnection connection = database.Open();
        string now = DateTime.UtcNow.ToString("O");
        string since24h = DateTime.UtcNow.AddHours(-24).ToString("O");
        string since7d = DateTime.UtcNow.AddDays(-7).ToString("O");
        string since30d = DateTime.UtcNow.AddDays(-30).ToString("O");

        int totalAccounts = ScalarInt(connection, "SELECT COUNT(*) FROM accounts");
        int newAccounts7d = ScalarInt(connection,
            "SELECT COUNT(*) FROM accounts WHERE created_at >= $s", ("$s", since7d));
        int newAccounts30d = ScalarInt(connection,
            "SELECT COUNT(*) FROM accounts WHERE created_at >= $s", ("$s", since30d));

        int logins24h = ScalarInt(connection,
            "SELECT COUNT(*) FROM login_events WHERE occurred_at >= $s", ("$s", since24h));
        int logins7d = ScalarInt(connection,
            "SELECT COUNT(*) FROM login_events WHERE occurred_at >= $s", ("$s", since7d));
        int activePlayers24h = ScalarInt(connection,
            "SELECT COUNT(DISTINCT player_id) FROM login_events WHERE occurred_at >= $s", ("$s", since24h));
        int activePlayers7d = ScalarInt(connection,
            "SELECT COUNT(DISTINCT player_id) FROM login_events WHERE occurred_at >= $s", ("$s", since7d));

        int totalMatches = ScalarInt(connection, "SELECT COUNT(*) FROM match_history");
        int rankedMatches = ScalarInt(connection, "SELECT COUNT(*) FROM match_history WHERE ranked = 1");
        int matches24h = ScalarInt(connection,
            "SELECT COUNT(*) FROM match_history WHERE ended_at >= $s", ("$s", since24h));
        int matches7d = ScalarInt(connection,
            "SELECT COUNT(*) FROM match_history WHERE ended_at >= $s", ("$s", since7d));

        // "Concluse" e "iniziate" sono due conti diversi da quando la riga della run nasce
        // all'avvio: chi molla a meta' non compare fra le prime e va cercato fra le seconde.
        int totalCampaignRuns = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at IS NOT NULL");
        int campaign24h = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at >= $s", ("$s", since24h));
        int campaign7d = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at >= $s", ("$s", since7d));
        int startedRuns24h = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE started_at >= $s", ("$s", since24h));
        int startedRuns7d = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE started_at >= $s", ("$s", since7d));
        int openRuns24h = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE started_at >= $s AND ended_at IS NULL",
            ("$s", since24h));
        int openRunsTotal = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at IS NULL");

        var accountsBySource = new List<object>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            // Gli account 'external' arrivano tutti da un token UGS: quello che
            // interessa e' il metodo dietro al token (Google o ospite anonimo).
            command.CommandText = @"
                SELECT a.source,
                       (SELECT ei.auth_method FROM external_identities ei
                        WHERE ei.player_id = a.player_id
                        ORDER BY ei.last_login_at DESC LIMIT 1) AS auth_method,
                       COUNT(*)
                FROM accounts a
                GROUP BY a.source, auth_method
                ORDER BY COUNT(*) DESC";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                accountsBySource.Add(new
                {
                    source = reader.GetString(0),
                    authMethod = NullableString(reader, 1),
                    count = reader.GetInt32(2)
                });
        }

        string activeSeason = ScalarString(connection,
            "SELECT name FROM seasons WHERE is_active = 1 ORDER BY season_id DESC LIMIT 1");

        return new
        {
            generatedAt = now,
            onlineNow = presence.OnlineCount,
            totalAccounts,
            newAccounts7d,
            newAccounts30d,
            accountsBySource,
            logins24h,
            logins7d,
            activePlayers24h,
            activePlayers7d,
            totalMatches,
            rankedMatches,
            matches24h,
            matches7d,
            totalCampaignRuns,
            campaignRuns24h = campaign24h,
            campaignRuns7d = campaign7d,
            startedRuns24h,
            startedRuns7d,
            openRuns24h,
            openRunsTotal,
            activeSeason
        };
    }

    // ---- Serie temporali (bucket giornalieri) -------------------------------

    public object GetTimeseries(int days)
    {
        days = Math.Clamp(days, 1, 365);
        string since = DateTime.UtcNow.Date.AddDays(-(days - 1)).ToString("O");

        using SqliteConnection connection = database.Open();
        Dictionary<string, int> logins = DailyCounts(connection, "login_events", "occurred_at", since);
        Dictionary<string, int> signups = DailyCounts(connection, "accounts", "created_at", since);
        Dictionary<string, int> matches = DailyCounts(connection, "match_history", "ended_at", since);
        Dictionary<string, int> campaign = DailyCounts(connection, "campaign_runs", "ended_at", since);
        Dictionary<string, int> campaignStarted = DailyCounts(connection, "campaign_runs", "started_at", since);

        var points = new List<object>();
        DateTime start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        for (int i = 0; i < days; i++)
        {
            string day = start.AddDays(i).ToString("yyyy-MM-dd");
            points.Add(new
            {
                date = day,
                logins = logins.GetValueOrDefault(day),
                signups = signups.GetValueOrDefault(day),
                matches = matches.GetValueOrDefault(day),
                campaign = campaign.GetValueOrDefault(day),
                campaignStarted = campaignStarted.GetValueOrDefault(day)
            });
        }
        return new { days, points };
    }

    private static Dictionary<string, int> DailyCounts(
        SqliteConnection connection, string table, string column, string since)
    {
        var result = new Dictionary<string, int>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT substr({column}, 1, 10) AS day, COUNT(*) FROM {table} " +
            $"WHERE {column} >= $since GROUP BY day";
        command.Parameters.AddWithValue("$since", since);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                result[reader.GetString(0)] = reader.GetInt32(1);
        }
        return result;
    }

    // ---- Retention (coorti per giorno di registrazione) ----------------------

    /// <summary>
    /// Quanti giocatori tornano dopo essersi registrati, per coorte: una coorte e'
    /// l'insieme degli account creati in un giorno UTC, e il giocatore "torna al
    /// giorno N" se in <c>login_events</c> c'e' un accesso datato esattamente N giorni
    /// dopo la registrazione. Il giorno preciso, non "entro N giorni": e' la
    /// definizione con cui sono scritti i benchmark del settore, e l'altra e' piu'
    /// generosa di qualche punto.
    ///
    /// La misura vive su <c>login_events</c>, che ha una riga per autenticazione
    /// riuscita: il token di sessione del client sta solo in memoria, quindi ogni
    /// avvio dell'app produce un login. Una riconnessione a meta' sessione no, ed e'
    /// giusto cosi' - conta il giorno, non quante volte.
    /// </summary>
    public object GetRetention(int days)
    {
        days = Math.Clamp(days, 7, 365);
        DateTime today = DateTime.UtcNow.Date;
        string from = today.AddDays(-(days - 1)).ToString("yyyy-MM-dd");

        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        // Il giorno si ricava con substr invece che con date(): created_at e'
        // un round-trip "O" con sette decimali di secondo, e il parser di SQLite
        // non e' un posto dove valga la pena scoprire quanti ne accetta.
        command.CommandText = @"
            SELECT substr(a.created_at, 1, 10) AS day,
                   COUNT(*) AS cohort,
                   SUM(EXISTS (SELECT 1 FROM login_events e
                               WHERE e.player_id = a.player_id
                                 AND substr(e.occurred_at, 1, 10)
                                     = date(substr(a.created_at, 1, 10), '+1 day'))) AS d1,
                   SUM(EXISTS (SELECT 1 FROM login_events e
                               WHERE e.player_id = a.player_id
                                 AND substr(e.occurred_at, 1, 10)
                                     = date(substr(a.created_at, 1, 10), '+7 day'))) AS d7,
                   SUM(EXISTS (SELECT 1 FROM login_events e
                               WHERE e.player_id = a.player_id
                                 AND substr(e.occurred_at, 1, 10)
                                     = date(substr(a.created_at, 1, 10), '+30 day'))) AS d30
            FROM accounts a
            WHERE substr(a.created_at, 1, 10) >= $from
            GROUP BY day
            ORDER BY day DESC";
        command.Parameters.AddWithValue("$from", from);

        var cohorts = new List<object>();
        int accounts = 0;
        int[] returned = new int[3];
        int[] measured = new int[3];
        int[] offsets = { 1, 7, 30 };

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string day = reader.GetString(0);
                int cohort = reader.GetInt32(1);
                accounts += cohort;

                // Una coorte e' matura per il giorno N solo quando quel giorno e'
                // finito. Contare come "non tornato" chi si e' registrato ieri e non
                // ha ancora avuto il suo settimo giorno e' l'errore che schiaccia
                // verso il basso qualsiasi media di retention: quelle coorti restano
                // in tabella, ma con il valore a null.
                var values = new int?[3];
                for (int i = 0; i < offsets.Length; i++)
                {
                    if (!DateTime.TryParse(day, out DateTime cohortDay)
                        || cohortDay.AddDays(offsets[i]) >= today)
                        continue;
                    int back = reader.GetInt32(2 + i);
                    values[i] = back;
                    returned[i] += back;
                    measured[i] += cohort;
                }

                cohorts.Add(new
                {
                    day,
                    cohort,
                    d1 = values[0],
                    d7 = values[1],
                    d30 = values[2]
                });
            }
        }

        return new
        {
            generatedAt = DateTime.UtcNow.ToString("O"),
            days,
            accounts,
            cohorts,
            // Media pesata sulle sole coorti mature: il conteggio dei tornati e
            // quello degli account su cui e' calcolato viaggiano insieme, cosi' il
            // pannello puo' scrivere "12 su 48" e non solo una percentuale.
            summary = new
            {
                d1 = returned[0], d1Of = measured[0],
                d7 = returned[1], d7Of = measured[1],
                d30 = returned[2], d30Of = measured[2]
            }
        };
    }

    // ---- Lista giocatori ----------------------------------------------------

    /// <summary>La ricerca copre anche la mail dell'identita' Google: e' il modo
    /// piu' diretto per accorgersi che due account sono la stessa persona.</summary>
    private const string PlayerSearchFilter = @"
        a.username_ci LIKE $q OR lower(a.player_id) LIKE $q
        OR EXISTS (SELECT 1 FROM external_identities ei
                   WHERE ei.player_id = a.player_id AND lower(ei.email) LIKE $q)";

    /// <summary>
    /// Colonne ordinabili dalla tabella giocatori. E' una whitelist: la chiave
    /// arriva dal client, il pezzo di SQL no, quindi nessun input finisce mai
    /// dentro la query. Una chiave sconosciuta ricade sull'ordinamento di default.
    /// </summary>
    private static readonly Dictionary<string, string> PlayerSortColumns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "a.username_ci",
            ["source"] = "a.source",
            // Il livello da solo non basta: a parita' di livello conta l'esperienza.
            ["level"] = "account_level, account_experience",
            ["exp"] = "account_total_experience",
            ["honey"] = "honey",
            ["matches"] = "matches",
            ["wins"] = "wins",
            ["losses"] = "losses",
            ["created"] = "a.created_at",
            ["lastLogin"] = "a.last_login_at"
        };

    private static string BuildPlayerOrderBy(string sort, bool descending)
    {
        if (!PlayerSortColumns.TryGetValue(sort ?? string.Empty, out string columns))
        {
            // Default: ultimi accessi in cima, chi non ha mai fatto login in fondo.
            return "(a.last_login_at IS NULL), a.last_login_at DESC";
        }

        string direction = descending ? "DESC" : "ASC";
        string ordered = string.Join(", ",
            columns.Split(',').Select(column => column.Trim() + " " + direction));
        // Tiebreak stabile: senza, due righe uguali possono scambiarsi di posto
        // tra una pagina e l'altra.
        return ordered + ", a.player_id ASC";
    }

    public object GetPlayers(string search, int limit, int offset, string sort = null, bool descending = true)
    {
        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);
        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        string like = hasSearch ? "%" + search.Trim().ToLowerInvariant() + "%" : null;

        using SqliteConnection connection = database.Open();

        int total;
        using (SqliteCommand count = connection.CreateCommand())
        {
            count.CommandText = hasSearch
                ? "SELECT COUNT(*) FROM accounts a WHERE " + PlayerSearchFilter
                : "SELECT COUNT(*) FROM accounts";
            if (hasSearch) count.Parameters.AddWithValue("$q", like);
            total = Convert.ToInt32(count.ExecuteScalar());
        }

        var rows = new List<object>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT a.player_id, a.username, a.source, a.created_at, a.last_login_at,
                       COALESCE(sp.honey, 0) AS honey,
                       COALESCE(st.matches, 0) AS matches,
                       COALESCE(st.wins, 0) AS wins,
                       (SELECT ei.auth_method FROM external_identities ei
                        WHERE ei.player_id = a.player_id
                        ORDER BY ei.last_login_at DESC LIMIT 1) AS auth_method,
                       (SELECT ei.email FROM external_identities ei
                        WHERE ei.player_id = a.player_id AND ei.email IS NOT NULL
                        ORDER BY ei.last_login_at DESC LIMIT 1) AS email,
                       COALESCE(sp.account_level, 1) AS account_level,
                       COALESCE(sp.account_experience, 0) AS account_experience,
                       -- La soglia si ricalcola dal livello invece di leggere la colonna: le
                       -- righe scritte prima della curva hanno tutte 100, e la lista
                       -- mostrerebbe un denominatore sbagliato fino al primo level-up.
                       (100 + 25 * (COALESCE(sp.account_level, 1) - 1)) AS account_experience_to_next,
                       COALESCE(sp.account_total_experience, 0) AS account_total_experience,
                       COALESCE(sp.talent_points, 0) AS talent_points,
                       COALESCE(st.losses, 0) AS losses,
                       n.nickname AS nickname
                FROM accounts a
                LEFT JOIN single_player_progress sp ON sp.player_id = a.player_id
                LEFT JOIN account_nicknames n ON n.player_id = a.player_id
                LEFT JOIN player_stats st ON st.player_id = a.player_id AND st.scope = 'lifetime'
                " + (hasSearch ? "WHERE " + PlayerSearchFilter + " " : "") + @"
                ORDER BY " + BuildPlayerOrderBy(sort, descending) + @"
                LIMIT $limit OFFSET $offset";
            if (hasSearch) command.Parameters.AddWithValue("$q", like);
            command.Parameters.AddWithValue("$limit", limit);
            command.Parameters.AddWithValue("$offset", offset);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new
                {
                    playerId = reader.GetString(0),
                    username = reader.GetString(1),
                    source = reader.GetString(2),
                    createdAt = NullableString(reader, 3),
                    lastLoginAt = NullableString(reader, 4),
                    honey = reader.GetInt32(5),
                    matches = reader.GetInt32(6),
                    wins = reader.GetInt32(7),
                    authMethod = NullableString(reader, 8),
                    email = NullableString(reader, 9),
                    accountLevel = reader.GetInt32(10),
                    accountExperience = reader.GetInt32(11),
                    accountExperienceToNextLevel = reader.GetInt32(12),
                    accountTotalExperience = reader.GetInt32(13),
                    talentPoints = reader.GetInt32(14),
                    losses = reader.GetInt32(15),
                    nickname = NullableString(reader, 16),
                    online = presence.IsOnline(reader.GetString(0))
                });
            }
        }

        return new { total, limit, offset, players = rows };
    }

    // ---- Dettaglio giocatore ------------------------------------------------

    public object GetPlayerDetail(string playerId)
    {
        using SqliteConnection connection = database.Open();

        object account = null;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT a.player_id, a.username, a.source, a.created_at, a.last_login_at,
                       COALESCE(sp.honey, 0), COALESCE(sp.tutorial_completed, 0),
                       COALESCE(sp.hardcore_unlocked, 0),
                       COALESCE(sp.account_level, 1), COALESCE(sp.account_experience, 0),
                       (100 + 25 * (COALESCE(sp.account_level, 1) - 1)),
                       COALESCE(sp.account_total_experience, 0),
                       COALESCE(sp.talent_points, 0), COALESCE(sp.talent_points_earned, 0),
                       sp.updated_at, p.selected_icon_id, p.bio, n.nickname,
                       (SELECT ei.auth_method FROM external_identities ei
                        WHERE ei.player_id = a.player_id
                        ORDER BY ei.last_login_at DESC LIMIT 1) AS auth_method,
                       (SELECT ei.email FROM external_identities ei
                        WHERE ei.player_id = a.player_id AND ei.email IS NOT NULL
                        ORDER BY ei.last_login_at DESC LIMIT 1) AS email
                FROM accounts a
                LEFT JOIN single_player_progress sp ON sp.player_id = a.player_id
                LEFT JOIN profiles p ON p.player_id = a.player_id
                LEFT JOIN account_nicknames n ON n.player_id = a.player_id
                WHERE a.player_id = $id";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            account = new
            {
                playerId = reader.GetString(0),
                username = reader.GetString(1),
                source = reader.GetString(2),
                createdAt = NullableString(reader, 3),
                lastLoginAt = NullableString(reader, 4),
                honey = reader.GetInt32(5),
                tutorialCompleted = reader.GetInt32(6) != 0,
                hardcoreUnlocked = reader.GetInt32(7) != 0,
                accountLevel = reader.GetInt32(8),
                accountExperience = reader.GetInt32(9),
                accountExperienceToNextLevel = reader.GetInt32(10),
                accountTotalExperience = reader.GetInt32(11),
                talentPoints = reader.GetInt32(12),
                talentPointsEarned = reader.GetInt32(13),
                progressUpdatedAt = NullableString(reader, 14),
                selectedIconId = NullableString(reader, 15),
                bio = NullableString(reader, 16),
                nickname = NullableString(reader, 17),
                authMethod = NullableString(reader, 18),
                email = NullableString(reader, 19),
                online = presence.IsOnline(playerId)
            };
        }

        object lifetime = ReadStatsScope(connection, playerId, "lifetime");
        object season = ReadStatsScope(connection, playerId, seasons.ActiveSeasonScope);

        var recentMatches = new List<object>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT m.match_id, m.ended_at, m.ranked, m.player_a, m.player_b,
                       m.winner, m.score_a, m.score_b, m.ended_reason,
                       aa.username AS name_a, bb.username AS name_b
                FROM match_history m
                LEFT JOIN accounts aa ON aa.player_id = m.player_a
                LEFT JOIN accounts bb ON bb.player_id = m.player_b
                WHERE m.player_a = $id OR m.player_b = $id
                ORDER BY m.ended_at DESC LIMIT 20";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                recentMatches.Add(ReadMatchRow(reader, playerId));
        }

        var recentRuns = new List<object>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            // Ordinate sull'ultimo momento noto della run: le abbandonate non hanno una
            // fine, e ordinarle su ended_at le spedirebbe tutte in coda.
            command.CommandText = @"
                SELECT ended_at, mode, chapter_id, stage_id, rooms_cleared,
                       enemies_defeated, bosses_defeated, honey_reward, started_at
                FROM campaign_runs WHERE player_id = $id
                ORDER BY COALESCE(ended_at, started_at) DESC LIMIT 20";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                recentRuns.Add(new
                {
                    endedAt = NullableString(reader, 0),
                    mode = NullableString(reader, 1),
                    chapterId = NullableString(reader, 2),
                    stageId = NullableString(reader, 3),
                    roomsCleared = reader.GetInt32(4),
                    enemiesDefeated = reader.GetInt32(5),
                    bossesDefeated = reader.GetInt32(6),
                    honeyReward = reader.GetInt32(7),
                    startedAt = NullableString(reader, 8)
                });
        }

        var recentLogins = new List<object>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT occurred_at, provider FROM login_events
                WHERE player_id = $id ORDER BY occurred_at DESC LIMIT 20";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                recentLogins.Add(new { occurredAt = reader.GetString(0), provider = reader.GetString(1) });
        }

        var unlocks = new List<object>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT unlock_type, unlock_id, unlocked_at FROM single_player_unlocks
                WHERE player_id = $id ORDER BY unlocked_at";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string type = reader.GetString(0);
                string id = reader.GetString(1);
                unlocks.Add(new
                {
                    type,
                    id,
                    name = SanctuaryCatalog.TryGetEntry(type, id, out SanctuaryCatalog.Entry entry)
                        ? entry.Name
                        : id,
                    unlockedAt = NullableString(reader, 2)
                });
            }
        }

        Dictionary<string, int> counterValues = ReadCounterValues(connection, playerId);

        return new
        {
            account,
            lifetime,
            season,
            friendly = ReadFriendlyMatches(connection, playerId),
            seasonName = seasons.ActiveSeasonName,
            ranked = ReadRankedState(connection, playerId),
            campaignTotals = ReadCampaignTotals(connection, playerId),
            counters = DescribeCounters(counterValues),
            tavern = ReadTavernState(connection, playerId, counterValues),
            rewards = ReadRewardClaims(connection, playerId),
            stash = BuildStashState(connection, playerId),
            achievements = ReadAchievements(connection, playerId),
            icons = ReadIcons(connection, playerId),
            campaignKills = ReadCampaignKills(connection, playerId),
            friends = ReadFriendCounts(connection, playerId),
            hallOfFame = ReadHallOfFame(connection, playerId),
            recentMatches,
            recentRuns,
            recentLogins,
            unlocks,
            unlockables = BuildUnlockState(connection, playerId)
        };
    }

    // ---- Letture di dettaglio -----------------------------------------------

    private static object ReadStatsScope(SqliteConnection connection, string playerId, string scope)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT matches, wins, losses, forfeits, rounds_won, rounds_lost,
                   best_streak, current_streak, total_match_seconds
            FROM player_stats WHERE player_id = $id AND scope = $scope";
        command.Parameters.AddWithValue("$id", playerId);
        command.Parameters.AddWithValue("$scope", scope);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        int matches = reader.GetInt32(0);
        int wins = reader.GetInt32(1);
        return new
        {
            matches,
            wins,
            losses = reader.GetInt32(2),
            forfeits = reader.GetInt32(3),
            roundsWon = reader.GetInt32(4),
            roundsLost = reader.GetInt32(5),
            bestStreak = reader.GetInt32(6),
            currentStreak = reader.GetInt32(7),
            totalMatchSeconds = reader.GetInt32(8),
            winRatePercent = matches > 0 ? (int)Math.Round(wins * 100.0 / matches) : 0
        };
    }

    /// <summary>
    /// Le amichevoli in stanza, contate dallo storico. Sono l'unico posto in cui restano:
    /// non toccano player_stats ne' la ladder, quindi qui il pannello e' il solo modo di
    /// sapere che sono state giocate. Null se non ce ne sono.
    /// </summary>
    private static object ReadFriendlyMatches(SqliteConnection connection, string playerId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*),
                   SUM(CASE WHEN (player_a = $id AND winner = 0)
                              OR (player_b = $id AND winner = 1) THEN 1 ELSE 0 END),
                   SUM(CASE WHEN (player_a = $id AND winner = 1)
                              OR (player_b = $id AND winner = 0) THEN 1 ELSE 0 END),
                   MAX(ended_at)
            FROM match_history
            WHERE ranked = 0 AND (player_a = $id OR player_b = $id)";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        int matches = reader.GetInt32(0);
        if (matches == 0)
            return null;
        return new
        {
            matches,
            wins = reader.GetInt32(1),
            losses = reader.GetInt32(2),
            lastAt = NullableString(reader, 3)
        };
    }

    /// <summary>
    /// Stato ranked della stagione attiva, con il tier tradotto dall'MMR nascosto e la
    /// posizione in classifica. Null se il giocatore non ha ancora giocato in ranked.
    /// </summary>
    private object ReadRankedState(SqliteConnection connection, string playerId)
    {
        int seasonId = seasons.ActiveSeasonId;
        int mmr, games, peak;
        bool placementDone;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT mmr, games_played, placement_done, peak_mmr
                FROM ranked_state WHERE player_id = $id AND season_id = $season";
            command.Parameters.AddWithValue("$id", playerId);
            command.Parameters.AddWithValue("$season", seasonId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            mmr = reader.GetInt32(0);
            games = reader.GetInt32(1);
            placementDone = reader.GetInt32(2) != 0;
            peak = reader.GetInt32(3);
        }

        RankedTierInfo tier = ranked.Describe(mmr);
        (int rank, int players) = ranked.GetGlobalStanding(playerId, seasonId);
        return new
        {
            seasonId,
            seasonName = seasons.ActiveSeasonName,
            mmr,
            peakMmr = peak,
            gamesPlayed = games,
            placementDone,
            tier = tier.TierName,
            division = tier.Division,
            leaguePoints = tier.LeaguePoints,
            rank,
            players
        };
    }

    private static object ReadCampaignTotals(SqliteConnection connection, string playerId)
    {
        using SqliteCommand command = connection.CreateCommand();
        // COUNT(ended_at) conta le run chiuse: le righe aperte all'avvio e mai concluse
        // stanno nella colonna a parte, non gonfiano il totale delle run giocate fino in
        // fondo. Le somme non ne risentono: una run aperta ha ancora tutti i contatori a zero.
        command.CommandText = @"
            SELECT COUNT(ended_at), COALESCE(SUM(rooms_cleared), 0), COALESCE(SUM(enemies_defeated), 0),
                   COALESCE(SUM(bosses_defeated), 0), COALESCE(SUM(minibosses_defeated), 0),
                   COALESCE(SUM(honey_reward), 0), MIN(ended_at), MAX(ended_at),
                   COUNT(started_at), SUM(CASE WHEN ended_at IS NULL THEN 1 ELSE 0 END),
                   MAX(COALESCE(ended_at, started_at))
            FROM campaign_runs WHERE player_id = $id";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new
        {
            runs = reader.GetInt32(0),
            roomsCleared = reader.GetInt32(1),
            enemiesDefeated = reader.GetInt32(2),
            bossesDefeated = reader.GetInt32(3),
            minibossesDefeated = reader.GetInt32(4),
            honeyReward = reader.GetInt32(5),
            firstRunAt = NullableString(reader, 6),
            lastRunAt = NullableString(reader, 7),
            startedRuns = reader.GetInt32(8),
            openRuns = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
            lastActivityAt = NullableString(reader, 10)
        };
    }

    private static Dictionary<string, int> ReadCounterValues(SqliteConnection connection, string playerId)
    {
        var values = new Dictionary<string, int>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT counter_key, value FROM player_counters WHERE player_id = $id ORDER BY counter_key";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            values[reader.GetString(0)] = reader.GetInt32(1);
        return values;
    }

    /// <summary>
    /// Contatori con etichetta. Quelli mappati vengono elencati per primi nell'ordine del
    /// catalogo, cosi' la lista non cambia forma quando un giocatore sblocca qualcosa di nuovo.
    /// </summary>
    private static List<object> DescribeCounters(Dictionary<string, int> values)
    {
        var rows = new List<object>();
        foreach ((string key, string label) in CounterLabels)
        {
            if (values.TryGetValue(key, out int value))
                rows.Add(new { key, label, value });
        }
        foreach ((string key, int value) in values)
        {
            if (!CounterLabels.ContainsKey(key))
                rows.Add(new { key, label = key, value });
        }
        return rows;
    }

    /// <summary>
    /// Bacheca di oggi del giocatore. Di sola lettura: a differenza del gioco non assegna le
    /// quest mancanti, altrimenti aprire una scheda dal pannello fisserebbe il baseline della
    /// giornata al posto del giocatore.
    /// </summary>
    private static object ReadTavernState(
        SqliteConnection connection, string playerId, Dictionary<string, int> counters)
    {
        string day = TavernQuests.TodayKey();
        // Un deploy a giornata iniziata lascia a database anche l'estrazione precedente: in
        // gioco quelle righe non si vedono, qui si', perche' questa e' la vista di cosa c'e'
        // davvero. Vanno pero' marcate, altrimenti un admin che confronta pannello e gioco
        // trova due numeri diversi e pensa a un bug.
        var onBoard = TavernQuests.DefinitionsForDay(day)
            .Select(quest => quest.Id)
            .ToHashSet(StringComparer.Ordinal);
        var quests = new List<object>();
        int completed = 0;
        int completedPoints = 0;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT quest_id, baseline, claimed_at FROM player_tavern_quests
                WHERE player_id = $id AND day = $day ORDER BY rowid";
            command.Parameters.AddWithValue("$id", playerId);
            command.Parameters.AddWithValue("$day", day);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string questId = reader.GetString(0);
                int baseline = reader.GetInt32(1);
                bool claimed = !reader.IsDBNull(2);
                if (!TavernQuests.TryDescribe(questId, out TavernQuests.QuestDefinition definition))
                {
                    quests.Add(new { questId, title = questId, description = "(non piu' in catalogo)",
                        current = 0, threshold = 0, completed = false, claimed, onBoard = false,
                        claimedAt = NullableString(reader, 2) });
                    continue;
                }

                int gained = Math.Max(0, counters.GetValueOrDefault(definition.CounterKey) - baseline);
                bool isCompleted = gained >= definition.Threshold;
                bool isOnBoard = onBoard.Contains(definition.Id);
                // Il conteggio segue la bacheca che il giocatore vede, non le righe: e' quello
                // che decide il premio di giornata.
                if (isCompleted && isOnBoard)
                {
                    completed++;
                    completedPoints += definition.BonusPoints;
                }
                quests.Add(new
                {
                    questId = definition.Id,
                    title = definition.Title,
                    description = definition.Description,
                    current = Math.Min(gained, definition.Threshold),
                    threshold = definition.Threshold,
                    completed = isCompleted,
                    claimed,
                    onBoard = isOnBoard,
                    claimedAt = NullableString(reader, 2)
                });
            }
        }

        bool bonusClaimed = ScalarInt(connection,
            "SELECT COUNT(*) FROM player_tavern_bonus WHERE player_id = $id AND day = $day",
            ("$id", playerId), ("$day", day)) > 0;
        int claimsAllTime = ScalarInt(connection,
            "SELECT COUNT(*) FROM player_tavern_quests WHERE player_id = $id AND claimed_at IS NOT NULL",
            ("$id", playerId));
        int bonusesAllTime = ScalarInt(connection,
            "SELECT COUNT(*) FROM player_tavern_bonus WHERE player_id = $id", ("$id", playerId));

        return new
        {
            day,
            quests,
            completedCount = completed,
            completedBonusPoints = completedPoints,
            bonusPointsRequired = TavernQuests.BonusPointsRequired,
            bonusClaimed,
            // Il premio va a punti, non a quest completate: chiedere tutta la bacheca
            // mostrerebbe "non disponibile" a un giocatore che invece puo' gia' riscuoterlo.
            bonusAvailable = completedPoints >= TavernQuests.BonusPointsRequired,
            claimsAllTime,
            bonusesAllTime,
            honeyFromQuests = claimsAllTime * TavernQuests.QuestHoneyReward
                + bonusesAllTime * TavernQuests.AllQuestsHoneyReward
        };
    }

    /// <summary>
    /// Ricompense riscattate per tipo. L'esperienza triplica con la pubblicita' mentre il
    /// miele triplica: <c>multiplier</c> in tabella e' quello del miele, l'EXP va contata a parte.
    /// </summary>
    private static List<object> ReadRewardClaims(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT reward_type, COUNT(*),
                   COALESCE(SUM(base_honey * multiplier), 0),
                   COALESCE(SUM(base_account_experience * CASE WHEN multiplier > 1 THEN 2 ELSE 1 END), 0),
                   COALESCE(SUM(CASE WHEN multiplier > 1 THEN 1 ELSE 0 END), 0),
                   MAX(created_at)
            FROM single_player_reward_claims WHERE player_id = $id
            GROUP BY reward_type ORDER BY reward_type";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new
            {
                type = reader.GetString(0),
                count = reader.GetInt32(1),
                honey = reader.GetInt32(2),
                experience = reader.GetInt32(3),
                withAd = reader.GetInt32(4),
                lastAt = NullableString(reader, 5)
            });
        return rows;
    }

    private static string ItemName(string itemId) =>
        SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeItem, itemId, out SanctuaryCatalog.Entry entry)
            ? entry.Name
            : itemId;

    private static List<object> ReadAchievements(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pa.achievement_id, ac.name, pa.progress, ac.threshold, pa.unlocked_at
            FROM player_achievements pa
            LEFT JOIN achievements ac ON ac.achievement_id = pa.achievement_id
            WHERE pa.player_id = $id
            ORDER BY (pa.unlocked_at IS NULL), COALESCE(ac.sort_order, 0)";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new
            {
                id = reader.GetString(0),
                name = NullableString(reader, 1) ?? reader.GetString(0),
                progress = reader.GetInt32(2),
                threshold = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                unlockedAt = NullableString(reader, 4)
            });
        return rows;
    }

    private static List<object> ReadIcons(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pi.icon_id, i.name, pi.unlock_source, pi.unlocked_at
            FROM player_icons pi
            LEFT JOIN icons i ON i.icon_id = pi.icon_id
            WHERE pi.player_id = $id ORDER BY pi.unlocked_at";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new
            {
                id = reader.GetString(0),
                name = NullableString(reader, 1) ?? reader.GetString(0),
                source = NullableString(reader, 2),
                unlockedAt = NullableString(reader, 3)
            });
        return rows;
    }

    private static List<object> ReadCampaignKills(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT monster_id, kills, first_killed_at FROM campaign_kills
            WHERE player_id = $id ORDER BY kills DESC, monster_id";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new
            {
                id = reader.GetString(0),
                kills = reader.GetInt32(1),
                firstKilledAt = NullableString(reader, 2)
            });
        return rows;
    }

    private static List<object> ReadFriendCounts(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT status, COUNT(*) FROM friends WHERE owner_id = $id GROUP BY status ORDER BY status";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new { status = reader.GetString(0), count = reader.GetInt32(1) });
        return rows;
    }

    private static List<object> ReadHallOfFame(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT h.season_id, s.name, h.final_rank, h.final_tier, h.final_division,
                   h.final_mmr, h.wins, h.losses
            FROM hall_of_fame h
            LEFT JOIN seasons s ON s.season_id = h.season_id
            WHERE h.player_id = $id ORDER BY h.season_id DESC";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new
            {
                seasonId = reader.GetInt32(0),
                seasonName = NullableString(reader, 1) ?? ("Stagione " + reader.GetInt32(0)),
                rank = reader.GetInt32(2),
                tier = reader.GetString(3),
                division = reader.GetString(4),
                mmr = reader.GetInt32(5),
                wins = reader.GetInt32(6),
                losses = reader.GetInt32(7)
            });
        return rows;
    }

    // ---- Partite recenti ----------------------------------------------------

    public object GetMatches(int limit, int offset)
    {
        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);

        using SqliteConnection connection = database.Open();
        int total = ScalarInt(connection, "SELECT COUNT(*) FROM match_history");

        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT m.match_id, m.ended_at, m.ranked, m.player_a, m.player_b,
                   m.winner, m.score_a, m.score_b, m.ended_reason,
                   aa.username AS name_a, bb.username AS name_b
            FROM match_history m
            LEFT JOIN accounts aa ON aa.player_id = m.player_a
            LEFT JOIN accounts bb ON bb.player_id = m.player_b
            ORDER BY m.ended_at DESC LIMIT $limit OFFSET $offset";
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(ReadMatchRow(reader, null));

        return new { total, limit, offset, matches = rows };
    }

    // ---- Run di campagna ----------------------------------------------------

    /// <summary>
    /// Classifica amministrativa della campagna. Ogni giocatore compare una sola volta con
    /// il proprio avanzamento massimo. Il capitolo ha priorita' sulla stanza: capitolo 2,
    /// stanza 4 precede capitolo 1, stanza 15. Le posizioni a pari record sono uguali.
    /// </summary>
    public object GetCampaignLeaderboard(int limit)
    {
        limit = Math.Clamp(limit, 1, 200);
        using SqliteConnection connection = database.Open();

        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            WITH scored AS (
                SELECT r.*,
                       CASE WHEN r.chapter_id GLOB 'chapter-[0-9]*'
                            THEN CAST(SUBSTR(r.chapter_id, 9) AS INTEGER) ELSE 0 END AS chapter_number
                FROM campaign_runs r
            ), best AS (
                SELECT *, ROW_NUMBER() OVER (
                    PARTITION BY player_id ORDER BY chapter_number DESC, rooms_cleared DESC) AS rn
                FROM scored
            ), totals AS (
                SELECT player_id, COUNT(*) AS runs,
                       MAX(COALESCE(ended_at, started_at)) AS last_run_at
                FROM campaign_runs GROUP BY player_id
            )
            SELECT r.player_id, COALESCE(a.username, r.player_id), r.chapter_number,
                   r.rooms_cleared, t.runs, t.last_run_at
            FROM best r
            JOIN totals t ON t.player_id = r.player_id
            LEFT JOIN accounts a ON a.player_id = r.player_id
            WHERE r.rn = 1
            ORDER BY r.chapter_number DESC, r.rooms_cleared DESC,
                     COALESCE(a.username, r.player_id) COLLATE NOCASE
            LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);

        using SqliteDataReader reader = command.ExecuteReader();
        int ordinal = 0;
        int position = 0;
        (int Chapter, int Room)? previousRecord = null;
        while (reader.Read())
        {
            ordinal++;
            int chapterNumber = reader.GetInt32(2);
            int personalRecord = reader.GetInt32(3);
            var record = (chapterNumber, personalRecord);
            if (previousRecord != record)
                position = ordinal;
            previousRecord = record;

            rows.Add(new
            {
                position,
                playerId = reader.GetString(0),
                username = reader.GetString(1),
                chapterNumber,
                personalRecord,
                runs = reader.GetInt32(4),
                lastRunAt = NullableString(reader, 5)
            });
        }

        return new { limit, players = rows };
    }

    /// <summary>
    /// Storico delle run di campagna, iniziate e concluse. Il filtro accetta 'ended' (solo
    /// le run arrivate a una fine), 'open' (iniziate e mai concluse: gioco chiuso a meta',
    /// crash, run ancora in corso in questo momento) e qualsiasi altro valore per tutte.
    ///
    /// L'ordinamento e' sull'ultimo momento noto della run, non su <c>ended_at</c>: una run
    /// aperta stamattina deve stare in cima insieme alle altre di stamattina, non in fondo
    /// con le righe senza data.
    /// </summary>
    public object GetRuns(string status, int limit, int offset)
    {
        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);
        // Whitelist: dalla query arriva solo la scelta, mai il pezzo di SQL.
        string filter = status switch
        {
            "ended" => "WHERE ended_at IS NOT NULL",
            "open" => "WHERE ended_at IS NULL",
            _ => string.Empty
        };

        using SqliteConnection connection = database.Open();
        int total = ScalarInt(connection, $"SELECT COUNT(*) FROM campaign_runs {filter}");
        int open = ScalarInt(connection, "SELECT COUNT(*) FROM campaign_runs WHERE ended_at IS NULL");
        int ended = ScalarInt(connection, "SELECT COUNT(*) FROM campaign_runs WHERE ended_at IS NOT NULL");

        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT r.run_id, r.player_id, a.username, r.started_at, r.ended_at, r.mode, r.chapter_id,
                   r.stage_id, r.rooms_cleared, r.enemies_defeated, r.bosses_defeated,
                   r.minibosses_defeated, r.honey_reward
            FROM campaign_runs r
            LEFT JOIN accounts a ON a.player_id = r.player_id
            {filter}
            ORDER BY COALESCE(r.ended_at, r.started_at) DESC
            LIMIT $limit OFFSET $offset";
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string startedAt = NullableString(reader, 3);
            string endedAt = NullableString(reader, 4);
            rows.Add(new
            {
                runId = reader.GetInt64(0),
                playerId = reader.GetString(1),
                username = NullableString(reader, 2) ?? reader.GetString(1),
                startedAt,
                endedAt,
                durationSeconds = DurationSeconds(startedAt, endedAt),
                mode = NullableString(reader, 5),
                chapterId = NullableString(reader, 6),
                stageId = NullableString(reader, 7),
                roomsCleared = reader.GetInt32(8),
                enemiesDefeated = reader.GetInt32(9),
                bossesDefeated = reader.GetInt32(10),
                minibossesDefeated = reader.GetInt32(11),
                honeyReward = reader.GetInt32(12)
            });
        }

        return new { total, open, ended, limit, offset, status, runs = rows };
    }

    /// <summary>Elimina esclusivamente la riga di storico identificata dall'id interno.</summary>
    public (bool ok, string error) DeleteCampaignRun(long runId)
    {
        if (runId <= 0)
            return (false, "Run non valida.");

        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM campaign_runs WHERE run_id = $id";
        command.Parameters.AddWithValue("$id", runId);
        return command.ExecuteNonQuery() == 1
            ? (true, null)
            : (false, "Run inesistente.");
    }

    /// <summary>
    /// Durata della run in secondi, o null quando manca un capo: le run precedenti al
    /// tracciamento dell'avvio non hanno un inizio, quelle abbandonate non hanno una fine.
    /// </summary>
    private static int? DurationSeconds(string startedAt, string endedAt)
    {
        if (startedAt == null || endedAt == null)
            return null;
        if (!DateTime.TryParse(startedAt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime start) ||
            !DateTime.TryParse(endedAt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime end))
            return null;
        double seconds = (end - start).TotalSeconds;
        return seconds < 0 ? null : (int)seconds;
    }

    // ---- Quest della taverna ------------------------------------------------

    /// <summary>
    /// Bacheca di oggi vista dal lato server: le cinque quest estratte per la giornata con
    /// quanti giocatori le hanno assegnate, completate e riscosse, piu' lo storico dei giorni
    /// precedenti e il catalogo completo.
    ///
    /// "Assegnate" e' il numero di giocatori che hanno aperto la taverna oggi: le righe
    /// nascono al primo contatto, non a mezzanotte. Nello storico si contano solo le
    /// riscossioni, perche' i contatori sono cumulativi e ricalcolare oggi il completamento
    /// di ieri direbbe quanti hanno superato la soglia da allora, non entro la giornata.
    /// </summary>
    public object GetTavernQuests(int days)
    {
        days = Math.Clamp(days, 1, 90);
        string today = TavernQuests.TodayKey();
        using SqliteConnection connection = database.Open();

        var quests = new List<object>();
        foreach (TavernQuests.QuestDefinition definition in TavernQuests.DefinitionsForDay(today))
        {
            (int assigned, int completed, int claimed) = QuestDayCounts(connection, today, definition);
            quests.Add(new
            {
                questId = definition.Id,
                title = definition.Title,
                description = definition.Description,
                counterKey = definition.CounterKey,
                counterLabel = CounterLabel(definition.CounterKey),
                threshold = definition.Threshold,
                advanced = definition.Advanced,
                assigned,
                completed,
                claimed
            });
        }

        int playersToday = ScalarInt(connection,
            "SELECT COUNT(DISTINCT player_id) FROM player_tavern_quests WHERE day = $d", ("$d", today));
        int claimsToday = ScalarInt(connection,
            "SELECT COUNT(*) FROM player_tavern_quests WHERE day = $d AND claimed_at IS NOT NULL", ("$d", today));
        int bonusToday = ScalarInt(connection,
            "SELECT COUNT(*) FROM player_tavern_bonus WHERE day = $d", ("$d", today));

        return new
        {
            day = today,
            secondsToRefresh = TavernQuests.SecondsToRefresh(),
            questHoneyReward = TavernQuests.QuestHoneyReward,
            bonusHoneyReward = TavernQuests.AllQuestsHoneyReward,
            questsPerDay = TavernQuests.QuestsPerDay,
            playersToday,
            claimsToday,
            bonusToday,
            honeyToday = claimsToday * TavernQuests.QuestHoneyReward
                + bonusToday * TavernQuests.AllQuestsHoneyReward,
            quests,
            history = TavernHistory(connection, days),
            catalog = TavernCatalog(connection)
        };
    }

    private static (int Assigned, int Completed, int Claimed) QuestDayCounts(
        SqliteConnection connection, string day, TavernQuests.QuestDefinition definition)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN COALESCE(c.value, 0) - q.baseline >= $threshold
                                     THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN q.claimed_at IS NOT NULL THEN 1 ELSE 0 END), 0)
            FROM player_tavern_quests q
            LEFT JOIN player_counters c
                ON c.player_id = q.player_id AND c.counter_key = $key
            WHERE q.day = $day AND q.quest_id = $quest";
        command.Parameters.AddWithValue("$threshold", definition.Threshold);
        command.Parameters.AddWithValue("$key", definition.CounterKey);
        command.Parameters.AddWithValue("$day", day);
        command.Parameters.AddWithValue("$quest", definition.Id);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : (0, 0, 0);
    }

    private static List<object> TavernHistory(SqliteConnection connection, int days)
    {
        string since = DateTime.UtcNow.Date.AddDays(-(days - 1)).ToString("yyyy-MM-dd");

        var players = new Dictionary<string, int>();
        var claims = new Dictionary<string, int>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT day, COUNT(DISTINCT player_id),
                       COALESCE(SUM(CASE WHEN claimed_at IS NOT NULL THEN 1 ELSE 0 END), 0)
                FROM player_tavern_quests WHERE day >= $since GROUP BY day";
            command.Parameters.AddWithValue("$since", since);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                players[reader.GetString(0)] = reader.GetInt32(1);
                claims[reader.GetString(0)] = reader.GetInt32(2);
            }
        }

        var bonuses = new Dictionary<string, int>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT day, COUNT(*) FROM player_tavern_bonus WHERE day >= $since GROUP BY day";
            command.Parameters.AddWithValue("$since", since);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                bonuses[reader.GetString(0)] = reader.GetInt32(1);
        }

        var points = new List<object>();
        DateTime start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        for (int index = 0; index < days; index++)
        {
            string day = start.AddDays(index).ToString("yyyy-MM-dd");
            int claimed = claims.GetValueOrDefault(day);
            int bonus = bonuses.GetValueOrDefault(day);
            points.Add(new
            {
                date = day,
                players = players.GetValueOrDefault(day),
                claims = claimed,
                bonuses = bonus,
                honey = claimed * TavernQuests.QuestHoneyReward
                    + bonus * TavernQuests.AllQuestsHoneyReward
            });
        }
        return points;
    }

    /// <summary>
    /// Catalogo completo con quanto ogni quest ha girato: giorni in cui e' uscita e
    /// riscossioni totali. Serve a vedere se l'estrazione e' equilibrata e quali obiettivi
    /// i giocatori lasciano indietro.
    /// </summary>
    private static List<object> TavernCatalog(SqliteConnection connection)
    {
        var daysOut = new Dictionary<string, int>();
        var claims = new Dictionary<string, int>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT quest_id, COUNT(DISTINCT day),
                       COALESCE(SUM(CASE WHEN claimed_at IS NOT NULL THEN 1 ELSE 0 END), 0)
                FROM player_tavern_quests GROUP BY quest_id";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                daysOut[reader.GetString(0)] = reader.GetInt32(1);
                claims[reader.GetString(0)] = reader.GetInt32(2);
            }
        }

        var rows = new List<object>();
        foreach (TavernQuests.QuestDefinition definition in TavernQuests.AllDefinitions())
        {
            rows.Add(new
            {
                questId = definition.Id,
                title = definition.Title,
                description = definition.Description,
                counterKey = definition.CounterKey,
                counterLabel = CounterLabel(definition.CounterKey),
                threshold = definition.Threshold,
                advanced = definition.Advanced,
                daysOut = daysOut.GetValueOrDefault(definition.Id),
                claims = claims.GetValueOrDefault(definition.Id)
            });
        }
        return rows;
    }

    private static string CounterLabel(string counterKey) =>
        CounterLabels.GetValueOrDefault(counterKey, counterKey);

    // ---- Stagioni -----------------------------------------------------------

    public object GetSeasons()
    {
        using SqliteConnection connection = database.Open();
        var seasons = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT s.season_id, s.name, s.starts_at, s.ends_at, s.is_active,
                   (SELECT COUNT(*) FROM match_history m WHERE m.season_id = s.season_id) AS matches,
                   (SELECT COUNT(*) FROM ranked_state r WHERE r.season_id = s.season_id) AS players
            FROM seasons s ORDER BY s.season_id DESC";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            seasons.Add(new
            {
                seasonId = reader.GetInt32(0),
                name = reader.GetString(1),
                startsAt = NullableString(reader, 2),
                endsAt = NullableString(reader, 3),
                isActive = reader.GetInt32(4) != 0,
                matches = reader.GetInt32(5),
                players = reader.GetInt32(6)
            });
        return new { seasons };
    }

    /// <summary>
    /// Classifica di una stagione: tutti i giocatori che l'hanno toccata (ladder ranked,
    /// aggregati di stagione o anche solo una partita amichevole) con vittorie, sconfitte
    /// e win rate. Null se la stagione non esiste.
    ///
    /// Partite, vittorie, sconfitte e win rate sono quelle classificate: le amichevoli
    /// non entrano negli aggregati, e stanno nella colonna a parte che si conta dallo
    /// storico. Chi ha giocato solo amichevoli e' in lista con zero partite.
    /// </summary>
    public object GetSeasonDetail(int seasonId)
    {
        using SqliteConnection connection = database.Open();

        object season;
        int matchesTotal, rankedMatches;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT s.season_id, s.name, s.starts_at, s.ends_at, s.is_active,
                       (SELECT COUNT(*) FROM match_history m WHERE m.season_id = s.season_id),
                       (SELECT COUNT(*) FROM match_history m
                        WHERE m.season_id = s.season_id AND m.ranked = 1)
                FROM seasons s WHERE s.season_id = $season";
            command.Parameters.AddWithValue("$season", seasonId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            matchesTotal = reader.GetInt32(5);
            rankedMatches = reader.GetInt32(6);
            season = new
            {
                seasonId = reader.GetInt32(0),
                name = reader.GetString(1),
                startsAt = NullableString(reader, 2),
                endsAt = NullableString(reader, 3),
                isActive = reader.GetInt32(4) != 0,
                matches = matchesTotal,
                rankedMatches
            };
        }

        var players = new List<object>();
        int rankedPlayers = 0, mmrSum = 0;
        using (SqliteCommand command = connection.CreateCommand())
        {
            // L'insieme dei partecipanti non e' solo la ladder: chi ha giocato amichevoli
            // non ha una riga in ranked_state, ma nella stagione ci e' comunque passato.
            // L'ordinamento ricalca quello della leaderboard (MMR decrescente, a pari MMR
            // davanti chi ha giocato meno), lo stesso con cui si scatta la hall of fame:
            // le posizioni qui e quelle congelate a fine stagione coincidono.
            command.CommandText = @"
                WITH participants AS (
                    SELECT player_a AS player_id, ended_at, ranked FROM match_history WHERE season_id = $season
                    UNION ALL
                    SELECT player_b, ended_at, ranked FROM match_history WHERE season_id = $season
                ),
                played AS (
                    SELECT player_id,
                           SUM(CASE WHEN ranked = 0 THEN 1 ELSE 0 END) AS friendly_matches,
                           MAX(ended_at) AS last_at
                    FROM participants GROUP BY player_id
                ),
                ids AS (
                    SELECT player_id FROM played
                    UNION
                    SELECT player_id FROM ranked_state WHERE season_id = $season
                    UNION
                    SELECT player_id FROM player_stats WHERE scope = $scope
                )
                SELECT ids.player_id, COALESCE(a.username, ids.player_id),
                       r.mmr, r.games_played, r.placement_done, r.peak_mmr,
                       COALESCE(st.matches, 0), COALESCE(st.wins, 0),
                       COALESCE(st.losses, 0), COALESCE(st.forfeits, 0),
                       COALESCE(st.best_streak, 0), COALESCE(st.current_streak, 0),
                       COALESCE(st.total_match_seconds, 0), pl.last_at,
                       COALESCE(pl.friendly_matches, 0)
                FROM ids
                LEFT JOIN accounts a ON a.player_id = ids.player_id
                LEFT JOIN ranked_state r ON r.player_id = ids.player_id AND r.season_id = $season
                LEFT JOIN player_stats st ON st.player_id = ids.player_id AND st.scope = $scope
                LEFT JOIN played pl ON pl.player_id = ids.player_id
                ORDER BY (r.mmr IS NULL), r.mmr DESC, r.games_played ASC,
                         COALESCE(st.wins, 0) DESC, ids.player_id ASC";
            command.Parameters.AddWithValue("$season", seasonId);
            command.Parameters.AddWithValue("$scope", $"season:{seasonId}");
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string playerId = reader.GetString(0);
                bool isRanked = !reader.IsDBNull(2);
                int mmr = isRanked ? reader.GetInt32(2) : 0;
                RankedTierInfo tier = isRanked ? ranked.Describe(mmr) : null;
                int matches = reader.GetInt32(6);
                int wins = reader.GetInt32(7);
                if (isRanked)
                {
                    rankedPlayers++;
                    mmrSum += mmr;
                }
                players.Add(new
                {
                    // Chi non e' in ladder resta senza posizione: la classifica e' quella ranked.
                    rank = isRanked ? rankedPlayers : 0,
                    playerId,
                    username = reader.GetString(1),
                    online = presence.IsOnline(playerId),
                    ranked = isRanked,
                    mmr = isRanked ? mmr : (int?)null,
                    peakMmr = isRanked ? reader.GetInt32(5) : (int?)null,
                    rankedGames = isRanked ? reader.GetInt32(3) : 0,
                    placementDone = isRanked && reader.GetInt32(4) != 0,
                    tier = tier?.TierName,
                    division = tier?.Division,
                    leaguePoints = tier?.LeaguePoints ?? 0,
                    matches,
                    wins,
                    losses = reader.GetInt32(8),
                    forfeits = reader.GetInt32(9),
                    bestStreak = reader.GetInt32(10),
                    currentStreak = reader.GetInt32(11),
                    totalMatchSeconds = reader.GetInt32(12),
                    lastMatchAt = NullableString(reader, 13),
                    // Le amichevoli non stanno negli aggregati: vengono contate a parte
                    // dallo storico, altrimenti in questa riga non ci sarebbe traccia.
                    friendlyMatches = reader.GetInt32(14),
                    winRatePercent = matches > 0 ? (int)Math.Round(wins * 100.0 / matches) : 0
                });
            }
        }

        return new
        {
            season,
            summary = new
            {
                players = players.Count,
                rankedPlayers,
                matches = matchesTotal,
                rankedMatches,
                averageMmr = rankedPlayers > 0 ? (int)Math.Round(mmrSum / (double)rankedPlayers) : 0
            },
            players
        };
    }

    // ---- Azioni di scrittura (curate) ---------------------------------------

    public (bool ok, string error) RenamePlayer(string playerId, string newName)
    {
        newName = newName?.Trim() ?? string.Empty;
        if (newName.Length is < 3 or > 18)
            return (false, "Il nome deve avere 3-18 caratteri.");
        string ci = newName.ToLowerInvariant();

        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            Execute(connection, transaction,
                "UPDATE accounts SET username = $n, username_ci = $ci WHERE player_id = $id",
                ("$n", newName), ("$ci", ci), ("$id", playerId));
            Execute(connection, transaction, @"
                INSERT INTO account_nicknames (player_id, nickname, nickname_ci, updated_at)
                VALUES ($id, $n, $ci, $now)
                ON CONFLICT(player_id) DO UPDATE SET
                    nickname = excluded.nickname,
                    nickname_ci = excluded.nickname_ci,
                    updated_at = excluded.updated_at",
                ("$id", playerId), ("$n", newName), ("$ci", ci),
                ("$now", DateTime.UtcNow.ToString("O")));
            transaction.Commit();
            return (true, null);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return (false, "Nome gia in uso da un altro giocatore.");
        }
    }

    public (bool ok, string error) SetHoney(string playerId, int honey)
    {
        if (honey < 0) return (false, "Il miele non puo essere negativo.");
        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        Execute(connection, null, @"
            INSERT INTO single_player_progress
                (player_id, honey, account_level, account_experience, account_total_experience,
                 account_experience_to_next_level, tutorial_completed, hardcore_unlocked, updated_at)
            VALUES ($id, $honey, 1, 0, 0, 100, 0, 0, $now)
            ON CONFLICT(player_id) DO UPDATE SET honey = $honey, updated_at = $now",
            ("$id", playerId), ("$honey", honey), ("$now", DateTime.UtcNow.ToString("O")));
        return (true, null);
    }

    public (bool ok, string error) ResetProgress(string playerId)
    {
        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        Execute(connection, transaction,
            "DELETE FROM single_player_unlocks WHERE player_id = $id", ("$id", playerId));
        // I contatori vanno azzerati insieme al resto: alimentano sia le prove del Santuario
        // sia le quest della taverna, e un "reset progressi" che li lasciasse in piedi
        // rimetterebbe il giocatore a zero di miele ma con le prove gia' superate.
        // La bisaccia e le quest di oggi seguono la stessa sorte.
        foreach (string table in new[] { "player_counters", "player_consumables", "player_bag",
            "player_tavern_quests", "player_tavern_bonus" })
        {
            Execute(connection, transaction, $"DELETE FROM {table} WHERE player_id = $id", ("$id", playerId));
        }
        Execute(connection, transaction, @"
            INSERT INTO single_player_progress
                (player_id, honey, account_level, account_experience, account_total_experience,
                 account_experience_to_next_level, tutorial_completed, hardcore_unlocked, updated_at)
            VALUES ($id, 0, 1, 0, 0, 100, 0, 0, $now)
            ON CONFLICT(player_id) DO UPDATE SET
                honey = 0,
                account_level = 1,
                account_experience = 0,
                account_total_experience = 0,
                account_experience_to_next_level = 100,
                tutorial_completed = 0,
                hardcore_unlocked = 0,
                updated_at = $now",
            ("$id", playerId), ("$now", DateTime.UtcNow.ToString("O")));
        transaction.Commit();
        return (true, null);
    }

    // ---- Sblocchi a mano (account di prova) ---------------------------------

    /// <summary>
    /// Catalogo concedibile con lo stato attuale del giocatore. Serve agli account di prova:
    /// da qui classi, supreme, oggetti e capitoli si danno e si tolgono senza pagare miele e
    /// senza superare le prove del Santuario.
    /// </summary>
    public object GetPlayerUnlocks(string playerId)
    {
        using SqliteConnection connection = database.Open();
        return PlayerExists(connection, playerId) ? BuildUnlockState(connection, playerId) : null;
    }

    private static object BuildUnlockState(SqliteConnection connection, string playerId)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT unlock_type, unlock_id FROM single_player_unlocks WHERE player_id = $id";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                owned.Add(reader.GetString(0) + "/" + reader.GetString(1));
        }

        bool tutorial = ReadProgressFlag(connection, playerId, "tutorial_completed");
        bool hardcore = ReadProgressFlag(connection, playerId, "hardcore_unlocked");

        var groups = new List<object>();
        foreach (AdminUnlockCatalog.Group group in AdminUnlockCatalog.Groups)
        {
            var entries = new List<object>();
            foreach (AdminUnlockCatalog.Entry entry in group.Entries)
            {
                // Le classi base non hanno bisogno della riga: il gioco le rimette in lista
                // finche' il tutorial risulta completato, quindi toglierle non avrebbe effetto.
                bool fromTutorial = tutorial && AdminUnlockCatalog.IsStarterClass(group.Type, entry.Id);
                bool isOwned = group.Type switch
                {
                    AdminUnlockCatalog.TypeTutorial => tutorial,
                    AdminUnlockCatalog.TypeMode => hardcore,
                    _ => fromTutorial || owned.Contains(group.Type + "/" + entry.Id)
                };
                entries.Add(new
                {
                    id = entry.Id,
                    name = entry.Name,
                    note = entry.Note,
                    owned = isOwned,
                    locked = fromTutorial,
                    lockedReason = fromTutorial ? "Consegnata dal tutorial: torna finche' risulta completato." : null
                });
            }
            groups.Add(new { type = group.Type, label = group.Label, note = group.Note, entries });
        }
        return new { groups };
    }

    /// <summary>Concede o revoca una singola voce. Nessun costo, nessuna prova: e' il punto.</summary>
    public (bool ok, string error) SetUnlock(string playerId, string type, string id, bool granted)
    {
        string unlockType = CanonicalUnlockType(type);
        if (unlockType == null ||
            !AdminUnlockCatalog.TryResolve(unlockType, id?.Trim(), out string unlockId))
            return (false, "Sblocco non riconosciuto.");

        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, playerId);

        if (unlockType == AdminUnlockCatalog.TypeTutorial)
        {
            SetProgressFlag(connection, transaction, playerId, "tutorial_completed", granted);
            // Stessa dotazione che il gioco consegna a fine tutorial: senza, il pannello
            // direbbe "tutorial fatto" e il giocatore si troverebbe senza classi.
            if (granted)
            {
                foreach (string classId in AdminUnlockCatalog.StarterClassIds)
                    GrantUnlockRow(connection, transaction, playerId, "class", classId);
                GrantUnlockRow(connection, transaction, playerId, "chapter", "chapter-1");
                // Anche i moduli del percorso: il flag da solo direbbe "onboarding finito"
                // mentre i cancelli terrebbero il giocatore fermo alla prima lezione.
                foreach (string moduleId in TutorialModuleCatalog.AllIds)
                    GrantUnlockRow(
                        connection, transaction, playerId, TutorialModuleCatalog.UnlockType, moduleId);
            }
            else
            {
                // "Rifai il tutorial" vuol dire rifarlo davvero: senza cancellare i moduli il
                // percorso resterebbe segnato come gia' fatto, e il primo contatto col server
                // rimetterebbe comunque il flag (vedi il backfill in EnsureProgressRow).
                Execute(connection, transaction, @"
                    DELETE FROM single_player_unlocks
                    WHERE player_id = $id AND unlock_type = $type",
                    ("$id", playerId), ("$type", TutorialModuleCatalog.UnlockType));
            }
        }
        else if (unlockType == AdminUnlockCatalog.TypeMode)
        {
            SetProgressFlag(connection, transaction, playerId, "hardcore_unlocked", granted);
        }
        else if (granted)
        {
            GrantUnlockRow(connection, transaction, playerId, unlockType, unlockId);
        }
        else
        {
            if (AdminUnlockCatalog.IsStarterClass(unlockType, unlockId) &&
                ReadProgressFlag(connection, playerId, "tutorial_completed", transaction))
                return (false, "Classe base: torna da sola col tutorial completato. Togli prima il tutorial.");

            Execute(connection, transaction, @"
                DELETE FROM single_player_unlocks
                WHERE player_id = $id AND unlock_type = $type AND unlock_id = $unlock",
                ("$id", playerId), ("$type", unlockType), ("$unlock", unlockId));
        }

        transaction.Commit();
        return (true, null);
    }

    /// <summary>
    /// Sblocca o blocca tutto in un colpo. Il "blocca tutto" tocca solo gli sblocchi: miele,
    /// livello e contatori restano dove sono (per azzerare quelli c'e' il reset progressi).
    /// </summary>
    public (bool ok, string error) SetAllUnlocks(string playerId, bool granted)
    {
        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, playerId);

        if (granted)
        {
            foreach ((string type, string id) in AdminUnlockCatalog.UnlockRows())
                GrantUnlockRow(connection, transaction, playerId, type, id);
        }
        else
        {
            Execute(connection, transaction,
                "DELETE FROM single_player_unlocks WHERE player_id = $id", ("$id", playerId));
        }

        SetProgressFlag(connection, transaction, playerId, "tutorial_completed", granted);
        SetProgressFlag(connection, transaction, playerId, "hardcore_unlocked", granted);
        transaction.Commit();
        return (true, null);
    }

    // ---- Scorta consumabili (account di prova) -------------------------------

    /// <summary>Tetto per riga: la scorta serve a provare gli oggetti, non a farne un magazzino.</summary>
    private const int MaxStashCount = 99;

    public object GetPlayerStash(string playerId)
    {
        using SqliteConnection connection = database.Open();
        return PlayerExists(connection, playerId) ? BuildStashState(connection, playerId) : null;
    }

    /// <summary>
    /// Catalogo consumabili con la quantita' posseduta e la bisaccia scelta. Il catalogo arriva
    /// intero e non solo le righe presenti: dal pannello si aggiunge anche quello che il
    /// giocatore non ha mai comprato, che e' il motivo per cui esiste questa schermata.
    /// </summary>
    private static object BuildStashState(SqliteConnection connection, string playerId)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_id, count FROM player_consumables WHERE player_id = $id";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                counts[reader.GetString(0)] = reader.GetInt32(1);
        }

        var bag = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_id FROM player_bag WHERE player_id = $id ORDER BY item_id";
            command.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                bag.Add(reader.GetString(0));
        }

        var items = SanctuaryCatalog.All
            .Where(entry => entry.Type == SanctuaryCatalog.TypeItem)
            .Select(entry => new
            {
                id = entry.Id,
                name = entry.Name,
                description = entry.Description,
                cost = SanctuaryCatalog.CopyCostOf(entry),
                count = counts.TryGetValue(entry.Id, out int owned) ? owned : 0,
                inBag = bag.Contains(entry.Id)
            })
            .ToList();

        // Righe fuori catalogo (oggetto rinominato o rimosso): non sono modificabili dalle voci
        // qui sopra e sparirebbero dalla vista, quindi si mostrano a parte invece di far finta
        // che non esistano.
        var unknown = counts
            .Where(pair => !SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeItem, pair.Key, out _))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new { id = pair.Key, count = pair.Value })
            .ToList();

        return new
        {
            items,
            unknown,
            maxCount = MaxStashCount,
            slots = SanctuaryBag.ReadSlots(connection, null, playerId),
            bag = bag.OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => new { id, name = ItemName(id) })
                .ToList()
        };
    }

    /// <summary>
    /// Fissa quante copie di un consumabile ha il giocatore. Il valore e' assoluto e non un
    /// delta: il pannello manda quello che deve risultare, cosi' due click ravvicinati non si
    /// sommano e un rinvio della stessa richiesta non raddoppia niente.
    /// </summary>
    public (bool ok, string error) SetStashItem(string playerId, string itemId, int count)
    {
        if (!SanctuaryCatalog.TryGetEntry(
                SanctuaryCatalog.TypeItem, (itemId ?? string.Empty).Trim(), out SanctuaryCatalog.Entry entry))
            return (false, "Oggetto non riconosciuto.");
        if (count < 0 || count > MaxStashCount)
            return (false, $"Quantita fuori intervallo (0-{MaxStashCount}).");

        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        WriteStashCount(connection, transaction, playerId, entry.Id, count);
        transaction.Commit();
        return (true, null);
    }

    /// <summary>Stessa quantita' per ogni voce a catalogo; a zero svuota anche la bisaccia.</summary>
    public (bool ok, string error) SetAllStash(string playerId, int count)
    {
        if (count < 0 || count > MaxStashCount)
            return (false, $"Quantita fuori intervallo (0-{MaxStashCount}).");

        using SqliteConnection connection = database.Open();
        if (!PlayerExists(connection, playerId))
            return (false, "Giocatore inesistente.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        if (count == 0)
        {
            // Le righe fuori catalogo vanno via con le altre: lasciarle darebbe una scorta
            // vuota nel pannello e ancora piena in gioco.
            Execute(connection, transaction,
                "DELETE FROM player_consumables WHERE player_id = $id", ("$id", playerId));
            Execute(connection, transaction,
                "DELETE FROM player_bag WHERE player_id = $id", ("$id", playerId));
        }
        else
        {
            foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All
                         .Where(entry => entry.Type == SanctuaryCatalog.TypeItem))
                WriteStashCount(connection, transaction, playerId, entry.Id, count);
        }
        transaction.Commit();
        return (true, null);
    }

    /// <summary>
    /// Scrive la riga della scorta. A zero la riga sparisce e con lei il posto in bisaccia:
    /// e' la stessa regola del consumo in run, altrimenti la bisaccia mostrerebbe uno slot
    /// pieno che alla run successiva parte vuoto.
    /// </summary>
    private static void WriteStashCount(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string itemId, int count)
    {
        if (count <= 0)
        {
            Execute(connection, transaction,
                "DELETE FROM player_consumables WHERE player_id = $id AND item_id = $item",
                ("$id", playerId), ("$item", itemId));
            Execute(connection, transaction,
                "DELETE FROM player_bag WHERE player_id = $id AND item_id = $item",
                ("$id", playerId), ("$item", itemId));
            return;
        }

        Execute(connection, transaction, @"
            INSERT INTO player_consumables (player_id, item_id, count)
            VALUES ($id, $item, $count)
            ON CONFLICT(player_id, item_id) DO UPDATE SET count = $count",
            ("$id", playerId), ("$item", itemId), ("$count", count));
    }

    private static string CanonicalUnlockType(string type) => (type ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "class" => SanctuaryCatalog.TypeClass,
        "secondability" => SanctuaryCatalog.TypeSecondAbility,
        "slot" => SanctuaryCatalog.TypeSlot,
        "chapter" => AdminUnlockCatalog.TypeChapter,
        "chaptercleared" => AdminUnlockCatalog.TypeChapterCleared,
        "mode" => AdminUnlockCatalog.TypeMode,
        "tutorial" => AdminUnlockCatalog.TypeTutorial,
        "tutorialmodule" => TutorialModuleCatalog.UnlockType,
        _ => null
    };

    private static void GrantUnlockRow(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string type, string id) =>
        Execute(connection, transaction, @"
            INSERT OR IGNORE INTO single_player_unlocks (player_id, unlock_type, unlock_id, unlocked_at)
            VALUES ($id, $type, $unlock, $now)",
            ("$id", playerId), ("$type", type), ("$unlock", id), ("$now", DateTime.UtcNow.ToString("O")));

    /// <summary>
    /// Il nome della colonna e' interpolato nella query: puo' arrivare solo dalle costanti qui
    /// dentro, mai dalla richiesta.
    /// </summary>
    private static void SetProgressFlag(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string column, bool value) =>
        Execute(connection, transaction,
            $"UPDATE single_player_progress SET {column} = $value, updated_at = $now WHERE player_id = $id",
            ("$value", value ? 1 : 0), ("$now", DateTime.UtcNow.ToString("O")), ("$id", playerId));

    private static bool ReadProgressFlag(
        SqliteConnection connection, string playerId, string column, SqliteTransaction transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {column} FROM single_player_progress WHERE player_id = $id";
        command.Parameters.AddWithValue("$id", playerId);
        object result = command.ExecuteScalar();
        return result != null && result != DBNull.Value && Convert.ToInt32(result) != 0;
    }

    /// <summary>
    /// Riga di progressione minima, per poter scrivere su un account che non ha ancora giocato.
    /// A differenza del percorso di gioco non assegna le quest del giorno: fissarne il baseline
    /// dal pannello significherebbe farlo al posto del giocatore.
    /// </summary>
    private static void EnsureProgressRow(
        SqliteConnection connection, SqliteTransaction transaction, string playerId) =>
        Execute(connection, transaction, @"
            INSERT OR IGNORE INTO single_player_progress
                (player_id, honey, account_level, account_experience, account_total_experience,
                 account_experience_to_next_level, tutorial_completed, hardcore_unlocked, updated_at)
            VALUES ($id, 0, 1, 0, 0, 100, 0, 0, $now)",
            ("$id", playerId), ("$now", DateTime.UtcNow.ToString("O")));

    /// <summary>
    /// La cancellazione vera vive in <see cref="AccountEraser"/>: la condivide con la
    /// pagina pubblica /account/delete, cosi' la lista delle tabelle da svuotare resta
    /// una sola e non puo' divergere tra i due percorsi.
    /// </summary>
    public (bool ok, string error) DeletePlayer(string playerId) => eraser.DeletePlayer(playerId);

    // ---- Helper -------------------------------------------------------------

    private static object ReadMatchRow(SqliteDataReader reader, string perspectivePlayerId)
    {
        string playerA = reader.GetString(3);
        string playerB = reader.GetString(4);
        int winner = reader.GetInt32(5);
        string nameA = reader.IsDBNull(9) ? playerA : reader.GetString(9);
        string nameB = reader.IsDBNull(10) ? playerB : reader.GetString(10);

        string result = null;
        if (perspectivePlayerId != null)
        {
            bool isA = playerA == perspectivePlayerId;
            result = winner == -1 ? "draw"
                : (winner == 0) == isA ? "win" : "loss";
        }

        return new
        {
            matchId = reader.GetInt32(0),
            endedAt = NullableString(reader, 1),
            ranked = reader.GetInt32(2) != 0,
            playerA,
            playerB,
            nameA,
            nameB,
            winner,
            scoreA = reader.GetInt32(6),
            scoreB = reader.GetInt32(7),
            endedReason = reader.GetString(8),
            result
        };
    }

    private static bool PlayerExists(SqliteConnection connection, string playerId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM accounts WHERE player_id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", playerId);
        return command.ExecuteScalar() != null;
    }

    private static int ScalarInt(SqliteConnection connection, string sql, params (string, object)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
            command.Parameters.AddWithValue(name, value);
        object result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private static string ScalarString(SqliteConnection connection, string sql, params (string, object)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
            command.Parameters.AddWithValue(name, value);
        object result = command.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : Convert.ToString(result);
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

    private static string NullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
