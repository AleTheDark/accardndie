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

### Cosa cambia per l'utente iPhone

- Su Safari: **Condividi → Aggiungi a Home**. Parte a schermo intero, con la sua
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

## Deploy della build

Da `cmd`, dalla root del progetto, dopo il build Unity in `output-web/`:

```bat
del /f /q output-web.zip 2>nul
tar -a -cf output-web.zip -C output-web Build StreamingAssets TemplateData index.html sw.js manifest.webmanifest ads.txt oauth2redirect -C ../Docs/web privacy.html
scp output-web.zip root@217.160.212.85:/tmp/
ssh root@217.160.212.85 "rm -rf /var/www/html/* && unzip /tmp/output-web.zip -d /var/www/html && rm /tmp/output-web.zip"
```

Cosa c'e' dentro e perche', visto che la riga e' lunga e ogni pezzo che manca rompe
qualcosa di diverso:

| voce | da dove | se manca |
|---|---|---|
| `Build`, `StreamingAssets`, `TemplateData` | build Unity | non parte niente |
| `index.html`, `sw.js`, `manifest.webmanifest` | template WebGL | niente PWA, niente cache |
| `ads.txt` | template WebGL | AdSense non serve annunci sul dominio |
| `oauth2redirect` | template WebGL | si rompe il login Google |
| `privacy.html` | `Docs/web/`, **fuori** da `output-web` | manca l'informativa richiesta da Play e da AdSense |

Il secondo `-C ../Docs/web` e' relativo alla cartella dove `tar` si trova in quel
momento, cioe' `output-web/`: si risale di uno e si scende in `Docs/web`. La pagina
privacy sta li' e non nel template perche' non e' roba di Unity, e passare dal
template significherebbe rigenerarla a ogni build.

> Il deploy fa `rm -rf /var/www/html/*`: tutto quello che non e' in questa riga
> sparisce dal sito. Aggiungendo un file al template, aggiungilo anche qui.

Verifica:

```bash
curl.exe -I https://accardndie.com/                       # Last-Modified = data deploy
curl.exe -I https://accardndie.com/sw.js                  # Cache-Control: no-cache
curl.exe -I https://accardndie.com/Build/output-web.wasm  # Cache-Control: ...immutable
curl.exe -s https://accardndie.com/ads.txt                # la riga google.com, pub-...
```

Poi nel browser: prima visita scarica tutto (normale), dalla **seconda** in poi i
file pesanti arrivano dalla cache locale. Su Chrome puoi controllare in
DevTools → Application → Service Workers / Cache Storage, e in Network la colonna
"Size" deve dire `(disk cache)` / `(ServiceWorker)` invece dei MB.
