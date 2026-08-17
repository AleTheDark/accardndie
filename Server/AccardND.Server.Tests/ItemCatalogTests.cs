using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccardND.Server.Tests;

public sealed class ItemCatalogTests
{
    [Fact]
    public void Sanctuary_exposes_relics_and_bag_slots_while_shop_exposes_every_item()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("catalogo-item");

        SanctuaryData data = progress.GetSanctuary(player);

        Assert.DoesNotContain(data.entries, entry => entry.type == SanctuaryCatalog.TypeItem);
        Assert.DoesNotContain(data.shopCatalog, entry => entry.id == "defrost");
        Assert.Equal(
            SanctuaryCatalog.All.Count(entry => entry.Type == SanctuaryCatalog.TypeItem),
            data.shopCatalog.Length);
        Assert.All(data.shopCatalog, entry =>
        {
            Assert.True(entry.owned);
            Assert.True(entry.available);
            Assert.True(entry.copyCost > 0);
        });
		Assert.Contains(data.entries, entry => entry.type == SanctuaryCatalog.TypeSlot && entry.id == "bag-slot-3");
		Assert.Contains(data.entries, entry => entry.type == SanctuaryCatalog.TypeSlot && entry.id == "bag-slot-4");
		Assert.Contains(data.entries, entry => entry.type == SanctuaryCatalog.TypeSlot && entry.id == "bag-slot-5");
		Assert.Contains(data.entries, entry => entry.type == SanctuaryCatalog.TypeSlot && entry.id == "bag-slot-6");
		Assert.Contains(data.entries, entry => entry.id == SanctuaryCatalog.MerchantUpgradeRelicOneId && entry.honeyCost == 80);
		Assert.Contains(data.entries, entry => entry.id == SanctuaryCatalog.MerchantUpgradeRelicTwoId && entry.honeyCost == 160);
	}

	[Fact]
	public void Second_merchant_upgrade_relic_requires_the_first()
	{
		using var server = new TestServer();
		var progress = new SinglePlayerProgressService(server.Database);
		AccountIdentity player = server.RegisterAccount("reliquie-upgrade");
		progress.GetSanctuary(player);
		GiveHoney(server, player, 500);

		(_, string blockedCode, _) = progress.PurchaseUnlock(player,
			new SinglePlayerPurchaseUnlockRequest
			{
				type = SanctuaryCatalog.TypeSlot,
				id = SanctuaryCatalog.MerchantUpgradeRelicTwoId
			});
		Assert.Equal(ErrorCodes.InvalidProgressionRequest, blockedCode);

		(SinglePlayerProgressData first, string firstCode, _) = progress.PurchaseUnlock(player,
			new SinglePlayerPurchaseUnlockRequest
			{
				type = SanctuaryCatalog.TypeSlot,
				id = SanctuaryCatalog.MerchantUpgradeRelicOneId
			});
		Assert.Null(firstCode);
		Assert.Contains(SanctuaryCatalog.MerchantUpgradeRelicOneId, first.unlockedSlots);

		(SinglePlayerProgressData second, string secondCode, _) = progress.PurchaseUnlock(player,
			new SinglePlayerPurchaseUnlockRequest
			{
				type = SanctuaryCatalog.TypeSlot,
				id = SanctuaryCatalog.MerchantUpgradeRelicTwoId
			});
		Assert.Null(secondCode);
		Assert.Contains(SanctuaryCatalog.MerchantUpgradeRelicTwoId, second.unlockedSlots);
	}

	[Fact]
	public void Bag_slots_are_bought_in_order_up_to_six_with_increasing_honey_costs()
	{
		using var server = new TestServer();
		var progress = new SinglePlayerProgressService(server.Database);
		AccountIdentity player = server.RegisterAccount("bisaccia-sei-slot");
		progress.GetSanctuary(player);
		GiveHoney(server, player, 2000);

		(_, string blockedCode, string blockedError) = progress.PurchaseUnlock(
			player,
			new SinglePlayerPurchaseUnlockRequest { type = SanctuaryCatalog.TypeSlot, id = "bag-slot-6" });

		Assert.Equal(ErrorCodes.InvalidProgressionRequest, blockedCode);
		Assert.Contains("slot precedente", blockedError);

		int expectedHoney = 2000;
		foreach ((string id, int cost) in new[]
		{
			("bag-slot-3", 30),
			("bag-slot-4", 60),
			("bag-slot-5", 120),
			("bag-slot-6", 240)
		})
		{
			(SinglePlayerProgressData after, string code, string error) = progress.PurchaseUnlock(
				player,
				new SinglePlayerPurchaseUnlockRequest { type = SanctuaryCatalog.TypeSlot, id = id });

			expectedHoney -= cost;
			Assert.Null(code);
			Assert.Null(error);
			Assert.Equal(expectedHoney, after.honey);
		}

		Assert.Equal(6, progress.GetSanctuary(player).bagSlots);
	}

    [Fact]
    public void Item_can_be_bought_without_a_legacy_unlock_row()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("compra-item");
        progress.GetSanctuary(player);
        using (SqliteConnection connection = server.Database.Open())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "UPDATE single_player_progress SET honey = 100 WHERE player_id = $id";
            command.Parameters.AddWithValue("$id", player.PlayerId);
            command.ExecuteNonQuery();
        }

        (SanctuaryData data, string errorCode, string error) = progress.BuyItem(
            player,
            new SanctuaryBuyItemRequest { itemId = "detector" });

        Assert.Null(errorCode);
        Assert.Null(error);
        Assert.Contains(data.stash, item => item.itemId == "detector" && item.count == 1);
        Assert.Equal(95, data.honey);
    }

    [Fact]
    public void Unused_bag_item_remains_in_account_stash_when_campaign_ends()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("bisaccia-non-usata");
        progress.GetSanctuary(player);
        GiveHoney(server, player, 100);

        progress.BuyItem(player, new SanctuaryBuyItemRequest { itemId = "detector" });
        progress.SetBag(player, new SanctuarySetBagRequest { itemIds = new[] { "detector" } });
        progress.ClaimDeathReward(player, EndRun("unused-item-run", Array.Empty<string>()));

        SanctuaryData data = progress.GetSanctuary(player);
        Assert.Contains(data.stash, item => item.itemId == "detector" && item.count == 1);
        Assert.Contains("detector", data.bag);
    }

    [Fact]
    public void Campaign_end_consumes_only_the_bag_items_that_were_used()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("bisaccia-usata");
        progress.GetSanctuary(player);
        GiveHoney(server, player, 100);

        progress.BuyItem(player, new SanctuaryBuyItemRequest { itemId = "detector" });
        progress.BuyItem(player, new SanctuaryBuyItemRequest { itemId = "detector" });
        progress.SetBag(player, new SanctuarySetBagRequest { itemIds = new[] { "detector" } });
        progress.ClaimDeathReward(player, EndRun("used-item-run", new[] { "detector" }));

        SanctuaryData data = progress.GetSanctuary(player);
        Assert.Contains(data.stash, item => item.itemId == "detector" && item.count == 1);
        Assert.Contains("detector", data.bag);
    }

    [Fact]
    public void Items_found_in_run_and_never_used_end_up_in_the_stash()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("bottino-di-run");
        progress.GetSanctuary(player);

        SinglePlayerDeathRewardRequest request = EndRun("loot-run", Array.Empty<string>());
        request.keptItemIds = new[] { "jolly", "jolly", "non-esiste" };
        progress.ClaimDeathReward(player, request);

        SanctuaryData data = progress.GetSanctuary(player);
        Assert.Contains(data.stash, item => item.itemId == "jolly" && item.count == 2);
        Assert.DoesNotContain(data.stash, item => item.itemId == "non-esiste");
        // La scorta cresce, la bisaccia no: quella la sceglie il giocatore al Santuario.
        Assert.Empty(data.bag);
    }

    [Fact]
    public void Items_used_counter_is_not_capped_to_the_bag_items()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("oggetti-usati-in-run");
        progress.GetSanctuary(player);

        SinglePlayerDeathRewardRequest request = EndRun("mixed-items-run", Array.Empty<string>());
        // Nessun oggetto dalla bisaccia: tutti trovati in campagna o comprati al mercante.
        request.itemsUsed = 4;
        progress.ClaimDeathReward(player, request);

        PlayerCounterData[] counters = progress.GetProgress(player).counters;
        Assert.Contains(counters, counter =>
            counter.key == CampaignCounters.ItemsUsed && counter.value == 4);
    }

    private static void GiveHoney(TestServer server, AccountIdentity player, int amount)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE single_player_progress SET honey = $amount WHERE player_id = $id";
        command.Parameters.AddWithValue("$amount", amount);
        command.Parameters.AddWithValue("$id", player.PlayerId);
        command.ExecuteNonQuery();
    }

    private static SinglePlayerDeathRewardRequest EndRun(string runId, string[] consumedItemIds) => new()
    {
        runId = runId,
        mode = "campaign",
        chapterId = "chapter-1",
        stageId = "climbing",
        consumedItemIds = consumedItemIds,
        itemsUsed = consumedItemIds.Length
    };
}
