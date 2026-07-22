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
		RefreshScenarioBackground();
		if ((Object)(object)cpuTitleText != (Object)null)
		{
			cpuTitleText.text = "CPU - IL MASTER   *   " + scenario.DisplayName.ToUpperInvariant();
		}
		AppendLog("SCENARIO - " + scenario.DisplayName + " [" + scenario.Id + "]");
		return true;
	}

	private Sprite CurrentScenarioBackgroundSprite()
	{
		if ((Object)(object)currentScenario == (Object)null)
		{
			return Resources.Load<Sprite>("Backgrounds/Background_terrain");
		}
		if (Screen.width > Screen.height && (Object)(object)currentScenario.BackgroundLandscape != (Object)null)
		{
			return currentScenario.BackgroundLandscape;
		}
		return currentScenario.Background;
	}

	private void RefreshScenarioBackground()
	{
		Sprite sprite = CurrentScenarioBackgroundSprite();
		if ((Object)(object)sprite == (Object)null)
		{
			return;
		}
		if ((Object)(object)backgroundFillImage != (Object)null)
		{
			backgroundFillImage.sprite = sprite;
		}
		if ((Object)(object)terrainImage != (Object)null)
		{
			terrainImage.sprite = sprite;
		}
		if ((Object)(object)terrainAspectFitter != (Object)null)
		{
			Rect rect = sprite.rect;
			terrainAspectFitter.aspectRatio = rect.width / rect.height;
		}
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
			string activeScenarioId = ActiveCampaignScenarioId();
			return LoadScenario(
				RoomType.Any,
				RoomDifficulty.Any,
				null,
				string.IsNullOrWhiteSpace(activeScenarioId) ? "default" : activeScenarioId);
		}
		return LoadScenario(currentRoomType, pendingRoomDifficulty, null, pendingScenarioId);
	}

	private void BeginRoomChoice()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		ClearDraftEntranceState();
		StopMusic();
		SetBattlefieldSurfaceVisible(visible: true);
		inputLocked = true;
		gameFinished = true;
		canAdvanceToNextRoom = false;
		currentMonsterTier = 2;
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Normal;
		currentScenarioDisplayOverride = null;
		activeComposableGolem = null;
		activeMedusaBoss = null;
		activeTrentorBoss = null;
		activeBragusBoss = null;
		activePalatirBoss = null;
		playerAura = BattleAuraType.None;
		cpuAura = BattleAuraType.None;
		formationAuraUsed = false;
		((Component)restartButton).gameObject.SetActive(false);
		((Component)confirmActionButton).gameObject.SetActive(false);
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
		roomChoiceBackgroundIndex = random != null ? random.NextInclusive(1, 5) : UnityEngine.Random.Range(1, 6);
		// Punto di salvataggio autorevole: lo stato tra le stanze è coerente qui.
		SaveCurrentRun();
		PrepareCampaignDoors();
		RefreshRoomChoiceLayout();
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(true);
		}
		SetTurnBanner(playerTurn: true, "SCELTA DELLA VIA");
		SetMessage("Scegli una delle tre porte per proseguire nella campagna.");
		ShowRoomChoiceHint();
		RefreshInitiativeDisplay();
		ApplyResponsiveLayout();
		if (ShouldForceFirstRoomComposableGolem() || ShouldForceFirstRoomMedusa() || ShouldForceFirstRoomTrentor() || ShouldForceFirstRoomBragus() || ShouldForceFirstRoomPalatir())
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
			campaignDoors.Add(nextDoorChoiceRevealed
				? new CampaignDoor(item, RollCampaignRoomPreview(item))
				: new CampaignDoor(item));
		}
		nextDoorChoiceRevealed = false;
		RefreshRoomChoiceRevealLabels();
	}

	private void RefreshRoomChoiceRevealLabels()
	{
		for (int i = 0; i < roomChoiceRevealLabels.Count; i++)
		{
			Text label = roomChoiceRevealLabels[i];
			if ((Object)(object)label == (Object)null)
			{
				continue;
			}
			bool revealed = i < campaignDoors.Count && campaignDoors[i].RevealedRoom.HasValue;
			((Component)label).gameObject.SetActive(revealed);
			if (revealed)
			{
				label.text = DescribeRoomRoll(campaignDoors[i].RevealedRoom.Value).ToUpperInvariant();
			}
		}
	}

	private void RevealCurrentCampaignDoorsWithDetector()
	{
		if (campaignDoors.Count == 0)
		{
			nextDoorChoiceRevealed = true;
			return;
		}
		for (int i = 0; i < campaignDoors.Count; i++)
		{
			CampaignDoor door = campaignDoors[i];
			if (!door.RevealedRoom.HasValue)
			{
				campaignDoors[i] = new CampaignDoor(door.Difficulty, RollCampaignRoomPreview(door.Difficulty));
			}
		}
		RefreshRoomChoiceRevealLabels();
		((MonoBehaviour)this).StartCoroutine(AnimateRoomChoiceRevealLabels());
	}

	private IEnumerator AnimateRoomChoiceRevealLabels()
	{
		for (int i = 0; i < roomChoiceRevealLabels.Count; i++)
		{
			Text label = roomChoiceRevealLabels[i];
			if ((Object)(object)label == (Object)null || !((Component)label).gameObject.activeSelf)
			{
				continue;
			}
			RectTransform rect = label.rectTransform;
			rect.localScale = new Vector3(0.82f, 0.82f, 1f);
			label.canvasRenderer.SetAlpha(0f);
			label.CrossFadeAlpha(1f, 0.28f, true);
			((MonoBehaviour)this).StartCoroutine(PopRoomChoiceRevealLabel(rect));
			yield return WaitForCardInspectionPause(0.12f);
		}
	}

	private IEnumerator PopRoomChoiceRevealLabel(RectTransform rect)
	{
		float elapsed = 0f;
		const float duration = 0.28f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float scale = Mathf.Lerp(0.82f, 1.08f, Mathf.Sin(t * Mathf.PI * 0.5f));
			rect.localScale = new Vector3(scale, scale, 1f);
			yield return null;
		}
		elapsed = 0f;
		const float settleDuration = 0.12f;
		while (elapsed < settleDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / settleDuration);
			float scale = Mathf.Lerp(1.08f, 1f, t);
			rect.localScale = new Vector3(scale, scale, 1f);
			yield return null;
		}
		rect.localScale = Vector3.one;
	}

	private void ChooseCampaignDoor(int index)
	{
		if (index >= 0 && index < campaignDoors.Count && !((Object)(object)roomTransition == (Object)null) && !roomTransition.IsPlaying)
		{
			CampaignDoor campaignDoor = campaignDoors[index];
			CampaignRoomRoll roomRoll = campaignDoor.RevealedRoom ?? RollCampaignRoom(campaignDoor.Difficulty);
			if (campaignDoor.RevealedRoom.HasValue)
			{
				RegisterCampaignRoomRoll(roomRoll);
			}
			currentRoomType = roomRoll.RoomType;
			currentMonsterTier = ((roomRoll.RoomType == RoomType.Monster) ?Mathf.Clamp(roomRoll.MonsterTier + ConsumeNextMonsterTierBonus(), 1, 4) : roomRoll.MonsterTier);
			pendingScenarioId = roomRoll.ScenarioId;
			pendingRoomDifficulty = ((currentRoomType == RoomType.Monster) ?DifficultyForMonsterTier(currentMonsterTier) : roomRoll.Difficulty);
			if (currentRoomType == RoomType.Monster)
			{
				pendingScenarioId = ActiveCampaignScenarioId();
			}
			AppendLog($"PORTA SCELTA - slot {index + 1}, difficolta nascosta {campaignDoor.Difficulty}, stanza {DescribeRoomRoll(roomRoll)}");
			AnimationConfiguration animation = configuration.Animation;
			PlayFootstepSfx();
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
		if (ShouldForceFirstRoomMedusa())
		{
			return new CampaignRoomRoll(RoomType.Boss, 4, "mirror", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomTrentor())
		{
			return new CampaignRoomRoll(RoomType.Boss, 4, "climbing", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomBragus())
		{
			return new CampaignRoomRoll(RoomType.Boss, 4, "fog", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomPalatir())
		{
			return new CampaignRoomRoll(RoomType.Boss, 4, "cosmic", RoomDifficulty.Hard);
		}
		if (num == progression.FinalBossRoom || (progression.MinibossEveryRooms > 0 && num % progression.MinibossEveryRooms == 0))
		{
			string bossScenarioId = ActiveCampaignScenarioId();
			if (string.IsNullOrWhiteSpace(bossScenarioId) && num == progression.FinalBossRoom)
			{
				bossScenarioId = "mirror";
			}
			return new CampaignRoomRoll(RoomType.Boss, 4, bossScenarioId, RoomDifficulty.Hard);
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
		if (runProgress != null && runProgress.RoomsCleared == 0 && roomRoll.RoomType == RoomType.Merchant)
		{
			return false;
		}
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
		return new CampaignRoomRoll(RoomType.Monster, num, null, num switch
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
		retryComposableGolemForms = null;
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
		RefreshPlayerHud();
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
		initialCpuFormation.AddRange(BuildCpuFormationForCurrentCombat());
		ResetBattle();
	}

	private CampaignRoomRoll RollCampaignRoomPreview(CampaignDoorDifficulty difficulty)
	{
		bool merchantBlocked = merchantRoomsBlockedUntilMonster;
		bool rewardBlocked = rewardRoomsBlockedUntilMonster;
		CampaignRoomRoll roomRoll = RollCampaignRoom(difficulty);
		merchantRoomsBlockedUntilMonster = merchantBlocked;
		rewardRoomsBlockedUntilMonster = rewardBlocked;
		AppendLog($"DETECTOR - porta {difficulty}: {DescribeRoomRoll(roomRoll)}");
		return roomRoll;
	}

	private void ActivateMinibossForCurrentRoom()
	{
		activeComposableGolem = null;
		activeMedusaBoss = null;
		activeTrentorBoss = null;
		activeBragusBoss = null;
		activePalatirBoss = null;
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
			activeComposableGolem = CreateComposableGolemForCurrentRoom();
			AppendLog("MINIBOSS - Golem Componibile entra nella stanza.");
			break;
		}
	}

	private ComposableGolem CreateComposableGolemForCurrentRoom()
	{
		if (retryComposableGolemForms == null || retryComposableGolemForms.Length == 0)
			return new ComposableGolem(random);

		return new ComposableGolem(
			random,
			ComposableGolem.DefaultHitPoints,
			ComposableGolem.DefaultRoundsPerForm,
			retryComposableGolemForms);
	}

	private static ComposableGolemFormStats[] SnapshotComposableGolemForms(ComposableGolem golem)
	{
		if (golem == null || golem.Forms == null || golem.Forms.Count == 0)
			return null;

		var snapshot = new ComposableGolemFormStats[golem.Forms.Count];
		for (int index = 0; index < golem.Forms.Count; index++)
			snapshot[index] = golem.Forms[index];
		return snapshot;
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

	private CpuEncounterKind CurrentCpuEncounterKind()
	{
		if (activeComposableGolem != null)
		{
			return CpuEncounterKind.ComposableGolem;
		}
		if (activeMedusaBoss != null)
		{
			return CpuEncounterKind.Medusa;
		}
		if (activeTrentorBoss != null)
		{
			return CpuEncounterKind.Trentor;
		}
		if (activeBragusBoss != null)
		{
			return CpuEncounterKind.Bragus;
		}
		if (activePalatirBoss != null)
		{
			return CpuEncounterKind.Palatir;
		}
		return currentRoomType == RoomType.Boss
			?CpuEncounterKind.BossFormation
			:CpuEncounterKind.MonsterFormation;
	}

	private bool UsesBossStyleDeployment()
	{
		CpuEncounterKind kind = CurrentCpuEncounterKind();
		return kind == CpuEncounterKind.BossFormation
			|| kind == CpuEncounterKind.ComposableGolem
			|| kind == CpuEncounterKind.Medusa
			|| kind == CpuEncounterKind.Trentor
			|| kind == CpuEncounterKind.Bragus
			|| kind == CpuEncounterKind.Palatir;
	}

	private List<CardDefinition> BuildCpuFormationForCurrentCombat()
	{
		return CurrentCpuEncounterKind() switch
		{
			CpuEncounterKind.ComposableGolem => BuildComposableGolemFormation(),
			CpuEncounterKind.Medusa => BuildMedusaFormation(),
			CpuEncounterKind.Trentor => BuildTrentorFormation(),
			CpuEncounterKind.Bragus => BuildBragusFormation(),
			CpuEncounterKind.Palatir => BuildPalatirFormation(),
			CpuEncounterKind.BossFormation => DrawStandardBossFormationForCurrentCombat(),
			_ => DrawMonsterFormationForCurrentTier(),
		};
	}

	private List<CardDefinition> BuildComposableGolemFormation()
	{
		CardDefinition golemProxy = FindCardDefinition(ComposableGolemCardId);
		if ((Object)(object)golemProxy != (Object)null)
		{
			return new List<CardDefinition> { golemProxy };
		}
		AppendLog("MINIBOSS - carta proxy Golem Componibile non trovata; disattivo il Golem e uso fallback Boss.");
		activeComposableGolem = null;
		return DrawStandardBossFormationForCurrentCombat();
	}

	private List<CardDefinition> BuildMedusaFormation()
	{
		CardDefinition medusa = FindCardDefinition(MedusaBossCardId);
		if ((Object)(object)medusa != (Object)null)
		{
			activeMedusaBoss ??= new MedusaBoss(random);
			return new List<CardDefinition> { medusa };
		}
		AppendLog("BOSS MEDUSA - carta boss-medusa non trovata; uso fallback Boss.");
		activeMedusaBoss = null;
		return DrawStandardBossFormationForCurrentCombat();
	}

	private List<CardDefinition> BuildTrentorFormation()
	{
		CardDefinition trentor = FindCardDefinition(TrentorBossCardId);
		if ((Object)(object)trentor != (Object)null)
		{
			activeTrentorBoss ??= new TrentorBoss(random);
			return new List<CardDefinition> { trentor };
		}
		AppendLog("BOSS TRENTOR - carta trentor non trovata; uso fallback Boss.");
		activeTrentorBoss = null;
		return DrawStandardBossFormationForCurrentCombat();
	}

	private List<CardDefinition> BuildBragusFormation()
	{
		CardDefinition bragus = FindCardDefinition(BragusBossCardId);
		if ((Object)(object)bragus != (Object)null)
		{
			activeBragusBoss ??= new BragusBoss(random);
			return new List<CardDefinition> { bragus };
		}
		AppendLog("BOSS BRAGUS - carta boss-bragus non trovata; uso fallback Boss.");
		activeBragusBoss = null;
		return DrawStandardBossFormationForCurrentCombat();
	}

	private List<CardDefinition> BuildPalatirFormation()
	{
		CardDefinition palatir = FindCardDefinition(PalatirBossCardId);
		if ((Object)(object)palatir != (Object)null)
		{
			activePalatirBoss ??= new PalatirBoss(random);
			return new List<CardDefinition> { palatir };
		}
		AppendLog("BOSS PALATIR - carta boss-palatir non trovata; uso fallback Boss.");
		activePalatirBoss = null;
		return DrawStandardBossFormationForCurrentCombat();
	}

	private List<CardDefinition> DrawStandardBossFormationForCurrentCombat()
	{
		if (!string.IsNullOrWhiteSpace(campaignScenarioBossId))
		{
			CardDefinition scenarioBoss = FindCardDefinition(campaignScenarioBossId);
			if ((Object)(object)scenarioBoss != (Object)null)
			{
				AppendLog($"BOSS SCENARIO - {scenarioBoss.DisplayName} emerge da {ActiveCampaignScenarioLabel()}.");
				if (string.Equals(scenarioBoss.Id, TrentorBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activeTrentorBoss = new TrentorBoss(random);
					return BuildTrentorFormation();
				}
				if (string.Equals(scenarioBoss.Id, BragusBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activeBragusBoss = new BragusBoss(random);
					return BuildBragusFormation();
				}
				if (string.Equals(scenarioBoss.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activePalatirBoss = new PalatirBoss(random);
					return BuildPalatirFormation();
				}
				return new List<CardDefinition> { scenarioBoss };
			}
			AppendLog($"BOSS SCENARIO - carta '{campaignScenarioBossId}' non trovata; uso fallback Boss.");
		}
		if (runProgress != null && runProgress.RoomsCleared + 1 == configuration.Progression.FinalBossRoom)
		{
			activeMedusaBoss = new MedusaBoss(random);
			return BuildMedusaFormation();
		}
		if (ShouldForceFirstRoomMedusa())
		{
			activeMedusaBoss = new MedusaBoss(random);
			return BuildMedusaFormation();
		}
		if (ShouldForceFirstRoomTrentor())
		{
			activeTrentorBoss = new TrentorBoss(random);
			return BuildTrentorFormation();
		}
		if (ShouldForceFirstRoomBragus())
		{
			activeBragusBoss = new BragusBoss(random);
			return BuildBragusFormation();
		}
		if (ShouldForceFirstRoomPalatir())
		{
			activePalatirBoss = new PalatirBoss(random);
			return BuildPalatirFormation();
		}
		List<CardDefinition> result = formationDraftService.DrawBossCandidates(cardDatabase.Cards, configuration.Progression.BossFormationSize);
		if (result.All((CardDefinition card) => card.Category != CardCategory.Boss))
		{
			AppendLog("BOSS FALLBACK - nessuna carta Boss disponibile; usato un Mostro come sostituto.");
		}
		return result;
	}

	private CardDefinition FindCardDefinition(string id)
	{
		if (cardDatabase == null || string.IsNullOrWhiteSpace(id))
		{
			return null;
		}
		return cardDatabase.Cards.FirstOrDefault((CardDefinition card) => (Object)(object)card != (Object)null && string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private string ActiveCampaignScenarioId()
	{
		return string.IsNullOrWhiteSpace(campaignScenarioId) ? null : campaignScenarioId;
	}

	private string ActiveCampaignScenarioLabel()
	{
		if ((Object)(object)scenarioCatalog == (Object)null)
		{
			scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		}
		ScenarioDefinition scenario = (Object)(object)scenarioCatalog != (Object)null
			?scenarioCatalog.FindById(campaignScenarioId)
			:null;
		if ((Object)(object)scenario != (Object)null && !string.IsNullOrWhiteSpace(scenario.DisplayName))
		{
			return scenario.DisplayName;
		}
		return string.IsNullOrWhiteSpace(campaignScenarioId) ? "scenario ignoto" : campaignScenarioId;
	}

	private bool IsComposableGolemProxy(BattleCardState card)
	{
		return activeComposableGolem != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, ComposableGolemCardId, StringComparison.OrdinalIgnoreCase);
	}

	private bool ShouldForceFirstRoomMedusa()
	{
		return debugForceFirstRoomMedusa && runProgress != null && runProgress.RoomsCleared == 0;
	}

	private bool ShouldForceFirstRoomTrentor()
	{
		return debugForceFirstRoomTrentor && runProgress != null && runProgress.RoomsCleared == 0;
	}

	private bool ShouldForceFirstRoomBragus()
	{
		return debugForceFirstRoomBragus && runProgress != null && runProgress.RoomsCleared == 0;
	}

	private bool ShouldForceFirstRoomPalatir()
	{
		return debugForceFirstRoomPalatir && runProgress != null && runProgress.RoomsCleared == 0;
	}

	private bool IsMedusaBossProxy(BattleCardState card)
	{
		return activeMedusaBoss != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, MedusaBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private bool IsTrentorBossProxy(BattleCardState card)
	{
		return activeTrentorBoss != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, TrentorBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private bool IsBragusBossProxy(BattleCardState card)
	{
		return activeBragusBoss != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, BragusBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private bool IsPalatirBossProxy(BattleCardState card)
	{
		return activePalatirBoss != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase);
	}
}
}
