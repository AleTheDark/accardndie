using System.Collections.Generic;
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	// Scena MerchantDebug: salta selezione modalita', deck builder e draft, e apre una stanza
	// mercato con mazzo ed esperienza fittizi. Ogni CONTINUA riporta a un nuovo mercato, cosi'
	// si puo' verificare che vetrina e vincolo di banco si azzerino a ogni stanza.
	private const int MerchantDebugExperience = 250;

	private const int MerchantDebugDeckSize = 8;

	private const int MerchantDebugGraveyardCards = 2;

	private const int MerchantDebugCooldownCards = 1;

	private bool ShouldForceMerchantDebugRoom()
	{
		return debugMerchantScene && runProgress != null;
	}

	private void StartMerchantDebugRun()
	{
		HideMerchantDebugMenus();
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		if ((Object)(object)cardDatabase == (Object)null)
		{
			SetMessage("MERCATO DEBUG: CardDatabase non trovato.");
			return;
		}
		campaignDeck = new CampaignDeckState(BuildMerchantDebugDeck());
		SeedMerchantDebugZones();
		playerReserve.Clear();
		initialPlayerReserve.Clear();
		foreach (CampaignCardInstance card in campaignDeck.Cards)
		{
			playerReserve.Add(card.Definition);
			initialPlayerReserve.Add(card.Definition);
		}
		campaignConsumables.Clear();
		ResetScenarioRuleState();
		runProgress.AddExperience(MerchantDebugExperience);
		AppendLog($"MERCATO DEBUG - {campaignDeck.Cards.Count} carte nel mazzo "
			+ $"({campaignDeck.GraveyardCount} al cimitero, {campaignDeck.CooldownCount} in cooldown), "
			+ $"{MerchantDebugExperience} EXP disponibili.");
		EnterMerchantDebugRoom();
	}

	private void HideMerchantDebugMenus()
	{
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)deckBuilderPanel != (Object)null)
		{
			deckBuilderPanel.SetActive(false);
		}
		if ((Object)(object)initialDraftPanel != (Object)null)
		{
			initialDraftPanel.SetActive(false);
		}
		SetAccountHubHudActive(false);
	}

	private List<CardDefinition> BuildMerchantDebugDeck()
	{
		List<CardDefinition> pool = GetMerchantCardPool();
		List<CardDefinition> deck = new List<CardDefinition>();
		while (deck.Count < MerchantDebugDeckSize && pool.Count > 0)
		{
			CardDefinition definition = pool[random.NextInclusive(0, pool.Count - 1)];
			pool.Remove(definition);
			deck.Add(definition);
		}
		return deck;
	}

	// Sposta alcune carte in cimitero e cooldown: senza questo RECUPERA non e' testabile.
	private void SeedMerchantDebugZones()
	{
		List<CampaignCardRestoreEntry> entries = new List<CampaignCardRestoreEntry>();
		int index = 0;
		foreach (CampaignCardInstance card in campaignDeck.Cards)
		{
			CampaignCardZone zone = CampaignCardZone.Deck;
			if (index < MerchantDebugGraveyardCards)
			{
				zone = CampaignCardZone.Graveyard;
			}
			else if (index < MerchantDebugGraveyardCards + MerchantDebugCooldownCards)
			{
				zone = CampaignCardZone.Cooldown;
			}
			entries.Add(new CampaignCardRestoreEntry(card.Definition, zone, card.InstanceId));
			index++;
		}
		campaignDeck.RestoreFrom(entries, campaignDeck.NextInstanceId);
	}

	private void EnterMerchantDebugRoom()
	{
		currentRoomType = RoomType.Merchant;
		pendingScenarioId = "god_merchant";
		pendingRoomDifficulty = RoomDifficulty.Hard;
		if (!LoadCampaignRoomScenario())
		{
			currentScenarioDisplayOverride = "Mercato";
			AppendLog("MERCATO DEBUG - scenario god_merchant non trovato, uso il nome di fallback.");
		}
		RefreshPlayerHud();
		PlayCurrentRoomEnterSfx();
		((MonoBehaviour)this).StartCoroutine(EnterNonCombatRoom(RoomType.Merchant));
	}
}
}
