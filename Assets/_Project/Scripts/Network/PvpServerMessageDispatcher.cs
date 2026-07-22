using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Drena PvpServerClient in un solo punto e permette a feature diverse di attendere
    /// risposte specifiche senza contendersi la coda WebSocket.
    /// </summary>
    public sealed class PvpServerMessageDispatcher
    {
        private sealed class PendingRequest
        {
            public string ExpectedType;
            public float Deadline;
            public TaskCompletionSource<Envelope> Completion;
        }

        private readonly PvpServerClient client;
        private readonly List<PendingRequest> pending = new();

        public PvpServerMessageDispatcher(PvpServerClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public event Action<Envelope> UnhandledMessage;

        public async Task SendAsync(string type, object payload = null)
        {
            await client.SendAsync(type, payload);
        }

        public async Task<Envelope> RequestAsync(
            string requestType,
            object payload,
            string expectedResponseType,
            float timeoutSeconds = 8f)
        {
            if (!client.IsConnected)
                throw new InvalidOperationException("Client server non connesso.");
            if (string.IsNullOrEmpty(expectedResponseType))
                throw new ArgumentException("Tipo risposta attesa mancante.", nameof(expectedResponseType));

            var request = new PendingRequest
            {
                ExpectedType = expectedResponseType,
                Deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, timeoutSeconds),
                Completion = new TaskCompletionSource<Envelope>()
            };
            pending.Add(request);

            try
            {
                await client.SendAsync(requestType, payload);
                while (!request.Completion.Task.IsCompleted)
                {
                    Pump();
                    await PvpAsync.NextFrameAsync();
                }
                return await request.Completion.Task;
            }
            finally
            {
                pending.Remove(request);
            }
        }

        public void Pump()
        {
            float now = Time.realtimeSinceStartup;
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                PendingRequest request = pending[index];
                if (now <= request.Deadline || request.Completion.Task.IsCompleted)
                    continue;

                request.Completion.TrySetException(new TimeoutException("Timeout richiesta server."));
                pending.RemoveAt(index);
            }

            while (client.TryDequeueMessage(out Envelope envelope))
            {
                if (TryCompletePending(envelope))
                    continue;

                UnhandledMessage?.Invoke(envelope);
            }
        }

        private bool TryCompletePending(Envelope envelope)
        {
            for (int index = 0; index < pending.Count; index++)
            {
                PendingRequest request = pending[index];
                if (request.Completion.Task.IsCompleted)
                    continue;

                if (envelope.type == request.ExpectedType || envelope.type == MessageTypes.Error)
                {
                    request.Completion.TrySetResult(envelope);
                    pending.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }
    }
}
