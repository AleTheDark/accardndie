// Bridge WebSocket per WebGL: espone al C# (PvpServerClient) la WebSocket nativa
// del browser. I messaggi in arrivo si accodano e il C# li tira via a ogni frame
// con AccardNdWsReceive, che restituisce un puntatore a stringa UTF8 (0 = coda vuota).
var AccardNdWebSocketLib = {
  $AccardNdWs: {
    sockets: {},
    nextId: 1
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
  }
};

autoAddDeps(AccardNdWebSocketLib, '$AccardNdWs');
mergeInto(LibraryManager.library, AccardNdWebSocketLib);
