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
	private void BeginInitialDeckBuilding()
	{
		initialDeckBuilder = new InitialDeckBuilder(cardDatabase.Cards, random, configuration.DeckBuilding.ToRules());
		deckBuilderPanel.SetActive(true);
		HideDeckBuilderToast();
		RefreshDeckBuilderView();
		AppendLog($"COSTRUZIONE MAZZO - {configuration.DeckBuilding.StartingEssence} Essenze, " + $"obiettivo {configuration.DeckBuilding.DeckSize} carte.");
	}

	private void BuyInitialDeckCard(DeckPurchaseMode mode)
	{
		if (initialDeckBuilder != null && !initialDeckBuilder.CanStartCampaign)
		{
			if (WouldSpendReservedDeckEssence(mode, out int minimumEssenceNeeded))
			{
				ShowDeckBuilderToast($"Non puoi spendere queste essenze: te ne servono ancora {minimumEssenceNeeded} per completare le {configuration.DeckBuilding.DeckSize} carte.");
				AppendLog("ACQUISTO MAZZO RIFIUTATO - essenze da conservare per completare il mazzo.");
				return;
			}
			CardDefinition purchased;
			bool bought = mode switch
			{
				DeckPurchaseMode.BlindRandom => initialDeckBuilder.TryBuyRandom(out purchased),
				DeckPurchaseMode.ChosenClass => initialDeckBuilder.TryBuyClass(deckBuilderSelectedClass, out purchased),
				DeckPurchaseMode.ChosenStrength => initialDeckBuilder.TryBuyStrength(deckBuilderSelectedStrength, out purchased),
				_ => false,
			};
			if (!bought)
			{
				AppendLog("ACQUISTO MAZZO RIFIUTATO - offerta non disponibile o Essenze da preservare.");
			}
			else
			{
				PlayBuyCardSfx();
				HideDeckBuilderToast();
			}
			RefreshDeckBuilderView();
		}
	}

	private bool WouldSpendReservedDeckEssence(DeckPurchaseMode mode, out int minimumEssenceNeeded)
	{
		minimumEssenceNeeded = 0;
		if (initialDeckBuilder == null)
		{
			return false;
		}
		DeckBuildingConfiguration deckBuilding = configuration.DeckBuilding;
		int strength = mode == DeckPurchaseMode.ChosenStrength ?deckBuilderSelectedStrength : 0;
		int cost = deckBuilding.ToRules().CostFor(mode, strength);
		int slotsAfterPurchase = deckBuilding.DeckSize - initialDeckBuilder.Deck.Count - 1;
		minimumEssenceNeeded = Mathf.Max(0, slotsAfterPurchase) * deckBuilding.BlindRandomCost;
		return initialDeckBuilder.EssenceRemaining - cost < minimumEssenceNeeded;
	}

	private void ShowDeckBuilderToast(string message)
	{
		if ((Object)(object)deckBuilderToastRoot == (Object)null || (Object)(object)deckBuilderToastText == (Object)null)
		{
			SetMessage(message);
			return;
		}
		deckBuilderToastText.text = message;
		deckBuilderToastRoot.SetActive(true);
		deckBuilderToastRoot.transform.SetAsLastSibling();
		if (deckBuilderToastRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(deckBuilderToastRoutine);
		}
		deckBuilderToastRoutine = ((MonoBehaviour)this).StartCoroutine(HideDeckBuilderToastAfterDelay());
	}

	private void HideDeckBuilderToast()
	{
		if (deckBuilderToastRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(deckBuilderToastRoutine);
			deckBuilderToastRoutine = null;
		}
		if ((Object)(object)deckBuilderToastRoot != (Object)null)
		{
			deckBuilderToastRoot.SetActive(false);
		}
	}

	private IEnumerator HideDeckBuilderToastAfterDelay()
	{
		yield return WaitForCardInspectionPause(2.2f);
		if ((Object)(object)deckBuilderToastRoot != (Object)null)
		{
			deckBuilderToastRoot.SetActive(false);
		}
		deckBuilderToastRoutine = null;
	}

	private void CycleDeckBuilderClass()
	{
		CycleDeckBuilderClass(1);
	}

	private void CycleDeckBuilderClass(int direction)
	{
		int length = Enum.GetValues(typeof(HeroClass)).Length;
		int next = ((int)deckBuilderSelectedClass + direction) % length;
		if (next < 0)
		{
			next += length;
		}
		deckBuilderSelectedClass = (HeroClass)next;
		PlayArrowChangeSfx();
		RefreshDeckBuilderView();
	}

	private void CycleDeckBuilderStrength()
	{
		CycleDeckBuilderStrength(1);
	}

	private void CycleDeckBuilderStrength(int direction)
	{
		const int minStrength = 2;
		const int maxStrength = 10;
		int range = maxStrength - minStrength + 1;
		int next = deckBuilderSelectedStrength + direction - minStrength;
		next %= range;
		if (next < 0)
		{
			next += range;
		}
		deckBuilderSelectedStrength = next + minStrength;
		PlayArrowChangeSfx();
		RefreshDeckBuilderView();
	}

	private void RefreshDeckBuilderView()
	{
		if (initialDeckBuilder != null)
		{
			RefreshDeckBuilderLayout();
			DeckBuildingConfiguration deckBuilding = configuration.DeckBuilding;
			deckBuilderStatusText.text = $"ESSENZE {initialDeckBuilder.EssenceRemaining}  -  " + $"MAZZO {initialDeckBuilder.Deck.Count}/{deckBuilding.DeckSize}";
			int num = deckBuilding.ChosenStrengthBaseCost + deckBuilderSelectedStrength;
			if ((Object)(object)deckBuilderRandomBuyText != (Object)null)
			{
				deckBuilderRandomBuyText.text = deckBuilding.BlindRandomCost.ToString();
			}
			if ((Object)(object)deckBuilderClassImage != (Object)null)
			{
				deckBuilderClassImage.sprite = LoadSpriteResource(DeckBuilderClassResourcePath(deckBuilderSelectedClass));
			}
			if ((Object)(object)deckBuilderClassText != (Object)null)
			{
				deckBuilderClassText.text = HeroClassDisplayName(deckBuilderSelectedClass).ToUpperInvariant();
			}
			if ((Object)(object)deckBuilderClassBuyText != (Object)null)
			{
				deckBuilderClassBuyText.text = deckBuilding.ChosenClassCost.ToString();
			}
			if ((Object)(object)deckBuilderStrengthImage != (Object)null)
			{
				deckBuilderStrengthImage.sprite = LoadSpriteResource(DeckBuilderStrengthResourcePath(deckBuilderSelectedStrength));
			}
			if ((Object)(object)deckBuilderStrengthBuyText != (Object)null)
			{
				deckBuilderStrengthBuyText.text = num.ToString();
			}
			RefreshDeckBuilderCardPreviews();
			startCampaignButton.interactable = initialDeckBuilder.CanStartCampaign;
			if ((Object)(object)startCampaignHelpAura != (Object)null)
			{
				((Component)startCampaignHelpAura).gameObject.SetActive(initialDeckBuilder.CanStartCampaign);
			}
		}
	}

	private void RefreshDeckBuilderLayout()
	{
		if ((Object)(object)deckBuilderPanel == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
			return;

		Rect safeRect = safeAreaRoot.rect;
		float width = Mathf.Max(1f, safeRect.width);
		float height = Mathf.Max(1f, safeRect.height);
		float aspect = width / height;
		bool compact = IsCompactLayout(aspect, configuration.ResponsiveLayout);
		bool wide = !compact && aspect >= 1.65f;
		RefreshResponsiveDeckBuilderFrame(deckBuilderFrameImage, deckBuilderFrameAspectFitter, compact);

		SetRect(deckBuilderHeadingText.rectTransform,
			compact ? new Vector2(0.08f, 0.862f) : new Vector2(0.12f, 0.852f),
			compact ? new Vector2(0.92f, 0.94f) : new Vector2(0.88f, 0.938f));
		deckBuilderHeadingText.fontSize = compact ? 46 : 42;
		deckBuilderHeadingText.resizeTextMaxSize = deckBuilderHeadingText.fontSize;
		deckBuilderHeadingText.resizeTextMinSize = compact ?34 : 30;

		SetRect(deckBuilderStatusText.rectTransform,
			compact ? new Vector2(0.08f, 0.79f) : new Vector2(0.18f, 0.776f),
			compact ? new Vector2(0.92f, 0.852f) : new Vector2(0.82f, 0.84f));
		deckBuilderStatusText.fontSize = compact ? 31 : 25;
		deckBuilderStatusText.resizeTextMaxSize = deckBuilderStatusText.fontSize;
		deckBuilderStatusText.resizeTextMinSize = compact ?23 : 18;

		SetRect(deckBuilderCardsRoot,
			compact ? new Vector2(0.06f, 0.43f) : new Vector2(wide ? 0.12f : 0.08f, 0.405f),
			compact ? new Vector2(0.94f, 0.79f) : new Vector2(wide ? 0.88f : 0.92f, 0.77f));
		ResizeDeckBuilderCardGrid();
		SetRect(deckBuilderCardsText.rectTransform,
			compact ? new Vector2(0.1f, 0.535f) : new Vector2(0.22f, 0.545f),
			compact ? new Vector2(0.9f, 0.665f) : new Vector2(0.78f, 0.665f));
		deckBuilderCardsText.fontSize = compact ? 28 : 24;
		deckBuilderCardsText.resizeTextMaxSize = deckBuilderCardsText.fontSize;
		deckBuilderCardsText.resizeTextMinSize = compact ?22 : 17;

		Vector2 buttonSize = compact ? new Vector2(0.26f, 0.118f) : new Vector2(wide ? 0.18f : 0.22f, 0.14f);
		float buttonYMin = compact ? 0.245f : 0.255f;
		float buttonYMax = buttonYMin + buttonSize.y;
		float leftCenter = compact ? 0.18f : 0.23f;
		float middleCenter = 0.5f;
		float rightCenter = compact ? 0.82f : 0.77f;
		PlaceDeckBuilderChoice(deckBuilderRandomButtonRect, deckBuilderRandomBuyText, leftCenter, buttonSize.x, buttonYMin, buttonYMax, compact);
		PlaceDeckBuilderChoice(deckBuilderClassButtonRect, deckBuilderClassBuyText, middleCenter, buttonSize.x, buttonYMin, buttonYMax, compact);
		PlaceDeckBuilderChoice(deckBuilderStrengthButtonRect, deckBuilderStrengthBuyText, rightCenter, buttonSize.x, buttonYMin, buttonYMax, compact);
		float classMinX = middleCenter - buttonSize.x * 0.5f;
		float classMaxX = middleCenter + buttonSize.x * 0.5f;
		float strengthMinX = rightCenter - buttonSize.x * 0.5f;
		float strengthMaxX = rightCenter + buttonSize.x * 0.5f;
		if ((Object)(object)deckBuilderClassText != (Object)null)
		{
			deckBuilderClassText.fontSize = compact ? 23 : 22;
			deckBuilderClassText.resizeTextMaxSize = deckBuilderClassText.fontSize;
			deckBuilderClassText.resizeTextMinSize = compact ?17 : 15;
		}

		float arrowWidth = compact ? 0.135f : 0.12f;
		float arrowHeight = compact ? 0.093f : 0.0975f;
		float arrowYMin = Mathf.Max(0.02f, buttonYMin - (compact ? 0.074f : 0.08f));
		float arrowYMax = arrowYMin + arrowHeight;
		PlaceDeckBuilderArrowInside(deckBuilderClassPreviousButtonRect, classMinX, classMaxX, false, arrowWidth, arrowYMin, arrowYMax);
		PlaceDeckBuilderArrowInside(deckBuilderClassNextButtonRect, classMinX, classMaxX, true, arrowWidth, arrowYMin, arrowYMax);
		PlaceDeckBuilderArrowInside(deckBuilderStrengthPreviousButtonRect, strengthMinX, strengthMaxX, false, arrowWidth, arrowYMin, arrowYMax);
		PlaceDeckBuilderArrowInside(deckBuilderStrengthNextButtonRect, strengthMinX, strengthMaxX, true, arrowWidth, arrowYMin, arrowYMax);

		SetRect(deckBuilderToastRect,
			compact ? new Vector2(0.08f, 0.34f) : new Vector2(0.19f, 0.35f),
			compact ? new Vector2(0.92f, 0.45f) : new Vector2(0.81f, 0.45f));
		SetRect(startCampaignHelpAuraRect,
			compact ? new Vector2(0.36f, 0.065f) : new Vector2(0.385f, 0.075f),
			compact ? new Vector2(0.64f, 0.19f) : new Vector2(0.615f, 0.205f));
		SetRect(startCampaignButtonRect,
			compact ? new Vector2(0.39f, 0.075f) : new Vector2(0.415f, 0.085f),
			compact ? new Vector2(0.61f, 0.175f) : new Vector2(0.585f, 0.19f));
	}

	private static void PlaceDeckBuilderChoice(RectTransform button, Text costText, float centerX, float width, float yMin, float yMax, bool compact)
	{
		if ((Object)(object)button == (Object)null)
			return;

		SetRect(button, new Vector2(centerX - width * 0.5f, yMin), new Vector2(centerX + width * 0.5f, yMax));
		if ((Object)(object)costText == (Object)null)
			return;

		float costWidth = compact ? 0.12f : 0.09f;
		float costHeight = compact ? 0.052f : 0.058f;
		float arrowHeight = compact ? 0.093f : 0.0975f;
		float arrowYMin = Mathf.Max(0.02f, yMin - (compact ? 0.074f : 0.08f));
		float costYMin = arrowYMin + (arrowHeight - costHeight) * 0.5f;
		SetRect(costText.rectTransform, new Vector2(centerX - costWidth * 0.5f, costYMin), new Vector2(centerX + costWidth * 0.5f, costYMin + costHeight));
		costText.fontSize = compact ? 32 : 34;
		costText.resizeTextMaxSize = costText.fontSize;
		costText.resizeTextMinSize = compact ?24 : 24;
	}

	private static void PlaceDeckBuilderArrowInside(RectTransform button, float minimumX, float maximumX, bool rightAligned, float width, float yMin, float yMax)
	{
		if ((Object)(object)button == (Object)null)
			return;

		float padding = 0f;
		float minX = rightAligned ? maximumX - width - padding : minimumX + padding;
		SetRect(button, new Vector2(minX, yMin), new Vector2(minX + width, yMax));
	}

	private static string DeckBuilderStrengthResourcePath(int strength)
	{
		int num = Mathf.Clamp(strength, 2, 10);
		return $"UI/{num}_choose_card";
	}

	private static string DeckBuilderClassResourcePath(HeroClass heroClass)
	{
		return "UI/" + heroClass.ToString().ToLowerInvariant() + "_choose_card";
	}

	private void RefreshDeckBuilderCardPreviews()
	{
		DestroyPrototypeViews(deckBuilderCardViews);
		if (initialDeckBuilder == null || (Object)(object)deckBuilderCardsRoot == (Object)null)
		{
			return;
		}
		ResizeDeckBuilderCardGrid();
		bool flag = initialDeckBuilder.Deck.Count > 0;
		((Component)deckBuilderCardsText).gameObject.SetActive(!flag);
		deckBuilderCardsText.text = (flag ?string.Empty : "Il tuo mazzo e' vuoto. Spendi le essenze per una carta casuale, oppure scegli classe o valore e lascia al caso il resto.");
		if (!flag)
		{
			deckBuilderCardsText.text = "Il tuo mazzo e vuoto. Decidi come spendere le essenze: carta casuale, classe scelta con valore casuale, o valore scelto con classe casuale.";
		}
		foreach (CardDefinition card in initialDeckBuilder.Deck)
		{
			PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)deckBuilderCardsRoot, card, configuration);
			prototypeCardView.RaiseStrengthText();
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				ShowCardInspection(card);
			});
			deckBuilderCardViews.Add(prototypeCardView);
		}
	}

	private void ResizeDeckBuilderCardGrid()
	{
		if ((Object)(object)deckBuilderCardsRoot == (Object)null)
			return;

		Canvas.ForceUpdateCanvases();
		GridLayoutGroup component = ((Component)deckBuilderCardsRoot).GetComponent<GridLayoutGroup>();
		if ((Object)(object)component == (Object)null)
			return;

		int rows = Mathf.Max(1, Mathf.CeilToInt((float)configuration.DeckBuilding.DeckSize / 3f));
		Rect rect = deckBuilderCardsRoot.rect;
		float availableWidth = Mathf.Max(1f, rect.width - component.spacing.x * 2f);
		float availableHeight = Mathf.Max(1f, rect.height - component.spacing.y * (float)(rows - 1));
		float cardSize = Mathf.Min(availableWidth / 3f, availableHeight / rows);
		component.cellSize = new Vector2(cardSize, cardSize);
	}

	private void StartBuiltCampaign()
	{
		if (initialDeckBuilder != null && initialDeckBuilder.CanStartCampaign)
		{
			campaignDeck = new CampaignDeckState(initialDeckBuilder.Deck);
			GrantStartingCampaignConsumablesForTesting();
			initialDeckBuilder = null;
			ResetScenarioRuleState();
			deckBuilderPanel.SetActive(false);
			DestroyPrototypeViews(deckBuilderCardViews);
			((Component)campaignZoneRect).gameObject.SetActive(false);
			AppendLog($"CAMPAGNA AVVIATA - {campaignDeck.Cards.Count} carte nel mazzo.");
			PlayTransitionSfx();
			BeginRoomChoice();
		}
	}

	private void ResetRunProgress()
	{
		runProgress = CreateRunProgress();
	}

	private RunProgressState CreateRunProgress()
	{
		ProgressionConfiguration progression = configuration.Progression;
		int startingVigorDieSides = debugForceFirstRoomMedusa ? 12 : configuration.Gameplay.VigorDieSides;
		return new RunProgressState(
			progression.ExperienceThresholdsByLevel,
			progression.MonsterRoomClearExperience,
			progression.MaximumLevel,
			progression.RoomsPerMasterLevel,
			progression.BuildVigorDiceByLevel(startingVigorDieSides));
	}
}
}
