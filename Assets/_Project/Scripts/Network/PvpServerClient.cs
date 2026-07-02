using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Client WebSocket verso il server PvP. I messaggi in arrivo finiscono in una
    /// coda thread-safe: i MonoBehaviour li drenano con TryDequeueMessage in Update.
    /// </summary>
    public sealed class PvpServerClient : IDisposable
    {
        private readonly ConcurrentQueue<Envelope> inbox = new();
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

            var envelope = new Envelope
            {
                type = type,
                payload = payload != null ? JsonUtility.ToJson(payload) : "{}"
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
            await socket.SendAsync(
                new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
        }

        public bool TryDequeueMessage(out Envelope envelope) => inbox.TryDequeue(out envelope);

        public static T ParsePayload<T>(Envelope envelope) where T : class =>
            envelope?.payload != null ? JsonUtility.FromJson<T>(envelope.payload) : null;

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

                    var envelope = JsonUtility.FromJson<Envelope>(message.ToString());
                    message.Clear();
                    if (envelope != null && !string.IsNullOrEmpty(envelope.type))
                        inbox.Enqueue(envelope);
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
    }
}
