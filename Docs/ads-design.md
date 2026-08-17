# Pubblicita': rewarded e interstitial

## Obiettivo

Collegare il gioco a una rete pubblicitaria vera senza che il gameplay ne sappia niente,
e senza che una ricompensa dipenda dalla parola del client.

Principio guida: **la pubblicita' e' un'offerta, mai un pedaggio**. Un premio che il
giocatore si e' gia' guadagnato deve arrivargli anche se la nostra pila pubblicitaria non
funziona; la pubblicita' puo' solo aggiungere qualcosa in piu' (il x3) o precedere un
incasso che avviene comunque.

## Le due piattaforme

Non esiste un SDK che copra entrambe le build:

| | WebGL / PWA (accardndie.com) | Android |
|---|---|---|
| AdMob / LevelPlay / MAX | non funzionano | si' |
| Strada praticabile | Google H5 Games Ads (AdSense/Ad Manager) o SDK di un portale (CrazyGames, Poki) | AdMob |

Android e' il canale primario per la pubblicita'. Il web e' collegato allo stesso layer da
`H5GamesAdProvider` (Fase 3), con tre limiti che restano: niente SSV, niente caricamento per
placement, e un blocco pubblicita' che spegne del tutto la rete per quel giocatore.

## Architettura

Tre pezzi, in `Assets/_Project/Scripts/Ads/` (assembly `AccardND.Ads`):

- `AdPlacement` — i momenti di gioco in cui puo' partire un annuncio. Il gameplay conosce
  solo questi. Il formato (interstitial o rewarded) e' una proprieta' del placement, non
  una scelta del punto di chiamata.
- `IAdProvider` — l'adattatore verso una rete. E' l'unico file che conoscera' un SDK.
  Oggi esistono `NoAdsProvider` (non ha mai niente: e' quello delle build vere finche'
  AdMob non c'e') e `FakeAdProvider` (pannello di prova con conto alla rovescia, attivo
  in editor e nelle build di sviluppo, o forzato col define `ACCARDND_FAKE_ADS`).
- `AdService` — la facciata statica che usa il gioco. Ci vivono le tre cose che non devono
  essere rifatte a ogni chiamata: le regole di frequenza, la pausa del gioco mentre
  l'annuncio e' a schermo, e la garanzia che non partano due annunci insieme.

Nessuna chiamata di `AdService` lancia eccezioni verso il gameplay: se la rete cade, si
gioca senza pubblicita'.

### I tre modi di chiedere un annuncio

| Chiamata | Quando | Se la pubblicita' non c'e' |
|---|---|---|
| `ShowInterstitial(p)` | fuoco e dimentica, parte un frame dopo | niente, si tira dritto |
| `ShowAsync(p)` | l'annuncio paga un extra (il x3) | niente extra |
| `ShowAsync(p, asGate: true)` | l'annuncio e' la condizione per riscuotere | **niente riscossione** |

**Si paga solo su `Watched`, sempre.** Non esiste piu' un cancello che si apre a vuoto: se
la rete non ha annunci, la riscossione viene rifiutata e la ricompensa resta li' da prendere
piu' tardi. `Unavailable` non apre niente, serve solo a scegliere le parole del rifiuto — "la
rete non ha annunci, riprova" e "l'hai chiusa a meta'" sono due messaggi diversi.

`asGate` cambia due cose rispetto a uno show normale: si **aspetta** il caricamento invece di
rinunciare subito (chi ha premuto sa che arriva una pubblicita'; dirgli di no mentre
l'annuncio sta arrivando gli costerebbe la ricompensa), e **non si applicano le regole di
frequenza** di `AdPolicy`, che difendono il giocatore dagli annunci che non ha chiesto — un
tetto raggiunto altrove gli chiuderebbe l'unica fonte di miele del gioco. Gli annunci mostrati
da un cancello continuano pero' a contare per quel tetto.

> **Scelta consapevole, non debito.** `TavernQuestClaim` e' un cancello fatto con un
> interstitial, e resta cosi' per decisione presa il 2026-08-05. Le due conseguenze che si
> accettano: usare un interstitial come condizione per una ricompensa e' contro le policy
> AdMob e AdSense (il formato previsto e' il rewarded), e senza SSV il server non puo'
> verificare niente, quindi "l'ho guardata" e' parola del client su una progressione che per
> tutto il resto e' autoritativa lato server. Il seguito sta in Fase 3, sotto.

## I placement di oggi

| Placement | Formato | Dove | Ricompensa |
|---|---|---|---|
| `TavernQuestClaim` | interstitial | riscossione di una quest giornaliera | cancello: niente annuncio, niente miele |
| `TavernBonusClaim` | rewarded | premio di giornata (50 vasetti) | cancello: niente annuncio, niente premio |
| `CampaignExperienceTriple` | rewarded | fine run di campagna | EXP account × 3 |
| `PvpExperienceTriple` | rewarded | fine partita PvP ranked | EXP account × 3 |
| `BagItemUsed` | interstitial | uso di un consumabile della bisaccia | — |

### Il triplicatore di campagna ha una seconda occasione

A fine run il x3 puo' saltare per motivi che non c'entrano col giocatore: la rete cade proprio
mentre il popup e' a schermo (niente claim id dal server, niente da far verificare), oppure
l'annuncio non arriva in tempo. Prima quell'occasione moriva con la run.

Adesso no: la reward resta sul server a `multiplier = 1`, e
`SinglePlayerProgressService.GetPendingAdRewards` la ripropone nella pagina **MESSAGGI** del
profilo, dove il TRIPLICA e' lo stesso di fine run — stesso placement, stesso claim, stessa
verifica lato server (`singleplayer.reward.pending_ads.get`).

- Vale **sette giorni** (`PendingAdRewardWindowHours`), al massimo 20 offerte in vetrina. La
  finestra vale solo per la lista: `ClaimAdMultiplier` non la ricontrolla, cosi' un video
  partito a cavallo della scadenza non finisce nel vuoto.
- Solo le reward di campagna (`reward_type = 'death'`). I claim delle partite classificate
  sono moltiplicabili allo stesso modo, ma passano da `PvpExperienceTriple`: mostrarli qui
  vorrebbe dire pagarli sull'ad unit sbagliata.
- Un badge rosso sul bottone PROFILO dell'hub dice che c'e' posta, altrimenti l'offerta
  dipenderebbe dal fatto che il giocatore apra il profilo per caso.
- Anche il popup di fine run e la riga di riepilogo lo dicono, quando il TRIPLICA non e'
  comparso: "Il triplicatore ti aspetta fra i messaggi del profilo".

Effetto collaterale voluto: pure il giocatore che a fine run preme CONTINUA ritrova l'offerta
nel profilo. Non e' un buco, e' la stessa promessa fatta due volte — l'occasione non si perde
per distrazione, e la pubblicita' la si guarda quando si vuole.

Nota su `BagItemUsed`: l'aggancio e' in `RecordConsumedBagItem`, cioe' dopo che l'oggetto
e' stato davvero tolto dalla borsa. Cosi' un uso rifiutato (sigillo senza bersaglio,
potenziamento in stanza boss) non produce pubblicita'. Resta il fatto che questo e' l'unico
placement che puo' cadere **dentro** una battaglia: se dovesse dare fastidio, la strada e'
spostarlo a fine stanza, non toglierlo.

## Regole di frequenza

In `AdPolicy`, tutte e sole per gli interstitial (i rewarded li chiede il giocatore, un
tetto glieli toglierebbe):

- **30 secondi** minimi fra due interstitial — piu' riscossioni di fila fanno una
  pubblicita', non una per tocco;
- **30 secondi** di esenzione a inizio sessione;
- **40 interstitial** per sessione al massimo.

> Questi valori sono **da collaudo**, scelti il 2026-08-02 per poter vedere la pubblicita'
> funzionare sul dispositivo. Per la produzione il punto di partenza ragionevole e' 120 di
> esenzione, 90 di distanza e un tetto di 8: sono numeri da tarare guardando retention e
> ricavi insieme, perche' le reti pagano le impression viste e un giocatore che disinstalla
> al secondo giorno non ne genera nessuna.

## Lato server

Il moltiplicatore x3 era gia' autoritativo prima di questo lavoro
(`SinglePlayerProgressService.ClaimAdMultiplier`): il client chiede, il server legge la base
dal claim registrato a fine run/partita e accredita `base × (3-1)`. E' idempotente due
volte, sul `rewardClaimId` gia' moltiplicato e sull'`adImpressionId` gia' usato.

Quello che e' cambiato: l'`adImpressionId` non e' piu' un GUID inventato dal client subito
prima della chiamata, e' l'identificativo dell'impression restituito dal provider dopo che
il video e' finito.

Gli altri tre placement non toccano il server: sono interstitial senza premio, o un cancello
davanti a una riscossione che ha gia' le sue regole (`TavernQuests.ClaimBonus`).

## Le unita' AdMob (Android)

App `AcCardNDie`, id `ca-app-pub-3580486749764055~5129791296`. Una unita' per placement, cosi'
il report dice quale placement rende e quale costa solo fastidio. La mappa vive in
`AdUnits.cs`: e' l'unico file da toccare quando se ne crea una nuova.

| Placement | Formato AdMob | Nome nella console |
|---|---|---|
| `TavernQuestClaim` | Interstitial | `android_interstitial_tavern_quest_claim` |
| `BagItemUsed` | Interstitial | `android_interstitial_bag_item_used` |
| `TavernBonusClaim` | Con premio | `android_rewarded_tavern_bonus_claim` |
| `CampaignExperienceTriple` | Con premio | `android_rewarded_campaign_experience_triple` |
| `PvpExperienceTriple` | Con premio | `android_rewarded_pvp_experience_triple` |

Formato "Con premio" e non "Interstitial con premio": i tre rewarded partono tutti da un
bottone che dice cosa si ottiene, mentre il rewarded interstitial e' pensato per annunci che
partono da soli in una transizione.

Il premio dichiarato su AdMob (importo 1, nome `daily_bonus` / `triple_experience`) e'
soltanto un'etichetta che tornera' indietro nella callback SSV. Il premio vero lo calcola il
server dalla base registrata nel claim: un valore dichiarato dalla rete non e' una cosa da
prendere per buona.

`AdUnits.For` restituisce le unita' **vere** anche nelle build di sviluppo: e' una scelta di
chi tiene l'account, presa per poter giocare la build vera e vedere che gli annunci arrivino.
Le unita' di prova pubbliche di Google si ottengono col define `ACCARDND_TEST_ADS`. Questo
vuol dire che collaudare una build sul proprio telefono genera impression vere, cioe' traffico
non valido: `AdUnits.TestDeviceIds` va riempito con l'id del proprio dispositivo, che si legge
in logcat al primo avvio. AdMob non distingue il collaudo dalla frode.

## Quando parte una richiesta: `Warm` / `Cool`

Le richieste alla rete non partono all'avvio del gioco, ma dal posto che sta per averne
bisogno: `AdService.Warm(placement)` all'ingresso, `AdService.Cool(placement)` all'uscita.

| placement | `Warm` | `Cool` |
|---|---|---|
| `TavernQuestClaim`, `TavernBonusClaim` | apertura della taverna | uscita dalla taverna |
| `CampaignExperienceTriple`, `BagItemUsed` | inizio run (bisaccia composta, o run ripresa da salvataggio) | fine run (`ReturnToStart`) |
| `CampaignExperienceTriple` (offerte recuperate) | apertura del profilo | uscita dal profilo |
| `PvpExperienceTriple` | `MatchStart` | — (non si ripete, non si ricarica mai) |

Il motivo e' un numero: nella prima giornata di AdMob (2026-08-02) le richieste sono state 36
e le impression 3, cioe' uno show rate dell'8%. Non era poco traffico, era il precaricamento
di tutti e cinque i placement a ogni avvio dell'app — richieste per posti dove il giocatore
non metteva piede. Le reti misurano quante richieste diventano impression e strozzano chi
chiede molto e mostra poco; in piu' un annuncio scade dopo circa un'ora, quindi quello
caricato all'avvio spesso era gia' da buttare quando serviva.

Due regole tengono il rapporto vicino a 1:1 anche dopo lo show:

- si ricarica solo dove il posto e' ancora aperto (`armed`) **e** puo' produrre piu' di un
  annuncio (`AdPlacements.MayRepeat`: le quest della taverna e gli oggetti della bisaccia).
  Il triplicatore di fine run no: ricaricarlo appena mostrato e' la richiesta piu' sprecata
  di tutte, perche' la run e' finita proprio in quel momento;
- un `Warm` su un annuncio gia' in cassa e ancora fresco non chiede niente.

Se un `Warm` manca, l'annuncio non si vede: `AdService.ShowAsync` non aspetta il caricamento
(terrebbe fermo il giocatore su un bottone gia' premuto), risponde `NoFill` e fa partire un
`Warm` per la volta dopo. Un placement che non compare mai si diagnostica cosi': nel diario
di partita deve esserci la riga `<placement>: annuncio in preparazione`.

## Fase 2 — AdMob su Android

Fatto il 2026-08-02:

1. Ad unit create nella console: vedi la tabella qui sopra.
2. Plugin **Google Mobile Ads v11.3.0** importato da `.unitypackage` (EDM4U aggiornato sul
   posto da 1.2.182 a 1.2.188, nessuna copia doppia). App ID scritto in
   `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`. Android Resolver
   eseguito: `play-services-ads:25.4.0` e `user-messaging-platform:4.0.0` iniettati in
   `mainTemplate.gradle`.
3. `AdMobProvider : IAdProvider` scritto, e ramo Android in `AdService.CreateDefaultProvider`.
   Mostra un annuncio per istanza e ne ricarica un altro alla chiusura, con scadenza su ogni
   attesa (l'SDK puo' non richiamare mai il callback).
4. `AdService.PrepareAsync()` all'avvio: inizializza l'SDK e basta, nessun annuncio caricato.
   Le richieste partono da `AdService.Warm(placement)` (vedi sotto).
5. `AdRewardContext` viaggia dai due placement x3 fino a `SetServerSideVerificationOptions`:
   il `rewardClaimId` finisce in `custom_data` e tornera' nella callback SSV.

Restano:

6. **SSV (Server-Side Verification)**: endpoint `/ads/admob/ssv` sul server .NET che verifica
   la firma con la chiave pubblica Google, legge `custom_data` (il `rewardClaimId`) e accredita
   il moltiplicatore. Poi l'URL va incollato nelle impostazioni delle tre unita' rewarded nella
   console. Serve una colonna in piu' su `single_player_reward_claims` per distinguere le
   impression verificate da quelle solo dichiarate dal client.
7. ~~UMP~~ **fatto**: `AdConsent` chiede il consenso e mostra il modulo prima di
   `MobileAds.Initialize`; se `CanRequestAds()` e' falso il provider non si inizializza
   nemmeno, perche' chiedere annunci in quel caso e' fiato sprecato. Il bottone PRIVACY
   nelle opzioni riapre il modulo e compare solo dove UMP dichiara che serve.

   **Resta da fare nella console AdMob**: creare il messaggio di consenso sotto
   *Privacy e messaggistica → Consenso UE*, pubblicarlo e includere l'app. Senza il
   messaggio pubblicato, UMP risponde che non c'e' nessun modulo da mostrare e per il
   traffico europeo non arriva pubblicita': e' la prima cosa da verificare se gli annunci
   non compaiono.
8. **Test device**: l'id si legge in logcat al primo avvio e va messo in `AdUnits.TestDeviceIds`.
   Finche' e' vuoto, collaudare una build di release sul proprio telefono genera traffico non
   valido. Cliccare i propri annunci significa chiusura dell'account.
9. `app-ads.txt` **scritto**: `Docs/web/app-ads.txt`, con la stessa riga di `ads.txt`
   (`google.com, pub-3580486749764055, DIRECT, f08c47fec0942fa0`), ed e' nella lista di
   deploy in `Docs/webgl-hosting-cache.md`. Resta da fare il deploy del sito e poi, su Play
   Console, controllare che "Sito web dello sviluppatore" dica esattamente lo stesso dominio
   su cui il file e' servito: AdMob confronta le stringhe, quindi `www.` di troppo o mancante
   fa fallire la scansione. Dopo il deploy servono almeno 24 ore prima che AdMob lo veda, e
   lo stato si legge in AdMob sotto *App -> app-ads.txt*.

   Restano il questionario annunci e la classificazione dei contenuti.

## Fase 3 — Web e PWA (H5 Games Ads)

La rete e' **Google H5 Games Ads**, servita dallo script di AdSense: e' l'unica che entra in
una build WebGL, perche' AdMob sul web non esiste. Rewarded e interstitial passano entrambi
dalla stessa chiamata, `adBreak`.

Scritto:

1. `Assets/Plugins/WebGL/AccardNdAds.jslib` — il bridge. Stesso schema di
   `AccardNdWebSocket.jslib`: il C# apre una richiesta e ne chiede lo stato a ogni frame,
   invece di farsi richiamare dal browser dentro il runtime Unity.
2. `H5GamesAdProvider : IAdProvider` — l'adattatore, e il ramo `UNITY_WEBGL` in
   `AdService.CreateDefaultProvider`. Il gameplay non cambia di una riga.
3. Nel `<head>` di `index.html` del template: `ACCARDND_ADSENSE_CLIENT_ID` (l'interruttore
   "questa build ha la pubblicita'") e **lo snippet AdSense scritto a mano**, statico.
4. `ads.txt` nel template e nella lista dei file di deploy in `Docs/webgl-hosting-cache.md`.

Il publisher id e' `pub-3580486749764055`, lo stesso numero che sta prima della tilde
nell'app id AdMob: iscrivendosi ad AdMob, Google crea l'account AdSense collegato, e i due
condividono l'id. In `index.html` va col prefisso (`ca-pub-...`), in `ads.txt` senza.

> **Lo snippet non va iniettato da JavaScript.** Il bridge inizialmente creava il tag da se';
> non funziona per due motivi. Il crawler che verifica la proprieta' del sito legge l'HTML e
> non aspetta che Unity si avvii, quindi un tag creato a runtime per lui non esiste; e due
> copie dello script sulla stessa pagina fanno fallire `adsbygoogle` con *"only one AdSense
> head tag supported per page"*. Il tag sta nel template, il bridge si limita a chiamare
> `adConfig`.

Da fare nella console, senza toccare il codice:

5. Sito `accardndie.com` aggiunto e proprieta' verificata. Vanno bene sia lo snippet nel
   `<head>` (gia' presente nel template) sia il metodo "Snippet ads.txt", perche' la riga in
   `ads.txt` e' gia' quella giusta: basta che il deploy sia stato fatto prima di premere
   *Verifica*.
6. **Messaggio di consenso UE** sotto *Privacy e messaggistica*, pubblicato. E' l'equivalente
   web di UMP, e come su AdMob: senza, al traffico europeo non arriva pubblicita'. La CMP la
   serve Google dallo script, il gioco non ha niente da chiamare — `AdConsent` resta una cosa
   di Android.
7. Approvazione del sito, e solo allora si toglie `data-adbreak-test="on"` dal tag in
   `index.html`. Fino a quel punto escono annunci di prova, che e' esattamente quello che
   serve per collaudare: valgono le stesse regole di AdMob, cliccare i propri annunci veri
   significa chiusura dell'account.

### Cosa il web non puo' fare come Android

| | AdMob | H5 Games Ads |
|---|---|---|
| caricamento per placement | `Warm` chiede l'annuncio | l'SDK precarica da solo, `Warm` non chiede niente |
| `IsReady` | vero stato della cassa | ottimista, con blocco di 60s dopo un buco |
| verifica lato server | SSV | non esiste: il x3 e' parola del client |
| id impressione | quello della rete | fabbricato dal client, non corrisponde ai report |
| consenso | UMP nel gioco | CMP servita da AdSense |

Il x3 sul web resta quindi protetto solo da idempotenza e cap lato server. E' un compromesso
accettabile finche' il web non e' il canale principale, e non c'e' modo di fare meglio: gli
H5 Games Ads non chiamano nessun endpoint a video finito.

### Il condono sul web (deciso il 2026-08-05)

Su Android un cancello pubblicitario si chiude solo quando la rete non ha annunci, cioe' di
rado. Sul web no: finche' AdSense non ha approvato il sito non arriva **nessun** annuncio, e
anche dopo un blocco pubblicita' fa sparire lo script in silenzio. In entrambi i casi il
miele delle quest — l'unica fonte del gioco — sarebbe irraggiungibile, non "riprova piu'
tardi".

Per questo sul web le ricompense dietro un annuncio si concedono comunque:

- `AdService.RewardsWaivedWithoutAds` e' vero solo su WebGL. E' **l'unico punto** che nomina
  la piattaforma: togliere il condono e' una riga sola.
- Un annuncio che non c'e' (`NoFill`, `Failed`, provider non inizializzato) diventa
  `AdOutcome.Waived` invece del rifiuto.
- I punti di chiamata chiedono `AdResult.Grants`, non piu' `AdResult.Watched`. Le due domande
  ora sono diverse: `Grants` decide se pagare, `Watched` conta le impressioni vere e resta
  quella che alimenta i tetti di `AdPolicy`.
- `AdService.IsReady` risponde di si' dove c'e' condono, altrimenti il bottone TRIPLICA non
  comparirebbe e non ci sarebbe niente da premere.
- L'identificativo mandato al server e' `waived-<guid>`. Il server rifiuta una richiesta senza
  identificativo, quindi qualcosa va mandato; marcarlo evita di sporcare gli unici dati che
  dicono quanta pubblicita' e' stata davvero guardata, e permette di contare i condoni.
- Dove il condono e' attivo, la UI non nomina la pubblicita': niente "guardando una
  pubblicita'" nel popup di fine campagna, niente "sto caricando la pubblicita'" in taverna.

Un annuncio **chiuso a meta'** resta un no anche col condono: li' una pubblicita' c'era
davvero ed e' stata rifiutata.

Quando togliere il condono e' una decisione, non una scadenza: dopo l'approvazione AdSense
si riprende il miele a chi ha un blocca-pubblicita', e quei giocatori si perdono.

### `TavernQuestClaim` resta un interstitial (deciso il 2026-08-05)

La conversione a rewarded e' stata proposta e **scartata**: il claim delle quest resta un
interstitial usato come cancello, su web come su Android. La decisione e' di chi tiene
l'account, e questo paragrafo esiste perche' non venga riaperta per distrazione.

Cosa si accetta con questa scelta, detto una volta e non piu': per AdSense e AdMob un premio
si paga dietro un rewarded, e dare qualcosa in cambio di un interstitial e' fuori policy.
Il rischio non e' che l'annuncio non parta - parte - ma che l'account prenda una limitazione
del servizio se la cosa viene notata. La conversione, se un giorno servisse, e' una riga in
`AdPlacements.FormatOf` (spostare il caso fra i rewarded) piu' una ad unit nuova in console
AdMob; sul web non servirebbe niente, perche' il formato lo decide la stessa tabella.

### I contenuti del sito, prima della revisione

L'approvazione AdSense richiede un sito con del contenuto, e un dominio che serve solo un
canvas Unity viene rifiutato spesso: e' il motivo di rifiuto piu' comune per i giochi
self-hosted. Per questo, prima di chiedere la revisione, `Docs/web/` e' diventato un piccolo
sito vero:

| pagina | cosa contiene |
|---|---|
| `guida.html` | il regolamento completo: mazzo e mano, triangolo delle fazioni, scala del Vigore, tutte le aure, e come si sceglie uno schieramento |
| `classi.html` | le nove classi, con abilita, aura da tre copie e una nota su come si giocano |
| `carte.html` | le 81 carte schierabili, **generate** dagli asset da `gen-carte.sh` |
| `privacy.html` | l'informativa, aggiornata con AdSense e i cookie pubblicitari sul web |

Tutte condividono `site.css` e la stessa navigazione, e sono raggiungibili dal footer di
`index.html`: senza quei collegamenti il crawler vedrebbe solo il canvas, perche' la pagina
di gioco per lui e' vuota. `sitemap.xml` e `robots.txt` completano il percorso.

`carte.html` si rigenera con `gen-carte.sh` e non si modifica a mano: e' l'unico modo perche'
la pagina non diverga dal gioco quando cambiano le carte.

Se la revisione va male lo stesso, restano gli SDK dei portali (CrazyGames, Poki,
GameDistribution), che pero' monetizzano il loro dominio, non il nostro.

## Economia: perche' la pubblicita' non paga miele

Il miele arriva solo dalle quest della taverna, ed e' una scelta di bilanciamento
(vedi `TavernQuests`). I rewarded qui sopra pagano EXP account o non pagano niente: aprire
un secondo rubinetto di miele con la pubblicita' svaluterebbe l'unico esistente e
renderebbe il costo degli sblocchi del Santuario una funzione di quanti video uno guarda.
Se un giorno servisse, la forma giusta e' un cap giornaliero rigido tarato come "una quest
in piu'", non un rubinetto aperto.
