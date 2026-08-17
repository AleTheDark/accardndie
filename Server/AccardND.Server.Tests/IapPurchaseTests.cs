using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Gli acquisti a valuta reale. Qui si prova la sola cosa che conta davvero: che non si
/// sblocchi niente senza una ricevuta firmata da Google, e che una ricevuta buona sblocchi
/// una volta sola anche se arriva dieci volte.
/// </summary>
public sealed class IapPurchaseTests : IDisposable
{
    private const string PackageName = "com.apesolution.accardndie";

    private readonly RSA signingKey = RSA.Create(2048);

    public void Dispose() => signingKey.Dispose();

    [Fact]
    public void Valid_receipt_unlocks_every_class()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        var progressService = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("iap-classi");

        IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest
        {
            productId = IapCatalog.ClassesId,
            receipt = BuildReceipt(IapCatalog.ClassesId, "token-classi")
        });

        Assert.True(result.granted);
        Assert.True(result.entitlements.allClasses);
        Assert.False(result.entitlements.allSupreme);
        Assert.False(result.entitlements.noAds);

        SinglePlayerProgressData progress = progressService.GetProgress(player);
        foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All)
            if (entry.Type == SanctuaryCatalog.TypeClass)
                Assert.Contains(entry.Id, progress.unlockedClasses);
        Assert.Empty(progress.unlockedSecondAbilities ?? Array.Empty<string>());
    }

    [Fact]
    public void Bundle_receipt_unlocks_classes_and_supreme_abilities()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        var progressService = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("iap-pacchetto");

        IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest
        {
            productId = IapCatalog.ClassesSupremeId,
            receipt = BuildReceipt(IapCatalog.ClassesSupremeId, "token-pacchetto")
        });

        Assert.True(result.granted);
        Assert.True(result.entitlements.allClasses);
        Assert.True(result.entitlements.allSupreme);

        SinglePlayerProgressData progress = progressService.GetProgress(player);
        foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All)
            if (entry.Type == SanctuaryCatalog.TypeSecondAbility)
                Assert.Contains(entry.Id, progress.unlockedSecondAbilities);
    }

    [Fact]
    public void The_same_receipt_grants_only_once()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        AccountIdentity player = server.RegisterAccount("iap-ripetuto");
        string receipt = BuildReceipt(IapCatalog.NoAdsId, "token-ripetuto");

        for (int attempt = 0; attempt < 3; attempt++)
        {
            IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest
            {
                productId = IapCatalog.NoAdsId,
                receipt = receipt
            });
            Assert.True(result.granted);
            Assert.True(result.entitlements.noAds);
        }

        Assert.Equal(1, CountPurchases(server, player.PlayerId));
    }

    [Fact]
    public void A_receipt_already_redeemed_by_someone_else_is_refused()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        AccountIdentity buyer = server.RegisterAccount("iap-compratore");
        AccountIdentity thief = server.RegisterAccount("iap-passante");
        string receipt = BuildReceipt(IapCatalog.ClassesId, "token-condiviso");

        Assert.True(purchases.Redeem(buyer, new IapRedeemRequest { receipt = receipt }).granted);

        IapRedeemResult stolen = purchases.Redeem(thief, new IapRedeemRequest { receipt = receipt });

        Assert.False(stolen.granted);
        Assert.False(stolen.entitlements.allClasses);
        Assert.Equal(0, CountPurchases(server, thief.PlayerId));
    }

    [Fact]
    public void A_tampered_receipt_unlocks_nothing()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        AccountIdentity player = server.RegisterAccount("iap-falsario");

        // Firma buona, dati cambiati dopo: e' il caso del dispositivo modificato che si
        // riscrive il productId da "no_ads" a "all_classes_supreme".
        string honest = PurchaseData(IapCatalog.NoAdsId, "token-falso", PackageName);
        string forged = honest.Replace(IapCatalog.NoAdsId, IapCatalog.ClassesSupremeId);
        string receipt = Wrap(forged, Sign(honest));

        IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest { receipt = receipt });

        Assert.False(result.granted);
        Assert.False(result.entitlements.allClasses);
        Assert.Equal(0, CountPurchases(server, player.PlayerId));
    }

    [Fact]
    public void A_receipt_from_another_app_is_refused()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        AccountIdentity player = server.RegisterAccount("iap-altra-app");

        string data = PurchaseData(IapCatalog.ClassesId, "token-altra-app", "com.qualcunaltro.gioco");
        IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest
        {
            receipt = Wrap(data, Sign(data))
        });

        Assert.False(result.granted);
        Assert.Equal(0, CountPurchases(server, player.PlayerId));
    }

    [Fact]
    public void Without_a_public_key_nothing_is_redeemable()
    {
        using var server = new TestServer();
        var verifier = new GooglePlayReceiptVerifier(new GooglePlayConfig
        {
            LicenseKey = string.Empty,
            PackageName = PackageName
        });
        var purchases = new IapPurchaseService(
            server.Database, verifier, NullLogger<IapPurchaseService>.Instance);
        AccountIdentity player = server.RegisterAccount("iap-senza-chiave");

        IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest
        {
            receipt = BuildReceipt(IapCatalog.ClassesId, "token-senza-chiave")
        });

        Assert.False(result.granted);
        Assert.Equal(0, CountPurchases(server, player.PlayerId));
    }

    [Fact]
    public void A_pending_purchase_is_not_a_paid_one()
    {
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        AccountIdentity player = server.RegisterAccount("iap-differito");

        string data = PurchaseData(IapCatalog.NoAdsId, "token-differito", PackageName, purchaseState: 2);
        IapRedeemResult result = purchases.Redeem(player, new IapRedeemRequest
        {
            receipt = Wrap(data, Sign(data))
        });

        Assert.False(result.granted);
        Assert.False(result.entitlements.noAds);
    }

    [Fact]
    public void Reading_entitlements_restores_unlocks_that_went_missing()
    {
        // E' il caso di una supreme aggiunta al catalogo dopo l'acquisto: chi aveva comprato
        // il pacchetto deve trovarsela sbloccata al primo accesso utile, senza migrazioni.
        using var server = new TestServer();
        IapPurchaseService purchases = CreateService(server);
        var progressService = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("iap-recupero");

        purchases.Redeem(player, new IapRedeemRequest
        {
            receipt = BuildReceipt(IapCatalog.ClassesSupremeId, "token-recupero")
        });
        ForgetUnlocks(server, player.PlayerId, SanctuaryCatalog.TypeSecondAbility);
        Assert.Empty(progressService.GetProgress(player).unlockedSecondAbilities ?? Array.Empty<string>());

        IapEntitlementsData entitlements = purchases.GetEntitlements(player);

        Assert.True(entitlements.allSupreme);
        Assert.NotEmpty(progressService.GetProgress(player).unlockedSecondAbilities);
    }

    private IapPurchaseService CreateService(TestServer server)
    {
        server.Config.GooglePlay = new GooglePlayConfig
        {
            LicenseKey = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo()),
            PackageName = PackageName
        };
        return new IapPurchaseService(
            server.Database,
            new GooglePlayReceiptVerifier(server.Config.GooglePlay),
            NullLogger<IapPurchaseService>.Instance);
    }

    private string BuildReceipt(string productId, string purchaseToken)
    {
        string data = PurchaseData(productId, purchaseToken, PackageName);
        return Wrap(data, Sign(data));
    }

    private static string PurchaseData(
        string productId, string purchaseToken, string packageName, int purchaseState = 0) =>
        JsonSerializer.Serialize(new
        {
            orderId = "GPA." + purchaseToken,
            packageName,
            productId,
            purchaseTime = 1735689600000L,
            purchaseState,
            purchaseToken,
            acknowledged = false
        });

    private string Sign(string purchaseData) => Convert.ToBase64String(
        signingKey.SignData(
            Encoding.UTF8.GetBytes(purchaseData), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));

    private static string Wrap(string purchaseData, string signature) =>
        JsonSerializer.Serialize(new
        {
            Store = "GooglePlay",
            TransactionID = "GPA.transazione",
            Payload = JsonSerializer.Serialize(new { json = purchaseData, signature })
        });

    private static int CountPurchases(TestServer server, string playerId)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM player_purchases WHERE player_id = $player";
        command.Parameters.AddWithValue("$player", playerId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ForgetUnlocks(TestServer server, string playerId, string unlockType)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM single_player_unlocks WHERE player_id = $player AND unlock_type = $type";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$type", unlockType);
        command.ExecuteNonQuery();
    }
}
