# Sistema di mana e abilita' supreme

## Obiettivo

Oggi le abilita' di classe sono mono-uso: si attivano una volta e la partita degenera in
"uso l'abilita', poi attacco, poi attacco". Non c'e' nessuna decisione ricorrente.

Il mana (rune) trasforma le abilita' da risorse "usa e dimentica" in scelte che si ripresentano
ogni turno: spendere subito, conservare per il colpo grosso, oppure rinunciare a un'attivazione
per accumulare. In parallelo ogni classe riceve una **suprema**: una seconda abilita' molto piu'
forte, e proporzionalmente piu' costosa.

Principio guida: **il mana e' l'unica cosa che attraversa le stanze**. Tutti i potenziamenti
(buff, malus, bonus permanenti) scadono a fine stanza/round. La riserva no. La riserva e' la
memoria delle decisioni prese prima.

## Stato attuale in codice

Cosa esiste gia' e a cui il sistema si aggancia:

- le 9 abilita' di classe: `ClassAbilityType` (`Assets/_Project/Scripts/GameCore/ClassAbilityType.cs`)
- stato per carta con tutti i campi utili: `PvpCardState` — `AbilityUsed`, `AbilityUsedThisTurn`,
  `AbilityArmed`, `PendingAttackBonus`, `PermanentCombatBonus`, `InhibitedTurns`,
  `PendingVigorStepPenalty`, `IsSpirit`, `MarkedTarget`, `ProtectedAlly`
- risoluzione del combattimento: `CombatResolver.ResolveAttack` e `CombatModifiers`
- valutazione di certezza: `CombatCertaintyCalculator` (`Impossible` / `RollRequired` / `Guaranteed`)
- scala dei dadi: `PvpVigorScale.Lower` / `.Raise` — D3-D4-D6-D8-D10-D12-D20
- aure da composizione della formazione: `PvpAura.Determine`
- parametri di partita: `PvpMatchRules.CreateDefault()` — formazione 3, 2 vite a carta,
  dado vigore **d4 / d6 / d8** per round, bonus Rabbia/Marchio/Benedizione = 2
- `SinglePlayerUnlockType.SecondAbility` esiste gia' lato progressione: le supreme sono le
  "tecniche" sbloccabili al Santuario (vedi `Docs/santuario-design.md`)

Il mana e' implementato in campagna e PvP. La fonte unica delle regole e' nel GameCore:
`ManaRules`, `ManaPool`, `AbilityManaCosts` e `ManaActionPolicy`. Il server PvP e le due
presentazioni consumano queste API; non devono mantenere copie locali di costi, classi
attivabili o ricompense di fine attivazione.

## Economia

| | |
|---|---|
| Titolarita' | globale del giocatore, non della singola pedina |
| Tetto | 10 |
| Inizio run | 3 |
| Inizio stanza/round | se sei sotto 2, risali a 2 |
| Persistenza | fra stanze (campagna) e fra round della stessa partita (PvP). Mai fra partite diverse |

Il mana globale e' una scelta deliberata: le pedine deboli generano, la pedina forte spende.
E' meta' del gusto del sistema.

## Generazione

| Evento | Mana |
|---|---|
| La pedina termina l'attivazione | +1 |
| La pedina **salta** l'attivazione | +3 (al posto del +1, non in aggiunta) |
| La pedina para un colpo | +1, massimo 1 per pedina per round |
| Elimini una pedina avversaria | +1 |
| Perdi una tua pedina | +1 |

Regole di contorno:

- Saltare significa rinunciare all'intera attivazione: niente movimento, attacco o abilita'.
  Lo skip e' **per pedina**, non per giocatore.
- Il mana si ottiene una sola volta per attivazione, indipendentemente da quante azioni
  la pedina compie.
- Il mana si paga **prima** dell'effetto e resta speso anche se il tiro va male. Senza questa
  regola le abilita' che manipolano i dadi diventerebbero scommesse senza rischio.
- Le abilita' passive non consumano mana.
- Il "+1 quando perdi una pedina" e' anti-valanga: chi perde una pedina perde anche un
  generatore di mana per i turni successivi, il compenso e' dovuto.
- Le evocazioni non generano mana (vedi Questioni aperte, Necromante).

## Costo delle abilita'

Fasce:

- **prime abilita'**: 1-3
- **supreme**: 3-5

### Formula per valutare una nuova abilita'

```
costo = base categoria (attacco 2 / buff-malus 2 / equipaggiamento 2)
  +1  colpisce piu' bersagli o tutta la squadra
  +1  l'effetto persiste oltre il turno
  +1  rende l'esito certo (porta il confronto a `Guaranteed`)
  +1  nega un'attivazione avversaria o ne restituisce una tua
  -1  richiede una condizione (bersaglio gia' ferito, posizione, ecc.)
  -1  agisce solo su un alleato senza toccare il turno corrente
```

Il costo non dipende solo dal danno. Pesano quantita' e affidabilita' del danno, numero di
bersagli, controllo prodotto, raggio, durata, necessita' di preparazione, possibilita' di
reazione avversaria, e soprattutto **economia delle azioni**: un'abilita' che fa perdere
un'attivazione al nemico vale piu' di una che infligge molto danno.

### Sovrapprezzi

C'e' **un solo** modificatore del costo base, e riguarda solo le supreme:

**Ripetizione di classe** — ogni suprema successiva della stessa classe nello stesso
stanza/round costa +1 **cumulativo** (4 -> 5 -> 6). Si azzera a inizio stanza/round.
Vale per classe, non per pedina: due Maghi condividono l'escalation.

Ordine di calcolo:

```
costo abilita' base = base                              (fisso, non sale mai)
costo suprema       = base + ripetizione di classe
```

Esempio: il Mago usa `WeakenEnemyVigor` (2, sempre 2) e poi la Palla di fuoco, ed e' la
seconda suprema di Mago del round -> 4 + 1 = **5**.

**Le abilita' base non hanno escalation**: usarne una non alza il prezzo di nessun'altra
abilita', ne' della stessa pedina ne' del resto della squadra, e non alza il costo delle
supreme. Il mana e' una cassa comune, ma il listino no: l'unica cosa che ricorda le azioni
gia' fatte e' il contatore delle supreme per classe, che si azzera a inizio stanza/round.
Esisteva un secondo sovrapprezzo detto "catena" (+1 sulla seconda spesa della stessa
attivazione): e' stato rimosso perche' faceva sembrare che le abilita' normali si
incarissero a vicenda dentro la squadra.

### Limiti per attivazione

Per attivazione una pedina puo' usare **1 abilita' non-d'attacco + 1 azione d'attacco**
(attacco normale o abilita' d'attacco). Senza questo limite una pedina con abbastanza mana
incatenerebbe buff su buff.

Non esiste un tetto fisso di spesa per attivazione: con supreme da 5 renderebbe alcune
combinazioni semplicemente illegali invece che costose.

## Prime abilita'

| Classe | `ClassAbilityType` | Categoria | Costo |
|---|---|---|---|
| Ladro | `RerollOne` | modificatore d'attacco | 1 |
| Cacciatore | `MarkTarget` | preparazione | 1 |
| Sacerdote | `BlessAlly` | buff alleato | 1 |
| Barbaro | `GainRage` | buff su se' | 2 |
| Assassino | `InhibitEnemy` | malus | 3 |
| Mago | `WeakenEnemyVigor` | malus | 2 |
| Paladino | `ProtectAlly` | difensiva | 2 |
| Guerriero | `DoubleVigorSum` | attacco | 3 |
| Necromante | `RaiseDefeated` | evocazione | 4 |

Il Guerriero sta a 3 perche' `DoubleVigorSum` e' l'unica abilita' base che sposta sia il minimo
sia il massimo del tiro: su d6 porta il range da 1-6 a 2-10 (tira d6 + d4 via `PvpVigorScale.Lower`).
E' il pezzo che rende letali tutte le catene.

Il Necromante sta a 4 perche' `RaiseDefeated` vale un'attivazione intera recuperata, non e'
un buff.

## Supreme

Sbloccabili al Santuario (`SinglePlayerUnlockType.SecondAbility`). Tutti gli effetti
**scadono a fine stanza/round**.

| Classe | Suprema | Costo |
|---|---|---|
| Guerriero | +2 alla Potenza; +4 se e' l'unica pedina rimasta | 3 |
| Ladro | Ruba tutti i buff del bersaglio; se non ne ha, -2 Potenza | 3 |
| Mago | Palla di fuoco: colpisce tutti i nemici con un dado di vigore in meno | 4 |
| Cacciatore | Volley: colpisce tutti i nemici con un dado di vigore in meno | 4 |
| Barbaro | Cornamusa: buffa tutto il party per il round | 4 |
| Paladino | Riserva: se il mana del giocatore e' inferiore a 6, sale a 6 | 2 |
| Sacerdote | Toglie i malus dagli alleati e i buff dai nemici, simultaneamente. Non agisce sulle aure | 4 |
| Assassino | Invisibilita': non targettabile, non decade. Quando resta l'ultimo torna targettabile con vantaggio in difesa | 5 |
| Necromante | Evoca sgherri | da definire |

Note:

- Il bonus "+4 se e' l'unica pedina rimasta" del Guerriero **non e' cumulabile con
  `DoubleVigorSum` nella stessa attivazione**.
- Ladro e Sacerdote sono carte di risposta: valgono molto contro una squadra che ha buffato,
  poco o nulla contro una che non l'ha fatto. Per questo stanno sotto la fascia alta.
- L'Assassino e' la piu' forte della lista: immunita' totale piu' offesa piena pagata con una
  sola attivazione. Il suo contro esiste ed e' il Dispel del Sacerdote (vedi sotto).
- Le supreme AoE colpiscono l'intera formazione avversaria (3 carte), con il dado abbassato
  di uno step: round 1 d4 -> d3, round 2 d6 -> d4, round 3 d8 -> d6.
- Il Paladino e' **l'unica suprema sotto la fascia 3-5**, ed e' l'unica che non produce alcun
  effetto in combattimento. E' deliberato: il Paladino non e' una classe di scontro, e' la
  classe che regge l'economia. La sua forza si vede nelle supreme che permette agli altri.

### Riserva del Paladino

> **Costo 2.** Dopo il pagamento, se il mana del giocatore e' inferiore a 6, sale a 6.

E' l'unica fonte di mana che non passa dalle attivazioni. La forma a **soglia** (non "riempi al
massimo") e' deliberata: rende massimo il valore quando sei a secco e nullo quando stai bene,
e non puo' finanziare un turno di scarico.

| Mana prima | Dopo | Netto |
|---|---|---|
| 2 | 6 | +4 |
| 3 | 6 | +3 |
| 4 | 6 | +2 |
| 5 | 6 | +1 |
| 6+ | invariato | -2 |

Sopra 6 e' una perdita secca, quindi si autoregola senza clausole aggiuntive. Dopo il recupero
di fine attivazione il Paladino chiude a 7 — non abbastanza per una doppia AoE (che ne chiede 9).

**Non serve un limite d'uso**: il +1 cumulativo di ripetizione di classe la spegne da solo. Il
guadagno netto massimo scende a ogni lancio (costo 2 -> +4, costo 3 -> +3, costo 4 -> +2,
costo 5 -> +1, costo 6 -> 0) perche' il tetto di arrivo resta sempre 6. Converge a zero senza
bisogno di una clausola separata. In pratica il limite vero e' l'attivazione: con un solo
Paladino in formazione si lancia comunque una volta per round.

La Riserva e' un'abilita' non-d'attacco, quindi il Paladino puo' lanciarla e poi attaccare
normalmente nella stessa attivazione, pagando l'attacco al prezzo pieno di listino.

**Caso limite noto**: sotto 2 di mana non e' lanciabile. Il pavimento di 2 a inizio stanza/round
lo copre in apertura, ma a meta' round si puo' restare tagliati fuori. Variante di riserva se in
playtest da' fastidio: **costo 0, ma la pedina rinuncia al +1 di fine attivazione** — sopra la
soglia resta comunque una perdita, e non c'e' nessuna soglia minima per lanciarla.

## Buff rimovibili

Serve una lista chiusa, perche' due supreme ci lavorano sopra (Dispel del Sacerdote, furto del
Ladro). Sono **buff rimovibili**:

- Cornamusa del Barbaro
- Invisibilita' dell'Assassino
- +2 della suprema del Guerriero
- Benedizione (`BlessAlly`), Rabbia (`GainRage`), Marchio (`MarkTarget`)
- la protezione del Paladino (`ProtectAlly`)

**Non** sono rimovibili:

- le aure (`PvpAura`), che nascono dalla composizione della formazione e non da una giocata
- il mana gia' generato dalla Riserva del Paladino: una volta salito, resta

Questa singola decisione chiude il triangolo: Sacerdote contro chi buffa, Ladro che ruba invece
di cancellare, chi buffa forte contro chi non ha ne' Sacerdote ne' Ladro.

## Verifiche numeriche

### Ritmo dell'economia

Con formazione da 3 si generano circa **3-5 mana per round** (3 attivazioni piu' parate ed
eliminazioni). Le supreme costano 3-5. Fa **una suprema per round**, che e' il ritmo giusto per
qualcosa che si chiama suprema. Chi ne vuole due nello stesso round deve aver saltato dei turni
prima: e' esattamente la decisione che il sistema vuole creare.

### Il tetto regge il burst

Formazione di tre Maghi che parte con la riserva piena:

- Palla di fuoco: 10 - 4 = 6, poi +1 di recupero -> 7
- seconda (ripetizione di classe, costa 5): 7 - 5 = 2, +1 -> 3
- terza (costa 6): ne ha 3. **Non ci arriva.**

Due supreme della stessa classe nello stesso round e' il massimo raggiungibile, e solo entrando
con la riserva piena. Il triplo AoE non e' in tabella.

Se in playtest la doppia AoE d'apertura in campagna (entri in stanza con 10 in cassa) risulta
troppo secca, la leva e' far salire le supreme AoE di **+2** invece di +1 per ripetizione, non
toccare i costi base.

### Soglia del `Guaranteed`

`CombatCertaintyCalculator` restituisce `Guaranteed` quando `attaccanteMin > difensoreMax`.
E' usato in entrambe le modalita', ma **con effetti diversi**:

- **Campagna** (`BattleBoardController.Combat.cs`): su `Guaranteed` il tiro viene **saltato** e si
  va dritti all'uccisione, con messaggio `GameTextKeys.Combat.GuaranteedKill`.
- **PvP** (`PvpMatchEngine.cs`): i dadi si tirano comunque, perche' serve il numero reale per
  stabilire l'Overkill. `Guaranteed` toglie l'incertezza, non il tiro.

A Forze pari e senza bonus difensivi, il bonus piatto necessario e':

| Dado vigore | Attacco normale | Con `DoubleVigorSum` |
|---|---|---|
| d4 (round 1) | +4 | +3 |
| d6 (round 2) | +6 | +5 |
| d8 (round 3) | +8 | +7 |

Al round 1 servono solo +4, e Rabbia + Benedizione fanno gia' +4 **oggi, senza supreme**. Con
la Cornamusa la soglia si supera comodamente.

Questo e' **accettato come design**: il mana va farmato e vale per entrambi i giocatori. Va
comunque tenuto d'occhio in playtest, perche' il round 1 e' il piu' fragile. Il test automatico
piu' utile da scrivere in fase di implementazione e': enumerare le catene raggiungibili con un
dato budget di mana e verificare quali producono `Guaranteed`.

## Decisioni prese e scartate

Registrate per non rimetterle in discussione:

- **Il `Guaranteed` non e' un problema.** Proposto un tetto ai bonus piatti pari a
  (facce del dado - 2): **scartato**. Il mana va farmato e l'avversario puo' fare lo stesso.
- **Mago e Cacciatore hanno supreme volutamente identiche** (AoE con uno step in meno).
  Proposto di differenziare il Volley agganciandolo a `MarkTarget`: **scartato**.
- **Limite rigido "una suprema per classe per round"**: sostituito dal +1 cumulativo, che
  esprime la stessa spinta dentro il sistema del mana invece che come regola a parte.
- **+1 mana a round per le formazioni mono-classe** (per compensare il budget inutilizzato):
  non piu' necessario, il +1 cumulativo lo risolve da solo.
- **Buff su alleato a costo 1**: scartato, tornato a 2. Il tempo speso per la preparazione
  viene rimborsato in mana dal recupero di fine attivazione, quindi lo sconto sarebbe doppio.
- **Suprema del Paladino "immunita' su se' + 1 a tutti gli alleati"**: **scartata**. La suprema
  del Paladino e' la Riserva; l'abilita' base resta `ProtectAlly`, che esiste gia' in codice.
- **"Fulla il mana" del Paladino**: sostituito dalla soglia a 6. Come riempimento al massimo
  avrebbe reso il mana non piu' una risorsa scarsa e il Paladino obbligatorio in ogni formazione.
- **Limite d'uso sulla Riserva ("una volta per stanza/round")**: non serve, il +1 cumulativo di
  ripetizione la fa convergere a zero da sola.

## Questioni aperte

1. **Suprema del Necromante (evoca sgherri).** Da definire: quanti, con che Forza, quanto durano.
   Raccomandazioni: gli sgherri **non generano mana** (due sgherri = +2 mana a round a vita, e'
   il buco piu' sfruttabile del sistema), non contano nel calcolo di `PvpAura.Determine`
   (che lavora su una formazione di esattamente 3), massimo 2 in campo, scadono a fine
   stanza/round. Costo stimato 5.
2. **Cooldown per le supreme piu' forti.** Con le abilita' riutilizzabili, il costo da solo
   potrebbe non bastare per quelle da 5. L'escalation +1 e' il primo freno; se non basta,
   la leva successiva e' "una volta per stanza" invece di "una volta per round".

## Note di implementazione

Da valutare quando si passa al codice:

- il mana e' stato di **giocatore**, non di carta: va in `PvpMatchEngine` lato PvP e nello stato
  di run lato campagna, non in `PvpCardState`
- il PvP e' server-autoritativo: la validazione del costo e la generazione devono stare lato
  server, il client mostra soltanto
- la persistenza fra stanze in campagna passa dal salvataggio run (`CampaignRunSave`)
- molti effetti hanno gia' un campo dedicato in `PvpCardState`: il +2 del Guerriero ->
  `PermanentCombatBonus`, gli sgherri -> `IsSpirit`, il passo di dado del Mago ->
  `PendingVigorStepPenalty`
- ogni parata riuscita genera mana; non serve stato per carta, resta soltanto il tetto
  massimo della riserva
- serve un contatore di supreme usate per classe nel round, azzerato a inizio stanza/round
