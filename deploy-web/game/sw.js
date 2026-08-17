/* AcCard N' Die - Service Worker
 *
 * Scopo: rendere la web app installabile su iPhone/Android (Aggiungi a Home) e
 * cachare in modo durevole lo "shell" (index.html, TemplateData, loader, icone),
 * cosi' i ritorni sul sito sono istantanei e funzionano anche offline.
 *
 * NB: i file PESANTI della build (.data / .wasm / .framework.js) NON vengono
 * duplicati qui: li tiene gia' la cache IndexedDB del loader Unity
 * (cacheControl "immutable" in index.html). Cosi' su iPhone non si occupa il
 * doppio dello spazio. Se un giorno vuoi che sia il Service Worker a gestire
 * anche quei file, togli isHeavyBuildAsset() e in index.html metti "no-store".
 *
 * La cache e' versionata con ?v=<Product Version> passato in fase di register:
 * alzando la Product Version in Unity, la vecchia cache viene ripulita in activate.
 *
 * SCOPE: il file e' servito da /game/sw.js, quindi controlla soltanto /game/**.
 * Le pagine del sito (homepage, guida, statistiche) restano fuori dal Service
 * Worker ed e' voluto: sono HTML leggero che deve poter cambiare senza dipendere
 * da una cache offline. In radice c'e' un /sw.js diverso, che serve solo a
 * disinstallare il Service Worker che stava li' quando il gioco era in radice.
 */

var VERSION = new URL(self.location).searchParams.get('v') || 'dev';
var CACHE = 'accardndie-shell-' + VERSION;

// File grossi della build: lasciati alla rete + cache IndexedDB di Unity.
function isHeavyBuildAsset(href) {
  return /\/Build\/[^?]*\.(data|wasm|framework\.js|worker\.js|symbols\.json)(\?.*)?$/.test(href);
}

// Contenuti DINAMICI serviti dal server .NET sulla stessa origine (pannello
// admin, API JSON): non devono MAI finire in cache, altrimenti la pagina
// continua a mostrare lo snapshot del primo caricamento.
function isDynamicEndpoint(pathname) {
  return pathname === '/ws'
    || pathname === '/admin'
    || pathname.indexOf('/admin/') === 0
    || pathname.indexOf('/api/') === 0;
}

self.addEventListener('install', function () {
  self.skipWaiting();
});

self.addEventListener('activate', function (event) {
  event.waitUntil(
    caches.keys()
      .then(function (keys) {
        return Promise.all(keys.map(function (k) {
          if (k !== CACHE) { return caches.delete(k); }
        }));
      })
      // Bonifica: rimuove le risposte dinamiche gia' finite in cache dalle
      // versioni precedenti del Service Worker (senza attendere un cambio di
      // Product Version).
      .then(function () { return caches.open(CACHE); })
      .then(function (cache) {
        return cache.keys().then(function (requests) {
          return Promise.all(requests.map(function (r) {
            if (isDynamicEndpoint(new URL(r.url).pathname)) { return cache.delete(r); }
          }));
        });
      })
      .then(function () { return self.clients.claim(); })
  );
});

self.addEventListener('fetch', function (event) {
  var req = event.request;
  if (req.method !== 'GET') { return; }

  var url = new URL(req.url);
  if (url.origin !== self.location.origin) { return; }   // solo stessa origine
  if (isDynamicEndpoint(url.pathname)) { return; }        // /ws, /admin, API: sempre dalla rete
  if (isHeavyBuildAsset(url.href)) { return; }            // gestiti da Unity/IndexedDB

  // Navigazione (index.html): rete prima (aggiornamenti immediati), cache di scorta offline.
  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req)
        .then(function (res) {
          var copy = res.clone();
          caches.open(CACHE).then(function (c) { c.put(req, copy); });
          return res;
        })
        .catch(function () {
          return caches.match(req).then(function (m) {
            return m || caches.match('index.html');
          });
        })
    );
    return;
  }

  // Shell (TemplateData, loader.js, manifest, icone): cache prima, poi rete.
  event.respondWith(
    caches.match(req).then(function (cached) {
      if (cached) { return cached; }
      return fetch(req).then(function (res) {
        if (res && res.status === 200 && res.type === 'basic') {
          var copy = res.clone();
          caches.open(CACHE).then(function (c) { c.put(req, copy); });
        }
        return res;
      });
    })
  );
});
