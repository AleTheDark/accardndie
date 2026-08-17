using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AccardND.NetProtocol;

namespace AccardND.LoadTest;

/// <summary>
/// Un client finto: parla lo stesso protocollo del gioco (busta JSON su WebSocket) e
/// correla richieste e risposte con <c>requestId</c>, esattamente come fa il client Unity.
/// Tutto quello che arriva senza requestId (eventi di match, presenza amici, kick) finisce
/// nel canale <see cref="Events"/>, che gli scenari leggono quando gli serve.
/// </summary>
public sealed class BotConnection : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    private readonly Uri endpoint;
    private readonly Metrics metrics;
    private readonly TimeSpan requestTimeout;
    private readonly ClientWebSocket socket = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Envelope>> pending = new();
    private readonly Channel<Envelope> events =
        Channel.CreateBounded<Envelope>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private Task pump;
    private int requestCounter;

    public BotConnection(string url, Metrics metrics, TimeSpan requestTimeout)
    {
        endpoint = new Uri(url);
        this.metrics = metrics;
        this.requestTimeout = requestTimeout;
    }

    public string Username { get; private set; }
    public string PlayerId { get; private set; }
    public string SessionToken { get; private set; }
    public ChannelReader<Envelope> Events => events.Reader;
    public bool IsOpen => socket.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            await socket.ConnectAsync(endpoint, cancellation);
        }
        catch
        {
            metrics.Record("ws.connect", stopwatch.Elapsed.TotalMilliseconds, "connect_failed");
            metrics.ConnectionFailed();
            throw;
        }

        metrics.Record("ws.connect", stopwatch.Elapsed.TotalMilliseconds);
        metrics.ConnectionOpened();
        pump = Task.Run(() => PumpAsync(cancellation), CancellationToken.None);
    }

    /// <summary>Login con password. Registra l'account se <paramref name="register"/>.</summary>
    public async Task AuthenticateAsync(
        string username, string password, string clientVersion, bool register, CancellationToken cancellation)
    {
        Username = username;

        // La versione viaggia nello stesso payload di username e password: il gate la
        // rilegge da qualunque messaggio di autenticazione (ClientVersionGate.IsAccepted),
        // ma RegisterRequest/LoginRequest non hanno il campo, quindi lo aggiunge questo DTO.
        var payload = new PasswordAuthPayload
        {
            username = username,
            password = password,
            clientVersion = clientVersion
        };

        Envelope reply = await RequestAsync(
            register ? "auth.register" : "auth.login",
            register ? MessageTypes.AuthRegister : MessageTypes.AuthLogin,
            payload,
            cancellation);

        var response = Parse<AuthResponse>(reply);
        if (response is not { ok: true })
            throw new BotFailure($"auth rifiutata: {DescribeAuthFailure(reply, response)}");

        PlayerId = response.playerId;
        SessionToken = response.sessionToken;
    }

    public Task<Envelope> RequestAsync(
        string operation, string type, object payload, CancellationToken cancellation,
        bool tolerateErrors = false)
    {
        string json = payload != null
            ? JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions)
            : "{}";
        return RequestRawAsync(operation, type, json, cancellation, tolerateErrors);
    }

    /// <summary>
    /// Richiesta con attesa della risposta correlata; misura la latenza.
    /// Con <paramref name="tolerateErrors"/> il rifiuto del server e' un esito previsto
    /// (il bot sta tentando una mossa che potrebbe non essere legale) e finisce in una
    /// riga a parte invece di gonfiare il tasso d'errore.
    /// </summary>
    public async Task<Envelope> RequestRawAsync(
        string operation, string type, string payloadJson, CancellationToken cancellation,
        bool tolerateErrors = false)
    {
        string requestId = $"{Username}-{Interlocked.Increment(ref requestCounter)}";
        var completion = new TaskCompletionSource<Envelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[requestId] = completion;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await SendRawAsync(type, payloadJson, requestId, cancellation);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(requestTimeout);
            Envelope reply;
            try
            {
                reply = await completion.Task.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                metrics.Record(operation, stopwatch.Elapsed.TotalMilliseconds, "timeout");
                throw new BotFailure($"{operation}: nessuna risposta entro {requestTimeout.TotalSeconds:F0}s");
            }

            string errorCode = reply.type == MessageTypes.Error
                ? Parse<ErrorMessage>(reply)?.code ?? "error"
                : null;
            if (errorCode != null && tolerateErrors)
                metrics.Record($"{operation} (rifiutata)", stopwatch.Elapsed.TotalMilliseconds);
            else
                metrics.Record(operation, stopwatch.Elapsed.TotalMilliseconds, errorCode);
            return reply;
        }
        finally
        {
            pending.TryRemove(requestId, out _);
        }
    }

    /// <summary>Invio senza attesa di risposta.</summary>
    public Task SendAsync(string type, object payload, CancellationToken cancellation)
    {
        string json = payload != null
            ? JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions)
            : "{}";
        return SendRawAsync(type, json, null, cancellation);
    }

    private async Task SendRawAsync(
        string type, string payloadJson, string requestId, CancellationToken cancellation)
    {
        var envelope = new Envelope { type = type, payload = payloadJson, requestId = requestId };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
        await sendLock.WaitAsync(cancellation);
        try
        {
            if (socket.State != WebSocketState.Open)
                throw new BotFailure("socket chiuso");
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellation);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private async Task PumpAsync(CancellationToken cancellation)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (!cancellation.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellation);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                Envelope envelope = null;
                try
                {
                    envelope = JsonSerializer.Deserialize<Envelope>(
                        Encoding.UTF8.GetString(message.ToArray()), JsonOptions);
                }
                catch (JsonException)
                {
                    metrics.BotError("json_non_valido_dal_server");
                }
                message.SetLength(0);

                if (envelope == null)
                    continue;

                if (!string.IsNullOrEmpty(envelope.requestId)
                    && pending.TryRemove(envelope.requestId, out TaskCompletionSource<Envelope> completion))
                {
                    completion.TrySetResult(envelope);
                    continue;
                }

                events.Writer.TryWrite(envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // Fine corsa.
        }
        catch (WebSocketException exception)
        {
            metrics.ConnectionDropped();
            metrics.BotError($"ws_{exception.WebSocketErrorCode}");
        }
        finally
        {
            events.Writer.TryComplete();
            foreach (KeyValuePair<string, TaskCompletionSource<Envelope>> entry in pending)
                entry.Value.TrySetException(new BotFailure("connessione caduta durante l'attesa"));
        }
    }

    public static T Parse<T>(Envelope envelope) where T : class
    {
        if (envelope?.payload == null)
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(envelope.payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string DescribeAuthFailure(Envelope reply, AuthResponse response)
    {
        if (response?.requiresUpdate == true)
            return $"versione client rifiutata, il server vuole {response.requiredVersion} (usa --client-version)";
        if (response?.maintenance == true)
            return "server in manutenzione";
        if (!string.IsNullOrEmpty(response?.error))
            return response.error;
        return reply?.type == MessageTypes.Error
            ? Parse<ErrorMessage>(reply)?.message ?? "errore"
            : "risposta inattesa";
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", closing.Token);
            }
        }
        catch
        {
            // Chiusura best effort: al server serve solo che il socket sparisca.
        }
        finally
        {
            if (pump != null)
                metrics.ConnectionClosed();
            socket.Dispose();
            sendLock.Dispose();
        }
    }
}

/// <summary>Errore atteso di un bot: non e' un bug dello strumento, e' il server che dice di no.</summary>
public sealed class BotFailure : Exception
{
    public BotFailure(string message) : base(message) { }
}

/// <summary>Login con password piu' la versione dichiarata, in un unico payload.</summary>
public sealed class PasswordAuthPayload
{
    public string username;
    public string password;
    public string clientVersion;
}
