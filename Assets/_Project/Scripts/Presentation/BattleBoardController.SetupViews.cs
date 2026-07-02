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
	private const float ImplementationArchiveCardSize = 104f;

	private void CreateImplementationArchiveView(Transform canvasTransform, Font font)
	{
		implementationArchiveButton = CreateImageButton("Implementation Archive Button", (Transform)(object)safeAreaRoot, font, LoadSpriteResource("UI/implementation_archive_button"), string.Empty);
		((UnityEvent)implementationArchiveButton.onClick).AddListener(new UnityAction(ToggleImplementationArchive));
		implementationArchiveButtonRect = (RectTransform)((Component)implementationArchiveButton).transform;
		Shadow shadow = ((Component)implementationArchiveButton).gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
		shadow.effectDistance = new Vector2(0f, -8f);
		Canvas obj = ((Component)implementationArchiveButton).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 630;
		((Component)implementationArchiveButton).gameObject.AddComponent<GraphicRaycaster>();
		Image image = CreateImage("Implementation Archive Panel", canvasTransform, new Color(0.012f, 0.018f, 0.026f, 0.97f));
		image.raycastTarget = true;
		StylePanel(image);
		implementationArchivePanel = ((Component)image).gameObject;
		implementationArchivePanelRect = image.rectTransform;
		Canvas obj2 = implementationArchivePanel.AddComponent<Canvas>();
		obj2.overrideSorting = true;
		obj2.sortingOrder = 640;
		implementationArchivePanel.AddComponent<GraphicRaycaster>();
		Button button = CreateImageButton("Close Implementation Archive", ((Component)image).transform, font, cancelActionSprite, string.Empty);
		((UnityEvent)button.onClick).AddListener(new UnityAction(CloseImplementationArchive));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.86f, 0.895f), new Vector2(0.96f, 0.965f));
		CreateImplementationZoneSection(((Component)image).transform, font, "Consumabili", new Vector2(0.05f, 0.745f), new Vector2(0.95f, 0.88f), out var _, out var _, string.Empty);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE NEL MAZZO", new Vector2(0.05f, 0.505f), new Vector2(0.95f, 0.725f), out implementationDeckRoot, out implementationDeckEmptyText);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE IN COOLDOWN", new Vector2(0.05f, 0.285f), new Vector2(0.95f, 0.485f), out implementationCooldownRoot, out implementationCooldownEmptyText);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE AL CIMITERO", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.265f), out implementationGraveyardRoot, out implementationGraveyardEmptyText);
		implementationArchivePanel.SetActive(false);
	}

	private void CreateImplementationZoneSection(Transform parent, Font font, string title, Vector2 minimum, Vector2 maximum, out RectTransform cardRoot, out Text emptyText, string emptyLabel = "Nessuna carta")
	{
		Image image = CreateImage(title + " Section", parent, new Color(0.02f, 0.04f, 0.052f, 0.72f));
		StylePanel(image);
		SetRect(image.rectTransform, minimum, maximum);
		Text text = CreateText(title, ((Component)image).transform, font, 17, (FontStyle)1, (TextAnchor)3);
		text.text = title;
		text.color = new Color(0.95f, 0.79f, 0.34f);
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		SetRect(text.rectTransform, new Vector2(0.035f, 0.81f), new Vector2(0.965f, 0.965f));
		cardRoot = new GameObject(title + " Cards", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)cardRoot).SetParent(((Component)image).transform, false);
		((Component)cardRoot).gameObject.AddComponent<RectMask2D>();
		SetRect(cardRoot, new Vector2(0.035f, 0.065f), new Vector2(0.965f, 0.765f));
		GridLayoutGroup component = ((Component)cardRoot).GetComponent<GridLayoutGroup>();
		component.cellSize = new Vector2(ImplementationArchiveCardSize, ImplementationArchiveCardSize);
		component.spacing = new Vector2(12f, 8f);
		component.childAlignment = (TextAnchor)3;
		component.constraint = GridLayoutGroup.Constraint.FixedRowCount;
		component.constraintCount = 2;
		emptyText = CreateText(title + " Empty", ((Component)image).transform, font, 16, (FontStyle)2, (TextAnchor)4);
		emptyText.text = emptyLabel;
		emptyText.color = new Color(0.68f, 0.76f, 0.78f);
		SetRect(emptyText.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.72f));
	}

	private void ToggleImplementationArchive()
	{
		if (!((Object)(object)implementationArchivePanel == (Object)null))
		{
			bool flag = !implementationArchivePanel.activeSelf;
			implementationArchivePanel.SetActive(flag);
			if (flag)
			{
				PlayOpenBagSfx();
				RefreshImplementationArchive();
			}
			else
			{
				PlayClosedBagSfx();
			}
		}
	}

	private void CloseImplementationArchive()
	{
		if ((Object)(object)implementationArchivePanel == (Object)null || !implementationArchivePanel.activeSelf)
		{
			return;
		}
		implementationArchivePanel.SetActive(false);
		PlayClosedBagSfx();
	}

	private void RefreshImplementationArchive()
	{
		ClearImplementationArchiveCards();
		PopulateImplementationZone(implementationDeckRoot, implementationDeckEmptyText, CampaignCardZone.Deck);
		PopulateImplementationZone(implementationCooldownRoot, implementationCooldownEmptyText, CampaignCardZone.Cooldown);
		PopulateImplementationZone(implementationGraveyardRoot, implementationGraveyardEmptyText, CampaignCardZone.Graveyard);
	}

	private void PopulateImplementationZone(RectTransform root, Text emptyText, CampaignCardZone zone)
	{
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		List<CampaignCardInstance> list = ((campaignDeck != null) ?campaignDeck.Cards.Where((CampaignCardInstance card) => card.Zone == zone).ToList() : new List<CampaignCardInstance>());
		if ((Object)(object)emptyText != (Object)null)
		{
			((Component)emptyText).gameObject.SetActive(list.Count == 0);
		}
		foreach (CampaignCardInstance item in list)
		{
			CardDefinition definition = item.Definition;
			PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)root, definition, configuration);
			prototypeCardView.SetInteractable((Object)(object)definition != (Object)null);
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				ShowCardInspection(definition);
			});
			LayoutElement component = ((Component)prototypeCardView).GetComponent<LayoutElement>();
			if ((Object)(object)component != (Object)null)
			{
				component.minWidth = ImplementationArchiveCardSize;
				component.preferredWidth = ImplementationArchiveCardSize;
				component.minHeight = ImplementationArchiveCardSize;
				component.preferredHeight = ImplementationArchiveCardSize;
				component.flexibleWidth = 0f;
				component.flexibleHeight = 0f;
			}
			implementationArchiveCardViews.Add(prototypeCardView);
		}
	}

	private void ClearImplementationArchiveCards()
	{
		for (int num = implementationArchiveCardViews.Count - 1; num >= 0; num--)
		{
			PrototypeCardView prototypeCardView = implementationArchiveCardViews[num];
			if ((Object)(object)prototypeCardView != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)prototypeCardView).gameObject);
			}
		}
		implementationArchiveCardViews.Clear();
	}

	private void CreateRoomChoiceView(Transform canvasTransform, Font font)
	{
		Image image = CreateImage("Room Choice", canvasTransform, Color.white);
		image.raycastTarget = true;
		image.preserveAspect = true;
		Stretch(image.rectTransform);
		roomChoiceImage = image;
		roomChoiceAspectFitter = ConfigureFittedBackground(image, null, 2f / 3f);
		roomChoicePanel = ((Component)image).gameObject;
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 360;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Text text = CreateText("Heading", ((Component)image).transform, font, 40, (FontStyle)1, (TextAnchor)4);
		text.text = "SCEGLI LA VIA";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(text.rectTransform, new Vector2(0.12f, 0.89f), new Vector2(0.88f, 0.965f));
		Text text2 = CreateText("Hint", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
		text2.text = "Tre porte. Un solo destino.";
		text2.color = new Color(0.84f, 0.9f, 0.92f);
		SetRect(text2.rectTransform, new Vector2(0.12f, 0.835f), new Vector2(0.88f, 0.89f));
		Button button = CreateTransparentButton("Left Door", ((Component)image).transform);
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			ChooseCampaignDoor(0);
		});
		roomChoiceLeftButton = button;
		Button button2 = CreateTransparentButton("Center Door", ((Component)image).transform);
		((UnityEvent)button2.onClick).AddListener((UnityAction)delegate
		{
			ChooseCampaignDoor(1);
		});
		roomChoiceCenterButton = button2;
		Button button3 = CreateTransparentButton("Right Door", ((Component)image).transform);
		((UnityEvent)button3.onClick).AddListener((UnityAction)delegate
		{
			ChooseCampaignDoor(2);
		});
		roomChoiceRightButton = button3;
		RefreshRoomChoiceLayout();
		roomChoicePanel.SetActive(false);
	}

	private void RefreshRoomChoiceLayout()
	{
		if ((Object)(object)roomChoiceImage == (Object)null)
		{
			return;
		}
		bool landscape = Screen.width > Screen.height;
		Sprite sprite = LoadSpriteResource(landscape ?"UI/background_choose_room_1_landscape" : "UI/background_choose_room_1");
		roomChoiceImage.sprite = sprite;
		if ((Object)(object)roomChoiceAspectFitter != (Object)null)
		{
			roomChoiceAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			roomChoiceAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : (landscape ?1672f / 941f : 941f / 1672f);
		}
		if ((Object)(object)roomChoiceLeftButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)roomChoiceLeftButton).transform,
				landscape ?new Vector2(0.08f, 0.30f) : new Vector2(0.02f, 0.12f),
				landscape ?new Vector2(0.32f, 0.84f) : new Vector2(0.32f, 0.83f));
		}
		if ((Object)(object)roomChoiceCenterButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)roomChoiceCenterButton).transform,
				landscape ?new Vector2(0.38f, 0.32f) : new Vector2(0.35f, 0.14f),
				landscape ?new Vector2(0.62f, 0.86f) : new Vector2(0.65f, 0.84f));
		}
		if ((Object)(object)roomChoiceRightButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)roomChoiceRightButton).transform,
				landscape ?new Vector2(0.68f, 0.30f) : new Vector2(0.68f, 0.12f),
				landscape ?new Vector2(0.92f, 0.84f) : new Vector2(0.98f, 0.83f));
		}
	}

	private void CreateCardInspectionOverlay(Transform canvasTransform, Font font)
	{
		Image image = CreateImage("Card Inspection Overlay", canvasTransform, new Color(0f, 0f, 0f, 0.72f));
		image.raycastTarget = true;
		Stretch(image.rectTransform);
		cardInspectionPanel = ((Component)image).gameObject;
		Button button = cardInspectionPanel.AddComponent<Button>();
		button.targetGraphic = image;
		((UnityEvent)button.onClick).AddListener(new UnityAction(CloseCardInspection));
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = Color.white;
		colors.pressedColor = Color.white;
		colors.disabledColor = Color.white;
		colors.colorMultiplier = 1f;
		button.colors = colors;
		Canvas obj = cardInspectionPanel.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 700;
		cardInspectionPanel.AddComponent<GraphicRaycaster>();
		cardInspectionBookRoot = new GameObject("Inspection Book Root", new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		((Transform)cardInspectionBookRoot).SetParent(cardInspectionPanel.transform, false);
		SetRect(cardInspectionBookRoot, new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.945f));
		cardInspectionBookAspectFitter = ((Component)cardInspectionBookRoot).gameObject.AddComponent<AspectRatioFitter>();
		cardInspectionBookAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		cardInspectionBookAspectFitter.aspectRatio = 0.562799f;
		Image image2 = CreateImage("Inspection Book", (Transform)(object)cardInspectionBookRoot, Color.white);
		cardInspectionBookImage = image2;
		image2.raycastTarget = false;
		Stretch(image2.rectTransform);
		cardInspectionSlot = new GameObject("Inspection Card Slot", new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		((Transform)cardInspectionSlot).SetParent((Transform)(object)cardInspectionBookRoot, false);
		SetRect(cardInspectionSlot, new Vector2(0.235f, 0.57f), new Vector2(0.765f, 0.885f));
		cardInspectionSummaryText = CreateText("Inspection Summary", (Transform)(object)cardInspectionBookRoot, font, 22, (FontStyle)1, (TextAnchor)0);
		cardInspectionSummaryText.color = new Color(0.16f, 0.085f, 0.025f);
		cardInspectionSummaryText.horizontalOverflow = (HorizontalWrapMode)0;
		cardInspectionSummaryText.verticalOverflow = (VerticalWrapMode)0;
		cardInspectionSummaryText.resizeTextForBestFit = true;
		cardInspectionSummaryText.resizeTextMinSize = 12;
		cardInspectionSummaryText.resizeTextMaxSize = 22;
		SetRect(cardInspectionSummaryText.rectTransform, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.54f));
		cardInspectionStatusRoot = new GameObject("Inspection Status Rows", new Type[2]
		{
			typeof(RectTransform),
			typeof(VerticalLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)cardInspectionStatusRoot).SetParent((Transform)(object)cardInspectionBookRoot, false);
		VerticalLayoutGroup component = ((Component)cardInspectionStatusRoot).GetComponent<VerticalLayoutGroup>();
		component.spacing = 8f;
		component.padding = new RectOffset(0, 0, 0, 0);
		component.childControlWidth = true;
		component.childControlHeight = true;
		component.childForceExpandWidth = true;
		component.childForceExpandHeight = false;
		SetRect(cardInspectionStatusRoot, new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.31f));
		cardInspectionCloseButton = CreateImageButton("Close Card Inspection", (Transform)(object)cardInspectionBookRoot, font, cancelActionSprite, string.Empty);
		((UnityEvent)cardInspectionCloseButton.onClick).AddListener(new UnityAction(CloseCardInspection));
		SetRect((RectTransform)((Component)cardInspectionCloseButton).transform, new Vector2(0.82f, 0.865f), new Vector2(0.91f, 0.92f));
		((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
		RefreshCardInspectionLayout();
		cardInspectionPanel.SetActive(false);
	}

	private void RefreshCardInspectionLayout()
	{
		if ((Object)(object)cardInspectionBookRoot == (Object)null)
		{
			return;
		}
		bool landscape = Screen.width > Screen.height;
		Sprite sprite = LoadSpriteResource(landscape ?"UI/card_inspection_landscape" : "UI/card_inspection");
		if ((Object)(object)cardInspectionBookImage != (Object)null)
		{
			cardInspectionBookImage.sprite = sprite;
			cardInspectionBookImage.preserveAspect = true;
		}
		if ((Object)(object)cardInspectionBookAspectFitter != (Object)null)
		{
			cardInspectionBookAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : (landscape ?1.4992679f : 0.562799f);
		}
		SetRect(cardInspectionBookRoot, landscape ?new Vector2(0.035f, 0.055f) : new Vector2(0.04f, 0.035f), landscape ?new Vector2(0.965f, 0.945f) : new Vector2(0.96f, 0.965f));
		if ((Object)(object)cardInspectionSlot != (Object)null)
		{
			SetRect(cardInspectionSlot, landscape ?new Vector2(0.115f, 0.12f) : new Vector2(0.235f, 0.57f), landscape ?new Vector2(0.485f, 0.9f) : new Vector2(0.765f, 0.885f));
		}
		if ((Object)(object)cardInspectionSummaryText != (Object)null)
		{
			cardInspectionSummaryText.resizeTextMinSize = landscape ?15 : 12;
			cardInspectionSummaryText.resizeTextMaxSize = landscape ?24 : 22;
			SetRect(cardInspectionSummaryText.rectTransform, landscape ?new Vector2(0.51f, 0.55f) : new Vector2(0.18f, 0.34f), landscape ?new Vector2(0.89f, 0.82f) : new Vector2(0.82f, 0.54f));
		}
		if ((Object)(object)cardInspectionStatusRoot != (Object)null)
		{
			SetRect(cardInspectionStatusRoot, landscape ?new Vector2(0.515f, 0.19f) : new Vector2(0.16f, 0.12f), landscape ?new Vector2(0.905f, 0.505f) : new Vector2(0.84f, 0.31f));
		}
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
			SetRect((RectTransform)((Component)cardInspectionCloseButton).transform, landscape ?new Vector2(0.94f, 0.845f) : new Vector2(0.82f, 0.865f), landscape ?new Vector2(0.985f, 0.92f) : new Vector2(0.91f, 0.92f));
			((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
		}
	}

	private void CreateRoomTransitionOverlay(Transform canvasTransform)
	{
		Image image = CreateImage("Room Fade", canvasTransform, Color.black);
		image.raycastTarget = true;
		Stretch(image.rectTransform);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 1000;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		((Component)image).gameObject.AddComponent<CanvasGroup>();
		roomTransition = ((Component)image).gameObject.AddComponent<ScreenFadeTransition>();
	}

	private void CreateModeSelectionView(Transform canvasTransform, Font font)
	{
		Image image = CreateImage("Mode Selection", canvasTransform, Color.white);
		image.raycastTarget = true;
		image.preserveAspect = true;
		Stretch(image.rectTransform);
		modeSelectionImage = image;
		modeSelectionAspectFitter = ConfigureFittedBackground(image, null, 0.5714286f);
		modeSelectionPanel = ((Component)image).gameObject;
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 900;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Button button = CreateTransparentButton("Campaign Mode", ((Component)image).transform);
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartCampaignMode();
		});
		modeSelectionCampaignButton = button;
		Button button2 = CreateTransparentButton("Multiplayer Mode", ((Component)image).transform);
		((UnityEvent)button2.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowMultiplayerPopup();
		});
		modeSelectionMultiplayerButton = button2;
		Button button3 = CreateTransparentButton("Tutorial Mode", ((Component)image).transform);
		((UnityEvent)button3.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartTutorial();
		});
		modeSelectionTutorialButton = button3;
		Button button4 = CreateTransparentButton("Tutorial Advance", ((Component)image).transform);
		((UnityEvent)button4.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			AdvanceTutorial();
		});
		Stretch((RectTransform)((Component)button4).transform);
		tutorialAdvanceButton = button4;
		((Component)tutorialAdvanceButton).gameObject.SetActive(false);
		RefreshModeSelectionLayout();
		CreateMultiplayerPopup(((Component)image).transform, font);
		modeSelectionPanel.SetActive(false);
	}

	private void RefreshModeSelectionLayout()
	{
		if ((Object)(object)modeSelectionImage == (Object)null)
		{
			return;
		}
		if (modeSelectionTutorialActive)
		{
			ShowTutorialPage();
			return;
		}
		bool landscape = Screen.width > Screen.height;
		Sprite sprite = LoadSpriteResource(landscape ?"UI/selection_mode_screen_landscape" : "UI/selection_mode_screen");
		modeSelectionImage.sprite = sprite;
		if ((Object)(object)modeSelectionAspectFitter != (Object)null)
		{
			modeSelectionAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			modeSelectionAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : (landscape ?1672f / 941f : 941f / 1672f);
		}
		if ((Object)(object)modeSelectionCampaignButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)modeSelectionCampaignButton).transform,
				landscape ?new Vector2(0.243f, 0.146f) : new Vector2(0.239f, 0.348f),
				landscape ?new Vector2(0.478f, 0.261f) : new Vector2(0.801f, 0.413f));
		}
		if ((Object)(object)modeSelectionMultiplayerButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)modeSelectionMultiplayerButton).transform,
				landscape ?new Vector2(0.52f, 0.147f) : new Vector2(0.222f, 0.265f),
				landscape ?new Vector2(0.749f, 0.26f) : new Vector2(0.819f, 0.328f));
		}
		if ((Object)(object)modeSelectionTutorialButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)modeSelectionTutorialButton).transform,
				landscape ?new Vector2(0.365f, 0.017f) : new Vector2(0.222f, 0.166f),
				landscape ?new Vector2(0.627f, 0.132f) : new Vector2(0.821f, 0.231f));
		}
	}

	private void StartTutorial()
	{
		StartTutorial(true);
	}

	private void StartTutorialFromOptions()
	{
		if ((Object)(object)optionsPanel != (Object)null)
		{
			optionsPanel.SetActive(false);
		}
		StartTutorial(false);
	}

	private void StartTutorial(bool returnToModeSelection)
	{
		modeSelectionTutorialActive = true;
		tutorialReturnToModeSelection = returnToModeSelection;
		tutorialPreviousInputLocked = inputLocked;
		inputLocked = true;
		tutorialPageIndex = 0;
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(true);
		}
		if ((Object)(object)multiplayerPopup != (Object)null)
		{
			multiplayerPopup.SetActive(false);
		}
		SetModeSelectionButtonsActive(false);
		if ((Object)(object)tutorialAdvanceButton != (Object)null)
		{
			((Component)tutorialAdvanceButton).gameObject.SetActive(true);
		}
		ShowTutorialPage();
	}

	private void AdvanceTutorial()
	{
		tutorialPageIndex++;
		if (tutorialPageIndex >= 4)
		{
			StopTutorial();
			return;
		}
		ShowTutorialPage();
	}

	private void StopTutorial()
	{
		modeSelectionTutorialActive = false;
		tutorialPageIndex = 0;
		if ((Object)(object)tutorialAdvanceButton != (Object)null)
		{
			((Component)tutorialAdvanceButton).gameObject.SetActive(false);
		}
		if (tutorialReturnToModeSelection)
		{
			inputLocked = tutorialPreviousInputLocked;
			SetModeSelectionButtonsActive(true);
			RefreshModeSelectionLayout();
		}
		else
		{
			inputLocked = tutorialPreviousInputLocked;
			SetModeSelectionButtonsActive(false);
			if ((Object)(object)modeSelectionPanel != (Object)null)
			{
				modeSelectionPanel.SetActive(false);
			}
		}
	}

	private void ShowTutorialPage()
	{
		if ((Object)(object)modeSelectionImage == (Object)null)
		{
			return;
		}
		int page = Mathf.Clamp(tutorialPageIndex, 0, 3) + 1;
		Sprite sprite = LoadSpriteResource($"UI/tutorial-{page}");
		modeSelectionImage.sprite = sprite;
		if ((Object)(object)modeSelectionAspectFitter != (Object)null)
		{
			modeSelectionAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			modeSelectionAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : 941f / 1672f;
		}
	}

	private void SetModeSelectionButtonsActive(bool active)
	{
		if ((Object)(object)modeSelectionCampaignButton != (Object)null)
		{
			((Component)modeSelectionCampaignButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionMultiplayerButton != (Object)null)
		{
			((Component)modeSelectionMultiplayerButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionTutorialButton != (Object)null)
		{
			((Component)modeSelectionTutorialButton).gameObject.SetActive(active);
		}
	}

	private void CreateMultiplayerPopup(Transform parent, Font font)
	{
		Image image = CreateImage("Multiplayer Popup", parent, new Color(0f, 0f, 0f, 0.58f));
		image.raycastTarget = true;
		Stretch(image.rectTransform);
		multiplayerPopup = ((Component)image).gameObject;
		Image image2 = CreateImage("Dialog", ((Component)image).transform, new Color(0.012f, 0.018f, 0.032f, 0.98f));
		image2.raycastTarget = true;
		StylePanel(image2);
		SetRect(image2.rectTransform, new Vector2(0.13f, 0.39f), new Vector2(0.87f, 0.59f));
		Text text = CreateText("Title", ((Component)image2).transform, font, 35, (FontStyle)1, (TextAnchor)4);
		text.text = "UNDER DEVELOPMENT";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(text.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.86f));
		Button button = CreateButton("Close Multiplayer Popup", ((Component)image2).transform, font, "OK");
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			multiplayerPopup.SetActive(false);
		});
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.34f, 0.12f), new Vector2(0.66f, 0.38f));
		multiplayerPopup.SetActive(false);
	}

	private void ShowModeSelection()
	{
		inputLocked = true;
		modeSelectionTutorialActive = false;
		tutorialPageIndex = 0;
		RefreshModeSelectionLayout();
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(true);
		}
		SetModeSelectionButtonsActive(true);
		if ((Object)(object)tutorialAdvanceButton != (Object)null)
		{
			((Component)tutorialAdvanceButton).gameObject.SetActive(false);
		}
		if ((Object)(object)multiplayerPopup != (Object)null)
		{
			multiplayerPopup.SetActive(false);
		}
		if (debugForceFirstRoomComposableGolem)
		{
			StartCampaignMode();
		}
	}

	private void StartCampaignMode()
	{
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		inputLocked = false;
		LoadBattle();
	}

	private void ShowMultiplayerPopup()
	{
		if ((Object)(object)multiplayerPopup != (Object)null)
		{
			multiplayerPopup.SetActive(true);
		}
	}

	private void CreateDeckBuilderView(Font font)
	{
		Image image = CreateImage("Initial Deck Builder", (Transform)(object)safeAreaRoot, Color.clear);
		image.raycastTarget = true;
		deckBuilderPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 520;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image image2 = CreateImage("Deck Builder Field Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		image2.sprite = null;
		image2.preserveAspect = false;
		SetRect(image2.rectTransform, Vector2.zero, Vector2.one);
		Image image3 = CreateImage("Deck Builder Frame", ((Component)image).transform, Color.white);
		image3.sprite = LoadSpriteResource("UI/build_deck_background_hud");
		image3.color = new Color(1f, 1f, 1f, 0.92f);
		image3.preserveAspect = false;
		image3.raycastTarget = false;
		SetRect(image3.rectTransform, Vector2.zero, Vector2.one);
		Text text = CreateText("Heading", ((Component)image).transform, font, 42, (FontStyle)1, (TextAnchor)4);
		text.text = "FORGIA IL TUO MAZZO";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		deckBuilderHeadingText = text;
		SetRect(text.rectTransform, new Vector2(0.12f, 0.855f), new Vector2(0.88f, 0.94f));
		deckBuilderStatusText = CreateText("Budget", ((Component)image).transform, font, 25, (FontStyle)1, (TextAnchor)4);
		SetRect(deckBuilderStatusText.rectTransform, new Vector2(0.18f, 0.775f), new Vector2(0.82f, 0.84f));
		deckBuilderCardsRoot = new GameObject("Deck Preview Grid", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)deckBuilderCardsRoot).SetParent(((Component)image).transform, false);
		SetRect(deckBuilderCardsRoot, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.745f));
		GridLayoutGroup component = ((Component)deckBuilderCardsRoot).GetComponent<GridLayoutGroup>();
		component.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		component.constraintCount = 3;
		component.spacing = new Vector2(12f, 12f);
		component.childAlignment = (TextAnchor)4;
		component.cellSize = new Vector2(210f, 210f);
		deckBuilderCardsText = CreateText("Empty Deck Hint", ((Component)image).transform, font, 24, (FontStyle)1, (TextAnchor)4);
		deckBuilderCardsText.color = new Color(0.88f, 0.92f, 0.96f);
		SetRect(deckBuilderCardsText.rectTransform, new Vector2(0.24f, 0.55f), new Vector2(0.76f, 0.66f));
		Button button = CreateImageButton("Buy Blind Random", ((Component)image).transform, font, LoadSpriteResource("UI/random_value_draw"), string.Empty);
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			BuyInitialDeckCard(DeckPurchaseMode.BlindRandom);
		});
		deckBuilderRandomButtonRect = (RectTransform)((Component)button).transform;
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.12f, 0.205f), new Vector2(0.34f, 0.345f));
		deckBuilderRandomBuyText = CreateText("Deck Random Cost", ((Component)image).transform, font, 34, (FontStyle)1, (TextAnchor)4);
		ConfigureDeckBuilderCostText(deckBuilderRandomBuyText);
		SetRect(deckBuilderRandomBuyText.rectTransform, new Vector2(0.19f, 0.18f), new Vector2(0.27f, 0.245f));
		Button button2 = CreateImageButton("Buy Selected Class", ((Component)image).transform, font, LoadSpriteResource(DeckBuilderClassResourcePath(deckBuilderSelectedClass)), HeroClassDisplayName(deckBuilderSelectedClass).ToUpperInvariant());
		((UnityEvent)button2.onClick).AddListener((UnityAction)delegate
		{
			BuyInitialDeckCard(DeckPurchaseMode.ChosenClass);
		});
		deckBuilderClassImage = ((Component)button2).GetComponent<Image>();
		deckBuilderClassText = ((Component)button2).GetComponentInChildren<Text>();
		ConfigureDeckBuilderChoiceLabel(deckBuilderClassText);
		deckBuilderClassButtonRect = (RectTransform)((Component)button2).transform;
		SetRect((RectTransform)((Component)button2).transform, new Vector2(0.39f, 0.205f), new Vector2(0.61f, 0.345f));
		Button button3 = CreateImageButton("Deck Class Previous", ((Component)image).transform, font, LoadSpriteResource("UI/left_arrow"), string.Empty);
		((UnityEvent)button3.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderClass(-1);
		});
		deckBuilderClassPreviousButtonRect = (RectTransform)((Component)button3).transform;
		SetRect((RectTransform)((Component)button3).transform, new Vector2(0.355f, 0.18f), new Vector2(0.455f, 0.245f));
		deckBuilderClassBuyText = CreateText("Deck Class Cost", ((Component)image).transform, font, 34, (FontStyle)1, (TextAnchor)4);
		ConfigureDeckBuilderCostText(deckBuilderClassBuyText);
		SetRect(deckBuilderClassBuyText.rectTransform, new Vector2(0.46f, 0.18f), new Vector2(0.54f, 0.245f));
		Button button4 = CreateImageButton("Deck Class Next", ((Component)image).transform, font, LoadSpriteResource("UI/right_arrow"), string.Empty);
		((UnityEvent)button4.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderClass(1);
		});
		deckBuilderClassNextButtonRect = (RectTransform)((Component)button4).transform;
		SetRect((RectTransform)((Component)button4).transform, new Vector2(0.545f, 0.18f), new Vector2(0.645f, 0.245f));
		Button button5 = CreateImageButton("Buy Selected Strength", ((Component)image).transform, font, LoadSpriteResource(DeckBuilderStrengthResourcePath(deckBuilderSelectedStrength)), string.Empty);
		((UnityEvent)button5.onClick).AddListener((UnityAction)delegate
		{
			BuyInitialDeckCard(DeckPurchaseMode.ChosenStrength);
		});
		deckBuilderStrengthImage = ((Component)button5).GetComponent<Image>();
		deckBuilderStrengthButtonRect = (RectTransform)((Component)button5).transform;
		SetRect((RectTransform)((Component)button5).transform, new Vector2(0.66f, 0.205f), new Vector2(0.88f, 0.345f));
		Button button6 = CreateImageButton("Deck Strength Previous", ((Component)image).transform, font, LoadSpriteResource("UI/left_arrow"), string.Empty);
		((UnityEvent)button6.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderStrength(-1);
		});
		deckBuilderStrengthPreviousButtonRect = (RectTransform)((Component)button6).transform;
		SetRect((RectTransform)((Component)button6).transform, new Vector2(0.625f, 0.18f), new Vector2(0.725f, 0.245f));
		deckBuilderStrengthBuyText = CreateText("Deck Strength Cost", ((Component)image).transform, font, 34, (FontStyle)1, (TextAnchor)4);
		ConfigureDeckBuilderCostText(deckBuilderStrengthBuyText);
		SetRect(deckBuilderStrengthBuyText.rectTransform, new Vector2(0.73f, 0.18f), new Vector2(0.81f, 0.245f));
		Button button7 = CreateImageButton("Deck Strength Next", ((Component)image).transform, font, LoadSpriteResource("UI/right_arrow"), string.Empty);
		((UnityEvent)button7.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderStrength(1);
		});
		deckBuilderStrengthNextButtonRect = (RectTransform)((Component)button7).transform;
		SetRect((RectTransform)((Component)button7).transform, new Vector2(0.815f, 0.18f), new Vector2(0.915f, 0.245f));
		Image image4 = CreateImage("Deck Builder Toast", ((Component)image).transform, new Color(0.58f, 0.03f, 0.02f, 0.94f));
		image4.sprite = GetRuntimePanelSprite();
		image4.type = (Image.Type)1;
		image4.raycastTarget = false;
		deckBuilderToastRoot = ((Component)image4).gameObject;
		deckBuilderToastRect = image4.rectTransform;
		SetRect(image4.rectTransform, new Vector2(0.19f, 0.36f), new Vector2(0.81f, 0.45f));
		deckBuilderToastText = CreateText("Deck Builder Toast Text", ((Component)image4).transform, font, 24, (FontStyle)1, (TextAnchor)4);
		deckBuilderToastText.color = Color.white;
		deckBuilderToastText.horizontalOverflow = HorizontalWrapMode.Wrap;
		deckBuilderToastText.verticalOverflow = (VerticalWrapMode)0;
		deckBuilderToastText.raycastTarget = false;
		Stretch(deckBuilderToastText.rectTransform);
		deckBuilderToastRoot.SetActive(false);
		startCampaignHelpAura = CreateImage("help_aura", ((Component)image).transform, Color.white);
		startCampaignHelpAura.sprite = GetHelpAuraSprite();
		startCampaignHelpAura.preserveAspect = true;
		startCampaignHelpAura.raycastTarget = false;
		((Component)startCampaignHelpAura).gameObject.SetActive(false);
		startCampaignHelpAuraRect = startCampaignHelpAura.rectTransform;
		SetRect(startCampaignHelpAura.rectTransform, new Vector2(0.385f, 0.055f), new Vector2(0.615f, 0.185f));
		startCampaignButton = CreateImageButton("Start Campaign", ((Component)image).transform, font, LoadSpriteResource("UI/start_game"), string.Empty);
		((UnityEvent)startCampaignButton.onClick).AddListener(new UnityAction(StartBuiltCampaign));
		startCampaignButtonRect = (RectTransform)((Component)startCampaignButton).transform;
		SetRect((RectTransform)((Component)startCampaignButton).transform, new Vector2(0.415f, 0.065f), new Vector2(0.585f, 0.17f));
		deckBuilderPanel.SetActive(false);
	}

	private static void ConfigureDeckBuilderChoiceLabel(Text text)
	{
		if ((Object)(object)text == (Object)null)
			return;

		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 8;
		text.resizeTextMaxSize = 22;
		SetRect(text.rectTransform, new Vector2(0.06f, 1.02f), new Vector2(0.94f, 1.26f));
	}

	private static void ConfigureDeckBuilderCostText(Text text)
	{
		if ((Object)(object)text == (Object)null)
		{
			return;
		}
		text.color = Color.white;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = (VerticalWrapMode)1;
		Outline outline = ((Component)text).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
	}

	private void CreateCombatResultView(Font font)
	{
		Image image = CreateImage("Combat Result", (Transform)(object)safeAreaRoot, new Color(0.01f, 0.018f, 0.028f, 0.92f));
		combatResultRoot = ((Component)image).gameObject;
		image.raycastTarget = false;
		SetRect(image.rectTransform, new Vector2(0.2f, 0.37f), new Vector2(0.8f, 0.63f));
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 230;
		combatScoreText = CreateText("Score", ((Component)image).transform, font, 82, (FontStyle)1, (TextAnchor)4);
		combatScoreText.color = Color.white;
		SetRect(combatScoreText.rectTransform, new Vector2(0.03f, 0.34f), new Vector2(0.97f, 0.98f));
		combatOutcomeText = CreateText("Outcome", ((Component)image).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		SetRect(combatOutcomeText.rectTransform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.38f));
		combatResultRoot.SetActive(false);
	}
}
}
