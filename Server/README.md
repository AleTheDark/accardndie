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
e viene validato server-side. Al pairing: `match.found`, `match.start`
(iniziativa tirata dal server), `match.hand` (mano privata del round).

## Test manuale da Unity

Componente `PvpClientSmokeTest` (in `Scripts/Network`): connette, autentica,
crea/entra in stanza o coda con un loadout di prova e logga i messaggi.
