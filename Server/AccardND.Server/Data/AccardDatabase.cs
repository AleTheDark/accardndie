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
                source        TEXT NOT NULL,          -- 'password' | 'external' (token UGS)
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

            CREATE TABLE IF NOT EXISTS external_identities (
                provider      TEXT NOT NULL,          -- sempre 'ugs': il token e' di Unity Auth
                external_id   TEXT NOT NULL,
                player_id     TEXT NOT NULL,
                auth_method   TEXT,                   -- 'google' | 'google-play-games' | 'anonymous' | NULL
                created_at    TEXT NOT NULL,
                last_login_at TEXT,
                PRIMARY KEY (provider, external_id),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );
            CREATE INDEX IF NOT EXISTS ix_external_identities_player
                ON external_identities(player_id);

            -- Preserve existing IDs because all progression tables reference them.
            INSERT OR IGNORE INTO external_identities
                (provider, external_id, player_id, created_at, last_login_at)
            SELECT 'ugs', substr(player_id, 5), player_id, created_at, last_login_at
            FROM accounts
            WHERE source='ugs' AND player_id LIKE 'ugs:%';

            CREATE TABLE IF NOT EXISTS account_nicknames (
                player_id   TEXT PRIMARY KEY,
                nickname    TEXT NOT NULL,
                nickname_ci TEXT NOT NULL UNIQUE,
                updated_at  TEXT NOT NULL,
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Duplicate historical display names are intentionally skipped.
            -- Those players will choose a unique nickname at their next login.
            INSERT OR IGNORE INTO account_nicknames
                (player_id, nickname, nickname_ci, updated_at)
            SELECT player_id, username, username_ci, COALESCE(last_login_at, created_at)
            FROM accounts
            WHERE length(username) BETWEEN 3 AND 18;

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

            -- Progressione permanente single player. Il client puo cacheare questi dati,
            -- ma la copia autoritativa vive qui.
            CREATE TABLE IF NOT EXISTS single_player_progress (
                player_id          TEXT PRIMARY KEY,
                honey              INTEGER NOT NULL DEFAULT 0,
                account_level      INTEGER NOT NULL DEFAULT 1,
                account_experience INTEGER NOT NULL DEFAULT 0,
                account_total_experience INTEGER NOT NULL DEFAULT 0,
                account_experience_to_next_level INTEGER NOT NULL DEFAULT 100,
                pending_level_rewards INTEGER NOT NULL DEFAULT 0,
                tutorial_completed INTEGER NOT NULL DEFAULT 0,
                hardcore_unlocked  INTEGER NOT NULL DEFAULT 0,
                updated_at         TEXT NOT NULL,
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            CREATE TABLE IF NOT EXISTS single_player_unlocks (
                player_id   TEXT NOT NULL,
                unlock_type TEXT NOT NULL,
                unlock_id   TEXT NOT NULL,
                unlocked_at TEXT NOT NULL,
                PRIMARY KEY (player_id, unlock_type, unlock_id),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Ricompense in miele riscattate (tutorial/morte). Il server calcola e possiede
            -- l'importo; la riga serve per l'idempotenza e per applicare in seguito il
            -- moltiplicatore pubblicitario a una specifica reward gia concessa.
            -- reward_type: 'tutorial' | 'death'. multiplier parte da 1 e diventa 3 con l'ad.
            -- source_ref: tutorialRunId/runId per l'idempotenza. ad_impression_id: unico se non nullo.
            CREATE TABLE IF NOT EXISTS single_player_reward_claims (
                claim_id         TEXT PRIMARY KEY,
                player_id        TEXT NOT NULL,
                reward_type      TEXT NOT NULL,
                base_honey       INTEGER NOT NULL,
                base_account_experience INTEGER NOT NULL DEFAULT 0,
                multiplier       INTEGER NOT NULL DEFAULT 1,
                ad_impression_id TEXT,
                source_ref       TEXT,
                created_at       TEXT NOT NULL,
                multiplied_at    TEXT,
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );
            CREATE INDEX IF NOT EXISTS ix_reward_claims_player
                ON single_player_reward_claims(player_id, reward_type, source_ref);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_reward_claims_ad
                ON single_player_reward_claims(ad_impression_id)
                WHERE ad_impression_id IS NOT NULL;

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

            -- Storico login: una riga per ogni autenticazione riuscita. Alimenta i
            -- grafici 'login nel tempo' del pannello admin (accounts.last_login_at
            -- conserva solo l'ultimo accesso, questa tabella conserva la serie).
            CREATE TABLE IF NOT EXISTS login_events (
                event_id    INTEGER PRIMARY KEY AUTOINCREMENT,
                player_id   TEXT NOT NULL,
                provider    TEXT NOT NULL,          -- 'password' | 'ugs' | 'google' | ...
                occurred_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_login_events_time ON login_events(occurred_at);
            CREATE INDEX IF NOT EXISTS ix_login_events_player ON login_events(player_id);

            -- Storico run di campagna (single player): una riga per run. La riga nasce
            -- all'avvio della run (started_at, ended_at NULL) e viene chiusa dal sommario
            -- che il client invia con la death reward. Le righe rimaste senza ended_at
            -- sono le run abbandonate: senza di loro il pannello vedrebbe solo chi muore
            -- o vince, e chi chiude il gioco a meta' sparirebbe dalle statistiche.
            -- client_run_ref = runId lato client: lega inizio, fine e reward claim.
            CREATE TABLE IF NOT EXISTS campaign_runs (
                run_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                player_id        TEXT NOT NULL,
                client_run_ref   TEXT,
                mode             TEXT,
                chapter_id       TEXT,
                stage_id         TEXT,
                rooms_cleared    INTEGER NOT NULL DEFAULT 0,
                enemies_defeated INTEGER NOT NULL DEFAULT 0,
                bosses_defeated  INTEGER NOT NULL DEFAULT 0,
                minibosses_defeated INTEGER NOT NULL DEFAULT 0,
                defeated_boss_ids TEXT,          -- id boss/miniboss sconfitti, separati da virgola
                honey_reward     INTEGER NOT NULL DEFAULT 0,
                started_at       TEXT,           -- NULL sulle run precedenti al tracciamento dell'avvio
                ended_at         TEXT            -- NULL finche' la run e' in corso o abbandonata
            );
            -- Contatori cumulativi di campagna (single player). Aggregano quello che le righe
            -- di campaign_runs raccontano una per una, cosi' i requisiti del Santuario si
            -- valutano con una lettura invece che con una scansione dello storico.
            CREATE TABLE IF NOT EXISTS player_counters (
                player_id   TEXT NOT NULL,
                counter_key TEXT NOT NULL,
                value       INTEGER NOT NULL DEFAULT 0,
                updated_at  TEXT NOT NULL,
                PRIMARY KEY (player_id, counter_key),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Scorta permanente di consumabili acquistati al Santuario. Il conteggio cala
            -- solo quando un oggetto viene davvero usato in una run.
            CREATE TABLE IF NOT EXISTS player_consumables (
                player_id TEXT NOT NULL,
                item_id   TEXT NOT NULL,
                count     INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (player_id, item_id),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Bisaccia: quali oggetti della scorta il giocatore porta nella prossima run.
            -- La chiave primaria impone da sola la regola di un solo pezzo per tipo.
            CREATE TABLE IF NOT EXISTS player_bag (
                player_id TEXT NOT NULL,
                item_id   TEXT NOT NULL,
                PRIMARY KEY (player_id, item_id),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Copie acquistate nelle offerte giornaliere del negozio.
            CREATE TABLE IF NOT EXISTS player_shop_offer_purchases (
                player_id TEXT NOT NULL,
                rotation  TEXT NOT NULL,
                item_id   TEXT NOT NULL,
                count     INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (player_id, rotation, item_id),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Quest della taverna assegnate a un giocatore per una data (UTC).
            -- baseline e' il valore del contatore nel momento dell'assegnazione: il progresso
            -- e' la differenza, cosi' non serve un secondo sistema di tracciamento.
            CREATE TABLE IF NOT EXISTS player_tavern_quests (
                player_id  TEXT NOT NULL,
                day        TEXT NOT NULL,          -- yyyy-MM-dd in UTC
                quest_id   TEXT NOT NULL,
                baseline   INTEGER NOT NULL DEFAULT 0,
                claimed_at TEXT,
                PRIMARY KEY (player_id, day, quest_id),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Premio di giornata (tutte le quest completate). Sta a parte invece che come
            -- riga speciale di player_tavern_quests: non ha un contatore ne' una soglia, e
            -- mescolarlo alle quest costringerebbe ogni lettura a filtrarlo via.
            CREATE TABLE IF NOT EXISTS player_tavern_bonus (
                player_id  TEXT NOT NULL,
                day        TEXT NOT NULL,          -- yyyy-MM-dd in UTC
                claimed_at TEXT NOT NULL,
                PRIMARY KEY (player_id, day),
                FOREIGN KEY (player_id) REFERENCES accounts(player_id)
            );

            -- Gli indici di campaign_runs li crea MigrateCampaignRuns: la tabella puo'
            -- essere ricostruita dalla migrazione, e una DROP TABLE si porta via gli indici.

            -- Risposte gia' date alle richieste che mutano lo stato, per (giocatore, requestId).
            -- Serve ai rinvii dopo una caduta di rete: la seconda copia della richiesta
            -- riceve questa riga invece di rieseguire l'acquisto. Sta su disco e non in
            -- memoria perche' il client puo' rinviare al riavvio successivo, anche giorni
            -- dopo, e un deploy del server nel frattempo non deve aprire una finestra di
            -- doppia esecuzione. Nessuna FK: le righe scadono da sole.
            CREATE TABLE IF NOT EXISTS request_dedup (
                player_id     TEXT NOT NULL,
                request_id    TEXT NOT NULL,
                reply_type    TEXT NOT NULL,
                reply_payload TEXT NOT NULL,
                expires_at    TEXT NOT NULL,
                PRIMARY KEY (player_id, request_id)
            );
            CREATE INDEX IF NOT EXISTS ix_request_dedup_expiry ON request_dedup(expires_at);

            -- Impostazioni cambiabili a caldo dal pannello admin. Stanno qui e non in
            -- serverconfig.json perche' quel file viene sovrascritto dal deploy: una
            -- versione client alzata dal pannello deve sopravvivere alla pubblicazione
            -- del binario successivo.
            CREATE TABLE IF NOT EXISTS server_settings (
                key        TEXT PRIMARY KEY,
                value      TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
        ";
        command.ExecuteNonQuery();
        AddColumnIfMissing(connection, "single_player_progress", "account_level", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "single_player_progress", "account_experience", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "single_player_progress", "account_total_experience", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "single_player_progress", "account_experience_to_next_level", "INTEGER NOT NULL DEFAULT 100");
        AddColumnIfMissing(connection, "single_player_progress", "pending_level_rewards", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "single_player_reward_claims", "base_account_experience", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "campaign_runs", "minibosses_defeated", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "campaign_runs", "defeated_boss_ids", "TEXT");
        MigrateCampaignRuns(connection);
        // Come si e' autenticato l'account esterno: 'google', 'google-play-games',
        // 'anonymous'... NULL sulle righe create prima di questa colonna, si
        // popola al primo login successivo.
        AddColumnIfMissing(connection, "external_identities", "auth_method", "TEXT");
        // Mail dell'account Google, letta dall'ID token verificato. NULL sulle righe
        // create prima di questa colonna e su tutto cio' che non e' Google (gli
        // account Play Games una mail non l'hanno mai esposta): si popola al primo
        // login Google successivo.
        AddColumnIfMissing(connection, "external_identities", "email", "TEXT");
    }

    /// <summary>
    /// Porta <c>campaign_runs</c> alla forma "una riga per run": aggiunge <c>started_at</c> e
    /// rende <c>ended_at</c> nullable, perche' una run aperta non ha ancora una fine e il
    /// sentinella (stringa vuota, data finta) inquinerebbe ogni query sullo storico.
    /// SQLite non sa togliere un NOT NULL con ALTER TABLE: serve la ricostruzione.
    /// Gli indici si creano qui perche' la DROP TABLE se li porta via.
    /// </summary>
    private static void MigrateCampaignRuns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "campaign_runs", "started_at", "TEXT");

        if (IsColumnNotNull(connection, "campaign_runs", "ended_at"))
        {
            using SqliteCommand rebuild = connection.CreateCommand();
            rebuild.CommandText = @"
                BEGIN;
                CREATE TABLE campaign_runs_new (
                    run_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    player_id        TEXT NOT NULL,
                    client_run_ref   TEXT,
                    mode             TEXT,
                    chapter_id       TEXT,
                    stage_id         TEXT,
                    rooms_cleared    INTEGER NOT NULL DEFAULT 0,
                    enemies_defeated INTEGER NOT NULL DEFAULT 0,
                    bosses_defeated  INTEGER NOT NULL DEFAULT 0,
                    minibosses_defeated INTEGER NOT NULL DEFAULT 0,
                    defeated_boss_ids TEXT,
                    honey_reward     INTEGER NOT NULL DEFAULT 0,
                    started_at       TEXT,
                    ended_at         TEXT
                );
                INSERT INTO campaign_runs_new
                    (run_id, player_id, client_run_ref, mode, chapter_id, stage_id,
                     rooms_cleared, enemies_defeated, bosses_defeated, minibosses_defeated,
                     defeated_boss_ids, honey_reward, started_at, ended_at)
                SELECT run_id, player_id, client_run_ref, mode, chapter_id, stage_id,
                       rooms_cleared, enemies_defeated, bosses_defeated, minibosses_defeated,
                       defeated_boss_ids, honey_reward, started_at, ended_at
                FROM campaign_runs;
                DROP TABLE campaign_runs;
                ALTER TABLE campaign_runs_new RENAME TO campaign_runs;
                COMMIT;";
            rebuild.ExecuteNonQuery();
        }

        using SqliteCommand indexes = connection.CreateCommand();
        indexes.CommandText = @"
            CREATE INDEX IF NOT EXISTS ix_campaign_runs_time ON campaign_runs(ended_at);
            CREATE INDEX IF NOT EXISTS ix_campaign_runs_start ON campaign_runs(started_at);
            CREATE INDEX IF NOT EXISTS ix_campaign_runs_player ON campaign_runs(player_id);
            -- La chiusura della run cerca la riga aperta per (giocatore, runId del client).
            CREATE INDEX IF NOT EXISTS ix_campaign_runs_ref ON campaign_runs(player_id, client_run_ref);";
        indexes.ExecuteNonQuery();
    }

    private static bool IsColumnNotNull(SqliteConnection connection, string tableName, string columnName)
    {
        using SqliteCommand check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({tableName})";
        using SqliteDataReader reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return reader.GetInt32(3) != 0;
        }
        return false;
    }

    private static void AddColumnIfMissing(
        SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using (SqliteCommand check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({tableName})";
            using SqliteDataReader reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using SqliteCommand alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
        alter.ExecuteNonQuery();
    }
}
