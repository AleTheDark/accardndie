# Debito tecnico / Cose da fare

Elenco delle cose che sappiamo di dover sistemare ma che abbiamo rimandato.
Formato: ogni voce ha priorità, contesto e cosa fare. Aggiorna la data quando tocchi una voce.

---

## Login / Autenticazione

### Migrare il login Google web da "Sign in with Google" a Unity Player Accounts
- **Priorità:** media (funziona oggi, ma il provider è deprecato)
- **Aggiornato:** 2026-07-20
- **Contesto:** il login Google su Web/PWA usa il provider **"Sign in with Google"** di Unity Authentication
  (`AuthenticationService.SignInWithGoogleAsync(idToken)` + One Tap con FedCM nel bridge
  `Assets/Plugins/WebGL/AccardNdWebSocket.jslib`). Nella dashboard Unity Cloud, aggiungendo il provider,
  compare l'avviso: *"Sign in with Google is being deprecated. We recommend using Google Play Games for
  Android or Unity Player Accounts (which supports web-based Google sign-in on all platforms) instead."*
- **Cosa fare:** valutare la migrazione a **Unity Player Accounts**, che gestisce il Google sign-in web su
  tutte le piattaforme senza il provider Google standalone. È un cambio del flusso lato client (non solo
  dashboard): probabile addio/riscrittura del bridge One Tap in `AccardNdWebSocket.jslib` e del ramo WebGL
  in `PvpUgsAuth.cs`.
- **Rischio se ignorato:** quando Unity dismette il provider, il login Google web smette di funzionare.

### Completare la configurazione Google Play Games per Android
- **Priorità:** alta quando si testerà l'APK (non è codice, è setup esterno)
- **Aggiornato:** 2026-07-20
- **Contesto:** il codice Android nativo è pronto (define `GPGS_AUTH` attivo, GameInfo configurato), ma il
  login riesce solo con la config esterna completa.
- **Cosa fare:**
  1. Google Play Console → Play Games Services: registrare la **SHA-1** del keystore di firma sull'OAuth
     client Android + aggiungere il proprio account come **tester**.
  2. Unity Cloud → Authentication: il provider **Google Play Games** è già aggiunto; verificare client ID
     (`866249556431-…`) e **client secret** del web client.

---

## Review codice — Auth (annotato 2026-07-20)

Osservazioni emerse rivedendo `PvpUgsAuth.cs`, `LoginScreenPrototype.cs`, `AccardNdWebSocket.jslib`.
Non sono bug bloccanti, ma debito di robustezza/UX/manutenzione.

### Login web: nessun fast-fail, attesa fino a 60s quando FedCM non può mostrarsi
- **Priorità:** media (UX)
- **Contesto:** `SignInWithWebGoogleAsync` fa polling fino a 60s. Tolta la rilevazione degli stati del
  "moment" (per il bug FedCM), se One Tap/FedCM non può comparire (nessuna sessione Google, cookie di terze
  parti bloccati, origin non autorizzata) niente fallisce subito: l'utente resta su "Accesso con Google..."
  per 60s e poi vede un timeout generico.
- **Cosa fare:** valutare un fallback con **`google.accounts.id.renderButton`** (bottone reale, robusto),
  oppure gestire l'errore FedCM, o accorciare il timeout con un messaggio di retry chiaro.

### Manca il logout / cambio account
- **Priorità:** media
- **Contesto:** con l'auto-resume, una volta loggato entri sempre diretto. Non c'è modo di disconnettersi o
  cambiare account (account Google sbagliato, device condiviso).
- **Cosa fare:** aggiungere un logout che pulisce la sessione UGS
  (`AuthenticationService.Instance.SignOut(clearCredentials: true)`) e riporta ai bottoni di login.

### Messaggi d'errore grezzi mostrati all'utente
- **Priorità:** bassa/media
- **Contesto:** `LoginScreenPrototype` mostra `exception.Message` e i codici del provider così come sono
  ("id provider not found", "credential_returned", "no-session", i nomi enum di `SignInStatus` GPGS):
  tecnici, in inglese, e trapelano dettagli interni.
- **Cosa fare:** mappare gli errori in messaggi amichevoli e localizzati; loggare il dettaglio grezzo solo
  in console.

### Costanti/config duplicate e hardcoded
- **Priorità:** bassa
- **Contesto:** il web client ID Google è ripetuto in più punti (template `index.html`, `GameInfo.cs`,
  jslib) e `ServerUrl = "wss://accardndie.com/ws"` è hardcoded in `LoginScreenPrototype`. Rischio di drift
  quando cambia uno solo.
- **Cosa fare:** centralizzare in un unico punto di config (es. ScriptableObject) letto da tutti.

### Timeout/cancellazione mancanti su resume e GPGS
- **Priorità:** bassa
- **Contesto:** `TryResumeSessionAsync` e i `TaskCompletionSource` di GPGS
  (`Authenticate`/`RequestServerSideAccess`) non hanno timeout: se una callback dell'SDK non scatta mai, il
  flusso può restare appeso (a differenza del path web che ha il guard a 60s).
- **Cosa fare:** aggiungere timeout/cancellazione anche a questi percorsi.

### `LoginScreenPrototype` costruisce tutta la UI in codice
- **Priorità:** bassa
- **Contesto:** il nome della classe dice "Prototype"; l'intera schermata è creata proceduralmente in
  `BuildInterface` (~250 righe). Difficile da mantenere, temizzare e localizzare.
- **Cosa fare:** pianificare la migrazione a una schermata basata su prefab, usando `MmoUiTheme` condiviso.

---

## Gioco — architettura & regole (annotato 2026-07-20)

> Nota positiva da preservare: le **regole di combattimento sono ben isolate in `GameCore`**
> (`CombatResolver` ~165 righe, `CombatModifiers`, `ClassMatchup`, boss) e **coperte da 15 test EditMode**.
> Questa separazione è il punto forte del progetto: va difesa.

### God object: `BattleBoardController` (~19.200 righe, 23 partial, 0 test)
- **Priorità:** alta (rischio strutturale)
- **Contesto:** tutta l'orchestrazione gameplay+presentazione (combattimento, schieramento, mercante,
  campagna, hint, consumabili, PvP…) vive in un'unica partial class gigante, senza test. Difficile da
  modificare senza regressioni; ogni feature lo gonfia ancora.
- **Cosa fare:** estrarre sistemi coesi in componenti/servizi separati e testabili (es. `CampaignFlow`,
  `MerchantService`, `DeploymentController`), lasciando al controller solo il wiring.

### File-mostro nel layer di presentazione
- **Priorità:** media
- **Contesto:** `PrototypeCardView` (~6.100 righe), `BattlePresentationAnimationPlayer` (~5.400),
  `BattleBoardController.Combat` (~3.850). Oltre alla dimensione, i nomi "Prototype" indicano codice mai
  consolidato. Zero marcatori `TODO/FIXME` in tutto il repo: il debito non è tracciato dove nasce.
- **Cosa fare:** spezzare per responsabilità; introdurre l'abitudine di marcare i punti provvisori con
  `// TODO` che rimandano a questo file.

### Rischio: logica di regole che filtra nella presentazione
- **Priorità:** media (da verificare)
- **Contesto:** con un `CombatResolver` testato da 165 righe accanto a un `BattleBoardController.Combat`
  non testato da ~3.850, c'è il rischio concreto che decisioni di regola (calcoli danno, edge case) vengano
  duplicate/decise nel layer di presentazione, dove possono divergere dal resolver.
- **Cosa fare:** verificare che ogni esito di regola sia calcolato in `GameCore` e che `Combat.cs` faccia
  solo presentazione; spostare in GameCore ciò che non lo è.

---

## Gioco — progressione (annotato 2026-07-20)

### La progressione di campagna/run non è persistita — SERVE save/resume (confermato)
- **Priorità:** ALTA — requisito confermato dall'utente (2026-07-20): la progressione deve essere salvata.
- **Contesto:** le uniche chiavi PlayerPrefs sono audio/nickname/guest; **nessuna** chiave di progressione,
  nessun `JsonUtility`/`persistentDataPath` per `RunProgressState`/`CampaignDeckState`. Oggi la run vive
  **solo in memoria**: su **PWA** (chiudibile in qualsiasi momento, anche per swipe) chiudere l'app a metà
  run perde tutto.
- **Cosa fare:** implementare **save/resume** della run:
  1. Serializzare `RunProgressState` + `CampaignDeckState` + stanza corrente in uno stato salvabile.
  2. Persistere: locale (file JSON in `Application.persistentDataPath`, che su WebGL usa IndexedDB) e/o
     lato **account server** (già esistente per il PvP), per sopravvivere anche al cambio device.
  3. Salvare a ogni avanzamento significativo (fine stanza, acquisto, level-up) e al `OnApplicationPause`/
     `focus lost` (importante su PWA/mobile).
  4. All'avvio: se esiste una run salvata, offrire "Riprendi" invece di ricominciare.
- **Dipendenza:** intreccia il god object (`BattleBoardController.Campaign*`); conviene estrarre prima la
  logica di progressione in un servizio testabile e poi aggiungerci la persistenza.

### Nessun test sul flusso di progressione end-to-end
- **Priorità:** media
- **Contesto:** `CampaignDeckState` ha unit test, ma l'avanzamento reale (stanze, ricompense, level-up,
  sblocco carte) è guidato da `BattleBoardController.Campaign`/`.CampaignProgress` (~1.480 righe), non
  coperti. Non esistono test PlayMode.
- **Cosa fare:** estrarre la logica di progressione dal controller (vedi god object) e coprirla con test.

---

## Gioco — UX (annotato 2026-07-20)

### Messaggistica di stato/errore incoerente e non localizzata
- **Priorità:** media
- **Contesto:** oltre al caso auth (già annotato), in generale gli stati vengono mostrati con stringhe
  hardcoded in italiano sparse nel codice; nessun sistema di localizzazione centralizzato.
- **Cosa fare:** introdurre una tabella di stringhe/localizzazione unica e messaggi utente coerenti.

### UI costruita in codice invece che con prefab/tema
- **Priorità:** media
- **Contesto:** diverse schermate/elementi sono generati proceduralmente (`LoginScreenPrototype`,
  `BattleUiFactory`, viste in `BattleBoardController.SetupViews`), anziché prefab basati su `MmoUiTheme`.
  Rende difficili iterazione visiva, responsive e localizzazione.
- **Cosa fare:** consolidare su prefab + tema condiviso; ridurre la UI generata a runtime.

### Onboarding/tutorial e contenuti ancora aperti
- **Priorità:** media (già in lavorazione)
- **Contesto:** hint contestuali "prima volta" gestiti con PlayerPrefs sparsi in `BattleBoardController.Hints`
  (~970 righe); onboarding/framing narrativo campagna ancora in evoluzione. Restano contenuti rotti/mancanti
  noti (artwork carte golem/kraken, icone miniboss in arrivo).
- **Cosa fare:** completare tutorial e sostituire gli asset rotti/placeholder; centralizzare la gestione
  degli hint "prima volta".
