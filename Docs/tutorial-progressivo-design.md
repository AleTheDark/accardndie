# Tutorial progressivo — design

Stato: **tutte e sei le fasi implementate** il 2026-08-14. Il percorso e' giocabile
dall'inizio alla fine; restano da rifinire i contenuti scriptati delle lezioni di
classe (vedi "Cosa resta" in fondo alla §10). Ultimo aggiornamento: 2026-08-14.

Documento di riferimento per il tutorial a moduli tematici con sblocchi a cascata.
Serve soprattutto a fissare **i cancelli**: chi apre cosa, in che ordine, e cosa deve
essere cliccabile in ogni istante. Senza questa tabella il collaudo diventa
ingestibile, perche' ogni modulo cambia lo stato di sette schermate diverse.

---

## 1. L'idea in una riga

Il tasto TUTORIAL non apre piu' quattro immagini: apre una **sezione di tutorial
tematici**. Ogni modulo insegna una meccanica, e alla fine sblocca una zona del
gioco; l'apertura della zona e' accompagnata da un **tour guidato** di Fleck che
spiega la zona e indirizza al modulo successivo. Durante tutto il percorso il resto
del gioco e' chiuso: e' cliccabile solo la cosa che il tutorial sta insegnando.

Il ritmo e' **impara → guadagna → compra → impara cosa hai comprato**. Il miele
resta guadagnabile solo in taverna: i 40 vasetti di un modulo sono un **dono
vincolato**, esatti al costo della classe da comprare, e i cancelli fanno si' che
non siano spendibili altrove. Il giocatore finisce ogni acquisto a zero.

I moduli sono tutti **di percorso**: obbligatori, ordinati, con ricompensa e
sblocco. (Un eventuale codex consultabile — aure, mercato, stanze speciali,
talenti — e' rimandato: si aggiunge dopo, riusando la stessa schermata indice.)

---

## 2. Vocabolario

| Termine | Significato |
| --- | --- |
| **Modulo** | Una lezione tematica con id stabile (es. `m1-warrior`). |
| **Tappa** | Uno schermo/passo dentro un modulo (il pannello con testo + spotlight). |
| **Tour** | Sequenza di tappe fuori dalla battaglia, dentro una schermata (Santuario, Negozio, Bisaccia). Fleck parla, lo spotlight illumina, il resto e' inerte. |
| **Cancello (gate)** | Regola che decide se una destinazione e' aperta, chiusa o *evidenziata*. |
| **Sblocco** | Effetto permanente di un modulo: apre un cancello, concede miele/oggetti/classi. |
| **Stage** | Numero di moduli di percorso completati (0..6). E' l'unico numero da cui derivano tutti i cancelli. |

---

## 3. Cosa esiste gia' (da riusare, non riscrivere)

| Pezzo | Dove | Riuso |
| --- | --- | --- |
| Tutorial scriptato di combattimento (8 passi, tiri truccati, spotlight, dimmer, testo a macchina) | [BattleBoardController.AdventureTutorial.cs](Assets/_Project/Scripts/Presentation/BattleBoardController.AdventureTutorial.cs) | Si spacchetta nei moduli M0/M1/M2/M3/M5. Lo spotlight + i 4 dimmer + il pannello sono gia' generici: vanno estratti in un servizio riusabile fuori dalla battaglia. |
| Fleck con bolla, bocca animata, alone, pagine multiple | [BattleBoardController.Hints.cs](Assets/_Project/Scripts/Presentation/BattleBoardController.Hints.cs) | E' il presentatore dei tour. Oggi e' spento di default (`AccardFleckHintsEnabled` = 0) e si accende solo da `ResetHints()`; va acceso all'inizio del percorso. |
| Zoom cinematografico dell'hub verso un hotspot | `PlayCampaignHubZoomThenOpen` in [BattleBoardController.SetupViews.cs:2698](Assets/_Project/Scripts/Presentation/BattleBoardController.SetupViews.cs) | E' gia' l'animazione "si sblocca il Santuario": basta poterla lanciare senza aprire la schermata. |
| VFX scintilla per hotspot | `HubPortalVfx.Attach` | Gia' pronto per l'evidenziazione del cancello appena aperto. |
| Riga TUTORIAL nella lista Avventura | `CreateAdventureTutorialRow` in [SetupViews.cs:1437](Assets/_Project/Scripts/Presentation/BattleBoardController.SetupViews.cs) | Diventa la porta della nuova sezione. |
| Reward tutorial server-side idempotente | `ClaimTutorialReward` in [Server/.../SinglePlayerProgressService.cs:553](Server/AccardND.Server/Progression/SinglePlayerProgressService.cs) | Modello esatto da replicare per le ricompense di modulo. |
| Sblocchi permanenti | `single_player_unlocks` + `SinglePlayerUnlockType` | Aggiunge un tipo, non una tabella. |

### Codice morto da rimuovere insieme

- `StartTutorial(bool)`, `AdvanceTutorial`, `StopTutorial`, `ShowTutorialPage`,
  `StartTutorialFromOptions`, `tutorialAdvanceButton`, gli sprite `UI/tutorial-1..4`:
  il vecchio tutorial a 4 immagini non e' piu' richiamato da nessuna parte dopo il
  rifacimento delle Impostazioni. `modeSelectionTutorialButton` e' gia' `null`.
- `BeginGuidedAdventureTutorial` / `GuidedTutorialStep` (9 pagine di testo statico):
  i contenuti migrano nei moduli, il pannello sparisce.

---

## 4. Il percorso: 6 moduli

L'ordine e': **Guerriero → Mago → Ladro → Oggetti → Un capitolo → Primi passi**.
Le lezioni spiegate vengono prima, la prova pratica per ultima: `m0-basics` non e'
il primo passo ma l'esame finale, la run guidata in cui si mette in pratica tutto.
E' anche il modulo che consegna il capitolo 1 e la Seconda Chance.

(Gli id dei moduli conservano la numerazione con cui sono nati e non corrispondono
piu' alla posizione: sono nel database degli sblocchi e non si rinominano.)

Due tipi di modulo:

- **Modulo di classe** (Guerriero, Mago, Ladro): stessa struttura ripetuta tre volte
  — **abilita' → suprema → aura** della classe. E' il modello che scalera' anche
  alle sei classi avanzate (§4.7).
- **Modulo di sistema** (Oggetti, Un capitolo, Primi passi): una meccanica
  trasversale, o la prova finale.

Ogni modulo: **prerequisito → lezione → azione forzata → ricompensa → sblocco →
tour → puntamento al modulo dopo**.

### 1 — `m1-warrior` · "Il Guerriero"

- **Prerequisito**: nessuno. E' il primo avvio.
- **Lezione**, nell'ordine:
  1. **il mana** — riserva, tetto a 10, il costo sotto ogni abilita' (entra qui
     perche' e' qui che serve la prima volta);
  2. **abilita' del Guerriero** — colpo pesante, 5 mana: dado Vigore + un dado di
     uno step inferiore, sommati (la piu' cara del gioco, e va detto);
  3. **suprema** — Empower, 6 mana: +2 Potenza, +4 se e' l'ultima pedina rimasta.
     Provata in sandbox, **non regalata** (§8.7);
  4. **aura Might** — la famiglia Guerriero/Barbaro/Paladino: batte Furtivi, perde
     contro Magici. Qui si mostra solo mezza regola: il triangolo si chiude al Ladro.
- **Ricompensa**: classe **Guerriero** (unica starter, §8.1) e **40 miele** vincolati.
- **Sblocca**: **SANTUARIO**.
- **Post-modulo**: hub → animazione di sblocco sull'hotspot Santuario → **Tour
  Santuario** (a cosa serve, i tre altari, che le classi si comprano col miele) →
  subito dopo, **tour d'acquisto**: altare Classi → **Mago** (40) → conferma. I due
  tour cadono nello stesso punto del percorso e si passano il testimone da soli: il
  primo smette di essere dovuto appena e' stato visto.

### 2 — `m2-mage` · "Il Mago"

- **Prerequisito**: Guerriero completo + Mago posseduto.
- **Lezione**: abilita' (Indebolisci, 2 mana — subito dopo i 5 del Guerriero, cosi'
  si capisce che i costi variano); suprema **Palla di fuoco** (4 mana, colpisce
  tutti i nemici con un dado in meno: la prima suprema ad area); **aura Magic** —
  batte Might, cioe' batte proprio la classe con cui il giocatore ha giocato finora.
- **Ricompensa**: **40 miele**.
- **Post-modulo**: **Tour acquisto** → Santuario → **Ladro** (40) → conferma.

### 3 — `m3-rogue` · "Il Ladro"

- **Prerequisito**: Mago completo + Ladro posseduto.
- **Lezione**: **l'abilita' passiva** — il Ladro non ha niente da premere, la sua
  ritira un dado da sola e costa 0 mana. E' il modulo dove si insegna che esistono
  due tipi di abilita' (§8.8); suprema **Ruba potenziamenti** (3 mana: ruba un buff
  e 2 mana, o 2 Potenza se non ci sono buff); **aura Cunning** — batte Magic, perde
  contro Might: **qui si chiude il triangolo** e va mostrato per intero, con le tre
  famiglie e le nove classi.
- **La scena da scriptare**: il Ladro tira **basso** contro un nemico che lo
  batterebbe, la passiva ritira da sola, il secondo tiro **ribalta lo scontro**.
  E' il momento didattico del modulo: la passiva non si spiega, si fa vedere
  mentre salva la partita (§8.8).
- **Sblocca**: **NEGOZIO**.
- **Post-modulo**: animazione di sblocco sull'hotspot Negozio → **Tour Negozio**
  (consumabili, offerta del giorno, non si accumulano copie).

### 4 — `m4-items-bag` · "Oggetti e bisaccia"

- **Prerequisito**: Ladro completo + Tour Negozio visto.
- **Lezione**: scorta contro bisaccia (possedere ≠ portarsi dietro), gli slot, come
  si usa un consumabile in run, quali non sono usabili in battaglia.

### 5 — `m5-chapter-run` · "Un capitolo"

- **Prerequisito**: modulo Oggetti completo.
- **Lezione**: com'e' fatto un capitolo — le porte, il miniboss alla stanza
  `MinibossEveryRooms`, il boss finale alla stanza `FinalBossRoom` (10 e 20, §8.4),
  panoramica sulle stanze non-mostro (Mercato, Tesoro, Prova Lampo), e cosa resta
  quando una run finisce.
- **Forma**: spiegazione illustrata. La pratica arriva col modulo dopo.

### 6 — `m0-basics` · "Primi passi"

E' l'ultimo, non il primo: il nome resta quello con cui e' nato, ma il suo posto nel
percorso e' la **prova finale**. Prima si spiega, poi si gioca.

- **Prerequisito**: tutti i moduli precedenti.
- **Lezione**: la run guidata completa — iniziative, schieramento, lettura di una
  pedina, attacco, dado Vigore, vantaggio, abilita', vittoria.
- **Ricompensa**: **capitolo 1**, oggetto **Seconda Chance** nella scorta,
  `tutorialCompleted = true`.
- **Sblocca**: **tutto il resto dell'hub** (Taverna, Biblioteca, Profilo,
  Classifica, Arena) e le righe dei capitoli in Avventura.
- **Post-modulo**: Fleck spiega la Seconda Chance e indica la Taverna come **la**
  fonte di miele d'ora in avanti.

### 4.7 Dopo il percorso: il modello si ripete

I moduli di classe sono uno stampo, non tre casi speciali. Appena il percorso
funziona, ogni classe avanzata comprata al Santuario (Assassino, Cacciatore,
Paladino, Barbaro, Negromante, Sacerdote) puo' sbloccare **il proprio modulo di
classe** con la stessa struttura abilita'/suprema/aura — facoltativo, senza
ricompensa, giocato in sandbox. Nove classi, un solo motore, contenuto che cresce
da solo. Stessa cosa per la Tecnica: chi compra la suprema di una classe sblocca
il modulo che gliela fa provare.

---

## 5. La tabella dei cancelli

`Stage` = numero di moduli di percorso completati. Legenda: **A** aperto ·
**C** chiuso (visibile, non cliccabile, con velo) · **E** evidenziato (unica cosa
cliccabile, con scintilla) · **—** nascosto.

### 5.1 Hub (`modeSelectionPanel`)

Le colonne sono i moduli completati, nell'ordine del percorso.

| Destinazione | 0 | Guerriero | Mago | Ladro | Oggetti | Un capitolo | Primi passi |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Campagna | **E** | C→E (dopo tour e acquisto Mago) | C→E (dopo acquisto Ladro) | C→E (dopo tour Negozio) | **E** | **E** | **A** |
| Santuario | C | **E** (tour, poi Mago) | **E** (acquisto Ladro) | A | A | A | **A** |
| Negozio | C | C | C | **E** (tour) | A | A | **A** |
| Taverna | C | C | C | C | C | C | **A** |
| Biblioteca | C | C | C | C | C | C | **A** |
| Profilo | C | C | C | C | C | C | **A** |
| Classifica | C | C | C | C | C | C | **A** |
| Arena (PvP) | C | C | C | C | C | C | **A** |
| Impostazioni (header) | **A** | **A** | **A** | **A** | **A** | **A** | **A** |
| Tasto Home (header) | **A** | **A** | **A** | **A** | **A** | **A** | **A** |

Due regole non negoziabili in fondo alla tabella:

- **Impostazioni sempre aperte.** Contengono lingua, logout e la cancellazione
  dell'account: chiuderle durante l'onboarding sarebbe un problema di conformita'
  sullo store, non solo una scomodita'.
- **Home sempre aperta.** E' il recupero della navigazione: se il tutorial si
  incastra, il giocatore deve poter tornare all'hub. Il tutorial riprende da dove
  era, non ricomincia.

### 5.2 Dentro Campagna

| Voce | Prima di M5 | Dopo M5 |
| --- | --- | --- |
| AVVENTURA | **E** | **A** |
| HARDCORE | **C** | A/acquistabile (invariato) |

### 5.3 Dentro Avventura (lista capitoli)

| Riga | Prima di M5 | Dopo M5 |
| --- | --- | --- |
| TUTORIAL (indice moduli) | **E** | **A** |
| Capitolo 1 | **C** | **A** |
| Capitoli 2..7 | **C** | catena boss invariata |

### 5.4 Dentro Santuario

| Elemento | Tour "conosci il Santuario" (post-M0) | Tour acquisto (post-M1 / post-M2) | Altrimenti in percorso | Dopo M5 |
| --- | --- | --- | --- | --- |
| Altare Classi | C (solo lettura guidata) | **E** | C | A |
| Altare Tecniche | C (mostrato, si spiega che le supreme si comprano qui) | C | C | A |
| Altare Reliquie | C | C | C | A |
| Voce Mago | C | **E** (post-M1) | C | A |
| Voce Ladro | C | **E** (post-M2) | C | A |
| Altre classi/voci | C | C | C | A |
| Indietro | A | A | A | A |

### 5.5 Dentro Negozio

| Elemento | Tour Negozio (post-M3) | Altrimenti in percorso | Dopo M5 |
| --- | --- | --- | --- |
| Offerte del giorno | C (lettura guidata) | C | A |
| Catalogo consumabili | C (lettura guidata) | C | A |
| Sezione premium (IAP) | **C** | **C** | A |
| Indietro | A | A | A |

Nota: la sezione premium resta chiusa per tutto l'onboarding. Un tour che finisce
per sbaglio su un acquisto reale e' l'unico errore di questo sistema che costa
soldi a qualcuno.

### 5.6 In battaglia (moduli scriptati)

Vale gia' oggi e va conservato: interagibile **solo** il bersaglio dello spotlight
(`IsAdventureTutorialDraftCardAllowed`, `SetAdventureTutorialDraftInteractivityFor*`).
Aggiungere alla lista dei bloccati: log, opzioni di run, abbandono run, oggetti
della bisaccia non pertinenti al modulo.

---

## 6. Stato persistito

### 6.1 Server (autorevole)

Le ricompense sono miele e oggetti: **devono** stare sul server, come tutto il
resto della progressione single player. Riuso della tabella esistente, nessuna
migrazione di schema:

```
single_player_unlocks (player_id, unlock_type='tutorial', unlock_id='m1-attack-dice')
```

Il progresso del percorso e' quindi l'insieme dei moduli completati. Lo `stage` e'
derivato, mai salvato: salvare un numero e una lista che possono divergere e'
esattamente il tipo di bug che in collaudo non si riproduce.

Nuovo tipo client-side: `SinglePlayerUnlockType.TutorialModule` in
[SinglePlayerProgressSave.cs](Assets/_Project/Scripts/GameData/SinglePlayerProgressSave.cs)
con la sua lista `completedTutorialModules`.

### 6.2 Server: nuovo endpoint

`SinglePlayerTutorialModuleClaim { moduleId, claimRef }` →
`ClaimTutorialModuleReward(identity, request)`, ricalcato su `ClaimTutorialReward`:

1. transazione + `EnsureProgressRow`;
2. se il modulo e' gia' in `single_player_unlocks` → **risposta idempotente**, zero
   concessioni (identico al ramo `current.tutorialCompleted` di oggi);
3. `RecordClaim(..., "tutorial-module", honey, 0, claimRef)`;
4. concede quello che il **catalogo server** associa a quel modulo — mai quello che
   chiede il client;
5. marca il modulo, rilegge, commit.

Catalogo server (unica fonte di verita' delle ricompense):

| # | Modulo | Miele | Sblocchi | Oggetti |
| --- | --- | --- | --- | --- |
| 1 | `m1-warrior` | prezzo del Mago | classe `warrior` | — |
| 2 | `m2-mage` | prezzo del Ladro | — | — |
| 3 | `m3-rogue` | 0 | — | — |
| 4 | `m4-items-bag` | 0 | — | — |
| 5 | `m5-chapter-run` | 0 | — | — |
| 6 | `m0-basics` | 0 | `chapter-1`, `tutorial_completed=1` | `second-chance` ×1 in scorta |

Il dono **non e' un numero scritto nel catalogo dei moduli**: il modulo dichiara
quale classe fa comprare (`PaysForClassId`) e l'importo si legge dal listino del
Santuario. Cosi' buono e prezzo non possono divergere — in eccesso lascerebbero
miele in tasca e romperebbero "il miele si guadagna in taverna", in difetto
bloccherebbero il tour davanti a un acquisto impossibile — e quando Mago e Ladro
passeranno a 40 (§8.1) il dono si adegua da solo, senza toccare due file.

Totale regalato: 80 vasetti, spesi entrambi in classi da 40 durante il percorso.
Il giocatore esce dall'onboarding con 3 classi, 1 capitolo, 1 oggetto e **0 miele**:
da li' in poi la taverna e' l'unica fonte, come da regola.

Finche' Mago e Ladro restano classi starter gratuite il dono vale 0, ed e'
coerente: non c'e' niente da pagare. E' anche il motivo per cui il cambio di
catalogo (§8.1) puo' aspettare la Fase 5 senza lasciare buchi.

Il vecchio `ClaimTutorialReward` resta, ma diventa l'alias di `m5-chapter-run` per
i client vecchi (vedi §8.5).

### 6.3 Client (locale, non autorevole)

Solo cose senza valore economico, in `PlayerPrefs`:

- `AccardTutorialTourSeen_<id>` — tour gia' visti (Santuario, Negozio, Bisaccia).
- `AccardTutorialPending` — il modulo/tour in corso, per riprendere dopo un
  riavvio a meta' percorso.
- `AccardFleckHintsEnabled` — acceso all'inizio di M1.

Se il client perde i PlayerPrefs ma il server ha i moduli, il percorso riparte dal
primo tour non registrato: si rivede un tour, non si riprende un modulo pagato.
E' la degradazione giusta.

---

## 7. Architettura client

Tre pezzi nuovi, tutti isolabili e testabili a parte.

### 7.1 `TutorialFlow` — la macchina a stati

Un solo file, un solo posto che sa "a che punto siamo".

```
Stage        = moduli completati (dal servizio di progressione)
NextModule   = primo modulo di percorso non completato
PendingTour  = tour dovuto ma non ancora visto (da PlayerPrefs)
IsOnboarding = Stage < 5
```

Espone gli eventi che i moduli emettono (`ModuleCompleted`, `TourCompleted`,
`PurchaseSatisfied`) e nient'altro. Non tocca la UI.

### 7.2 `TutorialGate` — i cancelli

Una sola funzione pura:

```
GateState Evaluate(Surface surface)   // Open | Closed | Highlighted
```

`Surface` enumera le voci della tabella §5. **Ogni** schermata interroga questa
funzione e nessuna decide per conto suo. Il motivo e' il collaudo: se i cancelli
sono sparsi in otto file, la matrice di test non e' verificabile; se sono qui, e'
un test a tabella con 8 superfici × 6 stage.

Il rendering dello stato e' altrettanto centralizzato:
`ApplyGate(Button, GateState)` → interattivita', velo scuro, scintilla
`HubPortalVfx`. Un tap su una superficie **C** non e' silenzioso: mostra la riga
"Prima completa: <nome modulo>" e riporta lo spotlight sul modulo corrente.

### 7.3 `GuidedTour` — i tour fuori dalla battaglia

Estrazione di quello che oggi vive dentro `AdventureTutorial`: pannello, spotlight,
dimmer a 4 rettangoli, testo a macchina, bottone CONTINUA. Diventa un servizio che
prende una lista di tappe:

```
Tappa = { titolo, testo, bersaglio: RectTransform, condizione di avanzamento }
condizione ∈ { TapContinua, TapBersaglio, EventoDiGioco(id) }
```

Con `EventoDiGioco("class-purchased:mage")` il tour dell'acquisto forzato non ha
bisogno di logica dedicata: e' una tappa che aspetta un evento.

Attenzione al Safe Area: i pannelli centrati vanno appesi alla radice del canvas,
non al rect Safe Area — vedi la nota gia' nota su questo progetto.

---

## 8. Conflitti col design attuale — decisioni da prendere

Queste sono le cose che il percorso descritto **rompe** rispetto a com'e' il gioco
oggi. Sono elencate perche' vanno decise prima di scrivere codice, non dopo.

### 8.1 Mago e Ladro oggi sono gratis — cambio APPROVATO

Oggi Mago, Guerriero e Ladro sono **starter**: `SanctuaryCatalog.StarterClass(...)`
le mette a costo 0 e `Available = false` ("Ottenuta completando il tutorial"), e
`ClaimTutorialReward` le concede tutte e tre con `GrantStarterClasses`. Il percorso
chiede invece di **comprare** Mago (dopo M2) e Ladro (dopo M4) con i 40 miele
regalati.

Proposta: **solo il Guerriero resta starter**. Mago e Ladro diventano
`AdvancedClass(..., 40)`, acquistabili come le altre. Impatti da mettere in conto:

- `GrantStarterClasses` concede solo `warrior`;
- `StarterHeroClasses` in [DeckBuilder.cs:23](Assets/_Project/Scripts/Presentation/BattleBoardController.DeckBuilder.cs)
  e il ramo `TutorialCompleted` di `IsHeroClassUnlockedForCampaign` vanno allineati,
  altrimenti il Deck Builder mostra come sbloccate classi che il Santuario considera
  da comprare;
- gli account esistenti hanno gia' le tre classi: `GrantStarterClasses` e' anche
  chiamata da una retro-concessione ([SinglePlayerProgressService.cs:1238](Server/AccardND.Server/Progression/SinglePlayerProgressService.cs)),
  quindi nessuno perde niente — ma il tutorial deve **saltare** i tour d'acquisto
  per chi possiede gia' la classe (§9);
- il costo di 40 e' esattamente il regalo: il giocatore finisce il modulo con 0
  miele, ed e' voluto (§8.2).

Da non dimenticare quando si tocca il catalogo: la descrizione delle starter
("Ottenuta completando il tutorial") vale ancora solo per il Guerriero, e Mago e
Ladro passano da `Available = false` ad acquistabili — cioe' compaiono nell'altare
Classi con un prezzo, dove oggi sono solo esposte.

### 8.2 "Il miele si guadagna solo in taverna" — resta vera

Decisione presa: i 40 vasetti non sono un guadagno, sono un **dono vincolato**.
Esatti al costo della classe, consegnati con i cancelli chiusi su tutto il resto,
spesi subito nel tour d'acquisto. Il giocatore non li vede mai come reddito e non
puo' portarli fuori dal tutorial.

Perche' la regola tenga anche nella percezione, servono tre accortezze:

- la copy del dono lo dice esplicitamente — "40 vasetti per la tua prima classe",
  non "hai guadagnato 40 vasetti";
- il messaggio che oggi recita "il miele si guadagna in taverna" non va cambiato,
  ma **non deve comparire prima** che la taverna esista per il giocatore: spostarlo
  al post-M5, dove la taverna si apre;
- se per qualunque motivo restasse del miele non speso a fine tour (§9.6), il
  percorso non deve regalarne altro nel modulo successivo: il dono e' per modulo e
  idempotente.

### 8.3 La Taverna e' chiusa fino a M5 — DECISO

Niente vetrina anticipata: la taverna si apre con tutto il resto alla fine del
percorso. Durante l'onboarding il miele arriva solo dai doni vincolati, e il
giocatore scopre la fonte ricorrente quando ne ha davvero bisogno.

Due cose da spegnere insieme al cancello:

- il **badge di notifica** della taverna: `RefreshTavernNotificationBadgeAsync` e'
  chiamata a ogni ritorno all'hub, quindi un pallino rosso comparirebbe su un
  edificio chiuso;
- il messaggio "il miele si guadagna in taverna" non deve comparire prima che la
  taverna esista per il giocatore (§8.2).

### 8.4 Stanza 10 / stanza 20: comporre i numeri, non scriverli

`GameConfiguration.asset` dice `minibossEveryRooms: 10` e `finalBossRoom: 20`, e
`IsCurrentRoomMinibossRoom` esclude esplicitamente la stanza del boss finale:
quindi **miniboss alla 10, boss alla 20**, esattamente come da progetto. (Il `25`
che si legge come default nel sorgente di
[GameConfiguration.cs:144](Assets/_Project/Scripts/GameData/GameConfiguration.cs)
e' un valore mai usato: l'asset lo sovrascrive. Vale la pena allinearlo per non
rileggerlo come vero in futuro.)

Il testo del modulo si compone comunque da quei due valori invece di scriverli a
mano, cosi' non mente se un domani si ribilancia.

### 8.5 Account che hanno gia' fatto il vecchio tutorial

Migrazione server, una volta: se `tutorial_completed = 1` e non c'e' nessuna riga
`unlock_type='tutorial'`, inserire tutti e cinque i moduli. Chi ha finito il
vecchio tutorial trova tutto aperto e la sezione tutorial consultabile a piacere.
Nessuna ricompensa retroattiva (niente 80 miele agli account vecchi): il claim e'
per modulo e quei moduli risultano gia' riscossi.

### 8.6 Offline

Le ricompense passano dal server (`ServerProgressReady`). Oggi il tutorial finito
senza rete dice "Connessione al server necessaria" e non registra niente. Per il
percorso a moduli serve una decisione esplicita:

- **Proposta**: la lezione si puo' giocare offline, il **modulo non si chiude**
  finche' il claim non passa. Alla riconnessione il claim parte da solo (esiste gia'
  la coda `PendingMutationsReplayed` in
  [ServerSinglePlayerProgressClient.cs](Assets/_Project/Scripts/Network/ServerSinglePlayerProgressClient.cs)).
  Il messaggio deve dire "ricompensa in attesa di connessione", non "errore".

---

### 8.7 Le supreme: si provano in sandbox, non si regalano — DECISO

Ogni modulo di classe fa **usare** la suprema una volta nella stanza scriptata, con
mana concesso dallo script, ma non la concede all'account: resta una Tecnica da
comprare all'altare Tecniche (80 miele, il doppio della classe).

Regola di copy non negoziabile: **si avvisa prima, non dopo**. La tappa che precede
l'uso dice "questa e' la sua Tecnica: qui te la faccio provare, ma si impara al
Santuario". Un giocatore che prova un potere e solo dopo scopre di non possederlo si
sente derubato, e sarebbe l'unico punto del percorso capace di lasciare un'impressione
negativa.

Conseguenze tecniche:

- lo script deve poter **abilitare la suprema di una carta ignorando l'unlock**
  (oggi la disponibilita' passa da `SinglePlayerUnlockType.SecondAbility`), e questo
  permesso deve valere **solo** dentro il modulo, mai fuori;
- il mana della sandbox va portato al costo necessario (6 per Empower, 4 per Palla
  di fuoco, 3 per Ruba potenziamenti) senza far sembrare che si parta sempre pieni;
- dopo l'uso, lo spotlight va sull'altare Tecniche del Santuario con il prezzo.

Scartate: **regalarla** (tre tecniche = 240 vasetti, svuota l'altare proprio
all'inizio) e **solo descriverla** (Palla di fuoco che colpisce tutti va vista).

Nota: `AbilityManaCosts.IsSupremeImplemented` oggi ritorna sempre `true` — tutte e
nove le supreme hanno una regola nel motore — quindi il commento in
`SanctuaryCatalog` sul Negromante "non ancora implementato" e' vecchio.

Nota tecnica: `AbilityManaCosts.IsSupremeImplemented` oggi ritorna sempre `true`
(tutte e nove le supreme hanno una regola nel motore), quindi il commento in
`SanctuaryCatalog` che dice "quella del Negromante non lo e' ancora" e' vecchio.

### 8.8 Il Ladro non ha un'abilita' da premere

`ManaActionPolicy.HasActivatablePrimary` esclude **Ladro e Barbaro**: la loro prima
abilita' e' passiva e costa 0 mana (il Ladro ritira un dado da solo). Il modulo del
Ladro quindi **non puo'** dire "premi ABILITA'": deve far vedere la passiva mentre
scatta, con lo spotlight sul tiro ritirato.

Non e' un problema, e' il contenuto migliore del modulo: e' li' che si insegna che
esistono due tipi di abilita'. Ma va progettato apposta — una tappa che aspetta un
evento del motore invece di un tap — ed e' il motivo per cui il Ladro sta bene come
**terzo**: arriva dopo due classi ad attivazione, quindi la differenza si nota.

**La scena e' scriptata, non lasciata al caso.** `ScriptAdventureTutorialCombatResult`
gia' sostituisce i valori dei dadi mantenendo le regole del resolver: la stessa
tecnica serve qui, con un vincolo in piu' — il **primo** tiro del Ladro deve perdere
lo scontro e il **ritiro** deve vincerlo. Serve quindi poter truccare i due tiri in
modo indipendente (oggi `ScriptRoll` prende primo e secondo valore: e' gia' la forma
giusta) e verificare che il resolver applichi davvero la passiva su quel tiro.

Sequenza della tappa:

1. il Ladro attacca, spotlight sul dado — esce **basso**, il totale perde;
2. pausa: "stavi per perdere lo scontro";
3. la passiva scatta da sola, spotlight sul **secondo** dado — il totale ribalta;
4. Fleck: "non hai premuto niente. Alcune classi hanno abilita' passive, sempre
   attive e senza costo in mana."

Se un domani la passiva del Ladro cambia effetto, questa scena va rivista: e'
l'unico punto del percorso in cui il contenuto didattico dipende da una regola
specifica del motore.

## 9. Casi limite (il vero contenuto del collaudo)

| # | Situazione | Comportamento richiesto |
| --- | --- | --- |
| 1 | Chiude l'app a meta' di un modulo scriptato | Al rientro: hub, modulo non completato, si rientra dall'indice tutorial. Nessuna ricompensa parziale. |
| 2 | Chiude l'app **dopo** il claim ma **prima** del tour | Al rientro il tour dovuto riparte (`AccardTutorialPending`). Il modulo resta completato. |
| 3 | Premi Home durante un tour | Il tour si chiude, l'hub mostra evidenziata la destinazione del tour. Rientrando, il tour riparte da capo. |
| 4 | Tour d'acquisto e il giocatore annulla la conferma | Il tour resta in attesa; Fleck ripete l'istruzione. Non si esce dal Santuario finche' non compra (l'unico modo e' Home, caso 3). |
| 5 | Tour d'acquisto ma il giocatore **possiede gia'** la classe | Il tour si salta intero e passa al modulo dopo (account migrati, §8.5, e account di test). |
| 6 | Miele insufficiente al tour d'acquisto (es. l'ha speso altrove) | Non deve poter succedere: durante l'onboarding gli unici acquisti possibili sono quelli guidati. Se succede lo stesso: Fleck lo dice e il tutorial concede il mancante (claim idempotente separato) invece di bloccare. |
| 7 | Claim inviato due volte (rete lenta, doppio tap) | Idempotente per `moduleId`: seconda risposta senza concessioni. |
| 8 | Server down a fine modulo | §8.6: modulo non chiuso, messaggio d'attesa, claim in coda. |
| 9 | Logout e login con un altro account a meta' percorso | Lo stato viene dal server: il nuovo account riparte dal suo stage. I PlayerPrefs dei tour vanno azzerati al cambio account (altrimenti l'account nuovo non vede i tour). |
| 10 | Sblocchi a mano dall'admin panel su un account di test | Deve poter (a) marcare/smarcare singoli moduli, (b) azzerare tutto il percorso. Senza questo il collaudo richiede un account nuovo per ogni giro. |
| 11 | Rotazione schermo durante un tour | Spotlight e pannello si riposizionano; il tour non salta di tappa. Va provato in entrambi gli orientamenti su ogni tour. |
| 12 | Tap su una zona chiusa | Messaggio "Prima completa: <modulo>", nessuna navigazione, nessun suono d'errore aggressivo. |
| 13 | Il giocatore ha gia' il capitolo 1 (admin) ma stage < 5 | I cancelli vincono sul possesso: la riga resta chiusa finche' il percorso non finisce. Deve essere una regola scritta, non un caso non gestito. |
| 14 | WebGL/PWA con build in cache vecchia | Nota nota del progetto: verificare sempre `Last-Modified` prima di dichiarare un bug del tutorial su web. |

---

## 10. Piano di implementazione

Ordinato per rendere ogni fase **collaudabile da sola**. Nessuna fase lascia il
gioco in uno stato peggiore della precedente.

**Fase 1 — Fondamenta invisibili. FATTA (2026-08-14).**
`SinglePlayerUnlockType.TutorialModule`, catalogo moduli su server e client,
endpoint `ClaimTutorialModuleReward` + test server (idempotenza, ordine, catalogo,
migrazione §8.5), comandi admin (§9.10). Il gioco non cambia.

Cosa e' venuto fuori strada facendo:

- l'`unlock_type` dei moduli e' **`tutorialModule`**, non `tutorial`: quel nome era
  gia' il pseudo-tipo con cui l'admin tocca la colonna `tutorial_completed`;
- il dono in miele si **deriva dal listino** invece di stare nel catalogo (§6.2);
- il claim rifiuta i moduli **fuori ordine**: senza, un client modificato poteva
  riscuotere `m5` per primo e portarsi a casa capitolo e Seconda Chance senza aver
  giocato niente;
- togliere il tutorial dall'admin ora cancella anche i moduli, altrimenti
  "rifai l'onboarding" lasciava il percorso segnato come gia' fatto e il backfill
  al primo contatto col server rimetteva tutto com'era;
- il cambio delle classi starter (§8.1) **non** e' in questa fase: farlo ora, con i
  moduli non ancora giocabili, lascerebbe un giocatore nuovo col solo Guerriero e
  zero miele per comprare il resto. Va in Fase 5, insieme ai moduli.

**Fase 2 — Cancelli. FATTA.**
`TutorialFlowState` + `TutorialGate` (logica pura in GameData, 11 test a tabella) e
l'applicazione ai bottoni in `BattleBoardController.TutorialGates.cs`, cablata su
hub, campagna, avventura, santuario e negozio.

Due cose emerse cablando:

- il **pulsante spento non puo' parlare** (Unity non gli manda l'evento): a spiegare
  la porta chiusa e' l'hotspot disegnato sullo sfondo, che resta interattivo;
- `SetModeSelectionButtonsInteractable(true)` riaccende tutto a fine cinematica, e
  quindi deve riapplicare i cancelli: senza, una zona chiusa si riapriva a ogni zoom
  sull'hub.

Il negozio premium non e' "non cliccabile": durante l'onboarding
`VisiblePremiumProducts()` restituisce zero prodotti, cosi' la sezione non esiste
proprio.

**Fase 3 — Indice tutorial. FATTA.**
La riga TUTORIAL apre l'elenco dei moduli con i tre stati. L'elenco **riusa la
schermata dei capitoli** invece di avere un pannello suo: stessa griglia, stesso
ritorno all'hub, una schermata in meno da tenere allineata.

Rimossi: il tutorial a 4 immagini (`StartTutorial`, `AdvanceTutorial`,
`ShowTutorialPage`, `tutorialAdvanceButton`), il pannello guidato a 9 pagine e i 14
campi che li tenevano in piedi.

**Fase 4 — `GuidedTour` + i tour. FATTA.**
Il servizio (`BattleBoardController.GuidedTour.cs`) prende una lista di tappe con
tre modi di avanzare: CONTINUA, tocca il bersaglio, aspetta un evento di gioco.

L'acquisto guidato non ha logica propria, come previsto: e' una tappa che aspetta
`class-purchased:mage`, e l'evento lo emette **l'acquisto vero** del Santuario. Non
esiste una finta conferma del tutorial da tenere allineata con quella reale.

Home interrompe il tour senza segnarlo visto (§9.3), e il logout dimentica i tour
visti (§9.9).

**Fase 5 — Moduli e classi starter. FATTA (con un residuo, vedi sotto).**
Mago e Ladro sono passati da starter gratuite ad acquistabili a 40; la vecchia
dotazione resta a chi aveva finito il tutorial monolitico (`LegacyTutorialClassIds`,
`MergeLegacyTutorialClasses`).

La lezione di classe e' **uno stampo solo**, parametrico su `HeroClass`: mana,
abilita', tecnica e aura si compongono da `AbilityManaCosts`, `HeroClassFamily` e
`CardRulesGlossary` invece di essere riscritte tre volte. Il ramo "abilita' passiva"
esiste gia' per il Ladro, quindi copre il Barbaro senza altro codice.

**Fase 6 — Copy e localizzazione. FATTA per l'italiano.**
Chiavi in `GameTextKeys.Adventure` (titoli, sottotitoli e presentazione per modulo,
composte dall'id invece che da un elenco parallelo di costanti) e voci nel catalogo
italiano. Le altre quattro lingue restano da tradurre: senza traduzione il gioco
ripiega sui testi di `TutorialModuleCatalog.DisplayText`, non su una chiave grezza.

### Cosa resta

- **Contenuto scriptato delle lezioni di classe**: oggi M1/M2/M3 sono tour spiegati,
  non ancora battaglie in cui premi tu l'abilita'. Lo stampo e' in piedi: manca la
  run scriptata sotto, con la prova della suprema in sandbox (§8.7) e la scena a
  tiro ribaltato del Ladro (§8.8).
- **M0 e M5 condividono la stessa run guidata**: M0 dovrebbe fermarsi prima di
  vantaggio e abilita', oggi la gioca intera.
- **Traduzioni** in inglese, spagnolo, francese e tedesco.
- **Illustrazioni** dei moduli (`UI/Tutorial/<id>`): senza, la griglia ripiega sulla
  copertina del tutorial.

---

## 11. Decisioni

### Prese (2026-08-14) — il percorso e' chiuso, si puo' partire con la Fase 1

- **Miele solo in taverna.** I 40 vasetti sono un dono vincolato, esatti al costo
  della classe, non un guadagno (§8.2). Totale regalato 80, speso 80.
- **Percorso class-centrico**: un modulo per classe con abilita' + suprema + aura,
  intervallato dagli acquisti (§4).
- **Guerriero unica starter.** Mago e Ladro diventano acquistabili a 40 al
  Santuario; i tour d'acquisto si saltano da soli per chi possiede gia' la classe
  (§8.1).
- **Supreme in sandbox**: provate una volta nel modulo, mai concesse, con avviso
  *prima* dell'uso che si imparano al Santuario (§8.7).
- **Abilita' passive**: il modulo del Ladro le insegna con una scena scriptata in
  cui il primo tiro perde e il ritiro automatico ribalta lo scontro (§8.8).
- **Il mana** entra nel modulo del Guerriero, con la sua abilita' da 5 (§4, M1).
- **Taverna chiusa fino a M5**, come tutto il resto dell'hub. Nessuna vetrina
  anticipata (§8.3).
- **Codex** consultabile: rimandato, non entra nella prima versione.

### Assunzioni prese in autonomia (dillo se non ti tornano)

- Dopo M5 l'hub si apre **tutto insieme**; l'Arena mantiene i requisiti di accesso
  che ha oggi (login), il tutorial non ne aggiunge e non ne toglie.
- I moduli delle sei classi avanzate (§4.7) **non** entrano nella prima versione:
  lo stampo viene costruito perche' li regga, ma il contenuto arriva dopo.
