using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Rooms;

namespace AccardND.Server.Sessions;

public sealed class ClientConnection
{
    // IncludeFields: i DTO del protocollo usano campi pubblici per compatibilità con JsonUtility.
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    private readonly WebSocket socket;
    private readonly SemaphoreSlim sendLock = new(1, 1);

    public ClientConnection(WebSocket socket)
    {
        this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
        ConnectionId = Guid.NewGuid().ToString("N");
    }

    public string ConnectionId { get; }
    public AccountIdentity Identity { get; set; }
    public Room CurrentRoom { get; set; }

    /// <summary>
    /// Token della sessione con cui questa connessione si è autenticata. Distingue
    /// il rientro della stessa sessione dopo una caduta di rete (stesso token, il
    /// socket vecchio è uno zombie da chiudere in silenzio) dall'accesso di un
    /// altro dispositivo (token diverso, l'altro va avvisato e sloggato).
    /// </summary>
    public string SessionToken { get; set; }
    public bool IsAuthenticated => Identity != null;
    public bool IsOpen => socket.State == WebSocketState.Open;

    // --- Correlazione richiesta/risposta ---
    //
    // Il router apre un "ambito di risposta" prima di eseguire un messaggio con
    // requestId: la prima cosa che spediamo da lì lo eredita, così il client sa a
    // quale richiesta appartiene. Gli invii verso l'altro giocatore (broadcast di
    // match) partono da un'altra connessione e restano senza requestId, che è
    // esattamente quello che vogliamo.
    private string replyRequestId;
    private RequestDedupStore.CachedReply? capturedReply;

    public void BeginReplyScope(string requestId)
    {
        replyRequestId = requestId;
        capturedReply = null;
    }

    /// <summary>Chiude l'ambito e restituisce la risposta spedita, se c'è stata.</summary>
    public bool EndReplyScope(out RequestDedupStore.CachedReply reply)
    {
        reply = capturedReply ?? default;
        bool captured = capturedReply.HasValue;
        replyRequestId = null;
        capturedReply = null;
        return captured;
    }

    public Task SendAsync(string type, object payload, CancellationToken cancellation = default)
    {
        string json = payload != null
            ? JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions)
            : "{}";
        return SendRawAsync(type, json, cancellation);
    }

    /// <summary>Invia un payload già serializzato: usato per rigiocare una risposta memorizzata.</summary>
    public async Task SendRawAsync(string type, string payloadJson, CancellationToken cancellation = default)
    {
        string requestId = replyRequestId;
        if (requestId != null)
        {
            // Solo la prima risposta dell'ambito viene correlata e memorizzata:
            // quello che segue è già flusso spontaneo.
            replyRequestId = null;
            capturedReply = new RequestDedupStore.CachedReply(type, payloadJson);
        }

        var envelope = new Envelope { type = type, payload = payloadJson ?? "{}", requestId = requestId };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));

        await sendLock.WaitAsync(cancellation);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellation);
        }
        finally
        {
            sendLock.Release();
        }
    }

    public Task SendErrorAsync(string code, string message, CancellationToken cancellation = default) =>
        SendAsync(MessageTypes.Error, new ErrorMessage { code = code, message = message }, cancellation);

    /// <summary>Riceve la prossima busta; null quando il client chiude la connessione.</summary>
    public async Task<Envelope> ReceiveAsync(CancellationToken cancellation)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, cancellation);
            }
            catch (WebSocketException)
            {
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            message.Write(buffer, 0, result.Count);
            if (message.Length > 256 * 1024)
                return null;
            if (!result.EndOfMessage)
                continue;

            try
            {
                return JsonSerializer.Deserialize<Envelope>(
                    Encoding.UTF8.GetString(message.ToArray()), JsonOptions);
            }
            catch (JsonException)
            {
                await SendErrorAsync(ErrorCodes.InvalidMessage, "JSON non valido.", cancellation);
                message.SetLength(0);
            }
        }
    }

    public static T ParsePayload<T>(Envelope envelope) where T : class
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

    public async Task CloseAsync()
    {
        if (socket.State == WebSocketState.Open)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Il client ha già chiuso.
            }
        }
    }
}
