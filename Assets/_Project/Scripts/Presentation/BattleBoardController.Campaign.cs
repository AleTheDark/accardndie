using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
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
	private Coroutine scenarioBackgroundTransitionRoutine;
	private Coroutine bossEntranceShakeRoutine;
	private Image bossTransitionBlackout;
	private Vector2 bossShakeOriginalPosition;
	private Vector3 bossShakeOriginalScale;
	private bool bossShakeTransformCaptured;
	private bool seraphelBossPresentationActive;
	private bool jurinashorBossPresentationActive;

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
		if (scenarioBackgroundTransitionRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(scenarioBackgroundTransitionRoutine);
			scenarioBackgroundTransitionRoutine = null;
			DestroyBossTransitionBlackout();
			SetScenarioBackgroundAlpha(1f);
		}
		bragusBossPresentationActive = false;
		trentorBossPresentationActive = false;
		seraphelBossPresentationActive = false;
		jurinashorBossPresentationActive = false;
		seraphelEntranceVfxPlayed = false;
		ResetBossPresentationUi();
		currentScenario = scenario;
		currentScenarioDisplayOverride = null;
		RefreshScenarioBackground();
		if ((Object)(object)cpuTitleText != (Object)null)
		{
			cpuTitleText.text = GameText.Format(GameTextKeys.Combat.CpuMasterScenario, scenario.DisplayName.ToUpperInvariant());
		}
		AppendLog("SCENARIO - " + scenario.DisplayName + " [" + scenario.Id + "]");
		return true;
	}

	private Sprite CurrentScenarioBackgroundSprite()
	{
		// I tutorial usano sempre il campo base. Il controller e' persistente e puo'
		// conservare il capitolo precedente (per esempio cosmic): quel fallback non
		// deve avere precedenza sul fondale didattico.
		if (adventureScriptedTutorialActive)
		{
			ScenarioDefinition tutorialScenario = (Object)(object)scenarioCatalog != (Object)null
				? scenarioCatalog.FindById("default")
				: null;
			if ((Object)(object)tutorialScenario != (Object)null)
			{
				if (Screen.width > Screen.height && (Object)(object)tutorialScenario.BackgroundLandscape != (Object)null)
					return tutorialScenario.BackgroundLandscape;
				if ((Object)(object)tutorialScenario.Background != (Object)null)
					return tutorialScenario.Background;
			}
			Sprite defaultBackground = Resources.Load<Sprite>("Backgrounds/bg_default");
			if ((Object)(object)defaultBackground != (Object)null)
				return defaultBackground;
			return Resources.Load<Sprite>("Backgrounds/Background_terrain");
		}

		// Il capitolo attivo resta valorizzato anche nelle stanze intermedie. Il
		// Mercato deve quindi usare il proprio ScenarioDefinition prima dei fallback
		// che anticipano lo sfondo del boss del capitolo.
		if (currentRoomType == RoomType.Merchant && (Object)(object)currentScenario != (Object)null)
		{
			if (Screen.width > Screen.height && (Object)(object)currentScenario.BackgroundLandscape != (Object)null)
				return currentScenario.BackgroundLandscape;
			if ((Object)(object)currentScenario.Background != (Object)null)
				return currentScenario.Background;
		}

		bool jurinashorScenarioSelected = debugForceFirstRoomJurinashor
			|| string.Equals(campaignScenarioBossId, JurinashorBossCardId, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(activeAdventureChapterId, "chapter-3", StringComparison.OrdinalIgnoreCase);
		if (jurinashorBossPresentationActive)
		{
			string backgroundPath = activeJurinashorBoss != null && activeJurinashorBoss.IsPhaseTwo
				? "Backgrounds/bg_jurinashor_phase_2"
				: "Backgrounds/bg_jurinashor_phase_1";
			Sprite phaseBackground = Resources.Load<Sprite>(backgroundPath);
			if ((Object)(object)phaseBackground != (Object)null)
				return phaseBackground;
		}
		if (jurinashorScenarioSelected && (Object)(object)scenarioCatalog != (Object)null)
		{
			ScenarioDefinition infestedScenario = scenarioCatalog.FindById("infested");
			if ((Object)(object)infestedScenario != (Object)null)
			{
				if (Screen.width > Screen.height && (Object)(object)infestedScenario.BackgroundLandscape != (Object)null)
					return infestedScenario.BackgroundLandscape;
				if ((Object)(object)infestedScenario.Background != (Object)null)
					return infestedScenario.Background;
			}
		}

		if (seraphelBossPresentationActive)
		{
			string phaseBackground = activeSeraphelBoss != null && activeSeraphelBoss.IsPhaseTwo
				? "Backgrounds/bg_seraphel_phase_2"
				: "Backgrounds/bg_seraphel_phase_1";
			Sprite seraphelBackground = Resources.Load<Sprite>(phaseBackground);
			if ((Object)(object)seraphelBackground != (Object)null)
				return seraphelBackground;
		}

		// La scena debug di Seraphel deve nascere direttamente su Lux. In particolare,
		// non deve mostrare per un frame lo scenario conservato dal controller persistente
		// del boss aperto in precedenza, prima che inizi il reveal di Seraphel.
		bool seraphelScenarioSelected = debugForceFirstRoomSeraphel
			|| string.Equals(campaignScenarioBossId, SeraphelBossCardId, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(activeAdventureChapterId, "chapter-4", StringComparison.OrdinalIgnoreCase);
		if (seraphelScenarioSelected)
		{
			Sprite luxBackground = Resources.Load<Sprite>("Backgrounds/bg_lux");
			if ((Object)(object)luxBackground != (Object)null)
				return luxBackground;
		}

		// Lo sfondo con Trentor incorporato e' una risorsa esclusiva del reveal.
		// Non passa dal catalogo scenari, cosi' la stanza parte sempre da
		// "climbing" e non puo' cadere sul background di un altro boss.
		bool trentorScenarioSelected = debugForceFirstRoomTrentor
			|| string.Equals(campaignScenarioBossId, TrentorBossCardId, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(activeAdventureChapterId, "chapter-1", StringComparison.OrdinalIgnoreCase);
		bool trentorRevealed = trentorBossPresentationActive
			|| (trentorScenarioSelected
				&& cpuCards != null
				&& cpuCards.Any(card => card != null && IsTrentorBossProxy(card)));
		if (trentorRevealed)
		{
			Sprite trentorBackground = Resources.Load<Sprite>("Backgrounds/bg_trentor");
			if ((Object)(object)trentorBackground != (Object)null)
				return trentorBackground;
		}

		// Prima del reveal Trentor deve gia' mostrare Rampicanti. Il controller e'
		// persistente e puo' conservare per alcuni frame lo scenario del boss aperto
		// in precedenza (per esempio fog di Bragus), quindi qui la selezione debug e'
		// l'autorita' sul fondale di preparazione.
		if (trentorScenarioSelected && (Object)(object)scenarioCatalog != (Object)null)
		{
			ScenarioDefinition climbingScenario = scenarioCatalog.Select(
				RoomType.Boss,
				RoomDifficulty.Hard,
				TrentorBossCardId,
				"climbing");
			if ((Object)(object)climbingScenario != (Object)null)
			{
				if (Screen.width > Screen.height && (Object)(object)climbingScenario.BackgroundLandscape != (Object)null)
					return climbingScenario.BackgroundLandscape;
				if ((Object)(object)climbingScenario.Background != (Object)null)
					return climbingScenario.Background;
			}
		}

		// Il fondale con Bragus incorporato appartiene al reveal, come quelli di
		// Trentor e Seraphel. Deve avere precedenza sullo scenario di preparazione
		// "fog", altrimenti lo stato di presentazione e' attivo ma bg_bragus non
		// viene mai selezionato durante lo schieramento.
		if (bragusBossPresentationActive && (Object)(object)scenarioCatalog != (Object)null)
		{
			ScenarioDefinition bragusScenario = scenarioCatalog.Select(
				RoomType.Boss,
				RoomDifficulty.Hard,
				BragusBossCardId,
				"bragus");
			if ((Object)(object)bragusScenario != (Object)null)
			{
				if (Screen.width > Screen.height && (Object)(object)bragusScenario.BackgroundLandscape != (Object)null)
					return bragusScenario.BackgroundLandscape;
				if ((Object)(object)bragusScenario.Background != (Object)null)
					return bragusScenario.Background;
			}
		}

		// La scena BossDebug e il controller persistente non devono affidarsi a
		// currentScenario, che puo' appartenere al test precedente. Il capitolo/boss
		// selezionato decide sempre il fondale di preparazione.
		string selectedBossScenarioId = SelectedBossScenarioId();
		if (!string.IsNullOrWhiteSpace(selectedBossScenarioId)
			&& (Object)(object)scenarioCatalog != (Object)null)
		{
			ScenarioDefinition selectedBossScenario = scenarioCatalog.FindById(selectedBossScenarioId);
			if ((Object)(object)selectedBossScenario != (Object)null)
			{
				if (Screen.width > Screen.height && (Object)(object)selectedBossScenario.BackgroundLandscape != (Object)null)
					return selectedBossScenario.BackgroundLandscape;
				if ((Object)(object)selectedBossScenario.Background != (Object)null)
					return selectedBossScenario.Background;
			}
		}

		if ((Object)(object)currentScenario == (Object)null)
		{
			return Resources.Load<Sprite>("Backgrounds/Background_terrain");
		}
		if (string.Equals(currentScenario.Id, "lux", StringComparison.OrdinalIgnoreCase))
		{
			Sprite luxBackground = Resources.Load<Sprite>("Backgrounds/bg_lux");
			if ((Object)(object)luxBackground != (Object)null)
				return luxBackground;
		}
		if (Screen.width > Screen.height && (Object)(object)currentScenario.BackgroundLandscape != (Object)null)
		{
			return currentScenario.BackgroundLandscape;
		}
		return currentScenario.Background;
	}

	private string SelectedBossScenarioId()
	{
		if (string.Equals(activeAdventureChapterId, "chapter-1", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(campaignScenarioBossId, TrentorBossCardId, StringComparison.OrdinalIgnoreCase)
			|| debugForceFirstRoomTrentor)
			return "climbing";
		if (string.Equals(activeAdventureChapterId, "chapter-2", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(campaignScenarioBossId, BragusBossCardId, StringComparison.OrdinalIgnoreCase)
			|| debugForceFirstRoomBragus)
			return "fog";
		if (string.Equals(activeAdventureChapterId, "chapter-3", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(campaignScenarioBossId, JurinashorBossCardId, StringComparison.OrdinalIgnoreCase)
			|| debugForceFirstRoomJurinashor)
			return "infested";
		if (string.Equals(activeAdventureChapterId, "chapter-4", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(campaignScenarioBossId, SeraphelBossCardId, StringComparison.OrdinalIgnoreCase)
			|| debugForceFirstRoomSeraphel)
			return "lux";
		if (debugForceFirstRoomMedusa)
			return "default";
		if (string.Equals(activeAdventureChapterId, "chapter-7", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(campaignScenarioBossId, PalatirBossCardId, StringComparison.OrdinalIgnoreCase)
			|| debugForceFirstRoomPalatir)
			return "cosmic";
		return null;
	}

	private void RefreshScenarioBackground()
	{
		if (pvpPresentationActive && pvpState != null)
		{
			RefreshPvpArenaBackground(pvpState.MatchRound);
			return;
		}

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

	private void TransitionToScenarioBackground()
	{
		if (scenarioBackgroundTransitionRoutine != null)
			((MonoBehaviour)this).StopCoroutine(scenarioBackgroundTransitionRoutine);
		DestroyBossTransitionBlackout();
		scenarioBackgroundTransitionRoutine = ((MonoBehaviour)this).StartCoroutine(
			TransitionToScenarioBackgroundRoutine());
	}

	private IEnumerator TransitionToScenarioBackgroundRoutine()
	{
		const float fadeOutDuration = 0.18f;
		const float fadeInDuration = 0.32f;
		Image blackout = CreateBossTransitionBlackout();
		bossTransitionBlackout = blackout;
		float elapsed = 0f;
		while (elapsed < fadeOutDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			SetImageAlpha(blackout, Mathf.SmoothStep(0f, 1f, elapsed / fadeOutDuration));
			yield return null;
		}

		SetImageAlpha(blackout, 1f);
		RefreshScenarioBackground();
		SetScenarioBackgroundAlpha(1f);
		TriggerBossEntranceImpact();

		elapsed = 0f;
		while (elapsed < fadeInDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			SetImageAlpha(blackout, 1f - Mathf.SmoothStep(0f, 1f, elapsed / fadeInDuration));
			yield return null;
		}

		DestroyBossTransitionBlackout();
		scenarioBackgroundTransitionRoutine = null;
	}

	private void DestroyBossTransitionBlackout()
	{
		if ((Object)(object)bossTransitionBlackout != (Object)null)
			Object.Destroy(((Component)bossTransitionBlackout).gameObject);
		bossTransitionBlackout = null;
	}

	private Image CreateBossTransitionBlackout()
	{
		if ((Object)(object)safeAreaRoot == (Object)null)
			return null;

		GameObject blackoutObject = new GameObject("Boss Transition Blackout",
			typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		RectTransform rect = blackoutObject.GetComponent<RectTransform>();
		rect.SetParent((Transform)(object)safeAreaRoot, false);
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(-24f, -24f);
		rect.offsetMax = new Vector2(24f, 24f);
		rect.SetAsLastSibling();

		Image image = blackoutObject.GetComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0f);
		image.raycastTarget = false;
		return image;
	}

	private static void SetImageAlpha(Image image, float alpha)
	{
		if ((Object)(object)image == (Object)null)
			return;
		Color color = image.color;
		color.a = Mathf.Clamp01(alpha);
		image.color = color;
	}

	private void TriggerBossEntranceImpact()
	{
		if (bossEntranceShakeRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(bossEntranceShakeRoutine);
			RestoreBossShakeTransform();
		}
		bossEntranceShakeRoutine = ((MonoBehaviour)this).StartCoroutine(PlayBossEntranceShake());

#if UNITY_ANDROID || UNITY_IOS
		Handheld.Vibrate();
#endif
	}

	private IEnumerator PlayBossEntranceShake()
	{
		if ((Object)(object)safeAreaRoot == (Object)null)
		{
			bossEntranceShakeRoutine = null;
			yield break;
		}

		const float duration = 0.42f;
		const float maximumOffset = 13f;
		RectTransform root = safeAreaRoot;
		bossShakeOriginalPosition = root.anchoredPosition;
		bossShakeOriginalScale = ((Transform)root).localScale;
		bossShakeTransformCaptured = true;
		((Transform)root).localScale = bossShakeOriginalScale * 1.025f;

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float strength = 1f - Mathf.Clamp01(elapsed / duration);
			root.anchoredPosition = bossShakeOriginalPosition + UnityEngine.Random.insideUnitCircle * (maximumOffset * strength);
			yield return null;
		}

		RestoreBossShakeTransform();
		bossEntranceShakeRoutine = null;
	}

	private void RestoreBossShakeTransform()
	{
		if (!bossShakeTransformCaptured || (Object)(object)safeAreaRoot == (Object)null)
			return;
		safeAreaRoot.anchoredPosition = bossShakeOriginalPosition;
		((Transform)safeAreaRoot).localScale = bossShakeOriginalScale;
		bossShakeTransformCaptured = false;
	}

	private void SetScenarioBackgroundAlpha(float alpha)
	{
		if ((Object)(object)backgroundFillImage != (Object)null)
		{
			Color color = backgroundFillImage.color;
			color.a = alpha;
			backgroundFillImage.color = color;
		}
		if ((Object)(object)terrainImage != (Object)null)
		{
			Color color = terrainImage.color;
			color.a = alpha;
			terrainImage.color = color;
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
			cpuTitleText.text = GameText.Format(GameTextKeys.Combat.CpuMasterScenario, displayOverride.ToUpperInvariant());
		}
		return true;
	}

	private bool LoadCampaignRoomScenario()
	{
		// La scena debug Trentor non deve ereditare pendingScenarioId/campaignScenarioId
		// da una sessione persistente (per esempio "fog" dopo Bragus).
		if (currentRoomType == RoomType.Boss
			&& (debugForceFirstRoomTrentor
				|| string.Equals(campaignScenarioBossId, TrentorBossCardId, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(activeAdventureChapterId, "chapter-1", StringComparison.OrdinalIgnoreCase)))
			return LoadScenario(
				RoomType.Boss,
				RoomDifficulty.Hard,
				TrentorBossCardId,
				"climbing");
		if (currentRoomType == RoomType.Boss
			&& (debugForceFirstRoomSeraphel
				|| string.Equals(campaignScenarioBossId, SeraphelBossCardId, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(activeAdventureChapterId, "chapter-4", StringComparison.OrdinalIgnoreCase)))
			return LoadScenario(RoomType.Boss, RoomDifficulty.Hard, SeraphelBossCardId, "lux");
		if (currentRoomType == RoomType.Boss
			&& (debugForceFirstRoomJurinashor
				|| string.Equals(campaignScenarioBossId, JurinashorBossCardId, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(activeAdventureChapterId, "chapter-3", StringComparison.OrdinalIgnoreCase)))
			return LoadScenario(RoomType.Boss, RoomDifficulty.Hard, JurinashorBossCardId, "infested");

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
		ClearBoardForRoomTransition();
		roomChoiceBackgroundIndex = random != null ? random.NextInclusive(1, 5) : UnityEngine.Random.Range(1, 6);
		PrepareCampaignDoors();
		// Punto di salvataggio autorevole: lo stato tra le stanze è coerente qui. Si scrive
		// dopo aver estratto le porte, non prima: sono già state decise, e un salvataggio
		// preso prima le farebbe riestrarre alla ripresa.
		SaveCurrentRun();
		ShowRoomChoicePanel();
	}

	/// <summary>
	/// Sgombra il campo fra una stanza e l'altra. È lo stato di partenza sia della scelta
	/// della via sia di una stanza ripresa da un salvataggio: in mezzo non deve restare
	/// niente della stanza precedente.
	/// </summary>
	private void ClearBoardForRoomTransition()
	{
		ClearManaDeltaCallouts();
		ClearEnemyManaDeltaCallouts();
		((MonoBehaviour)this).StopAllCoroutines();
		ClearDraftEntranceState();
		StopMusic();
		SetBattlefieldSurfaceVisible(visible: true);
		inputLocked = true;
		gameFinished = true;
		canAdvanceToNextRoom = false;
		campaignRoomEntered = false;
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Normal;
		currentScenarioDisplayOverride = null;
		activeComposableGolem = null;
		activeMedusaBoss = null;
		activeTrentorBoss = null;
		activeJurinashorBoss = null;
		activeBragusBoss = null;
		activePalatirBoss = null;
		activeSeraphelBoss = null;
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
	}

	/// <summary>
	/// Mostra la schermata delle tre porte. Le porte sono già decise da chi chiama: qui non
	/// si estrae più niente, così la stessa schermata la può riaprire anche una run ripresa
	/// con le porte che aveva davanti prima di chiudere il gioco.
	/// </summary>
	private void ShowRoomChoicePanel()
	{
		RefreshRoomChoiceCounter();
		RefreshRoomChoiceLayout();
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(true);
		}
		SetTurnBanner(playerTurn: true, "SCELTA DELLA VIA");
		SetMessage("Scegli una delle tre porte per proseguire nella campagna.");
		RefreshInitiativeDisplay();
		ApplyResponsiveLayout();
		if (ShouldForceMerchantDebugRoom() || ShouldForceFirstRoomComposableGolem() || ShouldForceFirstRoomMedusa() || ShouldForceFirstRoomTrentor() || ShouldForceFirstRoomBragus() || ShouldForceFirstRoomPalatir())
		{
			((MonoBehaviour)this).StartCoroutine(ChooseDebugMinibossDoor());
		}
	}

	private void RefreshRoomChoiceCounter()
	{
		if ((Object)(object)roomChoiceCounterText == (Object)null)
		{
			return;
		}

		int roomNumber = runProgress != null ? runProgress.RoomsCleared + 1 : 1;
		roomChoiceCounterText.text = GameText.Format(GameTextKeys.Combat.CpuHudRoom, roomNumber);
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
		for (int index = 0; index < 3; index++)
		{
			campaignDoors.Add(nextDoorChoiceRevealed
				? new CampaignDoor(RollCampaignRoomPreview())
				: new CampaignDoor());
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
				campaignDoors[i] = new CampaignDoor(RollCampaignRoomPreview());
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
			CampaignRoomRoll roomRoll = campaignDoor.RevealedRoom ?? RollCampaignRoom();
			if (campaignDoor.RevealedRoom.HasValue)
			{
				RegisterCampaignRoomRoll(roomRoll);
			}
			currentRoomType = roomRoll.RoomType;
			pendingScenarioId = roomRoll.ScenarioId;
			pendingRoomDifficulty = currentRoomType == RoomType.Monster
				? ApplyNextMonsterDifficultyIncrease(RollMonsterRoomDifficulty(runProgress.RoomsCleared + 1))
				: roomRoll.Difficulty;
			if (currentRoomType == RoomType.Monster)
			{
				pendingScenarioId = ActiveCampaignScenarioId();
			}
			AppendLog($"PORTA SCELTA - slot {index + 1}, stanza nascosta {DescribeRoomRoll(roomRoll)}");
			// La porta è varcata: da adesso il salvataggio dice "sono in questa stanza", non
			// più "sto scegliendo". Si scrive prima della dissolvenza, perché una stanza
			// aperta non deve poter tornare una scelta da rifare chiudendo il gioco.
			MarkCampaignRoomEntered();
			SaveCurrentRun();
			AnimationConfiguration animation = configuration.Animation;
			PlayFootstepSfx();
			roomTransition.Play(EnterChosenCampaignRoom, animation.RoomFadeOutDuration, animation.RoomBlackHoldDuration, animation.RoomFadeInDuration);
		}
	}

	private CampaignRoomRoll RollCampaignRoom()
	{
		int num = runProgress.RoomsCleared + 1;
		ProgressionConfiguration progression = configuration.Progression;
		if (ShouldForceMerchantDebugRoom())
		{
			return new CampaignRoomRoll(RoomType.Merchant, "god_merchant", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomComposableGolem())
		{
			return new CampaignRoomRoll(RoomType.Boss, null, RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomMedusa())
		{
			return new CampaignRoomRoll(RoomType.Boss, "default", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomTrentor())
		{
			return new CampaignRoomRoll(RoomType.Boss, "climbing", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomBragus())
		{
			return new CampaignRoomRoll(RoomType.Boss, "fog", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomPalatir())
		{
			return new CampaignRoomRoll(RoomType.Boss, "cosmic", RoomDifficulty.Hard);
		}
		if (ShouldForceFirstRoomSeraphel())
		{
			return new CampaignRoomRoll(RoomType.Boss, "lux", RoomDifficulty.Hard);
		}
		if (num == progression.FinalBossRoom || (progression.MinibossEveryRooms > 0 && num % progression.MinibossEveryRooms == 0))
		{
			string bossScenarioId = ActiveCampaignScenarioId();
			if (string.IsNullOrWhiteSpace(bossScenarioId) && num == progression.FinalBossRoom)
			{
				bossScenarioId = "mirror";
			}
			return new CampaignRoomRoll(RoomType.Boss, bossScenarioId, RoomDifficulty.Hard);
		}
		return RollHiddenDoorRoom();
	}

	private CampaignRoomRoll RollHiddenDoorRoom()
	{
		// Denominatore 72: conserva la distribuzione complessiva precedente delle tre
		// vecchie porte, ma ora ogni porta nascosta usa la stessa estrazione di stanza.
		return RollAllowedDoorRoom(72, (int roll) => roll switch
		{
			<= 52 => MonsterRoomRoll(),
			<= 66 => new CampaignRoomRoll(RoomType.Merchant, "god_merchant", RoomDifficulty.Hard),
			<= 69 => new CampaignRoomRoll(RoomType.Loot, "loot", RoomDifficulty.Any),
			_ => new CampaignRoomRoll(RoomType.QuickChallenge, "quick_challenge", RoomDifficulty.Any),
		});
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
		return RegisterCampaignRoomRoll(MonsterRoomRoll());
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
		if (rewardRoomsBlockedUntilMonster && (roomRoll.RoomType == RoomType.Loot || roomRoll.RoomType == RoomType.QuickChallenge))
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
		else if (roomRoll.RoomType == RoomType.Loot || roomRoll.RoomType == RoomType.QuickChallenge)
		{
			rewardRoomsBlockedUntilMonster = true;
		}
		return roomRoll;
	}

	private static CampaignRoomRoll MonsterRoomRoll()
	{
		return new CampaignRoomRoll(RoomType.Monster, null, RoomDifficulty.Any);
	}

	private RoomDifficulty ApplyNextMonsterDifficultyIncrease(RoomDifficulty difficulty)
	{
		int increase = nextMonsterDifficultyIncrease;
		nextMonsterDifficultyIncrease = 0;
		if (increase <= 0)
		{
			return difficulty;
		}

		RoomDifficulty increased = (RoomDifficulty)Mathf.Clamp((int)difficulty + increase, (int)RoomDifficulty.Easy, (int)RoomDifficulty.Hard);
		AppendLog($"PRESAGIO - difficolta mostro aumentata a {RoomDifficultyRules.For(increased).DisplayName}.");
		return increased;
	}

	private RoomDifficulty RollMonsterRoomDifficulty(int roomNumber)
	{
		int scenarioNumber = ActiveCampaignScenarioNumber();
		ScenarioMonsterDifficultyWeights weights = ScenarioMonsterDifficultyWeights.For(scenarioNumber, roomNumber);
		int roll = random.NextInclusive(1, Mathf.Max(1, weights.Total));
		RoomDifficulty result = roll <= weights.Accessible
			? RoomDifficulty.Easy
			: roll <= weights.Accessible + weights.Normal
				? RoomDifficulty.Normal
				: RoomDifficulty.Hard;
		AppendLog($"DIFFICOLTA MOSTRO - scenario {scenarioNumber}, stanza {roomNumber}, " +
			$"pesi {weights.Accessible}/{weights.Normal}/{weights.Diabolic}, estratta {RoomDifficultyRules.For(result).DisplayName}.");
		return result;
	}

	private int ActiveCampaignScenarioNumber()
	{
		if (!string.IsNullOrWhiteSpace(activeAdventureChapterId))
		{
			const string prefix = "chapter-";
			if (activeAdventureChapterId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
				&& int.TryParse(activeAdventureChapterId.Substring(prefix.Length), out int chapterNumber))
				return Mathf.Clamp(chapterNumber, 1, 9);
		}

		string id = campaignScenarioId ?? string.Empty;
		switch (id.ToLowerInvariant())
		{
			case "fog": return 1;
			case "climbing": return 2;
			case "mirror": return 3;
			case "cosmic": return 4;
		}

		for (int scenario = 1; scenario <= 9; scenario++)
		{
			if (string.Equals(id, $"scenario-{scenario}", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(id, $"scenario_{scenario}", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(id, scenario.ToString(), StringComparison.OrdinalIgnoreCase))
				return scenario;
		}
		return 1;
	}

	private static string DescribeRoomRoll(CampaignRoomRoll roomRoll)
	{
		if (roomRoll.RoomType != RoomType.Monster)
		{
			return roomRoll.RoomType.ToString();
		}
		return "Mostro";
	}

	private void EnterChosenCampaignRoom()
	{
		ClearLootRewardReveal();
		retryComposableGolemForms = null;
		retryComposableGolemHitPoints = null;
		retrySeraphelHitPoints = null;
		retryJurinashorHitPoints = null;
		retryJurinashorPhaseTwo = false;
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(false);
		}
		((Component)merchantBuyButton).gameObject.SetActive(false);
		ConfigureActionButtonLayout(merchantVisible: false);
		AppendLog("STANZA ESTRATTA - " + DescribeRoomRoll(new CampaignRoomRoll(currentRoomType, pendingScenarioId, pendingRoomDifficulty)));
		if (!LoadCampaignRoomScenario())
		{
			currentScenarioDisplayOverride = DescribeRoomRoll(new CampaignRoomRoll(currentRoomType, pendingScenarioId, pendingRoomDifficulty));
			AppendLog("SCENARIO - fallback nome stanza: scenario non trovato o non valido.");
		}
		ActivateMinibossForCurrentRoom();
		RefreshPlayerHud();
		PlayCurrentRoomEnterSfx();
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

	private CampaignRoomRoll RollCampaignRoomPreview()
	{
		bool merchantBlocked = merchantRoomsBlockedUntilMonster;
		bool rewardBlocked = rewardRoomsBlockedUntilMonster;
		CampaignRoomRoll roomRoll = RollCampaignRoom();
		merchantRoomsBlockedUntilMonster = merchantBlocked;
		rewardRoomsBlockedUntilMonster = rewardBlocked;
		AppendLog($"DETECTOR - stanza rivelata: {DescribeRoomRoll(roomRoll)}");
		return roomRoll;
	}

	private void ActivateMinibossForCurrentRoom()
	{
		activeComposableGolem = null;
		activeMedusaBoss = null;
		activeTrentorBoss = null;
		activeJurinashorBoss = null;
		activeBragusBoss = null;
		activePalatirBoss = null;
		if (currentRoomType != RoomType.Boss || !IsCurrentRoomMinibossRoom())
		{
			return;
		}
		MinibossKind miniboss = RollMinibossKind();
		switch (miniboss)
		{
		case MinibossKind.ComposableGolem:
			if ((Object)(object)FindCardDefinition(ComposableGolemCardId) == (Object)null)
			{
				AppendLog("MINIBOSS - carta proxy Golem Componibile assente dal CardDatabase; miniboss non attivato.");
				break;
			}
			activeComposableGolem = CreateComposableGolemForCurrentRoom();
			AppendLog("MINIBOSS - Golem Componibile entra nella stanza.");
			break;
		case MinibossKind.Medusa:
			if ((Object)(object)FindCardDefinition(MedusaBossCardId) == (Object)null)
			{
				AppendLog("MINIBOSS - carta proxy Medusa assente dal CardDatabase; miniboss non attivato.");
				break;
			}
			activeMedusaBoss = new MedusaBoss(random);
			AppendLog("MINIBOSS - Medusa entra nella stanza 10.");
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
			retryComposableGolemHitPoints ?? ComposableGolem.DefaultHitPoints,
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
		int roomNumber = runProgress?.RoomsCleared + 1 ?? 0;
		if (roomNumber != 10)
			return MinibossKind.ComposableGolem;

		MinibossKind[] pool =
		{
			MinibossKind.ComposableGolem,
			MinibossKind.Medusa
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
			_ => DrawMonsterFormationForCurrentDifficulty(),
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

	private List<CardDefinition> BuildSeraphelFormation()
	{
		CardDefinition seraphel = FindCardDefinition(SeraphelBossCardId);
		if ((Object)(object)seraphel != (Object)null)
		{
			activeSeraphelBoss ??= CreateSeraphelForCurrentRoom();
			return new List<CardDefinition> { seraphel };
		}
		AppendLog("BOSS SERAPHEL - carta non trovata; uso fallback Boss.");
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
				if (string.Equals(scenarioBoss.Id, JurinashorBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activeJurinashorBoss ??= new JurinashorBoss();
					return new List<CardDefinition> { scenarioBoss };
				}
				if (string.Equals(scenarioBoss.Id, MedusaBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activeMedusaBoss = new MedusaBoss(random);
					return BuildMedusaFormation();
				}
				if (string.Equals(scenarioBoss.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activePalatirBoss = new PalatirBoss(random);
					return BuildPalatirFormation();
				}
				if (string.Equals(scenarioBoss.Id, SeraphelBossCardId, StringComparison.OrdinalIgnoreCase))
				{
					activeSeraphelBoss = CreateSeraphelForCurrentRoom();
					return BuildSeraphelFormation();
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
		if (ShouldForceFirstRoomSeraphel())
		{
			activeSeraphelBoss = CreateSeraphelForCurrentRoom();
			return BuildSeraphelFormation();
		}
		List<CardDefinition> result = formationDraftService.DrawBossCandidates(cardDatabase.Cards, configuration.Progression.BossFormationSize);
		if (result.All((CardDefinition card) => card.Category != CardCategory.Boss))
		{
			AppendLog("BOSS FALLBACK - nessuna carta Boss disponibile; usato un Mostro come sostituto.");
		}
		return result;
	}

	private SeraphelBoss CreateSeraphelForCurrentRoom()
	{
		return retrySeraphelHitPoints.HasValue
			? new SeraphelBoss(random, SeraphelBoss.DefaultHitPoints, retrySeraphelHitPoints.Value)
			: new SeraphelBoss(random);
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

	private bool ShouldForceFirstRoomSeraphel()
	{
		return debugForceFirstRoomSeraphel && runProgress != null && runProgress.RoomsCleared == 0;
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

	private bool IsJurinashorBossProxy(BattleCardState card)
	{
		return activeJurinashorBoss != null && card != null && !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, JurinashorBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private bool IsPalatirBossProxy(BattleCardState card)
	{
		return activePalatirBoss != null
			&& card != null
			&& !card.BelongsToPlayer
			&& string.Equals(card.Definition.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private bool IsSeraphelBossProxy(BattleCardState card)
	{
		return activeSeraphelBoss != null && card != null && !card.BelongsToPlayer
			&& (string.Equals(card.Definition.Id, SeraphelBossCardId, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(card.Definition.Id, SeraphelPhaseTwoCardId, StringComparison.OrdinalIgnoreCase));
	}
}
}
