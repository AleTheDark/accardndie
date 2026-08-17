# Analisi di mercato e piano ricavi — AcCard N' Die

Scritto il 16 agosto 2026, a 41 giorni dal lancio ([roadmap-lancio-26-settembre.md](roadmap-lancio-26-settembre.md)).

Questo documento fa tre cose: dice **in che mercato entri**, quanti **utenti attivi** puoi
realisticamente portare e con quali canali, e quanto puoi **incassare dalla pubblicità** e
dagli acquisti. Tutti i numeri sono stime da benchmark pubblici 2026 più il comportamento
che il tuo design impone: vanno **sostituiti con i dati veri** del pannello admin nelle
prime quattro settimane dopo il lancio. Le fonti sono in fondo.

> **La conclusione, in una riga.** Con la pubblicità non si campa a questi volumi: a 100
> utenti attivi al giorno fai ~€70 al mese di ads, sotto la soglia di pagamento di Google.
> I soldi seri del primo anno stanno in due posti che non sono AdMob: i **portali web**
> (hai già la build WebGL) e i **4 prodotti IAP**. La pubblicità diventa interessante
> sopra i 1.000 DAU, e prima di allora serve soprattutto a non costare niente.

---

## 1. Il mercato in cui entri

### 1.1 Genere e concorrenza reale

AcCard N' Die è un **card battler tattico a dadi**: tre carte in campo, nove classi, aure,
scala del Vigore, campagna a capitoli e PvP in tempo reale. Su Play finisci nella categoria
*Giochi di carte*, che è la cosa peggiore che potesse capitarti dal punto di vista della
scoperta, perché quella categoria è occupata da:

| Fascia | Chi c'è | Cosa vuol dire per te |
| --- | --- | --- |
| Solitari e briscole | Solitaire Grand Harvest, Scopa/Briscola italiane | volume enorme, pubblico anziano, zero sovrapposizione col tuo gioco ma ti rubano tutte le query generiche ("gioco di carte") |
| CCG competitivi | Hearthstone, Marvel Snap, Legends of Runeterra | budget UA a sette cifre, non li tocchi |
| **Roguelike a carte/dadi** | Slay the Spire, Balatro, Dicey Dungeons, Luck be a Landlord | **è qui che stai**, ed è l'unica fascia dove un indie può ancora emergere |

Il pubblico di riferimento non è "chi gioca a carte", è **chi ha giocato Balatro o Slay the
Spire su telefono e cerca la prossima cosa**. È un pubblico piccolo ma con due proprietà
rare: guarda i video di gameplay fino in fondo, e paga volentieri uno sblocco una tantum
invece di subire un gacha. È esattamente il pubblico giusto per il tuo catalogo IAP.

### 1.2 Cosa hai di distintivo (e cosa no)

**Punti forti reali, in ordine di quanto valgono in fase di acquisizione:**

1. **I dadi 3D lanciati sul tavolo.** È il tuo unico asset di marketing che funziona senza
   spiegazioni: un dado che rimbalza e decide uno scontro è un video da 8 secondi che
   funziona su TikTok/Shorts/Reels senza voce fuori campo. Nessuno dei concorrenti indie ha
   dadi fisici in 3D. **Costruisci i contenuti attorno a questo, non attorno alle regole.**
2. **PvP in tempo reale.** Rarissimo in un indie card game. È l'unica cosa che ti fa
   scrivere "sfida altri giocatori" nella scheda, e vale una riga nella descrizione breve.
3. **Il triangolo Might/Cunning/Magic + aure.** Profondità dimostrabile in uno screenshot.
4. **Italiano nativo + 5 lingue.** In Italia la concorrenza ASO su termini di nicchia è
   bassissima: è la tua testa di ponte.
5. **Monetizzazione onesta** (sblocchi una tantum, niente gacha, niente energia). Nel 2026
   è un argomento di vendita, non un dettaglio: mettilo nella scheda.

**Punti deboli, detti senza giri di parole:**

1. **Il nome non è cercabile.** "AcCard N' Die" è un gioco di parole che non sopravvive a
   una ricerca: nessuno lo digiterà mai correttamente, e il campo nome di Play (30 caratteri)
   te ne lascia 17 inutilizzati. **Questa è la modifica ASO col miglior rapporto sforzo/resa
   di tutto il documento:** metti `AcCard N' Die: Carte e Dadi` (26/30) in italiano e
   `AcCard N' Die: Dice Cards` in inglese. Il nome dell'app pesa più di qualsiasi altro
   campo nell'algoritmo di Play.
2. **Zero comunità preesistente.** Nessun Discord, nessun follower, nessuna wishlist. Il
   giorno del lancio parti da zero assoluto: non esiste "il pubblico che aspetta".
3. **Nessun dato di retention.** Non sai se il tuo D1 è 15% o 35%, e questa differenza
   decide se il gioco è un business o un hobby. Il test chiuso in corso è l'unico posto
   dove puoi misurarlo prima del 26.
4. **Curva di apprendimento.** Aure, fazioni, scala del Vigore, scenari: è tanta roba nei
   primi 5 minuti, e il genere strategia ha già il D1 più basso della media (~25%). Il
   [tutorial progressivo](tutorial-progressivo-design.md) non è un extra, è il moltiplicatore
   di tutti i numeri qui sotto.

### 1.3 Geografia: dove ti conviene esistere

| Mercato | Volume | eCPM Android | Concorrenza ASO | Verdetto |
| --- | --- | --- | --- | --- |
| Italia | piccolo | medio-basso (~€3–5 rewarded) | **bassa** | testa di ponte: ci vinci l'ASO, ma non ci fai fatturato |
| DACH / UK / US | grande | alto (€6–12 rewarded) | altissima | dove stanno i soldi della pubblicità, dove non ti trova nessuno |
| ES / FR | medio | medio | media | gratis, hai già le lingue: tienile |
| LATAM / SEA | enorme | bassissimo (€0,3–1) | bassa | volume che non paga: buono per stress test, inutile per ricavi |

**Strategia geografica:** pubblica in tutti i paesi (non c'è motivo di non farlo), fai ASO
sul serio solo in italiano e inglese, e non stupirti se il 60% degli installi arriva da
mercati che valgono un decimo in eCPM. Il web, tramite i portali, è quello che ti porta
traffico anglofono senza pagarlo.

---

## 2. Quanti utenti attivi puoi realmente portare

### 2.1 Il modello (perché i numeri sono quelli che sono)

Due formule, e nient'altro:

```
DAU a regime  =  installi al giorno  ×  L
```

dove `L` = giorni attivi medi per install, cioè l'area sotto la curva di retention. Con i
benchmark 2026 per il tuo genere (D1 25%, D7 8%, D30 3%) viene **L ≈ 4,8 giorni**. Un
tutorial che porta il D1 a 32% e il D7 a 12% porta L a ~7,5: **+55% su ogni numero di
questo documento senza un euro di marketing.**

```
Ricavo mensile ads  =  DAU  ×  ARPDAU  ×  30
```

### 2.2 I canali, in ordine di quanto rendono per ora di lavoro

| # | Canale | Costo | Installi/mese realistici (a regime) | Note |
| --- | --- | --- | --- | --- |
| 1 | **Portali web** (CrazyGames, Poki, GameDistribution, itch.io) | 0 € | 20k–200k *partite web*, di cui 1–3% convertono in install Android → **200–3.000** | hai già la build WebGL: è l'unico canale che ti dà volume vero a costo zero |
| 2 | **Video brevi** (TikTok/Shorts/Reels sui dadi 3D) | 6–8 h/settimana | 100–1.500, con varianza brutale | 1 video su 30 fa numeri; devi pubblicarne 3-4 a settimana per mesi |
| 3 | **ASO Play** (nome, descrizione, screenshot, 5 lingue) | 2 giorni una tantum | 50–400 | l'unica cosa che cresce da sola nel tempo |
| 4 | **Comunità** (r/AndroidGaming, r/roguelikes, Discord di genere, forum italiani) | 3–4 h/settimana | 30–300 | funziona una volta per comunità: non è un rubinetto |
| 5 | **Stampa/creator indie italiani** | 5–10 mail | 50–500 in un colpo, poi zero | vale la pena solo attorno alla data di lancio |
| 6 | **UA a pagamento** | €0,50–1,00 per install | quanti ne compri | **da non fare**: vedi §4.3, sei 4–6× sotto il break-even |

### 2.3 Tre scenari a 12 mesi

**Scenario A — "Lancio silenzioso"** (pubblichi e torni a programmare; nessuna attività di
marketing continuativa). *Probabilità: alta se non pianifichi il contrario.*

| | Installi | DAU medio | Ads | IAP | Totale |
| --- | --- | --- | --- | --- | --- |
| Q4 2026 | 400 | 20 | €35 | €50 | €85 |
| Q1 2027 | 300 | 25 | €50 | €40 | €90 |
| Q2 2027 | 250 | 22 | €45 | €35 | €80 |
| Q3 2027 | 200 | 20 | €40 | €30 | €70 |
| **Anno 1** | **~1.150** | **~22** | **€170** | **€155** | **€325** |

Copri il VPS e poco altro. Nota pratica sgradevole: AdMob paga a **$100 di saldo maturato**,
quindi in questo scenario il primo bonifico arriva verso il **decimo mese**.

**Scenario B — "Marketing sostenibile"** (portali web attivi entro novembre, 3 video a
settimana, ASO curato in 5 lingue, presenza nelle comunità). Costo: **~8 ore a settimana**,
zero euro. *Probabilità: alta se ci metti le ore, ed è lo scenario su cui pianificare.*

| | Installi Android | Partite web | DAU app | Ads app | Portali | IAP | Totale |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q4 2026 | 2.000 | 60.000 | 90 | €190 | €120 | €270 | €580 |
| Q1 2027 | 4.000 | 150.000 | 200 | €420 | €300 | €540 | €1.260 |
| Q2 2027 | 5.000 | 250.000 | 300 | €630 | €500 | €680 | €1.810 |
| Q3 2027 | 5.500 | 300.000 | 360 | €750 | €600 | €750 | €2.100 |
| **Anno 1** | **~16.500** | **~760.000** | **~240** | **€1.990** | **€1.520** | **€2.240** | **~€5.750** |

**Scenario C — "Il colpo"** (un portale ti mette in home, o un video fa 2 milioni di
visualizzazioni). 80k–200k installi, 2.000–5.000 DAU, **€1.500–4.000 al mese**.
*Probabilità: sotto il 10%.* Non si pianifica, ci si prepara: il modo di prepararsi è avere
il server che regge ([prova-di-carico.md](prova-di-carico.md)) e il negozio già compilato,
perché un picco di traffico su un gioco che crasha non torna mai più.

### 2.4 Risposta secca: "quanti utenti nuovi al giorno?"

- **Primo mese senza marketing:** 3–12 installi al giorno. Non 100. Il lancio di un indie
  sconosciuto su Play, senza copertura stampa, fa numeri a due cifre.
- **Con i portali web attivi e i video:** 30–150 installi al giorno entro il quarto mese.
- **Il picco del giorno di lancio non è un dato.** Il numero che conta è quello del
  quindicesimo giorno, quando gli amici hanno finito di scaricarlo.

---

## 3. Piano ricavi da pubblicità

### 3.1 Quante impression genera davvero un giocatore

Questo il tuo design lo determina già, ed è una delle cose fatte meglio del progetto: il
miele arriva **solo** dalle quest della taverna, e ogni riscossione passa da un annuncio.
Dai placement in [ads-design.md](ads-design.md), con i tetti di produzione (120 s di
esenzione, 90 s di distanza, max 8 interstitial a sessione):

| Placement | Formato | Impression/giorno per utente attivo |
| --- | --- | --- |
| `TavernQuestClaim` | interstitial | 2–5 (riscuote 3-6 quest su 10) |
| `TavernBonusClaim` | rewarded | 0–1 (solo chi completa tutto) |
| `BagItemUsed` | interstitial | 0–2 |
| `CampaignExperienceTriple` | rewarded | 1–2 |
| `PvpExperienceTriple` | rewarded | 0–1 |
| **Totale** | | **3–8, base 5** |

5 impression/DAU è un numero **sano** per un gioco che non impone pubblicità obbligatorie:
molti casual stanno a 3–4. Il tuo problema non è la quantità di annunci, è il prezzo.

### 3.2 ARPDAU

| Ipotesi | Impression/DAU | eCPM medio | **ARPDAU** |
| --- | --- | --- | --- |
| Pessimistica (traffico tier-2/3, niente mediation, fill basso) | 3 | €3,00 | **€0,009** |
| **Base** (Italia + EU, mix 60% interstitial / 40% rewarded) | 5 | €4,50 | **€0,023** |
| Ottimistica (quote UK/US/DE, quest convertite a rewarded, mediation) | 8 | €6,50 | **€0,052** |

### 3.3 Ricavo pubblicitario mensile, per livello di DAU

| DAU | Pessimistico | **Base** | Ottimistico |
| --- | --- | --- | --- |
| 30 | €8 | **€21** | €47 |
| 100 | €27 | **€69** | €156 |
| 300 | €81 | **€207** | €468 |
| 1.000 | €270 | **€690** | €1.560 |
| 3.000 | €810 | **€2.070** | €4.680 |
| 10.000 | €2.700 | **€6.900** | €15.600 |

**Le tre soglie da tenere a mente:**

- **~25 DAU** → la pubblicità copre il VPS. È il tuo primo traguardo economico, ed è
  raggiungibile nel primo mese.
- **~150 DAU** → superi i $100 di soglia AdMob ogni mese, cioè inizi a essere pagato
  regolarmente invece che una volta ogni tanto.
- **~1.000 DAU** → conviene attivare la **mediation** (AdMob Mediation, LevelPlay o MAX):
  +20–40% di eCPM per mezza giornata di lavoro. Sotto quel volume non ti chiama nessuno e
  la configurazione non si ripaga.

### 3.4 Le sei leve, quantificate

| # | Leva | Effetto stimato | Quando |
| --- | --- | --- | --- |
| 1 | **Messaggio di consenso UE pubblicato** (UMP su AdMob + CMP su AdSense) | **da 0 a tutto**: senza, il traffico europeo — cioè quasi tutto il tuo — non riceve *nessun* annuncio | **bloccante, prima del lancio** |
| 2 | **`app-ads.txt` deployato e verificato** | +10–30% di domanda programmatica (senza, l'inventario è "non autorizzato" e i buyer premium lo saltano) | prima del lancio, serve 24-48 h perché AdMob lo veda |
| 3 | **`TavernQuestClaim` da interstitial a rewarded** | il rewarded paga ~2× l'interstitial e questo è il placement a volume più alto: **+30–45% di eCPM medio**, e toglie il rischio policy | prima patch post-lancio (§5) |
| 4 | **Mediation** | +20–40% eCPM | sopra i 1.000 DAU |
| 5 | **Portali web** invece dell'AdSense H5 sul tuo dominio | il tuo dominio senza traffico rende ~0; un portale ti dà pubblico *e* rev-share (CrazyGames: 60% ads, 70% acquisti) | novembre-dicembre |
| 6 | **Tutorial progressivo** (retention D1/D7) | non è una leva pubblicitaria ma **moltiplica tutto**: L da 4,8 a 7,5 giorni = +55% su ogni riga di queste tabelle | post-lancio, priorità massima |

### 3.5 Il web, senza illusioni

L'AdSense H5 sul tuo dominio, con il traffico che avrà accardndie.com, rende **cifre a una
cifra al mese**. Non è un fallimento del codice: è che il sito non ha pubblico. Il valore
di quel lavoro è un altro, ed è reale — ti tiene il gioco giocabile senza installazione, e
il condono (`RewardsWaivedWithoutAds`) fa sì che nessuno resti senza miele.

**Il web che rende è quello dei portali.** Con la build WebGL che hai già:

| Portale | Rev-share | Realistico per un gioco discreto ma non virale |
| --- | --- | --- |
| CrazyGames | 60% ads / 70% acquisti | 20k–150k partite/mese → **€40–300/mese** |
| Poki | ~50–70% | selezione più dura, volumi più alti se entri |
| GameDistribution | ~60% | volume facile, RPM basso |
| itch.io | 100% (niente ads) | zero soldi, ma pubblico di appassionati e feedback |

Tre cose da verificare **prima** di candidarti (sono i motivi tipici di rifiuto di una build
Unity WebGL): peso della build e tempo di caricamento (punta a sotto i 30-40 MB compressi e
sotto i 15 secondi al primo frame), nessun link esterno che porti fuori dal portale, e il
login — il tuo Google One Tap dentro un iframe di portale è la cosa che si rompe per prima,
quindi il gioco deve essere **pienamente giocabile da ospite** (già lo è, vedi il link
ospite headless della progressione server-authority).

---

## 4. Il quadro economico completo

### 4.1 Gli IAP valgono più della pubblicità, per utente

Con i 4 prodotti a catalogo ([iap-design.md](iap-design.md)) e una commissione Google del
**15%** (non 30: la tariffa ridotta vale sul primo milione di dollari all'anno):

| | Stima |
| --- | --- |
| Conversione a pagante (sblocchi una tantum, niente gacha) | 1,0–2,5% degli installi |
| Scontrino medio lordo | ~€8 |
| **Netto per pagante** | **~€6,80** |
| **LTV IAP per install** | **€0,07–0,17** |
| LTV ads per install (L 4,8 × ARPDAU €0,023) | **€0,11** |
| **LTV totale per install** | **€0,18–0,28** |

Due conseguenze concrete:

- **`no_ads` a €2,99 è prezzato bene.** Netto €2,54, cioè ~110 giorni di pubblicità di quel
  giocatore. Quasi nessuno resta 110 giorni: ogni vendita di `no_ads` è **guadagno netto**,
  non un mancato ricavo pubblicitario. Non abbassare quel prezzo.
- **Il catalogo non ha ricavo ripetibile.** Quattro non consumabili significano un tetto di
  €14,99 per giocatore, per sempre. Un gioco con quest giornaliere e stagioni può avere un
  acquisto ricorrente senza tradire il design: **non pacchetti di miele** (romperebbero il
  bilanciamento che hai difeso apposta), ma un pass stagionale cosmetico o skin per i dadi.
  È la voce numero uno della lista post-lancio.

### 4.2 Costi

| Voce | Costo |
| --- | --- |
| VPS 2 vCore / 2 GB | €5–15/mese |
| Dominio | ~€15/anno |
| Play Console | $25 una tantum (già pagato) |
| Tasse su ricavi ads/IAP | regime tuo — conta ~25-35% del netto |
| **Break-even infrastruttura** | **~25 DAU** |

### 4.3 Perché non devi comprare installi

| | |
| --- | --- |
| LTV per install (base) | €0,18–0,28 |
| CPI Android per il tuo genere in EU | €0,50–1,00 (strategia/carte tier-1 arriva a €4) |
| Rapporto | **0,2×–0,5×, contro un minimo sano di 1,5×** |

Sei **da 3 a 8 volte sotto il break-even**. Ogni euro speso in UA oggi ne restituisce venti
centesimi. La UA a pagamento diventa discutibile solo quando avrai, misurati e non stimati:
**D1 > 30%**, **ARPDAU > €0,05** e **conversione IAP > 3%**. Fino ad allora ogni euro va in
retention e contenuti, che è l'unico investimento che alza tutti e tre i numeri insieme.

---

## 5. Il rischio numero uno del piano ricavi

Va detto chiaro perché è l'unica cosa che può azzerare la colonna "ads" di ogni tabella qui
sopra: **`TavernQuestClaim` usa un interstitial come cancello per una ricompensa**, ed è
fuori policy sia per AdMob sia per AdSense (il formato previsto per un premio è il rewarded).
La decisione è tua e presa consapevolmente il 5 agosto, il documento lo dice.

Quello che il documento tecnico non quantifica è il lato economico:

- **La perdita potenziale non è il placement, è l'account.** Una limitazione del servizio su
  quell'account AdMob spegne tutti e cinque i placement insieme, e l'account AdSense è lo
  stesso numero (`pub-3580486749764055`), quindi porterebbe giù anche il web.
- **Il rischio cresce col volume.** A 30 DAU non se ne accorge nessuno; a 3.000 DAU quello è
  il tuo placement a volume più alto, con un pattern (premio dietro interstitial, dieci volte
  al giorno) che i controlli automatici riconoscono.
- **La conversione ti fa guadagnare di più.** Il rewarded paga circa il doppio
  dell'interstitial: convertire il placement a volume più alto vale **+30–45% di eCPM medio**.
  Non è una rinuncia, è la leva #3 della §3.4.
- **Costa una riga di codice** (`AdPlacements.FormatOf`) più una ad unit in console, e sul web
  niente.

Non è una cosa da fare prima del freeze del 17 settembre — la regola "niente feature nuove"
vale anche per questa. **È la prima voce della prima patch post-lancio**, dove è insieme la
correzione di rischio più economica e l'aumento di ricavo più grande disponibile.

---

## 6. Piano d'azione

### Prima del 26 settembre (non tocca il codice, non tocca la roadmap)

- [ ] **Nome dell'app**: `AcCard N' Die: Carte e Dadi` in IT, `AcCard N' Die: Dice Cards` in
      EN. Modifica di 2 minuti, effetto permanente sulla scoperta.
- [ ] **Messaggio di consenso UE**, pubblicato e con l'app inclusa, sia su AdMob sia su
      AdSense. Se salti questa casella, tutte le tabelle della §3 valgono zero.
- [ ] **`app-ads.txt` deployato**, dominio identico a quello dichiarato in Play Console
      (attenzione al `www.`), poi verifica lo stato dopo 48 h.
- [ ] **Descrizione breve**: usa la variante 2 già scritta in
      [play-store-listing.md](../Marketing/play-store-listing.md), e aggiungi alla descrizione
      completa una riga esplicita sulla monetizzazione — "nessun gacha, nessuna energia,
      sblocchi che si comprano una volta sola" è un argomento di vendita nel 2026.
- [ ] **Video verticale da 15-20 secondi solo di dadi** (lancio, rimbalzo, scontro vinto). Ti
      serve per: video di anteprima su Play, primo post ovunque, candidatura ai portali.
- [ ] **Misura D1 e D7 sui tester chiusi** nella scheda **Retention** del pannello admin
      ([admin-panel.md](admin-panel.md#retention)). Se il D1 è sotto il 20%, il problema da
      risolvere non è il marketing. Con 12 tester il numero è indicativo: serve a vedere un
      disastro, non a decidere.
- [ ] **Discord o canale Telegram aperto** e linkato nel gioco e nel sito. Serve prima del
      lancio, non dopo: i primi cento giocatori sono l'unica fonte di feedback che avrai.

### Ottobre-novembre (dopo il lancio, in ordine)

1. **Prima patch**: `TavernQuestClaim` → rewarded (§5).
2. **Candidatura ai portali web**, CrazyGames per primo. È il canale con il rapporto
   volume/sforzo più alto che hai.
3. **Numeri veri al posto delle stime**: ARPDAU, impression/DAU, conversione IAP,
   percentuale di quest riscosse. D1/D7/D30 per coorte ci sono già (scheda Retention);
   il resto no, e ARPDAU va letto dai report AdMob finché non lo si porta dentro.
4. **Tutorial progressivo**: la leva più grande di tutte, perché moltiplica ogni riga.
5. **Ritmo di contenuti**: 3 video brevi a settimana per 8 settimane, poi si guarda cosa ha
   funzionato e si smette di fare il resto.

### Cosa misurare, e la soglia che fa scattare una decisione

| Metrica | Soglia | Decisione |
| --- | --- | --- |
| D1 | < 20% | fermare il marketing, lavorare sull'onboarding |
| D1 | > 30% | il gioco tiene: spingere sull'acquisizione |
| ARPDAU | < €0,01 | controllare fill rate e consenso UE prima di tutto il resto |
| DAU | > 1.000 | attivare la mediation |
| Conversione IAP | < 0,5% | il negozio non si vede o non convince: rivedere le tile |
| LTV/CPI | > 1,5× | solo allora si può valutare la UA a pagamento |

---

## Fonti

Benchmark 2026 usati per eCPM, CPI e retention:

- [App Ad Revenue Benchmarks 2026: eCPMs by Format, Region, and Platform — AdReact](https://adreact.com/blog/app-ad-revenue-benchmarks-2026/)
- [AdMob eCPM Benchmarks — Playwire](https://www.playwire.com/blog/admob-ecpm-benchmarks-what-publishers-should-expect)
- [2026 Mobile Game UA Cost Benchmarks: CPI by Genre and Region — FoxData](https://foxdata.com/en/blogs/2026-mobile-game-user-acquisition-cost-benchmarks-how-much-should-you-spend/)
- [Mobile Game CPI Benchmarks 2026 — Game Growth Advisor](https://gamegrowthadvisor.com/blog/2026-03-17-user-acquisition-cpi-benchmarks-2026/)
- [Mobile Game Retention Benchmarks 2026 — Segwise](https://segwise.ai/blog/mobile-gaming-app-user-retention-strategies)
- [App Retention Benchmarks 2026: Day 1, 7, 30 by Category — Apsteq](https://apsteq.com/blog/app-retention-benchmarks/)
- [CrazyGames Developer Guide: Publish and Earn (2026) — Cinevva](https://app.cinevva.com/guides/publish-game-crazygames)
- [Web Game Monetization: What the Data Actually Says (2026) — Cinevva](https://app.cinevva.com/guides/web-game-monetization)

Documenti interni di riferimento: [ads-design.md](ads-design.md), [iap-design.md](iap-design.md),
[roadmap-lancio-26-settembre.md](roadmap-lancio-26-settembre.md),
[tutorial-progressivo-design.md](tutorial-progressivo-design.md),
[play-store-listing.md](../Marketing/play-store-listing.md).
