/* accardndie.com — comportamenti condivisi delle pagine statiche.
 *
 * Quattro cose, tutte piccole, tutte legate al fatto che il sito e' letto in
 * larghissima maggioranza da telefono:
 *
 *   1. il tasto "Gioca" su Android porta alla scheda Play Store invece che al
 *      WebGL: il gioco nel browser su Android gira, ma l'app nativa gira meglio
 *      e resta installata;
 *   2. sul telefono i link delle sezioni si raccolgono dietro un pulsante, cosi'
 *      la barra resta una riga sola invece di occuparne tre;
 *   3. i video-vetrina partono solo quando si vedono davvero, e si fermano
 *      quando escono dallo schermo, per non bruciare dati e batteria;
 *   4. chi ha chiesto meno animazioni al sistema operativo vede l'immagine
 *      ferma con un tasto play, non un loop che parte da solo.
 *
 * Niente framework e niente build: e' un file servito com'e'.
 */
(function () {
  'use strict';

  // Cambiare qui, non nelle pagine: i tasti "Gioca" sono piu' di uno.
  var PLAY_STORE_URL = 'https://play.google.com/store/apps/details?id=com.apesolution.accardndie';
  var GAME_URL = '/game/';

  // Android e basta. Gli iPhone non hanno una build nativa da offrire, e i
  // desktop devono restare sul WebGL: mandarli sul Play Store sarebbe un vicolo
  // cieco. "Android" senza "Windows" esclude i vecchi user agent di Windows
  // Phone, che si dichiaravano Android per compatibilita'.
  function isAndroidPhone() {
    var agent = navigator.userAgent || '';
    return /Android/i.test(agent) && !/Windows/i.test(agent);
  }

  /* --- 1. Il tasto Gioca ---------------------------------------------------
   * L'HTML scrive sempre il link al WebGL: e' la destinazione giusta per la
   * maggioranza dei visitatori ed e' quella che un crawler deve vedere. Su
   * Android lo riscriviamo qui. Gli elementi da riscrivere si dichiarano con
   * data-play; data-play-label, se c'e', e' il testo alternativo da mostrare.
   */
  function retargetPlayButtons() {
    if (!isAndroidPhone()) { return; }

    var buttons = document.querySelectorAll('[data-play]');
    for (var i = 0; i < buttons.length; i++) {
      var button = buttons[i];
      button.setAttribute('href', PLAY_STORE_URL);
      button.setAttribute('rel', 'noopener');

      var label = button.getAttribute('data-play-label');
      var slot = button.querySelector('[data-play-text]') || button;
      if (label) { slot.textContent = label; }
    }

    // Le note che spiegano dove porta il tasto: una versione per il browser e
    // una per il Play Store, cosi' non serve JavaScript per scrivere frasi.
    var notes = document.querySelectorAll('[data-play-note]');
    for (var n = 0; n < notes.length; n++) {
      notes[n].hidden = notes[n].getAttribute('data-play-note') !== 'android';
    }
  }

  /* --- 2. Il menu del telefono --------------------------------------------
   * Il pannello e' costruito qui e non nell'HTML per una ragione precisa: se
   * questo file non arriva, i link devono restare visibili. Percio' l'HTML li
   * serve gia' aperti, e la classe .js-menu - che e' quella a cui il CSS aggancia
   * il pannello a scomparsa - la mette solo JavaScript, dopo aver acceso il
   * pulsante che lo apre. Nessuno script, nessuna gabbia.
   *
   * Il pulsante e' nell'HTML con hidden: cosi' esiste gia' e non serve inventare
   * markup qui dentro, ma su desktop e senza JS non si vede.
   */
  function setUpMenu() {
    var nav = document.querySelector('.nav');
    if (!nav) { return; }

    var toggle = nav.querySelector('.menu-toggle');
    var links = nav.querySelector('.menu-links');
    if (!toggle || !links) { return; }

    toggle.hidden = false;
    nav.classList.add('js-menu');

    // Il pannello parte chiuso, ma solo dove il CSS lo tratta da pannello: su
    // desktop .menu-links e' la riga di link, e nasconderla sarebbe un disastro.
    // Stessa soglia della regola in site.css: se divergono, il pulsante compare
    // dove i link sono ancora visibili, o viceversa.
    var phone = window.matchMedia('(max-width: 58rem)');
    function applyLayout() {
      links.hidden = phone.matches && toggle.getAttribute('aria-expanded') !== 'true';
    }

    function setOpen(open) {
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      applyLayout();
    }

    setOpen(false);
    phone.addEventListener('change', applyLayout);

    toggle.addEventListener('click', function () {
      setOpen(toggle.getAttribute('aria-expanded') !== 'true');
    });

    // Chiudere toccando fuori e con Esc: un pannello che resta aperto sopra la
    // pagina finche' non ricentri il pulsante e' il difetto classico di questi menu.
    document.addEventListener('click', function (event) {
      if (!nav.contains(event.target)) { setOpen(false); }
    });
    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape') { setOpen(false); }
    });
  }

  /* --- 3 e 4. I video-vetrina ---------------------------------------------
   * I <video> hanno gia' muted/loop/playsinline nell'HTML (senza muted il
   * browser blocca l'autoplay) ma NON autoplay: lo decidiamo qui, altrimenti
   * quattro video partirebbero insieme al caricamento anche sotto rete mobile.
   */
  function activateShowreel() {
    var videos = document.querySelectorAll('video[data-showreel]');
    if (videos.length === 0) { return; }

    var reducedMotion = window.matchMedia &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reducedMotion) {
      // Resta il poster, con i controlli per chi vuole guardarlo lo stesso.
      for (var r = 0; r < videos.length; r++) {
        videos[r].controls = true;
      }
      return;
    }

    // Senza IntersectionObserver (browser molto vecchi) si parte e basta: e'
    // il comportamento di prima, non un errore.
    if (!('IntersectionObserver' in window)) {
      for (var f = 0; f < videos.length; f++) {
        videos[f].play().catch(function () {});
      }
      return;
    }

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        var video = entry.target;
        if (entry.isIntersecting) {
          // play() torna una promise rifiutata se il browser blocca comunque
          // l'autoplay: in quel caso resta il poster, che e' un'immagine del
          // gioco e non un buco nella pagina.
          video.play().catch(function () { video.controls = true; });
        } else {
          video.pause();
        }
      });
    }, { threshold: 0.25 });

    for (var v = 0; v < videos.length; v++) {
      observer.observe(videos[v]);
    }
  }

  function start() {
    retargetPlayButtons();
    setUpMenu();
    activateShowreel();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
