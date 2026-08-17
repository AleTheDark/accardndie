# Accard N' Die - Regole carte, abilita e aure

> **Fonte.** I numeri di questo documento sono stati riallineati al codice il 2026-08-10.
> Dove codice e documento divergessero, vince il codice:
>
> - costi e supreme: `Assets/_Project/Scripts/GameCore/Mana/` (`ManaRules`, `AbilityManaCosts`, `SupremeAbilityText`);
> - scala del Vigore e aure: `GameCore/Pvp/PvpAura.cs`, `GameCore/Pvp/PvpMatchEngine.cs`;
> - testi mostrati in partita: `Scripts/Localization/Editor/ItalianGameTextCatalog.cs`;
> - versione campagna delle stesse regole: `Scripts/Presentation/BattleBoardController.Combat.cs`.
>
> Le stesse regole sono divulgate al pubblico in `Docs/web/guida.html` e `Docs/web/classi.html`:
> se cambia qualcosa qui, vanno aggiornate anche quelle due pagine a mano.

## Struttura del combattimento

- Il mazzo iniziale contiene 9 carte casuali.
- Prima di un combattimento si forma una mano da 6 carte disponibili.
- Da quella mano si schierano 3 carte.
- Le carte nel cimitero non sono disponibili per mano e schieramento.
- Le carte non schierate possono restare come riserva per effetti futuri.

Ogni carta schierabile ha:

- valore da 2 a 10;
- classe;
- fazione;
- abilita di classe, che costa mana e si puo' usare una volta per turno;
- abilita suprema, che costa mana e va sbloccata al Santuario.

## Fazioni e matchup

| Fazione | Classi |
| --- | --- |
| Might | Warrior, Barbarian, Paladin |
| Cunning | Rogue, Assassin, Hunter |
| Magic | Mage, Necromancer, Priest |

Matchup:

- Might batte Cunning.
- Cunning batte Magic.
- Magic batte Might.

Nel matchup favorevole si tirano due dadi e si tiene il migliore.
Nel matchup sfavorevole si tirano due dadi e si tiene il peggiore.
Nel matchup neutro si tira un dado solo.

## Il Vigore: la scala dei dadi

Scala unica, valida per tutti (`PvpVigorScale`):

`D2 -> D4 -> D6 -> D8 -> D10 -> D12 -> D20`

Abbassare di uno step significa:

- D20 diventa D12.
- D12 diventa D10.
- D10 diventa D8.
- D8 diventa D6.
- D6 diventa D4.
- D4 diventa D2.
- D2 resta D2: e' il pavimento.

Alzare di uno step percorre la stessa scala al contrario, e dal D2 si risale al D4.

Il dado di partenza non dipende dalla carta ma dalla progressione:

- **campagna**: dal livello della run, `vigorDiceByLevel` = D4, D6, D8, D10, D12, D20 dal livello 1 al 6 (`RunProgressState.PlayerVigorDieSides`; il Master sale a parte con le stanze superate);
- **PvP**: dal round, `vigorDieByRound` = D4, D6, D8 dal primo round in poi (`PvpMatchRules`).

Il D2 quindi non e' mai un dado di partenza: ci si arriva solo accumulando malus.

## Mana

Riserva unica del giocatore, condivisa da tutte le sue pedine. Parametri di default in
`ManaRules`:

| Voce | Valore |
| --- | --- |
| Tetto | 10 |
| Riserva a inizio run / match | 3 |
| Pavimento a inizio stanza/round | 2 (chi e' sotto risale a 2) |
| Costo dell'attacco base | 1 |
| Recupero a fine attivazione | +1 |
| Recupero se la pedina salta il turno | +3 (sostituisce il +1) |
| Recupero su parata | +1 |
| Recupero su uccisione / su perdita | 0 |

Il mana e' l'unica risorsa che attraversa stanze e round.

Costo delle abilita di classe (`AbilityManaCosts.Primary`):

| Classe | Costo |
| --- | --- |
| Rogue, Barbarian | 0 - abilita passive, non si attivano |
| Assassin | 3 |
| Hunter, Mage, Paladin, Priest | 2 |
| Necromancer | 4 |
| Warrior | 5 |

Le abilita primarie **non sono mono-uso**: `AbilityUsed` si azzera a inizio turno della pedina,
quindi il limite vero e' il mana. Ogni pedina puo' usarne una per turno.

## Abilita di classe

| Classe | Fazione | Mana | Abilita |
| --- | --- | --- | --- |
| Warrior | Might | 5 | Al prossimo attacco tira il dado Vigore e un dado di uno step inferiore, poi somma i risultati. |
| Barbarian | Might | passiva | Accumula Furia sugli scambi persi (+2 in attacco e in difesa, cumulabili senza limite) e la scarica alla prima vittoria. |
| Paladin | Might | 2 | Alza la guardia: protegge un alleato deviando su di se' l'attacco che lo colpisce, oppure tiene la guardia per se'. Quando la protezione scatta, para con vantaggio. |
| Rogue | Cunning | passiva | In attacco, se il totale non basta a vincere, ritira una volta ogni dado Vigore uscito pari o sotto la soglia del livello. Se sta gia' vincendo non ritira. |
| Assassin | Cunning | 3 | Inibisce una carta nemica: quella carta salta il prossimo turno. |
| Hunter | Cunning | 2 | Marca un nemico. Il prossimo attacco contro quel bersaglio riceve +2, poi tutti i marchi sul bersaglio vengono consumati. Piu' marchi non si sommano. |
| Mage | Magic | 2 | Il dado Vigore del nemico scelto scende di una taglia nel prossimo confronto. I marchi si accumulano, uno step l'uno, fino al minimo di D2. |
| Necromancer | Magic | 4 | Riporta in campo un alleato eliminato, che agisce subito dopo di lui. |
| Priest | Magic | 2 | Purifica tutti i malus da un alleato e gli conferisce +2 al prossimo attacco. Le benedizioni si sommano sulla stessa pedina. |

Soglia di rilancio del Rogue, per dado (`RogueConditionalRerollMaximum`):

| Dado | D4 | D6 | D8 | D10 | D12 | D20 |
| --- | --- | --- | --- | --- | --- | --- |
| Ritira fino a | 1 | 2 | 3 | 4 | 5 | 6 |

La soglia si legge sempre dal dado della progressione, non dal dado effettivo dello scambio:
un malus del Mage abbassa il dado ma non la soglia. In campagna coincide con il livello della
run (D4 al livello 1 = ritira l'1, e cosi' via); in PvP coincide con il numero del round,
perche' `PvpMatchEngine` la ricava da `rules.VigorDieForRound(MatchRound)`:

| Round PvP | 1 | 2 | 3 |
| --- | --- | --- | --- |
| Dado | D4 | D6 | D8 |
| Ritira fino a | 1 | 2 | 3 |

Cosa toglie la purificazione del Priest (`CleanseMaluses`): inibizione in corso e inibizione
subita, gli step di Vigore tolti dal Mage, il bonus permanente se negativo, e il marchio
dell'Hunter puntato su quella pedina. La benedizione si somma al bonus gia' presente; se la
pedina ha Furia resta Furia, quindi continua a valere anche in difesa.

I bonus numerici stanno in `ClassBalanceConfiguration` (`GameConfiguration.asset`):
`barbarianRageBonus` 2, `hunterStrongTargetBonus` 2, `priestBlessingBonus` 2. Le rispettive aure
di classe aggiungono +1 a ciascuno.

## Abilita supreme

Seconda abilita di classe, sbloccata al Santuario, con effetti che scadono a fine stanza. Il
costo base sale di +1 cumulativo per ogni suprema successiva della stessa classe nella stessa
stanza (`SupremeRepeatSurcharge`), e il conto si azzera a inizio stanza.

| Classe | Nome | Mana | Effetto |
| --- | --- | --- | --- |
| Warrior | Potenziamento | 6 | +2 alla Potenza fino a fine stanza, +4 se e' l'unica pedina rimasta. |
| Barbarian | Cornamusa | 4 | Tutta la squadra accumula Furia, con le regole della Furia del Barbarian. |
| Paladin | Riserva | 2 | Se il mana del giocatore e' sotto 6, risale a 6. Nessun effetto in combattimento. |
| Rogue | Scippo | 3 | Ruba al bersaglio un potenziamento e 2 mana. Se non ha potenziamenti, gli ruba 2 di Potenza fino a fine stanza. |
| Assassin | Invisibilita | 5 | Diventa non bersagliabile. Quando resta l'unica pedina torna bersagliabile, ma difende con vantaggio. |
| Hunter | Raffica | 4 | Colpisce tutte le pedine avversarie con un dado Vigore abbassato di uno step. |
| Mage | Palla di Fuoco | 4 | Colpisce tutte le pedine avversarie con un dado Vigore abbassato di uno step. |
| Necromancer | Evoca Sgherri | 5 (stimato) | **Non implementata.** `AbilityManaCosts.IsSupremeImplemented` la esclude apposta: il motore la rifiuta invece di applicare un effetto provvisorio. |
| Priest | Purificazione | 4 | Toglie tutti i malus agli alleati e tutti i potenziamenti agli avversari. Non tocca le aure. |

Mage e Hunter hanno supreme volutamente identiche.

## Tipologie di aura

Con 3 carte schierate puo attivarsi una sola aura.

Priorita:

1. Se le 3 carte sono della stessa classe, si attiva l'Aura di Classe.
2. Altrimenti, se le 3 carte sono della stessa fazione, si attiva l'Aura di Fazione.
3. Altrimenti, se sono presenti 1 Might, 1 Cunning e 1 Magic, si attiva l'Aura di Formazione.

L'Aura di Classe sostituisce l'Aura di Fazione: non si sommano.

## Aura di Formazione

Condizione:

- 1 Might + 1 Cunning + 1 Magic.

Effetto:

- Quando una tua carta attacca in svantaggio di fazione, lo svantaggio diventa neutro.
- Vale solo in attacco, e vale ogni volta: non c'e' limite per combattimento.

Identita: flessibilita, copertura, sicurezza.

## Aure di Fazione

### Aura Might

Condizione:

- 3 carte Might non tutte della stessa classe.

Effetto:

- Ogni volta che una pedina qualsiasi muore, in campo o fuori, tutte le carte Might del
  proprietario dell'aura ancora vive guadagnano +1 permanente per il resto del combattimento.
- Il +1 vale sia in attacco sia in difesa.

Identita: pressione crescente, resistenza, combattimento lungo.

### Aura Cunning

Condizione:

- 3 carte Cunning non tutte della stessa classe.

Effetto:

- Quando una tua carta Cunning attacca un nemico che ha addosso un bonus o un malus - marchio,
  inibizione, malus al Vigore, potenziamento in attesa, bonus permanente diverso da zero - tira
  con vantaggio anche se il matchup sarebbe neutro o sfavorevole.
- Nessun limite per round.

Identita: preparare il bersaglio, sfruttare debolezze, colpire nel momento giusto.

### Aura Magic

Condizione:

- 3 carte Magic non tutte della stessa classe.

Effetto:

- Le carte Magic alleate si difendono con un dado Vigore di uno step superiore.

Identita: manipolazione progressiva del campo.

## Aure di Classe

### 3 Warrior

- Durante un confronto, se la Potenza del Warrior e inferiore a quella dell'avversario, il
  Warrior riceve +2 al totale. Vale in attacco e in difesa.

### 3 Barbarian

- Ogni scarica di Furia vale +3 invece di +2, sia in attacco sia in difesa.

### 3 Paladin

- Quando un Paladin para un colpo e sopravvive, contrattacca subito l'attaccante con +1 in
  attacco.

### 3 Rogue

- Il rilancio condizionale del Rogue vale anche in difesa: se il totale non basta a resistere,
  ritira i dadi usciti pari o sotto la soglia del livello.

### 3 Assassin

- Quando un Assassin inibisce un nemico, quel nemico subisce anche -1 permanente.

### 3 Hunter

- Il prossimo attacco contro un bersaglio marcato riceve +4 invece di +2.
- Dopo l'attacco tutti i marchi sul bersaglio vengono consumati, tranne sui boss: li' il
  marchio resta attivo.
- Piu marchi sullo stesso bersaglio non si sommano.

### 3 Mage

- Quando un Mage con questa aura muore per un attacco, l'attaccante che lo ha eliminato subisce
  -2 permanente.

### 3 Necromancer

- La prima volta per combattimento che un alleato viene eliminato, resta come Spirito.
- Lo Spirito ottiene un ultimo turno.
- Nel suo ultimo turno puo attaccare, usare abilita o diventare attachment.
- Alla fine del turno muore.

### 3 Priest

- La benedizione da +3 invece di +2.

## Divergenze note fra campagna e PvP

- **Purificazione della benedizione del Priest**: implementata nel motore PvP
  (`PvpMatchEngine.CleanseMaluses`) e descritta cosi' nel catalogo testi, ma il percorso
  campagna (`BattleBoardController.Selection.cs` per il giocatore e `TryUseCpuPriestAbility`
  per la CPU) applica solo il +2 senza togliere i malus. In campagna il cleanse arriva oggi
  solo dalla suprema Purificazione. Da riallineare.
