# Trailer di lancio — AcCard N' Die

Scritto il 18 agosto 2026, a 39 giorni dal lancio ([roadmap-lancio-26-settembre.md](roadmap-lancio-26-settembre.md)).

Questo documento dice **quali video servono**, **cosa c'è dentro fotogramma per fotogramma**,
**come si catturano** e **entro quando**. Nasce dalla §1.2 di
[analisi-mercato-e-ricavi.md](analisi-mercato-e-ricavi.md), che aveva già individuato l'unico
asset di marketing che funziona senza spiegazioni: **i dadi 3D lanciati sul tavolo**.

> **La conclusione, in una riga.** Non serve *un* trailer: servono quattro tagli diversi dallo
> stesso girato. Quello che porta installi è il preview da 30 secondi su Play e i verticali;
> il trailer lungo serve per i portali e per chi ti chiede "fammi vedere".

---

## 1. I quattro asset

| Asset | Durata | Formato | Dove finisce | La regola che lo governa |
|---|---|---|---|---|
| **Preview video Play** | 30s | 16:9 | scheda Play, ospitato su YouTube | parte **in automatico e muto** con l'icona sopra: i primi 3 secondi sono tutto |
| **Trailer di lancio** | 60–75s | 16:9 | YouTube, portali web, press kit, candidature | l'unico dove il suono conta davvero |
| **Verticali** | 8–20s | 9:16 | TikTok / Shorts / Reels | un'idea sola per video, nessuna introduzione |
| **Loop del sito** | 6–10s | 16:9 muti | li aspettano già i tag `<video>` in `web/index.html` | si comportano come GIF, non come filmati |

I tre loop del sito hanno già nome e posto: `Docs/web/media/vetrina-campagna.webm`,
`vetrina-scontro.webm`, `vetrina-boss.webm` (più i `.mp4` gemelli). Finché non ci sono, la
homepage mostra i poster `poster-hub.jpg`, `poster-scontro.jpg`, `poster-boss.jpg` — che sono
già i fotogrammi giusti da cui partire per capire l'inquadratura.

**Ordine di produzione:** prima il preview da 30s. Il trailer lungo è il preview con tre
blocchi in più, i verticali sono ritagli del suo girato. Se fai per primo il video lungo,
finisci a comprimerlo male in 30 secondi.

### Vincoli di Play da verificare in Console prima di caricare

Il preview video di Play è un link a un video YouTube pubblico, senza annunci e non soggetto a
limiti d'età. Le linee guida chiedono di **non** metterci call-to-action tipo "installa ora" né
prezzi o promozioni, e vogliono contenuto di gioco reale — niente montaggi di sola grafica
promozionale. La card finale con i due canali tienila quindi per la versione YouTube, non per
quella di Play. Ricontrolla i requisiti in Console il giorno del caricamento: cambiano.

---

## 2. La tesi

Un trailer dimostra **una frase sola**. La tua è già scritta in homepage:

> *La fortuna conta, ma è la tattica a piegarla dalla tua parte.*

Tutto il montaggio dimostra quella frase. Fuori dal trailer restano — e vanno negli screenshot,
nella descrizione e nei post separati — talenti, quest giornaliere, leghe e stagioni, miele,
negozio, santuario. Non perché non contino, ma perché ogni secondo speso a spiegarli è un
secondo tolto ai dadi.

La struttura che ne esce è quella di Dicey Dungeons e Luck be a Landlord, ed è la stessa del tuo
game design: **il dado ti frega → scopri che puoi truccarlo → tiro impossibile vinto**.

---

## 3. Shot list — preview 30s (16:9)

I tempi sono quelli del montaggio finale, non della cattura: cattura sempre 2–3 secondi in più
per lato di ogni ripresa.

Le nove riprese le produce tutte la scena di cattura (§5), che gioca con le pedine e i VFX veri.
Restano da girare in partita vera solo i tre blocchi del trailer lungo — rifugio, duelli,
campagna — perché sono schermate, non scontri.

| # | Tempo | Inquadratura | Cattura da | Audio |
|---|---|---|---|---|
| 1 | 0:00–0:03 | **Cold open.** Primo piano del dado 3D che cade, rimbalza sulla parete di metà campo e si ferma su un valore basso. Nessun testo, nessun logo. | scena trailer, blocco `ColdOpen` | il clatter secco in primo piano, musica assente |
| 2 | 0:03–0:07 | Stacco largo: le due formazioni in campo, il colpo di classe dell'avversario va a segno, la tua pedina cade. | scena trailer, blocco `ColdOpen` | il VFX porta il suo suono, la musica entra sotto |
| 3 | 0:07–0:10 | **Card 1: "Il dado decide."** | scena trailer | un beat isolato |
| 4 | 0:10–0:13 | Schieramento: le pedine entrano una alla volta, si vede il triangolo delle fazioni. | scena trailer, blocco `Montage` | il suono di ingresso di ogni classe, la musica costruisce |
| 5 | 0:13–0:16 | La costellazione del Paladino si accende su una pedina e la forza sale. Un solo esempio, leggibile. | scena trailer, blocco `Montage` | il suono dell'abilità di classe |
| 6 | 0:16–0:18 | Il marchio del Cacciatore si chiude su un bersaglio avversario. | scena trailer, blocco `Montage` | |
| 7 | 0:18–0:21 | **Card 2: "Tu decidi il dado."** | scena trailer | |
| 8 | 0:21–0:26 | **Payoff.** Jurinashor evoca le tre spade, il tuo primo colpo viene deviato da una lama, poi il dado: rotolamento lungo, valore alto, parte il supremo e il boss cade. | scena trailer, blocco `Payoff` | il colpo più forte del brano cade **sul dado che si ferma**, non un frame dopo |
| 9 | 0:26–0:30 | Titolo e icona su fondo scuro. Su Play si ferma qui. | scena trailer, blocco `Title` | coda |

### I tre blocchi che allungano il trailer a 60–75s

Si inseriscono tra lo shot 6 e la Card 2, in quest'ordine:

- **Il rifugio** (6s) — che il gioco continua tra una run e l'altra: taverna, negozio, santuario.
- **I duelli** (8s) — due tabelloni affiancati, il round che si chiude. È l'unico punto dove
  serve una parola di contesto: *"al meglio dei tre"*.
- **La campagna** (6s) — la mappa dei sette capitoli, uno che si sblocca.

E in coda, solo nella versione YouTube: card finale con nome, icona, "su Google Play e nel
browser", dominio.

### Testo, non voce fuori campo

Spedisci in sei lingue: una voce fuori campo va incisa sei volte, tre card di testo si
riesportano in dieci minuti. E le card funzionano mute, che è la condizione normale sia su Play
sia sui social. Le due frasi sono corte apposta — vanno tradotte con lo stesso peso, non con la
stessa lunghezza.

Le card usano **IM Fell English SC** (`MmoUiTheme.LoreFont`, il font da prosa del gioco), a corpo
pieno schermo. È un maiuscoletto: le righe vanno scritte in **maiuscolo/minuscolo**, perché su
una stringa già tutta in caps il font non aggiunge niente e tanto valeva usarne un altro. Vale
anche per le traduzioni.

| IT | EN | ES | FR | DE | PT |
|---|---|---|---|---|---|
| Il dado decide. | The die decides. | El dado decide. | Le dé décide. | Der Würfel entscheidet. | O dado decide. |
| Tu decidi il dado. | You decide the die. | Tú decides el dado. | Tu décides le dé. | Du entscheidest den Würfel. | Você decide o dado. |

Da far ricontrollare a chi rilegge le stringhe di gioco prima dell'export: queste finiscono
davanti a più persone di qualunque schermata.

---

## 4. I primi sei verticali (9:16)

Un'idea per video, gancio nel primo mezzo secondo, nessun logo, testo grande **in alto** (in
basso ci va l'interfaccia dell'app).

1. **Solo dadi** — 15s di lanci, rimbalzi e valori, UI ridotta al minimo. È il video numero uno
   indicato dall'analisi di mercato: serve anche da preview di Play e da candidatura ai portali.
2. **"Serviva un 6"** — il tiro decisivo, riuscito.
3. **"Serviva un 6", ma no** — la stessa inquadratura, fallita. La versione che perde spesso
   gira meglio di quella che vince.
4. **Il boss in 12 secondi** — dall'entrata alla morte, un capitolo per video.
5. **Il triangolo senza parole** — tre schieramenti, tre esiti, solo i nomi delle fazioni.
6. **Il nome è impronunciabile** — meta, sul nome del gioco. Funziona, e attacca da un altro
   lato il problema di ricercabilità descritto nell'analisi.

Il verticale si **cattura verticale**: ritagliare il 16:9 taglia fuori proprio i dadi.

---

## 5. Come si cattura

### La scena che si registra da sola

`Assets/_Project/Scenes/Promotional/PromotionalTrailer.unity`, che si apre anche dal menu
**AccardND ▸ Trailer ▸ Apri scena trailer**: si preme Play e la sequenza parte da sola, senza
bootstrap di gioco e senza input.

**Non c'è niente di finto dentro.** Le pedine sono `PrototypeCardView` create con
`CreateBattlefieldPreview`, le stesse della partita. La misura della pedina, il gioco d'aria fra
una e l'altra e l'altezza delle due file **non sono a occhio**: la scena rifà lo stesso calcolo
di `ApplyResponsiveLayout` leggendo `configuration.ResponsiveLayout`, e monta le carte a mano
come `ConfigureBattlefieldRow` invece di lasciarle a un `HorizontalLayoutGroup`. È quello che
tiene il numero di Potenza al suo posto sulla carta: con la pedina stirata scivolava in basso e
si staccava dalla pedina.

I colpi passano da `BattlePresentationAnimationPlayer` (`PlayClassAttack`, la costellazione del
Paladino, il marchio del Cacciatore, la deviazione di Jurinashor, il supremo del Guerriero) e i
suoni da `BattleSfxPlayer` — ingresso in campo, abilità di classe, evocazione delle spade, colpo
a segno, morte. Il boss usa la sua presentazione a fondale, non una pedina in fila. I dadi sono
`Dice3DRollView`.

Nessun riferimento è serializzato nella scena: carte, fondali, VFX e suoni li carica la sequenza
a runtime da `Resources` (`CardDatabase`, `GameConfiguration`, `Backgrounds/`, `SFX/`), gli
stessi che usa il gioco. Vuol dire che **il trailer non può andare fuori sincrono**: se cambi
una carta, un'animazione di classe o un suono, la ripresa cambia con loro.

Comandi durante la riproduzione:

| Tasto | Cosa fa |
|---|---|
| `R` o `1` | rigira la sequenza dall'inizio |
| `2` | card "IL DADO DECIDE." |
| `3` | montaggio (schieramento, abilità del Paladino, marchio del Cacciatore) |
| `4` | card "Tu decidi il dado." + payoff sul boss |
| `Spazio` | ripete la ripresa in corso — serve per catturare dieci take dello stesso tiro di fila |
| `G` | mostra/nasconde la guida del taglio 9:16 al centro |

Nell'ispettore del root si cambia il cast senza toccare il codice:

| Campo | A cosa serve |
|---|---|
| `playerClasses` / `enemyClasses` | le classi delle due formazioni. La carta scelta è sempre **la più forte** di quella classe: il trailer non deve mai mostrare materiale da tutorial |
| `bossCardId` / `bossBackground` | quale boss chiude il payoff e con che fondale. Di serie è **Jurinashor in prima fase** (`boss-jurinashor` + `Backgrounds/bg_jurinashor_phase_1`); valgono anche `trentor`, `boss-bragus`, `boss-palatir`, `boss-seraphel` |
| `swordCardId` / `summonedSwords` | le spade maledette evocate prima del tiro. Tre è il massimo della prima fase; sopra quel numero mostreresti una fase che nel gioco non esiste ancora |
| `coldOpenBackground` / `montageBackground` | gli scenari delle prime due riprese |
| `coldOpenResult` / `payoffResult` | i due tiri forzati: quello perso e quello vinto |
| `playbackSpeed` | mettilo a `0.6` per un rotolamento più lungo da rallentare in post |

La classe della prima pedina decide anche **il colore del dado**: nel cold open tira
l'avversario e il dado è suo, nel payoff tira il giocatore. È un dettaglio che si legge senza
spiegarlo, ed è il motivo per cui i due tiri non sembrano lo stesso stacco ripetuto.

Attenzione a una cosa: `Dice3DRollView` anima con `Time.unscaledDeltaTime`, quindi abbassare
`Time.timeScale` **non** rallenta il dado. Il rallentatore si ottiene in due modi, e vanno usati
insieme: durata del tiro più lunga in cattura (il campo `payoffRollDuration`) e rallentamento in
post su un girato a 60 fps.

### Impostazioni di cattura

- **Unity Recorder**, non OBS. Cattura a passo fisso, quindi niente micro-scatti quando il
  video verrà rallentato o riprodotto in loop.
- **1920×1080 a 60 fps** per il 16:9, **1080×1920 a 60 fps** per i verticali, come cattura
  separata.
- Nel Game view scegli la risoluzione esatta, non "Free Aspect": l'UI si ridispone e le riprese
  fatte in giorni diversi non si montano insieme.
- Per le riprese di partita vera: prepara **stati di gioco costruiti apposta** dal pannello
  admin (talenti sbloccati, campo pieno, boss al punto giusto). Il footage da tutorial — mazzo
  base, numeri bassi, campo vuoto — è la ragione più comune per cui un trailer indie sembra
  povero.

### Cosa non registrare mai

Menu e navigazione dell'interfaccia. Nemmeno un secondo, in nessuno dei quattro asset.

---

## 6. Errori che ammazzano un trailer indie

1. **Aprire sul logo.** Il logo sta alla fine. Su Play il video parte muto e in autoplay: nero
   più logo significa aver bruciato il 10% della durata e metà degli spettatori.
2. **Spiegare le regole.** Il regolamento sta sul sito, per intero. Il trailer mostra i verbi:
   pesco, schiero tre, lancio, rompo il piano dell'altro.
3. **Camera immobile.** Anche solo una spinta lenta in avanti in post cambia la percezione.
4. **Musica presa da YouTube.** Ti blocca il video nella settimana di lancio. Licenza pagata
   (Epidemic, Artlist o simili), ricevuta conservata insieme al progetto di montaggio.
5. **Mettere tutto.** Se nel trailer c'è anche il negozio, non c'è abbastanza dado.
6. **Un solo file per tutte le superfici.** È il punto §1 e resta l'errore più costoso.

---

## 7. Calendario, agganciato alla roadmap

Le fasi sono quelle di [roadmap-lancio-26-settembre.md](roadmap-lancio-26-settembre.md). Niente
di questo elenco tocca il codice di gioco, quindi il freeze del 17 settembre non lo blocca — ma
la **cattura** sì che dipende dal contenuto finale, e per questo sta prima.

| Quando | Cosa | Perché lì |
|---|---|---|
| **entro il 6 set** (fine Fase 2) | musica licenziata e scelta, guide di inquadratura decise | il montaggio senza brano definitivo si rifà da capo |
| **7–13 set** (Fase 3) | **cattura di tutto il girato**: scena trailer + partite preparate, 16:9 e 9:16 | i capitoli e l'arte sono al loro stato finale, la build è la candidata alla release |
| **14–17 set** (Fase 4) | montaggio del **preview 30s** e dei **primi 3 verticali** | prima del freeze hai ancora margine se una ripresa manca |
| **entro il 18 set** | preview caricato su YouTube (pubblico, senza annunci) e messo in scheda | va insieme all'AAB di release, così la revisione vede la scheda completa |
| **19–23 set** | trailer 60–75s e i **tre loop del sito** in `Docs/web/media/` | il deploy del sito del 26 li deve trovare già lì |
| **24 set** | verifica: autoplay muto della scheda su un telefono vero, loop del sito su 4G | i primi 3 secondi si giudicano sul dispositivo, non sul monitor |
| **26 set** | pubblicazione. Trailer lungo da non elencato a pubblico | |
| **da ottobre** | 3 verticali a settimana per 8 settimane, poi si guarda cosa ha funzionato | è il ritmo già deciso nell'analisi di mercato |

---

## 8. Riferimenti

- [analisi-mercato-e-ricavi.md](analisi-mercato-e-ricavi.md) — §1.2 (perché i dadi), §4 (canali
  e ritmo dei contenuti)
- [roadmap-lancio-26-settembre.md](roadmap-lancio-26-settembre.md) — le fasi citate in §7
- [../Marketing/play-store-listing.md](../Marketing/play-store-listing.md) — nome, descrizione
  breve e lunga, con cui il trailer deve dire la stessa cosa
- [web/media/README.md](web/media/README.md) — dove vanno i file del sito e da dove vengono i
  poster
- `Assets/_Project/Scenes/Promotional/PromotionalTrailer.unity` — la scena di cattura
- `Assets/_Project/Scripts/Presentation/Promo/PromotionalSequenceController.cs` — la sequenza
