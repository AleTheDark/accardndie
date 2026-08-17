# Prova di carico: il VPS regge il lancio?

Voce aperta nella [roadmap del 26 settembre](roadmap-lancio-26-settembre.md): *"2 vCore /
2 GB reggono un lancio? Misuralo adesso, non il 26."* Questo documento e' il come. Lo
strumento e' [`Server/AccardND.LoadTest`](../Server/AccardND.LoadTest/README.md).

La domanda "regge?" da sola non ha risposta. Quelle che ne hanno una sono tre:

1. **Quanti giocatori insieme** prima che i tempi di risposta diventino brutti?
2. **Cosa cede per primo** - CPU, memoria, il singolo scrittore di SQLite, i socket?
3. **Quanto margine** c'e' fra quel numero e quello che il lancio portera' davvero?

La terza e' la sola che decide se il 26 si dorme. Le prime due si misurano.

## 1. Il bersaglio: un gemello, non la produzione

I bot registrano account, scrivono progressione e chiudono partite classificate. Sul
database dei giocatori quelle righe restano, e finiscono in Hall of Fame. Lo strumento
per questo si rifiuta di partire contro `/ws` di un host remoto senza `--allow-production`.

La misura che serve e' della **macchina**, non dell'istanza: quindi un secondo processo
sullo stesso VPS, con il suo database e la sua porta. Il gemello e la produzione si
contendono gli stessi 2 vCore, ed e' esattamente quello che vogliamo sapere.

Sul VPS:

```bash
# 1. Copia del binario gia' pubblicato, con config e database propri.
sudo cp -r /opt/accardnd /opt/accardnd-test
sudo rm -f /opt/accardnd-test/accardnd.db*
```

`/opt/accardnd-test/serverconfig.json`, le sole voci che cambiano:

```json
{
  "Urls": "http://127.0.0.1:5018",
  "DatabaseFilePath": "/opt/accardnd-test/accardnd.db",
  "AllowPasswordAuth": true,
  "ClientVersion": { "Target": "0.9.2", "Enforce": false }
}
```

`AllowPasswordAuth` e' il punto: in produzione e' `false` e si entra col token di Unity
Authentication, che i bot non possono avere. Sul gemello si riapre il login con password,
che e' l'unico modo per fare centinaia di accessi senza passare da Google. Vedi il §7 per
cosa questo cambia nei numeri.

Un `location` di nginx per farci arrivare da fuori con TLS, cosi' la prova misura anche il
proxy e non solo il processo .NET - le stesse direttive di `/ws` in
[`accardndie-nginx-site.conf`](deploy/accardndie-nginx-site.conf), cambiando la porta:

```nginx
location /wstest {
    proxy_pass http://127.0.0.1:5018/ws;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_read_timeout 86400;
}
```

**Prima di iniziare, alza il tetto dei file aperti** del servizio, o a qualche migliaio di
connessioni si scopre un limite che non c'entra niente col codice:

```bash
sudo systemctl edit accardnd-test   # [Service] LimitNOFILE=65535
```

Alla fine di tutto: ferma il gemello, cancella `/opt/accardnd-test` e togli il `location`.
Un endpoint con il login a password aperto non deve sopravvivere alla prova.

## 2. Il generatore, e come si scavalca Cloudflare

Dal tuo PC, non dal VPS: cosi' nella misura ci sono anche rete, TLS e nginx, che il giorno
del lancio ci saranno. Il traffico e' fatto di messaggi piccoli, l'upstream di casa basta
fino a un migliaio di bot; oltre, o si aprono due generatori in parallelo, o se ne prende
uno a ore in un datacenter.

**Ma non attraverso Cloudflare.** `accardndie.com` e' proxato (`Server: cloudflare`), e
puntargli contro qualche centinaio di bot vuol dire tre cose, tutte sbagliate: misuri la
rete di Cloudflare invece della tua macchina, rischi che Bot Fight Mode o il rate limiting
respingano i bot - a quel punto i numeri sono di Cloudflare, non del server - e il tuo IP
di casa puo' finire limitato proprio mentre stai provando.

Il modo pulito e' fissare il DNS sulla sola macchina che genera il carico. In
`C:\Windows\System32\drivers\etc\hosts` (da amministratore):

```
217.160.212.85 accardndie.com
```

Cosi' l'URL resta identico - stesso SNI, stesso `Host`, il certificato di Let's Encrypt
combacia, nginx vede la richiesta come la vedra' il giorno del lancio - ma Cloudflare non
c'e'. Verificato che funziona: l'origine risponde in diretta sulla 443 con il certificato
buono. Alla fine della prova, togli la riga.

```bash
dotnet run -c Release --project Server/AccardND.LoadTest -- \
  --url wss://accardndie.com/wstest \
  --profile mixed --clients 200 --ramp 120 --duration 600 \
  --json prove/mixed-200.json
```

Una sola corsa **attraverso** Cloudflare vale la pena farla - piccola, 50 bot, dieci
minuti, senza la riga in `hosts` - ma non per misurare la capacita': serve solo a vedere
che il percorso regge, cioe' che nessuna protezione respinge i client e che le connessioni
restano su. Se in quella corsa i bot cadono a raffica, il problema e' nella configurazione
di Cloudflare e va risolto prima del 26, non sotto carico vero.

> Nota di sicurezza, emersa provando: l'origine risponde in diretta sul suo IP a chiunque
> lo conosca, e l'IP e' scritto in chiaro nel `server_name` di nginx (`217-160-212-85.sslip.io`).
> Ottimo per la prova di carico, meno per il lancio: chi lo trova scavalca WAF e protezione
> DDoS di Cloudflare. Se prima del 26 vuoi chiudere la porta, si filtra la 443 sui soli
> [range di Cloudflare](https://www.cloudflare.com/ips/) - ricordandosi che da quel momento
> la prova di carico va lanciata da dentro il VPS o riaprendo temporaneamente al tuo IP.

E sul VPS, in una sessione a parte, il campionatore della macchina:

```bash
./Docs/deploy/loadtest-monitor.sh 5018 5 > prove/mixed-200.csv
```

Senza il campionatore la prova dice solo *che* i tempi sono saliti. Con, dice **perche'**.

## 3. La scala delle prove

Cinque corse, in quest'ordine. Ognuna risponde a una domanda diversa; saltarne una
significa non sapere quella cosa li'.

| # | comando (oltre a `--url`)                                             | domanda                                               |
|---|-----------------------------------------------------------------------|-------------------------------------------------------|
| 1 | `--profile connect --clients 500 --ramp 60 --duration 300`             | quanto costa un giocatore **fermo**? (RAM per socket)  |
| 2 | `--profile singleplayer --clients 100/250/500/1000 --ramp 120 --duration 600` | dove sta il ginocchio della curva?             |
| 3 | `--profile pvp --clients 200 --ramp 60 --duration 600`                 | le partite reggono? timer e broadcast tengono il passo? |
| 4 | `--profile mixed --clients <ginocchio> --ramp 0 --duration 300`         | e se arrivano tutti insieme?                          |
| 5 | `--profile web --web-url https://accardndie.com --web-clients 50`      | le pagine che leggono dal DB reggono in parallelo?     |

La 2 e' la prova vera e va ripetuta raddoppiando finche' qualcosa non si rompe: il numero
che cerchi e' **l'ultimo gradino pulito**, non il primo che cede.

La 4 non e' teorica: un rollout Play che passa dal 20% al 50%, o un video di qualcuno che
ne parla, sono mandrie. `--ramp 0` fa entrare tutti insieme, e i login sono la parte piu'
cara di tutta la sessione di un giocatore.

## 4. Cosa vuol dire "pulito"

Un gradino conta come superato solo se **tutte** queste cose sono vere:

| misura                                    | soglia            | dove si legge                    |
|-------------------------------------------|-------------------|----------------------------------|
| p95 delle letture (`*.get`)               | < 300 ms          | tabella finale dello strumento   |
| p95 delle scritture (`reward.death`, run) | < 800 ms          | tabella finale                   |
| p99 di qualunque operazione               | < 3 s             | tabella finale                   |
| errori applicativi                        | < 0,5%            | riga "richieste ... con errore"  |
| connessioni cadute                        | 0                 | riga "connessioni fallite/cadute"|
| CPU del processo                          | < 140 su 200      | `cpu_processo` nel CSV           |
| RSS del server                            | < 1,2 GB          | `rss_mb` nel CSV                 |
| swap usata                                | 0                 | `swap_usata_mb` nel CSV          |
| WAL di SQLite                             | non cresce senza fermarsi | `wal_mb` nel CSV         |

Le soglie di CPU e memoria sono strette apposta: sul VPS il gemello gira **accanto** alla
produzione, e il giorno del lancio quel margine serve a nginx, ai backup e a te che entri
in SSH mentre le cose vanno male.

## 5. Come si legge quello che cede

| sintomo                                                        | quasi sempre e'                                                       | cosa si fa                                                                  |
|----------------------------------------------------------------|-----------------------------------------------------------------------|------------------------------------------------------------------------------|
| p95 delle **scritture** esplode, letture a posto               | il singolo scrittore di SQLite: le transazioni si mettono in fila       | ridurre le scritture per run, o accorpare; il `busy_timeout` e' 5 s, dopo e' errore |
| errori `server_error` in coda alle scritture                   | `SQLITE_BUSY`: il timeout e' scaduto davvero                            | e' il tetto vero del database: quello e' il numero da non superare            |
| CPU piantata a 200 durante la rampa, poi giu'                  | i login (PBKDF2 100.000 iterazioni)                                     | **artefatto della prova**, vedi §7: in produzione i login non fanno questo    |
| CPU alta a regime, senza rampa                                 | serializzazione JSON e broadcast di match                               | qui si guarda quante partite in parallelo, non quanti giocatori               |
| RSS che sale e non scende mai                                  | connessioni non chiuse, o cache che non ha un tetto                     | rifai la 1 con piu' bot e guarda RSS diviso connessioni                       |
| connessioni che cadono a un numero tondo                       | `LimitNOFILE`, o `worker_connections` di nginx                          | limite di configurazione, non del codice: alzalo e rifai                      |
| tutto a posto sul VPS ma p95 pessimo dal generatore            | il collo di bottiglia sei tu                                            | meno bot per generatore, o generatore in datacenter                          |
| `wal_mb` che cresce e basta                                    | nessun checkpoint riesce a passare fra una scrittura e l'altra          | segnale serio: significa scritture continue senza respiro                     |

## 6. Da "regge N bot" a "regge il lancio"

Il numero che esce dalla prova e' di **giocatori connessi insieme**. Quello che si sa del
lancio sono le installazioni. Il ponte fra i due, con le regole del pollice che si usano
per i giochi mobile:

- giocatori attivi al giorno ≈ 20-30% delle installazioni della prima settimana;
- connessi insieme al picco ≈ 5-10% degli attivi al giorno.

Mille installazioni nei primi giorni fanno quindi 200-300 attivi e **10-30 connessi
insieme** al picco. E' un ordine di grandezza sotto qualunque gradino di questa prova, ed
e' la ragione per cui il risultato piu' probabile e' che il VPS regga con margine largo.
Vale comunque la pena di misurarlo: serve sapere **dove** sta il muro, perche' e' quello
che dice quanto si puo' crescere prima di dover cambiare macchina - e perche' se il muro
salta fuori a 50 invece che a 5.000, allora c'e' un bug, e lo si scopre adesso.

Il rollout progressivo dal 20% gia' previsto in roadmap e' la rete di sicurezza per il
resto: se la progressione lato server ha un problema sotto carico vero, lo vede un quinto
dei giocatori.

## 7. Cosa questa prova **non** misura

Da tenere presente quando si guardano i numeri, per non credere a cose che non sono vere:

- **I login non sono quelli veri.** I bot usano il login con password, che costa un PBKDF2
  da 100.000 iterazioni - decine di millisecondi di CPU pura ciascuno. In produzione si
  entra col token di Unity Authentication: verifica di firma, molto piu' leggera, ma con
  una chiamata di rete verso Google che qui non c'e'. La CPU della rampa e' pessimista, la
  latenza del login e' ottimista. Per una prova a regime senza questo rumore: registra una
  volta i bot, poi ripeti con `--login login`.
- **Il download del gioco WebGL non c'e', ed e' il rischio piu' grosso dei due.** Vedi il
  §8: e' un problema di banda, non di CPU, e questa prova non lo tocca.
- **Gli annunci e gli acquisti non ci sono.** SSV di AdMob e verifica delle ricevute Play
  parlano con l'esterno: sotto carico dipendono da Google, non da noi.
- **I bot giocano male e sempre allo stesso modo.** Il carico e' realistico nei volumi, non
  nella varieta': una partita fra bot dura meno di una fra persone, quindi a parita' di
  giocatori i bot chiudono piu' partite e scrivono piu' risultati. E' pessimista, e va
  bene cosi'.

## 8. Il collo di bottiglia vero: la build WebGL, che Cloudflare oggi non copre

Misurato il 16 agosto 2026 su `accardndie.com`:

| file                    | dimensione | `cf-cache-status` |
|-------------------------|------------|-------------------|
| `Build/output-web.data` | 275,9 MB   | `DYNAMIC`         |
| `Build/output-web.wasm` | 71,3 MB    | `DYNAMIC`         |
| `Build/output-web.loader.js` | 27 KB | `MISS` (cacheabile) |

`DYNAMIC` vuol dire che Cloudflare **non** tiene quei file al bordo: li richiede al VPS
ogni volta. Sono ~347 MB per ogni giocatore nuovo sul web, e li paga la macchina. Cento
persone che aprono il gioco nella stessa ora sono 35 GB in uscita dal VPS - un ordine di
grandezza piu' impegnativo di tutto il traffico di gioco messo insieme, che di messaggi
JSON ne fa qualche decina di KB a testa.

Il motivo e' che `.wasm` e `.data` non sono fra le estensioni che Cloudflare mette in cache
da sola, e la risposta non porta un `Cache-Control` che glielo chieda. Anzi: **su
`Build/` non arriva nessun `Cache-Control`**, ne' attraverso Cloudflare ne' dall'origine in
diretta, mentre [`webgl-hosting-cache.md`](webgl-hosting-cache.md) prescrive
`public, max-age=31536000, immutable` e [`deploy/accardndie-nginx.conf`](deploy/accardndie-nginx.conf)
lo scrive. Quel blocco di `location` non e' nel vhost in produzione: non lo e' per `Build/`,
ne' per `/game/`, ne' per `/sw.js`. Verifica:

```bash
curl -sI https://accardndie.com/game/Build/output-web.wasm | grep -i "cache-control\|cf-cache-status"
```

Due leve, indipendenti, in ordine di resa:

1. **Rimettere le regole di nginx** del file di deploy nel vhost vivo. Da sola risolve la
   cache del browser (ritorni senza ri-download) e mette `Cache-Control` in una forma che
   Cloudflare accetta.
2. **Una Cache Rule su Cloudflare** per `/game/Build/*` con "Eligible for cache" e un Edge
   TTL lungo. Da quel momento i 347 MB li serve il bordo e il VPS li manda una volta per
   PoP invece che una per giocatore. Da sapere: il piano free ha limiti d'uso sulla
   distribuzione di file grossi non-HTML; se il gioco prende davvero, la strada pulita e'
   spostare `Build/` su un object storage con egress gratuito (R2) e lasciare al VPS solo
   il gioco vero.

Nessuna delle due si prova col load test: si verificano con `curl` e si guardano il giorno
del lancio nel grafico della banda. Ma se il 26 qualcosa cade, il candidato numero uno e'
questo, non il WebSocket.

## 9. Traccia dei risultati

Le corse vanno tenute: `--json` per lo strumento, il CSV del campionatore per la macchina,
un file per gradino. Servono a due cose - vedere se una release peggiora le cose (si
rifa' la 2 al ginocchio e si confrontano i p95) e avere qualcosa da guardare il giorno in
cui il server rallenta davvero, per sapere se e' un carico che gia' conosciamo o no.
