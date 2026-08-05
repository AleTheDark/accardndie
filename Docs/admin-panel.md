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

- **Panoramica** — KPI (account, attivi 24h/7g, login, partite PvP, run campagna,
  online ora) e un grafico dell'attività nel tempo (login, registrazioni, partite,
  run) su 7/30/90 giorni.
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
- **Partite PvP** — ultime partite con esito, punteggi, ranked/normale.
- **Stagioni** — elenco stagioni con conteggi.
- **Versione client** — la build ammessa all'accesso, cambiabile a caldo. Vedi
  [Versione client richiesta](#versione-client-richiesta).

> Nota sulle quest: "assegnata" conta i giocatori che hanno aperto la taverna quel
> giorno (le righe nascono al primo contatto, non a mezzanotte), e lo storico dei
> giorni passati conta le **riscossioni**: i contatori sono cumulativi, quindi
> rivalutare oggi il completamento di ieri direbbe quanti hanno superato la soglia
> da allora, non entro la giornata.

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
- Gli **oggetti** sbloccano il diritto di comprarli al negozio: le copie si prendono
  lì col miele (che si imposta con l'azione dedicata).
- La lista è una whitelist (`Admin/AdminUnlockCatalog.cs`): un id non a catalogo
  viene rifiutato, altrimenti resterebbe per sempre in `single_player_unlocks` senza
  che nulla lo riconosca.

API: `GET /admin/api/players/{id}/unlocks`, `POST .../unlocks`
(`{type,id,granted}`), `POST .../unlocks/all` (`{granted}`). Le POST rispondono col
catalogo aggiornato.

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

## Dati "nel tempo"

Due tabelle append-only alimentano i grafici storici, popolate agganciando i
flussi esistenti (nessuna modifica al client Unity):

- `login_events` — una riga per ogni login riuscito (password/UGS/Google).
  `accounts.last_login_at` conserva solo l'ultimo accesso; questa tabella la serie.
- `campaign_runs` — una riga per ogni run di campagna conclusa (morte), con
  modalità/capitolo/stanze/nemici/boss/miele, dal sommario della death-reward.

Lo storico parte dal deploy di questa modifica: gli eventi precedenti non erano
registrati.
