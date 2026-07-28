# AccardND — Server PvP

Server autoritativo per la modalità PvP. Riusa i sorgenti di `Assets/_Project/Scripts/GameCore`
e `NetProtocol` (linkati nel csproj): le regole di gioco vivono in un solo posto.

## Avvio

```
cd Server/AccardND.Server
dotnet run
```

Il server ascolta su `http://localhost:5017` (WebSocket su `/ws`, health check su `/health`).
Richiede il .NET SDK 9.

## Pannello admin

Dashboard web (statistiche login/attività, partite PvP, run campagna, gestione
account) su `/admin`, servita dallo stesso processo. Spenta finché non si imposta
una password admin (`ACCARDND_ADMIN_PASSWORD`). Dettagli e deploy:
[`Docs/admin-panel.md`](../../Docs/admin-panel.md).

```bash
ACCARDND_ADMIN_PASSWORD='...' dotnet run   # poi apri http://localhost:5017/admin
```

## Deploy sul VPS

Il server gira su `217.160.212.85` come servizio systemd `accardnd`, con eseguibile e
dati in `/opt/accardnd/`. Si pubblica come singolo file self-contained: sul VPS non
serve il runtime .NET installato.

### 1. Pubblica

```bash
dotnet publish Server/AccardND.Server/AccardND.Server.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

Non serve un `dotnet build` prima: `publish` compila comunque.

### 2. Copia il binario

```bash
cd Server/AccardND.Server/bin/Release/net9.0/linux-x64/publish
scp AccardND.Server root@217.160.212.85:/opt/accardnd/AccardND.Server.new
```

### 3. Sostituisci e riavvia

```bash
ssh root@217.160.212.85 "test -s /opt/accardnd/AccardND.Server.new && cp /opt/accardnd/accardnd.db /opt/accardnd/accardnd.db.bak && cp /opt/accardnd/AccardND.Server /opt/accardnd/AccardND.Server.old && systemctl stop accardnd && mv /opt/accardnd/AccardND.Server.new /opt/accardnd/AccardND.Server && chmod +x /opt/accardnd/AccardND.Server && systemctl start accardnd && sleep 3; systemctl is-active accardnd; journalctl -u accardnd -n 15 --no-pager"
```

Il `test -s` iniziale non e' un dettaglio: senza, se lo `scp` del passo 2 non e' stato
fatto o e' fallito, lo `stop` viene comunque eseguito, il `mv` fallisce, la catena `&&`
si interrompe e **il servizio resta giu'**. Con la guardia, se il binario nuovo manca non
viene toccato niente.

### Rollback

```bash
ssh root@217.160.212.85 "mv /opt/accardnd/AccardND.Server.old /opt/accardnd/AccardND.Server && systemctl restart accardnd"
```

Il database precedente resta in `/opt/accardnd/accardnd.db.bak`.

### Note

- `serverconfig.json`, `cardcatalog.json` e `accardnd.db` vivono in `/opt/accardnd/` e
  **non** sono dentro il singolo file: vanno copiati a parte solo se cambiano.
- Lo schema del database si aggiorna da solo all'avvio (`CREATE TABLE IF NOT EXISTS` e
  `AddColumnIfMissing` in `AccardDatabase.Initialize`): nessuna migrazione manuale. Se lo
  schema fallisse il processo non partirebbe, quindi un `is-active` che risponde `active`
  e' gia' una conferma.
- I log del VPS sono in UTC.
- Il deploy del server non tocca il client: le schermate Unity richiedono una
  ridistribuzione separata del build WebGL.

Verifica dello schema dopo un deploy che aggiunge tabelle:

```bash
ssh root@217.160.212.85 "sqlite3 /opt/accardnd/accardnd.db '.tables'"
ssh root@217.160.212.85 "sqlite3 /opt/accardnd/accardnd.db 'PRAGMA table_info(campaign_runs)'"
```

## Configurazione

Tutti i valori (budget, costi carte/dadi, timer, vite) sono in `serverconfig.json`,
letto all'avvio. Il client riceve le stesse regole via messaggio `rules.data`:
il ScriptableObject Unity serve solo come default per la UI, l'autorità è del server.

Gli account sono salvati in `accounts.json` (creato al primo register) con password
PBKDF2; da sostituire con un database allo Step 6 (persistenza/ranked).

## Protocollo

WebSocket testuale, buste JSON `{ "type": "...", "payload": "<json>" }`
(payload doppio-codificato per compatibilità con JsonUtility di Unity).
I tipi messaggio e i DTO sono in `Assets/_Project/Scripts/NetProtocol`.

Flusso: `auth.register`/`auth.login` → `room.create` (riceve codice) oppure
`room.join {code}` oppure `queue.join` — il loadout viaggia con la richiesta
e viene validato server-side. Al pairing: `match.found`, poi `match.start`
(con l'indice giocatore assegnato) e la partita vera.

## Match

Il match è pilotato da `PvpMatchEngine` (GameCore): best-of-3, 2 vite per
carta, dado vigore che scala col round (D4/D6/D8), abilità di classe complete,
aure e attachment. Il client invia `match.action`
(`deploy`/`ability`/`attack`/`attach`/`pass`/`decisive`) e riceve:

- `match.hand` — mano privata del round (solo al proprietario);
- `match.event` — eventi pubblici del motore (`RoundStarted`,
  `DeploymentStarted`, `CardDeployed`, `TurnStarted`, `AttackResolved`,
  `RoundEnded`, `MatchEnded`, ...), che il client riproduce senza logica propria.

Tutti i tiri (iniziativa, mescolate, vigore) avvengono sul server.

## Test manuale da Unity

Componente `PvpClientSmokeTest` (in `Scripts/Network`): connette, autentica,
crea/entra in stanza o coda con un loadout di prova e logga i messaggi.
