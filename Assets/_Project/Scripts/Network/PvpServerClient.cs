using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AccardND.NetProtocol;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Text;
using System.Threading;
#else
using System.Runtime.InteropServices;
#endif

namespace AccardND.Network
{
    /// <summary>
    /// Client WebSocket verso il server PvP. I messaggi in arrivo finiscono in una
    /// coda: i MonoBehaviour li drenano con TryDequeueMessage in Update.
    ///
    /// Due implementazioni con la stessa interfaccia:
    /// - Editor/Android/Windows: System.Net.WebSockets.ClientWebSocket.
    /// - WebGL: la WebSocket nativa del browser via il bridge AccardNdWebSocket.jslib
    ///   (nel browser non esistono socket/thread .NET).
    /// </summary>
    public sealed class PvpServerClient : IDisposable
    {
        private readonly ConcurrentQueue<Envelope> inbox = new();

        /// <summary>
        /// La caduta del socket viene segnalata una volta sola e sul thread di chi
        /// pompa (cioè il main thread di Unity): il receive loop gira in background e
        /// da lì non si possono toccare né UI né API di Unity.
        /// </summary>
        private bool disconnectReported = true;

        public bool TryDequeueMessage(out Envelope envelope)
        {
            PumpIncoming();
            return inbox.TryDequeue(out envelope);
        }

        /// <summary>
        /// true una volta sola, alla prima chiamata dopo che il socket è caduto.
        /// Chiudere di proposito (Dispose) non conta come caduta.
        /// </summary>
        public bool PollDisconnected()
        {
            PumpIncoming();
            if (disconnectReported || !HasClosed)
                return false;
            disconnectReported = true;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Finge che la rete non ci sia: finché resta acceso ogni tentativo di
        /// connessione fallisce. Serve a guardare in faccia il backoff, il badge e la
        /// coda delle mutazioni senza dover staccare davvero il Wi-Fi - cosa che in
        /// editor, con il server sulla stessa macchina, non si può nemmeno fare.
        /// Non esiste nelle build di produzione.
        /// </summary>
        public static bool SimulatedOutage { get; set; }
#endif

        public static T ParsePayload<T>(Envelope envelope) where T : class =>
            envelope?.payload != null ? JsonUtility.FromJson<T>(envelope.payload) : null;

        /// <summary>Doppia codifica: payload tipizzato dentro la busta di trasporto.</summary>
        private static string BuildEnvelopeJson(string type, object payload, string requestId)
        {
            var envelope = new Envelope
            {
                type = type,
                payload = payload != null ? JsonUtility.ToJson(payload) : "{}",
                requestId = requestId
            };
            return JsonUtility.ToJson(envelope);
        }

        private void EnqueueIncoming(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return;
            var envelope = JsonUtility.FromJson<Envelope>(raw);
            if (envelope != null && !string.IsNullOrEmpty(envelope.type))
                inbox.Enqueue(envelope);
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private ClientWebSocket socket;
        private CancellationTokenSource lifetime;
        private volatile bool socketClosed;

        public bool IsConnected => socket is { State: WebSocketState.Open };

        private bool HasClosed => socketClosed;

        public async Task ConnectAsync(string url)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SimulatedOutage)
                throw new InvalidOperationException("Rete assente (simulata dal menu di debug).");
#endif
            if (IsConnected)
                return;

            // La stessa istanza viene riusata dalla riconnessione: il socket vecchio
            // va rilasciato, non lasciato appeso.
            lifetime?.Cancel();
            lifetime?.Dispose();
            socket?.Dispose();

            socket = new ClientWebSocket();
            lifetime = new CancellationTokenSource();
            socketClosed = false;
            await socket.ConnectAsync(new Uri(url), lifetime.Token);
            disconnectReported = false;
            _ = ReceiveLoopAsync(socket, lifetime.Token);
        }

        public async Task SendAsync(string type, object payload = null, string requestId = null)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Client non connesso.");

            byte[] bytes = Encoding.UTF8.GetBytes(BuildEnvelopeJson(type, payload, requestId));
            await socket.SendAsync(
                new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
        }

        /// <summary>Su desktop il receive loop gira in background: qui non serve pompare.</summary>
        private void PumpIncoming()
        {
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Stronca il socket come farebbe un tunnel o un cambio di rete. Abort e non
        /// Dispose di proposito: il receive loop muore come in una caduta vera e la
        /// notizia passa dalla strada normale (PollDisconnected), quindi si prova il
        /// percorso di produzione e non una scorciatoia.
        /// </summary>
        public void SimulateConnectionLoss() => socket?.Abort();
#endif

        private async Task ReceiveLoopAsync(ClientWebSocket owner, CancellationToken cancellation)
        {
            var buffer = new byte[16 * 1024];
            var message = new StringBuilder();
            try
            {
                while (!cancellation.IsCancellationRequested && owner.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await owner.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellation);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage)
                        continue;

                    EnqueueIncoming(message.ToString());
                    message.Clear();
                }
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
            {
                // Connessione persa o chiusura volontaria.
            }
            finally
            {
                // Solo se e' ancora il socket corrente: un loop vecchio non deve
                // dichiarare caduta la connessione appena riaperta.
                if (ReferenceEquals(owner, socket))
                    socketClosed = true;
            }
        }

        public void Dispose()
        {
            disconnectReported = true;
            lifetime?.Cancel();
            socket?.Dispose();
            lifetime?.Dispose();
            socket = null;
            lifetime = null;
        }
#else
        // --- Percorso WebGL: bridge verso la WebSocket del browser ---
        [DllImport("__Internal")] private static extern int AccardNdWsConnect(string url);
        [DllImport("__Internal")] private static extern int AccardNdWsState(int id);
        [DllImport("__Internal")] private static extern void AccardNdWsSend(int id, string data);
        [DllImport("__Internal")] private static extern IntPtr AccardNdWsReceive(int id);
        [DllImport("__Internal")] private static extern void AccardNdWsClose(int id);

        // readyState del browser: 0 CONNECTING, 1 OPEN, 2 CLOSING, 3 CLOSED.
        private const int StateOpen = 1;
        private const int StateClosed = 3;

        private int socketId = -1;
        private bool opened;

        public bool IsConnected => opened && socketId >= 0 && AccardNdWsState(socketId) == StateOpen;

        /// <summary>Caduta solo se il socket era stato aperto davvero: un connect fallito lo segnala da sé.</summary>
        private bool HasClosed => opened && (socketId < 0 || AccardNdWsState(socketId) == StateClosed);

        public async Task ConnectAsync(string url)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SimulatedOutage)
                throw new InvalidOperationException("Rete assente (simulata dal menu di debug).");
#endif
            if (IsConnected)
                return;

            // Riconnessione sulla stessa istanza: chiudi il socket precedente.
            if (socketId >= 0)
                AccardNdWsClose(socketId);
            opened = false;

            socketId = AccardNdWsConnect(url);
            if (socketId < 0)
                throw new InvalidOperationException("Impossibile creare la WebSocket nel browser.");

            // Niente thread nel browser: attendo l'apertura pompando il loop principale.
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                int state = AccardNdWsState(socketId);
                if (state == StateOpen)
                {
                    opened = true;
                    disconnectReported = false;
                    return;
                }
                if (state == StateClosed)
                    throw new InvalidOperationException("Connessione al server fallita.");
                if (Time.realtimeSinceStartup - start > 10f)
                    throw new TimeoutException("Timeout di connessione al server.");
                await PvpAsync.NextFrameAsync();
            }
        }

        public Task SendAsync(string type, object payload = null, string requestId = null)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Client non connesso.");

            AccardNdWsSend(socketId, BuildEnvelopeJson(type, payload, requestId));
            return Task.CompletedTask;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Chiude la WebSocket del browser lasciando acceso il flag "era aperta": è
        /// esattamente lo stato di una caduta vera, e la caduta viene notata da
        /// PollDisconnected come sempre.
        /// </summary>
        public void SimulateConnectionLoss()
        {
            if (socketId >= 0)
                AccardNdWsClose(socketId);
        }
#endif

        /// <summary>Nel browser i messaggi vengono raccolti in JS: qui li tiro in coda a ogni frame.</summary>
        private void PumpIncoming()
        {
            if (socketId < 0)
                return;

            IntPtr ptr;
            while ((ptr = AccardNdWsReceive(socketId)) != IntPtr.Zero)
                EnqueueIncoming(Marshal.PtrToStringUTF8(ptr));
        }

        public void Dispose()
        {
            disconnectReported = true;
            if (socketId >= 0)
                AccardNdWsClose(socketId);
            socketId = -1;
            opened = false;
        }
#endif
    }
}
