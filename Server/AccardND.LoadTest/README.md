# AccardND.LoadTest

Generatore di carico per il server PvP/progressione. I bot parlano il protocollo vero:
questo progetto compila gli stessi sorgenti `GameCore` e `NetProtocol` del server, quindi
se un DTO cambia lo strumento smette di compilare invece di mentire.

Il piano di prova - come si prepara il bersaglio, cosa si guarda e come si leggono i
numeri - sta in [Docs/prova-di-carico.md](../../Docs/prova-di-carico.md). Qui c'e' solo il
manuale del comando.

## Uso

```bash
dotnet run -c Release --project Server/AccardND.LoadTest -- \
  --url ws://127.0.0.1:5018/ws \
  --profile mixed --clients 200 --ramp 120 --duration 600 \
  --json prova-200.json
```

## Profili

| profilo        | cosa fa                                                                  |
|----------------|--------------------------------------------------------------------------|
| `connect`      | connette e autentica, poi resta fermo: misura il costo di un giocatore inattivo |
| `singleplayer` | apertura app, run di campagna, reward, pannelli, classifiche              |
| `pvp`          | partite vere a coppie: stanze private, o coda ranked con `--pvp-ranked`   |
| `mixed`        | una quota `--pvp-share` in PvP, il resto in singolo (default)             |
| `web`          | solo pagine HTTP (`--web-url`), niente WebSocket                          |

`--web-clients` aggiunge worker HTTP a qualunque profilo.

## Opzioni

Tutte le opzioni sono elencate da `--help`. Le tre che cambiano il senso della prova:

- `--ramp` — su quanti secondi distribuire l'ingresso dei bot. `0` e' la mandria: serve
  a provare il caso "annuncio su un canale grosso", non il regime normale.
- `--login register|login` — `register` crea un account per bot (piu' pesante: scrive e
  paga un PBKDF2 da 100.000 iterazioni). `login` riusa gli account di una corsa
  precedente con lo stesso `--prefix`.
- `--pvp-ranked` — usa la coda ranked invece delle stanze private. I bot si sbloccano il
  Guerriero riscuotendo il primo modulo del tutorial, perche' la coda pretende le classi.

## Come si comporta un bot

Il bot PvP gioca come il server quando scade il timer di turno: schiera la prima carta,
in battaglia attacca uno slot avversario davvero occupato (ricostruito dagli eventi) o
passa, al round decisivo prende le prime tre. Non gioca bene, ma produce il traffico di
una partita vera fino alla scrittura del risultato con MMR, statistiche e stagione.

Il bot in singolo alterna run di campagna (`run.started`, rapporti di uccisioni, reward
di morte: tutte transazioni su SQLite) e consultazioni di taverna, santuario, talenti,
profilo e classifiche.

## Uscita

Una riga di stato ogni `--report` secondi con connessioni aperte, richieste al secondo,
errori al secondo e p95; alla fine la tabella delle latenze per operazione e i codici
d'errore. Con `--json` lo stesso riepilogo finisce in un file, per confrontare due corse.

Il codice di uscita e' `1` se ci sono stati errori: comodo negli script, ma leggi sempre
la tabella, perche' un 404 sulla home (nginx non c'e' in locale) e un timeout in coda
contano allo stesso modo li' dentro.

## Protezione

Puntato a un host remoto sul percorso `/ws` lo strumento si rifiuta di partire senza
`--allow-production`: i bot registrano account, scrivono progressione e chiudono partite
classificate, e sul database dei giocatori veri quelle righe restano.
