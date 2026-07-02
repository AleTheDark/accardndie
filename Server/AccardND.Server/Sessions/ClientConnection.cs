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
    public bool IsAuthenticated => Identity != null;
    public bool IsOpen => socket.State == WebSocketState.Open;

    public async Task SendAsync(string type, object payload, CancellationToken cancellation = default)
    {
        var envelope = new Envelope
        {
            type = type,
            payload = payload != null ? JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions) : "{}"
        };
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
