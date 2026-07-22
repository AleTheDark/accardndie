using System;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private enum AdventureTutorialAction
	{
		Started,
		DraftReady,
		DraftCardSelected,
		DraftConfirmed,
		DeploymentCardSelected,
		DeploymentConfirmed,
		PlayerTurnStarted,
		EnemyTargeted,
		BattleFinished
	}

	private void StartAdventureScriptedTutorial()
	{
		adventureScriptedTutorialActive = true;
		adventureScriptedTutorialStep = 0;
		EnsureAdventureScriptedTutorialView();
		ReturnToStart(showModeSelection: false);
		SetBattlefieldSurfaceVisible(visible: true);
		SetCombatChromeVisible(visible: true);
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}
		BuildScriptedTutorialRun();
		ShowAdventureScriptedTutorialStep(
			"Tutorial guidato",
			"Questa non e una spiegazione fuori dal gioco: giocherai una stanza controllata. Ti illumino cosa guardare o premere, e il resto resta bloccato finche non serve.",
			null);
	}

	private void BuildScriptedTutorialRun()
	{
		List<CardDefinition> playerDeck = BuildTutorialPlayerDeck();
		if (playerDeck.Count == 0)
		{
			SetMessage("Tutorial non disponibile: nessuna carta valida nel database.");
			EndAdventureScriptedTutorial(complete: false);
			return;
		}

		campaignDeck = new CampaignDeckState(playerDeck);
		currentRoomType = RoomType.Monster;
		currentMonsterTier = 1;
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Normal;
		currentScenarioDisplayOverride = "Tutorial - Primo Scontro";
		ResetScenarioRuleState();
		((Component)campaignZoneRect).gameObject.SetActive(false);
		AppendLog("TUTORIAL AVVENTURA - run scriptata avviata.");
		PrepareNextCampaignCombatDraft();
	}

	private List<CardDefinition> BuildTutorialPlayerDeck()
	{
		IReadOnlyList<CardDefinition> cards = TutorialCards();
		if (cards.Count == 0)
		{
			return new List<CardDefinition>();
		}
		HeroClass[] preferred =
		{
			HeroClass.Warrior,
			HeroClass.Mage,
			HeroClass.Hunter,
			HeroClass.Paladin,
			HeroClass.Rogue,
			HeroClass.Priest
		};
		List<CardDefinition> result = new List<CardDefinition>();
		foreach (HeroClass heroClass in preferred)
		{
			CardDefinition card = FindTutorialCard(cards, CardCategory.Monster, heroClass, result);
			if ((Object)(object)card != (Object)null)
			{
				result.Add(card);
			}
		}
		int targetCount = Mathf.Max(configuration.Gameplay.FormationSize, configuration.DeckBuilding.CombatHandSize);
		foreach (CardDefinition card in cards
			.Where(card => (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat)
			.OrderBy(card => card.Strength))
		{
			if (result.Count >= targetCount)
			{
				break;
			}
			if (!result.Contains(card))
			{
				result.Add(card);
			}
		}
		return result;
	}

	private IReadOnlyList<CardDefinition> TutorialCards()
	{
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		return cardDatabase?.Cards ?? Array.Empty<CardDefinition>();
	}

	private CardDefinition FindTutorialCard(IReadOnlyList<CardDefinition> cards, CardCategory category, HeroClass heroClass, List<CardDefinition> excluded)
	{
		return cards
			.Where(card => (Object)(object)card != (Object)null
				&& card.Category == category
				&& card.CanEnterCombat
				&& card.HeroClass == heroClass
				&& (excluded == null || !excluded.Contains(card)))
			.OrderBy(card => card.Strength)
			.FirstOrDefault();
	}

	private void NotifyAdventureTutorial(AdventureTutorialAction action)
	{
		if (!adventureScriptedTutorialActive)
		{
			return;
		}
		AdvanceAdventureTutorialAfter(action);
	}

	private void AdvanceAdventureTutorialAfter(AdventureTutorialAction action)
	{
		switch (adventureScriptedTutorialStep)
		{
		case 0:
			if (action == AdventureTutorialAction.DraftReady)
			{
				adventureScriptedTutorialStep = 1;
				ShowAdventureScriptedTutorialStep(
					"Scegli le carte",
					"Queste sono le carte disponibili per la stanza. Tocca una carta della tua mano: la useremo per costruire la formazione.",
					FirstVisibleDraftCardRect());
			}
			break;
		case 1:
			if (action == AdventureTutorialAction.DraftCardSelected)
			{
				adventureScriptedTutorialStep = 2;
				ShowAdventureScriptedTutorialStep(
					"Completa la formazione",
					"Ogni stanza richiede una formazione completa. Seleziona le altre carte richieste, poi premi CONFERMA quando il pulsante si illumina.",
					(Object)(object)confirmActionButton != (Object)null ? (RectTransform)((Component)confirmActionButton).transform : null);
			}
			break;
		case 2:
			if (action == AdventureTutorialAction.DraftConfirmed)
			{
				adventureScriptedTutorialStep = 3;
				ShowAdventureScriptedTutorialStep(
					"Iniziativa e schieramento",
					"Il gioco tira le iniziative. Quando tocca a te, scegli quale carta entra in campo in quel momento: le iniziative alte agiscono prima.",
					FirstVisibleDraftCardRect());
			}
			break;
		case 3:
			if (action == AdventureTutorialAction.DeploymentCardSelected)
			{
				adventureScriptedTutorialStep = 4;
				ShowAdventureScriptedTutorialStep(
					"Conferma lo schieramento",
					"Questa carta e pronta a entrare. Premi CONFERMA per metterla sul campo.",
					(Object)(object)confirmActionButton != (Object)null ? (RectTransform)((Component)confirmActionButton).transform : null);
			}
			break;
		case 4:
			if (action == AdventureTutorialAction.DeploymentConfirmed)
			{
				adventureScriptedTutorialStep = 5;
				ShowAdventureScriptedTutorialStep(
					"Il campo",
					"Sopra ci sono i mostri del Master, sotto la tua formazione. Aspettiamo il tuo primo turno: il gioco evidenziera la pedina attiva.",
					(Object)(object)playerRow != (Object)null ? playerRow : null);
			}
			break;
		case 5:
			if (action == AdventureTutorialAction.PlayerTurnStarted)
			{
				adventureScriptedTutorialStep = 6;
				ShowAdventureScriptedTutorialStep(
					"Attacca un mostro",
					"Nel tuo turno scegli un bersaglio nemico. Tocca una carta del Master: partiranno i dadi Vigore e vedrai come si risolve il confronto.",
					FirstAliveCpuCardRect());
			}
			break;
		case 6:
			if (action == AdventureTutorialAction.EnemyTargeted)
			{
				adventureScriptedTutorialStep = 7;
				ShowAdventureScriptedTutorialStep(
					"Dadi Vigore",
					"Durante l'attacco il dado Vigore si somma alla forza della carta. Nella prossima iterazione questi tiri saranno completamente scriptati.",
					null);
			}
			break;
		case 7:
			if (action == AdventureTutorialAction.BattleFinished)
			{
				adventureScriptedTutorialStep = 8;
				ShowAdventureScriptedTutorialStep(
					"Ricompensa",
					"Hai completato il tutorial. Ora assegno i vasetti di miele e sblocco il proseguimento dell'Avventura.",
					null);
				EndAdventureScriptedTutorial(complete: true);
			}
			break;
		}
	}

	private RectTransform FirstVisibleDraftCardRect()
	{
		foreach (PrototypeCardView view in draftViews)
		{
			if ((Object)(object)view != (Object)null && ((Component)view).gameObject.activeInHierarchy)
			{
				return view.RectTransform;
			}
		}
		return null;
	}

	private RectTransform FirstAliveCpuCardRect()
	{
		foreach (BattleCardState card in cpuCards)
		{
			if (card != null && !card.Eliminated && (Object)(object)card.View != (Object)null)
			{
				return card.View.RectTransform;
			}
		}
		return null;
	}

	private void EnsureAdventureScriptedTutorialView()
	{
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			return;
		}

		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		CreateAdventureTutorialDimmers();
		Image panel = CreateImage("Adventure Scripted Tutorial Panel", (Transform)(object)safeAreaRoot, new Color(0.01f, 0.018f, 0.028f, 0.94f));
		panel.raycastTarget = false;
		StylePanel(panel);
		SetRect(panel.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.22f));
		adventureScriptedTutorialPanel = ((Component)panel).gameObject;

		adventureScriptedTutorialTitleText = CreateText("Adventure Scripted Tutorial Title", ((Component)panel).transform, font, 22, (FontStyle)1, (TextAnchor)3);
		adventureScriptedTutorialTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		adventureScriptedTutorialTitleText.raycastTarget = false;
		SetRect(adventureScriptedTutorialTitleText.rectTransform, new Vector2(0.04f, 0.58f), new Vector2(0.72f, 0.9f));

		adventureScriptedTutorialStepText = CreateText("Adventure Scripted Tutorial Counter", ((Component)panel).transform, font, 16, (FontStyle)1, (TextAnchor)5);
		adventureScriptedTutorialStepText.color = new Color(0.64f, 0.78f, 0.86f);
		adventureScriptedTutorialStepText.raycastTarget = false;
		SetRect(adventureScriptedTutorialStepText.rectTransform, new Vector2(0.73f, 0.58f), new Vector2(0.96f, 0.9f));

		adventureScriptedTutorialBodyText = CreateText("Adventure Scripted Tutorial Body", ((Component)panel).transform, font, 18, (FontStyle)0, (TextAnchor)3);
		adventureScriptedTutorialBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		adventureScriptedTutorialBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		adventureScriptedTutorialBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		adventureScriptedTutorialBodyText.resizeTextForBestFit = true;
		adventureScriptedTutorialBodyText.resizeTextMinSize = 12;
		adventureScriptedTutorialBodyText.resizeTextMaxSize = 18;
		adventureScriptedTutorialBodyText.raycastTarget = false;
		SetRect(adventureScriptedTutorialBodyText.rectTransform, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.58f));

		adventureScriptedTutorialSpotlight = CreateImage("Adventure Scripted Tutorial Spotlight", (Transform)(object)safeAreaRoot, Color.white);
		adventureScriptedTutorialSpotlight.sprite = GetHelpAuraSprite();
		adventureScriptedTutorialSpotlight.preserveAspect = false;
		adventureScriptedTutorialSpotlight.raycastTarget = false;
		((Component)adventureScriptedTutorialSpotlight).gameObject.SetActive(false);
		adventureScriptedTutorialPanel.SetActive(false);
	}

	private void CreateAdventureTutorialDimmers()
	{
		if (adventureScriptedTutorialDimmers.Count > 0)
		{
			return;
		}
		for (int index = 0; index < 4; index++)
		{
			Image dimmer = CreateImage("Adventure Tutorial Dimmer " + index, (Transform)(object)safeAreaRoot, new Color(0f, 0f, 0f, 0.62f));
			dimmer.raycastTarget = false;
			((Component)dimmer).gameObject.SetActive(false);
			adventureScriptedTutorialDimmers.Add(dimmer);
		}
	}

	private void ShowAdventureScriptedTutorialStep(string title, string body, RectTransform target)
	{
		EnsureAdventureScriptedTutorialView();
		adventureScriptedTutorialPanel.SetActive(true);
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		adventureScriptedTutorialTitleText.text = title;
		adventureScriptedTutorialBodyText.text = body;
		adventureScriptedTutorialStepText.text = $"PASSO {adventureScriptedTutorialStep + 1}/8";
		PlaceAdventureTutorialPanel(target);
		MoveAdventureTutorialSpotlight(target);
	}

	private void PlaceAdventureTutorialPanel(RectTransform target)
	{
		if ((Object)(object)adventureScriptedTutorialPanel == (Object)null)
		{
			return;
		}
		RectTransform panelRect = (RectTransform)adventureScriptedTutorialPanel.transform;
		if ((Object)(object)target == (Object)null)
		{
			SetRect(panelRect, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.22f));
			return;
		}
		Vector3[] corners = new Vector3[4];
		target.GetWorldCorners(corners);
		Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
		Vector3 localCenter = ((Transform)safeAreaRoot).InverseTransformPoint(worldCenter);
		float normalizedY = Mathf.InverseLerp(safeAreaRoot.rect.yMin, safeAreaRoot.rect.yMax, localCenter.y);
		if (normalizedY < 0.42f)
		{
			SetRect(panelRect, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.68f));
		}
		else
		{
			SetRect(panelRect, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.22f));
		}
	}

	private void MoveAdventureTutorialSpotlight(RectTransform target)
	{
		if ((Object)(object)adventureScriptedTutorialSpotlight == (Object)null)
		{
			return;
		}
		GameObject spotlightObject = ((Component)adventureScriptedTutorialSpotlight).gameObject;
		if ((Object)(object)target == (Object)null || !((Component)target).gameObject.activeInHierarchy)
		{
			spotlightObject.SetActive(false);
			SetAdventureTutorialDimmers(null);
			return;
		}
		spotlightObject.SetActive(true);
		RectTransform spotlightRect = adventureScriptedTutorialSpotlight.rectTransform;
		spotlightRect.SetAsLastSibling();
		Vector3[] corners = new Vector3[4];
		target.GetWorldCorners(corners);
		Vector3 center = (corners[0] + corners[2]) * 0.5f;
		spotlightRect.position = center;
		float width = Vector3.Distance(corners[0], corners[3]) * 1.18f;
		float height = Vector3.Distance(corners[0], corners[1]) * 1.18f;
		spotlightRect.sizeDelta = new Vector2(width, height);
		SetAdventureTutorialDimmers(new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height));
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
	}

	private void SetAdventureTutorialDimmers(Rect? worldHole)
	{
		CreateAdventureTutorialDimmers();
		if (!worldHole.HasValue || (Object)(object)safeAreaRoot == (Object)null)
		{
			foreach (Image dimmer in adventureScriptedTutorialDimmers)
			{
				if ((Object)(object)dimmer != (Object)null)
				{
					((Component)dimmer).gameObject.SetActive(false);
				}
			}
			return;
		}
		Rect hole = worldHole.Value;
		Vector3[] safeCorners = new Vector3[4];
		safeAreaRoot.GetWorldCorners(safeCorners);
		Rect safe = new Rect(
			safeCorners[0].x,
			safeCorners[0].y,
			safeCorners[2].x - safeCorners[0].x,
			safeCorners[2].y - safeCorners[0].y);
		hole.xMin = Mathf.Clamp(hole.xMin, safe.xMin, safe.xMax);
		hole.xMax = Mathf.Clamp(hole.xMax, safe.xMin, safe.xMax);
		hole.yMin = Mathf.Clamp(hole.yMin, safe.yMin, safe.yMax);
		hole.yMax = Mathf.Clamp(hole.yMax, safe.yMin, safe.yMax);
		SetWorldDimmer(adventureScriptedTutorialDimmers[0], new Rect(safe.xMin, hole.yMax, safe.width, safe.yMax - hole.yMax));
		SetWorldDimmer(adventureScriptedTutorialDimmers[1], new Rect(safe.xMin, safe.yMin, safe.width, hole.yMin - safe.yMin));
		SetWorldDimmer(adventureScriptedTutorialDimmers[2], new Rect(safe.xMin, hole.yMin, hole.xMin - safe.xMin, hole.height));
		SetWorldDimmer(adventureScriptedTutorialDimmers[3], new Rect(hole.xMax, hole.yMin, safe.xMax - hole.xMax, hole.height));
	}

	private void SetWorldDimmer(Image dimmer, Rect worldRect)
	{
		if ((Object)(object)dimmer == (Object)null)
		{
			return;
		}
		GameObject dimmerObject = ((Component)dimmer).gameObject;
		if (worldRect.width <= 1f || worldRect.height <= 1f)
		{
			dimmerObject.SetActive(false);
			return;
		}
		dimmerObject.SetActive(true);
		RectTransform rect = dimmer.rectTransform;
		rect.position = new Vector3(worldRect.center.x, worldRect.center.y, 0f);
		rect.sizeDelta = new Vector2(worldRect.width, worldRect.height);
		rect.SetAsLastSibling();
	}

	private void EndAdventureScriptedTutorial(bool complete)
	{
		adventureScriptedTutorialActive = false;
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			adventureScriptedTutorialPanel.SetActive(false);
		}
		if ((Object)(object)adventureScriptedTutorialSpotlight != (Object)null)
		{
			((Component)adventureScriptedTutorialSpotlight).gameObject.SetActive(false);
		}
		SetAdventureTutorialDimmers(null);
		if (complete)
		{
			ConfirmStartTutorialAdventureStage();
		}
	}
}
}
