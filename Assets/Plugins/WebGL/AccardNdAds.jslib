// Bridge verso gli H5 Games Ads di AdSense (Ad Placement API): l'unica rete che
// serve annunci dentro una build WebGL, perche' AdMob sul web non esiste.
//
// Il C# (H5GamesAdProvider) non riceve callback dal browser: apre una richiesta e
// poi ne chiede lo stato a ogni frame, come fa il bridge WebSocket qui accanto.
// E' l'idioma piu' sicuro su WebGL, dove rientrare nel runtime Unity da un
// callback asincrono del browser e' il modo classico di prendersi un crash che
// nei log sembra tutt'altro.
//
// Gli esiti viaggiano come numeri e devono restare allineati alla tabella in
// H5GamesAdProvider: 0 vista, 1 interrotta, 2 nessun annuncio, 3 guasto.
var AccardNdAdsLib = {
  $AccardNdAds: {
    requests: {},
    nextId: 1,

    // 0 = si sta preparando, 1 = pronta, 2 = non utilizzabile qui.
    // Il 2 non e' un caso di scuola: su web un blocco pubblicita' e' la norma, non
    // l'eccezione, e allora lo script di Google non arriva proprio.
    state: 0,
    started: false,

    clientId: function () {
      if (typeof window === 'undefined') return '';
      return window.ACCARDND_ADSENSE_CLIENT_ID || '';
    },

    unavailable: function () { AccardNdAds.state = 2; },

    // adConfig e adBreak sono la stessa cosa: una push nella coda di adsbygoogle,
    // che l'SDK svuota quando arriva. Per questo l'ordine fra il caricamento dello
    // script e le chiamate non conta, e qui non si aspetta niente.
    push: function (options) {
      (window.adsbygoogle = window.adsbygoogle || []).push(options);
    },

    finish: function (request, outcome) {
      if (request.state !== 0) return;
      request.outcome = outcome;
      request.state = 1;
    },

    // Un rewarded paga solo se il premio e' maturato. I due flag valgono piu' del
    // breakStatus perche' arrivano dai callback dedicati; lo stato serve a
    // distinguere il resto, e soprattutto a separare "non c'era niente da mostrare"
    // da "si e' rotto", che al giocatore vanno detti in modo diverso.
    rewardedOutcome: function (request, info) {
      if (request.earned) return 0;
      if (request.dismissed) return 1;
      var status = (info && info.breakStatus) || 'other';
      if (status === 'viewed') return 0;
      if (status === 'dismissed') return 1;
      if (status === 'error') return 3;
      return 2;
    },

    // Un interstitial non ha niente da maturare: mostrato e' mostrato, anche se
    // chiuso al primo istante. E' la stessa lettura che fa AdMobProvider.
    interstitialOutcome: function (info) {
      var status = (info && info.breakStatus) || 'other';
      if (status === 'viewed' || status === 'dismissed') return 0;
      if (status === 'error') return 3;
      return 2;
    }
  },

  /// Configura la sessione pubblicitaria. Una volta sola: le chiamate successive
  /// non fanno niente.
  ///
  /// Lo script di AdSense NON si carica da qui: sta nel <head> di index.html, e
  /// deve restare li'. Il crawler che verifica la proprieta' del sito legge l'HTML
  /// senza aspettare l'avvio di Unity, e due copie dello script sulla stessa pagina
  /// fanno fallire adsbygoogle con "only one AdSense head tag supported per page".
  /// Se il tag manca o un blocco pubblicita' lo fa sparire, la coda non viene mai
  /// svuotata, onReady non arriva e il C# lo dichiara non utilizzabile alla scadenza.
  AccardNdAdsInit: function () {
    if (AccardNdAds.started) return;
    AccardNdAds.started = true;

    if (!AccardNdAds.clientId()) {
      // Nessun publisher id nel template: non e' un guasto, e' una build senza
      // pubblicita'. Si gioca lo stesso, e si evita di restare appesi alla scadenza.
      AccardNdAds.unavailable();
      return;
    }

    try {
      AccardNdAds.push({
        // Con il preload acceso l'SDK si tiene un annuncio pronto da solo: sul web
        // non esiste il Warm per singolo placement che c'e' su AdMob.
        preloadAdBreaks: 'on',
        sound: 'on',
        onReady: function () { AccardNdAds.state = 1; }
      });
    } catch (err) {
      AccardNdAds.unavailable();
    }
  },

  AccardNdAdsState: function () { return AccardNdAds.state; },

  /// Apre una richiesta e restituisce il suo id. Non aspetta niente: l'esito si
  /// legge con AccardNdAdsRequestState.
  AccardNdAdsShow: function (namePtr, rewarded) {
    var name = UTF8ToString(namePtr);
    var id = AccardNdAds.nextId++;
    var request = {
      state: 0, outcome: 3, impression: '', buffer: 0,
      earned: false, dismissed: false
    };
    AccardNdAds.requests[id] = request;

    if (AccardNdAds.state !== 1) {
      AccardNdAds.finish(request, 2);
      return id;
    }

    // Gli H5 Games Ads non restituiscono un identificativo di impressione, quindi
    // se lo fabbrica il client. Serve solo a rendere idempotente la richiesta al
    // nostro server: la corrispondenza con i report di AdSense si perde, ed e' una
    // delle cose che sul web restano peggiori che su AdMob.
    request.impression = 'h5-' + name + '-' + Date.now() + '-'
      + Math.floor(Math.random() * 1000000000);

    try {
      if (rewarded) {
        AccardNdAds.push({
          type: 'reward',
          name: name,
          // Chiamata solo se un annuncio c'e' davvero. Il bottone che ha portato
          // qui diceva gia' cosa si ottiene, quindi non si chiede un secondo
          // consenso: si mostra.
          beforeReward: function (showAdFn) { showAdFn(); },
          adViewed: function () { request.earned = true; },
          adDismissed: function () { request.dismissed = true; },
          adBreakDone: function (info) {
            AccardNdAds.finish(request, AccardNdAds.rewardedOutcome(request, info));
          }
        });
      } else {
        AccardNdAds.push({
          type: 'next',
          name: name,
          adBreakDone: function (info) {
            AccardNdAds.finish(request, AccardNdAds.interstitialOutcome(info));
          }
        });
      }
    } catch (err) {
      AccardNdAds.finish(request, 3);
    }

    return id;
  },

  AccardNdAdsRequestState: function (id) {
    var request = AccardNdAds.requests[id];
    return request ? request.state : 1;
  },

  AccardNdAdsRequestOutcome: function (id) {
    var request = AccardNdAds.requests[id];
    return request ? request.outcome : 3;
  },

  AccardNdAdsRequestImpression: function (id) {
    var request = AccardNdAds.requests[id];
    if (!request || !request.impression) return 0;
    if (request.buffer) _free(request.buffer);
    var size = lengthBytesUTF8(request.impression) + 1;
    request.buffer = _malloc(size);
    stringToUTF8(request.impression, request.buffer, size);
    return request.buffer;
  },

  AccardNdAdsRelease: function (id) {
    var request = AccardNdAds.requests[id];
    if (!request) return;
    if (request.buffer) _free(request.buffer);
    delete AccardNdAds.requests[id];
  }
};

autoAddDeps(AccardNdAdsLib, '$AccardNdAds');
mergeInto(LibraryManager.library, AccardNdAdsLib);
