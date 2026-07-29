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

    public AdminService(
        AccardDatabase database, PresenceRegistry presence, SeasonService seasons, RankedService ranked)
    {
        this.database = database;
        this.presence = presence;
        this.seasons = seasons;
        this.ranked = ranked;
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

        int totalCampaignRuns = ScalarInt(connection, "SELECT COUNT(*) FROM campaign_runs");
        int campaign24h = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at >= $s", ("$s", since24h));
        int campaign7d = ScalarInt(connection,
            "SELECT COUNT(*) FROM campaign_runs WHERE ended_at >= $s", ("$s", since7d));

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
                campaign = campaign.GetValueOrDefault(day)
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
                       COALESCE(sp.account_experience_to_next_level, 100) AS account_experience_to_next,
                       COALESCE(sp.account_total_experience, 0) AS account_total_experience,
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
                    losses = reader.GetInt32(14),
                    nickname = NullableString(reader, 15),
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
                       COALESCE(sp.account_experience_to_next_level, 100),
                       COALESCE(sp.account_total_experience, 0),
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
                progressUpdatedAt = NullableString(reader, 12),
                selectedIconId = NullableString(reader, 13),
                bio = NullableString(reader, 14),
                nickname = NullableString(reader, 15),
                authMethod = NullableString(reader, 16),
                email = NullableString(reader, 17),
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
            command.CommandText = @"
                SELECT ended_at, mode, chapter_id, stage_id, rooms_cleared,
                       enemies_defeated, bosses_defeated, honey_reward
                FROM campaign_runs WHERE player_id = $id
                ORDER BY ended_at DESC LIMIT 20";
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
                    honeyReward = reader.GetInt32(7)
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
            seasonName = seasons.ActiveSeasonName,
            ranked = ReadRankedState(connection, playerId),
            campaignTotals = ReadCampaignTotals(connection, playerId),
            counters = DescribeCounters(counterValues),
            tavern = ReadTavernState(connection, playerId, counterValues),
            rewards = ReadRewardClaims(connection, playerId),
            consumables = ReadConsumables(connection, playerId),
            bag = ReadBag(connection, playerId),
            achievements = ReadAchievements(connection, playerId),
            icons = ReadIcons(connection, playerId),
            campaignKills = ReadCampaignKills(connection, playerId),
            friends = ReadFriendCounts(connection, playerId),
            hallOfFame = ReadHallOfFame(connection, playerId),
            recentMatches,
            recentRuns,
            recentLogins,
            unlocks
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
        command.CommandText = @"
            SELECT COUNT(*), COALESCE(SUM(rooms_cleared), 0), COALESCE(SUM(enemies_defeated), 0),
                   COALESCE(SUM(bosses_defeated), 0), COALESCE(SUM(minibosses_defeated), 0),
                   COALESCE(SUM(honey_reward), 0), MIN(ended_at), MAX(ended_at)
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
            lastRunAt = NullableString(reader, 7)
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
        var quests = new List<object>();
        int completed = 0;
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
                        current = 0, threshold = 0, completed = false, claimed, claimedAt = NullableString(reader, 2) });
                    continue;
                }

                int gained = Math.Max(0, counters.GetValueOrDefault(definition.CounterKey) - baseline);
                bool isCompleted = gained >= definition.Threshold;
                if (isCompleted)
                    completed++;
                quests.Add(new
                {
                    questId = definition.Id,
                    title = definition.Title,
                    description = definition.Description,
                    current = Math.Min(gained, definition.Threshold),
                    threshold = definition.Threshold,
                    completed = isCompleted,
                    claimed,
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
            bonusClaimed,
            bonusAvailable = quests.Count > 0 && completed >= quests.Count,
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

    private static List<object> ReadConsumables(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT item_id, count FROM player_consumables
            WHERE player_id = $id AND count > 0 ORDER BY item_id";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string itemId = reader.GetString(0);
            rows.Add(new { id = itemId, name = ItemName(itemId), count = reader.GetInt32(1) });
        }
        return rows;
    }

    private static List<object> ReadBag(SqliteConnection connection, string playerId)
    {
        var rows = new List<object>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT item_id FROM player_bag WHERE player_id = $id ORDER BY item_id";
        command.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string itemId = reader.GetString(0);
            rows.Add(new { id = itemId, name = ItemName(itemId) });
        }
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
