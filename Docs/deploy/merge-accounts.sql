-- AcCardNDie - fusione di due account nel database SQLite del server.
--
-- A cosa serve: un giocatore che si ritrova due account (tipicamente il vecchio
-- Google Play Games, o il fallback con password, da una parte e il nuovo login
-- Google dall'altra) viene ricondotto a uno solo, spostando progressi,
-- statistiche, storico e nickname sull'account che sopravvive.
--
-- COSA QUESTO SCRIPT NON PUO' FARE
--   1. Non sa dirti quali due account sono la stessa persona: glielo dici tu con
--      i due player_id. La mail e' registrata solo dai login Google recenti, e
--      per gli account Play Games non e' mai esistita.
--   2. L'account di destinazione deve GIA' ESISTERE. Il player_id di un account
--      Google nasce da Unity Authentication al primo accesso: non e' prevedibile,
--      quindi non si puo' preparare la fusione in anticipo. L'ordine e': il
--      giocatore entra con Google (prendera' un nickname provvisorio, perche' il
--      suo e' ancora occupato dal doppione) e SOLO DOPO si esegue questo script,
--      che gli restituisce il nickname originale.
--
-- Come si usa, sul VPS:
--
--   sudo systemctl stop accardnd
--   cp /percorso/accardnd.db /percorso/accardnd.db.bak-$(date +%F)
--   sqlite3 /percorso/accardnd.db < merge-accounts.sql
--   sudo systemctl start accardnd
--
-- Fermare il servizio non e' pignoleria: il DB gira in WAL, e copiarlo mentre il
-- server scrive produce un backup incoerente.
--
-- Se uno dei due player_id non esiste, o sono lo stesso, lo script NON tocca
-- nulla: la tabella dei parametri resta vuota e ogni istruzione diventa un
-- colpo a vuoto. Il controllo stampato subito sotto te lo dice.

-- ===========================================================================
-- PARAMETRI - sostituisci i due player_id qui sotto.
--   source_id = l'account che sparisce (il doppione)
--   target_id = l'account che resta (quello Google buono)
-- ===========================================================================
CREATE TEMP TABLE merge_params AS
SELECT s.player_id AS source_id, t.player_id AS target_id
  FROM accounts s, accounts t
 WHERE s.player_id = 'SOSTITUISCI-CON-IL-PLAYER-ID-DEL-DOPPIONE'
   AND t.player_id = 'SOSTITUISCI-CON-IL-PLAYER-ID-CHE-RESTA'
   AND s.player_id <> t.player_id;

SELECT CASE WHEN (SELECT COUNT(*) FROM merge_params) = 1
            THEN 'OK: i due account esistono, la fusione procede.'
            ELSE 'FERMO: player_id inesistente o uguale. Nessuna modifica fatta.'
       END AS controllo;

-- ---------------------------------------------------------------------------
-- Anteprima: guarda cosa stai per fondere.
-- ---------------------------------------------------------------------------
SELECT 'PRIMA' AS quando, a.player_id, a.username, a.source,
       (SELECT ei.auth_method FROM external_identities ei
         WHERE ei.player_id = a.player_id ORDER BY ei.last_login_at DESC LIMIT 1) AS metodo,
       (SELECT ei.email FROM external_identities ei
         WHERE ei.player_id = a.player_id AND ei.email IS NOT NULL
         ORDER BY ei.last_login_at DESC LIMIT 1) AS mail,
       (SELECT n.nickname FROM account_nicknames n WHERE n.player_id = a.player_id) AS nickname,
       COALESCE(sp.honey, 0) AS miele,
       COALESCE(st.matches, 0) AS partite,
       COALESCE(st.wins, 0) AS vittorie,
       a.last_login_at
  FROM accounts a
  LEFT JOIN single_player_progress sp ON sp.player_id = a.player_id
  LEFT JOIN player_stats st ON st.player_id = a.player_id AND st.scope = 'lifetime'
 WHERE a.player_id IN (SELECT source_id FROM merge_params)
    OR a.player_id IN (SELECT target_id FROM merge_params);

-- ===========================================================================
-- FUSIONE. Tutto dentro una transazione: o passa tutto, o niente.
-- Niente UPDATE ... FROM: solo sottoquery correlate, che funzionano anche sulle
-- versioni piu' vecchie di sqlite3.
-- ===========================================================================
PRAGMA foreign_keys = ON;
BEGIN;

-- --- Progressione single player --------------------------------------------
-- Se il superstite non ha una riga, quella del doppione diventa sua (la UPDATE
-- OR IGNORE riesce). Se ce l'ha, la UPDATE fallisce sulla primary key, la riga
-- del doppione resta dov'e' e la si fonde a mano subito sotto.
UPDATE OR IGNORE single_player_progress
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

-- Il miele si somma. I campi di livello no: livello, esperienza e soglia sono
-- coerenti solo tra loro, quindi si tiene in blocco la riga piu' avanzata invece
-- di sommare numeri che poi non tornerebbero.
UPDATE single_player_progress
   SET honey = honey + (SELECT s.honey FROM single_player_progress s
                         WHERE s.player_id = (SELECT source_id FROM merge_params)),
       account_level = CASE WHEN (SELECT s.account_total_experience FROM single_player_progress s
                                   WHERE s.player_id = (SELECT source_id FROM merge_params))
                                 > account_total_experience
                            THEN (SELECT s.account_level FROM single_player_progress s
                                   WHERE s.player_id = (SELECT source_id FROM merge_params))
                            ELSE account_level END,
       account_experience = CASE WHEN (SELECT s.account_total_experience FROM single_player_progress s
                                        WHERE s.player_id = (SELECT source_id FROM merge_params))
                                      > account_total_experience
                                 THEN (SELECT s.account_experience FROM single_player_progress s
                                        WHERE s.player_id = (SELECT source_id FROM merge_params))
                                 ELSE account_experience END,
       account_experience_to_next_level =
           CASE WHEN (SELECT s.account_total_experience FROM single_player_progress s
                       WHERE s.player_id = (SELECT source_id FROM merge_params))
                     > account_total_experience
                THEN (SELECT s.account_experience_to_next_level FROM single_player_progress s
                       WHERE s.player_id = (SELECT source_id FROM merge_params))
                ELSE account_experience_to_next_level END,
       account_total_experience =
           MAX(account_total_experience,
               (SELECT s.account_total_experience FROM single_player_progress s
                 WHERE s.player_id = (SELECT source_id FROM merge_params))),
       tutorial_completed =
           MAX(tutorial_completed,
               (SELECT s.tutorial_completed FROM single_player_progress s
                 WHERE s.player_id = (SELECT source_id FROM merge_params))),
       hardcore_unlocked =
           MAX(hardcore_unlocked,
               (SELECT s.hardcore_unlocked FROM single_player_progress s
                 WHERE s.player_id = (SELECT source_id FROM merge_params))),
       updated_at =
           MAX(updated_at,
               (SELECT s.updated_at FROM single_player_progress s
                 WHERE s.player_id = (SELECT source_id FROM merge_params)))
 WHERE player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM single_player_progress s
                WHERE s.player_id = (SELECT source_id FROM merge_params));

DELETE FROM single_player_progress WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Statistiche PvP (una riga per scope: lifetime, season:<id>) ------------
UPDATE OR IGNORE player_stats
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE player_stats AS t
   SET matches  = t.matches  + (SELECT s.matches  FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope),
       wins     = t.wins     + (SELECT s.wins     FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope),
       losses   = t.losses   + (SELECT s.losses   FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope),
       forfeits = t.forfeits + (SELECT s.forfeits FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope),
       rounds_won  = t.rounds_won  + (SELECT s.rounds_won  FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope),
       rounds_lost = t.rounds_lost + (SELECT s.rounds_lost FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope),
       -- la striscia in corso non e' sommabile: appartiene a una sola linea di partite
       best_streak = MAX(t.best_streak, (SELECT s.best_streak FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope)),
       total_match_seconds = t.total_match_seconds + (SELECT s.total_match_seconds FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope)
 WHERE t.player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM player_stats s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.scope = t.scope);

DELETE FROM player_stats WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Ranked: l'MMR non si somma, vince quello con piu' partite alle spalle ---
UPDATE OR IGNORE ranked_state
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE ranked_state AS t
   SET mmr = CASE WHEN (SELECT s.games_played FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id) > t.games_played
                  THEN (SELECT s.mmr FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id)
                  ELSE t.mmr END,
       games_played   = t.games_played + (SELECT s.games_played FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id),
       placement_done = MAX(t.placement_done, (SELECT s.placement_done FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id)),
       peak_mmr       = MAX(t.peak_mmr,       (SELECT s.peak_mmr       FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id)),
       updated_at     = MAX(t.updated_at,     (SELECT s.updated_at     FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id))
 WHERE t.player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM ranked_state s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.season_id = t.season_id);

DELETE FROM ranked_state WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Achievement: progresso migliore, sblocco piu' vecchio -------------------
UPDATE OR IGNORE player_achievements
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE player_achievements AS t
   SET progress = MAX(t.progress, (SELECT s.progress FROM player_achievements s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.achievement_id = t.achievement_id)),
       unlocked_at = MIN(COALESCE(t.unlocked_at, (SELECT s.unlocked_at FROM player_achievements s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.achievement_id = t.achievement_id)),
                         COALESCE((SELECT s.unlocked_at FROM player_achievements s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.achievement_id = t.achievement_id), t.unlocked_at))
 WHERE t.player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM player_achievements s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.achievement_id = t.achievement_id);

DELETE FROM player_achievements WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Contatori campagna (requisiti del Santuario): si sommano ---------------
UPDATE OR IGNORE player_counters
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE player_counters AS t
   SET value = t.value + (SELECT s.value FROM player_counters s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.counter_key = t.counter_key),
       updated_at = MAX(t.updated_at, (SELECT s.updated_at FROM player_counters s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.counter_key = t.counter_key))
 WHERE t.player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM player_counters s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.counter_key = t.counter_key);

DELETE FROM player_counters WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Mostri sconfitti: uccisioni sommate, prima uccisione la piu' vecchia ----
UPDATE OR IGNORE campaign_kills
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE campaign_kills AS t
   SET kills = t.kills + (SELECT s.kills FROM campaign_kills s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.monster_id = t.monster_id),
       first_killed_at = MIN(t.first_killed_at, (SELECT s.first_killed_at FROM campaign_kills s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.monster_id = t.monster_id))
 WHERE t.player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM campaign_kills s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.monster_id = t.monster_id);

DELETE FROM campaign_kills WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Scorta consumabili: si somma il conteggio ------------------------------
UPDATE OR IGNORE player_consumables
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE player_consumables AS t
   SET count = t.count + (SELECT s.count FROM player_consumables s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.item_id = t.item_id)
 WHERE t.player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM player_consumables s WHERE s.player_id = (SELECT source_id FROM merge_params) AND s.item_id = t.item_id);

DELETE FROM player_consumables WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Tabelle di sola appartenenza: si sposta quel che non collide -----------
-- (sblocchi, bisaccia, icone, quest e premio taverna, hall of fame, profilo)
UPDATE OR IGNORE single_player_unlocks SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM single_player_unlocks WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE OR IGNORE player_bag SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM player_bag WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE OR IGNORE player_icons SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM player_icons WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE OR IGNORE player_tavern_quests SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM player_tavern_quests WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE OR IGNORE player_tavern_bonus SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM player_tavern_bonus WHERE player_id = (SELECT source_id FROM merge_params);

UPDATE OR IGNORE hall_of_fame SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM hall_of_fame WHERE player_id = (SELECT source_id FROM merge_params);

-- Il profilo (icona scelta, bio) passa solo se il superstite non ne ha uno suo.
UPDATE OR IGNORE profiles SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM profiles WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Storico: si ripunta e basta, nessuna collisione possibile --------------
UPDATE single_player_reward_claims SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
UPDATE campaign_runs SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
UPDATE login_events  SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
UPDATE match_history SET player_a  = (SELECT target_id FROM merge_params) WHERE player_a  = (SELECT source_id FROM merge_params);
UPDATE match_history SET player_b  = (SELECT target_id FROM merge_params) WHERE player_b  = (SELECT source_id FROM merge_params);

-- --- Amicizie: righe speculari, con doppioni e auto-amicizie da ripulire ----
UPDATE OR IGNORE friends SET owner_id = (SELECT target_id FROM merge_params) WHERE owner_id = (SELECT source_id FROM merge_params);
UPDATE OR IGNORE friends SET other_id = (SELECT target_id FROM merge_params) WHERE other_id = (SELECT source_id FROM merge_params);
DELETE FROM friends
 WHERE owner_id = (SELECT source_id FROM merge_params)
    OR other_id = (SELECT source_id FROM merge_params);
-- I due account potevano essersi aggiunti a vicenda: dopo la fusione sarebbe una
-- riga "amico di se' stesso".
DELETE FROM friends
 WHERE owner_id = other_id
   AND owner_id = (SELECT target_id FROM merge_params);

-- --- Identita' esterne: restano attaccate al superstite come storico --------
-- La riga Google Play Games non tornera' mai a fare login (il provider e' stato
-- dismesso), ma tenerla ricorda da dove veniva l'account.
UPDATE OR IGNORE external_identities SET player_id = (SELECT target_id FROM merge_params) WHERE player_id = (SELECT source_id FROM merge_params);
DELETE FROM external_identities WHERE player_id = (SELECT source_id FROM merge_params);

-- --- Nickname: il pezzo per cui di solito si fa tutta questa fatica ---------
-- Il nome del doppione passa al superstite. Prima si libera quello provvisorio
-- che il superstite si era preso, altrimenti l'indice unico su nickname_ci
-- rifiuterebbe lo spostamento.
DELETE FROM account_nicknames
 WHERE player_id = (SELECT target_id FROM merge_params)
   AND EXISTS (SELECT 1 FROM account_nicknames
                WHERE player_id = (SELECT source_id FROM merge_params));

UPDATE OR IGNORE account_nicknames
   SET player_id = (SELECT target_id FROM merge_params)
 WHERE player_id = (SELECT source_id FROM merge_params);

DELETE FROM account_nicknames WHERE player_id = (SELECT source_id FROM merge_params);

-- Anche lo username storico segue il nickname, cosi' il pannello admin mostra il
-- nome giusto, e la data di registrazione diventa la piu' vecchia delle due.
-- Non si tocca se il superstite e' un account con password, dove username_ci ha
-- un indice unico da rispettare.
UPDATE accounts
   SET username    = (SELECT s.username    FROM accounts s WHERE s.player_id = (SELECT source_id FROM merge_params)),
       username_ci = (SELECT s.username_ci FROM accounts s WHERE s.player_id = (SELECT source_id FROM merge_params)),
       created_at  = MIN(created_at,
                         (SELECT s.created_at FROM accounts s WHERE s.player_id = (SELECT source_id FROM merge_params)))
 WHERE player_id = (SELECT target_id FROM merge_params)
   AND source <> 'password';

-- --- Il doppione sparisce ---------------------------------------------------
DELETE FROM accounts WHERE player_id = (SELECT source_id FROM merge_params);

COMMIT;

-- ---------------------------------------------------------------------------
-- Verifica: deve restare una sola riga, con i totali sommati.
-- ---------------------------------------------------------------------------
SELECT 'DOPO' AS quando, a.player_id, a.username, a.source,
       (SELECT n.nickname FROM account_nicknames n WHERE n.player_id = a.player_id) AS nickname,
       COALESCE(sp.honey, 0) AS miele,
       COALESCE(st.matches, 0) AS partite,
       COALESCE(st.wins, 0) AS vittorie
  FROM accounts a
  LEFT JOIN single_player_progress sp ON sp.player_id = a.player_id
  LEFT JOIN player_stats st ON st.player_id = a.player_id AND st.scope = 'lifetime'
 WHERE a.player_id IN (SELECT source_id FROM merge_params)
    OR a.player_id IN (SELECT target_id FROM merge_params);

-- Righe orfane: deve restituire 0 dappertutto.
SELECT 'ORFANI' AS controllo,
       (SELECT COUNT(*) FROM single_player_progress WHERE player_id NOT IN (SELECT player_id FROM accounts)) AS progressi,
       (SELECT COUNT(*) FROM player_stats           WHERE player_id NOT IN (SELECT player_id FROM accounts)) AS statistiche,
       (SELECT COUNT(*) FROM account_nicknames      WHERE player_id NOT IN (SELECT player_id FROM accounts)) AS nickname,
       (SELECT COUNT(*) FROM external_identities    WHERE player_id NOT IN (SELECT player_id FROM accounts)) AS identita,
       (SELECT COUNT(*) FROM player_icons           WHERE player_id NOT IN (SELECT player_id FROM accounts)) AS icone,
       (SELECT COUNT(*) FROM friends WHERE owner_id NOT IN (SELECT player_id FROM accounts)
                                        OR other_id NOT IN (SELECT player_id FROM accounts)) AS amicizie;

DROP TABLE merge_params;
