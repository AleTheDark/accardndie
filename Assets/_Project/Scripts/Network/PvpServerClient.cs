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

        public bool TryDequeueMessage(out Envelope envelope)
        {
            PumpIncoming();
            return inbox.TryDequeue(out envelope);
        }

        public static T ParsePayload<T>(Envelope envelope) where T : class =>
            envelope?.payload != null ? JsonUtility.FromJson<T>(envelope.payload) : null;

        /// <summary>Doppia codifica: payload tipizzato dentro la busta di trasporto.</summary>
        private static string BuildEnvelopeJson(string type, object payload)
        {
            var envelope = new Envelope
            {
                type = type,
                payload = payload != null ? JsonUtility.ToJson(payload) : "{}"
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

        public bool IsConnected => socket is { State: WebSocketState.Open };

        public async Task ConnectAsync(string url)
        {
            if (IsConnected)
                return;

            socket = new ClientWebSocket();
            lifetime = new CancellationTokenSource();
            await socket.ConnectAsync(new Uri(url), lifetime.Token);
            _ = ReceiveLoopAsync(lifetime.Token);
        }

        public async Task SendAsync(string type, object payload = null)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Client non connesso.");

            byte[] bytes = Encoding.UTF8.GetBytes(BuildEnvelopeJson(type, payload));
            await socket.SendAsync(
                new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
        }

        /// <summary>Su desktop il receive loop gira in background: qui non serve pompare.</summary>
        private void PumpIncoming()
        {
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellation)
        {
            var buffer = new byte[16 * 1024];
            var message = new StringBuilder();
            try
            {
                while (!cancellation.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(
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
                // Connessione persa o chiusura volontaria: la coda smette di riempirsi.
            }
        }

        public void Dispose()
        {
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

        public async Task ConnectAsync(string url)
        {
            if (IsConnected)
                return;

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
                    return;
                }
                if (state == StateClosed)
                    throw new InvalidOperationException("Connessione al server fallita.");
                if (Time.realtimeSinceStartup - start > 10f)
                    throw new TimeoutException("Timeout di connessione al server.");
                await PvpAsync.NextFrameAsync();
            }
        }

        public Task SendAsync(string type, object payload = null)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Client non connesso.");

            AccardNdWsSend(socketId, BuildEnvelopeJson(type, payload));
            return Task.CompletedTask;
        }

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
            if (socketId >= 0)
                AccardNdWsClose(socketId);
            socketId = -1;
            opened = false;
        }
#endif
    }
}
