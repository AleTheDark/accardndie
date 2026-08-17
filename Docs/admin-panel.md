# Pannello admin

Dashboard web per amministrare il gioco: statistiche di login/attività nel tempo,
partite PvP e run di campagna, gestione degli account. Vive **dentro il server
PvP .NET** (`Server/AccardND.Server`), quindi non c'è un secondo servizio da avviare.

- Pagina: `GET /admin`
- API JSON: `/admin/api/*`
- In locale: <http://localhost:5017/admin>
- In produzione (dietro nginx): <https://accardndie.com/admin>

## Attivazione

Il pannello è **spento di default**. Si attiva fornendo una password admin; senza
password ogni route `/admin` risponde `404` (non rivela nemmeno di esistere).

La password si passa via variabile d'ambiente (consigliato, così non finisce nel
file versionato):

```bash
export ACCARDND_ADMIN_PASSWORD='una-password-robusta'
```

Username e durata sessione (opzionali) stanno in `serverconfig.json`:

```json
"Admin": {
  "Username": "admin",
  "SessionTtlHours": 12
}
```

> In alternativa alla env var si può mettere `"Password"` dentro `Admin` in
> `serverconfig.json`, ma **quel file è versionato**: non committare segreti lì.
> La env var, se presente, ha la precedenza.

### Sul VPS (systemd)

Aggiungere la env var al service unit del server .NET, es.:

```ini
# /etc/systemd/system/accardnd.service  (dentro [Service])
Environment=ACCARDND_ADMIN_PASSWORD=una-password-robusta
```

Poi `sudo systemctl daemon-reload && sudo systemctl restart accardnd`.

### nginx

Aggiungere il blocco `location /admin` di
[`deploy/accardndie-nginx.conf`](deploy/accardndie-nginx.conf) dentro il `server { }`,
`sudo nginx -t`, `sudo systemctl reload nginx`.

## Colonne della tab Giocatori

Nome (con nickname sotto, se diverso, e il badge *online*), fonte, **livello** con
l'esperienza verso il prossimo (`40/220`), **esperienza totale**, miele, match,
vittorie, sconfitte, data di registrazione e ultimo login. Cliccando la riga si
apre il dettaglio completo. Chi non ha mai giocato al single player mostra i
valori di partenza (livello 1, `0/100`), perché la riga di progressione non esiste
ancora.

## Mail degli account Google

Nella lista giocatori, sotto la fonte, compare la mail dell'account Google; la
ricerca la usa come chiave, quindi si possono trovare i doppioni cercando
l'indirizzo. Si popola **solo dai login Google successivi al 2026-07-29** (prima
non veniva salvata) e **non esiste per gli account Google Play Games**, che dal
provider ricevevano un id giocatore e mai l'indirizzo. Dettagli in
[login-google-android.md](login-google-android.md).

## Sicurezza

- Login con username+password (confronto constant-time); il login riuscito emette
  un **token bearer** in memoria con scadenza (`SessionTtlHours`). I token non
  sopravvivono al riavvio: è voluto.
- Servire **solo su HTTPS** (Cloudflare/nginx lo fanno già). La password viaggia
  nel body del POST di login: senza TLS sarebbe in chiaro.
- Scegliere una password lunga: non c'è rate-limiting sul login.

## Cosa mostra

- **Panoramica** — KPI (account, attivi 24h/7g, login, partite PvP, run iniziate,
  run concluse, online ora) e un grafico dell'attività nel tempo (login,
  registrazioni, partite, run iniziate, run concluse) su 7/30/90 giorni.
- **Retention** — quanti giocatori tornano dopo essersi registrati, per coorte.
  Vedi [Retention](#retention).
- **Giocatori** — ricerca per nome o `player_id`; il dettaglio raccoglie tutto
  quello che il server sa di un account:
  - account (fonte, registrazione, ultimo login, miele, livello/esperienza,
    tutorial, hardcore, nickname, icona, bio);
  - quest della taverna di oggi con progresso e stato, piu' i totali di sempre
    (quest riscosse, giornate complete, miele guadagnato in taverna);
  - progressione campagna: aggregati delle run e **contatori** (gli stessi su cui
    si valutano requisiti del Santuario e quest), ricompense riscattate per tipo;
  - Santuario: sblocchi, scorta consumabili, bisaccia della prossima run;
  - PvP: tier/MMR/classifica della stagione attiva, statistiche da sempre e di
    stagione, Hall of Fame, amici;
  - collezione: achievement, icone, mostri sconfitti;
  - storico: ultime partite, ultime run, ultimi login.
- **Quest taverna** — le cinque quest estratte per oggi con quanti giocatori le
  hanno assegnate, completate e riscosse; storico delle giornate (giocatori in
  taverna, quest riscosse, giornate complete, miele erogato) su 7/14/30/90 giorni;
  catalogo completo con quante volte ogni quest e' uscita e quante riscossioni ha
  prodotto. Utile perche' le quest sono l'unica fonte di miele del gioco.
- **Run campagna** — tutte le run, iniziate e concluse, con orario di inizio,
  giocatore, stato, durata, capitolo e progressi. Il filtro in alto isola le run
  **non concluse**. Vedi [Run iniziate e run concluse](#run-iniziate-e-run-concluse).
- **Partite PvP** — ultime partite con esito, punteggi, ranked/normale.
- **Stagioni** — elenco stagioni con conteggi.
- **Versione client** — la build ammessa all'accesso, cambiabile a caldo. Vedi
  [Versione client richiesta](#versione-client-richiesta).
- **Manutenzione** — chiude gli accessi senza spegnere il server. Vedi
  [Manutenzione](#manutenzione).

> Nota sulle quest: "assegnata" conta i giocatori che hanno aperto la taverna quel
> giorno (le righe nascono al primo contatto, non a mezzanotte), e lo storico dei
> giorni passati conta le **riscossioni**: i contatori sono cumulativi, quindi
> rivalutare oggi il completamento di ieri direbbe quanti hanno superato la soglia
> da allora, non entro la giornata.

## Retention

La scheda risponde a una domanda sola: **di cento persone che si registrano, quante
tornano?** È la metrica che moltiplica tutte le altre, perché i giorni giocati per
install decidono insieme ricavi pubblicitari, acquisti e passaparola
(il conto sta in [analisi-mercato-e-ricavi.md](analisi-mercato-e-ricavi.md)).

Una **coorte** è l'insieme degli account creati in un giorno UTC. Il giocatore
"torna al giorno N" se in `login_events` c'è un accesso datato **esattamente** N
giorni dopo la registrazione: il giorno preciso, non «entro N giorni». È la
definizione con cui sono scritti i benchmark del settore, e l'altra è più generosa
di qualche punto.

| | Cosa vedi |
| --- | --- |
| KPI in alto | D1, D7 e D30 medi sulla finestra, con il numero di account su cui sono calcolati |
| Tabella | una riga per giorno di registrazione: dimensione della coorte e i tre valori |
| Finestra | 30 / 60 / 90 / 180 giorni (default 60: con 30 il D30 avrebbe una sola coorte matura) |

Tre scelte che vale la pena conoscere prima di leggere i numeri:

- **Le coorti acerbe mostrano «—», non 0%.** Chi si è registrato ieri non ha ancora
  avuto il suo settimo giorno, e contarlo come "non tornato" schiaccia verso il
  basso qualsiasi media. Una coorte entra nel conto del giorno N solo quando quel
  giorno è **finito**; nella tabella compare comunque, così si vede crescere.
- **Le medie sono pesate sulla dimensione delle coorti**, non sulla media delle
  percentuali: due coorti da 3 e da 1 con un ritorno ciascuna fanno 50%, non 66%.
- **Sotto i 20 account la percentuale resta grigia.** Non è una misura: in una
  coorte da 12 tester una persona vale 8 punti. Sopra quella soglia il colore usa i
  benchmark 2026 del genere — rosso sotto, verde sopra — con una banda per colonna,
  perché un D7 dell'8% è buono quanto un D1 del 30%:

  | | rosso sotto | verde da |
  | --- | --- | --- |
  | D1 | 20% | 30% |
  | D7 | 4% | 8% |
  | D30 | 1% | 3% |

Due avvertenze sulla fonte del dato. La coorte è per **account creato**, non per
install: combacia solo finché ogni giocatore ottiene una riga in `accounts` al primo
avvio. E `login_events` ha una riga per **autenticazione riuscita**: il token di
sessione del client vive solo in memoria, quindi ogni avvio dell'app produce un
login, mentre una riconnessione a metà sessione no — ed è giusto così, conta il
giorno, non quante volte. Dentro ci sono anche gli account di prova.

API: `GET /admin/api/retention?days=60`.

## Azioni sul DB (curate)

Dal dettaglio giocatore: **rinomina**, **imposta miele**, **reset progressi**
(azzera miele/tutorial/hardcore/sblocchi, mantiene lo storico run per le
statistiche), **elimina account** (rimuove il giocatore e tutti i suoi dati,
inclusi i match che lo referenziano — irreversibile).

## Sblocchi a mano (account di prova)

Sempre nel dettaglio giocatore, il riquadro **Sblocchi a mano** concede e revoca
classi, abilità supreme, oggetti, slot bisaccia, capitoli (accesso e completamento),
hardcore e tutorial **senza costo in miele e senza le prove del Santuario**. Serve a
tenere in piedi un account di testing: ogni casella è un interruttore, e i due
bottoni fanno "sblocca tutto" / "blocca tutto". Il "blocca tutto" tocca solo gli
sblocchi — miele, livello e contatori restano dove sono; per azzerare anche quelli
c'è il reset progressi.

Sotto ogni voce è scritto cosa si sta scavalcando (costo e prove), così il pannello
non concede alla cieca.

Dettagli che contano:

- Il gioco vede i cambiamenti alla **prossima sincronizzazione della progressione**
  (rientro nel menu campagna, o riavvio): non c'è push verso un client connesso.
- Le **tre classi base** non si tolgono finché il tutorial risulta completato: il
  server le rimette in lista a ogni lettura, quindi il pannello le mostra spuntate e
  bloccate. Per toglierle si toglie prima il tutorial.
- Dare il **tutorial** consegna anche classi base e primo capitolo, come a fine
  tutorial nel gioco.
- La lista è una whitelist (`Admin/AdminUnlockCatalog.cs`): un id non a catalogo
  viene rifiutato, altrimenti resterebbe per sempre in `single_player_unlocks` senza
  che nulla lo riconosca.

API: `GET /admin/api/players/{id}/unlocks`, `POST .../unlocks`
(`{type,id,granted}`), `POST .../unlocks/all` (`{granted}`). Le POST rispondono col
catalogo aggiornato.

## Scorta consumabili

I consumabili non sono uno sblocco ma una quantità, quindi hanno un riquadro loro nel
dettaglio giocatore: **Scorta consumabili** elenca tutto il catalogo del negozio — non
solo quello che il giocatore ha già — con `−` / campo numerico / `+` per ogni voce, e
tre scorciatoie "1 di tutto", "5 di tutto", "Svuota scorta". Nessun costo in miele.
Tetto di 99 copie per oggetto.

- La quantità mandata al server è **assoluta**, non un delta: due click ravvicinati non
  si sommano e un rinvio della stessa richiesta non raddoppia niente.
- Portare un oggetto a **zero** lo toglie anche dalla **bisaccia**, con la stessa regola
  del consumo in run: altrimenti la bisaccia mostrerebbe uno slot pieno che alla run
  successiva parte vuoto.
- La bisaccia si vede sotto la scorta (con gli slot disponibili) ma **non si modifica**
  da qui: è una scelta del giocatore al Santuario.
- Le righe di `player_consumables` che non stanno più a catalogo (oggetto rinominato o
  rimosso) sono elencate a parte come "fuori catalogo": non sono modificabili voce per
  voce e vanno via solo con "Svuota scorta".
- Come per gli sblocchi, il gioco le legge alla prossima sincronizzazione della
  progressione.

API: `GET /admin/api/players/{id}/stash`, `POST .../stash` (`{itemId,count}`),
`POST .../stash/all` (`{count}`). Anche qui le POST rispondono con lo stato aggiornato.

## Dal telefono

La pagina è la stessa, si riadatta sotto i 640px: barra delle sezioni su una riga sola
che scorre, KPI a due colonne, schede a colonna singola, scheda giocatore a tutto
schermo con la X appiccicata in alto, campi a 16px (sotto, iOS ingrandisce la pagina da
solo al focus). Le tabelle scorrono orizzontalmente nel loro riquadro e le colonne
marcate `.opt` — quelle secondarie, es. fonte, exp totale, data di registrazione —
spariscono sotto i 780px: nessun dato e nessuna azione è raggiungibile solo da desktop.

## Versione client richiesta

La scheda **Versione client** decide quali build possono accedere: chi si presenta
con una versione diversa non entra e resta sulla schermata di login con l'avviso di
aggiornare. Si imposta la versione target (il *bundleVersion* dei Project Settings
Unity, es. `0.9.3`), il link di aggiornamento e l'interruttore del blocco.

Il valore vive nella tabella `server_settings` del DB, **non** in `serverconfig.json`:
quel file viene sovrascritto a ogni deploy del binario, e una versione alzata dal
pannello deve sopravvivere alla pubblicazione successiva. `ClientVersion` in
`serverconfig.json` e la env var `ACCARDND_CLIENT_VERSION` restano i valori di
*avvio*, usati finché nessuno tocca il pannello; "Torna alla configurazione di
avvio" cancella l'override e ci ritorna.

> **Ordine del deploy.** Pubblica prima la build nuova, poi alza la versione qui.
> Al contrario chiudi fuori tutti i giocatori finché la build non è online. La
> versione deve **coincidere esatta**: un client più nuovo del target viene
> respinto quanto uno più vecchio.

Il cambio vale dai login successivi: chi sta già giocando non viene disconnesso.

## Manutenzione

La scheda **Manutenzione** chiude il portone **senza spegnere il server**: acceso
l'interruttore nessun accesso passa più — login Google e riaggancio di sessione
allo stesso modo — e chi bussa resta sulla schermata di login con il popup di
manutenzione. Il messaggio è scrivibile dal pannello (max 240 caratteri, es.
"Torniamo alle 18:00"); lasciandolo vuoto il gioco mostra il proprio testo tradotto.

**Chi è già dentro non viene toccato.** È un *drain*, non uno sfratto: si smette di
far entrare gente e si aspetta che il campo si svuoti. Il popup del client ha un
tasto **Riprova** — il blocco è temporaneo, a differenza di quello di versione che
manda a scaricare la build nuova.

Come per la versione client, lo stato vive in `server_settings` sul DB e non in
`serverconfig.json`: la manutenzione si accende **per** riavviare, quindi deve
sopravvivere al riavvio, o il server riaprirebbe da solo a metà deploy. All'avvio
con la manutenzione attiva il log lo dice a chiare lettere (`Avvio in MANUTENZIONE`).

> Da accesa non entra **nessuno**, e non scade da sola: finché non la spegni il
> gioco è chiuso a tutti. Per questo una banda rossa resta in cima al pannello in
> ogni scheda, con il tasto "Riapri il server" a portata di mano.

### Si può riavviare?

Il riquadro sotto risponde alla domanda vera, e si rinfresca da solo ogni 10s
mentre la scheda è aperta: **match PvP in corso**, **stanze in attesa**,
**collegati ora**.

Il numero che conta è il primo, perché le due modalità reagiscono in modo opposto
a un riavvio:

- **PvP** — una partita vive **solo in memoria** e non si riprende: al riavvio
  viene chiusa da `MatchDrainService` con esito neutro (`server_shutdown`), quindi
  conta come giocata ma **non tocca l'MMR** e non addebita forfeit a nessuno. I due
  giocatori però la perdono. Vale solo per lo spegnimento **pulito** (SIGTERM, cioè
  `systemctl stop/restart`): un `kill -9` o una caduta del VPS non registra niente.
- **Campagna** — regge. La run prosegue offline (le letture vengono dalla cache
  locale dell'ultima istantanea autoritativa) e la ricompensa di fine run passa dal
  `PersistentMutationOutbox`: scritta su disco **prima** di partire, viene rigiocata
  al lancio successivo con lo stesso `requestId`, e il dedup lato server impedisce
  che venga applicata due volte. Nessun giocatore perde miele o sblocchi.

Quindi: accendi la manutenzione, aspetti che i match in corso arrivino a zero,
riavvii, spegni la manutenzione.

API: `GET /admin/api/maintenance`, `POST /admin/api/maintenance`
(`{enabled,message}`). La GET porta anche i contatori del drain; la POST risponde
con lo stato aggiornato.

## Dati "nel tempo"

Due tabelle append-only alimentano i grafici storici, popolate agganciando i
flussi esistenti (nessuna modifica al client Unity):

- `login_events` — una riga per ogni login riuscito (password/UGS/Google).
  `accounts.last_login_at` conserva solo l'ultimo accesso; questa tabella la serie.
- `campaign_runs` — una riga per ogni run di campagna, con
  modalità/capitolo/stanze/nemici/boss/miele. La riga **nasce all'avvio della run**
  (`started_at`) e viene **chiusa** dal sommario della death-reward (`ended_at`).

Lo storico parte dal deploy di questa modifica: gli eventi precedenti non erano
registrati.

## Run iniziate e run concluse

Fino al 2026-08-07 il server sentiva parlare di una run **solo alla fine**, quando
il client chiedeva la ricompensa: chi chiudeva il gioco a metà, restava senza rete
o crashava non lasciava alcuna traccia. Da fuori sembrava che non avesse giocato.

Adesso il client manda `singleplayer.run.started` appena si entra in campagna (lo
stesso `runId` che chiudera' la run), il server apre la riga con `started_at` e la
death-reward la chiude aggiornandola. Ne discende la lettura del pannello:

- **conclusa** — `ended_at` valorizzato: la run è arrivata a morte o vittoria;
- **in corso** — nessuna fine e inizio da meno di due ore: probabilmente ci sta
  giocando qualcuno proprio adesso;
- **abbandonata** — nessuna fine e inizio più vecchio: gioco chiuso a metà.

Limiti da tenere presenti:

- l'avvio **non è persistente**: se il client è offline in quel momento la run
  compare solo alla fine, senza `started_at` (un avvio rispedito ore dopo
  racconterebbe una run cominciata quando invece era già finita);
- una run **ripresa** dopo un riavvio chiude la propria riga perché il `runId` sta
  nel salvataggio; i salvataggi creati prima di questa modifica non ce l'hanno, e
  la loro riga di avvio resta fra le abbandonate;
- le run già in archivio non hanno `started_at`: nel pannello risultano concluse
  senza inizio né durata.

API: `GET /admin/api/runs?status=all|open|ended&limit=&offset=`.
