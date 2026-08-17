using AccardND.Server.Data;
using AccardND.NetProtocol;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>Tier/divisione/LP derivati da un MMR.</summary>
public sealed record RankedTierInfo(
    int TierIndex, string TierName, string Division, int LeaguePoints, int GlobalDivision);

/// <summary>Stato ranked corrente di un giocatore (per ranked.get).</summary>
public sealed record RankedProgress(
    bool Ranked, int Mmr, int GamesPlayed, bool PlacementDone, int PlacementRemaining, RankedTierInfo Tier);

/// <summary>Variazione ranked di un giocatore dopo una partita (per match.result).</summary>
public sealed record PlayerRankedDelta(
    RankedTierInfo Before, RankedTierInfo After,
    int LpDelta, bool Promoted, bool Demoted, bool Placement, int PlacementRemaining);

public sealed record ApplyMatchResult(PlayerRankedDelta A, PlayerRankedDelta B);

public sealed record LeaderboardRow(
    string PlayerId, string Username, string SelectedIconId,
    int Mmr, int GamesPlayed, bool PlacementDone, RankedTierInfo Tier,
    int Wins, int Losses);

/// <summary>
/// MMR nascosto (Elo) e sua traduzione in tier a leghe. Le scritture avvengono
/// dentro la transazione del <see cref="MatchResultRecorder"/> per essere atomiche
/// con match_history e statistiche.
/// </summary>
public sealed class RankedService
{
    private readonly AccardDatabase database;
    private readonly RankedConfig config;

    public RankedService(AccardDatabase database, ServerConfig serverConfig)
    {
        this.database = database;
        config = serverConfig.Ranked;
    }

    /// <summary>Traduce un MMR in tier, divisione e punti lega.</summary>
    public RankedTierInfo Describe(int mmr)
    {
        int totalDivisions = config.Tiers.Length * config.DivisionsPerTier;
        int globalDivision = (int)Math.Floor((mmr - config.TierFloor) / (double)config.DivisionWidth);
        globalDivision = Math.Clamp(globalDivision, 0, totalDivisions - 1);

        int tierIndex = globalDivision / config.DivisionsPerTier;
        int divisionWithinTier = globalDivision % config.DivisionsPerTier;
        int divisionFloor = config.TierFloor + globalDivision * config.DivisionWidth;
        int leaguePoints = Math.Clamp(
            (int)Math.Round((mmr - divisionFloor) * 100.0 / config.DivisionWidth), 0, 100);

        return new RankedTierInfo(
            tierIndex,
            config.Tiers[tierIndex],
            ToRoman(config.DivisionsPerTier - divisionWithinTier),
            leaguePoints,
            globalDivision);
    }

    public AdventureLeaderboardData GetAdventureLeaderboard(int limit)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            WITH ranked_runs AS (
                SELECT r.*,
                       CASE WHEN r.chapter_id GLOB 'chapter-[0-9]*'
                            THEN CAST(SUBSTR(r.chapter_id, 9) AS INTEGER) ELSE 0 END AS chapter_number,
                       ROW_NUMBER() OVER (
                           PARTITION BY r.player_id
                           ORDER BY CASE WHEN r.chapter_id GLOB 'chapter-[0-9]*'
                                         THEN CAST(SUBSTR(r.chapter_id, 9) AS INTEGER) ELSE 0 END DESC,
                                    r.rooms_cleared DESC) AS personal_position
                FROM campaign_runs r
            )
            SELECT r.player_id, COALESCE(n.nickname, a.username, r.player_id),
                   COALESCE(p.selected_icon_id, ''), r.chapter_number, r.rooms_cleared
            FROM ranked_runs r
            LEFT JOIN accounts a ON a.player_id = r.player_id
            LEFT JOIN account_nicknames n ON n.player_id = r.player_id
            LEFT JOIN profiles p ON p.player_id = r.player_id
            WHERE r.personal_position = 1
            ORDER BY r.chapter_number DESC, r.rooms_cleared DESC,
                     COALESCE(n.nickname, a.username, r.player_id) COLLATE NOCASE
            LIMIT $limit";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));

        var entries = new List<AdventureLeaderboardEntry>();
        using SqliteDataReader reader = command.ExecuteReader();
        int ordinal = 0;
        int rank = 0;
        (int Chapter, int Room)? previousRecord = null;
        while (reader.Read())
        {
            ordinal++;
            int chapterNumber = reader.GetInt32(3);
            int roomsCleared = reader.GetInt32(4);
            var record = (chapterNumber, roomsCleared);
            if (record != previousRecord)
                rank = ordinal;
            previousRecord = record;
            entries.Add(new AdventureLeaderboardEntry
            {
                rank = rank,
                playerId = reader.GetString(0),
                username = reader.GetString(1),
                selectedIconId = reader.GetString(2),
                chapterNumber = chapterNumber,
                roomsCleared = roomsCleared
            });
        }
        return new AdventureLeaderboardData { entries = entries.ToArray() };
    }

    public RankedProgress GetProgress(string playerId, int seasonId)
    {
        using SqliteConnection connection = database.Open();
        (int mmr, int games, bool placementDone, bool exists) = ReadState(connection, null, playerId, seasonId);
        return new RankedProgress(
            exists, mmr, games, placementDone,
            Math.Max(0, config.PlacementMatches - games),
            Describe(mmr));
    }

    /// <summary>
    /// Posizione in classifica (1-based, 0 se il giocatore non è in tabella) e
    /// totale dei classificati nella stagione. L'ordinamento è lo stesso della
    /// leaderboard: MMR decrescente, a pari MMR vince chi ha giocato meno partite.
    /// </summary>
    public (int Rank, int Players) GetGlobalStanding(string playerId, int seasonId)
    {
        using SqliteConnection connection = database.Open();

        int players;
        using (SqliteCommand count = connection.CreateCommand())
        {
            count.CommandText =
                "SELECT COUNT(*) FROM ranked_state WHERE season_id=$season AND games_played > 0";
            count.Parameters.AddWithValue("$season", seasonId);
            players = (int)(long)count.ExecuteScalar();
        }

        // Stesso filtro della leaderboard: chi non ha ancora giocato in questa
        // stagione non e' in classifica, e non deve leggere una posizione che sulla
        // pagina della Hall of Fame non troverebbe.
        (int mmr, int games, _, bool exists) = ReadState(connection, null, playerId, seasonId);
        if (!exists || games == 0)
            return (0, players);

        using SqliteCommand ahead = connection.CreateCommand();
        ahead.CommandText = @"
            SELECT COUNT(*) FROM ranked_state
            WHERE season_id=$season AND games_played > 0
              AND (mmr > $mmr OR (mmr = $mmr AND games_played < $games))";
        ahead.Parameters.AddWithValue("$season", seasonId);
        ahead.Parameters.AddWithValue("$mmr", mmr);
        ahead.Parameters.AddWithValue("$games", games);
        return ((int)(long)ahead.ExecuteScalar() + 1, players);
    }

    /// <summary>
    /// Quanti giocatori sono in classifica nella stagione, cioe' quanti ci hanno
    /// giocato almeno una partita. Il soft reset di inizio stagione riporta avanti
    /// una riga per ognuno dei classificati precedenti: contarle tutte darebbe una
    /// classifica piena il giorno in cui non ha ancora giocato nessuno.
    /// </summary>
    public int CountRanked(int seasonId)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand count = connection.CreateCommand();
        count.CommandText =
            "SELECT COUNT(*) FROM ranked_state WHERE season_id=$season AND games_played > 0";
        count.Parameters.AddWithValue("$season", seasonId);
        return (int)(long)count.ExecuteScalar();
    }

    /// <summary>
    /// La classifica della stagione: solo chi ci ha giocato almeno una partita.
    /// Il filtro non e' cosmetico. Il soft reset di inizio stagione porta avanti la
    /// riga di tutti i classificati della precedente, con l'MMR dimezzato verso il
    /// centro e zero partite: senza filtro, il primo giorno di stagione la
    /// classifica sarebbe gia' ordinata e in testa ci sarebbe chi non ha ancora
    /// tirato un dado.
    ///
    /// Vittorie e sconfitte si contano da match_history filtrato su ranked=1, non da
    /// player_stats: lo storico ha una riga per partita, quindi una graduatoria di
    /// classificate resta coerente anche se un giorno gli aggregati tornassero a
    /// contare altro. Sono contate entrambe, e non una per differenza da
    /// games_played: cancellare un account elimina anche le sue partite, e la
    /// differenza trasformerebbe quelle sparite in sconfitte mai subite dagli
    /// avversari. Cosi' invece i due numeri calano insieme.
    /// </summary>
    public IReadOnlyList<LeaderboardRow> GetLeaderboard(int seasonId, int limit)
    {
        var rows = new List<LeaderboardRow>();
        using SqliteConnection connection = database.Open();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT r.player_id, COALESCE(a.username, ''), COALESCE(p.selected_icon_id, ''),
                   r.mmr, r.games_played, r.placement_done,
                   (SELECT COUNT(*) FROM match_history m
                     WHERE m.season_id = r.season_id AND m.ranked = 1
                       AND ((m.player_a = r.player_id AND m.winner = 0)
                         OR (m.player_b = r.player_id AND m.winner = 1))),
                   (SELECT COUNT(*) FROM match_history m
                     WHERE m.season_id = r.season_id AND m.ranked = 1
                       AND ((m.player_a = r.player_id AND m.winner = 1)
                         OR (m.player_b = r.player_id AND m.winner = 0)))
            FROM ranked_state r
            LEFT JOIN accounts a ON a.player_id = r.player_id
            LEFT JOIN profiles p ON p.player_id = r.player_id
            WHERE r.season_id=$season AND r.games_played > 0
            ORDER BY r.mmr DESC, r.games_played ASC LIMIT $limit";
        query.Parameters.AddWithValue("$season", seasonId);
        query.Parameters.AddWithValue("$limit", limit);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            int mmr = reader.GetInt32(3);
            rows.Add(new LeaderboardRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), mmr,
                reader.GetInt32(4), reader.GetInt32(5) != 0, Describe(mmr),
                reader.GetInt32(6), reader.GetInt32(7)));
        }
        return rows;
    }

    /// <summary>
    /// Applica l'esito ranked ad entrambi i giocatori dentro una transazione esistente.
    /// Winner: 0 = A, 1 = B. I round vinti servono a pesare la partita: un 2-0 muove
    /// l'MMR pieno, un 2-1 meno (vedi <see cref="MarginFactor"/>).
    /// </summary>
    public ApplyMatchResult ApplyMatch(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerAId, string playerBId, int winner, int scoreA, int scoreB, int seasonId)
    {
        (int aMmr, int aGames, bool aDone, _) = ReadState(connection, transaction, playerAId, seasonId);
        (int bMmr, int bGames, bool bDone, _) = ReadState(connection, transaction, playerBId, seasonId);

        bool aWon = winner == 0;
        double margin = MarginFactor(aWon ? scoreA : scoreB, aWon ? scoreB : scoreA);
        int aNew = NextMmr(aMmr, bMmr, aWon, placement: !aDone, margin);
        int bNew = NextMmr(bMmr, aMmr, !aWon, placement: !bDone, margin);

        int aGamesNew = aGames + 1;
        int bGamesNew = bGames + 1;
        bool aDoneNew = aDone || aGamesNew >= config.PlacementMatches;
        bool bDoneNew = bDone || bGamesNew >= config.PlacementMatches;

        WriteState(connection, transaction, playerAId, seasonId, aNew, aGamesNew, aDoneNew);
        WriteState(connection, transaction, playerBId, seasonId, bNew, bGamesNew, bDoneNew);

        return new ApplyMatchResult(
            BuildDelta(aMmr, aNew, aGamesNew, aDoneNew),
            BuildDelta(bMmr, bNew, bGamesNew, bDoneNew));
    }

    private PlayerRankedDelta BuildDelta(int mmrBefore, int mmrAfter, int gamesAfter, bool doneAfter)
    {
        RankedTierInfo before = Describe(mmrBefore);
        RankedTierInfo after = Describe(mmrAfter);
        int lpDelta = (int)Math.Round((mmrAfter - mmrBefore) * 100.0 / config.DivisionWidth);
        return new PlayerRankedDelta(
            before, after, lpDelta,
            after.GlobalDivision > before.GlobalDivision,
            after.GlobalDivision < before.GlobalDivision,
            !doneAfter,
            Math.Max(0, config.PlacementMatches - gamesAfter));
    }

    private int NextMmr(int mmr, int opponentMmr, bool won, bool placement, double margin)
    {
        double expected = 1.0 / (1.0 + Math.Pow(10, (opponentMmr - mmr) / 400.0));
        int k = placement ? config.PlacementK : config.StandardK;
        double next = mmr + k * margin * ((won ? 1.0 : 0.0) - expected);
        return Math.Max(0, (int)Math.Round(next));
    }

    /// <summary>
    /// Peso della partita in base al margine: 1 quando il perdente non vince nessun
    /// round, <see cref="RankedConfig.CloseMatchFactor"/> quando arriva a un round
    /// dal pareggio (il 2-1 del meglio di tre), interpolato in mezzo per formati piu'
    /// lunghi. Un abbandono a tavolino, dove il vincitore non ha round a referto,
    /// vale pieno: non e' una partita combattuta, e' una partita non giocata.
    /// </summary>
    private double MarginFactor(int winnerRounds, int loserRounds)
    {
        int mostConceded = winnerRounds - 1;
        if (mostConceded <= 0)
            return 1.0;

        double closeness = Math.Clamp(loserRounds / (double)mostConceded, 0.0, 1.0);
        return 1.0 + (config.CloseMatchFactor - 1.0) * closeness;
    }

    private (int Mmr, int Games, bool PlacementDone, bool Exists) ReadState(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int seasonId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT mmr, games_played, placement_done FROM ranked_state WHERE player_id=$id AND season_id=$season";
        query.Parameters.AddWithValue("$id", playerId);
        query.Parameters.AddWithValue("$season", seasonId);
        using SqliteDataReader reader = query.ExecuteReader();
        if (!reader.Read())
            return (config.StartMmr, 0, false, false);
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2) != 0, true);
    }

    private void WriteState(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, int seasonId, int mmr, int games, bool placementDone)
    {
        using SqliteCommand upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = @"
            INSERT INTO ranked_state (player_id, season_id, mmr, games_played, placement_done, peak_mmr, updated_at)
            VALUES ($id, $season, $mmr, $games, $done, $mmr, $now)
            ON CONFLICT(player_id, season_id) DO UPDATE SET
                mmr = excluded.mmr,
                games_played = excluded.games_played,
                placement_done = excluded.placement_done,
                peak_mmr = MAX(ranked_state.peak_mmr, excluded.mmr),
                updated_at = excluded.updated_at";
        upsert.Parameters.AddWithValue("$id", playerId);
        upsert.Parameters.AddWithValue("$season", seasonId);
        upsert.Parameters.AddWithValue("$mmr", mmr);
        upsert.Parameters.AddWithValue("$games", games);
        upsert.Parameters.AddWithValue("$done", placementDone ? 1 : 0);
        upsert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        upsert.ExecuteNonQuery();
    }

    private static string ToRoman(int value)
    {
        return value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            8 => "VIII",
            9 => "IX",
            10 => "X",
            _ => value.ToString()
        };
    }
}
