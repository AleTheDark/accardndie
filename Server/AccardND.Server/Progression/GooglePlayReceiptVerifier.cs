using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AccardND.Server.Progression;

/// <summary>Una ricevuta di Google Play gia' verificata e letta.</summary>
public sealed record GooglePlayReceipt(
    string ProductId,
    string PurchaseToken,
    string OrderId,
    string PackageName);

public enum ReceiptRejection
{
    None,

    /// <summary>Manca la chiave pubblica: la verifica e' spenta, non si concede niente.</summary>
    VerificationDisabled,

    /// <summary>Non e' JSON, o non ha la forma di una ricevuta Unity IAP.</summary>
    Malformed,

    /// <summary>Ricevuta di un altro store (Apple, Amazon): qui non vale.</summary>
    WrongStore,

    /// <summary>La firma non torna: la ricevuta non l'ha emessa Google per questa app.</summary>
    BadSignature,

    /// <summary>Ricevuta di un'altra app.</summary>
    WrongPackage,

    /// <summary>Prodotto che non esiste nel nostro catalogo.</summary>
    UnknownProduct,

    /// <summary>Acquisto annullato o ancora in attesa di pagamento.</summary>
    NotPurchased
}

/// <summary>
/// Verifica offline delle ricevute di Google Play. Google firma i dati dell'acquisto con la
/// chiave RSA dell'app: chi ha la chiave pubblica puo' controllare la firma senza chiamare
/// nessuna API, ed e' quello che si fa qui.
///
/// La firma si calcola sulla stringa esatta ricevuta, non su una sua riserializzazione: un
/// solo spazio di differenza e la verifica fallisce. Per questo il payload viaggia come
/// stringa fino all'ultimo momento.
/// </summary>
public sealed class GooglePlayReceiptVerifier
{
    private const string GooglePlayStoreName = "GooglePlay";

    private readonly string licenseKey;
    private readonly string packageName;

    public GooglePlayReceiptVerifier(GooglePlayConfig config)
    {
        licenseKey = config?.ResolveLicenseKey() ?? string.Empty;
        packageName = config?.PackageName ?? string.Empty;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(licenseKey);

    public ReceiptRejection Verify(string unifiedReceipt, out GooglePlayReceipt receipt)
    {
        receipt = null;
        if (!IsEnabled)
            return ReceiptRejection.VerificationDisabled;
        if (string.IsNullOrWhiteSpace(unifiedReceipt))
            return ReceiptRejection.Malformed;

        string store;
        string payload;
        try
        {
            using JsonDocument document = JsonDocument.Parse(unifiedReceipt);
            store = ReadString(document.RootElement, "Store");
            payload = ReadString(document.RootElement, "Payload");
        }
        catch (JsonException)
        {
            return ReceiptRejection.Malformed;
        }

        if (string.IsNullOrEmpty(payload))
            return ReceiptRejection.Malformed;
        if (!string.Equals(store, GooglePlayStoreName, StringComparison.OrdinalIgnoreCase))
            return ReceiptRejection.WrongStore;

        string purchaseData;
        string signature;
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            purchaseData = ReadString(document.RootElement, "json");
            signature = ReadString(document.RootElement, "signature");
        }
        catch (JsonException)
        {
            return ReceiptRejection.Malformed;
        }

        if (string.IsNullOrEmpty(purchaseData) || string.IsNullOrEmpty(signature))
            return ReceiptRejection.Malformed;
        if (!VerifySignature(purchaseData, signature))
            return ReceiptRejection.BadSignature;

        string productId;
        string purchaseToken;
        string orderId;
        string receiptPackage;
        int purchaseState;
        try
        {
            using JsonDocument document = JsonDocument.Parse(purchaseData);
            productId = ReadString(document.RootElement, "productId");
            purchaseToken = ReadString(document.RootElement, "purchaseToken");
            orderId = ReadString(document.RootElement, "orderId");
            receiptPackage = ReadString(document.RootElement, "packageName");
            purchaseState = ReadInt(document.RootElement, "purchaseState");
        }
        catch (JsonException)
        {
            return ReceiptRejection.Malformed;
        }

        if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(purchaseToken))
            return ReceiptRejection.Malformed;
        if (!string.IsNullOrEmpty(packageName) &&
            !string.Equals(receiptPackage, packageName, StringComparison.Ordinal))
            return ReceiptRejection.WrongPackage;
        if (!AccardND.NetProtocol.IapCatalog.IsKnown(productId))
            return ReceiptRejection.UnknownProduct;

        // 0 = acquistato. 1 e' annullato, 2 e' in attesa (pagamento differito): in entrambi
        // i casi il giocatore non ha ancora pagato e non si sblocca niente.
        if (purchaseState != 0)
            return ReceiptRejection.NotPurchased;

        receipt = new GooglePlayReceipt(productId, purchaseToken, orderId, receiptPackage);
        return ReceiptRejection.None;
    }

    private bool VerifySignature(string purchaseData, string signature)
    {
        byte[] signatureBytes;
        byte[] keyBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature);
            keyBytes = Convert.FromBase64String(licenseKey);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] data = Encoding.UTF8.GetBytes(purchaseData);
        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);

            // Play firma in SHA1withRSA. SHA256 e' il piano B: se un domani Google cambia
            // algoritmo, le ricevute nuove continuano a passare senza toccare il server.
            return rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1)
                || rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static string ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int number)
            ? number
            : 0;
}
