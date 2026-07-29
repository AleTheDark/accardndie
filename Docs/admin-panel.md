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
# /etc/systemd/system/accardnd-server.service  (dentro [Service])
Environment=ACCARDND_ADMIN_PASSWORD=una-password-robusta
```

Poi `sudo systemctl daemon-reload && sudo systemctl restart accardnd-server`.

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

## Dati "nel tempo"

Due tabelle append-only alimentano i grafici storici, popolate agganciando i
flussi esistenti (nessuna modifica al client Unity):

- `login_events` — una riga per ogni login riuscito (password/UGS/Google).
  `accounts.last_login_at` conserva solo l'ultimo accesso; questa tabella la serie.
- `campaign_runs` — una riga per ogni run di campagna conclusa (morte), con
  modalità/capitolo/stanze/nemici/boss/miele, dal sommario della death-reward.

Lo storico parte dal deploy di questa modifica: gli eventi precedenti non erano
registrati.
