# Login Google su Android (senza Google Play Games)

## Perché è cambiato

Unity Authentication tratta **"Sign in with Google"** e **"Google Play Games"** come
due provider distinti. UGS crea un giocatore per ogni coppia *(provider, id esterno)*,
quindi la stessa persona con la stessa mail otteneva:

- `PlayerId` A entrando dalla PWA (provider `google.com`),
- `PlayerId` B entrando dall'APK (provider `google-play-games`).

Due `PlayerId` = due account nel nostro DB, con nickname, progressi e statistiche
separati. Per questo GPGS è stato **tolto dal flusso di login**: ora tutte le
piattaforme passano dal provider `google.com`.

## Come funziona ora

L'APK non parla direttamente con Google. Un client OAuth di tipo "Android"
produrrebbe un ID token con *audience* diversa da quella configurata sul provider
Google di UGS (che è il **Web Client ID**), e Google non accetta redirect loopback
per i client Android. Quindi lo scambio lo fa il nostro server, con lo stesso Web
Client ID del login web:

```
App                       Server (.NET)                  Google
 |  POST /auth/google/begin    |                            |
 |  { challenge = SHA256(v) }  |                            |
 |---------------------------->|  crea requestId + state    |
 |  { requestId, authorizeUrl }|                            |
 |<----------------------------|                            |
 |  apre il browser di sistema ------------------------------>|
 |                             |   GET /auth/google/callback |
 |                             |<---------------------------- |
 |                             |   scambia code -> id_token   |
 |                             |----------------------------->|
 |  POST /auth/google/token    |                            |
 |  { requestId, verifier v }  |                            |
 |---------------------------->|  verifica SHA256(v)        |
 |  { status: ready, idToken } |  consegna una volta sola   |
 |<----------------------------|                            |
 |  AuthenticationService.SignInWithGoogleAsync(idToken)     |
```

- Il **client secret non lascia mai il server**.
- L'app dimostra di essere quella che ha aperto il browser presentando un
  `verifier` monouso di cui, all'avvio, aveva mandato solo l'hash.
- Tra server e Google c'è PKCE: un codice di autorizzazione rubato altrove non è
  spendibile sul nostro callback.
- Le richieste in attesa scadono dopo `RequestTtlMinutes` (default 10) e il token
  si ritira **una volta sola**.

Codice: [`GoogleOAuthBroker.cs`](../Server/AccardND.Server/Accounts/GoogleOAuthBroker.cs),
[`GoogleAuthEndpoints.cs`](../Server/AccardND.Server/Accounts/GoogleAuthEndpoints.cs),
lato client `SignInWithBrokeredGoogleAsync` in
[`PvpUgsAuth.cs`](../Assets/_Project/Scripts/PvpUi/PvpUgsAuth.cs).

## Setup (da fare una volta)

### 1. Google Cloud Console

Sul **client OAuth Web** già usato dal login del browser
(`866249556431-mgdm97uvov7mjvect4bp453dpp2oe48u.apps.googleusercontent.com`),
aggiungere agli *URI di reindirizzamento autorizzati*:

```
https://accardndie.com/auth/google/callback
```

Non serve creare un client Android: è proprio quello che vogliamo evitare.

### 2. VPS — client secret

Il broker resta **spento** finché non trova il secret, e in quel caso Android
ripiega sull'account locale. Aggiungere al service unit del server .NET:

```ini
# /etc/systemd/system/accardnd-server.service  (dentro [Service])
Environment=ACCARDND_GOOGLE_CLIENT_SECRET=il-secret-del-web-client
```

Poi `sudo systemctl daemon-reload && sudo systemctl restart accardnd-server`.

> Il secret **non va committato**. `serverconfig.json` è versionato: il campo
> `GoogleOAuth.ClientSecret` esiste solo come ripiego per lo sviluppo locale.

### 3. nginx

Aggiungere il blocco `location /auth/` di
[`deploy/accardndie-nginx.conf`](deploy/accardndie-nginx.conf) dentro il `server { }`,
poi `sudo nginx -t && sudo systemctl reload nginx`. Senza questo blocco Google
atterra su nginx (che serve i file statici) e l'app resta in attesa a vuoto.

### 4. Unity Cloud

Il provider **Sign in with Google** è già configurato con lo stesso client ID:
niente da cambiare. Il provider **Google Play Games** può essere rimosso dalla
dashboard *dopo* aver migrato gli account (vedi sotto), così non può ricreare
identità duplicate.

### 5. Verifica

```bash
curl -s -X POST https://accardndie.com/auth/google/begin \
  -H 'Content-Type: application/json' \
  -d '{"challenge":"E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"}'
```

Deve rispondere con `requestId` e `authorizeUrl`. Se risponde
`{"error":"Login Google non configurato sul server."}` manca il secret (punto 2);
se risponde HTML o 404 manca il blocco nginx (punto 3).

## Solo account Google

Dal 2026-07-29 l'unico modo di avere un account è il login Google. Le altre due
strade sono chiuse:

- **Account con password / ospite.** Il server li rifiuta:
  `"AllowPasswordAuth": false` in `serverconfig.json`. È l'interruttore
  autoritativo, vale anche per i client vecchi. Lato client sono spariti sia il
  ripiego di `PvpBootstrap` (che registrava un account locale quando il login
  Google falliva) sia le credenziali ospite legate al dispositivo di
  `SinglePlayerServerLink`; `GuestCredentials` è stato eliminato.
- **Anonimo UGS.** Il metodo pubblico `SignInAnonymouslyAsync` non c'è più.
  Attenzione: dentro `TryResumeSessionAsync` la chiamata
  `AuthenticationService.SignInAnonymouslyAsync()` **resta e non va tolta** — con
  un session token in cache non crea un utente anonimo, ripristina il giocatore
  già collegato. È il meccanismo di resume ufficiale di UGS.

Conseguenza da conoscere: chi arriva al single player **senza** una sessione
account non ottiene più un profilo silenzioso sul server, e la progressione resta
locale (`SinglePlayerServerLink` restituisce null). In pratica non capita, perché
la schermata di login fa da porta d'ingresso; ma se un giorno si volesse un
"gioca senza account", va progettato apposta, non riattivando gli account password.

## La mail nel pannello admin

Dal 2026-07-29 il client manda al server, insieme al token UGS, anche l'**ID token
Google** del login appena fatto. Il server ne verifica la firma contro le chiavi
pubbliche di Google (`GoogleIdTokenReader`), controlla `email_verified` ed estrae
la mail, che finisce in `external_identities.email` e compare nel pannello admin
sotto la fonte. La ricerca giocatori cerca anche per mail.

Due limiti da tenere a mente:

- **Solo dai login nuovi.** La mail non è mai stata salvata prima, quindi le righe
  già esistenti restano vuote finché quel giocatore non rifà un accesso Google.
  Sui resume di sessione l'ID token non c'è e la mail già salvata non viene toccata.
- **Gli account Google Play Games non hanno una mail, punto.** Play Games
  restituisce un id giocatore, non l'indirizzo: per quelle righe la mail non è
  recuperabile in nessun modo, né ora né a posteriori. Per abbinarle si può usare
  solo il nome.

## Migrazione degli account già duplicati

Le vecchie sessioni con identità `google-play-games` non vengono più accettate:
`IsCurrentSessionGoogle()` riconosce solo `google`, quindi al primo avvio la
sessione viene scartata e l'utente rifà l'accesso con Google. Il suo **vecchio
account GPGS resta però nel DB** e continua a occupare il nickname.

### Se il doppione non ha progressi

Basta il [pannello admin](admin-panel.md): elimina il doppione GPGS. La
cancellazione libera anche il **nickname** (`account_nicknames`), che è la cosa
che impedirebbe al nuovo account Google di riprendersi lo stesso nome. Il
giocatore rientra con Google e riprende il suo nome.

### Se il doppione ha progressi da salvare

Si fondono i due account con [`deploy/merge-accounts.sql`](deploy/merge-accounts.sql).
L'ordine conta, perché il `player_id` di un account Google nasce da Unity
Authentication al primo accesso e non è prevedibile:

1. Il giocatore entra con Google e si prende un **nickname provvisorio** (il suo è
   ancora occupato dal doppione).
2. Trovi i due `player_id` nel pannello admin.
3. Fermi il servizio, fai il backup del DB, esegui lo script con i due id.
4. Riavvii: il superstite ha miele, esperienza, statistiche, ranked, sblocchi,
   storico e il **nickname originale**.

Lo script somma quel che è sommabile (miele, partite, contatori, consumabili),
tiene il massimo dove sommare non avrebbe senso (best streak, peak MMR, livello
account) e non tocca niente se uno dei due `player_id` non esiste.

Per vedere i doppioni in un colpo solo, direttamente sul DB del VPS:

```sql
SELECT a.username, a.player_id, a.source, ei.auth_method, a.last_login_at,
       COALESCE(sp.honey, 0) AS miele, COALESCE(st.matches, 0) AS match_giocati
FROM accounts a
LEFT JOIN external_identities ei ON ei.player_id = a.player_id
LEFT JOIN single_player_progress sp ON sp.player_id = a.player_id
LEFT JOIN player_stats st ON st.player_id = a.player_id AND st.scope = 'lifetime'
WHERE a.username_ci IN (
    SELECT username_ci FROM accounts GROUP BY username_ci HAVING COUNT(*) > 1
)
ORDER BY a.username_ci, a.last_login_at DESC;
```

> Se in futuro capitasse un doppione **con progressi da entrambe le parti**, non
> esiste ancora una fusione automatica: andrebbe scritta (trasferimento di miele,
> unlock, statistiche, ranked, storico partite e nickname) prima di cancellare
> qualcosa.
