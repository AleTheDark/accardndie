using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Sessions;

/// <summary>
/// Ricorda la risposta già data a una richiesta, per (giocatore, requestId).
///
/// Serve ai rinvii dopo una riconnessione: il client non sa se la richiesta era
/// arrivata prima che il socket cadesse, quindi la rispedisce con lo stesso
/// requestId. Senza questo, comprare un consumabile al Santuario durante un blip
/// di rete lo pagherebbe due volte. Con questo, la seconda copia della richiesta
/// non riesegue nulla e riceve la stessa identica risposta.
///
/// Su disco, non in memoria: il client persiste le mutazioni critiche e le rigioca
/// anche al riavvio successivo, magari il giorno dopo. Se il ricordo morisse col
/// processo, un deploy nel frattempo riaprirebbe esattamente la finestra di doppia
/// esecuzione che questo store esiste per chiudere.
/// </summary>
public sealed class RequestDedupStore
{
    /// <summary>Risposta memorizzata: tipo e payload già serializzato.</summary>
    public readonly record struct CachedReply(string Type, string Payload);

    /// <summary>
    /// Quanto a lungo si ricorda una risposta. Deve coprire con margine la finestra
    /// in cui un client può ancora rigiocare la sua coda persistita (vedi
    /// PersistentMutationOutbox lato Unity, che scarta le voci più vecchie di 7 giorni).
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(8);

    private readonly AccardDatabase database;
    private readonly ILogger<RequestDedupStore> logger;

    public RequestDedupStore(AccardDatabase database, ILogger<RequestDedupStore> logger)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
        this.logger = logger;
    }

    public bool TryGet(string playerId, string requestId, out CachedReply reply)
    {
        reply = default;
        if (!IsValidKey(playerId, requestId))
            return false;

        try
        {
            using SqliteConnection connection = database.Open();
            using SqliteCommand select = connection.CreateCommand();
            select.CommandText = @"
                SELECT reply_type, reply_payload FROM request_dedup
                WHERE player_id = $player AND request_id = $request AND expires_at > $now";
            select.Parameters.AddWithValue("$player", playerId);
            select.Parameters.AddWithValue("$request", requestId);
            select.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
                return false;

            reply = new CachedReply(reader.GetString(0), reader.GetString(1));
            return true;
        }
        catch (SqliteException exception)
        {
            // Meglio rieseguire una richiesta che rifiutarla: il caso peggiore resta
            // raro e i mutatori hanno quasi tutti una loro chiave naturale.
            logger?.LogError(exception, "Lettura del dedup fallita per {Player}.", playerId);
            return false;
        }
    }

    public void Store(string playerId, string requestId, CachedReply reply)
    {
        if (!IsValidKey(playerId, requestId) || string.IsNullOrEmpty(reply.Type))
            return;

        try
        {
            using SqliteConnection connection = database.Open();
            using SqliteCommand insert = connection.CreateCommand();
            // Una richiesta muta lo stato una volta sola: le scadute si portano via qui,
            // dove il costo si perde nel rumore di un acquisto.
            insert.CommandText = @"
                DELETE FROM request_dedup WHERE expires_at <= $now;
                INSERT INTO request_dedup (player_id, request_id, reply_type, reply_payload, expires_at)
                VALUES ($player, $request, $type, $payload, $expires)
                ON CONFLICT (player_id, request_id) DO UPDATE SET
                    reply_type = excluded.reply_type,
                    reply_payload = excluded.reply_payload,
                    expires_at = excluded.expires_at";
            insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            insert.Parameters.AddWithValue("$player", playerId);
            insert.Parameters.AddWithValue("$request", requestId);
            insert.Parameters.AddWithValue("$type", reply.Type);
            insert.Parameters.AddWithValue("$payload", reply.Payload ?? "{}");
            insert.Parameters.AddWithValue("$expires", DateTime.UtcNow.Add(Lifetime).ToString("O"));
            insert.ExecuteNonQuery();
        }
        catch (SqliteException exception)
        {
            // La richiesta è già stata eseguita: fallire qui non deve toglierne la
            // risposta al client, che è già partita.
            logger?.LogError(exception, "Memorizzazione del dedup fallita per {Player}.", playerId);
        }
    }

    private static bool IsValidKey(string playerId, string requestId) =>
        !string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(requestId);
}
