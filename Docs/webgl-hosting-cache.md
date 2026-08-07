# WebGL su accardndie.com — cache & PWA (niente ri-download ogni volta)

Obiettivo: quando un utente torna sul sito (soprattutto da iPhone) il gioco
**non deve riscaricare** i file pesanti, e deve poter essere "installato" come app.

## La causa del problema (risolta)

Nel template WebGL, `createUnityInstance` aveva:

```js
cacheControl: function (url) { return "no-store"; }
```

`"no-store"` diceva a browser **e** IndexedDB di **non salvare niente**, annullando
di fatto il `Data Caching` di Unity. Per questo si riscaricava tutto a ogni visita.
Ora è:

```js
cacheControl: function (url) {
  if (url.match(/\.(data|wasm|framework\.js|worker\.js|symbols\.json)(\?.*)?$/)) {
    return "immutable";      // serviti da IndexedDB, zero ri-download
  }
  return "must-revalidate";
}
```

Funziona perché gli URL sono già versionati con `?v=<Product Version>`. Quando
alzi la **Product Version** in Unity, l'URL cambia e i client scaricano la build
nuova; finché non la alzi, restano (giustamente) sulla copia in cache.

> ⚠️ **Regola d'oro dei deploy:** alza la Product Version in Unity
> (Player Settings → Player → Version) a ogni pubblicazione, altrimenti i client
> continuano a usare la build in cache.

## PWA (installabile + cache più robusta su iPhone)

Aggiunti al template e alla build:

- `manifest.webmanifest` — nome, icone, `display: standalone`, orientamento landscape.
- `sw.js` — Service Worker che cacha lo *shell* (index.html, TemplateData, loader,
  icone) in Cache Storage; i file pesanti restano gestiti da Unity/IndexedDB per non
  occupare il **doppio** dello spazio su iPhone.
- Icone `TemplateData/icon-192.png`, `icon-512.png`, `icon-512-maskable.png`,
  `apple-touch-icon.png` (generate dall'icona master dell'app).
- Meta `apple-mobile-web-app-*` e `<link rel="manifest">` nell'`<head>`.

Sui file sorgente stanno in `Assets/WebGLTemplates/AccardND/`, quindi **resistono
ai rebuild** di Unity.

Da quando il gioco sta in `/game/`, **anche la PWA sta in `/game/`**: `scope` e
`start_url` del manifest sono relativi, quindi si risolvono li' da soli, e il Service
Worker registrato da `/game/index.html` controlla soltanto `/game/**`. E' la divisione
giusta: l'app installata e' il gioco, mentre homepage, guida e statistiche restano
pagine web normali che devono poter cambiare senza passare da una cache offline.
Chi vuole installare parte quindi da `accardndie.com/game/`, non dalla radice.

### Cosa cambia per l'utente iPhone

- Su Safari, dalla pagina del gioco: **Condividi → Aggiungi a Home**. Parte a schermo intero, con la sua
  icona, e la cache diventa molto più durevole (resiste meglio all'eviction ITP).
- Anche senza installare, i ritorni ravvicinati non riscaricano i file pesanti.

> Nota onesta: iOS può comunque svuotare la cache sotto pressione di memoria o dopo
> ~7 giorni di inutilizzo se il sito **non** è installato a Home. La versione
> installata (standalone) è quella che si avvicina di più a "non scaricare mai più".

## Configurazione nginx

Vedi [`deploy/accardndie-nginx.conf`](deploy/accardndie-nginx.conf). In sintesi:

- `index.html`, `sw.js`, `manifest.webmanifest` → `no-cache` (aggiornamenti immediati).
- `Build/` → `immutable` (URL versionati con `?v=`).
- `TemplateData/` → cache 7 giorni; `StreamingAssets/` → rivalidazione.
- `gzip on` + MIME `application/wasm`.

Applicazione sul VPS:

```bash
# sul server, dentro il server { } di /etc/nginx/sites-available/default
sudo nginx -t
sudo systemctl reload nginx
```

## Come e' organizzato il sito

Dalla riorganizzazione in poi il gioco **non sta piu' in radice**. La radice e' una
homepage vera, con testo, video e un tasto "Gioca"; il WebGL vive sotto `/game/`.

| indirizzo | cosa c'e' | da dove viene |
|---|---|---|
| `/` | homepage | `Docs/web/index.html` |
| `/guida.html`, `/classi.html`, `/carte.html`, `/privacy.html` | pagine di contenuto | `Docs/web/` |
| `/site.css`, `/site.js`, `/media/` | stile, script del tasto Gioca, immagini e video | `Docs/web/` |
| `/game/` | la build WebGL (`Build`, `TemplateData`, `StreamingAssets`, `index.html`, `sw.js`, `manifest`) | `output-web/` |
| `/statistiche` | statistiche del giocatore | server .NET, non e' un file |
| `/ads.txt`, `/oauth2redirect/` | **restano in radice** | template WebGL |
| `/sw.js` | Service Worker "lapide" | `Docs/web/sw.js` |

Le due voci da non spostare mai sono `ads.txt` e `oauth2redirect/`: la prima e'
l'indirizzo che AdSense controlla sul dominio, la seconda e' un URI di
reindirizzamento registrato in Google Cloud Console. Muoverle rompe rispettivamente
gli annunci e il login Google dell'APK.

Il `/sw.js` in radice non e' un doppione di `/game/sw.js`: e' un Service Worker che si
disinstalla da solo, e serve ai browser che hanno visitato il sito quando il gioco
stava in radice. Senza, resterebbero con la vecchia registrazione viva. Vedi il
commento in testa a `Docs/web/sw.js`.

## Deploy della build

Da `cmd`, dalla root del progetto, dopo il build Unity in `output-web/`. Due archivi
perche' vanno in due cartelle diverse sul server:

```bat
del /f /q output-web.zip site.zip 2>nul
tar -a -cf output-web.zip -C output-web Build StreamingAssets TemplateData index.html sw.js manifest.webmanifest
tar -a -cf site.zip --exclude=README.md -C output-web ads.txt oauth2redirect -C ../Docs/web index.html sw.js site.css site.js fonts media privacy.html guida.html classi.html carte.html sitemap.xml robots.txt
scp output-web.zip site.zip root@217.160.212.85:/tmp/
ssh root@217.160.212.85 "rm -rf /var/www/html/* && unzip /tmp/site.zip -d /var/www/html && unzip /tmp/output-web.zip -d /var/www/html/game && rm /tmp/output-web.zip /tmp/site.zip"
```

Cosa c'e' dentro e perche', visto che le righe sono lunghe e ogni pezzo che manca
rompe qualcosa di diverso:

| voce | da dove | dove finisce | se manca |
|---|---|---|---|
| `Build`, `StreamingAssets`, `TemplateData` | build Unity | `/game/` | non parte niente |
| `index.html`, `sw.js`, `manifest.webmanifest` (di `output-web`) | template WebGL | `/game/` | niente PWA, niente cache |
| `ads.txt` | template WebGL | radice | AdSense non serve annunci sul dominio |
| `oauth2redirect` | template WebGL | radice | si rompe il login Google dell'APK |
| `index.html` (di `Docs/web`) | `Docs/web/` | radice | il dominio si apre di nuovo sul vuoto |
| `sw.js` (di `Docs/web`) | `Docs/web/` | radice | i vecchi visitatori restano col Service Worker della vecchia struttura |
| `site.css`, `site.js` | `Docs/web/` | radice | pagine senza stile, tasto "Gioca" che ignora Android e menu del telefono che non si apre |
| `fonts` | `Docs/web/` | radice | i titoli tornano al font di sistema |
| `media` | `Docs/web/` | radice | homepage senza immagini di sfondo, stemmi delle classi mancanti, anteprime social rotte |
| `privacy.html` | `Docs/web/` | radice | manca l'informativa richiesta da Play e da AdSense |
| `guida.html`, `classi.html`, `carte.html` | `Docs/web/` | radice | il dominio torna a essere un canvas senza contenuti, che e' il motivo di rifiuto piu' comune di AdSense |
| `sitemap.xml`, `robots.txt` | `Docs/web/` | radice | i crawler trovano solo la pagina di gioco |

Il `-C ../Docs/web` e' relativo alla cartella dove `tar` si trova in quel momento,
cioe' `output-web/`: si risale di uno e si scende in `Docs/web`. Le pagine di
contenuto stanno li' e non nel template perche' non sono roba di Unity: passare dal
template significherebbe ricopiarle a ogni build e non poterle correggere senza
ricompilare il gioco.

`Docs/web/gen-carte.sh` non e' nella lista: e' lo script che rigenera `carte.html`
leggendo gli asset in `Assets/_Project/Data/Cards/Monster`, e va rilanciato quando si
aggiungono o si modificano carte, altrimenti la pagina resta indietro rispetto al gioco.

L'`--exclude=README.md` serve a `media/`: quella cartella si copia intera (i video di
domani non devono richiedere una modifica a questa riga) ma la nota su come registrarli
e' documentazione interna e non ha motivo di stare su un sito pubblico.

> Il deploy fa `rm -rf /var/www/html/*`: tutto quello che non e' in queste righe
> sparisce dal sito. Aggiungendo un file al template o a `Docs/web`, aggiungilo anche qui.

Verifica:

```bash
curl.exe -I https://accardndie.com/                            # Last-Modified = data deploy
curl.exe -I https://accardndie.com/game/                       # 200, Cache-Control: no-cache
curl.exe -I https://accardndie.com/sw.js                       # 200 (la "lapide"), no-cache
curl.exe -I https://accardndie.com/game/sw.js                  # 200, Cache-Control: no-cache
curl.exe -I https://accardndie.com/game/Build/output-web.wasm  # Cache-Control: ...immutable
curl.exe -I https://accardndie.com/statistiche                 # 200 dal server .NET
curl.exe -s https://accardndie.com/ads.txt                     # la riga google.com, pub-...
```

Poi nel browser: prima visita scarica tutto (normale), dalla **seconda** in poi i
file pesanti arrivano dalla cache locale. Su Chrome puoi controllare in
DevTools → Application → Service Workers / Cache Storage, e in Network la colonna
"Size" deve dire `(disk cache)` / `(ServiceWorker)` invece dei MB.
