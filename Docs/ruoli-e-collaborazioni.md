# Chi cercare, cosa chiedergli, come pagarlo

Companion di [`roadmap-lancio-26-settembre.md`](roadmap-lancio-26-settembre.md).
Scritto il 2026-08-16, a 41 giorni dal lancio.

---

## 1. Prima di tutto: cosa NON delegare adesso

A 41 giorni dal lancio, **aggiungere programmatori ti rallenta**. Il progetto ha un
`BattleBoardController` da 19.200 righe in 23 partial senza test: portare qualcuno al punto
di poterci mettere le mani costa più tempo di quanto ne resti. Ogni ora che passi a spiegare
il codice è un'ora che non passi a scriverlo, e chi arriva ora produrrà il suo primo commit
utile intorno al 10 settembre, cioè a code freeze quasi fatto.

Questo **non** significa restare soli. Significa che l'aiuto che serve adesso è
**tutto ciò che non tocca il repository**. E per fortuna è anche l'aiuto che ti manca di più.

---

## 2. Il buco vero: i canali ci sono ma sono scollegati

I canali del gioco esistono:

- YouTube — <https://www.youtube.com/@accardndie>
- Instagram — <https://www.instagram.com/accardndie/>
- TikTok — <https://www.tiktok.com/@accardndie>

Il problema non è che manchino: è che **non sono collegati a niente**. Verificato il
2026-08-16:

- nessuna delle pagine del sito li nomina (né le pagine statiche in `Docs/web/`, né il
  footer generato dal server in `Server/AccardND.Server/Web/SiteLayout.cs`);
- il gioco non contiene nessun link a essi (l'unico `Application.OpenURL` in tutto il
  progetto serve al login Google e all'aggiornamento);
- non esiste un Discord, cioè non c'è un posto dove le persone che ti trovano possano
  restare e parlarsi.

Il risultato è che i tre canali e il gioco sono tre isole. Chi guarda un video su TikTok non
sa dove scaricare; chi apre il sito non sa che esistono i video; chi finisce una partita non
ha nessun posto dove andare. Ogni persona che arriva viene persa subito dopo.

E c'è una conseguenza pratica immediata: **i 12 tester del test chiuso sono un problema di
pubblico**, non di codice. Chi ha un canale con dei follower li rimedia in un giorno.

Quindi la priorità non è "aprire i social" — è **collegarli tra loro e alimentarli con
costanza fino al lancio**, che è esattamente il lavoro che non hai il tempo di fare da solo
mentre chiudi quattro capitoli, gli acquisti in-app e il PvP.

> **Lavoro da fare tu, mezz'ora, questa settimana:** i tre link nel footer del sito (due
> punti: le pagine statiche e `SiteLayout.Footer()`) e un blocco social nelle Impostazioni
> del gioco. Costa pochissimo e trasforma ogni giocatore in un potenziale follower.

---

## 3. I cinque ruoli, in ordine di urgenza

Ogni ruolo è scritto per essere **circoscritto**: un impegno chiaro, una fine, e nessun
bisogno di accesso al codice. È l'unico modo in cui una collaborazione con uno sconosciuto
funziona davvero.

### Ruolo 1 — Community & social (il più urgente)
**Impegno:** 3–5 ore a settimana, da subito al lancio.
**Cosa fa concretamente:**
- **alimenta i tre canali che già esistono** (YouTube, Instagram, TikTok) con 3–4 clip a
  settimana ricavate dalle catture di gioco che gli passi tu: è la differenza tra un canale
  aperto e un canale vivo, ed è l'unica cosa che gli algoritmi premiano;
- apre e gestisce il Discord del gioco, che diventa anche la casa dei tester;
- costruisce il **conto alla rovescia verso il 26 settembre**: una data pubblica è il
  materiale narrativo migliore che hai, e oggi non la sa nessuno;
- porta il gioco dove stanno i giocatori: r/AndroidGaming, r/playmygame, r/IndieGaming,
  IndieDB, i server Discord di giochi di carte;
- prepara il "kit del giorno del lancio": post pronti, orari, chi avvisare.

**Il contenuto è già mezzo pronto e non lo stai usando:** hai una scena `PromotionalTrailer`,
i poster in `Docs/web/media/` (`poster-boss.jpg`, `poster-hub.jpg`, `poster-scontro.jpg`) e
una `social-card.jpg`. Serve qualcuno che li trasformi in pubblicazioni con una cadenza, non
qualcuno che parta da zero.

**Cosa ti serve dargli:** le catture di gioco (la scena `PromotionalTrailer` esiste già),
le immagini in `Docs/web/media/`, e la libertà di parlare a nome del gioco.

**Perché è il primo:** è l'unico ruolo che, se non lo copri, rende inutile tutto il resto
della roadmap.

---

### Ruolo 2 — Coordinatore dei tester
**Impegno:** 2–3 ore a settimana fino al lancio.
**Cosa fa:**
- recluta e tiene vivi i **12 tester opted-in** che il test chiuso di Play richiede per
  14 giorni consecutivi (e controlla due volte a settimana che nessuno esca, perché quello
  ti azzoppa il conteggio);
- raccoglie le segnalazioni in un posto solo, con un formato fisso: cosa facevo, cosa mi
  aspettavo, cosa è successo, telefono e versione di Android;
- filtra: ti arriva un elenco ordinato per gravità, non 40 messaggi Discord.

**Perché è il secondo:** sta letteralmente sul percorso critico del lancio. È il ruolo con
il collegamento più diretto e più misurabile alla data del 26.

---

### Ruolo 3 — Traduttori madrelingua (en, es, fr, de)
**Impegno:** una tantum, 4–8 ore a persona, tra il 14 e il 20 settembre.
**Cosa fanno:** rileggono la loro lingua nel gioco. Le tabelle esistono già
(`Assets/_Project/Localization/Tables/`), ma il tutorial nuovo non è tradotto e i messaggi
di errore escono in inglese tecnico grezzo.
**Cosa NON serve:** che tocchino Unity. Mandi loro un foglio con chiave, italiano e
traduzione attuale; te lo rimandano corretto.

**Perché conviene:** è il ruolo più facile da riempire (quattro persone diverse, ognuna con
un compito piccolo e finito) e alza la qualità percepita in quattro mercati.

---

### Ruolo 4 — Artista 2D, a cottimo
**Impegno:** un pacchetto di lavoro definito, con una scadenza.
**Cosa fa:** le **17 icone dei nodi talento** mancanti (i prompt sono già scritti in
`Docs/talenti-art-prompts.md`, 9 su 28 esistono già) e la sostituzione degli artwork rotti
delle carte golem e kraken.
**Perché funziona:** è il tipo di lavoro perfetto da pagare — perimetro chiuso, risultato
verificabile, nessuna dipendenza dal resto del progetto, e se la persona sparisce a metà non
ti blocca il lancio.

---

### Ruolo 5 — Game designer / secondo cervello sul contenuto
**Impegno:** variabile, ma serve solo se decidi di salvare il capitolo 5.
**Cosa fa:** progetta il capitolo 5 (oggi non ha nome, scenario né boss) e il
comportamento del boss Jurinashor, in un documento che tu poi implementi.
**Attenzione:** questo è l'unico ruolo che tocca il gioco vero. Aprilo solo se trovi
qualcuno di cui ti fidi *e* che è veloce. Altrimenti applichi il piano B della roadmap
(capitolo 5 non giocabile) e lo riapri dopo il lancio, senza rimpianti.

---

## 4. Come pagarli — dal più leggero al più impegnativo

Sali questa scala **solo quando serve davvero**. Il livello 1 copre più casi di quanti
immagini.

### Livello 1 — Contropartita non monetaria
Funziona per tester, traduttori, primi membri della community. Non è un ripiego: per molte
persone vale più dei soldi.
- **nome nei titoli di coda** del gioco (fallo davvero, e mettilo in una schermata visibile);
- accesso anticipato a ogni build;
- sblocco permanente di tutti i contenuti sul loro account (hai già il pannello admin con
  gli sblocchi a mano per gli account di test: è esattamente questo);
- un ruolo dedicato e visibile sul Discord;
- una carta, un nemico o un oggetto del gioco intitolato a loro. Costa zero e la gente lo
  ricorda per anni.

### Livello 2 — Compenso simbolico, ma fatto bene
Qui sta la tua domanda su "pagarli come dipendenti in maniera simbolica non formale".

**Il pezzo "non formale" è quello da togliere.** Pagare per lavoro senza inquadramento è
lavoro nero, e le sanzioni ricadono su chi paga, cioè te — proprio mentre stai per mettere il
tuo nome su un prodotto commerciale con acquisti in-app. La buona notizia è che non ti serve:
esiste una forma quasi altrettanto leggera e del tutto regolare.

- **Prestazione occasionale** (lavoro autonomo occasionale): niente partita IVA, basta una
  ricevuta con ritenuta d'acconto. È pensata esattamente per questo — lavoro saltuario, non
  continuativo, sotto una soglia annua per committente. È il veicolo giusto per l'artista a
  cottimo e per un compenso simbolico al community manager.
- **Cessione dei diritti d'autore** per chi produce opere creative (icone, musica,
  illustrazioni): è un contratto diverso, con un trattamento fiscale suo, e ti risolve
  insieme il pagamento **e** la proprietà (vedi sezione 5).

Le soglie, le aliquote e la modulistica cambiano e non sono cose da ricostruire a memoria:
**una mezz'ora da un commercialista** prima del primo pagamento ti mette a posto per tutti i
collaboratori che verranno. È il singolo investimento con il miglior ritorno di questa lista.

### Livello 3 — Revenue share (il vero punto d'incontro)
Quando qualcuno vale più di un compenso simbolico ma tu non hai liquidità, **questo è lo
strumento, non le quote.**

Una percentuale dei **ricavi netti** del gioco (dopo il 15–30% di Google, dopo i costi del
VPS e degli strumenti), per una **durata definita** — per esempio 24 mesi dal lancio — e con
un **tetto** oltre il quale si chiude. Tutto per iscritto.

Perché è meglio delle quote: paga solo se il gioco incassa, ha una fine naturale, non
richiede di costituire una società, non dà diritto di voto su cosa fai del progetto, e non ti
lascia legato a vita a qualcuno che ha smesso di rispondere ai messaggi a novembre.

### Livello 4 — Quote: non adesso
"Cedere le quote" presuppone una società che al momento non c'è: senza una srl, ciò che
staresti davvero cedendo è la **comproprietà della proprietà intellettuale**, che è la cosa
più difficile da disfare nell'intero panorama delle cose che puoi firmare.

E c'è un problema di tempi: stai valutando di dare una fetta permanente del progetto a
persone che non hai ancora conosciuto, per un lavoro che non hanno ancora fatto, **41 giorni
prima del lancio**. La sequenza corretta è l'inverso: fai lavorare le persone sui compiti
circoscritti della sezione 3, guarda chi consegna davvero, e **riapri il discorso a novembre**
su chi è ancora lì.

Se un giorno lo farai, due condizioni non negoziabili, che sono lo standard ovunque proprio
perché tutti hanno già sbagliato prima:
- **vesting su 4 anni**, cioè la quota si matura poco per volta lavorando, non si riceve alla
  firma;
- **cliff di 1 anno**: chi se ne va prima di dodici mesi non porta via niente.

Senza queste due righe, la storia standard è che il 20% del tuo progetto appartiene per
sempre a qualcuno che c'è stato tre settimane.

---

## 5. L'unica cosa che non puoi saltare

**Chi crea qualcosa ne detiene il diritto d'autore, finché non lo cede per iscritto.**

Vale anche per i volontari, anche per gli amici, anche per chi ti manda un'icona gratis su
Discord "così, per aiutarti". Senza una cessione scritta, quelle icone non sono tue: sono
sue, tu hai solo un permesso implicito che può essere ritirato. È il tipo di problema che non
si manifesta mai il primo mese e si manifesta sempre nel momento peggiore — una controversia,
un publisher che fa due domande, uno store che riceve una segnalazione.

Quindi: **una liberatoria di poche righe, firmata da chiunque produca qualcosa che finisce
nel gioco**, gratis o pagato che sia. Nome, cosa ha realizzato, cessione dei diritti di
utilizzo economico in esclusiva, data, firma. È mezza pagina e ti evita l'unico rischio di
questa lista che può davvero fermare una pubblicazione.

Stessa logica per l'account Google Play: **resta intestato a te**, sempre, chiunque entri nel
progetto.

---

## 6. Dove trovare queste persone

Per **community e social**: server Discord di sviluppo indie italiano, IGDA Italia, Svilupparty,
i gruppi Facebook/Reddit di gamedev italiani. Cerca chi già fa questo per un altro progetto
piccolo e lo fa bene — si vede in dieci minuti guardando cosa pubblica.

Per i **tester**: i gruppi di test reciproco per il requisito dei 12 tester di Google esistono
apposta e sono pieni di sviluppatori nella tua identica situazione (r/TestMyApp e simili). In
più: amici, parenti, colleghi. Dodici persone con un telefono Android sono meno di quante
sembrino.

Per **traduttori e artisti**: università e ITS con corsi di grafica o game design. Gli studenti
cercano portfolio e crediti reali su un gioco pubblicato, che è esattamente ciò che tu puoi
dare e che vale più di quanto potresti pagare.

Per tutti: **il tuo sito e il gioco stesso sono il canale di reclutamento migliore che hai.**
Chi ha già giocato e gli è piaciuto è mille volte più adatto di uno sconosciuto.

---

## 7. Come proporlo

Il messaggio che funziona è specifico, breve e onesto sui limiti. Modello:

> Sto per pubblicare **AcCard N' Die**, un gioco di carte tattico a dadi per Android e web,
> il **26 settembre**. L'ho scritto da solo ed è in fase avanzata: campagna a capitoli,
> multiplayer online, progressione, cinque lingue.
>
> Cerco **[ruolo]** per **[3-5 ore a settimana / questo pacchetto di lavoro]** fino al lancio.
> Concretamente: **[le tre righe della sezione 3]**.
>
> Cosa posso offrire adesso: crediti nel gioco, accesso completo, e **[compenso simbolico
> regolare / una quota dei ricavi da definire insieme]**. Sono trasparente: è un progetto
> indipendente senza finanziamenti, non posso promettere uno stipendio. Quello che posso
> promettere è un progetto vero che esce davvero, con una data, e il tuo nome sopra.
>
> Se ti interessa, ci giochiamo una partita insieme e ne parliamo.

Tre cose che rendono questo messaggio migliore del 90% degli annunci simili: **c'è una data**
(quindi il progetto è vero), **l'impegno è quantificato** (quindi non è un buco nero), e
**ammetti che non ci sono soldi** (quindi non stai fingendo, e chi risponde risponde sapendolo).

---

## 8. La sequenza consigliata

| Quando | Cosa |
| --- | --- |
| **questa settimana** | **collega i canali**: i tre link nel footer del sito e nelle Impostazioni del gioco. Mezz'ora, e smetti di perdere ogni persona che arriva |
| **questa settimana** | apri il Discord tu stesso, anche vuoto. Serve un posto dove far atterrare le persone, e serve prima di cercarle |
| **questa settimana** | pubblica l'annuncio per il ruolo 1 e il ruolo 2. Sono gli unici due sul percorso critico |
| **questa settimana** | **annuncia la data del 26 settembre** sui tre canali. Da lì in poi ogni pubblicazione ha una cornice |
| **entro fine agosto** | mezz'ora dal commercialista, prima di qualsiasi pagamento |
| **inizio settembre** | ingaggia l'artista a cottimo, se il budget lo consente |
| **metà settembre** | traduttori, giusto in tempo per la fase 4 della roadmap |
| **novembre** | *solo allora* riapri il discorso quote/revenue share, con chi è ancora lì |

---

**Nota su questo documento.** Contiene orientamento pratico, non consulenza legale o fiscale:
le forme contrattuali e le soglie vanno confermate da un commercialista o da un avvocato
prima di firmare o pagare qualcosa.
