using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	public bool LoadScenario(RoomType roomType, RoomDifficulty difficulty, string bossId = null, string scenarioId = null)
	{
		if ((Object)(object)scenarioCatalog == (Object)null)
		{
			scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		}
		ScenarioDefinition scenario = (((Object)(object)scenarioCatalog != (Object)null) ?scenarioCatalog.Select(roomType, difficulty, bossId, scenarioId) : null);
		return ApplyScenario(scenario);
	}

	private bool ApplyScenario(ScenarioDefinition scenario)
	{
		if ((Object)(object)scenario == (Object)null || (Object)(object)scenario.Background == (Object)null)
		{
			return false;
		}
		currentScenario = scenario;
		currentScenarioDisplayOverride = null;
		if ((Object)(object)backgroundFillImage != (Object)null)
		{
			backgroundFillImage.sprite = scenario.Background;
		}
		if ((Object)(object)terrainImage != (Object)null)
		{
			terrainImage.sprite = scenario.Background;
		}
		if ((Object)(object)terrainAspectFitter != (Object)null)
		{
			AspectRatioFitter aspectRatioFitter = terrainAspectFitter;
			Rect rect = scenario.Background.rect;
			float width = rect.width;
			rect = scenario.Background.rect;
			aspectRatioFitter.aspectRatio = width / rect.height;
		}
		if ((Object)(object)cpuTitleText != (Object)null)
		{
			cpuTitleText.text = "CPU - IL MASTER   *   " + scenario.DisplayName.ToUpperInvariant();
		}
		AppendLog("SCENARIO - " + scenario.DisplayName + " [" + scenario.Id + "]");
		return true;
	}

	private bool ApplyScenario(ScenarioDefinition scenario, string displayOverride)
	{
		if (!ApplyScenario(scenario))
		{
			return false;
		}
		currentScenarioDisplayOverride = displayOverride;
		if ((Object)(object)cpuTitleText != (Object)null && !string.IsNullOrWhiteSpace(displayOverride))
		{
			cpuTitleText.text = "CPU - IL MASTER   *   " + displayOverride.ToUpperInvariant();
		}
		return true;
	}

	private bool LoadCampaignRoomScenario()
	{
		if (currentRoomType == RoomType.Monster)
		{
			return LoadMonsterScenarioForTier(currentMonsterTier);
		}
		return LoadScenario(currentRoomType, pendingRoomDifficulty, null, pendingScenarioId);
	}

	private bool LoadMonsterScenarioForTier(int tier)
	{
		if ((Object)(object)scenarioCatalog == (Object)null)
		{
			scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		}
		string displayOverride = $"Mostro {Mathf.Clamp(tier, 1, 4)}";
		string[] array = Mathf.Clamp(tier, 1, 4) switch
		{
			1 => new string[3] { "monster_1", "default", "monster_2" }, 
			2 => new string[2] { "monster_2", "default" }, 
			3 => new string[3] { "monster_3", "monster_2", "default" }, 
			_ => new string[3] { "monster_4", "monster_3", "default" }, 
		};
		if ((Object)(object)scenarioCatalog != (Object)null)
		{
			string[] array2 = array;
			foreach (string id in array2)
			{
				ScenarioDefinition scenarioDefinition = scenarioCatalog.FindById(id);
				if ((Object)(object)scenarioDefinition != (Object)null && ApplyScenario(scenarioDefinition, displayOverride))
				{
					return true;
				}
			}
		}
		return LoadScenario(RoomType.Monster, pendingRoomDifficulty);
	}

	private void LoadRandomScenario(RoomType roomType)
	{
		if ((Object)(object)scenarioCatalog == (Object)null || scenarioCatalog.Scenarios.Count == 0)
		{
			return;
		}
		RoomDifficulty roomDifficulty = ((roomType != RoomType.Merchant) ?((runProgress.MasterLevel <= 2) ?RoomDifficulty.Easy : ((runProgress.MasterLevel <= 4) ?RoomDifficulty.Normal : RoomDifficulty.Hard)) : ((runProgress.MasterLevel <= 3) ?RoomDifficulty.Easy : RoomDifficulty.Hard));
		List<ScenarioDefinition> list = new List<ScenarioDefinition>();
		List<ScenarioDefinition> list2 = new List<ScenarioDefinition>();
		foreach (ScenarioDefinition scenario in scenarioCatalog.Scenarios)
		{
			if (!((Object)(object)scenario == (Object)null) && scenario.RoomType == roomType)
			{
				list2.Add(scenario);
				if (scenario.Difficulty == roomDifficulty || scenario.Difficulty == RoomDifficulty.Any)
				{
					list.Add(scenario);
				}
			}
		}
		List<ScenarioDefinition> list3 = ((list.Count > 0) ?list : list2);
		if (list3.Count > 0)
		{
			ApplyScenario(list3[random.NextInclusive(0, list3.Count - 1)]);
		}
	}

	private RoomType GenerateNextRoomType()
	{
		ProgressionConfiguration progression = configuration.Progression;
		int num = runProgress.RoomsCleared + 1;
		if (ShouldForceFirstRoomComposableGolem())
		{
			return RoomType.Boss;
		}
		if (num == progression.FinalBossRoom || (progression.MinibossEveryRooms > 0 && num % progression.MinibossEveryRooms == 0))
		{
			return RoomType.Boss;
		}
		int num2 = progression.MonsterRoomWeight + progression.MerchantRoomWeight + progression.LootRoomWeight + progression.OpportunityRoomWeight;
		if (num2 <= 0)
		{
			return RoomType.Monster;
		}
		int num3 = random.NextInclusive(1, num2);
		if (num3 <= progression.MonsterRoomWeight)
		{
			return RoomType.Monster;
		}
		num3 -= progression.MonsterRoomWeight;
		if (num3 <= progression.MerchantRoomWeight)
		{
			return RoomType.Merchant;
		}
		num3 -= progression.MerchantRoomWeight;
		if (num3 <= progression.LootRoomWeight)
		{
			return RoomType.Loot;
		}
		return RoomType.UnexpectedOpportunity;
	}

	private void BeginRoomChoice()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		ClearDraftEntranceState();
		StopMusic();
		SetCombatChromeVisible(visible: true);
		inputLocked = true;
		gameFinished = true;
		canAdvanceToNextRoom = false;
		currentMonsterTier = 2;
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Normal;
		currentScenarioDisplayOverride = null;
		activeComposableGolem = null;
		playerAura = BattleAuraType.None;
		cpuAura = BattleAuraType.None;
		formationAuraUsed = false;
		ResetRoundAuraUsage();
		((Component)restartButton).gameObject.SetActive(false);
		((Component)confirmFormationButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
		CloseMerchantPanel();
		ConfigureActionButtonLayout(merchantVisible: false);
		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		DestroyPrototypeViews(draftViews);
		DestroyPrototypeViews(playerDeploymentPreviewViews);
		DestroyPrototypeViews(cpuDeploymentPreviewViews);
		turnOrder.Clear();
		initialPlayerFormation.Clear();
		initialPlayerCampaignFormation.Clear();
		initialCpuFormation.Clear();
		PrepareCampaignDoors();
		RefreshRoomChoiceLayout();
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(true);
		}
		SetTurnBanner(playerTurn: true, "SCELTA DELLA VIA");
		SetMessage("Scegli una delle tre porte per proseguire nella campagna.");
		RefreshInitiativeDisplay();
		ApplyResponsiveLayout();
		if (ShouldForceFirstRoomComposableGolem())
		{
			((MonoBehaviour)this).StartCoroutine(ChooseDebugMinibossDoor());
		}
	}

	private IEnumerator ChooseDebugMinibossDoor()
	{
		yield return null;
		if ((Object)(object)roomChoicePanel != (Object)null && roomChoicePanel.activeSelf)
		{
			ChooseCampaignDoor(0);
		}
	}

	private void PrepareCampaignDoors()
	{
		campaignDoors.Clear();
		List<CampaignDoorDifficulty> list = new List<CampaignDoorDifficulty>
		{
			CampaignDoorDifficulty.Easy,
			CampaignDoorDifficulty.Normal,
			CampaignDoorDifficulty.Hard
		};
		for (int num = list.Count - 1; num > 0; num--)
		{
			int num2 = random.NextInclusive(0, num);
			List<CampaignDoorDifficulty> list2 = list;
			int index = num;
			List<CampaignDoorDifficulty> list3 = list;
			int index2 = num2;
			CampaignDoorDifficulty campaignDoorDifficulty = list[num2];
			CampaignDoorDifficulty campaignDoorDifficulty2 = list[num];
			CampaignDoorDifficulty campaignDoorDifficulty3 = (list2[index] = campaignDoorDifficulty);
			campaignDoorDifficulty3 = (list3[index2] = campaignDoorDifficulty2);
		}
		foreach (CampaignDoorDifficulty item in list)
		{
			campaignDoors.Add(new CampaignDoor(item));
		}
	}

	private void ChooseCampaignDoor(int index)
	{
		if (index >= 0 && index < campaignDoors.Count && !((Object)(object)roomTransition == (Object)null) && !roomTransition.IsPlaying)
		{
			CampaignDoor campaignDoor = campaignDoors[index];
			CampaignRoomRoll roomRoll = RollCampaignRoom(campaignDoor.Difficulty);
			currentRoomType = roomRoll.RoomType;
			currentMonsterTier = ((roomRoll.RoomType == RoomType.Monster) ?Mathf.Clamp(roomRoll.MonsterTier + ConsumeNextMonsterTierBonus(), 1, 4) : roomRoll.MonsterTier);
			pendingScenarioId = roomRoll.ScenarioId;
			pendingRoomDifficulty = ((currentRoomType == RoomType.Monster) ?DifficultyForMonsterTier(currentMonsterTier) : roomRoll.Difficulty);
			if (currentRoomType == RoomType.Monster)
			{
				pendingScenarioId = ScenarioIdForMonsterTier(currentMonsterTier);
			}
			AppendLog($"PORTA SCELTA - slot {index + 1}, difficolta nascosta {campaignDoor.Difficulty}, stanza {DescribeRoomRoll(roomRoll)}");
			AnimationConfiguration animation = configuration.Animation;
			PlayTransitionSfx();
			roomTransition.Play(EnterChosenCampaignRoom, animation.RoomFadeOutDuration, animation.RoomBlackHoldDuration, animation.RoomFadeInDuration);
		}
	}

	private CampaignRoomRoll RollCampaignRoom(CampaignDoorDifficulty difficulty)
	{
		int num = runProgress.RoomsCleared + 1;
		ProgressionConfiguration progression = configuration.Progression;
		if (ShouldForceFirstRoomComposableGolem())
		{
			return new CampaignRoomRoll(RoomType.Boss, 4, null, RoomDifficulty.Hard);
		}
		if (num == progression.FinalBossRoom || (progression.MinibossEveryRooms > 0 && num % progression.MinibossEveryRooms == 0))
		{
			return new CampaignRoomRoll(RoomType.Boss, 4, null, RoomDifficulty.Hard);
		}
		return difficulty switch
		{
			CampaignDoorDifficulty.Easy => RollEasyDoorRoom(), 
			CampaignDoorDifficulty.Normal => RollNormalDoorRoom(), 
			CampaignDoorDifficulty.Hard => RollHardDoorRoom(), 
			_ => RollNormalDoorRoom(), 
		};
	}

	private CampaignRoomRoll RollEasyDoorRoom()
	{
		return RollAllowedDoorRoom(8, (int roll) => roll switch
		{
			1 => MonsterRoomRoll(1), 
			2 => MonsterRoomRoll(2), 
			3 => MonsterRoomRoll(3), 
			4 => MonsterRoomRoll(4), 
			5 => new CampaignRoomRoll(RoomType.Merchant, 0, "low_merchant", RoomDifficulty.Easy), 
			6 => new CampaignRoomRoll(RoomType.Merchant, 0, "god_merchant", RoomDifficulty.Hard), 
			7 => new CampaignRoomRoll(RoomType.UnexpectedOpportunity, 0, "unexpected_opportunity", RoomDifficulty.Any), 
			_ => new CampaignRoomRoll(RoomType.Loot, 0, "loot", RoomDifficulty.Any), 
		});
	}

	private CampaignRoomRoll RollNormalDoorRoom()
	{
		return RollAllowedDoorRoom(6, (int roll) => roll switch
		{
			1 => MonsterRoomRoll(1), 
			2 => MonsterRoomRoll(2), 
			3 => MonsterRoomRoll(3), 
			4 => MonsterRoomRoll(4), 
			5 => new CampaignRoomRoll(RoomType.Merchant, 0, "low_merchant", RoomDifficulty.Easy), 
			_ => new CampaignRoomRoll(RoomType.Merchant, 0, "god_merchant", RoomDifficulty.Hard), 
		});
	}

	private CampaignRoomRoll RollHardDoorRoom()
	{
		return RegisterCampaignRoomRoll(MonsterRoomRoll(random.NextInclusive(1, 4)));
	}

	private CampaignRoomRoll RollAllowedDoorRoom(int rollSides, Func<int, CampaignRoomRoll> rollFactory)
	{
		for (int i = 0; i < rollSides * 2; i++)
		{
			CampaignRoomRoll roomRoll = rollFactory(random.NextInclusive(1, rollSides));
			if (IsCampaignRoomRollAllowed(roomRoll))
			{
				return RegisterCampaignRoomRoll(roomRoll);
			}
		}
		return RegisterCampaignRoomRoll(MonsterRoomRoll(random.NextInclusive(1, 4)));
	}

	private bool IsCampaignRoomRollAllowed(CampaignRoomRoll roomRoll)
	{
		if (merchantRoomsBlockedUntilMonster && roomRoll.RoomType == RoomType.Merchant)
		{
			return false;
		}
		if (rewardRoomsBlockedUntilMonster && (roomRoll.RoomType == RoomType.Loot || roomRoll.RoomType == RoomType.UnexpectedOpportunity))
		{
			return false;
		}
		return true;
	}

	private CampaignRoomRoll RegisterCampaignRoomRoll(CampaignRoomRoll roomRoll)
	{
		if (roomRoll.RoomType == RoomType.Monster)
		{
			merchantRoomsBlockedUntilMonster = false;
			rewardRoomsBlockedUntilMonster = false;
		}
		else if (roomRoll.RoomType == RoomType.Merchant)
		{
			merchantRoomsBlockedUntilMonster = true;
		}
		else if (roomRoll.RoomType == RoomType.Loot || roomRoll.RoomType == RoomType.UnexpectedOpportunity)
		{
			rewardRoomsBlockedUntilMonster = true;
		}
		return roomRoll;
	}

	private static CampaignRoomRoll MonsterRoomRoll(int tier)
	{
		int num = Mathf.Clamp(tier, 1, 4);
		string scenarioId = ScenarioIdForMonsterTier(num);
		return new CampaignRoomRoll(RoomType.Monster, num, scenarioId, num switch
		{
			1 => RoomDifficulty.Easy, 
			2 => RoomDifficulty.Easy, 
			3 => RoomDifficulty.Normal, 
			_ => RoomDifficulty.Hard, 
		});
	}

	private int ConsumeNextMonsterTierBonus()
	{
		int num = nextMonsterTierBonus;
		nextMonsterTierBonus = 0;
		if (num > 0)
		{
			AppendLog($"PRESAGIO - il prossimo mostro sale di {num} tier.");
		}
		return num;
	}

	private static string ScenarioIdForMonsterTier(int tier)
	{
		return Mathf.Clamp(tier, 1, 4) switch
		{
			3 => "monster_3", 
			4 => "monster_4", 
			_ => "monster_2", 
		};
	}

	private static RoomDifficulty DifficultyForMonsterTier(int tier)
	{
		int num = Mathf.Clamp(tier, 1, 4);
		if (num > 2)
		{
			if (num == 3)
			{
				return RoomDifficulty.Normal;
			}
			return RoomDifficulty.Hard;
		}
		return RoomDifficulty.Easy;
	}

	private static string DescribeRoomRoll(CampaignRoomRoll roomRoll)
	{
		if (roomRoll.RoomType != RoomType.Monster)
		{
			return roomRoll.RoomType.ToString();
		}
		return $"Mostro {roomRoll.MonsterTier}";
	}

	private void EnterChosenCampaignRoom()
	{
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(false);
		}
		((Component)merchantBuyButton).gameObject.SetActive(false);
		ConfigureActionButtonLayout(merchantVisible: false);
		AppendLog("STANZA ESTRATTA - " + DescribeRoomRoll(new CampaignRoomRoll(currentRoomType, currentMonsterTier, pendingScenarioId, pendingRoomDifficulty)));
		if (!LoadCampaignRoomScenario())
		{
			currentScenarioDisplayOverride = DescribeRoomRoll(new CampaignRoomRoll(currentRoomType, currentMonsterTier, pendingScenarioId, pendingRoomDifficulty));
			AppendLog("SCENARIO - fallback nome stanza: scenario non trovato o non valido.");
		}
		PlayCurrentRoomEnterSfx();
		ActivateMinibossForCurrentRoom();
		if (currentRoomType != RoomType.Monster && currentRoomType != RoomType.Boss)
		{
			((MonoBehaviour)this).StartCoroutine(EnterNonCombatRoom(currentRoomType));
			return;
		}
		initialCpuFormation.Clear();
		if (campaignDeck != null)
		{
			PrepareNextCampaignCombatDraft();
			return;
		}
		if (currentRoomType == RoomType.Boss)
		{
			List<CardDefinition> list = DrawBossFormationForCurrentCombat();
			initialCpuFormation.AddRange(list);
			if (list.All((CardDefinition card) => card.Category != CardCategory.Boss))
			{
				AppendLog("BOSS FALLBACK - nessuna carta Boss disponibile; usato un Mostro come sostituto.");
			}
		}
		else
		{
			initialCpuFormation.AddRange(DrawMonsterFormationForCurrentTier());
		}
		ResetBattle();
	}

	private void ActivateMinibossForCurrentRoom()
	{
		activeComposableGolem = null;
		activeComposableGolemPreview = null;
		if (currentRoomType != RoomType.Boss || !IsCurrentRoomMinibossRoom())
		{
			return;
		}
		if ((Object)(object)FindCardDefinition(ComposableGolemCardId) == (Object)null)
		{
			AppendLog("MINIBOSS - carta proxy Golem Componibile assente dal CardDatabase; miniboss non attivato.");
			return;
		}
		MinibossKind miniboss = RollMinibossKind();
		switch (miniboss)
		{
		case MinibossKind.ComposableGolem:
			activeComposableGolem = new ComposableGolem(random);
			AppendLog("MINIBOSS - Golem Componibile entra nella stanza.");
			break;
		}
	}

	private bool IsCurrentRoomMinibossRoom()
	{
		if (ShouldForceFirstRoomComposableGolem())
		{
			return true;
		}
		ProgressionConfiguration progression = configuration.Progression;
		int roomNumber = runProgress.RoomsCleared + 1;
		return progression.MinibossEveryRooms > 0
			&& roomNumber % progression.MinibossEveryRooms == 0
			&& roomNumber != progression.FinalBossRoom;
	}

	private bool ShouldForceFirstRoomComposableGolem()
	{
		return debugForceFirstRoomComposableGolem && runProgress != null && runProgress.RoomsCleared == 0;
	}

	private MinibossKind RollMinibossKind()
	{
		MinibossKind[] pool =
		{
			MinibossKind.ComposableGolem
		};
		return pool[random.NextInclusive(0, pool.Length - 1)];
	}

	private List<CardDefinition> DrawBossFormationForCurrentCombat()
	{
		if (activeComposableGolem != null)
		{
			CardDefinition golemProxy = FindCardDefinition(ComposableGolemCardId);
			if ((Object)(object)golemProxy != (Object)null)
			{
				return new List<CardDefinition> { golemProxy };
			}
			AppendLog("MINIBOSS - carta proxy Golem Componibile non trovata; disattivo il Golem e uso fallback Boss.");
			activeComposableGolem = null;
		}
		return formationDraftService.DrawBossCandidates(cardDatabase.Cards, configuration.Progression.BossFormationSize);
	}

	private CardDefinition FindCardDefinition(string id)
	{
		if (cardDatabase == null || string.IsNullOrWhiteSpace(id))
		{
			return null;
		}
		return cardDatabase.Cards.FirstOrDefault((CardDefinition card) => (Object)(object)card != (Object)null && string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private bool IsComposableGolemProxy(BattleCardState card)
	{
		return activeComposableGolem != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, ComposableGolemCardId, StringComparison.OrdinalIgnoreCase);
	}
}
}
