using Microsoft.Data.Sqlite;

namespace AccardND.Server.Data;

/// <summary>
/// Accesso a SQLite: crea/apre il file e garantisce lo schema (idempotente).
/// Ogni fase del sistema account aggiunge qui i propri CREATE TABLE IF NOT EXISTS.
/// </summary>
public sealed class AccardDatabase
{
    private readonly string connectionString;

    public AccardDatabase(ServerConfig config)
    {
        string path = Path.IsPathRooted(config.DatabaseFilePath)
            ? config.DatabaseFilePath
            : Path.Combine(AppContext.BaseDirectory, config.DatabaseFilePath);

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        Initialize();
    }

    /// <summary>Apre una connessione pronta all'uso (foreign key + busy timeout attivi).</summary>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void Initialize()
    {
        using SqliteConnection connection = new(connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS accounts (
                player_id     TEXT PRIMARY KEY,
                source        TEXT NOT NULL,          -- 'password' | 'ugs'
                username      TEXT NOT NULL,
                username_ci   TEXT NOT NULL,          -- username in minuscolo, per confronto case-insensitive
                password_salt TEXT,                   -- NULL per gli account UGS
                password_hash TEXT,                   -- NULL per gli account UGS
                created_at    TEXT NOT NULL,
                last_login_at TEXT
            );

            -- Unicità dello username solo per gli account con password;
            -- i display name UGS possono coincidere.
            CREATE UNIQUE INDEX IF NOT EXISTS ux_accounts_username_ci
                ON accounts(username_ci) WHERE source='password';

            CREATE TABLE IF NOT EXISTS seasons (
                season_id INTEGER PRIMARY KEY AUTOINCREMENT,
                name      TEXT NOT NULL,
                starts_at TEXT NOT NULL,
                ends_at   TEXT,
                is_active INTEGER NOT NULL DEFAULT 0
            );

            -- Aggregati per giocatore. scope = 'lifetime' oppure 'season:<id>'.
            CREATE TABLE IF NOT EXISTS player_stats (
                player_id           TEXT NOT NULL,
                scope               TEXT NOT NULL,
                matches             INTEGER NOT NULL DEFAULT 0,
                wins                INTEGER NOT NULL DEFAULT 0,
                losses              INTEGER NOT NULL DEFAULT 0,
                forfeits            INTEGER NOT NULL DEFAULT 0,
                rounds_won          INTEGER NOT NULL DEFAULT 0,
                rounds_lost         INTEGER NOT NULL DEFAULT 0,
                current_streak      INTEGER NOT NULL DEFAULT 0,
                best_streak         INTEGER NOT NULL DEFAULT 0,
                total_match_seconds INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (player_id, scope)
            );

            -- winner: 0 = player_a, 1 = player_b, -1 = nessuno.
            CREATE TABLE IF NOT EXISTS match_history (
                match_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                season_id    INTEGER NOT NULL,
                room_code    TEXT,
                ranked       INTEGER NOT NULL DEFAULT 0,
                player_a     TEXT NOT NULL,
                player_b     TEXT NOT NULL,
                winner       INTEGER NOT NULL,
                score_a      INTEGER NOT NULL,
                score_b      INTEGER NOT NULL,
                ended_reason TEXT NOT NULL,
                started_at   TEXT,
                ended_at     TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_match_history_player_a ON match_history(player_a);
            CREATE INDEX IF NOT EXISTS ix_match_history_player_b ON match_history(player_b);

            -- Stato ranked per giocatore e stagione. tier/divisione/LP sono derivati
            -- dall'MMR: qui si conserva solo l'MMR nascosto e lo stato di piazzamento.
            CREATE TABLE IF NOT EXISTS ranked_state (
                player_id      TEXT NOT NULL,
                season_id      INTEGER NOT NULL,
                mmr            INTEGER NOT NULL,
                games_played   INTEGER NOT NULL DEFAULT 0,
                placement_done INTEGER NOT NULL DEFAULT 0,
                peak_mmr       INTEGER NOT NULL,
                updated_at     TEXT NOT NULL,
                PRIMARY KEY (player_id, season_id)
            );
            CREATE INDEX IF NOT EXISTS ix_ranked_state_ladder
                ON ranked_state(season_id, mmr DESC);

            CREATE TABLE IF NOT EXISTS profiles (
                player_id        TEXT PRIMARY KEY,
                selected_icon_id TEXT,
                bio              TEXT,
                updated_at       TEXT
            );

            -- Catalogo icone selezionabili. source: free | tier | achievement | halloffame | campaign.
            CREATE TABLE IF NOT EXISTS icons (
                icon_id    TEXT PRIMARY KEY,
                name       TEXT NOT NULL,
                source     TEXT NOT NULL,
                unlock_ref TEXT,
                sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS player_icons (
                player_id     TEXT NOT NULL,
                icon_id       TEXT NOT NULL,
                unlocked_at   TEXT NOT NULL,
                unlock_source TEXT,
                PRIMARY KEY (player_id, icon_id)
            );

            -- Mostri sconfitti in campagna (sblocco icone). monster_id = famiglia (es. 'goblin').
            CREATE TABLE IF NOT EXISTS campaign_kills (
                player_id       TEXT NOT NULL,
                monster_id      TEXT NOT NULL,
                kills           INTEGER NOT NULL DEFAULT 0,
                first_killed_at TEXT NOT NULL,
                PRIMARY KEY (player_id, monster_id)
            );

            -- Catalogo achievement e progressi per giocatore.
            CREATE TABLE IF NOT EXISTS achievements (
                achievement_id TEXT PRIMARY KEY,
                name        TEXT NOT NULL,
                description TEXT NOT NULL,
                metric      TEXT NOT NULL,       -- wins | matches | best_streak | tier
                threshold   INTEGER NOT NULL,
                reward_icon TEXT,
                sort_order  INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS player_achievements (
                player_id      TEXT NOT NULL,
                achievement_id TEXT NOT NULL,
                progress       INTEGER NOT NULL DEFAULT 0,
                unlocked_at    TEXT,
                PRIMARY KEY (player_id, achievement_id)
            );

            -- Amicizie a righe speculari (una per prospettiva) per liste O(1).
            -- status: requested (inviata da me) | incoming (ricevuta) | accepted | blocked.
            CREATE TABLE IF NOT EXISTS friends (
                owner_id   TEXT NOT NULL,
                other_id   TEXT NOT NULL,
                status     TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (owner_id, other_id)
            );
            CREATE INDEX IF NOT EXISTS ix_friends_owner ON friends(owner_id);

            -- Snapshot di fine stagione: classifica finale conservata per la Hall of Fame.
            CREATE TABLE IF NOT EXISTS hall_of_fame (
                season_id      INTEGER NOT NULL,
                player_id      TEXT NOT NULL,
                final_rank     INTEGER NOT NULL,
                final_tier     TEXT NOT NULL,
                final_division TEXT NOT NULL,
                final_mmr      INTEGER NOT NULL,
                wins           INTEGER NOT NULL DEFAULT 0,
                losses         INTEGER NOT NULL DEFAULT 0,
                snapshot_at    TEXT NOT NULL,
                PRIMARY KEY (season_id, player_id)
            );
        ";
        command.ExecuteNonQuery();
    }
}
