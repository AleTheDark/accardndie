using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Unico punto di contatto con il server: drena la coda del socket, correla le
    /// risposte alle richieste e tiene viva la sessione attraverso le cadute di rete.
    ///
    /// Tre garanzie, in ordine di importanza:
    /// - la connessione si riapre da sola, con attese crescenti, e si riautentica
    ///   col token di sessione senza ripassare da Google;
    /// - quello che era in volo quando il socket è caduto non va perso: le richieste
    ///   vengono rispedite con lo stesso requestId, che il server usa per non
    ///   rieseguire una mutazione già applicata;
    /// - ogni richiesta riceve la propria risposta, non quella di un'altra richiesta
    ///   dello stesso tipo partita nel frattempo.
    ///
    /// Va pompato ogni frame da un MonoBehaviour (vedi AccountServerSessionDriver).
    /// </summary>
    public sealed class PvpServerMessageDispatcher
    {
        /// <summary>
        /// Messaggi che non ha senso rispedire dopo una riconnessione: valgono per
        /// l'istante in cui sono stati pensati. Rigiocare una mossa o un ingresso in
        /// coda dopo mezzo minuto di buio confonde e basta.
        /// </summary>
        private static readonly HashSet<string> TransientMessageTypes = new()
        {
            MessageTypes.QueueJoin,
            MessageTypes.QueueLeave,
            MessageTypes.RoomsList,
            MessageTypes.RoomLeave
        };

        /// <summary>
        /// I quattro tempi della rete, che vanno letti insieme:
        /// - <see cref="MaxBackoffSeconds"/> (qui): ogni quanto, al massimo, si ritenta.
        ///   Sta sotto la grazia del server perché dentro quella finestra devono starci
        ///   più tentativi, non uno solo arrivato tardi.
        /// - <see cref="OfflineGraceSeconds"/> (qui): quanto una richiesta in volo
        ///   sopravvive alla rete assente. Pari alla grazia di riconnessione del server
        ///   (ServerConfig.DisconnectTimeoutSeconds, 120s): oltre quella il tavolo è
        ///   perso comunque, e tenere in vita la richiesta non serve più a niente.
        /// - l'attesa delle schermate (AccountServerSession.WaitUntilReadyAsync, 12s):
        ///   più corta di proposito, è quanto si fa aspettare un giocatore davanti a un
        ///   pannello vuoto prima di ripiegare sui dati locali. La riconnessione va
        ///   avanti lo stesso e la schermata si aggiorna quando torna.
        /// - la grazia di riconnessione del match, che vive sul server ed è l'unica
        ///   autorevole: il client non la decide, la legge da match.resume.
        /// </summary>
        private const float MaxBackoffSeconds = 15f;

        /// <inheritdoc cref="MaxBackoffSeconds"/>
        private const float OfflineGraceSeconds = 120f;

        private const int MaxOutboxEntries = 64;

        /// <summary>
        /// Timeout consecutivi dopo i quali un socket che si dichiara ancora aperto
        /// viene dato per morto. Serve al caso peggiore della rete assente: quando la
        /// connessione muore senza che il sistema operativo se ne accorga (NAT che
        /// scade, portale captive, proxy in mezzo) il receive loop non finisce mai e
        /// nessuno segnala la caduta. Senza questo il giocatore resta a guardare
        /// richieste che scadono a una a una, senza badge e senza riconnessione.
        /// Due e non uno: un singolo timeout può essere solo un server lento.
        /// </summary>
        private const int TimeoutsBeforeAssumingDeadSocket = 2;

        private sealed class PendingRequest
        {
            public string RequestId;
            public string RequestType;
            public object Payload;
            public string ExpectedType;

            /// <summary>Scadenza della singola spedizione: riparte a ogni rinvio.</summary>
            public float Deadline;

            /// <summary>Scadenza assoluta: oltre questa si molla anche se siamo ancora offline.</summary>
            public float GiveUpAt;

            public float TimeoutSeconds;
            public bool AwaitingResponse;
            public TaskCompletionSource<Envelope> Completion;
        }

        private readonly struct OutboxEntry
        {
            public OutboxEntry(string type, object payload, string requestId)
            {
                Type = type;
                Payload = payload;
                RequestId = requestId;
            }

            public string Type { get; }
            public object Payload { get; }
            public string RequestId { get; }
        }

        private readonly PvpServerClient client;
        private readonly List<PendingRequest> pending = new();
        private readonly Queue<OutboxEntry> outbox = new();

        private string serverUrl;
        private Func<PvpServerClient, PvpServerMessageDispatcher, Task<bool>> authenticate;
        private int reconnectAttempt;
        private float nextAttemptAt;
        private bool reconnecting;
        private bool handshaking;

        /// <summary>
        /// Richieste scadute di fila senza che dal server arrivasse più niente.
        /// Qualunque messaggio in arrivo lo azzera: è la prova che il socket è vivo.
        /// </summary>
        private int consecutiveTimeouts;

        public PvpServerMessageDispatcher(PvpServerClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public event Action<Envelope> UnhandledMessage;

        /// <summary>Il socket è caduto: la sessione è sospesa, non ancora persa.</summary>
        public event Action Disconnected;

        /// <summary>Connessione ripresa e riautenticata: quello che era in sospeso è ripartito.</summary>
        public event Action Reconnected;

        /// <summary>La riconnessione ha smesso di provarci: serve un login vero.</summary>
        public event Action ReconnectFailed;

        public bool IsConnected => client.IsConnected;

        public bool IsReconnecting => reconnecting || (!client.IsConnected && CanReconnect);

        private bool CanReconnect => !string.IsNullOrEmpty(serverUrl) && authenticate != null;

        /// <summary>
        /// Abilita la riconnessione automatica. Il delegato deve riautenticare la
        /// connessione appena riaperta (token di sessione) e restituire true solo se il
        /// server ha accettato: senza, riprovare non serve a niente.
        /// </summary>
        public void ConfigureReconnect(
            string url, Func<PvpServerClient, PvpServerMessageDispatcher, Task<bool>> authenticator)
        {
            serverUrl = url;
            authenticate = authenticator;
            reconnectAttempt = 0;
        }

        /// <summary>
        /// Invio senza risposta attesa. Se il socket è giù il messaggio aspetta in coda
        /// e parte alla riconnessione, tranne quelli che scadono con l'istante.
        /// </summary>
        public async Task SendAsync(string type, object payload = null)
        {
            if (client.IsConnected)
            {
                await client.SendAsync(type, payload);
                return;
            }

            if (!CanReconnect || TransientMessageTypes.Contains(type))
                throw new InvalidOperationException("Client server non connesso.");

            if (outbox.Count >= MaxOutboxEntries)
                outbox.Dequeue();
            outbox.Enqueue(new OutboxEntry(type, payload, Guid.NewGuid().ToString("N")));
        }

        /// <summary>
        /// Richiesta con risposta attesa. Attraversa una caduta di rete: se il socket
        /// va giù prima della risposta, la richiesta viene rispedita alla riconnessione
        /// con lo stesso requestId e il server non la riesegue due volte.
        /// </summary>
        public async Task<Envelope> RequestAsync(
            string requestType,
            object payload,
            string expectedResponseType,
            float timeoutSeconds = 8f,
            string requestId = null)
        {
            if (string.IsNullOrEmpty(expectedResponseType))
                throw new ArgumentException("Tipo risposta attesa mancante.", nameof(expectedResponseType));
            if (!client.IsConnected && !CanReconnect)
                throw new InvalidOperationException("Client server non connesso.");

            float timeout = Mathf.Max(0.5f, timeoutSeconds);
            var request = new PendingRequest
            {
                RequestId = string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString("N") : requestId,
                RequestType = requestType,
                Payload = payload,
                ExpectedType = expectedResponseType,
                TimeoutSeconds = timeout,
                Deadline = Time.realtimeSinceStartup + timeout,
                GiveUpAt = Time.realtimeSinceStartup + Mathf.Max(timeout, OfflineGraceSeconds),
                Completion = new TaskCompletionSource<Envelope>()
            };
            pending.Add(request);

            try
            {
                await TrySendRequestAsync(request);
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

        /// <summary>
        /// Spedisce (o rispedisce) una richiesta. Se il socket è giù resta in sospeso:
        /// ci pensa la riconnessione, che è già in moto.
        /// </summary>
        private async Task TrySendRequestAsync(PendingRequest request)
        {
            if (!client.IsConnected)
            {
                request.AwaitingResponse = false;
                return;
            }

            try
            {
                await client.SendAsync(request.RequestType, request.Payload, request.RequestId);
                request.AwaitingResponse = true;
                request.Deadline = Time.realtimeSinceStartup + request.TimeoutSeconds;
            }
            catch (Exception)
            {
                // Il socket è caduto proprio adesso: la riconnessione la rispedirà.
                request.AwaitingResponse = false;
            }
        }

        public void Pump()
        {
            // Durante l'handshake di riconnessione è quello a drenare la coda: se
            // pompassimo anche qui, la risposta di auth verrebbe consumata da noi.
            if (handshaking)
                return;

            DrainIncoming();

            if (client.PollDisconnected())
            {
                consecutiveTimeouts = 0;
                reconnectAttempt = 0;
                nextAttemptAt = Time.realtimeSinceStartup;
                foreach (PendingRequest request in pending)
                    request.AwaitingResponse = false;
                Disconnected?.Invoke();
            }

            ExpireTimedOutRequests();
            DropSocketIfDead();

            if (!client.IsConnected && CanReconnect && !reconnecting
                && Time.realtimeSinceStartup >= nextAttemptAt)
            {
                _ = ReconnectAsync();
            }
        }

        private void DrainIncoming()
        {
            while (client.TryDequeueMessage(out Envelope envelope))
            {
                // Un messaggio, qualunque messaggio, dice che il socket è vivo.
                consecutiveTimeouts = 0;
                Route(envelope);
            }
        }

        /// <summary>
        /// Chiude di sua iniziativa un socket che si dichiara aperto ma non risponde
        /// più, e fa ripartire la riconnessione dal primo tentativo. È l'unico modo di
        /// accorgersi di una rete sparita in silenzio: la caduta "vera" arriva dal
        /// receive loop, che in questo caso non finisce mai.
        /// </summary>
        private void DropSocketIfDead()
        {
            if (consecutiveTimeouts < TimeoutsBeforeAssumingDeadSocket
                || !client.IsConnected || !CanReconnect || reconnecting)
            {
                return;
            }

            Debug.LogWarning(
                "[Net] Il server non risponde più su una connessione ancora aperta: la chiudo e riparto.");
            consecutiveTimeouts = 0;
            // Chiudere di proposito non conta come caduta (PollDisconnected resta muto):
            // la caduta la annunciamo qui, una volta sola.
            client.Dispose();
            foreach (PendingRequest request in pending)
                request.AwaitingResponse = false;
            reconnectAttempt = 0;
            nextAttemptAt = Time.realtimeSinceStartup;
            Disconnected?.Invoke();
        }

        private void Route(Envelope envelope)
        {
            if (TryCompletePending(envelope))
                return;
            UnhandledMessage?.Invoke(envelope);
        }

        private void ExpireTimedOutRequests()
        {
            float now = Time.realtimeSinceStartup;
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                PendingRequest request = pending[index];
                if (request.Completion.Task.IsCompleted)
                    continue;

                // Finché siamo offline il tempo non conta contro la richiesta: conta
                // solo il limite assoluto, oltre il quale la rete è andata sul serio.
                bool expired = now > request.GiveUpAt
                    || (request.AwaitingResponse && now > request.Deadline);
                if (!expired)
                    continue;

                // Scaduta mentre il socket si dichiarava aperto: o il server è lento, o
                // quella connessione è già morta e nessuno ce l'ha detto. Il conteggio
                // distingue i due casi (vedi DropSocketIfDead).
                if (client.IsConnected && request.AwaitingResponse)
                    consecutiveTimeouts++;

                request.Completion.TrySetException(new TimeoutException(
                    client.IsConnected
                        ? "Timeout richiesta server."
                        : "Server irraggiungibile: richiesta non completata."));
                pending.RemoveAt(index);
            }
        }

        private bool TryCompletePending(Envelope envelope)
        {
            for (int index = 0; index < pending.Count; index++)
            {
                PendingRequest request = pending[index];
                if (request.Completion.Task.IsCompleted)
                    continue;

                // Il requestId è la correlazione vera. Il tipo atteso resta come
                // secondo controllo: un evento spontaneo che si portasse dietro un id
                // altrui non deve poter chiudere una richiesta.
                bool matchesId = !string.IsNullOrEmpty(envelope.requestId)
                    && envelope.requestId == request.RequestId;

                // Le risposte del server portano il requestId; quelle senza arrivano da
                // percorsi che non lo ricopiano e restano correlate per tipo, come prima.
                if (!matchesId && !string.IsNullOrEmpty(envelope.requestId))
                    continue;

                if (envelope.type == MessageTypes.Error)
                {
                    // Un errore chiude una richiesta solo se ne porta l'id. Gli errori
                    // spontanei - una mossa PvP rifiutata, un avviso di stanza - viaggiano
                    // senza id e prima facevano match con qualunque pendente: bastava
                    // giocare una carta fuori turno per far fallire l'acquisto al
                    // Santuario che stava viaggiando in quel momento.
                    if (!matchesId)
                        continue;
                }
                else if (envelope.type != request.ExpectedType)
                {
                    continue;
                }

                request.Completion.TrySetResult(envelope);
                pending.RemoveAt(index);
                return true;
            }
            return false;
        }

        // --- Riconnessione ---

        private async Task ReconnectAsync()
        {
            reconnecting = true;
            try
            {
                await client.ConnectAsync(serverUrl);

                handshaking = true;
                bool authenticated;
                try
                {
                    authenticated = await authenticate(client, this);
                }
                finally
                {
                    handshaking = false;
                }

                if (!authenticated)
                {
                    // Il server ha rifiutato: riprovare col vecchio token non cambia
                    // esito, serve un login nuovo.
                    ReconnectFailed?.Invoke();
                    serverUrl = null;
                    return;
                }

                reconnectAttempt = 0;
                consecutiveTimeouts = 0;
                await FlushAsync();
                Reconnected?.Invoke();
            }
            catch (Exception exception)
            {
                float delay = ScheduleNextAttempt();
                Debug.Log($"[Net] Riconnessione fallita ({exception.Message}): nuovo tentativo fra {delay:0.#}s.");
            }
            finally
            {
                reconnecting = false;
            }
        }

        /// <summary>Rimanda quello che era rimasto indietro: prima la coda, poi le richieste senza risposta.</summary>
        private async Task FlushAsync()
        {
            while (outbox.Count > 0 && client.IsConnected)
            {
                OutboxEntry entry = outbox.Peek();
                try
                {
                    await client.SendAsync(entry.Type, entry.Payload, entry.RequestId);
                    outbox.Dequeue();
                }
                catch (Exception)
                {
                    // Caduta di nuovo: il resto resta in coda per il giro dopo.
                    break;
                }
            }

            foreach (PendingRequest request in pending.ToArray())
            {
                if (!request.Completion.Task.IsCompleted && !request.AwaitingResponse)
                    await TrySendRequestAsync(request);
            }
        }

        /// <summary>
        /// Attese crescenti con un pizzico di casualità, per non ripartire tutti insieme.
        /// Restituisce il ritardo davvero schedulato: la parte casuale va estratta una
        /// volta sola, o si finisce per annunciarne uno e aspettarne un altro.
        /// </summary>
        private float ScheduleNextAttempt()
        {
            reconnectAttempt++;
            float baseDelay = Mathf.Min(MaxBackoffSeconds, Mathf.Pow(2f, Mathf.Max(0, reconnectAttempt - 1)));
            float delay = baseDelay + UnityEngine.Random.Range(0f, Mathf.Min(1f, baseDelay * 0.25f));
            nextAttemptAt = Time.realtimeSinceStartup + delay;
            return delay;
        }

        /// <summary>
        /// Scambio diretto usato solo dall'handshake di riconnessione: manda un
        /// messaggio e aspetta la risposta drenando la coda a mano. Non passa dalla
        /// macchina di riconnessione, che in quel momento è già in corsa.
        /// </summary>
        public async Task<Envelope> HandshakeAsync(
            string requestType, object payload, string expectedResponseType, float timeoutSeconds = 10f)
        {
            await client.SendAsync(requestType, payload);

            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, timeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                while (client.TryDequeueMessage(out Envelope envelope))
                {
                    if (envelope.type == expectedResponseType || envelope.type == MessageTypes.Error)
                        return envelope;
                    // Tutto il resto arrivato nel frattempo segue la strada normale.
                    Route(envelope);
                }

                if (!client.IsConnected)
                    return null;
                await PvpAsync.NextFrameAsync();
            }
            return null;
        }
    }
}
