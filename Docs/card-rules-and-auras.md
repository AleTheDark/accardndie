# Accard N' Die - Regole carte, abilita e aure

## Struttura del combattimento

- Il mazzo iniziale contiene 9 carte casuali.
- Prima di un combattimento si forma una mano da 6 carte disponibili.
- Da quella mano si schierano 3 carte.
- Le carte nel cimitero non sono disponibili per mano e schieramento.
- Le carte non schierate possono restare come riserva per effetti futuri.

Ogni carta schierabile ha:

- valore da 2 a 10;
- classe;
- famiglia;
- abilita di classe.

## Famiglie e matchup

| Famiglia | Classi |
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

## Abilita di classe

| Classe | Famiglia | Abilita |
| --- | --- | --- |
| Warrior | Might | Una volta per combattimento, nel prossimo attacco somma due dadi Vigore invece di tirarne uno solo. |
| Barbarian | Might | Se attacca e non elimina il bersaglio, ottiene Furia: +2 in attacco e difesa. |
| Paladin | Might | Protegge un alleato: quando quell'alleato viene attaccato, il Paladin prende il suo posto come bersaglio. |
| Rogue | Cunning | In attacco rilancia tutti gli 1 sul proprio dado Vigore, anche con vantaggio o svantaggio. |
| Assassin | Cunning | Inibisce una carta nemica: quella carta salta il prossimo turno. |
| Hunter | Cunning | Marca un nemico. Chi lo attacca prende +2; se lo attacca un Hunter prende +4. |
| Mage | Magic | Abbassa di uno step il dado Vigore del nemico scelto per il prossimo tiro. |
| Necromancer | Magic | Rialza una carta alleata eliminata. |
| Priest | Magic | Benedice un alleato: +2 al prossimo attacco. |

Scala del Vigore di base:

`D4 -> D6 -> D8 -> D10 -> D12 -> D20`

Scala del Vigore usata dal Mage quando abbassa il dado:

`D3 -> D4 -> D6 -> D8 -> D10 -> D12 -> D20`

Abbassare di uno step significa:

- D20 diventa D12.
- D12 diventa D10.
- D10 diventa D8.
- D8 diventa D6.
- D6 diventa D4.
- D4 diventa D3.
- D3 resta D3.

## Tipologie di aura

Con 3 carte schierate puo attivarsi una sola aura.

Priorita:

1. Se le 3 carte sono della stessa classe, si attiva l'Aura di Classe.
2. Altrimenti, se le 3 carte sono della stessa famiglia, si attiva l'Aura di Famiglia.
3. Altrimenti, se sono presenti 1 Might, 1 Cunning e 1 Magic, si attiva l'Aura di Formazione.

L'Aura di Classe sostituisce l'Aura di Famiglia: non si sommano.

## Aura di Formazione

Condizione:

- 1 Might + 1 Cunning + 1 Magic.

Effetto:

- Una volta per combattimento, quando una tua carta avrebbe svantaggio di famiglia, lo svantaggio diventa neutro.

Identita: flessibilita, copertura, sicurezza.

## Aure di Famiglia

### Aura Might

Condizione:

- 3 carte Might non tutte della stessa classe.

Effetto:

- La prima volta per round che una tua carta Might non elimina il bersaglio, ottiene +1 permanente per il resto del combattimento.
- Il +1 vale sia in attacco sia in difesa.

Identita: pressione crescente, resistenza, combattimento lungo.

### Aura Cunning

Condizione:

- 3 carte Cunning non tutte della stessa classe.

Effetto:

- Una volta per round, quando una tua carta Cunning attacca un nemico con un malus, una marca o un'inibizione, tira con vantaggio anche se il matchup sarebbe neutro o sfavorevole.

Identita: preparare il bersaglio, sfruttare debolezze, colpire nel momento giusto.

### Aura Magic

Condizione:

- 3 carte Magic non tutte della stessa classe.

Effetto:

- Le carte Magic alleate aumentano il dado difesa di 1 step.

Identita: manipolazione progressiva del campo.

Nota implementativa attuale: finche non esiste una UI di scelta dedicata, l'effetto applica automaticamente -1 permanente al nemico vivo piu forte.

## Aure di Classe

### 3 Warrior

- Quando un Warrior usa somma dadi, aggiunge +1 al totale.

### 3 Barbarian

- Furia diventa +3 invece di +2, sia in attacco sia in difesa.

### 3 Paladin

- Quando un Paladin protegge un alleato e sopravvive, contrattacca subito l'attaccante con +1 in attacco.

### 3 Rogue

- I Rogue rilanciano gli 1 in attacco anche quando tirano due dadi per vantaggio o svantaggio.

### 3 Assassin

- Quando un Assassin inibisce un nemico, quel nemico subisce anche -1 permanente.

### 3 Hunter

- La marca da +3.
- Se ad attaccare il bersaglio marcato e un Hunter, il bonus diventa +5.

### 3 Mage

- La prima abilita Mage applicata abbassa il dado avversario di 2 step invece di 1.

Nota implementativa attuale: ogni Mage con aura Mage applica 2 step al nemico scelto.

### 3 Necromancer

- La prima volta per combattimento che un alleato viene eliminato, resta come Spirito.
- Lo Spirito ottiene un ultimo turno.
- Nel suo ultimo turno puo attaccare, usare abilita o diventare attachment.
- Alla fine del turno muore.

### 3 Priest

- Benedizione da +3 invece di +2.
