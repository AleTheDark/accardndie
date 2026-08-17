# Talenti: albero di progressione account

## Obiettivo

Dare una forma alla progressione dell'account, che oggi e' un contatore e non una curva.

Tre interventi che si tengono insieme:

1. una **curva di esperienza** al posto dei 100 exp fissi per livello
2. un **moltiplicatore per capitolo** sull'exp di fine run, cosi' spingere avanti in
   campagna diventa il modo di salire di livello
3. un **albero di talenti** sotto il profilo, comprato con i **punti propoli** che il
   livello account distribuisce

Principio guida: **il talento e' un pollice sulla bilancia, non un'altra bilancia**. Ogni
nodo deve farsi notare dentro una run senza cambiare quale run e' vincente. Se un talento
rende una scelta di gioco ovvia, e' sbagliato il talento.

## Stato attuale

### Cosa esiste gia'

- livello account server-authoritative in `single_player_progress`: `account_level`,
  `account_experience`, `account_total_experience`, `account_experience_to_next_level`,
  `pending_level_rewards`
- `GrantAccountExperience` (`SinglePlayerProgressService.cs`) accumula exp e livelli in
  transazione, e mette i livelli non riscossi in `pending_level_rewards`
- `ClaimLevelRewards` converte i livelli in sospeso in miele a `HoneyPerAccountLevel = 5`
- moltiplicatore pubblicitario `AccountAdMultiplier = 3` su una reward gia' concessa
- protocollo a envelope su WebSocket (`MessageTypes.SinglePlayer*`, `Sanctuary*`,
  `Tavern*`), con il pattern "catalogo sul server, il client disegna" gia' collaudato dal
  Santuario (`SanctuaryCatalog`) e dalla taverna (`TavernQuests`)

### I tre difetti da correggere

**1. La curva non esiste.**
`AccountExperiencePerLevel = 100`, costante. Il livello 3 e il livello 60 costano
identico: il numero cresce, la progressione no.

**2. La sorgente e' piatta.**
`CalculateAccountExperience` fa `matchExperience / 10` con `DeathRewardExperienceCeiling
= 5000`: **massimo 500 exp per run, per sempre**. Capitolo 1 e Capitolo 7 pagano uguale,
quindi arrivare in fondo alla campagna non accelera niente.

**3. La ricompensa e' piatta e ridondante.**
`HoneyPerAccountLevel = 5` versa una goccia di miele in una pozza gia' riempita dalle
quest giornaliere (75 al giorno). Salire di livello non da' niente che il giocatore non
abbia gia', e ne da' sempre la stessa quantita'.

### Difetto collaterale trovato scrivendo questo documento

`RunProgressState.TrySpendExperience` e `AddSpendableExperience`
(`Assets/_Project/Scripts/GameCore/CombatResult.cs`) **non li chiama nessuno**.
`AvailableExperience` quindi non scende mai ed e' sempre uguale a `TotalExperience`.

Il commento in `BattleBoardController.CampaignProgress.cs` ("Solo l'EXP non spesa al
mercato viene convertita in EXP account") e il commento gemello sul server descrivono una
meccanica smontata. Vanno corretti insieme a questo lavoro: **exp account = exp totale
della run / 10**, punto.

Che sia cosi' e' un bene. L'alternativa era un'anti-sinergia nascosta, dove il giocatore
che spende bene dentro la run progredisce di meno fuori.

## Curva di esperienza

```
XpToNext(livello) = 100 + 25 * (livello - 1)
```

Una riga, nessuna tabella da mantenere allineata.

| Livello | Costo del livello | Run cumulate (~400 exp/run) |
| --- | --- | --- |
| 1 → 2 | 100 | — |
| 5 → 6 | 200 | ~2 |
| 10 → 11 | 325 | ~5 |
| 20 → 21 | 575 | ~16 |
| 30 → 31 | 825 | ~33 |
| 50 → 51 | 1325 | ~86 |

Exp cumulata per raggiungere il livello n: `100*(n-1) + 25*(n-1)*(n-2)/2`.

I primi dieci livelli restano rapidissimi, perche' il giocatore nuovo deve sentire la
spinta subito. Poi la coda si allunga senza mai fermarsi.

`account_experience_to_next_level` esiste gia' in tabella e oggi viene sempre riscritta a
100: diventa il valore vero della curva, e la UI del profilo e il pannello admin iniziano
a dire qualcosa senza modifiche.

**Migrazione.** Chi ha gia' un livello alto e' stato pagato con la curva piatta. Non si
ricalcola niente: `account_total_experience` resta lo storico lordo, il livello resta dov'e'
e da li' in avanti vale la curva nuova. Ricalcolare all'indietro degraderebbe account
esistenti, che e' il modo peggiore di introdurre una progressione.

## Moltiplicatore di capitolo

Un campo sul record `Chapter` in `ChapterCatalog`, che e' gia' dichiarato sorgente unica
dei capitoli ed e' quindi il posto naturale.

| Capitolo | Moltiplicatore |
| --- | --- |
| 1 · Rampicanti | ×1.0 |
| 2 · Nebbia | ×1.2 |
| 3 · Infestata | ×1.4 |
| 4 · Illuminata | ×1.6 |
| 5 | ×1.8 |
| 6 · Medusa | ×2.0 |
| 7 · Cosmica | ×2.5 |

Si applica in `CalculateAccountExperience`, **dopo** il cap:

```
exp account = min(matchExperience, 5000) / 10 * moltiplicatore(capitolo)
```

Il cap resta sull'exp di run, non sul risultato: il moltiplicatore deve poterlo superare,
altrimenti non esiste.

Effetto collaterale voluto: i capitoli comprati col miele al Santuario acquistano un
secondo valore, e la campagna smette di essere solo una collezione di boss.

**Il moltiplicatore pubblicitario resta ×3.** Per un breve periodo era stato abbassato a ×2,
temendo che ×3 sopra il ×2.5 del settimo capitolo facesse una run da ×7.5. Il timore
nasceva da un conto sbagliato: dava per buono che una run arrivasse al tetto delle 5000,
mentre ne produce circa 650. Il ×7.5 vero vale ~490 di esperienza account, cioe' una run
ottima e non un salto di livelli — ed e' quello che tiene il ritmo su valori umani. Vedi
[Quanto ci vuole davvero](#quanto-ci-vuole-davvero).

## Quanto ci vuole davvero

### Quanta esperienza produce una run

Il numero da cui dipende tutto, e che era stato dato per scontato sbagliando. Non e' il
tetto delle 5000: quello e' una rete contro un client che mente.

| Fonte | Valore |
| --- | --- |
| Stanza mostro | `BaseExperience` (5 Accessibile / 10 Normale / 15 Diabolica) + somma delle forze dei nemici abbattuti (~15-27) |
| Miniboss | 50 fissi, uno ogni 10 stanze |
| Stanza bottino | 10 |
| Opportunita' / mercante | 0 |

Una run completa e' di 25 stanze (`finalBossRoom`), di cui circa 18 mostro. Fa **~650 di
esperienza di run**, cioe' **65 di base** per l'account. Una run che muore a meta' ne fa
circa la meta'.

### La catena dei moltiplicatori

```
exp account = (exp di run / 10) × capitolo × video × pass stagionale
```

**Il pass stagionale non e' implementato**: nel codice la catena si ferma a capitolo e
video, e le colonne "con pass" delle tabelle qui sotto sono previsioni per dimensionare il
ritmo, non qualcosa che gira oggi.

Quando si fara', va messo accanto al capitolo in `CalculateAccountExperience` e **non** sul
video: chi ha comprato la rimozione della pubblicita' e chiunque giochi sul web ricevono
gia' il ×3 senza guardare nulla (`RewardsWaivedWithoutAds`), quindi attaccarlo li' premierebbe
due volte lo stesso gesto invece di premiare il pass.

Il video vale ×3 ed e' il ritmo normale, non un pedaggio: `AdsRemoved` (chi ha comprato la
rimozione della pubblicita') e tutto il web passano da `RewardsWaivedWithoutAds`, che
concede il moltiplicatore senza annuncio.

| Capitolo | ×cap | base | + video ×3 | + pass ×1.5 *(previsione)* |
| --- | --- | --- | --- | --- |
| 1 | 1.0 | 65 | 195 | 292 |
| 2 | 1.2 | 78 | 234 | 351 |
| 4 | 1.6 | 104 | 312 | 468 |
| 6 | 2.0 | 130 | 390 | 585 |
| 7 | 2.5 | 162 | **487** | **731** |

### Il ritmo che ne esce

Con il video e un percorso che sale di capitolo strada facendo (media ~330 a run):

| Livello | Exp cumulata | Run | Con pass ×1.5 *(previsione)* |
| --- | --- | --- | --- |
| 10 | 1.800 | 5 | 4 |
| 20 | 6.175 | 19 | 12 |
| 30 | 13.050 | 40 | 26 |
| 50 | 34.300 | 104 | 69 |
| 68 | 61.975 | **188** | **125** |

Il livello 68 e' dove i 114 punti dell'albero completo sono in mano, contando i 21 dei primi
boss. Con quattro capitoli giocabili su sette e' piu' avanti, intorno al 75.

Il pass toglie circa un terzo del percorso: si sente senza sostituire il gioco.

## Punti propoli

I punti talento del gioco. Stanno sul profilo, si guadagnano salendo di livello.

| Sorgente | Punti |
| --- | --- |
| ogni livello account | 1 |
| livelli multipli di 5 | +2 extra |
| prima uccisione di ogni boss di capitolo | +3 (21 in tutto, una tantum) |

Il premio del boss e' l'unica sorgente che non passa dal livello: paga l'avanzamento in
campagna invece del tempo passato a giocare. Si appoggia alla riga `chapterCleared`, quindi
rigiocare un capitolo continua a dare esperienza e contatori ma non ripaga i punti — se lo
facesse, il modo piu' veloce di riempire l'albero sarebbe ribattere per sempre il boss piu'
facile.

La formula e' `(livello − 1) + 2 × floor(livello / 5) + 3 × primi boss battuti`. Al livello
50 con tutti e sette i boss fa 49 + 20 + 21 = **90 punti**; al 68 arriva a 114.

L'albero completo costa **114 punti**, quindi si chiude al **livello 68** avendo battuto
tutti e sette i boss di capitolo. Con i quattro capitoli giocabili di oggi (12 punti di
boss) serve invece il livello 75. E' lontano ma raggiungibile: e' una fine, non un asintoto.

**Il livello smette di dare miele.** `pending_level_rewards` resta la stessa colonna e lo
stesso flusso di riscossione, cambia solo cosa accredita: punti propoli invece di
`HoneyPerAccountLevel`. Il miele torna a essere al 100% quest giornaliere, come gia'
dichiarato altrove, e il livello guadagna una ricompensa sua e riconoscibile.

**Le due valute non si toccano mai.** Miele → Santuario (capitoli, classi, tecniche,
oggetti). Propoli → talenti. Se i talenti costassero miele competerebbero con i capitoli, e
il giocatore smetterebbe di comprare capitoli: cioe' esattamente il contenuto che il
moltiplicatore di capitolo serve a spingere.

## Struttura dell'albero

### Nessun respec, quindi nessun bivio

Non e' previsto respec. Ne consegue una regola vincolante: **nessun nodo puo' essere
mutuamente esclusivo con un altro, e nessun nodo puo' essere una trappola**. Chi spende
male resterebbe fregato per sempre senza rimedio.

Ogni nodo e' quindi un miglioramento secco, tutto e' comprabile prima o poi, e l'unica
scelta reale e' **l'ordine**. Va bene cosi': la run e' gia' piena di scelte irreversibili,
l'albero account deve essere una rampa, non un rompicapo.

### Cancelli a tier, non a livello

Il tier successivo di un ramo si apre con i punti spesi **in quel ramo**:

| Tier | Punti spesi nel ramo |
| --- | --- |
| 1 | 0 |
| 2 | 2 |
| 3 | 7 |
| 4 | 14 |

Il livello distribuisce i punti, il ramo decide dove possono andare. La
specializzazione emerge da sola senza bisogno di un secondo cancello sul livello account.

> **Le soglie erano 5/12/20 e muravano tre rami su quattro.** Un cancello si misura sui punti
> spesi in quel ramo, quindi va tarato su quanto il ramo puo' assorbire, e nessuno l'aveva
> fatto: le Occasioni hanno un solo nodo di tier 1 che vale 4 punti in tutto, contro un
> cancello da 5, e il ramo si fermava li' anche con propoli infiniti. Stessa fine per
> Iniziativa (3 punti a tier 1) e Maestria (9 cumulativi a tier 2). Solo la Borsa, che ha due
> nodi di tier 2, arrivava in fondo. Il tetto reale ramo per ramo e' 3/9/17 punti cumulativi;
> le soglie attuali lasciano un rango di margine ovunque.
>
> `TalentTests.Every_branch_can_be_finished` compra tutto il comprabile a ripetizione e
> pretende che nessun nodo resti indietro: e' l'unica forma di test che se ne sarebbe accorta,
> perche' il totale del listino (114 punti) non dice niente su quanto se ne riesca a comprare.

### Ramo Borsa — 37 punti

L'economia della run: oro iniziale, budget della forgia, mercante.

| Tier | Nodo | Ranghi | Costo/rango | Effetto |
| --- | --- | --- | --- | --- |
| 1 | Fondo di viaggio | 5 | 1 | +2/4/6/8/10 oro iniziale |
| 2 | Forgia generosa | 5 | 2 | +3/6/9/12/15 al budget della forgia (`StartingEssence`) |
| 2 | Mercante compiacente | 2 | 3 | −10%/−20% su `CardCost` e `UpgradeCost` |
| 3 | Tempra del fabbro | 2 | 5 | a forgia conclusa, 1/2 carte a caso del mazzo +1 forza |
| 4 | Primo affare | 1 | 6 | il primo upgrade comprato dal mercante e' gratis |

Note di taratura:

- **+1 oro non si vede.** `MerchantEconomy.MonsterRoomGold` paga 6-20 a stanza e una carta
  costa 12-36 (`CardCost`). Lo scalino minimo percepibile e' 2.
- **Lo sconto sulla forgia va sul budget, non sul prezzo unitario.** Con
  `chosenClassCost = 7` su 9 acquisti, uno sconto di −5 a carta vale −45 su un budget di 75:
  +60% di potere d'acquisto, cioe' un altro gioco. `+15` al rango 5 e' +20%, che e' un
  vantaggio reale e non una rottura.
- **Nei testi il budget della forgia non si chiama "essenza".** Il campo in codice si chiama
  `StartingEssence`, ma quella parola non esiste da nessuna parte nel gioco: il talento parla
  di "quanto hai da spendere nella forgia", che e' quello che il giocatore vede.
- **Tempra del fabbro** e' sicura perche' `maximumCopiesPerCard = 1`: non puo' impilarsi
  sulla stessa carta.

### Ramo Iniziativa — 26 punti

`InitiativeDieSides = 20` e la formazione ha `formationSize = 3`: un dado per pedina.
Un +3 su d20 vale +15%, che e' esattamente il pollice sulla bilancia cercato.

| Tier | Nodo | Ranghi | Costo/rango | Effetto |
| --- | --- | --- | --- | --- |
| 1 | Avanguardia | 3 | 1 | +1/2/3 al 1º dado d'iniziativa |
| 2 | Fiancheggiatore | 3 | 2 | +1/2/3 al 2º dado |
| 3 | Retroguardia | 3 | 3 | +1/2/3 al 3º dado |
| 4 | Apertura | 1 | 8 | il 1º dado d'iniziativa batte qualunque numero in campo |

**Trappola implementativa.** `RollUniqueInitiative`
(`BattleBoardController.Combat.cs`) garantisce iniziative **uniche** tra tutti i
combattenti. Sommare il bonus al valore tirato crea collisioni con le iniziative gia'
assegnate. Il bonus va applicato **al momento dell'ordinamento**, non al tiro.

> **Il capstone era "Colpo d'anticipo" e vinceva le parita' d'iniziativa.** Le parita' non
> esistono: e' la stessa `RollUniqueInitiative` di due righe fa a garantirlo. Il nodo era
> comprabile e inerte — il ramo del `TieBreaker` non veniva percorso mai — cioe' esattamente
> la cosa che questo documento dice di non fare. **Apertura** non aspetta piu' un caso che
> non arriva: si mette davanti a tutti nell'ordinamento, punto.

Il dado mostrato a schermo resta il tiro nudo, con il bonus indicato a parte: mentire sul
numero del dado e' il modo piu' rapido per far sospettare che il gioco bari.

### Ramo Maestria — 25 punti

Il livello maestro della run guida `vigorDiceByLevel = { 4, 6, 8, 10, 12, 20 }`: il dado
vigore passa da d4 a d20 lungo 6 livelli. E' la curva di potenza piu' ripida che esista
dentro una run, e accorciarla di poco si sente moltissimo.

Soglie attuali (`experienceThresholdsByLevel`): 50, 75, 100, 125, 150 — 500 exp per
arrivare al livello 6.

| Tier | Nodo | Ranghi | Costo/rango | Effetto |
| --- | --- | --- | --- | --- |
| 1 | Apprendista | 5 | 1 | −2%/4%/6%/8%/10% su tutte le soglie di livello |
| 2 | Concentrazione | 2 | 2 | +1/+2 mana a ogni cambio stanza |
| 3 | Riserva | 2 | 4 | +1/+2 al tetto della riserva (10 → 12) |
| 4 | Trance | 1 | 8 | la prima abilita' **base** di ogni stanza non costa mana |

**Trance non vale sulle supreme.** Si aggancia a `TrySpendCampaignPrimaryMana`, che paga solo
l'abilita' base; le supreme si pagano in `CampaignSupreme.cs`, percorso separato. Il testo lo
dice esplicitamente, perche' "abilita' di classe" comprende anche quelle.

**Riserva cambia le regole, non la riserva.** Il tetto vive in `ManaRules.Maximum` e
`Gain`/`RaiseTo`/`Restore` ci tagliano tutti sopra, compresa la barra a schermo: percio' il
talento ricostruisce `campaignPlayerMana` con `ManaRules.WithMaximum` all'inizio della run e
alla ripresa di un salvataggio, invece di tenere un tetto "vero" e uno "mostrato".

> **Il ramo era fatto di quattro sconti sulle soglie e adesso ne ha uno.** Quattro nodi che
> spingono tutti sulla stessa leva non cambiano la run, la accorciano: sommati portavano il
> d20 troppo avanti e il resto della campagna diventava una discesa. Resta il piu' blando,
> l'Apprendista.
>
> **Gli altri tre stanno sul mana e non sui totali di combattimento.** Un primo tentativo li
> aveva scritti come bonus di Potenza — in difesa, contro bersagli piu' grossi, all'ultima
> carta rimasta — ed era la strada sbagliata: il combattimento confronta numeri piccoli, e un
> talento che ne somma altri sposta l'esito di ogni singolo scontro invece di dare al
> giocatore qualcosa da usare. Il mana cambia **quanto spesso** puoi giocare le abilita'
> senza toccare chi vince il confronto.
>
> Ogni nodo tocca una cosa diversa — quanto ne recuperi, quanto ne tieni, quanto ne spendi —
> cosi' il ramo non e' lo stesso effetto tre volte con la cifra piu' grossa. I ganci sono uno
> per nodo e tutti a sito singolo: `BeginCampaignRoomMana`, `RebuildPlayerManaPool` e
> `TrySpendCampaignPrimaryMana`.

**L'unico nodo rimasto sulle soglie non regala exp: abbassa il traguardo.** Non e' una scelta
estetica, e' il modo in cui si rompe il loop descritto qui sotto.

### Ramo Occasioni — 26 punti

I condizionali, che sono quelli che il giocatore si ricorda.

| Tier | Nodo | Ranghi | Costo/rango | Effetto |
| --- | --- | --- | --- | --- |
| 1 | Recupero | 2 | 2 | `RecoveryCost` −10%/−20% |
| 2 | Sfidante | 2 | 3 | +1/+2 di Potenza sul primo attacco contro un boss o un miniboss |
| 3 | Cercatore | 2 | 4 | ogni stanza bottino consegna 1/2 consumabili in piu' |
| 4 | Secondo fiato | 1 | 8 | la prima pedina persa in ogni run non va al cimitero: torna nel mazzo |

> **Due nodi cambiati in fase di scrittura**, perche' la meccanica che il progetto dava per
> esistente non esisteva.
>
> - **Cercatore** doveva dare carte di riserva. La stanza bottino non consegna carte:
>   consegna un consumabile (`GrantRandomConsumable`), e `lootReserveCards` in configurazione
>   non lo legge nessuno. Il nodo ora aggiunge consegne di consumabili, che e' la stessa
>   fantasia sulla meccanica che c'e' davvero.
> - **Ultima parola** doveva concedere un ritiro del tiro di vigore. Vorrebbe una richiesta
>   interattiva dentro la coroutine dei dadi - una UI nuova, in mezzo a un'animazione, su
>   ogni ramo di ogni boss. Il nodo e' diventato **Secondo fiato**, che si aggancia allo
>   stesso punto gia' toccato dal Recupero e chiude il ramo con un effetto verificabile.
>   Vendere un talento inerte sarebbe stato peggio che non venderlo.
>
> **Secondo fiato e' stato poi rifatto una seconda volta.** Agganciarlo al recupero al
> mercato lo rendeva una versione piu' piccola del nodo che apre il ramo: Recupero costa 4
> propoli in tutto e sconta *ogni* recupero della run, il capstone ne costava 8 e ne copriva
> *uno*. Un capstone che nessuno comprerebbe, se potesse comprarlo. Adesso la pedina non
> arriva nemmeno al cimitero — il gancio e' `CampaignDeckState.CompleteCombat`, l'unico punto
> in cui una carta sconfitta cambia zona — e i due nodi smettono di contendersi la stessa leva.
>
> **Sfidante** e' passato dal tiro di vigore alla Potenza dell'attacco: il tiro nudo si
> risolve dentro il motore, la Potenza no. Passa da `BuildAttackModifiers`, che e' il collo
> di bottiglia di ogni attacco compresi quelli dei boss, e si consuma una volta per scontro.

## Il loop da rompere

`exp account = exp di run / 10`. Un talento che aggiunge exp **dentro** la run aggiunge
quindi exp all'account, che da' livelli, che danno punti, che comprano piu' exp:

```
+% exp in run  →  +exp account  →  +livelli  →  +punti  →  +% exp in run
```

E' un anello aritmetico chiuso e senza freni interni. Tre conseguenze, tutte cattive:

1. il ramo Maestria diventa **obbligatorio**, perche' e' l'unico che si ripaga da solo, e
   gli altri tre diventano decorativi
2. il bilanciamento non e' verificabile a mano: l'effetto di un rango dipende da quanti
   ranghi sono gia' stati presi
3. il tetto `DeathRewardExperienceCeiling = 5000` e' l'unica cosa che lo contiene, cioe' il
   sistema regge per una ragione che non c'entra niente con il suo progetto

### Come si rompe: per costruzione

**Regola invariante: nessun talento puo' aumentare l'esperienza guadagnata in una run.**

Tutti i nodi Maestria agiscono sulle **soglie di livello**, non sull'exp incassata. Il
giocatore sale prima di livello — che e' la fantasia che voleva — ma `TotalExperience` a
fine run e' identica a quella di chi non ha nessun talento. L'anello e' tagliato dove
nasce, non contenuto a valle.

Questa e' la ragione per cui "Apprendista" e' `−10% alle soglie` e non `+10% exp`, e per
cui "Slancio" e' `soglia del livello 2 dimezzata` e non `parti con 25 exp`. Le due
formulazioni si assomigliano, ma solo una e' chiusa.

### La rete di sicurezza, se un giorno servisse

Se in futuro un talento dovesse davvero regalare exp (per esempio un bonus di fine
capitolo), non basta ricordarsene: serve che il codice lo sappia.

`RunProgressState` traccia allora un contatore separato `TalentExperience`, alimentato solo
dalle fonti-talento, e il sommario di fine run manda

```
matchExperience = TotalExperience - TalentExperience
```

`TotalExperience` resta lo storico lordo per la UI e per i contatori della taverna; il
server riceve la sola exp guadagnata giocando. Il costo e' un campo in piu' e una
sottrazione, e va scritto **insieme** al primo talento che regala exp, mai dopo.

### Cosa invece va bene che si autoalimenti

Un talento della Borsa rende il mazzo migliore, il mazzo migliore fa sopravvivere piu'
stanze, piu' stanze danno piu' exp. E' un anello anche quello, ma passa attraverso il
**gioco**: lo chiude il giocatore sopravvivendo, non l'aritmetica, ed e' limitato da quanto
lontano si riesce ad arrivare. E' il motivo per cui i talenti esistono. Il loop da
rompere e' solo quello che si chiude dentro una formula.

## Modello dati

### Colonne nuove su `single_player_progress`

```sql
talent_points        INTEGER NOT NULL DEFAULT 0  -- punti disponibili, non spesi
talent_points_earned INTEGER NOT NULL DEFAULT 0  -- storico lordo, per la UI e l'admin
```

Entrambe via `AddColumnIfMissing`, come le colonne aggiunte finora.

`pending_level_rewards` non cambia forma: continua a contare i livelli non riscossi,
`ClaimLevelRewards` smette di convertirli in miele e li converte in
`talent_points`/`talent_points_earned`.

### Tabella nuova

```sql
CREATE TABLE IF NOT EXISTS player_talents (
    player_id  TEXT NOT NULL,
    talent_id  TEXT NOT NULL,
    rank       INTEGER NOT NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (player_id, talent_id),
    FOREIGN KEY (player_id) REFERENCES accounts(player_id)
);
```

Una riga per nodo posseduto, con il rango raggiunto. Niente storico degli acquisti: senza
respec la riga e' gia' la storia.

### Catalogo

`Server/AccardND.Server/Progression/TalentCatalog.cs`, sullo stampo di `SanctuaryCatalog`:
statico, sorgente unica, il client riceve voci **gia' valutate** (posseduto/comprabile/
bloccato e perche') e si limita a disegnarle.

```csharp
public sealed record Talent(
    string Id,
    string Branch,      // "purse" | "initiative" | "mastery" | "occasion"
    int Tier,           // 1..4
    string Name,
    string Description,
    int MaxRank,
    int CostPerRank,
    int[] Values);      // valore dell'effetto per rango
```

I valori degli effetti stanno qui e scendono al client dentro il pacchetto di inizio run:
duplicarli sul client li farebbe divergere al primo ritocco di bilanciamento, che e'
esattamente l'errore gia' commesso e corretto sui costi del Santuario.

### Protocollo

Tre messaggi nuovi in `MessageTypes`, sullo stampo di `Sanctuary*`:

```
talents.get       →  talents.data      catalogo + ranghi + punti disponibili
talents.buy       →  talents.data      acquisto di un rango, server-authoritative
talents.loadout   →  (in progress.data) pacchetto modificatori a inizio run
```

L'acquisto valida sul server: punti sufficienti, tier aperto, rango non gia' massimo. Il
client non calcola prezzi.

Il **pacchetto modificatori** di inizio run e' uno struct piatto — oro iniziale, essenza
iniziale, sconti mercante, bonus iniziativa per slot, sconti soglie, flag dei condizionali
— che il client innesta in `DeckBuildingRules`, `RunProgressState` e nel giro
dell'iniziativa. Un solo punto di ingresso: se domani un talento cambia, cambia il campo
del pacchetto e nient'altro.

Il combattimento single player resta client-side e non e' pienamente validabile, ma
**l'acquisto** deve stare sul server, come gli unlock.

## Schermata

L'albero vive **sotto il profilo**, non nel Santuario: il Santuario converte campagna in
contenuto, l'albero converte livello in potenza, e mescolarli renderebbe illeggibili
entrambe le valute.

Impianto visivo: **favo esagonale**, non albero. Le celle di un favo sono la metafora
giusta per una progressione a caselle adiacenti, legano propoli e miele senza spiegazioni,
e danno una cornice di nodo unica e riconoscibile invece di un ennesimo riquadro.

- quattro settori del favo, uno per ramo, con l'emblema al centro
- ogni nodo e' una cella esagonale con tre stati: bloccata, disponibile, al massimo
- i collegamenti tra celle sono tracciati proceduralmente (`MmoUiTheme` gia' disegna
  bottoni e cornici a runtime): niente asset per le linee
- badge sul bottone del profilo quando ci sono punti non spesi, che e' l'unico richiamo
  necessario perche' il giocatore ci torni

## Stato: implementato

Tutto quello che segue e' scritto e verificato (893 test sul server). Restano fuori le icone
dei singoli nodi, che aspettano gli asset, e due cose note: **Primo affare non ha una cella
nel favo** (la Borsa ha 5 nodi e il reticolo ne disegna 4) e i testi dell'albero non passano
dalla localizzazione, quindi restano in italiano in ogni lingua.

**Quando si ritira un nodo** vanno fatte tre cose insieme, o se ne dimentica una: togliere la
riga dal catalogo, aggiungere id e costo a `RemovedTalentRefundMigration.RetiredTalents` con
una chiave di migrazione nuova, e cancellare l'icona in `Resources/UI/ProfileTalents`. Senza
il rimborso chi aveva investito nel nodo resta con meno propoli di chi non l'aveva comprato;
senza la cancellazione dell'icona resta un PNG che non carica piu' nessuno.

| Pezzo | Dove |
| --- | --- |
| Curva condivisa client/server | `Assets/_Project/Scripts/GameCore/AccountLevelCurve.cs` |
| Moltiplicatore di capitolo | `ChapterCatalog.AccountExperiencePercent` |
| Catalogo dei 17 nodi | `Server/AccardND.Server/Progression/TalentCatalog.cs` |
| Acquisto e pacchetto modificatori | `Server/AccardND.Server/Progression/TalentService.cs` |
| Punti retroattivi a chi ha gia' livelli | `TalentPointsBackfillMigration.cs` |
| Rimborso dei nodi ritirati | `RemovedTalentRefundMigration.cs` |
| Punti del primo boss di capitolo | `SinglePlayerProgressService.ClearChapter` |
| Conti degli effetti, testabili senza scena | `Assets/_Project/Scripts/GameData/TalentRunModifiers.cs` |
| Effetti nella run | `BattleBoardController.Talents.cs` |
| Schermata a favo | `BattleBoardController.Profile.cs`, scheda TALENTI |

Tre cose sono emerse dal codice e sono state corrette insieme al resto:

1. `TrySpendExperience` e `AddSpendableExperience` non li chiamava nessuno, quindi
   `AvailableExperience` non scendeva mai: i commenti che parlavano di "exp non spesa al
   mercato" sono stati riscritti.
2. `accountExperienceToNextLevel` era letto come "quanta ne manca" dalla pagina statistiche
   e come "quanta ne serve" dal pannello admin. Vale la seconda, ed e' ora ricalcolato dal
   livello a ogni lettura: le righe scritte prima della curva hanno tutte 100 in tabella.
3. La curva era riscritta a mano in tre posti (`SinglePlayerProgressService`,
   `MatchResultRecorder`, `LocalSinglePlayerProgressRepository`). Ora e' una sola.

I talenti una-tantum (Primo affare, Secondo fiato) sono salvati nella run: senza,
uscire e riprendere la partita li riarmava, e "una volta per run" diventava una volta per
ogni riapertura del gioco.

## Fasi di implementazione

**Fase 1 — la curva.** `XpToNext` e moltiplicatore di capitolo, ads a ×2, commenti
bugiardi su `AvailableExperience` corretti. Due costanti, un campo su `Chapter`, nessuna
tabella nuova. Si sente dalla prima run e vale da sola.

**Fase 2 — i punti.** Colonne `talent_points`, `ClaimLevelRewards` che accredita punti al
posto del miele, punti visibili sul profilo e nel pannello admin. Ancora nessun albero:
il giocatore accumula una valuta che a breve avra' dove andare.

**Fase 3 — un ramo solo, completo.** `TalentCatalog` con il solo ramo **Borsa**, tabella
`player_talents`, i tre messaggi, il pacchetto modificatori innestato in
`DeckBuildingRules` e nell'oro iniziale, la schermata a favo con un settore. E' qui che
saltano fuori i problemi veri, e conviene che saltino fuori su cinque nodi.

**Fase 4 — gli altri tre rami.** A quel punto sono righe di tabella, piu' i due
agganci non banali: l'ordinamento dell'iniziativa e le soglie di maestria.

## Punti aperti

- **Primo boss e punti.** I +3 per prima uccisione premiano anche i boss gia' battuti prima
  che i talenti esistano? `campaign_runs` conserva `defeated_boss_ids`, quindi si
  potrebbe accreditare a ritroso. Accreditare e' piu' generoso e piu' semplice da
  spiegare; non accreditare da' a chi torna una ragione per rigiocare i capitoli.
- **Tetto dei ranghi sull'iniziativa.** +3 su tutte e tre le pedine e' +45% di iniziativa
  complessiva. Va guardato con i numeri veri di una run prima di confermarlo: se
  l'iniziativa smette di essere un tiro, il cap scende a +2 per pedina.
- **Hardcore.** Fuori da questo documento per decisione presa. Quando rientrera' andra'
  deciso se i talenti valgono li' dentro, perche' altrimenti la classifica diventa una
  classifica di talenti.
- **Il cap a 5000.** Con il moltiplicatore di capitolo il tetto effettivo sale a 1250 exp
  account per run. Regge la curva fino al livello ~50; oltre, va rivisto il cap o
  accettato che i livelli alti richiedano molte run, che e' probabilmente giusto.
