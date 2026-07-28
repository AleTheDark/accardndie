# Santuario: sblocco di classi, tecniche e oggetti

## Obiettivo

Sviluppare il Santuario, oggi presente nell'hub solo come bottone che apre il popup
"in sviluppo" (`BattleBoardController.SetupViews.cs`, `CreateHubBannerButton("Sanctuary Hub", ...)`).

Il Santuario e' il luogo dove il giocatore converte la progressione di campagna in
sblocchi permanenti. Tre assi:

- classi giocabili
- seconde abilita' di classe ("tecniche")
- oggetti consumabili da portare in run

Principio guida: **il miele non compra lo sblocco, lo conferma**. Ogni sblocco richiede
una prova guadagnata giocando, piu' un costo in miele. Il Santuario non genera contenuto,
converte quello che il giocatore ha gia' fatto.

## Stato attuale

### Cosa esiste gia'

- `SinglePlayerUnlockType` include gia' `Chapter, Stage, Class, Scenario, SecondAbility`
  (`Assets/_Project/Scripts/GameData/SinglePlayerProgressSave.cs`)
- acquisto server-authoritative funzionante: `PurchaseUnlock` con tabella costi
  (`Server/AccardND.Server/Progression/SinglePlayerProgressService.cs`)
- 3 classi starter gratuite dal tutorial: mage, warrior, rogue
- 6 classi avanzate gia' a catalogo lato server (40/60 miele), ma **nessuna UI le vende**:
  il deck builder si limita a mostrare "Classe bloccata"
- consumabili di run: `CampaignConsumableType` (Detector, SecondChance, Defrost, Empower,
  DoubleExp), acquistabili solo dal mercante dentro la run e pagati in **EXP di run**
- achievement (`AchievementService`), ma quasi tutti su metriche PvP
- livello account con EXP = `EXP campagna / 10`, 100 EXP per livello

### Cosa manca

- il server non sa cosa il giocatore ha **completato**: `single_player_unlocks` traccia
  solo cosa e' sbloccato
- nessun contatore aggregato di campagna (boss battuti, nemici, miniboss)
- nessun sistema di missioni giornaliere
- nessun inventario permanente di oggetti

### Difetti da correggere prima (prerequisiti) — RISOLTI nella fase 1

1. **Lo sblocco del capitolo successivo non arriva al server.**
   `UnlockNextAdventureChapterForBoss` (`BattleBoardController.CampaignProgress.cs`) chiama
   `singlePlayerProgressService.Unlock(...)`, cioe' il repository **locale**. Il repository
   server non espone `Unlock`, solo `PurchaseUnlockAsync`. Al primo `RefreshAsync()`
   successivo, `ApplyAuthoritative` sovrascrive la cache e **lo sblocco guadagnato sparisce**.

2. **Il sommario di fine run e' inutilizzabile per i requisiti.**
   In `ClaimCampaignRunAccountReward`:
   - `enemiesDefeated` e' `0` fisso, quindi il termine `+ enemies` della formula del miele
     lato server e' morto e ogni requisito basato sui nemici e' irraggiungibile
   - `chapterId` contiene in realta' l'id del boss (`campaignScenarioBossId`)
   - non viene mandato *quale* boss e' stato sconfitto

3. **Le vittorie sui boss sono registrate solo in parte.**
   `RecordCampaignBossVictory()` viene chiamata solo se `IsFinalBossRoom()`, cioe' solo per
   il boss dell'ultima stanza della run. Le uccisioni del Golem non sono registrate affatto
   (il ramo `activeComposableGolem` concede solo EXP).

## Mappa capitoli e boss

| Capitolo | Titolo | Boss finale |
| --- | --- | --- |
| 1 | La Nebbia di Bragus | Bragus |
| 2 | I Rampicanti di Trentor | Trentor |
| 3 | Gli Specchi di Medusa | Medusa |
| 4 | La Cosmica di Palatir | Palatir |

I capitoli completati restano nella lista e sono rigiocabili: i requisiti che chiedono
piu' vittorie sullo stesso boss sono quindi soddisfacibili.

## Struttura della schermata

```
HUB -> [SANTUARIO] -> tre altari
     |- CLASSI     griglia 3x3 per ClassFamily, costo + prova con progresso
     |- TECNICHE   lista per classe posseduta, visibili ma bloccate
     |- RELIQUIE   oggetti, bisaccia, slot
```

Riuso di `MmoUiTheme` e dei pattern della lista capitoli. Nuovo file parziale
`BattleBoardController.Sanctuary.cs` (il controller e' gia' molto grande).

Il bottone di acquisto va disabilitato quando `IsSynced == false`: la cache offline non
deve creare aspettative che il server poi rifiuta.

## Altare delle Classi

Le 3 starter sono mostrate come "ottenuta col tutorial", non nascoste.

| Classe | Miele | Prova |
| --- | --- | --- |
| Assassino | 40 | Sconfiggi Bragus 2 volte |
| Cacciatore | 40 | Sconfiggi Trentor 2 volte |
| Paladino | 40 | Sconfiggi il Golem 3 volte |
| Barbaro | 60 | Sconfiggi Medusa 2 volte |
| Negromante | 60 | Livello account 5 + 300 nemici sconfitti |
| Sacerdote | 60 | Sconfiggi Palatir 2 volte + completa 5 volte la missione giornaliera |

Criteri dietro la tabella:

- la difficolta' della prova sale insieme al costo in miele: i due segnali devono dire
  la stessa cosa al giocatore
- tutti e quattro i boss di capitolo sono usati, in ordine
- il Paladino e' legato al Golem (miniboss) e non a un capitolo, cosi' resta ottenibile
  in parallelo invece che in coda
- la giornaliera e' sull'ultima classe: li' funziona come gancio di ritorno, mentre su una
  classe iniziale sarebbe un muro di calendario (5 giorni reali) subito dopo il tutorial

Nota sul bilanciamento del Negromante: "livello account 5" vale circa 4.000 EXP campagna
(~13 run), mentre 100 nemici sarebbero ~3 run. La soglia e' stata alzata a 300 perche' i
due termini pesino in modo confrontabile.

La UI mostra sempre la prova e il progresso, anche per le classi non ancora avviate: e' il
modo piu' efficace di comunicare "c'e' altro la' fuori" senza tutorial.

## Altare delle Tecniche (seconde abilita')

Gli effetti non sono ancora definiti. In questa fase le tecniche sono **visibili con il
prezzo ma non acquistabili**.

- id definitivi da subito: `ability-warrior-2`, `ability-mage-2`, ... Finiscono nel DB
  degli unlock e non si rinominano piu'
- il tipo `SecondAbility` e il mapping stringa `secondAbility` esistono gia' su client e
  server: nessun lavoro di plumbing
- costo indicativo mostrato: 80 miele
- stato `LOCKED`, badge "in arrivo", bottone disabilitato
- nome e descrizione arrivano **dal server**, cosi' si riempiono senza toccare il client
- requisito gia' visibile: possedere la classe

Decisione rimandata, ma da prendere prima di scrivere gli effetti: la seconda abilita' e'
**alternativa** alla prima (si sceglie in loadout, aggiunge varieta') o **aggiuntiva** (si
hanno entrambe, aggiunge potenza)? La scelta condiziona prezzo e presentazione. Nota: se le
tecniche valessero anche in PvP, l'asimmetria tra chi ha grindato e chi no va valutata
esplicitamente.

## Altare delle Reliquie: sblocchi, non acquisti

**Correzione di rotta (2026-07-25).** Il Santuario e' il posto degli *sblocchi*: classi,
tecniche e **il diritto di comprare un oggetto**. Le copie degli oggetti si comprano al
**negozio**, una pagina separata da costruire.

Quindi:

- al Santuario un oggetto si sblocca **una volta sola** e non da' nessuna copia
- i prezzi di sblocco (20/25/30/40/70) pagano un permesso permanente, non un pezzo
- `BuyItem` resta lato server ed e' l'operazione del negozio: rifiuta gli oggetti non
  ancora sbloccati, cosi' le due pagine sono legate da una regola e non da una convenzione
- il prezzo per copia e' per ora derivato (un quarto dello sblocco): il listino vero
  arrivera' col negozio
- la **scelta della bisaccia** non sta piu' al Santuario: appartiene al negozio, dove vive
  la scorta

## Bisaccia (meccanica invariata)

Nessun conflitto di valuta: il mercante in-run compra con **EXP di run**, il Santuario con
**miele**. Ruoli diversi: il mercante e' il canale di reazione, il Santuario quello di
preparazione.

### Regola centrale

**Un solo pezzo per tipo nella bisaccia.** Da questa regola discende quasi tutto il
bilanciamento: non serve un cap speciale su SecondChance, ed e' impossibile accumulare tre
copie dell'oggetto piu' forte. Costringe a diversificare.

### Slot

- 2 slot iniziali
- 3o slot: 60 miele (`unlock_type = 'slot'`, id `bag-slot-3`)
- 4o slot: 150 miele (`bag-slot-4`)
- massimo 4

### Prezzi

Riferimento: prezzi del mercante in EXP di run (Detector 12, Defrost 15, DoubleExp 18,
Empower 22, SecondChance 26).

| Oggetto | Miele |
| --- | --- |
| Detector | 6 |
| Defrost | 8 |
| DoubleExp | 10 |
| Empower | 14 |
| SecondChance | 30 |

Calibrazione: una run rende oggi circa `5 + rooms*3 + bosses*10` = 35-60 miele. Con 2 slot,
un loadout medio (Detector + Empower) costa 20, cioe' circa meta' dell'incasso di una run:
si sente ma non blocca la progressione verso capitoli (25/75/120/180) e classi.
SecondChance a 30 significa "quasi tutto l'incasso di una run per una rete di sicurezza":
giusto prima di un boss, sbagliato come abitudine.

**Questi prezzi vanno rivisti al rialzo** appena `enemiesDefeated` viene corretto, perche'
il miele per run salira'.

### Regole di run

- gli oggetti usati sono persi
- gli oggetti non usati tornano nella scorta a fine run

## Missione giornaliera

Sottosistema nuovo. Oltre a servire il requisito del Sacerdote, risolve un problema
esistente: **oggi il miele entra solo a fine run**, non c'e' un rubinetto regolare che
sostenga l'economia della bisaccia.

Versione minima:

- catalogo di obiettivi semplici sui contatori gia' esistenti ("completa 3 stanze",
  "sconfiggi un boss", "usa 2 oggetti")
- una missione attiva al giorno
- reset a mezzanotte **UTC** (il fuso orario del device e' manipolabile)
- valutazione e assegnazione server-side
- ricompensa 10-15 miele
- contatore cumulativo `daily_completed`, letto dal requisito del Sacerdote

## Modello dati

### Riuso senza migration

`single_player_unlocks` e' generica `(player_id, unlock_type, unlock_id)` con
`INSERT OR IGNORE`. Si riusa per:

- `chapterCleared` / `chapter-N` -> capitoli completati
- `slot` / `bag-slot-N` -> slot bisaccia

### Tabelle nuove

- `player_counters (player_id, counter_key, value, updated_at)` -> contatori ripetibili.
  Chiavi attive: `enemies_defeated`, `rooms_cleared`, `runs_ended`, `boss_bragus`,
  `boss_trentor`, `boss_medusa`, `boss_palatir`, `miniboss_golem`. In arrivo con la
  giornaliera: `daily_completed`
- `player_consumables (player_id, type, count)` -> scorta permanente di oggetti
- stato bisaccia (loadout scelto per la prossima run)

Le chiavi dei contatori sono **canoniche e distinte dagli id carta del client**: `trentor`
non ha il prefisso `boss-` che hanno gli altri boss, e quella stranezza non deve finire nel
catalogo dei requisiti.

**Una sola via di scrittura per contatore.** I boss di capitolo passano da `ClearChapter`
(scatta alla morte del boss, quindi il conteggio non si perde se il giocatore chiude il
gioco senza completare la run); tutto il resto dal sommario di fine run, dopo il controllo
di idempotenza sul runId. Gli id sconosciuti nel sommario vengono ignorati, cosi' un client
manipolato non puo' creare chiavi arbitrarie: la lista grezza resta comunque archiviata in
`campaign_runs.defeated_boss_ids`.

## Motore dei requisiti

Un solo concetto, riusato da classi, tecniche e slot.

```
Requisito = (tipo, chiave, soglia)

  counter       boss_bragus        2
  counter       miniboss_golem     3
  counter       enemies_defeated   300
  counter       daily_completed    5
  chapterCleared chapter-2         1
  accountLevel  -                  5
  achievement   ach-boss-medusa    1
```

Uno sblocco ha una **lista di requisiti in AND** piu' un costo in miele.

Il server:

1. valuta i requisiti dentro `PurchaseUnlock`. Oggi la funzione controlla solo costo e
   tutorial: senza questa aggiunta il gating sarebbe solo decorativo lato client
2. espone catalogo **e progressi** con un nuovo messaggio `sanctuary.get`, cosi' la UI
   mostra "Bragus 1/2" senza duplicare il catalogo

Su quest'ultimo punto: la tabella costi lato server ha oggi il commento "allineato alle
costanti client", cioe' e' duplicata a mano. Il Santuario e' l'occasione per rendere il
**server sorgente unica** del catalogo e il client un semplice renderer.

### Quest e achievement

Sono la stessa macchina (contatore + soglia + stato). Un solo motore, due presentazioni:
gli achievement restano il muro dei trofei nel profilo, le prove del Santuario sono le
stesse entita' filtrate per scope campagna e mostrate come obiettivi attivi.

### Limite noto

Il combattimento single player e' client-side (lo dice gia' il commento in
`ClaimDeathReward`), quindi i contatori di campagna sono falsificabili da un client
modificato. Accettabile per il PvE; da rivalutare se questi sblocchi arrivassero a contare
in PvP.

## Fasi di implementazione

1. **Correzioni preliminari** — FATTA
   - evento "capitolo completato" server-side, che registra `chapterCleared` e concede lo
     sblocco del capitolo successivo (risolve il difetto 1)
   - sommario di fine run arricchito: boss id sconfitto, kill miniboss, nemici reali;
     `chapterId` con il vero id capitolo (risolve i difetti 2 e 3)

   Dettaglio di cosa e' stato costruito:
   - messaggio `singleplayer.chapter.cleared` con `SinglePlayerClearChapterRequest { bossId }`
   - `SinglePlayerProgressService.ClearChapter`: mappa boss->capitolo lato server, registra
     `chapterCleared` e concede il capitolo successivo. Idempotente (INSERT OR IGNORE)
   - `chapterCleared` esplicitamente non acquistabile da `PurchaseUnlock`
   - `SinglePlayerUnlockType.ChapterCleared` + `clearedChapters` nel save e nel DTO
   - `RunProgressState.EnemiesDefeated` / `MinibossesDefeated`, persistiti nel save della run
   - `activeAdventureChapterId` e `defeatedBossIdsInRun` nel controller, persistiti nel save
   - colonne `minibosses_defeated` e `defeated_boss_ids` su `campaign_runs`
2. **Contatori**: tabella `player_counters` alimentata da `ClaimDeathReward` — FATTA
   - `CampaignCounters` (server) con incremento e lettura transazionali
   - `ClearChapter` incrementa il contatore del boss di capitolo a ogni vittoria: e' lo
     unlock a essere idempotente, non il conteggio
   - contatori esposti al client in `SinglePlayerProgressData.counters` e rispecchiati nella
     cache locale, leggibili con `GetCounter(key)`
3. **Motore requisiti + `sanctuary.get`**, con validazione dentro `PurchaseUnlock` — FATTA
   - `SanctuaryCatalog`: 9 classi (3 starter non acquistabili, 6 avanzate con le prove
     approvate) e 9 tecniche placeholder bloccate, con id definitivi
   - `SanctuaryRequirementContext` valuta le prove sullo stesso snapshot che viene mandato
     al client: quello che il giocatore vede e quello che il server valida non divergono
   - tipi di prova implementati: `counter`, `accountLevel`, `classOwned`. Aggiungerne uno
     e' un caso in piu' in `CurrentValue`
   - costi delle classi rimossi da `UnlockCosts`: il listino tiene solo capitoli e modalita',
     mentre le voci del Santuario hanno una sola sorgente
   - `PurchaseUnlock` rifiuta con `requirements_not_met` quando una prova manca, e rifiuta
     del tutto le voci non acquistabili (starter, tecniche)
4. **Schermata Santuario** a tre altari (`BattleBoardController.Sanctuary.cs`) — FATTA
   - il bottone dell'hub apre il Santuario invece del popup "in sviluppo"
   - tre altari a tab: Classi, Tecniche, Reliquie (quest'ultimo con il solo messaggio
     di attesa, la bisaccia arriva alla fase 6)
   - le carte mostrano nome, stato (ottenuta / costo / in arrivo) e il progresso di ogni
     prova, letti dal catalogo del server
   - schermata non disponibile offline **di proposito**: mostrare costi da una cache
     locale darebbe numeri che il server potrebbe poi rifiutare
   - l'acquisto non e' ancora agganciato: le carte sono informative, il click arriva
     con la fase 5
5. **Altare Classi** (lato server gia' quasi pronto) — FATTA
   - le carte con prove superate diventano cliccabili e aprono una conferma
   - il miele insufficiente non spegne la carta: la conferma dice quanto manca, cosi' il
     giocatore vede la distanza invece di trovarsi un bottone morto
   - l'acquisto passa da `PurchaseUnlockAsync`; al ritorno il catalogo viene **ricaricato**
     invece di aggiustare la carta a mano, cosi' lo stato mostrato resta autoritativo
   - il catalogo si ricarica anche dopo un rifiuto: il server puo' avere uno stato piu'
     recente di quello che il client aveva in mano
   - l'esito dell'offerta sopravvive al ricaricamento, altrimenti il messaggio generico
     dell'altare cancellerebbe subito l'unico riscontro dell'acquisto
6. **Altare Reliquie**: scorta, slot, selezione bisaccia pre-run — FATTA
   - `player_consumables` (scorta) e `player_bag` (selezione): due concetti distinti. La
     bisaccia non trasferisce niente, e' una selezione; un oggetto lascia la scorta solo
     quando viene davvero usato
   - la regola "un solo pezzo per tipo" **e' la chiave primaria di `player_bag`**, non un
     controllo scritto a mano
   - slot in ordine: il quarto non e' offerto finche' manca il terzo
   - le carte oggetto hanno due azioni: COMPRA (spende miele, passa dalla conferma) e
     IN BISACCIA / TOGLI (gratis e reversibile, quindi immediato)
   - la bisaccia viaggia anche in `SinglePlayerProgressData`, non solo nel catalogo: la run
     deve poterla caricare senza che il giocatore sia passato dal Santuario
   - **rimosso `GrantStartingCampaignConsumablesForTesting`**: la run non regala piu' 2 copie
     di ogni oggetto, parte con la bisaccia
   - gli oggetti usati vengono riportati a fine run (`consumedItemIds`) e scalati dalla
     scorta; quelli non usati restano
7. **Altare Tecniche** in sola visualizzazione — FATTA (consegnata dalle fasi 3 e 4: le 9
   tecniche sono a catalogo con id definitivi, mostrate bloccate con prezzo e requisito di
   classe). Resta solo da definire gli effetti.
8. **Missione giornaliera** — FATTA
   - `player_daily_missions (player_id, day, mission_id, baseline, claimed_at)`
   - ogni obiettivo e' "fai salire di N un contatore che gia' esiste": all'assegnazione si
     registra il valore di partenza e il progresso e' la differenza. Nessun secondo sistema
     di tracciamento nel gameplay
   - missione uguale per tutti, derivata dalla data UTC: niente estrazione da memorizzare,
     e due giocatori possono parlare della prova di oggi
   - la selezione non usa `GetHashCode` (non e' garantito costante tra avvii: un riavvio del
     server cambierebbe la missione a meta' giornata)
   - riscossione manuale, 12 miele, idempotente; incrementa `daily_completed`, cioe' la
     prova del Sacerdote
   - mostrata sopra le tab, visibile da ogni altare: e' il rubinetto di miele regolare che
     l'economia della bisaccia richiede, non un dettaglio di una sezione

## Punti aperti

- seconde abilita': alternative o aggiuntive? Valgono in PvP?
- prezzi della bisaccia da ricalibrare dopo la correzione di `enemiesDefeated`
- la giornaliera va nella fase 8 o si anticipa? Finche' non c'e', il Sacerdote non e'
  completabile
- il progetto server non ha test: `ClearChapter`, i contatori e il motore requisiti sono
  verificati solo da un harness usa e getta fuori dal repo. Vale la pena promuoverlo a
  progetto xUnit, ora che la logica server-side non e' piu' banale
- i contatori partono da zero: le run gia' giocate non contano. Se serve, prima che il
  Santuario diventi visibile ai giocatori va fatto un backfill una tantum da `campaign_runs`
- il requisito `daily_completed` del Sacerdote restera' fermo a 0/5 finche' non arriva la
  missione giornaliera (fase 8)
