# Roadmap di lancio — 26 settembre 2026

Obiettivo: **AcCard N' Die 1.0 su Google Play (produzione) + accardndie.com aggiornato, sabato 26 settembre 2026.**

Oggi è domenica 16 agosto. Restano **41 giorni**, cioè 6 settimane piene.

---

## 1. Il vincolo che comanda tutto

Non è il codice: è la coda di revisione di Google. Tutto il resto si pianifica **a ritroso** da queste date.

| Data | Cosa deve essere successo | Perché |
| --- | --- | --- |
| **entro il 26 ago** | i 14 giorni consecutivi di test chiuso devono essere **iniziati** (12 tester opted-in) | altrimenti non arrivi in tempo alla richiesta di produzione |
| **~9 set** | 14 giorni completati → premi **"Apply for production"** | la revisione della richiesta dichiara "fino a 7 giorni" |
| **~16 set** | accesso alla produzione **concesso** | senza questo non esiste nessun canale di produzione da riempire |
| **17 set (gio)** | **code freeze**: nessuna feature nuova, solo bug | serve una build stabile su cui girare 24h di prove |
| **18 set (ven)** | AAB della release caricato in produzione, **con Pubblicazione gestita attiva** | la revisione della release è altri 1–7 giorni per una prima pubblicazione |
| **~24 set** | release **approvata ma trattenuta** | la Pubblicazione gestita ti fa approvare la review senza pubblicare |
| **26 set (sab)** | premi il bottone. Web e Play insieme | pubblichi al minuto che vuoi, non quando decide Google |

> **Azione immediata, oggi.** Apri la Play Console e verifica tre cose del test chiuso in corso:
> data di inizio, **numero di tester attualmente opted-in** (devono restare ≥12 per tutti i 14 giorni: se uno esce, il conteggio ne risente) e che la build caricata lì sia quella che stai davvero testando. Se il conteggio è partito dopo il 26 agosto, il 26 settembre su Android non è raggiungibile e va spostata la data o il canale.

> **Attiva subito la Pubblicazione gestita** (Play Console → Pubblicazione). È la differenza tra "lancio il 26" e "lancio quando Google approva".

Il target API 36 (obbligatorio dal 31 agosto per le app nuove) è **già a posto**: il progetto è su `AndroidTargetSdkVersion: 36`. Nessun lavoro.

---

## 2. Le sei settimane

Ogni fase ha un **cancello**: se non è verde, non si passa alla settimana dopo, si taglia scope.

### Fase 0 — 17–23 agosto · Mettere in sicurezza il cantiere

Non si costruisce niente sopra un cantiere aperto. Questa settimana non produce feature e va fatta lo stesso.

- [ ] **Committare le 770 modifiche non committate.** L'ultimo commit è del 7 agosto: hai 9 giorni di lavoro che vivono solo sul disco. Spezzalo in commit tematici, non in un `asd`.
- [ ] **Riportare a verde i 18 test rossi** (`CombatResolverTests`, `PvpMatchEngineTests`, `CampaignDeckStateTests`, `RunProgressStateTests`). Sono attese vecchie rispetto alle regole nuove, non bug. A 6 settimane dal lancio una suite rossa significa che non hai nessuna rete: ogni fix delle prossime settimane sarà a occhio.
- [ ] **Verifica del test chiuso** in Play Console (sopra) + attivazione Pubblicazione gestita.
- [ ] **Manda il sito ad AdSense per l'approvazione.** È la voce con il tempo di attesa più lungo e meno controllabile di tutte (settimane), quindi parte per prima. Non blocca il lancio web — `RewardsWaivedWithoutAds` già condona le ricompense senza annuncio — ma se non parte ora non arriverà mai.
- [ ] **Backup automatico del DB SQLite sul VPS** (cron giornaliero + copia fuori dal VPS). La progressione è server-authoritative: quel file *è* il gioco dei tuoi giocatori.

**Cancello:** suite verde, working tree pulito, data di inizio del test chiuso confermata.

---

### Fase 1 — 24–30 agosto · Contenuto: i capitoli mancanti

La voce più rischiosa dello scope, quindi la prima ad essere attaccata.

- [ ] **Capitolo 4 (Seraphel) e capitolo 6 (Medusa) → giocabili.** `SeraphelBoss.cs` e `MedusaBoss.cs` esistono e hanno i test. Manca il collegamento: scenario, `BossId`, flag `Playable: true` in `ChapterCatalog.cs` **e nella gemella client** `AdventureChapterCatalog`. Per Medusa va deciso lo scenario Specchi (oggi gira su quello di default). È il rapporto sforzo/risultato migliore di tutta la roadmap: due capitoli al prezzo del cablaggio.
- [ ] **Capitolo 3 — boss Jurinashor da scrivere.** Non esiste nessuna classe. Modellalo su `BragusBoss` e scrivi i test insieme al boss, come per gli altri quattro.
- [ ] **Decidere il capitolo 5**, che oggi non ha nome, scenario né boss. Se entro il **5 settembre** non è giocabile, si applica il piano B (sotto) e si passa oltre senza rimorsi.
- [ ] **Cosmetico dadi del capitolo 7.** Il capitolo 7 è già giocabile e promette un premio che non ha nessun sistema dietro: chi finisce la campagna oggi riceve niente. O lo implementi, o cambi il premio in qualcosa che esiste.

**Piano B sul capitolo 5 (già coperto dal codice):** lascialo `Playable: false`. La regola a cascata in `ChapterCatalog` concede già i capitoli non giocabili fino al primo giocabile, quindi la progressione non si ferma e nessuno resta bloccato. **Non rinumerare gli id**: `ChapterRemapMigration` è protetta da una chiave in `server_settings` e una seconda rinumerazione richiede una chiave nuova — è esattamente il genere di lavoro che non vuoi a tre settimane dal lancio.

**Cancello (30 ago):** capitoli 3, 4, 6 giocabili e testati. Sul 5 hai una decisione presa, non un forse.

---

### Fase 2 — 31 agosto – 6 settembre · Soldi: la settimana su dispositivo vero

Tutto il codice di IAP e ads è scritto. Quello che manca è **configurazione esterna e prova sul telefono**, ed è il tipo di lavoro che non si può accelerare a fine mese.

- [ ] **IAP su Play Console**: creare i 4 prodotti (`no_ads` 2,99 · `all_classes` 9,99 · `all_classes_supreme` 14,99 · `supreme_upgrade` 4,99), aggiungere i license tester, mettere `ACCARDND_PLAY_LICENSE_KEY` sul VPS.
- [ ] **Acquisto reale su dispositivo, tutti e 4 i prodotti.** Verifica che l'ordine si confermi a Google **solo dopo** la concessione del server, e che gli sblocchi si riapplichino a un `iap.get` successivo (reinstallazione, secondo device).
- [ ] **Prova del ripristino acquisti** su un'installazione pulita. È il bug che genera più recensioni a una stella nella storia dei giochi mobile.
- [ ] **AdMob**: SSV lato server, `AdUnits.TestDeviceIds` con l'id del tuo dispositivo, flusso di consenso UMP provato su un device UE.
- [ ] **Le 5 posizioni pubblicitarie provate una per una** su Android: quest, oggetto bisaccia, premio di giornata, x3 EXP campagna, x3 EXP PvP.
- [ ] Verifica che `no_ads` condoni davvero i cancelli pubblicitari (miele quest, EXP tripla), non solo gli interstitial.

**Cancello (6 set):** hai comprato con soldi veri dal tuo telefono e hai visto lo sblocco arrivare. Finché non succede, la monetizzazione è teorica.

---

### Fase 3 — 7–13 settembre · PvP, resistenza e candidata alla release

- [ ] **Richiedi l'accesso alla produzione** appena scadono i 14 giorni (~9 set). Non aspettare di essere pronto col codice: le due code sono indipendenti e questa è più lenta.
- [ ] **Riconnessione al match PvP dopo una caduta.** Oggi una disconnessione è `match.opponent_left` e stanza chiusa. Su mobile la rete cade di continuo: è il difetto più visibile del PvP.
- [ ] **UI di selezione del round decisivo** nel battlefield. Oggi al round 3 il server auto-sceglie allo scadere del timer: il giocatore subisce il momento più importante della partita senza toccarlo.
- [ ] **Sessione di gioco reale in PvP con più persone insieme**, non due tuoi client. Serve per vedere i tempi di coda e il comportamento del matchmaking con utenti veri.
- [ ] **Prova di carico sul VPS.** 2 vCore / 2 GB reggono un lancio? Misuralo adesso, non il 26. Strumento e piano pronti: `Server/AccardND.LoadTest` e [prova-di-carico.md](prova-di-carico.md).
- [ ] **Piano di rollback**: come torni indietro se la release del 26 rompe la progressione. Il binario precedente va tenuto sul VPS, pronto.

**Cancello (13 set):** build candidata alla release, funzionante end-to-end sul telefono. Da qui in avanti si aggiungono solo correzioni.

---

### Fase 4 — 14–20 settembre · Rifinitura, negozio, freeze

Nessuna feature nuova. Questa settimana è dedicata a tutto ciò che il giocatore vede nei primi 5 minuti.

- [ ] **Screenshot del negozio**: ne servono almeno 4 (3 a 1080p+ per essere promuovibile), più tablet 7" e 10" (riusa i 16:9). Ne hai già 4 in `StoreAssets/GooglePlay/` da rivedere, e la scaletta consigliata è in `Marketing/play-store-listing.md`.
- [ ] **Allineare la descrizione del negozio alla build vera.** Google verifica che corrispondano. Se il capitolo 5 non entra, la descrizione non deve promettere una campagna completa; il blocco "SFIDE ONLINE" e le tecniche del Santuario vanno riletti riga per riga.
- [ ] **Passata sulle traduzioni** en/es/fr/de. Le 5 lingue esistono come tabelle, ma il tutorial nuovo non è tradotto e i messaggi d'errore dell'autenticazione escono grezzi e in inglese tecnico ("id provider not found", "credential_returned"): mappali in messaggi amichevoli e localizzati.
- [ ] **Prima partita da zero su un telefono pulito**, con un account mai visto, in ognuna delle 5 lingue. Cronometra quanto passa prima che il giocatore capisca cosa deve fare.
- [ ] **Sostituire gli asset rotti noti**: artwork delle carte golem e kraken (GUID sprite rotta), 17 icone dei nodi talento mancanti.
- [ ] **Contenuti obbligatori Play**: informativa privacy (già online), sezione Sicurezza dei dati compilata, questionario dei contenuti, dichiarazione sugli annunci, cancellazione account (già implementata — va **dichiarata** nella console).
- [ ] **Giovedì 17: code freeze.** **Venerdì 18: carica l'AAB di produzione** e lascialo trattenuto dalla Pubblicazione gestita.

**Cancello (20 set):** release in revisione, negozio compilato al 100%.

---

### Fase 5 — 21–26 settembre · Lancio

- [ ] **Solo bug bloccanti.** Un bug non bloccante scoperto il 24 si corregge il 28: una patch è economica, una release rifiutata a 48 ore dal lancio no.
- [ ] Ultimo backup del DB e riavvio pulito del servizio prima del giorno X.
- [ ] **Pubblica con rollout progressivo, non al 100%.** Parti dal 20%: se la progressione lato server ha un problema sotto carico, lo vedi su un quinto dei giocatori invece che su tutti. Sali al 50% e poi al 100% nei giorni seguenti.
- [ ] Web: carica la build WebGL lo stesso giorno (zip + scp + unzip), poi **verifica il `Last-Modified`** e che il service worker non stia servendo la versione vecchia dalla cache.
- [ ] **Resta davanti al monitor le prime ore.** Il primo giorno di traffico vero trova cose che 6 settimane di prove non hanno trovato.

---

## 3. Rischi, in ordine di quanto possono farti male

| # | Rischio | Come lo riconosci in tempo | Piano B |
| --- | --- | --- | --- |
| 1 | Il test chiuso non chiude in tempo (tester che escono, conteggio ripartito) | controllo del conteggio opted-in **due volte a settimana**, non a fine mese | lanci il **web il 26** come previsto e Android appena arriva l'accesso; la data resta, cambia il canale |
| 2 | Capitolo 5 non pronto | il 5 settembre non è giocabile | resta `Playable: false`, la cascata copre la progressione, la descrizione del negozio non lo promette |
| 3 | Il VPS cade il giorno del lancio | prova di carico in fase 3 | rollout al 20%, backup fuori dal VPS, binario precedente pronto sul disco |
| 4 | Release rifiutata dalla revisione | carichi il **18**, non il 25: hai una settimana di margine per un secondo tentativo | ripubblichi corretto entro il 26 |
| 5 | AdSense non approva in tempo | dipende da loro, non da te | non blocca niente: il condono web è già nel codice |
| 6 | Un bug di regressione scoperto a freeze fatto | la suite verde di fase 0 è ciò che te lo dice | patch post-lancio, non slittamento |

---

## 4. Cosa NON fare tra oggi e il 26 settembre

Il debito tecnico più grosso del progetto è noto e va **lasciato stare**:

- **non spezzare `BattleBoardController`** (19.200 righe, 23 partial, 0 test). È il refactor giusto e il momento peggiore possibile: nessun test lo protegge.
- **non migrare a Unity Player Accounts.** "Sign in with Google" è deprecato ma funziona; una migrazione dell'autenticazione a 6 settimane dal lancio è come cambiare la serratura la sera della festa.
- **non rinumerare gli id dei capitoli.**
- **niente feature che non è già in questo documento.** Ogni idea nuova va in un file "post-lancio" e ci resta.

Il 26 settembre non ha bisogno del gioco migliore che puoi scrivere. Ha bisogno di un gioco che si compra, si scarica, si gioca e non perde i progressi.
