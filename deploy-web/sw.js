/* accardndie.com — Service Worker "lapide" della radice.
 *
 * Questo file NON e' il Service Worker del gioco: quello sta in /game/sw.js e
 * lavora normalmente. Questo qui esiste per un motivo solo, e temporaneo.
 *
 * Fino alla riorganizzazione del sito il gioco era servito dalla radice e il suo
 * Service Worker era registrato con scope "/". Ora la radice e' la homepage e il
 * gioco e' in /game/, ma i browser che hanno gia' visitato il sito continuano ad
 * avere quel vecchio Service Worker installato: senza un /sw.js da scaricare
 * l'aggiornamento fallirebbe con un 404 e la vecchia registrazione resterebbe
 * viva a tempo indeterminato, servendo dalla cache lo shell di un gioco che a
 * quell'indirizzo non c'e' piu'.
 *
 * Quindi qui rispondiamo con un Service Worker che si disinstalla da solo:
 * niente handler di fetch (le richieste passano dritte alla rete), pulizia
 * delle cache vecchie e unregister. Da quel momento la radice torna a essere
 * HTML normale. La cache di /game/ verra' ricostruita alla prima visita al
 * gioco; i file pesanti della build non sono toccati, vivono in IndexedDB.
 *
 * Si potra' cancellare quando sara' ragionevole dare per disinstallati i vecchi
 * Service Worker (indicativamente qualche mese dopo il passaggio a /game/), ma
 * tenerlo non costa niente: e' qualche riga servita una volta sola per browser.
 */

self.addEventListener('install', function () {
  self.skipWaiting();
});

self.addEventListener('activate', function (event) {
  event.waitUntil(
    caches.keys()
      .then(function (keys) {
        return Promise.all(keys.map(function (key) { return caches.delete(key); }));
      })
      .then(function () { return self.registration.unregister(); })
      // Ricarica le pagine ancora controllate dal vecchio worker: senza questo
      // la scheda aperta resta servita dalla cache fino alla prossima visita.
      .then(function () { return self.clients.matchAll({ type: 'window' }); })
      .then(function (clients) {
        clients.forEach(function (client) { client.navigate(client.url); });
      })
      .catch(function () { /* disinstallarsi non deve poter fallire rumorosamente */ })
  );
});
