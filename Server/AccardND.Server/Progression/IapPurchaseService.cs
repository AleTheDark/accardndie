using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Acquisti a valuta reale. Il client manda una ricevuta, qui si controlla che l'abbia
/// emessa Google per questa app e solo allora si sblocca qualcosa.
///
/// Due scelte reggono tutto il resto:
///
/// 1. Il token dell'acquisto e' la chiave primaria della tabella. La stessa ricevuta puo'
///    arrivare quante volte vuole - al riavvio, dopo una disconnessione, a ogni login del
///    ripristino - e concede sempre una volta sola.
///
/// 2. Gli sblocchi si riapplicano a ogni lettura degli entitlement, non solo al momento
///    dell'acquisto. Chi ha comprato "classi + supreme" ha comprato anche le supreme che
///    non esistevano ancora: quando ne arriva una nuova a catalogo, il primo accesso
///    successivo gliela mette in mano senza bisogno di una migrazione.
/// </summary>
public sealed class IapPurchaseService
{
    private readonly AccardDatabase database;
    private readonly GooglePlayReceiptVerifier verifier;
    private readonly ILogger<IapPurchaseService> logger;

    public IapPurchaseService(
        AccardDatabase database,
        GooglePlayReceiptVerifier verifier,
        ILogger<IapPurchaseService> logger)
    {
        this.database = database;
        this.verifier = verifier;
        this.logger = logger;
    }

    /// <summary>Cosa possiede l'account, riapplicando gli sblocchi che ne discendono.</summary>
    public IapEntitlementsData GetEntitlements(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        return ReadAndApply(connection, identity.PlayerId);
    }

    public IapRedeemResult Redeem(AccountIdentity identity, IapRedeemRequest request)
    {
        ReceiptRejection rejection = verifier.Verify(request?.receipt, out GooglePlayReceipt receipt);
        if (rejection != ReceiptRejection.None)
        {
            logger.LogWarning(
                "Ricevuta rifiutata per {Player}: {Reason}", identity.PlayerId, rejection);
            return new IapRedeemResult
            {
                granted = false,
                productId = request?.productId ?? string.Empty,
                messageKey = RejectionKey(rejection),
                entitlements = GetEntitlements(identity)
            };
        }

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        string owner = FindPurchaseOwner(connection, transaction, receipt.PurchaseToken);
        if (owner != null && owner != identity.PlayerId)
        {
            // Stessa ricevuta, altro account: e' il caso della condivisione di un acquisto.
            transaction.Rollback();
            logger.LogWarning(
                "Ricevuta gia' riscattata da {Owner}, rifiutata a {Player}", owner, identity.PlayerId);
            return new IapRedeemResult
            {
                granted = false,
                productId = receipt.ProductId,
                messageKey = "shop.premium.already_redeemed",
                entitlements = GetEntitlements(identity)
            };
        }

        if (owner == null)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO player_purchases
                    (purchase_token, player_id, product_id, order_id, store, redeemed_at)
                VALUES ($token, $player, $product, $order, 'GooglePlay', $now)";
            insert.Parameters.AddWithValue("$token", receipt.PurchaseToken);
            insert.Parameters.AddWithValue("$player", identity.PlayerId);
            insert.Parameters.AddWithValue("$product", receipt.ProductId);
            insert.Parameters.AddWithValue("$order", (object)receipt.OrderId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
            logger.LogInformation(
                "Acquisto {Product} riscattato da {Player}", receipt.ProductId, identity.PlayerId);
        }

        IapEntitlementsData entitlements = ReadAndApply(connection, identity.PlayerId, transaction);
        transaction.Commit();

        return new IapRedeemResult
        {
            granted = true,
            productId = receipt.ProductId,
            messageKey = "shop.premium.granted",
            entitlements = entitlements
        };
    }

    private static string FindPurchaseOwner(
        SqliteConnection connection, SqliteTransaction transaction, string purchaseToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT player_id FROM player_purchases WHERE purchase_token = $token";
        command.Parameters.AddWithValue("$token", purchaseToken);
        return command.ExecuteScalar() as string;
    }

    private IapEntitlementsData ReadAndApply(
        SqliteConnection connection, string playerId, SqliteTransaction transaction = null)
    {
        List<string> products = ReadProducts(connection, transaction, playerId);
        bool noAds = false;
        bool allClasses = false;
        bool allSupreme = false;
        foreach (string product in products)
        {
            noAds |= IapCatalog.GrantsNoAds(product);
            allClasses |= IapCatalog.GrantsClasses(product);
            allSupreme |= IapCatalog.GrantsSupreme(product);
        }

        if (allClasses || allSupreme)
            ApplyUnlocks(connection, transaction, playerId, allClasses, allSupreme);

        return new IapEntitlementsData
        {
            noAds = noAds,
            allClasses = allClasses,
            allSupreme = allSupreme,
            productIds = products.ToArray()
        };
    }

    private static List<string> ReadProducts(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        List<string> products = new();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT product_id FROM player_purchases WHERE player_id = $player";
        command.Parameters.AddWithValue("$player", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string product = reader.GetString(0);
            if (!products.Contains(product))
                products.Add(product);
        }
        return products;
    }

    private static void ApplyUnlocks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        bool allClasses,
        bool allSupreme)
    {
        foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All)
        {
            bool wanted =
                (allClasses && entry.Type == SanctuaryCatalog.TypeClass)
                || (allSupreme && entry.Type == SanctuaryCatalog.TypeSecondAbility);
            if (!wanted)
                continue;

            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO single_player_unlocks (player_id, unlock_type, unlock_id, unlocked_at)
                VALUES ($player, $type, $id, $now)";
            insert.Parameters.AddWithValue("$player", playerId);
            insert.Parameters.AddWithValue("$type", entry.Type);
            insert.Parameters.AddWithValue("$id", entry.Id);
            insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }
    }

    private static string RejectionKey(ReceiptRejection rejection) => rejection switch
    {
        ReceiptRejection.VerificationDisabled => "shop.premium.store_off",
        ReceiptRejection.WrongStore => "shop.premium.wrong_store",
        ReceiptRejection.BadSignature => "shop.premium.bad_receipt",
        ReceiptRejection.WrongPackage => "shop.premium.bad_receipt",
        ReceiptRejection.UnknownProduct => "shop.premium.unknown_product",
        ReceiptRejection.NotPurchased => "shop.premium.not_paid",
        _ => "shop.premium.bad_receipt"
    };
}
