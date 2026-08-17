# Restyling stanza: Sfida Veloce / Quick Challenge

## Obiettivo

La stanza precedentemente chiamata **Imprevisto o Opportunità** diventa **Sfida Veloce** (`Quick Challenge` in inglese): una stanza non-combat nella quale il giocatore affronta un quiz, un gioco di memoria o un puzzle molto breve e riceve una ricompensa immediata in base alla prestazione.

La stanza non rivela, sceglie o modifica più lo scenario Boss. Lo scenario è già determinato dal capitolo corrente.

## Flusso generale

1. Il giocatore entra nella Prova Lampo.
2. Viene selezionato un minigioco dal catalogo disponibile.
3. Il giocatore può iniziare oppure rinunciare.
4. Durante la prova può abbandonare, con conferma.
5. Una prova completata assegna una fascia di risultato e una ricompensa immediata.
6. Un tentativo fallito non applica penalità e può assegnare una consolazione o il premio relativo al risultato raggiunto.
7. Rinuncia e abbandono non assegnano premi, attivano il malus della prossima stanza Mostro e possono mostrare una pubblicità interstitial.

## Risultati comuni

Tutti i minigiochi restituiscono uno dei seguenti risultati:

- `Perfect`
- `Excellent`
- `Good`
- `Completed`
- `Failed`
- `Forfeited`

I minigiochi misurano soltanto la prestazione. La stanza converte la fascia ottenuta nella ricompensa, mantenendo separati logica del gioco e bilanciamento dei premi.

## Minigioco: Quiz rapido

- Una sessione di tre domande, ciascuna con tre risposte.
- Domande relative alle regole, alle classi, ai personaggi o al mondo narrativo.
- Durata indicativa: 10–15 secondi.
- Le domande provengono da un catalogo dati estendibile e localizzabile.
- Ogni domanda ha il proprio countdown; risposta errata o tempo scaduto valgono come errore ma non interrompono la sessione.
- Dopo la terza domanda: 1/3 produce `BAD`, 2/3 `GOOD`, 3/3 `EXCELLENT`; 0/3 produce `FAILED`.
- Conclusa la terza domanda parte la slot machine della ricompensa.
- La domanda viene scelta all'avvio e non può essere cambiata chiudendo e riaprendo l'interfaccia.

## Minigioco: Sequenza delle classi

Gioco di memoria in stile Simon basato sui simboli delle classi.

1. Il gioco illumina un simbolo di classe.
2. Il giocatore ripete la sequenza.
3. Il gioco riproduce la stessa sequenza aggiungendo un simbolo.
4. Il giocatore deve ripeterla integralmente.
5. La prova termina al primo errore, per inattività o al raggiungimento del limite massimo.

Configurazione iniziale:

- sequenza iniziale: 1 simbolo;
- limite: 8 livelli;
- illuminazione: circa 0,55 secondi;
- pausa tra simboli: circa 0,20 secondi;
- massimo 3 secondi tra due input del giocatore;
- input disabilitato durante la riproduzione;
- feedback visivo e sonoro per ogni simbolo.

La ricompensa dipende dalla sequenza più lunga completata nel tentativo corrente:

| Livelli completati | Fascia |
| ---: | --- |
| 0–1 | `Failed` / consolazione |
| 2–3 | `Completed` |
| 4–5 | `Good` |
| 6–7 | `Excellent` |
| 8 | `Perfect` |

Il record personale può essere salvato per statistiche o achievement, ma non modifica il premio delle prove successive.

## Minigioco: Puzzle scorrevole 3×3

Classico 8-puzzle costruito con artwork selezionate del gioco:

- l'immagine è divisa in una griglia 3×3;
- l'artwork viene scelta casualmente a ogni nuova prova da una whitelist curata; la prima selezione comprende tutte le carte Goblin e tutte le carte Skeleton;
- sono presenti 8 tasselli e una cella vuota;
- soltanto un tassello adiacente allo spazio vuoto può essere spostato;
- il puzzle è completato ricostruendo l'immagine originale.

La disposizione iniziale viene generata partendo dalla soluzione e applicando 40–60 mosse valide casuali. Non si usa una permutazione arbitraria, perché potrebbe produrre puzzle irrisolvibili. Il mescolamento evita, quando possibile, di annullare immediatamente la mossa precedente.

Presentazione:

1. Artwork completa per circa 2 secondi.
2. Divisione visiva in tasselli.
3. Mescolamento.
4. Avvio del cronometro quando il puzzle diventa interattivo.
5. Contatori visibili di tempo e mosse.
6. Al completamento viene ripristinata anche la nona cella e mostrata l'artwork completa.

Valutazione iniziale da sottoporre a playtest:

| Fascia | Tempo | Mosse |
| --- | ---: | ---: |
| `Perfect` | ≤ 45 s | ≤ 60 |
| `Excellent` | ≤ 75 s | ≤ 100 |
| `Good` | ≤ 120 s | ≤ 150 |
| `Completed` | oltre le soglie precedenti | oltre le soglie precedenti |

Tempo e mosse sono valutati separatamente e il risultato finale usa la fascia peggiore. Dopo 90 secondi può essere offerta la conclusione anticipata con premio minimo. Il tentativo è considerato valido dopo almeno 10 mosse e 15 secondi; prima di entrambe le soglie l'uscita equivale a rinuncia.

Quando il puzzle viene risolto parte la slot machine comune, con classe, potenza e premio EXP/oro.

## Ricompense

Al termine di un tentativo valido, il contenitore del minigioco esce lateralmente dallo schermo e viene sostituito con un'animazione da slot machine a tre rulli:

1. **Classe**: determina la classe della carta vinta.
2. **Potenza**: mostra la potenza determinata dal record del memory: livelli completati +2, da un minimo di 2 a un massimo di 10. Per esempio, 8 livelli danno potenza 10 e 7 livelli danno potenza 9.
3. **Premio**: determina un bonus immediato in EXP oppure oro.

Il risultato completo assegna quindi una carta compatibile con classe e potenza estratte, più la quantità di EXP o oro mostrata dal terzo rullo. La rinuncia salta interamente la slot machine.

La carta viene estratta esclusivamente dal pool di definizioni non ancora possedute, usando lo stesso criterio di equivalenza del mazzo di campagna. Classe e potenza mostrate dalla slot appartengono quindi a una carta reale eleggibile, non sono due risultati indipendenti. La potenza obiettivo è `record + 2`; se tutte le carte di quella potenza sono già possedute, viene scelta la potenza disponibile più vicina, privilegiando quella inferiore a parità di distanza. Se non rimane alcuna carta nuova, la slot assegna soltanto il premio EXP/oro e comunica che la collezione disponibile è completa.

Le ricompense non introducono modificatori nascosti per i combattimenti successivi. Il pool previsto comprende:

- EXP;
- oro;
- consumabili;
- recupero di una carta dal cooldown;
- raramente una nuova carta.

Bilanciamento indicativo:

| Fascia | Ricompensa indicativa |
| --- | --- |
| `Failed` | 5 EXP oppure poco oro |
| `Completed` | 10 EXP oppure oro |
| `Good` | 20 EXP oppure un consumabile |
| `Excellent` | 30 EXP e scelta fra due premi |
| `Perfect` | 50 EXP, consumabile raro o carta |

## Rinuncia, abbandono e malus

All'ingresso sono disponibili `INIZIA LA PROVA` e `RINUNCIA`. Durante il minigioco è disponibile `ABBANDONA`.

Rinuncia e abbandono richiedono conferma esplicita:

> Rinunciando alla prova, nella prossima stanza Mostro otterrai soltanto metà EXP e metà oro. Se non possiedi il blocco pubblicità, verrà mostrato un annuncio.

Conseguenze:

- nessuna ricompensa della Prova Lampo;
- attivazione persistente di **Prezzo della Rinuncia**;
- nella prossima stanza `RoomType.Monster` completata: EXP e oro ridotti del 50%;
- valori dispari arrotondati per difetto;
- la penalità non si consuma in stanze Mercante, Loot, Prova Lampo o Boss;
- in caso di sconfitta o ritirata rimane attiva;
- più rinunce non si accumulano: resta una singola penalità del 50%;
- il malus viene salvato nella run.

I bonus vengono calcolati prima del malus. Per esempio, Double EXP può compensare la penalità sull'EXP, mentre l'oro resta dimezzato.

La HUD mostra finché necessario:

> **Prezzo della Rinuncia** — Prossima ricompensa Mostro: −50% EXP e oro.

## Pubblicità interstitial

Alla conferma di rinuncia o abbandono:

1. Il malus viene attivato e salvato prima della richiesta pubblicitaria.
2. Se il giocatore non possiede il blocco pubblicità, viene richiesta una normale interstitial non ricompensata.
3. Alla chiusura dell'annuncio la stanza termina e la campagna prosegue.
4. Se l'annuncio non è disponibile o fallisce, si prosegue comunque e il malus resta applicato.
5. Con blocco pubblicità attivo si prosegue immediatamente.

L'interstitial viene mostrata soltanto per una rinuncia volontaria, mai per risposta errata, errore di memoria, puzzle fallito, crash o indisponibilità tecnica.

## Rimozione del sistema legacy

Il restyling elimina:

- lancio del D12;
- i dodici vecchi eventi e premi;
- la rivelazione casuale dello scenario Boss dalla stanza;
- i bonus one-shot collegati ai vecchi eventi;
- dati e campi di salvataggio rimasti senza utilizzo;
- i pesi legacy `monsterRoomWeight`, `merchantRoomWeight`, `lootRoomWeight` e `opportunityRoomWeight` (`60/15/15/10`) se confermati senza altri consumatori;
- testi, banner e asset obsoleti della vecchia identità.

Restano intatti lo scenario e il Boss già determinati dal capitolo. I campi di scenario condivisi con capitoli, HUD, formazioni e Boss non devono essere cancellati indiscriminatamente.

## Nome

Nome definitivo: **Sfida Veloce** in italiano e **Quick Challenge** in inglese. Identificativo tecnico: `quick_challenge`.
