using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// Le tappe del duello del Guerriero. L'ordine e' la lezione: prima si guarda il campo
	/// (mana e potenza), poi si prova un attacco che vince, poi l'abilita', poi un attacco
	/// che **perde** - serve a far vedere che la potenza da sola non basta - e infine la
	/// tecnica che ribalta lo stesso scontro.
	/// </summary>
	private enum TutorialWarriorDuelStep
	{
		Intro,
		Mana,
		Vigor,
		AttackWeakest,
		AbilityOnMiddle,
		AttackMiddle,
		AttackStrongest,
		ImpossibleExplained,
		SupremeExplained,
		AttackStrongestAgain,
		Done
	}

	private const string TutorialDuelPlayerCardId = "6-chimera-warrior";
	private const string TutorialDuelWeakEnemyId = "4-animal-warrior";
	private const string TutorialDuelMiddleEnemyId = "7-whitealien-warrior";
	private const string TutorialDuelStrongEnemyId = "10-champion-warrior";

	private bool tutorialWarriorDuelActive;

	private TutorialWarriorDuelStep tutorialWarriorDuelStep;
	private bool tutorialWarriorDuelActionUnlocked;

	/// <summary>
	/// Quante volte il Guerriero ha gia' attaccato il nemico piu' forte. Il primo attacco
	/// deve fallire e il secondo, dopo la tecnica, deve vincere: e' l'unico modo di far
	/// vedere cosa cambia, invece di dirlo.
	/// </summary>
	private int tutorialDuelStrongestAttacks;
	private readonly List<Outline> tutorialWarriorHudHighlights = new List<Outline>();
	private Image tutorialWarriorFocusOverlay;
	private Material tutorialWarriorFocusMaterial;
	private Coroutine tutorialWarriorFocusRoutine;
	private Coroutine tutorialWarriorPawnEntranceRoutine;

	// I gate del combattimento sono condivisi dalle lezioni pratiche delle classi. Il nome
	// storico resta per non toccare decine di call site, ma comprende anche il duello Mago.
	private bool IsTutorialWarriorDuelActive => tutorialWarriorDuelActive || tutorialMageDuelActive || tutorialRoguePracticeActive;
	private string ActiveTutorialDuelRoomLabel => tutorialMageDuelActive
		? TutorialClassText("warrior_room_mage")
		: tutorialRoguePracticeActive
			? TutorialClassText("warrior_room_rogue")
			: TutorialClassText("warrior_room_warrior");

	/// <summary>
	/// La tecnica si puo' provare senza possederla, ma solo dentro la lezione e solo per la
	/// classe che la lezione sta insegnando (§8.7 del design).
	/// </summary>
	private bool IsTutorialSandboxSupremeAllowed(HeroClass heroClass)
	{
		return (tutorialWarriorDuelActive && heroClass == HeroClass.Warrior)
			|| (tutorialMageDuelActive && heroClass == HeroClass.Mage);
	}

	private void StartTutorialWarriorDuel()
	{
		tutorialWarriorDuelActive = true;
		tutorialWarriorDuelStep = TutorialWarriorDuelStep.Intro;
		tutorialWarriorDuelActionUnlocked = false;
		tutorialDuelStrongestAttacks = 0;
		// Il tutorial di combattimento e' gia' quello che spegne aure automatiche e
		// interazioni non pertinenti: la lezione ne e' una variante, non un secondo sistema.
		adventureScriptedTutorialActive = true;
		adventureScriptedTutorialStepAcknowledged = false;
		adventureScriptedTutorialPendingTarget = null;

		EnsureAdventureScriptedTutorialView();
		ReturnToStart(showModeSelection: false);
		SetAccountHubHudActive(false);
		SetBattlefieldSurfaceVisible(visible: true);
		SetCombatChromeVisible(visible: true);
		SetAdventureTutorialTimelineVisible(visible: false);
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}

		if (!BuildTutorialWarriorDuelRoom())
		{
			SetMessage(TutorialClassText("warrior_cards_missing"));
			EndTutorialWarriorDuel(complete: false);
			return;
		}
		RefreshTutorialWarriorHudVisibility();

		ShowTutorialDuelStep(
			TutorialClassText("warrior_welcome_title"),
			TutorialClassText("warrior_welcome_body"),
			null,
			continueEnabled: true);
		ShowTutorialWarriorCircularFocus(null);
	}

	/// <summary>
	/// Costruisce la stanza a formazioni fisse, saltando pesca e schieramento: la lezione
	/// riguarda il combattimento, e far scegliere le carte prima di aver spiegato cosa
	/// significano i numeri non insegnerebbe niente.
	/// </summary>
	private bool BuildTutorialWarriorDuelRoom()
	{
		CardDefinition player = FindTutorialCard(TutorialDuelPlayerCardId);
		CardDefinition weak = FindTutorialCard(TutorialDuelWeakEnemyId);
		CardDefinition middle = FindTutorialCard(TutorialDuelMiddleEnemyId);
		CardDefinition strong = FindTutorialCard(TutorialDuelStrongEnemyId);
		if ((Object)(object)player == (Object)null
			|| (Object)(object)weak == (Object)null
			|| (Object)(object)middle == (Object)null
			|| (Object)(object)strong == (Object)null)
		{
			return false;
		}

		campaignDeck = new CampaignDeckState(new List<CardDefinition>());
		currentRoomType = RoomType.Monster;
		pendingRoomDifficulty = RoomDifficulty.Easy;
		campaignScenarioId = "default";
		pendingScenarioId = "default";
		currentScenarioDisplayOverride = TutorialClassText("scenario_name");
		ResetScenarioRuleState();
		LoadScenario(RoomType.Any, RoomDifficulty.Any, null, "default");
		RestoreCampaignMana(10);
		if ((Object)(object)campaignZoneRect != (Object)null)
		{
			((Component)campaignZoneRect).gameObject.SetActive(false);
		}

		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);

		BattleCardState playerState = AddCard(playerCards, playerRow, player, belongsToPlayer: true, 0);
		if (playerState != null)
		{
			// Iniziativa piu' alta di tutte: nella lezione si muove sempre prima il giocatore,
			// cosi' la sequenza e' quella scritta e non dipende da un tiro.
			playerState.Initiative = 20;
		}

		initialPlayerFormation.Clear();
		initialPlayerFormation.Add(player);
		initialCpuFormation.Clear();
		survivingCpuFormation.Clear();

		CardDefinition[] enemies = { weak, middle, strong };
		for (int index = 0; index < enemies.Length; index++)
		{
			BattleCardState state = AddCard(cpuCards, cpuRow, enemies[index], belongsToPlayer: false, index);
			if (state != null)
			{
				state.Initiative = 3 - index;
			}
			initialCpuFormation.Add(enemies[index]);
		}

		ApplyResponsiveLayout();
		RestoreBattlefieldCardVisibility();
		// Questa e' una palestra, non una normale stanza di combattimento: l'ordine e'
		// gia' determinato dal copione e StartBattle non deve lanciare i dadi iniziativa.
		deploymentInitiativesReady = true;
		StartBattle();
		return true;
	}

	private CardDefinition FindTutorialCard(string id)
	{
		List<CardDefinition> resolved = ResolveTutorialCards(new[] { id });
		return resolved.Count > 0 ? resolved[0] : null;
	}

	// ---- Avanzamento --------------------------------------------------------------

	/// <summary>
	/// La lezione ascolta gli stessi eventi del tutorial di combattimento. Restituisce true
	/// quando ha gestito l'evento, cosi' il tutorial di battaglia non lo elabora due volte.
	/// </summary>
	private bool AdvanceTutorialWarriorDuel(AdventureTutorialAction action)
	{
		if (tutorialMageDuelActive)
			return AdvanceTutorialMageDuel(action);
		if (tutorialRoguePracticeActive)
			return AdvanceTutorialRoguePractice(action);
		if (!tutorialWarriorDuelActive)
		{
			return false;
		}

		switch (tutorialWarriorDuelStep)
		{
		case TutorialWarriorDuelStep.Intro:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.Mana;
				RefreshTutorialWarriorHudVisibility();
				ShowTutorialDuelStep(
					TutorialClassText("warrior_mana_title"),
					TutorialClassText("warrior_mana_body"),
					null,
					continueEnabled: true);
				ShowTutorialWarriorCircularFocus(
					manaRuneImage != null ? manaRuneImage.rectTransform : null,
					enemyManaRuneImage != null ? enemyManaRuneImage.rectTransform : null);
			}
			return true;

		case TutorialWarriorDuelStep.Mana:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.Vigor;
				RefreshTutorialWarriorHudVisibility();
				ShowTutorialDuelStep(
					TutorialClassText("warrior_vigor_title"),
					TutorialClassText("warrior_vigor_body"),
					null,
					continueEnabled: true);
				ShowTutorialWarriorCircularFocus(
					combatVigorImage != null ? combatVigorImage.rectTransform : null,
					cpuHud?.DiceImage != null ? cpuHud.DiceImage.rectTransform : null);
			}
			return true;

		case TutorialWarriorDuelStep.Vigor:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.AttackWeakest;
				RefreshTutorialWarriorHudVisibility();
				HideTutorialWarriorCircularFocus();
				StartTutorialWarriorPawnEntrance();
				ShowTutorialDuelStep(
					TutorialClassText("warrior_attack_weak_title"),
					TutorialClassText("warrior_attack_weak_body"),
					null,
					continueEnabled: true);
			}
			return true;

		case TutorialWarriorDuelStep.AttackWeakest:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelActionUnlocked = true;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				RefreshCardActionOverlays();
				MoveAdventureTutorialSpotlight(ActivePlayerAttackActionRect());
				return true;
			}
			if (action == AdventureTutorialAction.AttackPressed)
			{
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialDuelWeakEnemyId));
				return true;
			}
			if (action == AdventureTutorialAction.BattleFinished)
			{
				return true;
			}
			if (action == AdventureTutorialAction.PlayerTurnStarted)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.AbilityOnMiddle;
				ShowTutorialDuelAbilityStep();
			}
			return true;

		case TutorialWarriorDuelStep.AbilityOnMiddle:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelActionUnlocked = true;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				RefreshCardActionOverlays();
				MoveAdventureTutorialSpotlight(ActivePlayerAbilityActionRect());
				return true;
			}
			if (action == AdventureTutorialAction.AbilityPressed)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.AttackMiddle;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				if (selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
					ShowTargetHints(playerCards[selectedPlayerIndex]);
				UpdateInteractions();
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialDuelMiddleEnemyId));
			}
			return true;

		case TutorialWarriorDuelStep.AttackMiddle:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelActionUnlocked = true;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				RefreshCardActionOverlays();
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialDuelMiddleEnemyId));
				return true;
			}
			if (action == AdventureTutorialAction.PlayerTurnStarted)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.AttackStrongest;
				ShowTutorialDuelAttackStep(
					TutorialClassText("warrior_attack_strong_title"),
					TutorialClassText("warrior_attack_strong_body"));
			}
			return true;

		case TutorialWarriorDuelStep.AttackStrongest:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelActionUnlocked = true;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				RefreshCardActionOverlays();
				MoveAdventureTutorialSpotlight(ActivePlayerAttackActionRect());
				return true;
			}
			if (action == AdventureTutorialAction.AttackPressed)
			{
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialDuelStrongEnemyId));
				return true;
			}
			if (action == AdventureTutorialAction.PlayerTurnStarted && tutorialDuelStrongestAttacks >= 1)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.ImpossibleExplained;
				ShowTutorialDuelImpossibleStep();
			}
			return true;

		case TutorialWarriorDuelStep.ImpossibleExplained:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.SupremeExplained;
				ShowTutorialDuelSupremeStep();
			}
			return true;

		case TutorialWarriorDuelStep.SupremeExplained:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelActionUnlocked = true;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				RefreshCardActionOverlays();
				MoveAdventureTutorialSpotlight(ActivePlayerSupremeActionRect());
				return true;
			}
			if (action == AdventureTutorialAction.SupremeUsed)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.AttackStrongestAgain;
				ShowTutorialDuelAttackStep(
					TutorialClassText("warrior_retry_title"),
					TutorialClassText("warrior_retry_body"));
			}
			return true;

		case TutorialWarriorDuelStep.AttackStrongestAgain:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialWarriorDuelActionUnlocked = true;
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				RefreshCardActionOverlays();
				MoveAdventureTutorialSpotlight(ActivePlayerAttackActionRect());
				return true;
			}
			if (action == AdventureTutorialAction.AttackPressed)
			{
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialDuelStrongEnemyId));
				return true;
			}
			if (action == AdventureTutorialAction.BattleFinished)
			{
				tutorialWarriorDuelStep = TutorialWarriorDuelStep.Done;
				ShowTutorialDuelStep(
					TutorialClassText("warrior_complete_title"),
					TutorialClassText("warrior_complete_body"),
					null,
					continueEnabled: true);
			}
			return true;

		case TutorialWarriorDuelStep.Done:
			if (action == AdventureTutorialAction.NextPressed)
			{
				EndTutorialWarriorDuel(complete: true);
			}
			return true;
		}

		return true;
	}

	private void ShowTutorialDuelAbilityStep()
	{
		int cost = AbilityManaCosts.Primary(HeroClass.Warrior);
		ShowTutorialDuelStep(
			TutorialClassText("warrior_ability_title"),
			TutorialClassText("warrior_ability_body", cost),
			null,
			continueEnabled: true);
	}

	private void ShowTutorialDuelImpossibleStep()
	{
		ShowTutorialDuelStep(
			TutorialClassText("warrior_impossible_title"),
			TutorialClassText("warrior_impossible_body"),
			null,
			continueEnabled: true);
	}

	private void ShowTutorialDuelSupremeStep()
	{
		// La tecnica costa piu' di quanto sia rimasto dopo l'abilita': in una lezione la
		// riserva si rimette a posto, altrimenti il passo successivo sarebbe impossibile e
		// il giocatore resterebbe fermo davanti a un pulsante spento.
		RestoreCampaignMana(10);
		int cost = AbilityManaCosts.Supreme(HeroClass.Warrior);
		ShowTutorialDuelStep(
			TutorialClassText("warrior_supreme_title"),
			TutorialClassText("warrior_supreme_body", cost),
			null,
			continueEnabled: true);
	}

	private void ShowTutorialDuelAttackStep(string title, string body)
	{
		ShowTutorialDuelStep(title, body, null, continueEnabled: true);
	}

	private RectTransform TutorialWarriorEnemyRect(string cardId)
	{
		BattleCardState target = cpuCards.FirstOrDefault(card => card != null && !card.Eliminated
			&& IsTutorialCard(card.Card.Id, cardId));
		return target?.View != null ? target.View.RectTransform : null;
	}

	private void ShowTutorialDuelStep(string title, string body, RectTransform target, bool continueEnabled)
	{
		tutorialWarriorDuelActionUnlocked = false;
		EnsureAdventureScriptedTutorialView();
		adventureScriptedTutorialPanel.SetActive(true);
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		SetMessagePanelVisibleDuringAdventureTutorial(visible: false);
		adventureScriptedTutorialPendingTarget = target;
		if ((Object)(object)adventureScriptedTutorialTitleText != (Object)null)
			adventureScriptedTutorialTitleText.text = title;
		adventureScriptedTutorialStepText.text = LocalizedAdventureTutorialStepCounter(
			(int)tutorialWarriorDuelStep + 1, (int)TutorialWarriorDuelStep.Done + 1);
		PlaceAdventureTutorialPanel(target);
		ResizeAdventureTutorialPanelForBody(body);
		StartAdventureTutorialBodyText(body);
		SetAdventureTutorialNextButtonEnabled(continueEnabled);
		MoveAdventureTutorialSpotlight(target);
		RefreshCardActionOverlays();
	}

	private bool TutorialWarriorDuelAllowsAttack()
	{
		if (tutorialMageDuelActive)
			return TutorialMageDuelAllowsAttack();
		if (tutorialRoguePracticeActive)
			return TutorialRoguePracticeAllowsAttack();
		return !tutorialWarriorDuelActive || (tutorialWarriorDuelActionUnlocked
			&& tutorialWarriorDuelStep is TutorialWarriorDuelStep.AttackWeakest
				or TutorialWarriorDuelStep.AttackMiddle
				or TutorialWarriorDuelStep.AttackStrongest
				or TutorialWarriorDuelStep.AttackStrongestAgain);
	}

	private bool TutorialWarriorDuelAllowsAbility()
	{
		if (tutorialMageDuelActive)
			return TutorialMageDuelAllowsAbility();
		if (tutorialRoguePracticeActive)
			return false;
		return !tutorialWarriorDuelActive || (tutorialWarriorDuelActionUnlocked
			&& tutorialWarriorDuelStep == TutorialWarriorDuelStep.AbilityOnMiddle);
	}

	private bool TutorialWarriorDuelAllowsSupreme()
	{
		if (tutorialMageDuelActive)
			return TutorialMageDuelAllowsSupreme();
		if (tutorialRoguePracticeActive)
			return false;
		return !tutorialWarriorDuelActive || (tutorialWarriorDuelActionUnlocked
			&& tutorialWarriorDuelStep == TutorialWarriorDuelStep.SupremeExplained);
	}

	private bool TutorialWarriorDuelAllowsEnemyTarget(BattleCardState target)
	{
		if (tutorialMageDuelActive)
			return TutorialMageDuelAllowsEnemyTarget(target);
		if (tutorialRoguePracticeActive)
			return TutorialRoguePracticeAllowsEnemyTarget(target);
		if (!tutorialWarriorDuelActive)
			return true;
		if (!tutorialWarriorDuelActionUnlocked || target == null)
			return false;
		string expectedId = tutorialWarriorDuelStep switch
		{
			TutorialWarriorDuelStep.AttackWeakest => TutorialDuelWeakEnemyId,
			TutorialWarriorDuelStep.AttackMiddle => TutorialDuelMiddleEnemyId,
			TutorialWarriorDuelStep.AttackStrongest => TutorialDuelStrongEnemyId,
			TutorialWarriorDuelStep.AttackStrongestAgain => TutorialDuelStrongEnemyId,
			_ => null
		};
		return !string.IsNullOrEmpty(expectedId)
			&& IsTutorialCard(target.Card.Id, expectedId);
	}

	private void RefreshTutorialWarriorHudVisibility()
	{
		bool showPawns = tutorialWarriorDuelActive
			&& tutorialWarriorDuelStep > TutorialWarriorDuelStep.Vigor;
		bool showMana = tutorialWarriorDuelActive
			&& tutorialWarriorDuelStep >= TutorialWarriorDuelStep.Mana;
		bool showVigor = tutorialWarriorDuelActive
			&& tutorialWarriorDuelStep >= TutorialWarriorDuelStep.Vigor;

		SetTutorialWarriorPawnVisibility(playerCards, showPawns);
		SetTutorialWarriorPawnVisibility(cpuCards, showPawns);
		SetAdventureTutorialTimelineVisible(visible: false);
		SetHudImageVisible(manaRuneImage, showMana);
		SetHudImageVisible(enemyManaRuneImage, showMana);
		SetHudImageVisible(combatVigorImage, showVigor);
		SetHudImageVisible(cpuHud?.DiceImage, showVigor);
		SetHudRectVisible(cpuHud?.DiceText?.rectTransform, showVigor);
		if ((Object)(object)combatExperienceRoot != (Object)null)
			combatExperienceRoot.gameObject.SetActive(false);

		ClearTutorialWarriorHudHighlights();
		if (tutorialWarriorDuelStep == TutorialWarriorDuelStep.Mana)
		{
			AddTutorialWarriorHudHighlight(manaRuneImage);
			AddTutorialWarriorHudHighlight(enemyManaRuneImage);
		}
	}

	private void ShowTutorialWarriorCircularFocus(params RectTransform[] targets)
	{
		EnsureTutorialWarriorFocusOverlay();
		if ((Object)(object)tutorialWarriorFocusOverlay == (Object)null)
			return;

		GameObject overlayObject = tutorialWarriorFocusOverlay.gameObject;
		overlayObject.SetActive(true);
		tutorialWarriorFocusOverlay.rectTransform.SetAsLastSibling();
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();

		RectTransform overlayRect = tutorialWarriorFocusOverlay.rectTransform;
		(Vector2 center, float radius) first = TutorialWarriorTargetFocus(
			targets != null && targets.Length > 0 ? targets[0] : null,
			overlayRect);
		(Vector2 center, float radius) second = TutorialWarriorTargetFocus(
			targets != null && targets.Length > 1 ? targets[1] : null,
			overlayRect);

		if (tutorialWarriorFocusRoutine != null)
			StopCoroutine(tutorialWarriorFocusRoutine);
		tutorialWarriorFocusRoutine = StartCoroutine(AnimateTutorialWarriorFocus(first, second));
	}

	private static (Vector2 center, float radius) TutorialWarriorTargetFocus(
		RectTransform target,
		RectTransform overlayRect)
	{
		if ((Object)(object)target == (Object)null
			|| (Object)(object)overlayRect == (Object)null
			|| !target.gameObject.activeInHierarchy)
			return (new Vector2(0.5f, 0.5f), 0f);

		Vector3[] corners = new Vector3[4];
		target.GetWorldCorners(corners);
		Vector3 min = new Vector3(float.MaxValue, float.MaxValue);
		Vector3 max = new Vector3(float.MinValue, float.MinValue);
		for (int index = 0; index < corners.Length; index++)
		{
			Vector3 local = overlayRect.InverseTransformPoint(corners[index]);
			min = Vector3.Min(min, local);
			max = Vector3.Max(max, local);
		}

		Vector2 localCenter = (min + max) * 0.5f;
		Vector2 center = new Vector2(
			Mathf.InverseLerp(overlayRect.rect.xMin, overlayRect.rect.xMax, localCenter.x),
			Mathf.InverseLerp(overlayRect.rect.yMin, overlayRect.rect.yMax, localCenter.y));
		float diameterPixels = Mathf.Max(max.x - min.x, max.y - min.y) + 36f;
		float radius = diameterPixels * 0.5f / Mathf.Max(1f, overlayRect.rect.height);
		return (center, radius);
	}

	private void EnsureTutorialWarriorFocusOverlay()
	{
		if ((Object)(object)tutorialWarriorFocusOverlay != (Object)null)
			return;
		Shader shader = Shader.Find("AccardND/UI/Tutorial Circular Spotlight");
		if ((Object)(object)shader == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
			return;
		Canvas canvas = safeAreaRoot.GetComponentInParent<Canvas>();
		RectTransform overlayParent = canvas != null
			? canvas.rootCanvas.transform as RectTransform
			: safeAreaRoot;
		if ((Object)(object)overlayParent == (Object)null)
			return;

		tutorialWarriorFocusOverlay = CreateImage(
			"Tutorial Warrior Circular Focus",
			(Transform)(object)overlayParent,
			Color.white);
		RectTransform rect = tutorialWarriorFocusOverlay.rectTransform;
		SetRect(rect, Vector2.zero, Vector2.one);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		tutorialWarriorFocusOverlay.raycastTarget = false;
		tutorialWarriorFocusMaterial = new Material(shader) { name = "Tutorial Warrior Circular Focus (Runtime)" };
		tutorialWarriorFocusOverlay.material = tutorialWarriorFocusMaterial;

		// L'overlay vive sul root canvas per coprire anche notch e bande fuori dalla
		// safe area. Il dialogo invece resta nella safe area, ma riceve un canvas
		// ordinato sopra la maschera cosi' testo e CONTINUA rimangono visibili/cliccabili.
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			Canvas dialogCanvas = adventureScriptedTutorialPanel.GetComponent<Canvas>();
			if ((Object)(object)dialogCanvas == (Object)null)
				dialogCanvas = adventureScriptedTutorialPanel.AddComponent<Canvas>();
			dialogCanvas.overrideSorting = true;
			dialogCanvas.sortingOrder = canvas != null ? canvas.rootCanvas.sortingOrder + 100 : 100;
			if ((Object)(object)adventureScriptedTutorialPanel.GetComponent<GraphicRaycaster>() == (Object)null)
				adventureScriptedTutorialPanel.AddComponent<GraphicRaycaster>();
		}
	}

	private IEnumerator AnimateTutorialWarriorFocus(
		(Vector2 center, float radius) first,
		(Vector2 center, float radius) second)
	{
		const float duration = 0.32f;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)tutorialWarriorFocusMaterial != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
			ApplyTutorialWarriorFocus(first.center, Mathf.Lerp(0f, first.radius, progress),
				second.center, Mathf.Lerp(0f, second.radius, progress));
			yield return null;
		}
		ApplyTutorialWarriorFocus(first.center, first.radius, second.center, second.radius);
		tutorialWarriorFocusRoutine = null;
	}

	private void ApplyTutorialWarriorFocus(Vector2 center, float radius, Vector2 center2, float radius2)
	{
		if ((Object)(object)tutorialWarriorFocusMaterial == (Object)null
			|| (Object)(object)tutorialWarriorFocusOverlay == (Object)null)
			return;
		Rect rect = tutorialWarriorFocusOverlay.rectTransform.rect;
		tutorialWarriorFocusMaterial.SetColor("_Color", Color.black);
		tutorialWarriorFocusMaterial.SetVector("_HoleCenter", new Vector4(center.x, center.y, 0f, 0f));
		tutorialWarriorFocusMaterial.SetFloat("_HoleRadius", radius);
		tutorialWarriorFocusMaterial.SetVector("_HoleCenter2", new Vector4(center2.x, center2.y, 0f, 0f));
		tutorialWarriorFocusMaterial.SetFloat("_HoleRadius2", radius2);
		tutorialWarriorFocusMaterial.SetFloat("_Aspect", rect.height > 0f ? rect.width / rect.height : 1f);
		tutorialWarriorFocusMaterial.SetFloat("_Feather", 14f / Mathf.Max(1f, rect.height));
	}

	private void HideTutorialWarriorCircularFocus()
	{
		if (tutorialWarriorFocusRoutine != null)
		{
			StopCoroutine(tutorialWarriorFocusRoutine);
			tutorialWarriorFocusRoutine = null;
		}
		if ((Object)(object)tutorialWarriorFocusOverlay != (Object)null)
			tutorialWarriorFocusOverlay.gameObject.SetActive(false);
	}

	private void StartTutorialWarriorPawnEntrance()
	{
		if (tutorialWarriorPawnEntranceRoutine != null)
			StopCoroutine(tutorialWarriorPawnEntranceRoutine);
		tutorialWarriorPawnEntranceRoutine = StartCoroutine(PlayTutorialWarriorPawnEntrance());
	}

	private IEnumerator PlayTutorialWarriorPawnEntrance()
	{
		var views = playerCards.Concat(cpuCards)
			.Where(card => card?.View != null)
			.Select(card => card.View)
			.ToList();
		HeroClass entranceClass = tutorialMageDuelActive ? HeroClass.Mage : HeroClass.Warrior;
		BattleCardState entrancePawn = playerCards.Concat(cpuCards)
			.FirstOrDefault(card => card != null && card.Card.HeroClass == entranceClass);
		PlayPawnEnteringBattlefieldSfx(entrancePawn);
		var originalScales = views.Select(view => view.RectTransform.localScale).ToList();
		for (int index = 0; index < views.Count; index++)
			views[index].RectTransform.localScale = originalScales[index] * 0.15f;

		const float duration = 0.42f;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			float overshoot = 1f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
			for (int index = 0; index < views.Count; index++)
			{
				if ((Object)(object)views[index] != (Object)null && (Object)(object)views[index].RectTransform != (Object)null)
					views[index].RectTransform.localScale = originalScales[index] * Mathf.Lerp(0.15f, overshoot, Mathf.SmoothStep(0f, 1f, progress));
			}
			yield return null;
		}
		for (int index = 0; index < views.Count; index++)
			if ((Object)(object)views[index] != (Object)null && (Object)(object)views[index].RectTransform != (Object)null)
				views[index].RectTransform.localScale = originalScales[index];
		tutorialWarriorPawnEntranceRoutine = null;
	}

	private static void SetTutorialWarriorPawnVisibility(
		IEnumerable<BattleCardState> cards,
		bool visible)
	{
		if (cards == null)
			return;
		foreach (BattleCardState card in cards)
		{
			if (card?.View != null)
				((Component)card.View).gameObject.SetActive(visible);
		}
	}

	private void AddTutorialWarriorHudHighlight(Image image)
	{
		if ((Object)(object)image == (Object)null)
			return;
		Outline highlight = ((Component)image).gameObject.AddComponent<Outline>();
		highlight.effectColor = new Color(1f, 0.76f, 0.16f, 0.95f);
		highlight.effectDistance = new Vector2(5f, -5f);
		highlight.useGraphicAlpha = false;
		tutorialWarriorHudHighlights.Add(highlight);
	}

	private void ClearTutorialWarriorHudHighlights()
	{
		foreach (Outline highlight in tutorialWarriorHudHighlights)
		{
			if ((Object)(object)highlight != (Object)null)
				Object.Destroy((Object)(object)highlight);
		}
		tutorialWarriorHudHighlights.Clear();
	}

	private void EndTutorialWarriorDuel(bool complete)
	{
		ClearTutorialWarriorHudHighlights();
		HideTutorialWarriorCircularFocus();
		if (tutorialWarriorPawnEntranceRoutine != null)
		{
			StopCoroutine(tutorialWarriorPawnEntranceRoutine);
			tutorialWarriorPawnEntranceRoutine = null;
		}
		tutorialWarriorDuelActive = false;
		tutorialWarriorDuelActionUnlocked = false;
		adventureScriptedTutorialActive = false;
		if (adventureScriptedTutorialTextRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(adventureScriptedTutorialTextRoutine);
			adventureScriptedTutorialTextRoutine = null;
		}
		// A lezione completata resta visibile solo il pannello RICOMPENSA OTTENUTA;
		// il riepilogo standard della stanza tornera' disponibile uscendo verso l'Hub.
		SetMessagePanelVisibleDuringAdventureTutorial(visible: !complete);
		SetAdventureTutorialTimelineVisible(visible: false);
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			adventureScriptedTutorialPanel.SetActive(false);
		}
		MoveAdventureTutorialSpotlight(null);
		SetAdventureTutorialDimmers(null);

		if (complete)
		{
			CompleteActiveTutorialModule();
			return;
		}
		activeTutorialModuleId = null;
		ReturnToTutorialIndex();
	}

	// ---- Tiri scriptati -----------------------------------------------------------

	/// <summary>
	/// I tiri della lezione. Non e' un combattimento equilibrato: e' una dimostrazione, e
	/// ogni scontro deve finire come dice il copione. In particolare il primo attacco al
	/// Guerriero da 10 **deve** fallire, altrimenti la tecnica non avrebbe niente da
	/// dimostrare.
	/// </summary>
	private bool TryScriptTutorialWarriorDuelResult(
		BattleCardState attacker,
		BattleCardState defender,
		CombatResult resolved,
		out CombatResult scripted)
	{
		scripted = resolved;
		if (tutorialMageDuelActive)
			return TryScriptTutorialMageDuelResult(attacker, defender, resolved, out scripted);
		if (tutorialRoguePracticeActive)
			return TryScriptTutorialRoguePracticeResult(attacker, defender, resolved, out scripted);
		if (!tutorialWarriorDuelActive || attacker == null || defender == null)
		{
			return false;
		}

		// I nemici non attaccano mai in questa lezione (vedi il turno CPU): se per qualunque
		// motivo ci arrivassero, il loro colpo non deve comunque uccidere l'unica pedina del
		// giocatore e interrompere la lezione a meta'.
		if (!attacker.BelongsToPlayer)
		{
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 1, 0, resolved.DefenderRoll.DieSides);
			return true;
		}

		string defenderId = defender.Card.Id;
		if (IsTutorialCard(defenderId, TutorialDuelWeakEnemyId))
		{
			// 6+3 = 9 contro 4+1 = 5: vince con margine, senza sembrare truccato.
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 3, 0, 1);
			return true;
		}

		if (IsTutorialCard(defenderId, TutorialDuelMiddleEnemyId))
		{
			// Col colpo pesante il Guerriero somma due dadi: 6+3+2 = 11 contro 7+2 = 9.
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 3, 2, 2);
			return true;
		}

		if (IsTutorialCard(defenderId, TutorialDuelStrongEnemyId))
		{
			tutorialDuelStrongestAttacks++;
			if (tutorialDuelStrongestAttacks <= 1)
			{
				// Primo tentativo: anche col massimo del D4, 6+4 = 10 non puo'
				// superare il minimo avversario, 10+1 = 11. Deve perdere.
				scripted = BuildTutorialDuelResult(attacker, defender, resolved, 4, 0, 1);
				return true;
			}
			// Dopo Potenziamento la potenza e' 10: 10+3 = 13 contro 10+2 = 12.
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 3, 0, 2);
			return true;
		}

		return false;
	}

	private bool ShouldRollImpossibleTutorialWarriorDuelAttack(
		BattleCardState attacker,
		BattleCardState defender)
	{
		return tutorialWarriorDuelActive
			&& tutorialWarriorDuelStep == TutorialWarriorDuelStep.AttackStrongest
			&& tutorialDuelStrongestAttacks == 0
			&& attacker != null
			&& attacker.BelongsToPlayer
			&& IsTutorialCard(attacker.Card.Id, TutorialDuelPlayerCardId)
			&& defender != null
			&& IsTutorialCard(defender.Card.Id, TutorialDuelStrongEnemyId);
	}

	private static CombatResult BuildTutorialDuelResult(
		BattleCardState attacker,
		BattleCardState defender,
		CombatResult resolved,
		int attackerFirst,
		int attackerSecond,
		int defenderRoll)
	{
		VigorRollResult attackerVigor = ScriptRoll(resolved.AttackerRoll, attackerFirst, attackerSecond);
		VigorRollResult defenderVigor = ScriptRoll(resolved.DefenderRoll, defenderRoll);
		// La potenza si rilegge dal risolutore invece di ricalcolarla: Potenziamento l'ha
		// gia' alzata, e sommarla di nuovo qui vorrebbe dire contarla due volte.
		int attackerPower = resolved.AttackerTotal - resolved.AttackerRoll.SelectedRoll;
		int defenderPower = resolved.DefenderTotal - resolved.DefenderRoll.SelectedRoll;
		return new CombatResult(
			attackerVigor,
			defenderVigor,
			attackerPower + attackerVigor.SelectedRoll,
			defenderPower + defenderVigor.SelectedRoll);
	}

}
}
