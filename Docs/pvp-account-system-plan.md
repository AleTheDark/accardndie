# Piano — Account, Profili, Ranked, Statistiche, Icone, Amici, Hall of Fame, Stagioni

> Sistema di progressione e social per il PvP di AccardND. Server .NET autoritativo
> (`Server/AccardND.Server`, VPS IONOS `217.160.212.85:5017`), client Unity, dati su **SQLite**.
> Decisioni: tier a leghe derivati da **MMR nascosto**; icone sbloccabili (tier / achievement /
> Hall of Fame / **uccisioni mostri in campagna**); **stagioni trimestrali con soft reset**.

## 0. Principi

- **Il server è la sola fonte di verità** per account, MMR, tier, stat, unlock, amici.
  Il client non calcola mai il rank: lo riceve.
- **Retrocompatibilità**: `AccountService` mantiene la stessa firma pubblica; il resto del
  `MessageRouter` cambia il minimo. Auth UGS (`auth.ugs`) e legacy password restano.
- **Incrementale**: ogni fase è rilasciabile da sola e non rompe il match esistente.
- **Idempotenza** su tutti gli unlock (icone/achievement) e sul recording del match.

---

## 1. Database (SQLite)

**Tecnologia:** `Microsoft.Data.Sqlite` con **SQL diretto** e schema idempotente
(`CREATE TABLE IF NOT EXISTS`), racchiuso in una classe `AccardDatabase`. Un solo file
`accardnd.db` accanto al binario in `/opt/accardnd`. Attivare **WAL** (`PRAGMA journal_mode=WAL`)
+ `busy_timeout` per letture concorrenti col match loop. Backup = copia del file (systemd timer opzionale).

**Perché SQL diretto e non EF Core:** il codebase è volutamente hand-rolled (PBKDF2, JSON a mano)
e il deploy è un **single-file self-contained** su VPS da 2 GB; SQL diretto evita il tooling
design-time e mantiene il binario piccolo. L'evoluzione dello schema fase-per-fase = aggiungere
statement idempotenti in `AccardDatabase.Initialize()`. Le query di leaderboard/hall of fame sono
semplici in SQL puro. (Se in futuro le query diventano complesse si può introdurre EF Core.)

### Schema (tabelle)

```
accounts        player_id PK, username UNIQUE (CI), ugs_player_id NULL, password_salt NULL,
                password_hash NULL, created_at, last_login_at
                -- password_* NULL per account solo-UGS; ugs_player_id NULL per solo-legacy

profiles        player_id PK/FK, display_name, selected_icon_id FK, bio NULL,
                title_id NULL, updated_at

seasons         season_id PK, name, starts_at, ends_at, is_active
                -- una sola is_active alla volta

ranked_state    player_id + season_id  (PK composta),
                mmr, tier, division, league_points, games_played, placement_done,
                peak_mmr, peak_tier, updated_at
                -- tier/division/league_points sono DERIVATI da mmr (vedi §4) ma
                --   memorizzati per query leaderboard veloci e per lo storico

player_stats    player_id + scope (PK)  scope = 'lifetime' | 'season:<id>'
                matches, wins, losses, forfeits, rounds_won, rounds_lost,
                current_streak, best_streak, total_match_seconds,
                per_class_json  -- {classId: {picked, wins}}

match_history   match_id PK, season_id, room_code, player_a, player_b, winner (0/1/-1),
                score_a, score_b, ended_reason ('normal'|'forfeit'|'disconnect'|'timeout'),
                mmr_a_before, mmr_a_after, mmr_b_before, mmr_b_after,
                started_at, ended_at

icons           icon_id PK, name, source ('free'|'tier'|'achievement'|'halloffame'|'campaign'),
                unlock_ref NULL  -- es. tier='champion', achievement id, monster id
campaign_kills  player_id + monster_id (PK), kills, first_killed_at
player_icons    player_id + icon_id (PK), unlocked_at, unlock_source

achievements    achievement_id PK, name, description, criteria_json, hidden
player_achiev.  player_id + achievement_id (PK), progress, unlocked_at NULL

friends         player_id + friend_id (PK), status ('pending'|'accepted'|'blocked'),
                requested_by, created_at, updated_at
                -- una riga per relazione ordinata (min,max) + campo requested_by,
                --   oppure due righe speculari: scegliere due righe speculari per query O(1)

hall_of_fame    season_id + player_id (PK), final_rank, final_tier, final_division,
                final_mmr, wins, losses, snapshot_at
```

**Migrazione da `accounts.json`:** al primo boot con DB vuoto, importare gli `StoredAccount`
esistenti in `accounts`, creare un `profiles` e un `player_stats` lifetime a zero per ciascuno.
Poi `accounts.json` diventa read-only di fallback (o si archivia).

---

## 2. Lato server — servizi e wiring

Nuovi singleton in `Program.cs` (DI), tutti dietro un `AppDbContextFactory`:

| Servizio | Responsabilità |
|---|---|
| `AppDbContext` (EF Core) | schema + migrazioni |
| `AccountService` *(refactor)* | come oggi ma su DB; PBKDF2 invariato |
| `ProfileService` | profilo, icona selezionata, titolo |
| `RankedService` | calcolo MMR (Elo), mappatura MMR→tier/div/LP, placement |
| `StatsService` | aggregazione stat lifetime + stagione |
| `MatchResultRecorder` | aggancio a fine match → scrive match_history, aggiorna MMR/stat, valuta unlock |
| `SeasonService` | stagione attiva, rollover (soft reset + snapshot) |
| `HallOfFameService` | snapshot e query storici |
| `UnlockService` | icone + achievement (idempotente) |
| `FriendService` | richieste, accept, block, lista |
| `PresenceRegistry` | mappa in-memory player_id→ClientConnection, stato online/in-match |

### Aggancio fine match (cardine)

In `MatchSession.DispatchAsync`, quando `engine.Phase == Finished`:

```
var result = new MatchOutcome(
    room.Host.Identity, room.Guest.Identity,
    engine.MatchWinner, engine.WinsOf(0), engine.WinsOf(1),
    endedReason, startedAt, DateTime.UtcNow);
await matchResultRecorder.RecordAsync(result);   // callback iniettato nel Room/Session
```

`RecordAsync` (transazione unica):
1. `RankedService.ApplyMatch` → nuovi MMR, tier/div/LP per entrambi.
2. `StatsService.Apply` → W/L, streak, rounds, per-class, durata.
3. `UnlockService.EvaluateAfterMatch` → icone da tier appena raggiunto, achievement.
4. Insert in `match_history`.
5. A ciascun giocatore un messaggio **`match.result`** con delta LP, tier prima/dopo,
   eventuale promozione/retrocessione, unlock ottenuti → overlay di fine partita.

> Solo le partite **ranked** toccano MMR/tier. Stanze con codice = amichevoli (contano solo
> in stat "amichevoli", niente MMR). Il matchmaking a coda = ranked. Flag `ranked` sul match.

### Aggiunte al protocollo (`NetProtocol/MessageTypes` + DTO)

```
profile.get / profile.data            profilo completo (icona, tier, titolo, stat sintetiche)
profile.setIcon / profile.setBio
stats.get / stats.data                lifetime + stagione corrente
ranked.get / ranked.data              tier, division, LP, MMR mascherato in progress %
leaderboard.get / leaderboard.data    paginata, stagione corrente
halloffame.list / halloffame.data     per season_id
icons.list / icons.data               catalogo + set sbloccato
achievements.get / achievements.data
campaign.reportKills                  client→server: mostri uccisi in campagna (unlock icone)
friends.list / friends.add / friends.respond / friends.remove / friends.data
friends.presence                      push: amico online/offline/in-match
friends.challenge                     invito diretto (riusa il room code)
match.result                          push post-partita: delta rank + unlock
```

Tutte le richieste che non sono auth/rules restano dietro `IsAuthenticated` come oggi.

---

## 3. Lato client (Unity)

Estendere `PvpServerClient` con gli handler dei nuovi messaggi (o un `PvpSocialClient`
parallelo che condivide la connessione). Nuove schermate, stile code-generated coerente con
`PvpLobbyScreen` (o portate nella grafica campagna come già fatto per il battlefield):

- **Profilo** — icona grande, nome, badge tier, titolo, riepilogo stat, achievement recenti.
- **Ranked / Ladder** — tuo tier+divisione, barra LP, leaderboard paginata della stagione.
- **Hall of Fame** — selettore stagione, top N con tier finale.
- **Amici** — lista con presenza (online / in menu / in match), aggiungi/rimuovi, **sfida**.
- **Selettore icone** — griglia icone sbloccate + anteprima bloccate con requisito.
- **Overlay fine match** — animazione guadagno/perdita LP, promozione, unlock ottenuti.

**Aggancio campagna → profilo (icone mostri):** quando in campagna si sconfigge un mostro,
registrare l'evento in un save locale (`CampaignKillTracker`, PlayerPrefs/file). Al login PvP
il client invia `campaign.reportKills` con l'insieme dei `monster_id` sconfitti; il server
concede le icone corrispondenti in modo idempotente e risponde con gli unlock nuovi. È cosmetico
e a basso rischio: ci si può fidare del client, validando gli id contro il catalogo mostri.

Caching locale del profilo per non ri-richiedere a ogni schermata; invalidazione su `match.result`.

---

## 4. Ranked: MMR nascosto → tier a leghe

- **MMR** = Elo. Start 1000. K=40 durante i **placement** (prime ~5 partite, tier nascosto),
  K=24 a regime, K=16 oltre un MMR alto (anti-inflazione).
  `expected = 1/(1+10^((mmrOpp-mmr)/400))`, `mmr += K*(score-expected)` con score 1/0.
- **Tier** (bande di MMR crescenti), 5 livelli attuali (rinominabili: sono in `serverconfig.json`,
  non hard-coded), ognuno con **divisioni IV→I**:
  `Nabbo → Apprendista → Esperto → Divino → Onnipotente`.
- **League Points (LP)** = posizione dentro la banda del tier:
  `LP = round((mmr - bandFloor) / (bandCeil - bandFloor) * 100)`. Promozione/retrocessione
  automatiche al superamento dei confini di banda → così **il tier "corrisponde" sempre al MMR
  nascosto**, come richiesto, senza un'economia LP separata da mantenere.
- `match.result` porta: `tierBefore/after`, `divisionBefore/after`, `lpDelta`, `promoted`,
  `demoted`. Il client anima solo ciò che riceve.
- Confini di banda, K-factor e nomi tier **tutti in `serverconfig.json`** (come le altre regole).

---

## 5. Stagioni (trimestrali, soft reset)

- Tabella `seasons`; una attiva. Durata ~3 mesi (date in config).
- **Rollover** (all'avvio server + timer orario `IHostedService` che confronta `now` con `ends_at`):
  1. **Snapshot Hall of Fame**: top N + tier finale di tutti → `hall_of_fame`.
  2. **Reward** di fine stagione: icone/titoli per tier raggiunto (via `UnlockService`).
  3. **Soft reset MMR**: `newMmr = target + (oldMmr - target) * 0.5` (target ~1200, compressione
     e target da config). Nuova riga `ranked_state` per la nuova stagione; `placement_done=false`.
  4. Reset `player_stats` di scope stagione (il **lifetime resta** e continua ad accumulare).
  5. Attiva la nuova stagione.
- Idempotente: se il server riparte a rollover già fatto, non lo ripete (guardia su `is_active`
  + `snapshot_at`).

---

## 6. Hall of Fame

- Snapshot per stagione conservato in `hall_of_fame` → le stagioni passate restano consultabili.
- `halloffame.list` (stagioni disponibili) + `halloffame.data(season_id)` paginata con tier
  finale, MMR finale, W/L, ranking. Include il "personal best" del richiedente.
- Le prime posizioni concedono **icone/titoli esclusivi** (source `halloffame`), non riottenibili.

---

## 7. Icone e unlock

Sorgenti (campo `icons.source`):
- **free** — set base selezionabile subito.
- **tier** — sbloccata al raggiungimento del tier (in qualsiasi stagione).
- **achievement** — legata a un achievement.
- **halloffame** — piazzamenti top di fine stagione.
- **campaign** — icona del mostro sbloccata **uccidendolo in campagna** (`campaign.reportKills`).

`UnlockService` idempotente: `player_icons` con `unlock_source`. Ogni concessione nuova torna al
client (overlay). Il selettore mostra le bloccate con il requisito ("Raggiungi tier Champion",
"Sconfiggi lo Scheletro in campagna", ecc.).

---

## 8. Statistiche

- **Lifetime** + **per-stagione**: partite, W/L, win rate, streak corrente/migliore, round
  vinti/persi, forfeit, durata media, **win rate per classe** (`per_class_json`).
- Aggregati precalcolati in `player_stats` (aggiornati a fine match); `match_history` resta per
  dettaglio e ricalcoli. `stats.data` serve la vista profilo.

---

## 9. Amici e presenza

- `friends`: richiesta → `pending`, `friends.respond` accetta/rifiuta, `remove`, `block`.
- **Presenza** in-memory (`PresenceRegistry`, popolato da `ClientConnection.Identity` a login e
  ripulito on-disconnect in `OnDisconnectedAsync`): online / in menu / in match. Push
  `friends.presence` agli amici online quando cambia stato.
- **Sfida amico** (`friends.challenge`): genera un room code e lo consegna all'amico → riusa
  tutto il flusso stanze/`StartMatchAsync` esistente. Amichevole (non tocca MMR).

---

## 10. Roadmap di implementazione (fasi rilasciabili)

| Fase | Contenuto | Rischio |
|---|---|---|
| **0. Fondamenta DB** | SQLite+EF Core, migrazione `accounts.json`, refactor `AccountService` (nessun cambio di comportamento) | basso |
| **1. Match recording + stat** | `MatchResultRecorder`, `match_history`, `player_stats`, `stats.get/data`, vista profilo minima | basso |
| **2. Ranked** | `RankedService` (Elo+tier), match a coda = ranked, `ranked.data`, `match.result`, schermata ladder + overlay | medio |
| **3. Profili + icone** | `ProfileService`, `UnlockService`, catalogo icone, selettore, unlock da tier + **campagna** | medio |
| **4. Stagioni** | `SeasonService`, rollover `IHostedService`, soft reset, reward | medio |
| **5. Hall of Fame** | snapshot + query storiche + icone esclusive | basso |
| **6. Amici + presenza** | `FriendService`, `PresenceRegistry`, sfida amico | medio |
| **7. Achievement** | `achievements`, valutatori, unlock icone/titoli | basso |

---

## 11. Verifica (coerente con il progetto)

- Server: `dotnet build` + test standalone nello scratchpad; endpoint `/health`.
- DB: test EF Core InMemory/SQLite file temporaneo per `RankedService`/rollover/unlock idempotenti.
- E2E: due client console (pattern `ugse2e/`) → coda ranked → match completo → verifica
  `match.result`, righe DB, snapshot Hall of Fame forzando un rollover con stagione a durata 0.
- Client: compile-check contro le DLL Unity come già in uso.
