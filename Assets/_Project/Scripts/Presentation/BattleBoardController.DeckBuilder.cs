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
	private static readonly HeroClass[] StarterHeroClasses =
	{
		HeroClass.Mage,
		HeroClass.Warrior,
		HeroClass.Rogue
	};

	private void BeginInitialDeckBuilding()
	{
		EnsureSelectedDeckBuilderClassIsUnlocked();
		initialDeckBuilder = new InitialDeckBuilder(GetUnlockedCombatCards(), random, configuration.DeckBuilding.ToRules());
		deckBuilderPanel.SetActive(true);
		HideDeckBuilderToast();
		RefreshDeckBuilderView();
		AppendLog($"COSTRUZIONE MAZZO - scegli campione, vice campione e completa {configuration.DeckBuilding.DeckSize} carte.");
	}

	private void BuyInitialDeckCard(DeckPurchaseMode mode)
	{
		if (initialDeckBuilder != null && !initialDeckBuilder.CanStartCampaign)
		{
			if (WouldSpendReservedDeckEssence(mode, out int minimumEssenceNeeded))
			{
				ShowDeckBuilderToast($"Completa il mazzo scegliendo ancora {configuration.DeckBuilding.DeckSize - initialDeckBuilder.Deck.Count} carte.");
				AppendLog("DRAFT MAZZO RIFIUTATO - scelta non valida per completare il mazzo.");
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
				AppendLog("DRAFT MAZZO RIFIUTATO - carta non disponibile.");
			}
			else
			{
				PlayBuyCardSfx();
				HideDeckBuilderToast();
			}
			RefreshDeckBuilderView();
		}
	}

	private void BuyInitialDeckClass(HeroClass heroClass)
	{
		if (!IsHeroClassUnlockedForCampaign(heroClass))
		{
			ShowDeckBuilderToast("Classe bloccata: sbloccala con la progressione Avventura.");
			return;
		}
		deckBuilderSelectedClass = heroClass;
		BuyInitialDeckClassDraft(heroClass);
	}

	private void BuyInitialDeckClassDraft(HeroClass heroClass)
	{
		if (initialDeckBuilder == null || initialDeckBuilder.CanStartCampaign)
			return;

		int currentCount = initialDeckBuilder.Deck.Count;
		CardDefinition purchased;
		bool bought = currentCount switch
		{
			0 => initialDeckBuilder.TryDraftExact(heroClass, 10, out purchased),
			1 => initialDeckBuilder.TryDraftExact(heroClass, 9, out purchased),
			_ => initialDeckBuilder.TryDraftClass(heroClass, out purchased)
		};
		if (!bought)
		{
			ShowDeckBuilderToast("Carta non disponibile per questa classe.");
			AppendLog("DRAFT MAZZO RIFIUTATO - carta non disponibile.");
			return;
		}

		PlayBuyCardSfx();
		HideDeckBuilderToast();
		RefreshDeckBuilderView();
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
		deckBuilderSelectedClass = NextUnlockedHeroClass(deckBuilderSelectedClass, direction);
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
			EnsureSelectedDeckBuilderClassIsUnlocked();
			RefreshDeckBuilderLayout();
			DeckBuildingConfiguration deckBuilding = configuration.DeckBuilding;
			deckBuilderStatusText.text = DeckBuilderPromptText(initialDeckBuilder.Deck.Count, deckBuilding.DeckSize);
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
			RefreshDeckBuilderClassOptions();
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
			((Component)startCampaignButton).gameObject.SetActive(initialDeckBuilder.CanStartCampaign);
		}
	}

	private static string DeckBuilderPromptText(int deckCount, int deckSize)
	{
		if (deckCount <= 0)
			return "SCEGLI IL TUO CAMPIONE";
		if (deckCount == 1)
			return "SCEGLI IL VICE CAMPIONE";
		return $"ORA COMPLETA IL MAZZO {deckCount}/{deckSize}";
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
		RefreshScreenOuterFrame(deckBuilderFrameImage, deckBuilderFrameAspectFitter);
		SetRect(
			deckBuilderFrameImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.872f : 0.862f));
		SetRect(
			deckBuilderInnerBackgroundImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.872f : 0.862f));

		SetRect(
			deckBuilderTitlePanel.rectTransform,
			compact ? new Vector2(0.08f, 0.852f) : new Vector2(0.16f, 0.842f),
			compact ? new Vector2(0.92f, 0.952f) : new Vector2(0.84f, 0.952f));
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

		SetRect(deckBuilderClassGridRoot,
			compact ? new Vector2(0.07f, 0.155f) : new Vector2(wide ? 0.28f : 0.22f, 0.125f),
			compact ? new Vector2(0.93f, 0.425f) : new Vector2(wide ? 0.72f : 0.78f, 0.405f));
		ResizeDeckBuilderClassGrid(compact);

		Vector2 buttonSize = compact ? new Vector2(0.26f, 0.105f) : new Vector2(wide ? 0.18f : 0.22f, 0.12f);
		float buttonYMin = compact ? 0.065f : 0.065f;
		float buttonYMax = buttonYMin + buttonSize.y;
		float leftCenter = compact ? 0.18f : 0.23f;
		float rightCenter = compact ? 0.82f : 0.77f;
		PlaceDeckBuilderChoice(deckBuilderRandomButtonRect, deckBuilderRandomBuyText, leftCenter, buttonSize.x, buttonYMin, buttonYMax, compact);
		if ((Object)(object)deckBuilderRandomButtonRect != (Object)null)
		{
			((Component)deckBuilderRandomButtonRect).gameObject.SetActive(false);
		}
		if ((Object)(object)deckBuilderRandomBuyText != (Object)null)
		{
			((Component)deckBuilderRandomBuyText).gameObject.SetActive(false);
		}
		if ((Object)(object)deckBuilderClassButtonRect != (Object)null)
		{
			((Component)deckBuilderClassButtonRect).gameObject.SetActive(false);
		}
		if ((Object)(object)deckBuilderClassBuyText != (Object)null)
		{
			SetRect(deckBuilderClassBuyText.rectTransform, compact ?new Vector2(0.43f, 0.145f) : new Vector2(0.46f, 0.145f), compact ?new Vector2(0.57f, 0.195f) : new Vector2(0.54f, 0.195f));
		}
		PlaceDeckBuilderChoice(deckBuilderStrengthButtonRect, deckBuilderStrengthBuyText, rightCenter, buttonSize.x, buttonYMin, buttonYMax, compact);
		if ((Object)(object)deckBuilderStrengthButtonRect != (Object)null)
		{
			((Component)deckBuilderStrengthButtonRect).gameObject.SetActive(false);
		}
		if ((Object)(object)deckBuilderStrengthBuyText != (Object)null)
		{
			((Component)deckBuilderStrengthBuyText).gameObject.SetActive(false);
		}
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
		if ((Object)(object)deckBuilderClassPreviousButtonRect != (Object)null)
		{
			((Component)deckBuilderClassPreviousButtonRect).gameObject.SetActive(false);
		}
		if ((Object)(object)deckBuilderClassNextButtonRect != (Object)null)
		{
			((Component)deckBuilderClassNextButtonRect).gameObject.SetActive(false);
		}
		PlaceDeckBuilderArrowInside(deckBuilderStrengthPreviousButtonRect, strengthMinX, strengthMaxX, false, arrowWidth, arrowYMin, arrowYMax);
		PlaceDeckBuilderArrowInside(deckBuilderStrengthNextButtonRect, strengthMinX, strengthMaxX, true, arrowWidth, arrowYMin, arrowYMax);
		if ((Object)(object)deckBuilderStrengthPreviousButtonRect != (Object)null)
		{
			((Component)deckBuilderStrengthPreviousButtonRect).gameObject.SetActive(false);
		}
		if ((Object)(object)deckBuilderStrengthNextButtonRect != (Object)null)
		{
			((Component)deckBuilderStrengthNextButtonRect).gameObject.SetActive(false);
		}

		SetRect(deckBuilderToastRect,
			compact ? new Vector2(0.08f, 0.34f) : new Vector2(0.19f, 0.35f),
			compact ? new Vector2(0.92f, 0.45f) : new Vector2(0.81f, 0.45f));
		SetRect(startCampaignButtonRect,
			compact ? new Vector2(0.31f, 0.045f) : new Vector2(0.37f, 0.045f),
			compact ? new Vector2(0.69f, 0.135f) : new Vector2(0.63f, 0.14f));
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

	private void ResizeDeckBuilderClassGrid(bool compact)
	{
		if ((Object)(object)deckBuilderClassGridRoot == (Object)null)
			return;

		Canvas.ForceUpdateCanvases();
		GridLayoutGroup component = ((Component)deckBuilderClassGridRoot).GetComponent<GridLayoutGroup>();
		if ((Object)(object)component == (Object)null)
			return;

		const int columns = 3;
		const int rows = 3;
		Rect rect = deckBuilderClassGridRoot.rect;
		float spacing = compact ? 8f : 10f;
		component.spacing = new Vector2(spacing, spacing);
		float availableWidth = Mathf.Max(1f, rect.width - spacing * (columns - 1));
		float availableHeight = Mathf.Max(1f, rect.height - spacing * (rows - 1));
		float size = Mathf.Min(availableWidth / columns, availableHeight / rows);
		component.cellSize = new Vector2(size, size);
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
		deckBuilderCardsText.text = (flag ?string.Empty : "Scegli una classe: la prima carta sara' il tuo campione di valore 10.");
		if (!flag)
		{
			deckBuilderCardsText.text = "Scegli una classe: la prima carta sara' il tuo campione di valore 10.";
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
			LoadCampaignConsumablesFromBag();
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

	private void RefreshDeckBuilderClassOptions()
	{
		for (int index = 0; index < deckBuilderClassOptionClasses.Count; index++)
		{
			HeroClass heroClass = deckBuilderClassOptionClasses[index];
			bool unlocked = IsHeroClassUnlockedForCampaign(heroClass);
			if (index < deckBuilderClassOptionButtons.Count && (Object)(object)deckBuilderClassOptionButtons[index] != (Object)null)
			{
				deckBuilderClassOptionButtons[index].interactable = unlocked;
			}
			if (index < deckBuilderClassOptionImages.Count && (Object)(object)deckBuilderClassOptionImages[index] != (Object)null)
			{
				Image image = deckBuilderClassOptionImages[index];
				image.sprite = GetClassIconSprite(heroClass, grayscale: !unlocked);
				image.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.42f);
			}
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

	private List<CardDefinition> GetUnlockedCombatCards()
	{
		return cardDatabase.Cards
			.Where(card => card != null
				&& (!card.HasHeroClass || IsHeroClassUnlockedForCampaign(card.HeroClass)))
			.ToList();
	}

	private void EnsureSelectedDeckBuilderClassIsUnlocked()
	{
		if (!IsHeroClassUnlockedForCampaign(deckBuilderSelectedClass))
		{
			deckBuilderSelectedClass = FirstUnlockedHeroClass();
		}
	}

	private HeroClass FirstUnlockedHeroClass()
	{
		foreach (HeroClass heroClass in Enum.GetValues(typeof(HeroClass)))
		{
			if (IsHeroClassUnlockedForCampaign(heroClass))
				return heroClass;
		}
		return HeroClass.Warrior;
	}

	private HeroClass NextUnlockedHeroClass(HeroClass current, int direction)
	{
		Array values = Enum.GetValues(typeof(HeroClass));
		int currentIndex = Math.Max(0, Array.IndexOf(values, current));
		int step = direction >= 0 ? 1 : -1;
		for (int offset = 1; offset <= values.Length; offset++)
		{
			int nextIndex = (currentIndex + step * offset) % values.Length;
			if (nextIndex < 0)
				nextIndex += values.Length;
			HeroClass candidate = (HeroClass)values.GetValue(nextIndex);
			if (IsHeroClassUnlockedForCampaign(candidate))
				return candidate;
		}
		return current;
	}

	private bool IsHeroClassUnlockedForCampaign(HeroClass heroClass)
	{
		if (Array.IndexOf(StarterHeroClasses, heroClass) >= 0 && singlePlayerProgressService.TutorialCompleted)
			return true;

		return singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Class, HeroClassUnlockId(heroClass));
	}

	private static string HeroClassUnlockId(HeroClass heroClass)
	{
		return heroClass.ToString().ToLowerInvariant();
	}
}
}
