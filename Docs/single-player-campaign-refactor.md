# Refactor Single Player: Campagna, Avventura e Hardcore

## Obiettivo

Ristrutturare il single player eliminando la divisione attuale tra `Builder` e `Draft`, e sostituendola con due modalita principali:

- `Avventura`
- `Hardcore`

Il gameplay base deve restare condiviso dove possibile, evitando di duplicare scene, UI e logiche di combattimento. Le differenze tra le modalita devono essere gestite tramite configurazioni, progressione e flussi dedicati.

## Decisione Architetturale Proposta

Usare una scena gameplay principale condivisa, pilotata da configurazioni diverse.

### Scene Proposte

#### Gameplay Scene

Scena unica usata per:

- stage Avventura
- tutorial guidato
- run Hardcore

La scena deve caricare dati diversi in base al contesto:

- modalita di gioco
- scenario
- nemici
- regole speciali
- reward
- tutorial steps
- eventuali tiri di dado scriptati
- eventi scriptati

#### Campaign / Adventure Scene

Schermata dedicata alla progressione Avventura.

Responsabilita:

- mostrare capitoli disponibili
- mostrare stage dentro ogni capitolo
- gestire capitoli bloccati e sbloccati
- permettere l'acquisto dei capitoli con i vasetti di miele
- mostrare ricompense e progresso

#### Shop / Unlock Scene o Panel

Schermata o pannello per usare i vasetti di miele.

Possibili acquisti:

- capitoli Avventura
- classi
- scenari
- seconde abilita delle classi
- sblocco Hardcore, se previsto come acquisto diretto o milestone

#### Main Menu / Mode Select

Schermata di ingresso single player.

Mostra:

- Avventura
- Hardcore

Hardcore deve risultare bloccata finche il player non soddisfa il requisito richiesto.

## Modalita Avventura

La modalita Avventura e la nuova campagna single player.

### Struttura

L'Avventura e divisa in:

- capitoli
- stage
- scenari fissi

Ogni stage deve sapere gia quale scenario andra ad affrontare il player. Non deve essere una modalita generata liberamente come la vecchia Builder.

### Capitoli

Ogni capitolo puo avere:

- costo in vasetti di miele
- lista stage
- scenario o set di scenari
- classi permesse
- reward previste
- requisiti di sblocco
- eventuali regole speciali

### Stage

Ogni stage puo definire:

- scenario fisso
- nemici iniziali
- mostri scriptati
- eventi scriptati
- reward base
- reward alla morte
- reward al completamento
- eventuali tiri di dado controllati
- eventuale sequenza tutorial

## Tutorial

Il tutorial deve essere il primo stage dell'Avventura.

Non dovrebbe essere una scena separata, perche deve insegnare il gioco vero usando:

- board reale
- UI reale
- dadi reali
- combattimento reale
- nemici reali o pseudo-reali
- regole il piu possibile vicine alla partita normale

### Tutorial Guidato

Il player deve essere obbligato a seguire un flusso guidato.

Elementi richiesti:

- blocco degli input non permessi
- highlight sugli elementi da toccare
- indicazione chiara dell'azione richiesta
- testi esplicativi dettagliati
- pulsanti avanti / continua
- mostri scriptati
- tiri di dado scriptati
- azioni obbligate
- ricompensa finale in vasetti di miele

### Tutorial Director

Serve un sistema dedicato, per esempio `TutorialDirector`, responsabile di:

- avanzare tra gli step
- abilitare solo gli input corretti
- mostrare highlight
- mostrare testo e pulsanti
- forzare tiri di dado quando richiesto
- generare o attivare mostri scriptati
- verificare che il player abbia completato l'azione richiesta

Esempio di step:

```text
Step 1
- mostra testo: "Questa e la tua plancia"
- evidenzia la plancia
- attende pressione su Avanti

Step 2
- evidenzia il dado
- forza risultato dado
- obbliga il player a tirare

Step 3
- evidenzia una cella valida
- permette solo quel movimento
- attende completamento movimento
```

## Modalita Hardcore

Hardcore sostituisce la vecchia esperienza Builder, con alcuni cambiamenti.

### Identita

Hardcore deve essere la modalita piu libera, rischiosa e rigiocabile.

Caratteristiche:

- eredita la struttura principale della Builder attuale
- non usa il flusso guidato dell'Avventura
- puo avere generazione o scelta piu libera degli elementi
- produce reward in vasetti di miele
- viene sbloccata tramite progressione o spesa di vasetti di miele

### Sblocco

Hardcore non deve essere disponibile subito.

Possibili requisiti:

- completamento tutorial
- acquisto con vasetti di miele
- completamento del primo capitolo
- combinazione di completamento tutorial e costo in miele

Decisione da confermare.

## Vasetti di Miele

I vasetti di miele diventano la currency single player principale.

### Utilizzi

Servono per:

- comprare oggetti
- sbloccare classi
- sbloccare scenari
- sbloccare capitoli Avventura
- sbloccare seconde abilita delle classi
- sbloccare Hardcore, se deciso

### Guadagno

Il player guadagna miele:

- completando il tutorial
- completando stage Avventura
- morendo in Avventura, con calcolo reward
- giocando Hardcore
- eventualmente completando obiettivi o missioni

### Reward alla Morte

Quando il player muore:

- viene calcolata una quantita di vasetti di miele
- il player puo accettare la reward normale
- il player puo guardare una pubblicita per triplicare la reward

Esempio:

```text
Reward base morte: 12 miele
Con pubblicita: 36 miele
```

## Pubblicita Reward

Quando disponibile, la pubblicita permette di triplicare la reward calcolata.

Serve separare:

- calcolo reward
- richiesta visualizzazione ads
- conferma completamento ads
- applicazione moltiplicatore

Questo evita che la logica economica dipenda direttamente dal provider pubblicitario.

## Progressione Player

Serve un nuovo modello di salvataggio/progressione single player.

Campi proposti:

```text
PlayerProgress
- honey
- tutorialCompleted
- hardcoreUnlocked
- unlockedChapters
- unlockedStages
- unlockedClasses
- unlockedScenarios
- unlockedSecondAbilities
- completedStages
- completedChapters
```

Da valutare se separare:

- progressione permanente
- stato run corrente
- dati temporanei di sessione

## Configurazioni Dati

Per evitare scene duplicate, molte informazioni devono diventare data-driven.

### Game Mode Config

```text
GameModeConfig
- modeId
- displayName
- gameplayRules
- rewardRules
- unlockRequirement
```

### Adventure Chapter Config

```text
AdventureChapterConfig
- chapterId
- displayName
- unlockCostHoney
- requiredCompletedChapter
- stages
- rewardPreview
```

### Adventure Stage Config

```text
AdventureStageConfig
- stageId
- chapterId
- scenarioId
- enemySetup
- scriptedEvents
- scriptedDiceRolls
- tutorialSequence
- completionReward
- deathRewardRules
```

### Unlockable Config

```text
UnlockableConfig
- unlockableId
- type
- costHoney
- requirement
- targetId
```

Tipi possibili:

- class
- scenario
- chapter
- secondAbility
- hardcoreMode

## Seconda Abilita delle Classi

Ogni classe dovra poter avere una seconda abilita sbloccabile.

Da definire:

- se la seconda abilita e passiva o attiva
- se ogni classe ne ha una sola o piu alternative
- se viene equipaggiata manualmente
- se e sempre attiva dopo lo sblocco
- costo in miele
- requisito minimo per acquisto

Proposta iniziale:

- ogni classe ha una seconda abilita unica
- la seconda abilita e bloccata di default
- una volta acquistata, diventa disponibile in tutte le modalita single player

## Flusso Player Proposto

```text
1. Player apre Single Player
2. Vede Avventura disponibile e Hardcore bloccata
3. Entra in Avventura
4. Gioca tutorial guidato
5. Completa tutorial
6. Riceve vasetti di miele
7. Usa miele per comprare il primo capitolo
8. Gioca stage del primo capitolo
9. Se muore, riceve miele calcolato
10. Puo guardare pubblicita per triplicare il miele
11. Usa miele per comprare capitoli, classi, scenari o abilita
12. Sblocca Hardcore tramite requisito da decidere
```

## Rischi Principali

### Duplicazione Scene

Creare una scena diversa per Avventura e Hardcore sembra semplice all'inizio, ma rischia di duplicare:

- UI
- board
- combattimento
- dadi
- nemici
- reward
- fix futuri

Proposta: evitare duplicazione e rendere il gameplay configurabile.

### Tutorial Troppo Separato

Un tutorial finto rischia di insegnare meccaniche diverse dal gioco reale.

Proposta: tutorial come stage reale con regia scriptata.

### Economia Non Bilanciata

Il miele diventa centrale. Se reward, costi e pubblicita non sono separati bene, sara difficile bilanciare.

Proposta: centralizzare calcoli in un `HoneyRewardService` o sistema equivalente.

### Salvataggi

La progressione permanente avra piu responsabilita di prima.

Proposta: definire chiaramente cosa e permanente e cosa appartiene alla run corrente.

## Fasi di Implementazione Proposte

## Audit Stato Attuale

### Sintesi

Il single player attuale e gia centrato su una "campagna" roguelite con:

- scelta iniziale Builder o Draft
- costruzione mazzo iniziale
- scelta porte
- stanze randomiche
- scenari selezionati da catalogo
- combattimenti
- reward in esperienza
- salvataggio della run corrente

Questo conferma che il refactor piu sensato non e duplicare scene, ma separare meglio:

- ingresso modalita
- configurazione run
- progressione permanente
- progressione temporanea della run
- dati capitolo/stage

### File Principali Trovati

#### Controller principale

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.cs
```

Contiene molti campi condivisi del controller:

- pannelli Builder
- pannelli Draft
- pannello selezione modalita campagna
- stato run
- stato draft/deployment
- riferimenti UI
- consumabili campagna

E il punto centrale da cui oggi vengono orchestrati quasi tutti i flussi single player.

#### Selezione modalita e creazione UI

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.SetupViews.cs
```

Responsabilita rilevanti:

- `ShowModeSelection`
- `StartCampaignMode`
- `ShowCampaignModeSelection`
- `StartCampaignBuilderMode`
- creazione pulsanti `BUILDER` e `DRAFT`
- creazione view `Initial Deck Builder`
- creazione view `Initial Draft`

Qui oggi nasce la distinzione Builder/Draft.

Implicazione refactor:

- questo e il primo punto da cambiare per introdurre `Avventura` e `Hardcore`
- Builder puo diventare il flusso iniziale provvisorio di Hardcore
- Draft puo essere rimosso o lasciato temporaneamente nascosto durante la transizione

#### Builder iniziale

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.DeckBuilder.cs
Assets/_Project/Scripts/GameData/InitialDeckBuilder.cs
```

Responsabilita:

- comprare carte iniziali con Essenze
- scelta casuale, per classe o per forza
- controllo costo minimo per completare il mazzo
- avvio campagna tramite `StartBuiltCampaign`

Oggi il Builder non e una modalita completa: e un modo di costruire il mazzo prima di entrare nella campagna attuale.

Implicazione refactor:

- il futuro `Hardcore` puo riusare questa logica come ingresso iniziale
- "Essenze" e "vasetti di miele" non vanno confusi: le Essenze sono budget interno di deck building, il miele e progressione permanente

#### Draft iniziale

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.InitialDraft.cs
Assets/_Project/Scripts/GameData/FormationDraftService.cs
```

Responsabilita:

- scelta capitano
- offerte draft iniziali
- selezione pacchetti
- avvio campagna tramite `StartDraftBuiltCampaign`

Anche Draft e solo un ingresso alternativo alla stessa campagna.

Implicazione refactor:

- puo essere eliminato dal menu pubblico
- eventualmente si puo conservare come debug/dev flow se utile

#### Progressione run e fine stanza

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.CampaignProgress.cs
Assets/_Project/Scripts/GameCore/CombatResult.cs
```

Responsabilita:

- vittoria/sconfitta stanza
- `CheckEndGame`
- avanzamento stanza
- retry stanza
- game over
- completamento campagna
- reward esperienza
- `RunProgressState`
- `RoomReward`

La progressione attuale e temporanea della run:

- livello player
- esperienza corrente
- esperienza totale
- esperienza spendibile
- stanze superate
- dado vigore per livello

Implicazione refactor:

- `RunProgressState` non deve diventare la progressione permanente
- serve un nuovo modello separato per miele/unlock/tutorial/capitoli
- le reward miele alla morte dovranno agganciarsi vicino a `CheckEndGame`, ma restando in un servizio dedicato

#### Stanze, porte e scenari

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.Campaign.cs
Assets/_Project/Scripts/GameData/RoomType.cs
Assets/_Project/Scripts/GameData/ScenarioDefinition.cs
Assets/_Project/Scripts/GameData/ScenarioCatalog.cs
Assets/_Project/Data/Scenarios/*.asset
Assets/_Project/Resources/ScenarioCatalog.asset
```

Responsabilita:

- `BeginRoomChoice`
- `PrepareCampaignDoors`
- `ChooseCampaignDoor`
- `RollCampaignRoom`
- `LoadCampaignRoomScenario`
- `ApplyScenario`
- scelta miniboss/boss
- pool scenari da catalogo

Oggi gli scenari sono gia dati esterni tramite asset.

Implicazione refactor:

- Avventura puo usare scenari fissi bypassando il roll delle porte
- Hardcore puo riusare il roll attuale delle porte
- serve introdurre uno strato tipo `StageConfig` sopra `LoadScenario`

#### Salvataggio run corrente

```text
Assets/_Project/Scripts/GameData/CampaignRunSave.cs
Assets/_Project/Scripts/GameData/CampaignRunSaveService.cs
Assets/_Project/Scripts/Presentation/BattleBoardController.RunSave.cs
Assets/_Project/Tests/EditMode/CampaignRunSaveTests.cs
```

Responsabilita:

- salvare e caricare run corrente
- PlayerPrefs key: `AccardND.CampaignRun`
- snapshot progressione run
- snapshot mazzo campagna
- snapshot scenario/regole stanza
- snapshot consumabili

Punto importante:

- questo save e per riprendere una run interrotta
- non e un salvataggio permanente di account/progressione

Implicazione refactor:

- creare un nuovo save separato, per esempio `SinglePlayerProgressSave`
- non mescolare miele e unlock dentro `CampaignRunSave`, salvo solo dati strettamente necessari alla run corrente

#### Consumabili campagna

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.Consumables.cs
```

Consumabili attuali:

- Detector
- Seconda Chance
- Defrost
- Empower
- Doppia EXP

Oggi sono stato di run, salvato in `CampaignRunSave`.

Implicazione refactor:

- possono diventare oggetti acquistabili con miele
- ma bisogna decidere se sono permanenti, consumabili di run, o comprati prima di una run

### Architettura Effettiva Attuale

```text
Mode Selection
-> Campaign Mode Selection
   -> Builder iniziale
      -> CampaignDeckState
      -> BeginRoomChoice
   -> Draft iniziale
      -> CampaignDeckState
      -> BeginRoomChoice

BeginRoomChoice
-> roll porte
-> scelta stanza
-> scenario
-> combat / non-combat
-> reward EXP
-> prossima stanza o game over
```

### Primo Punto di Refactor Consigliato

Il primo refactor piccolo dovrebbe introdurre un concetto esplicito di modalita single player, senza cambiare ancora il gameplay.

Proposta:

```text
SinglePlayerMode
- Adventure
- Hardcore
```

Poi mappare provvisoriamente:

```text
Hardcore -> flusso Builder attuale
Adventure -> placeholder / futuro menu capitoli
Draft -> nascosto o debug-only
```

Questo permette di cambiare il menu e preparare i dati futuri senza rompere subito:

- deck builder
- scelta porte
- run save
- progressione EXP
- combattimento

### Implementato Nel Primo Refactor

File aggiunti:

```text
Assets/_Project/Scripts/GameData/SinglePlayerMode.cs
Assets/_Project/Scripts/GameData/SinglePlayerProgressSave.cs
Assets/_Project/Tests/EditMode/SinglePlayerProgressServiceTests.cs
```

File modificati:

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.cs
Assets/_Project/Scripts/Presentation/BattleBoardController.SetupViews.cs
```

Cambiamenti introdotti:

- aggiunto enum `SinglePlayerMode` con `Adventure` e `Hardcore`
- aggiunto `SinglePlayerProgressSave`
- aggiunto `SinglePlayerProgressService`
- aggiunto store PlayerPrefs separato dalla run corrente
- aggiunta key PlayerPrefs `AccardND.SinglePlayerProgress`
- aggiunti test EditMode per miele, spesa miele e unlock
- rinominata la scelta interna campagna da `BUILDER` / `DRAFT` a `AVVENTURA` / `HARDCORE`
- `Avventura` per ora e un placeholder
- `Hardcore` per ora continua ad aprire il Builder attuale come ponte temporaneo

Nota importante:

Hardcore ha gia il campo permanente `hardcoreUnlocked`.

### Implementato Primo Flusso Avventura/Hardcore

Prima feature visibile implementata nella schermata single player:

```text
Avventura
- se il tutorial non e completato:
  - marca tutorialCompleted
  - assegna 60 miele
  - aggiorna la UI progressione
- se il tutorial e gia completato:
  - mostra placeholder dei futuri capitoli/stage

Hardcore
- se non e sbloccata:
  - richiede 50 miele
  - scala il miele
  - marca hardcoreUnlocked
  - aggiorna la UI progressione
- se e sbloccata:
  - avvia il Builder attuale come modalita Hardcore provvisoria
```

UI aggiunta:

```text
MIELE X - Tutorial completato/da completare - stato Hardcore
```

Nota:

Questo flusso usa ancora il repository locale per procedere rapidamente nel gameplay. La stessa logica di unlock dovra poi chiamare `ServerSinglePlayerProgressClient.PurchaseUnlockAsync` quando collegheremo il menu single player alla connessione autenticata.

### Implementata Prima Schermata Avventura

Premendo `Avventura` ora non viene piu assegnata subito la reward. Si apre una schermata capitoli/stage.

La schermata usa una griglia di riquadri:

- immagine/quadro quadrato placeholder
- nome capitolo sotto il quadro
- stato/costo/boss dentro il quadro
- colori temporanei, da sostituire con immagini definitive

Lista attuale:

```text
Tutorial
- primo stage
- ricompensa provvisoria: 60 miele

Capitolo 1 - La Nebbia di Bragus
- scenario: Nebbia
- boss: Bragus
- costo provvisorio: 25 miele

Capitolo 2 - I Rampicanti di Trentor
- scenario: Rampicanti
- boss: Trentor
- costo provvisorio: 75 miele

Capitolo 3 - Gli Specchi di Medusa
- scenario: Specchi
- boss: Medusa
- costo provvisorio: 120 miele

Capitolo 4 - La Cosmica di Palatir
- scenario: Cosmica
- boss: Palatir
- costo provvisorio: 180 miele
```

Comportamento:

- il Tutorial e sempre cliccabile
- al primo click completa il tutorial e assegna miele
- i capitoli si comprano con miele
- i capitoli comprati vengono marcati come unlock `Chapter`
- i capitoli gia sbloccati mostrano placeholder di ingresso stage

Nota:

La lista e ancora in codice, non data-driven. Il prossimo step corretto e spostarla in config/ScriptableObject o in un catalogo dati server-authoritative.

### Nuovo Salvataggio Necessario

Serve un nuovo salvataggio permanente separato:

```text
SinglePlayerProgressSave
- version
- honey
- tutorialCompleted
- hardcoreUnlocked
- unlockedChapters
- unlockedStages
- unlockedClasses
- unlockedScenarios
- unlockedSecondAbilities
```

Possibile servizio:

```text
SinglePlayerProgressService
- Load
- Save
- AddHoney
- TrySpendHoney
- IsUnlocked
- Unlock
```

Questo servizio deve vivere in `GameData` o in un namespace equivalente, con test EditMode come per `CampaignRunSaveService`.

### Progressione Server-Authoritative

Decisione architetturale:

La progressione permanente single player non deve essere autoritativa sul client.

Il client puo:

- mostrare miele e unlock
- chiedere di comprare un contenuto
- chiedere di riscattare una reward
- tenere una cache locale per UX/offline/dev

Il server deve:

- validare reward
- calcolare miele guadagnato
- verificare pubblicita completata prima del moltiplicatore
- scalare miele sugli acquisti
- sbloccare capitoli/classi/scenari/abilita
- salvare la progressione permanente

Il client non deve mai poter inviare uno stato finale del tipo:

```text
"ho 99999 miele"
"ho sbloccato tutte le classi"
"ho completato il tutorial"
```

Deve invece inviare richieste/eventi validabili:

```text
PurchaseUnlock(type, id)
ClaimTutorialReward(tutorialRunId)
ClaimDeathReward(runId, resultSummary)
ClaimAdMultiplier(adImpressionId, rewardClaimId)
```

La risposta del server deve essere il nuovo stato autorevole:

```text
SinglePlayerProgressSave aggiornato
```

### Repository Progressione

Nel codice il primo refactor ha introdotto una separazione:

```text
ISinglePlayerProgressRepository
LocalSinglePlayerProgressRepository
SinglePlayerProgressService
```

`LocalSinglePlayerProgressRepository` usa PlayerPrefs ed e esplicitamente non autoritativo.

Uso previsto:

- sviluppo locale
- cache temporanea
- fallback durante refactor

Uso non previsto:

- fonte affidabile per miele/unlock in produzione

Prossimo passaggio lato server:

```text
ServerSinglePlayerProgressRepository
- LoadProgress
- PurchaseUnlock
- ClaimReward
- ClaimAdMultiplier
```

Quando il server sara disponibile, `SinglePlayerProgressService` dovra delegare al repository server invece che a quello locale.

### Implementato: Layer Client Autoritativo

File aggiunti:

```text
Assets/_Project/Scripts/Network/ServerSinglePlayerProgressRepository.cs
Assets/_Project/Tests/EditMode/ServerSinglePlayerProgressRepositoryTests.cs
```

File modificati:

```text
Assets/_Project/Scripts/GameData/SinglePlayerProgressSave.cs
Assets/_Project/Tests/EditMode/SinglePlayerProgressServiceTests.cs
Assets/_Project/Tests/EditMode/AccardND.GameCore.Tests.asmdef
```

`ServerSinglePlayerProgressRepository` (namespace `AccardND.Network`) implementa
`ISinglePlayerProgressRepository` col server come fonte autoritativa:

- letture sincrone (miele/unlock/tutorial) servite dalla UI da una cache locale che
  contiene l'ultima istantanea nota
- `RefreshAsync()` carica lo stato dal server e sostituisce la cache; se il server non
  e raggiungibile conserva la cache (uso offline) e segnala `IsSynced = false`
- `PurchaseUnlockAsync(type, id)` passa dal server e, in caso di successo, sostituisce la
  cache col nuovo stato; se il server rifiuta (miele insufficiente, unlock non valido,
  offline) l'eccezione viene propagata al chiamante con il messaggio del server
- i mutatori locali (`AddHoney`, `TrySpendHoney`, `SetTutorialCompleted`,
  `SetHardcoreUnlocked`, `Unlock`) lanciano `NotSupportedException`: in modalita
  autoritativa il client non puo modificare da solo miele/unlock. E un forcing function
  voluto: chi adotta questo repository deve usare il percorso async server

Per la cache e stato aggiunto `ApplyAuthoritative(snapshot)` a
`ISinglePlayerProgressRepository` + `LocalSinglePlayerProgressRepository` +
`SinglePlayerProgressService`: sostituisce l'intero stato con un'istantanea autoritativa
(clonando le liste per evitare aliasing) e la persiste nello store locale.

Risolve il mismatch sincrono/asincrono: la vecchia interfaccia repository e sincrona,
mentre le chiamate al server sono async. Le letture restano sincrone dalla cache; le
scritture autoritative sono async esplicite.

Note di collocazione:

- il repository server vive in `AccardND.Network` (che referenzia `GameData`) per evitare
  una dipendenza circolare: `GameData` non deve conoscere il layer di rete
- l'asmdef dei test EditMode ora referenzia anche `AccardND.Network`

### Aggancio In-Game (Completato)

La connessione e l'aggancio sono ora implementati. Il controller single player usa il server
come fonte autoritativa quando raggiungibile, con fallback locale trasparente.

File aggiunti:

```text
Assets/_Project/Scripts/Network/SinglePlayerServerLink.cs
Assets/_Project/Scripts/Network/GuestCredentials.cs
```

File modificati:

```text
Assets/_Project/Scripts/Presentation/BattleBoardController.cs
Assets/_Project/Scripts/Presentation/BattleBoardController.SetupViews.cs
```

Come funziona:

- `SinglePlayerServerLink` (MonoBehaviour headless, senza UI) apre una WebSocket, si autentica
  come ospite (register -> login) usando `GuestCredentials.Derive()`, che replica lo schema di
  `PvpBootstrap` cosi single player e PvP condividono la stessa identita ospite. Espone un
  `ServerSinglePlayerProgressRepository` sulla connessione autenticata.
- Il controller chiama `EnsureServerProgressAsync()` all'apertura del menu campagna: se il
  server risponde, specchia lo stato autoritativo nella cache locale (`ApplyAuthoritative`) che
  la UI continua a leggere. Se il server non risponde o rifiuta il login, resta il locale.
- I tre siti di scrittura (tutorial, acquisto capitolo, sblocco Hardcore) passano dal server
  quando `ServerProgressReady`; l'errore del server (es. miele insufficiente) viene mostrato a
  video. Offline si usa il percorso locale come prima, quindi il gioco non si rompe mai.
- Entrando in PvP (`StartPvpMode`) il link viene chiuso (`Shutdown`) per non avere due socket
  sullo stesso account ospite; si riconnette alla successiva apertura del menu campagna.

Identita ospite: il link usa sempre l'account ospite per-dispositivo. Su device con Unity
Authentication il PvP puo usare un'identita UGS diversa: in quel caso il miele single player
resta legato all'account ospite. L'unificazione dell'identita e un affinamento successivo.

Limite noto (combattimento client-side): il server non puo validare pienamente gli eventi di
gioco single player, quindi le reward morte si fidano di un sommario riportato dal client ma
limitato con cap lato server (vedi `CalculateDeathReward`). E comunque piu forte dell'autorita
client pura: il client non puo settare miele arbitrario.

### Ancora Da Fare

- Reward morte/ads: il contratto e il calcolo server sono attivi e testati, ma non c'e' ancora
  un aggancio nel gameplay ne la UI (accetta reward / guarda pubblicita per triplicare). Va
  collegato vicino a `CheckEndGame` in `BattleBoardController.CampaignProgress.cs` con una UI
  dedicata e l'integrazione del provider ads.
- Catalogo unlock server: i costi sono ancora hardcoded (allineati al client) in
  `SinglePlayerProgressService`; vanno spostati in config server-authoritative.
- Verifica ad reale (SSV lato provider) non integrata: per ora l'idempotenza e sull'adImpressionId.

### Implementato Lato Server/Protocollo

File aggiunti:

```text
Assets/_Project/Scripts/NetProtocol/SinglePlayerProgressMessages.cs
Assets/_Project/Scripts/Network/PvpServerMessageDispatcher.cs
Assets/_Project/Scripts/Network/ServerSinglePlayerProgressClient.cs
Server/AccardND.Server/Progression/SinglePlayerProgressService.cs
```

File modificati:

```text
Assets/_Project/Scripts/NetProtocol/Envelope.cs
Assets/_Project/Scripts/Network/AccardND.Network.asmdef
Assets/_Project/Scripts/PvpUi/PvpBootstrap.cs
AccardND.Network.csproj
Server/AccardND.Server/Data/AccardDatabase.cs
Server/AccardND.Server/Program.cs
Server/AccardND.Server/Sessions/MessageRouter.cs
```

Messaggi protocollo aggiunti:

```text
singleplayer.progress.get
singleplayer.progress.data
singleplayer.unlock.purchase
singleplayer.reward.tutorial
singleplayer.reward.death
singleplayer.reward.ad_multiplier
```

Attivi lato server in questa fase:

```text
singleplayer.progress.get
singleplayer.unlock.purchase
```

Solo DTO/contratto, non ancora attivi:

```text
singleplayer.reward.tutorial
singleplayer.reward.death
singleplayer.reward.ad_multiplier
```

Tabelle SQLite aggiunte:

```text
single_player_progress
- player_id
- honey
- tutorial_completed
- hardcore_unlocked
- updated_at

single_player_unlocks
- player_id
- unlock_type
- unlock_id
- unlocked_at
```

Regole temporanee acquisti server:

```text
chapter / chapter-1 = 25 miele
mode / hardcore = 50 miele
```

Nota:

Questi costi sono hardcoded temporanei nel server solo per validare il flusso anti-cheat. In seguito dovranno diventare catalogo/config server-authoritative.

Importante:

Le reward tutorial/morte/ads non devono essere attivate finche non esiste un modo server-side per validare l'evento. Per ora il contratto esiste, ma il server non concede miele da richieste reward del client.

### Implementato Ponte Client-Server

Classe aggiunta:

```text
ServerSinglePlayerProgressClient
```

Responsabilita:

- inviare `singleplayer.progress.get`
- inviare `singleplayer.unlock.purchase`
- ricevere `singleplayer.progress.data`
- convertire lo stato server in `SinglePlayerProgressSave`

Uso previsto:

```text
LoadProgressAsync()
PurchaseUnlockAsync(type, id)
```

Nota tecnica:

`PvpServerClient` usa una coda messaggi condivisa. Per evitare consumer concorrenti e stato perso, e stato introdotto `PvpServerMessageDispatcher`.

Responsabilita del dispatcher:

- drenare `PvpServerClient` in un solo punto
- completare richieste che aspettano una risposta specifica
- propagare i messaggi non gestiti tramite evento `UnhandledMessage`
- gestire timeout

`ServerSinglePlayerProgressClient` ora puo usare il dispatcher invece di drenare direttamente la coda.

### Implementato Nel Bootstrap PvP

`PvpBootstrap` ora:

- crea `PvpServerMessageDispatcher` subito dopo `PvpServerClient`
- drena i messaggi tramite `dispatcher.Pump()`
- riceve i messaggi non gestiti tramite `dispatcher.UnhandledMessage`
- invia i messaggi tramite `dispatcher.SendAsync`
- espone `ServerDispatcher`
- espone `IsAuthenticated`

Questo rende possibile costruire `ServerSinglePlayerProgressClient` sulla connessione gia autenticata, senza aprire una seconda WebSocket e senza consumare direttamente la coda del client.

### Dove Agganciare le Reward Miele

Punti probabili:

- sconfitta definitiva: `BattleBoardController.CampaignProgress.cs`, ramo `GAME OVER`
- completamento tutorial: futuro `TutorialDirector`
- completamento stage Avventura: futuro flusso stage
- completamento/avanzamento Hardcore: vicino agli esiti stanza o run

La reward miele non dovrebbe essere calcolata direttamente nel controller. Meglio un servizio dedicato:

```text
HoneyRewardService
- CalculateDeathReward
- CalculateStageCompletionReward
- ApplyAdMultiplier
```

### Conclusione Audit

La base attuale e adatta al refactor proposto:

- il gameplay e gia centralizzato
- gli scenari sono gia asset data-driven
- il save run e gia separato abbastanza bene
- Builder e Draft sono ingressi, non modalita profonde

Il rischio principale e che `BattleBoardController` e molto grande e contiene troppa orchestrazione. Conviene procedere per estrazioni progressive, iniziando da dati e servizi piccoli testabili.

### Fase 1: Analisi Stato Attuale

- trovare codice Builder e Draft
- capire come vengono caricate scene e modalita
- individuare sistema salvataggi esistente
- individuare sistema reward esistente
- individuare gestione scenari/classi

### Fase 2: Modello Dati

- introdurre concetto di `GameMode`
- introdurre config per Avventura e Hardcore
- introdurre progressione player single player
- introdurre currency miele

### Fase 3: Menu e Flussi

- aggiornare selezione single player
- aggiungere Avventura
- aggiungere Hardcore bloccata
- aggiungere schermata capitoli/stage
- aggiungere shop/unlock base

### Fase 4: Gameplay Scene Configurabile

- far partire la scena gameplay da una config
- supportare scenario fisso
- supportare reward per stage
- supportare modalita Hardcore con logica ex Builder

### Fase 5: Tutorial Director

- creare sistema step tutorial
- bloccare input non validi
- aggiungere highlight
- aggiungere testi e pulsanti avanti
- supportare dadi scriptati
- supportare mostri/eventi scriptati

### Fase 6: Economia e Ads

- calcolare reward miele alla morte
- applicare reward normale
- integrare triplicatore via pubblicita
- salvare miele aggiornato

### Fase 7: Seconda Abilita Classi

- definire dati abilita secondarie
- aggiungere unlock tramite miele
- integrare nel gameplay
- aggiornare UI classe/shop

## Decisioni Da Confermare

- Hardcore si sblocca pagando miele, completando tutorial, completando il primo capitolo o con una combinazione?
- Il primo capitolo si compra subito dopo il tutorial o viene sbloccato gratuitamente?
- Le classi si comprano solo nello shop o anche come reward capitolo?
- Gli scenari sono acquistabili singolarmente o legati ai capitoli?
- Le seconde abilita sono sempre attive dopo lo sblocco o vanno equipaggiate?
- La reward morte dipende da stage raggiunto, nemici uccisi, turni sopravvissuti, danni fatti o altri parametri?
- La pubblicita triplica solo reward morte o anche reward completamento stage?
