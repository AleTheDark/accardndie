// Bridge WebSocket per WebGL: espone al C# (PvpServerClient) la WebSocket nativa
// del browser. I messaggi in arrivo si accodano e il C# li tira via a ogni frame
// con AccardNdWsReceive, che restituisce un puntatore a stringa UTF8 (0 = coda vuota).
var AccardNdWebSocketLib = {
  $AccardNdWs: {
    sockets: {},
    nextId: 1
  },

  $AccardNdGoogleAuth: {
    requests: {},
    nextId: 1,
    scriptPromise: null,
    activeRequest: null,
    initializedClientId: '',

    clientId: function () {
      if (typeof window !== 'undefined') {
        if (window.ACCARDND_GOOGLE_CLIENT_ID) return window.ACCARDND_GOOGLE_CLIENT_ID;
        var meta = document.querySelector('meta[name="google-signin-client_id"]');
        if (meta && meta.content) return meta.content;
      }
      return '';
    },

    loadScript: function () {
      if (AccardNdGoogleAuth.scriptPromise) return AccardNdGoogleAuth.scriptPromise;
      AccardNdGoogleAuth.scriptPromise = new Promise(function (resolve, reject) {
        if (window.google && google.accounts && google.accounts.id) {
          resolve();
          return;
        }
        var script = document.createElement('script');
        script.src = 'https://accounts.google.com/gsi/client';
        script.async = true;
        script.defer = true;
        script.onload = resolve;
        script.onerror = function () { reject(new Error('Google Identity Services non caricabile.')); };
        document.head.appendChild(script);
      });
      return AccardNdGoogleAuth.scriptPromise;
    },

    setSuccess: function (request, credential) {
      if (!request || request.state !== 0) return;
      request.state = 1;
      request.credential = credential || '';
    },

    setError: function (request, message) {
      if (!request || request.state !== 0) return;
      request.state = 2;
      request.error = message || 'Login Google non completato.';
    },

    initialize: function (clientId) {
      if (AccardNdGoogleAuth.initializedClientId === clientId) return;
      google.accounts.id.initialize({
        client_id: clientId,
        use_fedcm_for_prompt: true,
        callback: function (response) {
          var request = AccardNdGoogleAuth.activeRequest;
          if (response && response.credential)
            AccardNdGoogleAuth.setSuccess(request, response.credential);
          else
            AccardNdGoogleAuth.setError(request, 'Risposta Google senza ID token.');
        }
      });
      AccardNdGoogleAuth.initializedClientId = clientId;
    }
  },

  AccardNdWsConnect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = AccardNdWs.nextId++;
    try {
      var ws = new WebSocket(url);
      var sock = { ws: ws, queue: [], buffer: 0, state: 0 };
      ws.onopen = function () { sock.state = 1; };
      ws.onmessage = function (e) {
        // Il protocollo è testo (JSON). Se arrivasse un frame binario lo ignoriamo.
        if (typeof e.data === 'string') sock.queue.push(e.data);
      };
      ws.onclose = function () { sock.state = 3; };
      ws.onerror = function () { sock.state = 3; };
      AccardNdWs.sockets[id] = sock;
      return id;
    } catch (err) {
      return -1;
    }
  },

  AccardNdWsState: function (id) {
    var s = AccardNdWs.sockets[id];
    return s ? s.state : 3;
  },

  AccardNdWsSend: function (id, dataPtr) {
    var s = AccardNdWs.sockets[id];
    if (!s || s.state !== 1) return;
    try { s.ws.send(UTF8ToString(dataPtr)); } catch (err) {}
  },

  AccardNdWsReceive: function (id) {
    var s = AccardNdWs.sockets[id];
    if (!s || s.queue.length === 0) return 0;
    var msg = s.queue.shift();
    // Libera il buffer del messaggio precedente (il C# lo ha già letto).
    if (s.buffer) { _free(s.buffer); s.buffer = 0; }
    var size = lengthBytesUTF8(msg) + 1;
    s.buffer = _malloc(size);
    stringToUTF8(msg, s.buffer, size);
    return s.buffer;
  },

  AccardNdWsClose: function (id) {
    var s = AccardNdWs.sockets[id];
    if (!s) return;
    try { s.ws.close(); } catch (err) {}
    if (s.buffer) { _free(s.buffer); s.buffer = 0; }
    delete AccardNdWs.sockets[id];
  },

  AccardNdGoogleSignInStart: function () {
    var id = AccardNdGoogleAuth.nextId++;
    var request = { state: 0, credential: '', error: '', credentialBuffer: 0, errorBuffer: 0 };
    AccardNdGoogleAuth.requests[id] = request;

    try {
      if (window.AccardND && typeof window.AccardND.requestGoogleIdToken === 'function') {
        Promise.resolve(window.AccardND.requestGoogleIdToken())
          .then(function (token) { AccardNdGoogleAuth.setSuccess(request, token); })
          .catch(function (err) { AccardNdGoogleAuth.setError(request, err && err.message); });
        return id;
      }

      var clientId = AccardNdGoogleAuth.clientId();
      if (!clientId) {
        AccardNdGoogleAuth.setError(
          request,
          'Configura window.ACCARDND_GOOGLE_CLIENT_ID o il meta tag google-signin-client_id nel template WebGL.');
        return id;
      }

      AccardNdGoogleAuth.loadScript()
        .then(function () {
          AccardNdGoogleAuth.activeRequest = request;
          AccardNdGoogleAuth.initialize(clientId);
          // Con FedCM (ormai attivato in automatico da Chrome) i metodi di stato del
          // "moment" (isNotDisplayed / isSkippedMoment / getReason...) sono deprecati e
          // riportano valori fuorvianti: il successo 'credential_returned' rientrava dai
          // rami skipped/notDisplayed e veniva scambiato per un errore. L'esito arriva
          // solo dalla callback registrata in initialize(); qui non leggiamo più lo stato.
          google.accounts.id.prompt();
        })
        .catch(function (err) { AccardNdGoogleAuth.setError(request, err && err.message); });
      return id;
    } catch (err) {
      AccardNdGoogleAuth.setError(request, err && err.message);
      return id;
    }
  },

  AccardNdGoogleSignInState: function (id) {
    var request = AccardNdGoogleAuth.requests[id];
    return request ? request.state : 2;
  },

  AccardNdGoogleSignInCredential: function (id) {
    var request = AccardNdGoogleAuth.requests[id];
    if (!request || !request.credential) return 0;
    if (request.credentialBuffer) _free(request.credentialBuffer);
    var size = lengthBytesUTF8(request.credential) + 1;
    request.credentialBuffer = _malloc(size);
    stringToUTF8(request.credential, request.credentialBuffer, size);
    return request.credentialBuffer;
  },

  AccardNdGoogleSignInError: function (id) {
    var request = AccardNdGoogleAuth.requests[id];
    if (!request || !request.error) return 0;
    if (request.errorBuffer) _free(request.errorBuffer);
    var size = lengthBytesUTF8(request.error) + 1;
    request.errorBuffer = _malloc(size);
    stringToUTF8(request.error, request.errorBuffer, size);
    return request.errorBuffer;
  },

  AccardNdGoogleSignInRelease: function (id) {
    var request = AccardNdGoogleAuth.requests[id];
    if (!request) return;
    if (AccardNdGoogleAuth.activeRequest === request)
      AccardNdGoogleAuth.activeRequest = null;
    if (request.credentialBuffer) _free(request.credentialBuffer);
    if (request.errorBuffer) _free(request.errorBuffer);
    delete AccardNdGoogleAuth.requests[id];
  }
};

autoAddDeps(AccardNdWebSocketLib, '$AccardNdWs');
autoAddDeps(AccardNdWebSocketLib, '$AccardNdGoogleAuth');
mergeInto(LibraryManager.library, AccardNdWebSocketLib);
