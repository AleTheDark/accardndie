using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	// Scena LootRoomDebug: salta menu, deck builder e porte, poi apre subito una
	// stanza tesoro con un mazzo vuoto cosi' la pool ricompense e' ampia.
	private void StartLootRoomDebugRun()
	{
		HideLootRoomDebugMenus();
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		if ((Object)(object)cardDatabase == (Object)null)
		{
			SetMessage("LOOT DEBUG: CardDatabase non trovato.");
			return;
		}
		if (formationDraftService == null)
		{
			formationDraftService = new FormationDraftService(random);
		}

		campaignDeck = new CampaignDeckState(System.Array.Empty<CardDefinition>());
		playerReserve.Clear();
		initialPlayerReserve.Clear();
		campaignConsumables.Clear();
		ResetScenarioRuleState();
		AppendLog("LOOT DEBUG - avvio diretto stanza tesoro.");
		EnterLootRoomDebugRoom();
	}

	private void HideLootRoomDebugMenus()
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

	private void EnterLootRoomDebugRoom()
	{
		currentRoomType = RoomType.Loot;
		currentMonsterTier = 0;
		pendingScenarioId = "loot";
		pendingRoomDifficulty = RoomDifficulty.Any;
		if (!LoadCampaignRoomScenario())
		{
			currentScenarioDisplayOverride = "Tesoro";
			AppendLog("LOOT DEBUG - scenario loot non trovato, uso il nome di fallback.");
		}
		RefreshPlayerHud();
		PlayCurrentRoomEnterSfx();
		((MonoBehaviour)this).StartCoroutine(EnterNonCombatRoom(RoomType.Loot));
	}
}
}
