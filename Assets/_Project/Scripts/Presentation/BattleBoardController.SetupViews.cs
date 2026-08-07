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
	private const float ImplementationArchiveCardSize = 104f;
	private const int CardInspectionFontSize = 26;
	private const string CampaignPortalBackgroundResource = "UI/CampaignRestyle/campaign_portal_background";
	private const string CampaignAdventureEmblemResource = "UI/CampaignRestyle/adventure_portal_emblem";
	private const string CampaignHardcoreEmblemResource = "UI/CampaignRestyle/hardcore_portal_emblem";
	private const string CampaignHardcoreLockedEmblemResource = "UI/CampaignRestyle/hardcore_portal_emblem_locked";
	private const string CampaignHardcoreCtaResource = "UI/CampaignRestyle/campaign_cta_back_red";
	private const string MultiplayerRankedCtaResource = "UI/MultiplayerRestyle/ranked_cta_frame_v3";
	private static Sprite campaignAdventureCtaSprite;
	private static Sprite campaignHardcoreCtaSprite;
	private RectTransform cardInspectionContentViewport;
	private RectTransform cardInspectionContentRoot;
	private ScrollRect cardInspectionContentScroll;
	private ScrollRect implementationConsumablesScroll;

	private void CreateImplementationArchiveView(Transform canvasTransform, Font font)
	{
		implementationArchiveButton = CreateImageButton("Implementation Archive Button", (Transform)(object)safeAreaRoot, font, LoadSpriteResource("UI/SharedHeader/bag_satchel"), string.Empty);
		((UnityEvent)implementationArchiveButton.onClick).AddListener(new UnityAction(ToggleImplementationArchive));
		implementationArchiveButtonRect = (RectTransform)((Component)implementationArchiveButton).transform;
		SetRect(implementationArchiveButtonRect, new Vector2(0.71f, 0.902f), new Vector2(0.865f, 0.992f));
		Shadow shadow = ((Component)implementationArchiveButton).gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
		shadow.effectDistance = new Vector2(0f, -8f);
		Canvas obj = ((Component)implementationArchiveButton).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 630;
		((Component)implementationArchiveButton).gameObject.AddComponent<GraphicRaycaster>();
		Image backdrop = CreateImage("Implementation Archive Backdrop", canvasTransform, new Color(0f, 0f, 0f, 0.72f));
		backdrop.raycastTarget = true;
		Stretch(backdrop.rectTransform);
		implementationArchiveBackdropPanel = ((Component)backdrop).gameObject;
		Button backdropButton = implementationArchiveBackdropPanel.AddComponent<Button>();
		backdropButton.transition = Selectable.Transition.None;
		((UnityEvent)backdropButton.onClick).AddListener(new UnityAction(CloseImplementationArchive));
		Canvas backdropCanvas = implementationArchiveBackdropPanel.AddComponent<Canvas>();
		backdropCanvas.overrideSorting = true;
		backdropCanvas.sortingOrder = 639;
		implementationArchiveBackdropPanel.AddComponent<GraphicRaycaster>();
		implementationArchiveBackdropPanel.SetActive(false);
		Image image = CreateImage("Implementation Archive Panel", canvasTransform, new Color(0.012f, 0.018f, 0.026f, 0.97f));
		image.raycastTarget = true;
		implementationArchivePanel = ((Component)image).gameObject;
		implementationArchivePanelRect = image.rectTransform;
		Canvas obj2 = implementationArchivePanel.AddComponent<Canvas>();
		obj2.overrideSorting = true;
		obj2.sortingOrder = 640;
		implementationArchivePanel.AddComponent<GraphicRaycaster>();
		Image outerFrame = CreateImage("Implementation Archive Outer Frame", ((Component)image).transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(outerFrame);
		outerFrame.raycastTarget = false;
		SetRect(outerFrame.rectTransform, new Vector2(0.004f, 0.004f), new Vector2(0.996f, 0.996f));
		Button button = CreateImageButton("Close Implementation Archive", ((Component)image).transform, font, cancelActionSprite, string.Empty);
		((UnityEvent)button.onClick).AddListener(new UnityAction(CloseImplementationArchive));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.86f, 0.895f), new Vector2(0.96f, 0.965f));
		Text title = CreateText("Implementation Archive Title", ((Component)image).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		title.text = "BISACCIA";
		title.color = new Color(0.95f, 0.79f, 0.34f);
		title.horizontalOverflow = HorizontalWrapMode.Wrap;
		title.verticalOverflow = VerticalWrapMode.Truncate;
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		SetRect(title.rectTransform, new Vector2(0.14f, 0.9f), new Vector2(0.86f, 0.972f));
		implementationArchiveGoldText = CreateText(
			"Bag Gold Counter",
			((Component)image).transform,
			font,
			34,
			FontStyle.Bold,
			TextAnchor.MiddleCenter);
		implementationArchiveGoldText.color = new Color(1f, 0.82f, 0.28f, 1f);
		implementationArchiveGoldText.raycastTarget = false;
		implementationArchiveGoldText.resizeTextForBestFit = true;
		implementationArchiveGoldText.resizeTextMinSize = 18;
		implementationArchiveGoldText.resizeTextMaxSize = 34;
		implementationArchiveGoldText.rectTransform.anchorMin = new Vector2(0.035f, 0.9f);
		implementationArchiveGoldText.rectTransform.anchorMax = new Vector2(0.25f, 0.975f);
		implementationArchiveGoldText.rectTransform.offsetMin = new Vector2(49f, 0f);
		implementationArchiveGoldText.rectTransform.offsetMax = new Vector2(49f, 0f);
		RefreshBagGoldCounter();
		CreateImplementationZoneSection(((Component)image).transform, font, "CONSUMABILI", new Vector2(0.05f, 0.745f), new Vector2(0.95f, 0.88f), out implementationConsumablesRoot, out implementationConsumablesEmptyText, string.Empty);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE NEL MAZZO", new Vector2(0.05f, 0.505f), new Vector2(0.95f, 0.725f), out implementationDeckRoot, out implementationDeckEmptyText);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE IN COOLDOWN", new Vector2(0.05f, 0.285f), new Vector2(0.95f, 0.485f), out implementationCooldownRoot, out implementationCooldownEmptyText);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE AL CIMITERO", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.265f), out implementationGraveyardRoot, out implementationGraveyardEmptyText);
		SetImplementationArchiveVisible(false);
	}

	private void CreateImplementationZoneSection(Transform parent, Font font, string title, Vector2 minimum, Vector2 maximum, out RectTransform cardRoot, out Text emptyText, string emptyLabel = "Nessuna carta")
	{
		Image image = CreateImage(title + " Section", parent, Color.white);
		image.sprite = LoadSpriteResource("UI/Common/merchant_rock_panel_aaa");
		image.type = Image.Type.Sliced;
		image.pixelsPerUnitMultiplier = 1f;
		image.raycastTarget = true;
		SetRect(image.rectTransform, minimum, maximum);
		Text text = CreateText(title, ((Component)image).transform, font, 17, (FontStyle)1, TextAnchor.MiddleCenter);
		text.text = title;
		text.color = new Color(0.95f, 0.79f, 0.34f);
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(text);
		SetRect(text.rectTransform, new Vector2(0.02f, 0.94f), new Vector2(0.98f, 1.08f));
		bool isConsumablesSection = string.Equals(title, "CONSUMABILI", StringComparison.OrdinalIgnoreCase);
		RectTransform cardsViewport = null;
		if (isConsumablesSection)
		{
			GameObject viewportObject = new GameObject(
				title + " Viewport",
				typeof(RectTransform),
				typeof(Image),
				typeof(RectMask2D),
				typeof(ScrollRect));
			viewportObject.transform.SetParent(((Component)image).transform, false);
			cardsViewport = viewportObject.GetComponent<RectTransform>();
			SetRect(cardsViewport, new Vector2(0.11f, 0.02f), new Vector2(0.94f, 0.71f));
			Image viewportImage = viewportObject.GetComponent<Image>();
			viewportImage.color = Color.clear;
			viewportImage.raycastTarget = true;
			implementationConsumablesScroll = viewportObject.GetComponent<ScrollRect>();
			implementationConsumablesScroll.viewport = cardsViewport;
			implementationConsumablesScroll.horizontal = true;
			implementationConsumablesScroll.vertical = false;
			implementationConsumablesScroll.inertia = true;
			implementationConsumablesScroll.decelerationRate = 0.16f;
			implementationConsumablesScroll.scrollSensitivity = 32f;
			implementationConsumablesScroll.movementType = ScrollRect.MovementType.Clamped;
		}
		cardRoot = new GameObject(title + " Cards", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)cardRoot).SetParent(
			isConsumablesSection ? (Transform)cardsViewport : ((Component)image).transform,
			false);
		if (isConsumablesSection)
		{
			cardRoot.anchorMin = new Vector2(0f, 0f);
			cardRoot.anchorMax = new Vector2(0f, 1f);
			cardRoot.pivot = new Vector2(0f, 0.5f);
			cardRoot.anchoredPosition = Vector2.zero;
			cardRoot.sizeDelta = Vector2.zero;
			ContentSizeFitter fitter = ((Component)cardRoot).gameObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
			implementationConsumablesScroll.content = cardRoot;
		}
		else
		{
			((Component)cardRoot).gameObject.AddComponent<RectMask2D>();
			SetRect(cardRoot, new Vector2(0.11f, 0.1f), new Vector2(0.94f, 0.86f));
		}
		GridLayoutGroup component = ((Component)cardRoot).GetComponent<GridLayoutGroup>();
		component.cellSize = new Vector2(ImplementationArchiveCardSize, ImplementationArchiveCardSize);
		component.spacing = new Vector2(12f, 8f);
		component.childAlignment = TextAnchor.UpperLeft;
		component.constraint = GridLayoutGroup.Constraint.FixedRowCount;
		component.constraintCount = 2;
		if (isConsumablesSection)
		{
			component.constraint = GridLayoutGroup.Constraint.FixedRowCount;
			component.constraintCount = 1;
			component.spacing = new Vector2(10f, 0f);
		}
		emptyText = CreateText(title + " Empty", ((Component)image).transform, font, 16, (FontStyle)2, (TextAnchor)4);
		emptyText.text = emptyLabel;
		emptyText.color = new Color(0.68f, 0.76f, 0.78f);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(emptyText);
		SetRect(emptyText.rectTransform, new Vector2(0.11f, 0.1f), new Vector2(0.94f, 0.86f));
	}

	private void StyleImplementationArchiveTexts()
	{
		if ((Object)(object)implementationArchivePanel == (Object)null)
		{
			return;
		}
		foreach (Text text in implementationArchivePanel.GetComponentsInChildren<Text>(true))
		{
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(text);
		}
	}

	private void ToggleImplementationArchive()
	{
		if (!((Object)(object)implementationArchivePanel == (Object)null))
		{
			bool flag = !implementationArchivePanel.activeSelf;
			SetImplementationArchiveVisible(flag);
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
		SetImplementationArchiveVisible(false);
		PlayClosedBagSfx();
	}

	private void SetImplementationArchiveVisible(bool visible)
	{
		if ((Object)(object)implementationArchiveBackdropPanel != (Object)null)
		{
			implementationArchiveBackdropPanel.SetActive(visible);
			if (visible)
			{
				implementationArchiveBackdropPanel.transform.SetAsLastSibling();
			}
		}
		if ((Object)(object)implementationArchivePanel != (Object)null)
		{
			implementationArchivePanel.SetActive(visible);
			if (visible)
			{
				implementationArchivePanel.transform.SetAsLastSibling();
			}
		}
	}

	private void RefreshImplementationArchive()
	{
		RefreshBagGoldCounter();
		ClearImplementationArchiveCards();
		ClearImplementationConsumables();
		PopulateImplementationConsumables();
		PopulateImplementationZone(implementationDeckRoot, implementationDeckEmptyText, CampaignCardZone.Deck);
		PopulateImplementationZone(implementationCooldownRoot, implementationCooldownEmptyText, CampaignCardZone.Cooldown);
		PopulateImplementationZone(implementationGraveyardRoot, implementationGraveyardEmptyText, CampaignCardZone.Graveyard);
		StyleImplementationArchiveTexts();
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
			prototypeCardView.SetCompactPreviewStrengthReadability(64);
			prototypeCardView.SetInteractable((Object)(object)definition != (Object)null);
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				HandleImplementationCardClicked(item);
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

	private void PopulateImplementationConsumables()
	{
		if ((Object)(object)implementationConsumablesRoot == (Object)null)
		{
			return;
		}
		CampaignConsumableType[] itemTypes = new[]
			{
				CampaignConsumableType.Detector,
				CampaignConsumableType.SecondChance,
				CampaignConsumableType.Defrost,
				CampaignConsumableType.Empower,
				CampaignConsumableType.SigilloRubino,
				CampaignConsumableType.DoubleExp
			}
			.OrderByDescending(itemType => (campaignConsumables?.GetQuantity(itemType) ?? 0) > 0)
			.ToArray();
		if ((Object)(object)implementationConsumablesEmptyText != (Object)null)
		{
			((Component)implementationConsumablesEmptyText).gameObject.SetActive(false);
		}
		foreach (CampaignConsumableType itemType in itemTypes)
		{
			CreateImplementationConsumableView(itemType);
		}
		Canvas.ForceUpdateCanvases();
		if ((Object)(object)implementationConsumablesScroll != (Object)null)
		{
			implementationConsumablesScroll.horizontalNormalizedPosition = 0f;
		}
	}

	private void CreateImplementationConsumableView(CampaignConsumableType itemType)
	{
		int quantity = campaignConsumables?.GetQuantity(itemType) ?? 0;
		GameObject root = new GameObject("Consumable " + itemType, new Type[3]
		{
			typeof(RectTransform),
			typeof(Image),
			typeof(Button)
		});
		root.transform.SetParent((Transform)(object)implementationConsumablesRoot, false);
		Image frame = root.GetComponent<Image>();
		frame.color = quantity > 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.18f, 0.2f, 0.22f, 0.68f);
		frame.sprite = LoadSpriteResource("UI/" + CampaignConsumableResourceName(itemType));
		frame.preserveAspect = true;
		Button button = root.GetComponent<Button>();
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			HandleCampaignConsumableClicked(itemType);
		});
		Text count = CreateText("Count", root.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 18, (FontStyle)1, (TextAnchor)4);
		count.text = quantity.ToString();
		count.color = quantity > 0 ? Color.white : new Color(0.78f, 0.82f, 0.84f, 0.9f);
		Outline outline = ((Component)count).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		SetRect(count.rectTransform, new Vector2(0.62f, 0.02f), new Vector2(0.98f, 0.34f));
		LayoutElement layout = root.AddComponent<LayoutElement>();
		layout.minWidth = ImplementationArchiveCardSize;
		layout.preferredWidth = ImplementationArchiveCardSize;
		layout.minHeight = ImplementationArchiveCardSize;
		layout.preferredHeight = ImplementationArchiveCardSize;
		implementationConsumableViews.Add(root);
	}

	private void ClearImplementationConsumables()
	{
		for (int num = implementationConsumableViews.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)implementationConsumableViews[num] != (Object)null)
			{
				Object.Destroy((Object)(object)implementationConsumableViews[num]);
			}
		}
		implementationConsumableViews.Clear();
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
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text);
		text.text = "SCEGLI LA VIA";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		Outline headingOutline = ((Component)text).gameObject.AddComponent<Outline>();
		headingOutline.effectColor = new Color(0.08f, 0.035f, 0.01f, 0.95f);
		headingOutline.effectDistance = new Vector2(3f, -3f);
		SetRect(text.rectTransform, new Vector2(0.12f, 0.54f), new Vector2(0.88f, 0.64f));
		Text text2 = CreateText("Hint", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
		text2.text = "Tre porte. Un solo destino.";
		text2.color = new Color(0.84f, 0.9f, 0.92f);
		Outline hintOutline = ((Component)text2).gameObject.AddComponent<Outline>();
		hintOutline.effectColor = new Color(0.05f, 0.025f, 0.015f, 0.92f);
		hintOutline.effectDistance = new Vector2(2f, -2f);
		SetRect(text2.rectTransform, new Vector2(0.12f, 0.47f), new Vector2(0.88f, 0.54f));
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
		CreateRoomChoiceRevealLabel(((Component)image).transform, font);
		CreateRoomChoiceRevealLabel(((Component)image).transform, font);
		CreateRoomChoiceRevealLabel(((Component)image).transform, font);
		RefreshRoomChoiceLayout();
		roomChoicePanel.SetActive(false);
	}

	private void CreateRoomChoiceRevealLabel(Transform parent, Font font)
	{
		Text label = CreateText("Door Reveal Label", parent, font, 24, (FontStyle)1, (TextAnchor)4);
		label.color = new Color(0.96f, 0.84f, 0.36f);
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
		label.resizeTextForBestFit = true;
		label.resizeTextMinSize = 12;
		label.resizeTextMaxSize = 24;
		Outline outline = ((Component)label).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		((Component)label).gameObject.SetActive(false);
		roomChoiceRevealLabels.Add(label);
	}

	private void RefreshRoomChoiceLayout()
	{
		if ((Object)(object)roomChoiceImage == (Object)null)
		{
			return;
		}
		bool landscape = Screen.width > Screen.height;
		int backgroundIndex = Mathf.Clamp(roomChoiceBackgroundIndex, 1, 5);
		string backgroundPath = $"UI/background_choose_room_{backgroundIndex}";
		Sprite sprite = landscape
			?LoadSpriteResource(backgroundPath + "_landscape") ?? LoadSpriteResource(backgroundPath)
			:LoadSpriteResource(backgroundPath);
		sprite ??= LoadSpriteResource("UI/background_choose_room_1");
		roomChoiceImage.sprite = sprite;
		if ((Object)(object)roomChoiceAspectFitter != (Object)null)
		{
			roomChoiceAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			roomChoiceAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : (landscape ?1672f / 941f : 941f / 1672f);
		}
		if ((Object)(object)roomChoiceLeftButton != (Object)null)
		{
			SetRoomChoiceDoorRect((RectTransform)((Component)roomChoiceLeftButton).transform, 0, landscape);
		}
		if ((Object)(object)roomChoiceCenterButton != (Object)null)
		{
			SetRoomChoiceDoorRect((RectTransform)((Component)roomChoiceCenterButton).transform, 1, landscape);
		}
		if ((Object)(object)roomChoiceRightButton != (Object)null)
		{
			SetRoomChoiceDoorRect((RectTransform)((Component)roomChoiceRightButton).transform, 2, landscape);
		}
		for (int i = 0; i < roomChoiceRevealLabels.Count; i++)
		{
			Text label = roomChoiceRevealLabels[i];
			if ((Object)(object)label == (Object)null)
			{
				continue;
			}
			Vector2 min;
			Vector2 max;
			if (landscape)
			{
				min = i switch
				{
					0 => new Vector2(0.08f, 0.78f),
					1 => new Vector2(0.36f, 0.8f),
					_ => new Vector2(0.64f, 0.78f),
				};
				max = i switch
				{
					0 => new Vector2(0.34f, 0.86f),
					1 => new Vector2(0.64f, 0.88f),
					_ => new Vector2(0.92f, 0.86f),
				};
			}
			else
			{
				min = i switch
				{
					0 => new Vector2(0.035f, 0.84f),
					1 => new Vector2(0.34f, 0.832f),
					_ => new Vector2(0.62f, 0.84f),
				};
				max = i switch
				{
					0 => new Vector2(0.355f, 0.91f),
					1 => new Vector2(0.66f, 0.902f),
					_ => new Vector2(0.94f, 0.91f),
				};
			}
			SetRect(label.rectTransform, min, max);
		}
		RefreshRoomChoiceRevealLabels();
	}

	private static void SetRoomChoiceDoorRect(RectTransform rect, int index, bool landscape)
	{
		Vector2 min;
		Vector2 max;
		if (landscape)
		{
			min = index switch
			{
				0 => new Vector2(0.055f, 0.31f),
				1 => new Vector2(0.365f, 0.29f),
				_ => new Vector2(0.685f, 0.31f),
			};
			max = index switch
			{
				0 => new Vector2(0.285f, 0.9f),
				1 => new Vector2(0.635f, 0.92f),
				_ => new Vector2(0.915f, 0.9f),
			};
		}
		else
		{
			min = index switch
			{
				0 => new Vector2(0.015f, 0.695f),
				1 => new Vector2(0.32f, 0.705f),
				_ => new Vector2(0.705f, 0.695f),
			};
			max = index switch
			{
				0 => new Vector2(0.285f, 0.955f),
				1 => new Vector2(0.68f, 0.965f),
				_ => new Vector2(0.985f, 0.955f),
			};
		}
		SetRect(rect, min, max);
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
		SetRect(cardInspectionSlot, new Vector2(0.215f, 0.63f), new Vector2(0.785f, 0.955f));
		cardInspectionContentViewport = new GameObject("Inspection Content Viewport", new Type[5]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image),
			typeof(RectMask2D),
			typeof(ScrollRect)
		}).GetComponent<RectTransform>();
		((Transform)cardInspectionContentViewport).SetParent((Transform)(object)cardInspectionBookRoot, false);
		Image contentViewportImage = ((Component)cardInspectionContentViewport).GetComponent<Image>();
		contentViewportImage.color = Color.clear;
		contentViewportImage.raycastTarget = true;
		SetRect(cardInspectionContentViewport, new Vector2(0.12f, 0.08f), new Vector2(0.84f, 0.615f));
		cardInspectionContentRoot = new GameObject("Inspection Content", new Type[3]
		{
			typeof(RectTransform),
			typeof(VerticalLayoutGroup),
			typeof(ContentSizeFitter)
		}).GetComponent<RectTransform>();
		((Transform)cardInspectionContentRoot).SetParent((Transform)(object)cardInspectionContentViewport, false);
		cardInspectionContentRoot.anchorMin = new Vector2(0f, 1f);
		cardInspectionContentRoot.anchorMax = Vector2.one;
		cardInspectionContentRoot.pivot = new Vector2(0.5f, 1f);
		cardInspectionContentRoot.anchoredPosition = Vector2.zero;
		cardInspectionContentRoot.sizeDelta = Vector2.zero;
		VerticalLayoutGroup contentLayout = ((Component)cardInspectionContentRoot).GetComponent<VerticalLayoutGroup>();
		contentLayout.spacing = 10f;
		contentLayout.padding = new RectOffset(0, 0, 0, 0);
		contentLayout.childAlignment = TextAnchor.UpperLeft;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childForceExpandHeight = false;
		ContentSizeFitter contentFitter = ((Component)cardInspectionContentRoot).GetComponent<ContentSizeFitter>();
		contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		cardInspectionContentScroll = ((Component)cardInspectionContentViewport).GetComponent<ScrollRect>();
		cardInspectionContentScroll.viewport = cardInspectionContentViewport;
		cardInspectionContentScroll.content = cardInspectionContentRoot;
		cardInspectionContentScroll.horizontal = false;
		cardInspectionContentScroll.vertical = true;
		cardInspectionContentScroll.movementType = ScrollRect.MovementType.Clamped;
		cardInspectionContentScroll.inertia = true;
		cardInspectionContentScroll.scrollSensitivity = 36f;
		cardInspectionSummaryText = CreateText("Inspection Summary", (Transform)(object)cardInspectionContentRoot, AccardND.Battlefield.MmoUiTheme.BodyFont, CardInspectionFontSize, (FontStyle)1, (TextAnchor)0);
		cardInspectionSummaryText.color = new Color(0.16f, 0.085f, 0.025f);
		cardInspectionSummaryText.horizontalOverflow = (HorizontalWrapMode)0;
		cardInspectionSummaryText.verticalOverflow = (VerticalWrapMode)1;
		ConfigureCardInspectionText(cardInspectionSummaryText);
		cardInspectionStatusRoot = new GameObject("Inspection Status Rows", new Type[3]
		{
			typeof(RectTransform),
			typeof(VerticalLayoutGroup),
			typeof(LayoutElement)
		}).GetComponent<RectTransform>();
		((Transform)cardInspectionStatusRoot).SetParent((Transform)(object)cardInspectionContentRoot, false);
		VerticalLayoutGroup component = ((Component)cardInspectionStatusRoot).GetComponent<VerticalLayoutGroup>();
		component.spacing = 6f;
		component.padding = new RectOffset(0, 0, 0, 0);
		component.childAlignment = TextAnchor.UpperLeft;
		component.childControlWidth = true;
		component.childControlHeight = true;
		component.childForceExpandWidth = true;
		component.childForceExpandHeight = false;
		((Component)cardInspectionStatusRoot).GetComponent<LayoutElement>().ignoreLayout = true;
		cardInspectionCloseButton = CreateImageButton("Close Card Inspection", (Transform)(object)cardInspectionBookRoot, font, cancelActionSprite, string.Empty);
		((UnityEvent)cardInspectionCloseButton.onClick).AddListener(new UnityAction(CloseCardInspection));
		SetRect((RectTransform)((Component)cardInspectionCloseButton).transform, new Vector2(0.82f, 0.865f), new Vector2(0.91f, 0.92f));
		cardInspectionDraftConfirmButton = CreateButton("Draft Inspect Confirm", (Transform)(object)cardInspectionBookRoot, font, "SELEZIONA");
		((UnityEvent)cardInspectionDraftConfirmButton.onClick).AddListener(new UnityAction(ConfirmInspectedInitialDraftOffer));
		cardInspectionDraftConfirmButtonRect = (RectTransform)((Component)cardInspectionDraftConfirmButton).transform;
		cardInspectionDraftConfirmButtonText = ((Component)cardInspectionDraftConfirmButton).GetComponentInChildren<Text>();
		SetRect(cardInspectionDraftConfirmButtonRect, new Vector2(0.31f, 0.012f), new Vector2(0.69f, 0.07f));
		((Component)cardInspectionDraftConfirmButton).gameObject.SetActive(false);
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
		SetRect(cardInspectionBookRoot, landscape ?new Vector2(0.01f, 0.015f) : new Vector2(0.04f, 0.035f), landscape ?new Vector2(0.99f, 0.985f) : new Vector2(0.96f, 0.965f));
		if ((Object)(object)cardInspectionSlot != (Object)null)
		{
			SetRect(cardInspectionSlot, landscape ?new Vector2(0.105f, 0.14f) : new Vector2(0.215f, 0.63f), landscape ?new Vector2(0.5f, 0.945f) : new Vector2(0.785f, 0.955f));
		}
		if ((Object)(object)cardInspectionSummaryText != (Object)null)
		{
			ConfigureCardInspectionText(cardInspectionSummaryText);
		}
		if ((Object)(object)cardInspectionContentViewport != (Object)null)
		{
			SetRect(cardInspectionContentViewport, landscape ?new Vector2(0.49f, 0.085f) : new Vector2(0.12f, 0.08f), landscape ?new Vector2(0.915f, 0.875f) : new Vector2(0.84f, 0.615f));
		}
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
			SetRect((RectTransform)((Component)cardInspectionCloseButton).transform, landscape ?new Vector2(0.94f, 0.845f) : new Vector2(0.82f, 0.865f), landscape ?new Vector2(0.985f, 0.92f) : new Vector2(0.91f, 0.92f));
		}
		if ((Object)(object)cardInspectionDraftConfirmButtonRect != (Object)null)
		{
			SetCardInspectionConfirmButtonRect(landscape);
		}
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
			((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
		}
	}

	private void ApplyCardInspectionContentLayout(bool compactBossStatusLayout)
	{
		bool landscape = Screen.width > Screen.height;
		if ((Object)(object)cardInspectionContentViewport != (Object)null)
		{
			Vector2 minimum = landscape
				? new Vector2(0.49f, compactBossStatusLayout ?0.10f : 0.085f)
				: new Vector2(0.12f, compactBossStatusLayout ?0.09f : 0.08f);
			SetRect(
				cardInspectionContentViewport,
				minimum,
				landscape ?new Vector2(0.915f, 0.875f) : new Vector2(0.84f, 0.615f));
		}
	}

	private static void ConfigureCardInspectionText(Text text)
	{
		if ((Object)(object)text == (Object)null)
		{
			return;
		}

		text.font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		text.fontSize = CardInspectionFontSize;
		text.resizeTextForBestFit = false;
		text.resizeTextMinSize = CardInspectionFontSize;
		text.resizeTextMaxSize = CardInspectionFontSize;
	}

	private void SetCardInspectionConfirmButtonRect(bool landscape)
	{
		bool consumableInspection = inspectedCampaignConsumableActive;
		Vector2 min = landscape
			? (consumableInspection ?new Vector2(0.18f, 0.055f) : new Vector2(0.18f, 0.035f))
			: (consumableInspection ?new Vector2(0.31f, 0.055f) : new Vector2(0.31f, 0.012f));
		Vector2 max = landscape
			? (consumableInspection ?new Vector2(0.42f, 0.125f) : new Vector2(0.42f, 0.105f))
			: (consumableInspection ?new Vector2(0.69f, 0.113f) : new Vector2(0.69f, 0.07f));
		SetRect(cardInspectionDraftConfirmButtonRect, min, max);
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
		Button button = CreateHubBannerButton("Campaign Mode", ((Component)image).transform, font, MultiplayerRankedCtaResource, "CAMPAGNA");
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			PlayHubZoomThenOpen(button, OpenCampaignModeSelection);
		});
		modeSelectionCampaignButton = button;
		Button button2 = CreateHubBannerButton("Multiplayer Mode", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_back_red", "MULTIPLAYER");
		((UnityEvent)button2.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartPvpMode();
		});
		modeSelectionMultiplayerButton = button2;
		Button button3 = CreateHubBannerButton("Sanctuary Hub", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_blue", "SANTUARIO");
		((UnityEvent)button3.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowSanctuary();
		});
		modeSelectionSanctuaryButton = button3;
		Button button4 = CreateHubBannerButton("Library Hub", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_brown", "BIBLIOTECA");
		((UnityEvent)button4.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowLibrary();
		});
		modeSelectionLibraryButton = button4;
		Button button5 = CreateHubBannerButton("Shop Hub", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_olive", "NEGOZIO");
		((UnityEvent)button5.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowShop();
		});
		modeSelectionShopButton = button5;
		Button button6 = CreateHubBannerButton("Profile Hub", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_dark_gray", "PROFILO");
		((UnityEvent)button6.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowProfile();
		});
		modeSelectionProfileButton = button6;
		CreateProfileHubNotificationBadge(button6, font);
		Button button7 = CreateHubBannerButton("Hall Of Fame Hub", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_black", "CLASSIFICA");
		((UnityEvent)button7.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowHallOfFame();
		});
		modeSelectionHallOfFameButton = button7;
		Button tavernButton = CreateHubBannerButton("Tavern Hub", ((Component)image).transform, font, "UI/CampaignRestyle/campaign_cta_orange", "TAVERNA");
		((UnityEvent)tavernButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowTavern();
		});
		modeSelectionTavernButton = tavernButton;
		CreateTavernNotificationBadge(tavernButton, font);
		CreateHubHotspots(((Component)image).transform);
		CreateAccountBanner(canvasTransform, font);
		CreateAccountHoneyIndicator(canvasTransform, font);
		modeSelectionTutorialButton = null;
		Button button8 = CreateTransparentButton("Tutorial Advance", ((Component)image).transform);
		((UnityEvent)button8.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			AdvanceTutorial();
		});
		Stretch((RectTransform)((Component)button8).transform);
		tutorialAdvanceButton = button8;
		((Component)tutorialAdvanceButton).gameObject.SetActive(false);
		RefreshModeSelectionLayout();
		CreateMultiplayerPopup(((Component)image).transform, font);
		CreateLevelUpRewardPopup(((Component)image).transform, font);
		modeSelectionPanel.SetActive(false);
		if ((Object)(object)accountBannerImage != (Object)null)
		{
			((Component)accountBannerImage).gameObject.SetActive(false);
		}
		if ((Object)(object)accountHoneyPanelImage != (Object)null)
		{
			((Component)accountHoneyPanelImage).gameObject.SetActive(false);
		}
	}

	private void CreateHubHotspots(Transform parent)
	{
		modeSelectionHotspotButtons.Clear();
		modeSelectionHotspotRects.Clear();
		AddHubHotspot("Portal Hotspot", parent, modeSelectionCampaignButton, new Vector2(0.375f, 0.612f), new Vector2(0.625f, 0.878f), new Color(0.65f, 0.2f, 0.96f, 1f), new Vector2(33.357f, 44.7885f), new Vector2(16.2f, -56.4727f));
		AddHubHotspot("Arena Hotspot", parent, modeSelectionMultiplayerButton, new Vector2(0.036f, 0.575f), new Vector2(0.378f, 0.754f), new Color(0.96f, 0.12f, 0.1f, 1f), new Vector2(42.12f, -32.64f), new Vector2(-13.6313f, -37.973f), 0.45f);
		AddHubHotspot("Sanctuary Hotspot", parent, modeSelectionSanctuaryButton, new Vector2(0.655f, 0.575f), new Vector2(0.955f, 0.754f), new Color(0.12f, 0.58f, 1f, 1f), new Vector2(-6.8156f, -51.6043f), new Vector2(-39.5014f, -23.3682f));
		AddHubHotspot("Statue Hotspot", parent, modeSelectionProfileButton, new Vector2(0.414f, 0.44f), new Vector2(0.596f, 0.612f), new Color(0.48f, 0.5f, 0.54f, 1f));
		AddHubHotspot("Library Hotspot", parent, modeSelectionLibraryButton, new Vector2(0.045f, 0.325f), new Vector2(0.459f, 0.558f), new Color(1f, 0.38f, 0.045f, 1f), new Vector2(0f, 57.6f), new Vector2(-109.8879f, -9.6f), 0.32f);
		AddHubHotspot("Monument Hotspot", parent, modeSelectionHallOfFameButton, new Vector2(0.64f, 0.366f), new Vector2(0.945f, 0.553f), new Color(0.015f, 0.015f, 0.02f, 1f), Vector2.zero, new Vector2(-17.7777f, -18.8889f));
		AddHubHotspot("Shop Hotspot", parent, modeSelectionShopButton, new Vector2(0.054f, 0.07f), new Vector2(0.444f, 0.338f), new Color(0.12f, 0.86f, 0.24f, 1f), new Vector2(16.1995f, 44.3088f), new Vector2(-41.1635f, -26.3083f), 0.28f);
		AddHubHotspot("Tavern Hotspot", parent, modeSelectionTavernButton, new Vector2(0.556f, 0.07f), new Vector2(0.946f, 0.338f), new Color(1f, 0.38f, 0.045f, 1f), new Vector2(41.1635f, 44.3088f), new Vector2(-16.1995f, -26.3083f), 0.55f);
	}

	private void AddHubHotspot(string name, Transform parent, Button target, Vector2 minimum, Vector2 maximum, Color sparkleColor, Vector2 offsetMinimum = default, Vector2 offsetMaximum = default, float glowStrength = 1f)
	{
		if ((Object)(object)target == (Object)null)
		{
			return;
		}
		Button hotspot = CreateTransparentButton(name, parent);
		RectTransform hotspotRect = (RectTransform)((Component)hotspot).transform;
		SetRect(hotspotRect, minimum, maximum);
		hotspotRect.offsetMin = offsetMinimum;
		hotspotRect.offsetMax = offsetMaximum;
		((Component)hotspot).transform.SetAsFirstSibling();
		((UnityEvent)hotspot.onClick).AddListener((UnityAction)delegate
		{
			if (target.interactable && ((Component)target).gameObject.activeInHierarchy)
			{
				((UnityEvent)target.onClick).Invoke();
			}
		});
		modeSelectionHotspotButtons.Add(hotspot);
		modeSelectionHotspotRects[target] = hotspotRect;
		HubPortalVfx.Attach(hotspot, sparkleColor, 20, glowStrength);
	}

	private void CreateCampaignModeSelectionView(Font font)
	{
		Image image = CreateImage("Campaign Mode Selection", (Transform)(object)canvasRect, Color.clear);
		image.raycastTarget = true;
		campaignModeSelectionPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 905;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image image2 = CreateImage("Campaign Mode Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		ApplyCampaignPortalBackground(image2);
		SetRect(image2.rectTransform, Vector2.zero, Vector2.one);
		Image image3 = CreateImage("Screen Outer Frame", ((Component)image).transform, Color.white);
		campaignModeSelectionFrameImage = image3;
		campaignModeSelectionFrameAspectFitter = ConfigureScreenOuterFrame(image3);
		image3.color = new Color(1f, 1f, 1f, 0.92f);
		image3.raycastTarget = false;
		SetRect(image3.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));
		Transform campaignContentRoot = ((Component)image).transform;
		campaignModeSelectionTitlePanel = CreateImage("Campaign Title Panel", campaignContentRoot, Color.white);
		campaignModeSelectionTitlePanel.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		campaignModeSelectionTitlePanel.type = Image.Type.Simple;
		campaignModeSelectionTitlePanel.preserveAspect = false;
		campaignModeSelectionTitlePanel.raycastTarget = false;
		campaignModeSelectionHeadingText = CreateText(
			"Heading",
			((Component)campaignModeSelectionTitlePanel).transform,
			font,
			42,
			(FontStyle)1,
			(TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(campaignModeSelectionHeadingText);
		campaignModeSelectionHeadingText.text = "CAMPAGNA";
		campaignModeSelectionHeadingText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(
			campaignModeSelectionHeadingText.rectTransform,
			new Vector2(0.08f, 0.18f),
			new Vector2(0.92f, 0.72f));
		campaignModeSelectionHeadingText.rectTransform.offsetMin = new Vector2(0f, -21f);
		campaignModeSelectionHeadingText.rectTransform.offsetMax = new Vector2(0f, -21f);
		Button button = CreateButton("Campaign Adventure Mode", campaignContentRoot, font, "AVVENTURA");
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartAdventureMode();
		});
		campaignModeAdventureButton = button;
		ApplyCampaignAdventureCta(button);
		campaignModeBuilderButtonRect = (RectTransform)((Component)button).transform;
		Button button2 = CreateButton("Campaign Hardcore Mode", campaignContentRoot, font, "HARDCORE");
		((UnityEvent)button2.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartHardcoreMode();
		});
		campaignModeHardcoreButton = button2;
		campaignModeHardcoreButtonText = ((Component)button2).GetComponentInChildren<Text>();
		ApplyCampaignHardcoreCta(button2);
		campaignModeDraftButtonRect = (RectTransform)((Component)button2).transform;
		CreateAdventureChapterView(font);
		RefreshCampaignModeSelectionLayout();
		campaignModeSelectionPanel.SetActive(false);
	}

	private void CreateAdventureChapterView(Font font)
	{
		Image image = CreateImage("Adventure Chapters", (Transform)(object)canvasRect, new Color(0.006f, 0.008f, 0.01f, 1f));
		image.raycastTarget = true;
		adventureChapterPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas canvas = ((Component)image).gameObject.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 906;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image background = CreateImage("Adventure Chapters Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		ApplyCampaignPortalBackground(background);
		background.raycastTarget = false;
		SetRect(background.rectTransform, Vector2.zero, Vector2.one);
		adventureChapterInnerBackgroundImage = CreateImage(
			"Screen Inner Background",
			((Component)image).transform,
			new Color(0.004f, 0.005f, 0.008f, 0.72f));
		adventureChapterInnerBackgroundImage.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
		adventureChapterInnerBackgroundImage.type = Image.Type.Sliced;
		adventureChapterInnerBackgroundImage.raycastTarget = true;
		SetRect(
			adventureChapterInnerBackgroundImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, 0.795f));
		Image frame = CreateImage("Screen Outer Frame", ((Component)image).transform, Color.white);
		adventureChapterFrameImage = frame;
		adventureChapterFrameAspectFitter = ConfigureScreenOuterFrame(frame);
		frame.color = new Color(1f, 1f, 1f, 0.92f);
		frame.raycastTarget = false;
		SetRect(frame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));
		Transform adventureContentRoot = ((Component)image).transform;
		adventureChapterTitlePanel = CreateImage("Adventure Title Panel", adventureContentRoot, Color.white);
		adventureChapterTitlePanel.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		adventureChapterTitlePanel.type = Image.Type.Simple;
		adventureChapterTitlePanel.preserveAspect = false;
		adventureChapterTitlePanel.raycastTarget = false;
		adventureChapterHeadingText = CreateText(
			"Adventure Heading",
			((Component)adventureChapterTitlePanel).transform,
			font,
			42,
			(FontStyle)1,
			(TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(adventureChapterHeadingText);
		adventureChapterHeadingText.text = "AVVENTURA";
		adventureChapterHeadingText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(
			adventureChapterHeadingText.rectTransform,
			new Vector2(0.08f, 0.18f),
			new Vector2(0.92f, 0.72f));
		adventureChapterHeadingText.rectTransform.offsetMin = new Vector2(0f, -21f);
		adventureChapterHeadingText.rectTransform.offsetMax = new Vector2(0f, -21f);
		adventureChapterListRoot = new GameObject("Adventure Chapter List", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)adventureChapterListRoot).SetParent(adventureContentRoot, false);
		GridLayoutGroup layout = ((Component)adventureChapterListRoot).GetComponent<GridLayoutGroup>();
		layout.spacing = new Vector2(18f, 18f);
		layout.childAlignment = (TextAnchor)1;
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 2;
		CreateAdventureTutorialConfirmPopup(((Component)image).transform, font);
		adventureChapterPanel.SetActive(false);
	}

	private void CreateAdventureTutorialConfirmPopup(Transform parent, Font font)
	{
		Image overlay = CreateImage("Adventure Tutorial Confirm Popup", parent, new Color(0f, 0f, 0f, 0.68f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		adventureTutorialConfirmPopup = ((Component)overlay).gameObject;
		Canvas canvas = adventureTutorialConfirmPopup.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 920;
		adventureTutorialConfirmPopup.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Adventure Tutorial Confirm Dialog", ((Component)overlay).transform, new Color(0.012f, 0.018f, 0.032f, 0.98f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(dialog.rectTransform, "Tutorial Confirm Crest", new Vector2(0.5f, 1f), new Vector2(42f, 42f), Color.white);
		SetRect(dialog.rectTransform, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f));

		adventureTutorialConfirmTitleText = CreateText("Tutorial Confirm Title", ((Component)dialog).transform, AccardND.Battlefield.MmoUiTheme.LoreFont, 40, FontStyle.Normal, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(adventureTutorialConfirmTitleText);
		adventureTutorialConfirmTitleText.text = "TUTORIAL: PRIMI PASSI";
		adventureTutorialConfirmTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(adventureTutorialConfirmTitleText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f));

		adventureTutorialConfirmBodyText = CreateText("Tutorial Confirm Body", ((Component)dialog).transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 30, FontStyle.Normal, (TextAnchor)4);
		adventureTutorialConfirmBodyText.text = "Entrerai in uno stage guidato: ti verra indicato cosa toccare, i tiri saranno controllati e ogni passo spieghera le basi del gioco. Al completamento ricevi le classi base e il primo capitolo.";
		adventureTutorialConfirmBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		adventureTutorialConfirmBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		adventureTutorialConfirmBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		adventureTutorialConfirmBodyText.resizeTextForBestFit = true;
		adventureTutorialConfirmBodyText.resizeTextMinSize = 16;
		adventureTutorialConfirmBodyText.resizeTextMaxSize = 30;
		SetRect(adventureTutorialConfirmBodyText.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.7f));

		Button cancelButton = CreateButton("Cancel Tutorial Confirm", ((Component)dialog).transform, font, "ANNULLA");
		AccardND.Battlefield.MmoUiTheme.ApplyBackButtonStyle(cancelButton);
		((UnityEvent)cancelButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideAdventureTutorialConfirmPopup();
		});
		SetRect((RectTransform)((Component)cancelButton).transform, new Vector2(0.08f, 0.1f), new Vector2(0.44f, 0.27f));

		Button goButton = CreateButton("Start Tutorial Confirm", ((Component)dialog).transform, font, "ANDIAMO");
		AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(goButton);
		((UnityEvent)goButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			Action action = adventureConfirmAction;
			HideAdventureTutorialConfirmPopup();
			if (action != null)
			{
				action.Invoke();
			}
		});
		SetRect((RectTransform)((Component)goButton).transform, new Vector2(0.56f, 0.1f), new Vector2(0.92f, 0.27f));

		adventureTutorialConfirmPopup.SetActive(false);
		CreateGuidedAdventureTutorialView(parent, font);
	}

	private void CreateGuidedAdventureTutorialView(Transform parent, Font font)
	{
		Image overlay = CreateImage("Guided Adventure Tutorial", parent, new Color(0f, 0f, 0f, 0.78f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		guidedTutorialPanel = ((Component)overlay).gameObject;
		Canvas canvas = guidedTutorialPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 930;
		guidedTutorialPanel.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Guided Tutorial Dialog", ((Component)overlay).transform, new Color(0.01f, 0.016f, 0.03f, 0.985f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(dialog.rectTransform, "Guided Tutorial Crest", new Vector2(0.5f, 1f), new Vector2(46f, 46f), Color.white);
		SetRect(dialog.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.84f));

		guidedTutorialTitleText = CreateText("Guided Tutorial Title", ((Component)dialog).transform, AccardND.Battlefield.MmoUiTheme.LoreFont, 31, FontStyle.Normal, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(guidedTutorialTitleText);
		guidedTutorialTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(guidedTutorialTitleText.rectTransform, new Vector2(0.07f, 0.79f), new Vector2(0.93f, 0.93f));

		guidedTutorialStepText = CreateText("Guided Tutorial Step", ((Component)dialog).transform, font, 18, (FontStyle)1, (TextAnchor)4);
		guidedTutorialStepText.color = new Color(0.66f, 0.78f, 0.84f);
		SetRect(guidedTutorialStepText.rectTransform, new Vector2(0.1f, 0.7f), new Vector2(0.9f, 0.78f));

		guidedTutorialBodyText = CreateText("Guided Tutorial Body", ((Component)dialog).transform, font, 23, (FontStyle)1, (TextAnchor)4);
		guidedTutorialBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		guidedTutorialBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		guidedTutorialBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		guidedTutorialBodyText.resizeTextForBestFit = true;
		guidedTutorialBodyText.resizeTextMinSize = 15;
		guidedTutorialBodyText.resizeTextMaxSize = 23;
		SetRect(guidedTutorialBodyText.rectTransform, new Vector2(0.08f, 0.29f), new Vector2(0.92f, 0.69f));

		Button exitButton = CreateButton("Guided Tutorial Exit", ((Component)dialog).transform, font, "ESCI");
		((UnityEvent)exitButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			EndGuidedAdventureTutorial(complete: false);
		});
		SetRect((RectTransform)((Component)exitButton).transform, new Vector2(0.06f, 0.08f), new Vector2(0.25f, 0.19f));

		guidedTutorialPreviousButton = CreateButton("Guided Tutorial Previous", ((Component)dialog).transform, font, "INDIETRO");
		((UnityEvent)guidedTutorialPreviousButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			MoveGuidedTutorialStep(-1);
		});
		SetRect((RectTransform)((Component)guidedTutorialPreviousButton).transform, new Vector2(0.31f, 0.08f), new Vector2(0.52f, 0.19f));

		guidedTutorialNextButton = CreateButton("Guided Tutorial Next", ((Component)dialog).transform, font, "AVANTI");
		guidedTutorialNextButtonText = ((Component)guidedTutorialNextButton).GetComponentInChildren<Text>();
		((UnityEvent)guidedTutorialNextButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			MoveGuidedTutorialStep(1);
		});
		SetRect((RectTransform)((Component)guidedTutorialNextButton).transform, new Vector2(0.58f, 0.08f), new Vector2(0.94f, 0.19f));

		guidedTutorialPanel.SetActive(false);
	}

	private void RefreshCampaignModeSelectionLayout()
	{
		if ((Object)(object)campaignModeSelectionPanel == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
		{
			return;
		}
		Rect safeRect = safeAreaRoot.rect;
		float width = Mathf.Max(1f, safeRect.width);
		float height = Mathf.Max(1f, safeRect.height);
		bool compact = IsCompactLayout(width / height, configuration.ResponsiveLayout);
		RefreshScreenOuterFrame(campaignModeSelectionFrameImage, campaignModeSelectionFrameAspectFitter);
		SetRect(
			campaignModeSelectionFrameImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.795f : 0.73f));
		SetRect(
			campaignModeSelectionTitlePanel.rectTransform,
			compact ? new Vector2(0.08f, 0.785f) : new Vector2(0.16f, 0.72f),
			compact ? new Vector2(0.92f, 0.9f) : new Vector2(0.84f, 0.845f));
		campaignModeSelectionHeadingText.fontSize = compact ?46 : 44;
		if (compact)
		{
			SetCampaignModeButtonNaturalRect(
				campaignModeBuilderButtonRect,
				new Vector2(0.5f, 0.55f),
				0.86f,
				width / height);
			SetCampaignModeButtonNaturalRect(
				campaignModeDraftButtonRect,
				new Vector2(0.5f, 0.39f),
				0.86f,
				width / height);
		}
		else
		{
			SetCampaignModeButtonNaturalRect(
				campaignModeBuilderButtonRect,
				new Vector2(0.275f, 0.41f),
				0.4f,
				width / height);
			SetCampaignModeButtonNaturalRect(
				campaignModeDraftButtonRect,
				new Vector2(0.725f, 0.41f),
				0.4f,
				width / height);
		}
		RefreshSinglePlayerProgressView();
	}

	private static void SetCampaignModeButtonNaturalRect(
		RectTransform rect,
		Vector2 center,
		float normalizedWidth,
		float canvasAspect)
	{
		if ((Object)(object)rect == (Object)null)
			return;

		Sprite frame = GetCampaignAdventureCtaSprite();
		float frameAspect = (Object)(object)frame != (Object)null && frame.rect.height > 0f
			? frame.rect.width / frame.rect.height
			: 1692f / 400f;
		RectTransform parentRect = rect.parent as RectTransform;
		float parentAspect = (Object)(object)parentRect != (Object)null && parentRect.rect.height > 0f
			? parentRect.rect.width / parentRect.rect.height
			: canvasAspect;
		float normalizedHeight = normalizedWidth * parentAspect / frameAspect;
		Vector2 halfSize = new Vector2(normalizedWidth, normalizedHeight) * 0.5f;
		SetRect(rect, center - halfSize, center + halfSize);
		rect.localScale = Vector3.one;
	}

	private void RefreshAdventureChapterLayout()
	{
		if ((Object)(object)adventureChapterPanel == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
		{
			return;
		}
		Rect safeRect = safeAreaRoot.rect;
		float width = Mathf.Max(1f, safeRect.width);
		float height = Mathf.Max(1f, safeRect.height);
		bool compact = IsCompactLayout(width / height, configuration.ResponsiveLayout);
		RefreshScreenOuterFrame(adventureChapterFrameImage, adventureChapterFrameAspectFitter);
		SetRect(
			adventureChapterFrameImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.795f : 0.73f));
		SetRect(
			adventureChapterInnerBackgroundImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.795f : 0.73f));
		SetRect(
			adventureChapterTitlePanel.rectTransform,
			compact ? new Vector2(0.08f, 0.785f) : new Vector2(0.16f, 0.79f),
			compact ? new Vector2(0.92f, 0.9f) : new Vector2(0.84f, 0.915f));
		adventureChapterHeadingText.fontSize = compact ? 46 : 44;
		adventureChapterListRoot.anchorMin = new Vector2(0.06f, 0.17f);
		adventureChapterListRoot.anchorMax = new Vector2(0.94f, 0.77f);
		adventureChapterListRoot.offsetMin = new Vector2(0f, -220f);
		adventureChapterListRoot.offsetMax = new Vector2(0f, -220f);
		adventureChapterListRoot.localScale = Vector3.one;
		ConfigureAdventureChapterGrid(compact);
	}

	private void ConfigureAdventureChapterGrid(bool compact)
	{
		if ((Object)(object)adventureChapterListRoot == (Object)null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		GridLayoutGroup grid = ((Component)adventureChapterListRoot).GetComponent<GridLayoutGroup>();
		if ((Object)(object)grid == (Object)null)
		{
			return;
		}
		Rect rect = adventureChapterListRoot.rect;
		int columns = 3;
		float horizontalSpacing = compact ? 10f : 16f;
		float verticalSpacing = 30f;
		float sizingVerticalSpacing = compact ? 14f : 18f;
		float captionHeight = compact ? 32f : 60f;
		float width = Mathf.Max(1f, rect.width - horizontalSpacing * (columns - 1));
		float height = Mathf.Max(1f, rect.height - sizingVerticalSpacing);
		float verticalLimit = compact
			? height / 2f - captionHeight
			: height / 2f - 56f;
		float square = Mathf.Min(width / columns, verticalLimit, compact ? 224f : 298f);
		square = Mathf.Max(compact ? 154f : 188f, square);
		grid.constraintCount = columns;
		grid.spacing = new Vector2(horizontalSpacing, verticalSpacing);
		grid.cellSize = new Vector2(square, square + captionHeight);
	}

	private void ShowAdventureChapterSelection()
	{
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(true);
			adventureChapterPanel.transform.SetAsLastSibling();
		}
		RefreshAdventureChapterLayout();
		RefreshAdventureChapterList();
	}

	private void RefreshAdventureChapterList()
	{
		ClearAdventureChapterRows();
		if ((Object)(object)adventureChapterListRoot == (Object)null)
		{
			return;
		}
		CreateAdventureTutorialRow();
		foreach (AdventureChapter chapter in AdventureChapterCatalog.All)
		{
			string chapterId = chapter.Id;
			CreateAdventureChapterRow(
				chapter,
				singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, chapterId),
				() => TryOpenAdventureChapter(chapterId));
		}
	}

	/// <summary>
	/// La riga del tutorial: e' sempre aperta ed e' la porta del primo capitolo, quindi non
	/// passa dal catalogo dei capitoli.
	/// </summary>
	private void CreateAdventureTutorialRow()
	{
		bool done = singlePlayerProgressService.TutorialCompleted;
		string status = done
			? GameText.GetOrFallbackSilent(GameTextKeys.Campaign.ChapterCompleted, "completato")
			: GameText.GetOrFallbackSilent(GameTextKeys.Campaign.ChapterUnlockFirst, "sblocca il primo capitolo");

		CreateAdventureRow(
			"tutorial",
			"Tutorial",
			"Primi Passi",
			status,
			available: true,
			locked: false,
			LoadSpriteResource("UI/tutorial_chapter"),
			StartTutorialAdventureStage);
	}

	private void CreateAdventureChapterRow(AdventureChapter chapter, bool unlocked, Action action)
	{
		// Tre stati, non due: aperto, chiuso, e "in arrivo" per i capitoli gia' in campagna
		// ma senza il loro boss. Mostrare un capitolo in arrivo come semplicemente chiuso
		// manderebbe il giocatore al Santuario a cercare qualcosa che non e' in vendita.
		bool available = unlocked && chapter.Playable;
		string status;
		if (!chapter.Playable)
		{
			status = GameText.GetOrFallbackSilent(GameTextKeys.Campaign.ChapterComingSoon, "in arrivo");
		}
		else if (IsAdventureChapterCompleted(chapter.Id))
		{
			status = GameText.GetOrFallbackSilent(GameTextKeys.Campaign.ChapterCompleted, "completato");
		}
		else if (unlocked)
		{
			status = GameText.GetOrFallbackSilent(GameTextKeys.Campaign.ChapterUnlocked, "sbloccato");
		}
		else
		{
			// Un capitolo chiuso non si apre piu' da qui: si vince o si compra al Santuario.
			// La riga deve dire dove, altrimenti resta un muro senza porta.
			status = GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.ChapterHoneySanctuary,
				"al santuario: {0} miele",
				chapter.HoneyCost);
		}

		// Il lucchetto solo su cio' che e' davvero chiuso. Un capitolo gia' in mano ma senza
		// boss non ha uno sfondo da mostrare (lo scenario non e' collegato) e cade sul
		// pannello di ripiego: il velo scuro e la scritta "in arrivo" bastano a raccontarlo,
		// mentre un lucchetto direbbe al giocatore che deve ancora ottenerlo.
		Sprite cover = unlocked
			? AdventureChapterBackgroundSprite(chapter.Id)
			: LoadSpriteResource("UI/locked_chapter");

		CreateAdventureRow(
			chapter.Id,
			chapter.Title,
			chapter.ScenarioLabel == null ? "???" : "Scenario: " + chapter.ScenarioLabel,
			status,
			available,
			locked: !unlocked,
			cover,
			action);
	}

	private void CreateAdventureRow(
		string id,
		string title,
		string subtitle,
		string status,
		bool available,
		bool locked,
		Sprite coverSprite,
		Action action)
	{
		GameObject row = new GameObject("Adventure " + id, new Type[3]
		{
			typeof(RectTransform),
			typeof(Image),
			typeof(Button)
		});
		row.transform.SetParent((Transform)(object)adventureChapterListRoot, false);
		Image hitTarget = row.GetComponent<Image>();
		hitTarget.color = new Color(1f, 1f, 1f, 0.001f);
		hitTarget.raycastTarget = true;
		Button button = row.GetComponent<Button>();
		button.targetGraphic = hitTarget;
		// Cliccabile anche da chiuso: il tocco serve a spiegare come si apre. Un bottone
		// spento non risponde e lascia il giocatore a indovinare.
		button.interactable = true;
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			action?.Invoke();
		});
		Image cover = CreateImage("Cover", row.transform, AdventureChapterPlaceholderColor(id, available));
		cover.raycastTarget = false;
		if ((Object)(object)coverSprite != (Object)null)
		{
			cover.sprite = coverSprite;
			cover.color = Color.white;
			cover.preserveAspect = false;
		}
		else
		{
			StylePanel(cover);
		}
		GridLayoutGroup chapterGrid = ((Component)adventureChapterListRoot).GetComponent<GridLayoutGroup>();
		float captionRatio = (Object)(object)chapterGrid != (Object)null && chapterGrid.cellSize.y > 0f
			? Mathf.Clamp01((chapterGrid.cellSize.y - chapterGrid.cellSize.x) / chapterGrid.cellSize.y)
			: 0.27f;
		SetRect(cover.rectTransform, new Vector2(0f, captionRatio), new Vector2(1f, 1f));
		// L'immagine del lucchetto e' gia' scura di suo: velarla ancora la renderebbe illeggibile.
		// Il velo pieno serve invece ai capitoli aperti ma non ancora giocabili.
		Image veil = CreateImage("Lock Veil", ((Component)cover).transform, locked
			? new Color(0f, 0f, 0f, 0.12f)
			: (available ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.48f)));
		veil.raycastTarget = false;
		Stretch(veil.rectTransform);
		Text coverText = CreateText("Cover Status", ((Component)cover).transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 20, (FontStyle)1, (TextAnchor)7);
		coverText.raycastTarget = false;
		coverText.text = subtitle.ToUpperInvariant() + "\n" + status.ToUpperInvariant();
		coverText.color = Color.white;
		coverText.horizontalOverflow = HorizontalWrapMode.Wrap;
		coverText.verticalOverflow = VerticalWrapMode.Truncate;
		coverText.resizeTextForBestFit = true;
		coverText.resizeTextMinSize = 10;
		coverText.resizeTextMaxSize = 20;
		Outline outline = ((Component)coverText).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		SetRect(coverText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
		Text titleText = CreateText("Title", row.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 17, (FontStyle)1, (TextAnchor)1);
		titleText.raycastTarget = false;
		titleText.text = title.ToUpperInvariant();
		titleText.color = available ? new Color(0.95f, 0.79f, 0.34f) : new Color(0.56f, 0.62f, 0.66f);
		titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
		titleText.verticalOverflow = VerticalWrapMode.Truncate;
		titleText.resizeTextForBestFit = true;
		titleText.resizeTextMinSize = 10;
		titleText.resizeTextMaxSize = 17;
		SetRect(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, Mathf.Max(0.12f, captionRatio - 0.015f)));
		titleText.rectTransform.offsetMin = new Vector2(titleText.rectTransform.offsetMin.x, -24f);
		adventureChapterRows.Add(row);
	}

	private Sprite AdventureChapterBackgroundSprite(string chapterId)
	{
		if (!TryGetAdventureChapterConfig(chapterId, out string scenarioId, out string bossId, out _))
		{
			return null;
		}
		if ((Object)(object)scenarioCatalog == (Object)null)
		{
			scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		}
		ScenarioDefinition scenario = (Object)(object)scenarioCatalog != (Object)null
			? scenarioCatalog.Select(RoomType.Boss, RoomDifficulty.Hard, bossId, scenarioId)
			: null;
		if ((Object)(object)scenario == (Object)null)
		{
			return null;
		}
		return Screen.width > Screen.height && (Object)(object)scenario.BackgroundLandscape != (Object)null
			? scenario.BackgroundLandscape
			: scenario.Background;
	}

	private bool IsAdventureChapterCompleted(string chapterId)
	{
		return singlePlayerProgressService != null
			&& singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.ChapterCleared, chapterId);
	}

	/// <summary>
	/// Tinta di riserva quando lo sfondo dello scenario non c'e': serve solo a non mostrare
	/// un riquadro nero, quindi basta che i capitoli vicini non si somiglino.
	/// </summary>
	private static Color AdventureChapterPlaceholderColor(string id, bool available)
	{
		Color color = id switch
		{
			"tutorial" => new Color(0.13f, 0.36f, 0.42f, 1f),
			"chapter-1" => new Color(0.47f, 0.33f, 0.1f, 1f),
			"chapter-2" => new Color(0.45f, 0.25f, 0.08f, 1f),
			"chapter-3" => new Color(0.16f, 0.33f, 0.18f, 1f),
			"chapter-4" => new Color(0.5f, 0.42f, 0.14f, 1f),
			"chapter-5" => new Color(0.2f, 0.22f, 0.26f, 1f),
			"chapter-6" => new Color(0.24f, 0.19f, 0.42f, 1f),
			"chapter-7" => new Color(0.35f, 0.11f, 0.19f, 1f),
			_ => new Color(0.08f, 0.14f, 0.18f, 1f)
		};
		return available ? color : Color.Lerp(color, new Color(0.02f, 0.025f, 0.03f, 1f), 0.58f);
	}

	private void ClearAdventureChapterRows()
	{
		for (int index = adventureChapterRows.Count - 1; index >= 0; index--)
		{
			if ((Object)(object)adventureChapterRows[index] != (Object)null)
			{
				Object.Destroy((Object)(object)adventureChapterRows[index]);
			}
		}
		adventureChapterRows.Clear();
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
		Sprite sprite = LoadSpriteResource(GetCurrentHubBackgroundResource());
		if ((Object)(object)sprite == (Object)null)
		{
			sprite = LoadSpriteResource(landscape ?"UI/selection_mode_screen_landscape" : "UI/selection_mode_screen");
		}
		modeSelectionImage.sprite = sprite;
		if ((Object)(object)modeSelectionAspectFitter != (Object)null)
		{
			modeSelectionAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			modeSelectionAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : 944f / 1676f;
		}
		if ((Object)(object)modeSelectionCampaignButton != (Object)null)
		{
			RectTransform campaignRect = (RectTransform)((Component)modeSelectionCampaignButton).transform;
			SetRect(
				campaignRect,
				landscape ?new Vector2(0.38f, 0.492f) : new Vector2(0.269f, 0.608f),
				landscape ?new Vector2(0.62f, 0.573f) : new Vector2(0.769f, 0.688f));
			campaignRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(-6f, 43f);
		}
		if ((Object)(object)modeSelectionMultiplayerButton != (Object)null)
		{
			SetRect(
				(RectTransform)((Component)modeSelectionMultiplayerButton).transform,
				landscape ?new Vector2(0.187f, 0.473f) : new Vector2(0.075f, 0.536f),
				landscape ?new Vector2(0.413f, 0.547f) : new Vector2(0.465f, 0.612f));
		}
		if ((Object)(object)modeSelectionSanctuaryButton != (Object)null)
		{
			RectTransform sanctuaryRect = (RectTransform)((Component)modeSelectionSanctuaryButton).transform;
			SetRect(
				sanctuaryRect,
				landscape ?new Vector2(0.582f, 0.463f) : new Vector2(0.514f, 0.528f),
				landscape ?new Vector2(0.808f, 0.537f) : new Vector2(0.904f, 0.604f));
			sanctuaryRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(59f, 4f);
		}
		if ((Object)(object)modeSelectionLibraryButton != (Object)null)
		{
			RectTransform libraryRect = (RectTransform)((Component)modeSelectionLibraryButton).transform;
			SetRect(
				libraryRect,
				landscape ?new Vector2(0.157f, 0.358f) : new Vector2(0.069f, 0.333f),
				landscape ?new Vector2(0.383f, 0.432f) : new Vector2(0.459f, 0.409f));
			libraryRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(0f, -28f);
		}
		if ((Object)(object)modeSelectionShopButton != (Object)null)
		{
			RectTransform shopRect = (RectTransform)((Component)modeSelectionShopButton).transform;
			SetRect(
				shopRect,
				landscape ?new Vector2(0.107f, 0.193f) : new Vector2(0.054f, 0.078f),
				landscape ?new Vector2(0.333f, 0.267f) : new Vector2(0.444f, 0.154f));
			shopRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(17f, 0f);
		}
		if ((Object)(object)modeSelectionTavernButton != (Object)null)
		{
			RectTransform tavernRect = (RectTransform)((Component)modeSelectionTavernButton).transform;
			SetRect(
				tavernRect,
				landscape ?new Vector2(0.667f, 0.193f) : new Vector2(0.556f, 0.078f),
				landscape ?new Vector2(0.893f, 0.267f) : new Vector2(0.946f, 0.154f));
			tavernRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(-17f, 0f);
		}
		if ((Object)(object)modeSelectionProfileButton != (Object)null)
		{
			RectTransform profileRect = (RectTransform)((Component)modeSelectionProfileButton).transform;
			SetRect(
				profileRect,
				landscape ?new Vector2(0.373f, 0.362f) : new Vector2(0.289f, 0.416f),
				landscape ?new Vector2(0.627f, 0.443f) : new Vector2(0.719f, 0.492f));
			profileRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(0f, -41f);
		}
		if ((Object)(object)modeSelectionHallOfFameButton != (Object)null)
		{
			RectTransform hallOfFameRect = (RectTransform)((Component)modeSelectionHallOfFameButton).transform;
			SetRect(
				hallOfFameRect,
				landscape ?new Vector2(0.602f, 0.343f) : new Vector2(0.561f, 0.355f),
				landscape ?new Vector2(0.828f, 0.417f) : new Vector2(0.951f, 0.429f));
			hallOfFameRect.anchoredPosition = landscape ?Vector2.zero : new Vector2(5f, -16f);
		}
		RefreshAccountBannerLayout(landscape);
		RefreshAccountHoneyIndicatorLayout(landscape);
		RefreshAccountBannerView();
	}

	private void CreateAccountHoneyIndicator(Transform parent, Font font)
	{
		accountHoneyPanelImage = CreateImage("Account Honey Currency", parent, Color.white);
		accountHoneyPanelImage.sprite = LoadHoneyPotCurrencySprite();
		accountHoneyPanelImage.preserveAspect = true;
		accountHoneyPanelImage.raycastTarget = false;
		AddHubGraphicOutline(accountHoneyPanelImage);
		Canvas honeyCanvas = ((Component)accountHoneyPanelImage).gameObject.AddComponent<Canvas>();
		honeyCanvas.overrideSorting = true;
		honeyCanvas.sortingOrder = 911;

		accountHoneyAmountText = CreateText("Account Honey Amount", ((Component)accountHoneyPanelImage).transform, font, 35, FontStyle.Bold, TextAnchor.MiddleCenter);
		accountHoneyAmountText.color = Color.white;
		accountHoneyAmountText.horizontalOverflow = HorizontalWrapMode.Overflow;
		accountHoneyAmountText.verticalOverflow = VerticalWrapMode.Overflow;
		Outline amountOutline = ((Component)accountHoneyAmountText).gameObject.AddComponent<Outline>();
		amountOutline.effectColor = new Color(0.08f, 0.035f, 0f, 1f);
		amountOutline.effectDistance = new Vector2(2f, -2f);
		Stretch(accountHoneyAmountText.rectTransform);
	}

	private void CreateAccountBanner(Transform parent, Font font)
	{
		// La testata dell'hub usa la stessa grammatica del multiplayer: nessun
		// fondale-banner, ritratto circolare con cornice, nome, livello e barra EXP.
		accountBannerImage = CreateImage("Account Header", parent, Color.clear);
		accountBannerImage.sprite = null;
		accountBannerImage.preserveAspect = false;
		accountBannerImage.raycastTarget = false;
		Canvas accountHeaderCanvas = ((Component)accountBannerImage).gameObject.AddComponent<Canvas>();
		accountHeaderCanvas.overrideSorting = true;
		accountHeaderCanvas.sortingOrder = 910;
		((Component)accountBannerImage).gameObject.AddComponent<GraphicRaycaster>();

		Image portraitRootImage = CreateImage(
			"Account Portrait Root", ((Component)accountBannerImage).transform, Color.clear);
		portraitRootImage.raycastTarget = false;
		accountBannerPortraitRoot = portraitRootImage.rectTransform;

		Image portraitGlow = CreateImage(
			"Account Portrait Glow", (Transform)(object)accountBannerPortraitRoot,
			new Color(0.48f, 0.12f, 0.82f, 0.34f));
		portraitGlow.sprite = AccardND.Battlefield.MmoUiTheme.GetRadialGlowSprite();
		portraitGlow.raycastTarget = false;
		SetRect(portraitGlow.rectTransform, new Vector2(-0.04f, -0.04f), new Vector2(1.04f, 1.04f));

		var maskObject = new GameObject(
			"Account Circular Portrait Mask",
			typeof(RectTransform),
			typeof(Image),
			typeof(Mask));
		maskObject.transform.SetParent((Transform)(object)accountBannerPortraitRoot, false);
		RectTransform maskRect = (RectTransform)maskObject.transform;
		SetRect(maskRect, new Vector2(0.145f, 0.145f), new Vector2(0.855f, 0.855f));
		Image maskImage = maskObject.GetComponent<Image>();
		maskImage.sprite = AccardND.Battlefield.MmoUiTheme.GetRadialGlowSprite();
		maskImage.color = Color.white;
		maskImage.raycastTarget = false;
		maskObject.GetComponent<Mask>().showMaskGraphic = false;

		accountBannerPortraitImage = CreateImage(
			"Account Portrait", maskObject.transform, Color.white);
		accountBannerPortraitImage.sprite = LoadSpriteResource(
			"UI/MultiplayerRestyle/multiplayer_hooded_avatar");
		accountBannerPortraitImage.preserveAspect = false;
		accountBannerPortraitImage.raycastTarget = false;
		Stretch(accountBannerPortraitImage.rectTransform);

		Image portraitFrame = CreateImage(
			"Account Gold Violet Frame", (Transform)(object)accountBannerPortraitRoot, Color.white);
		portraitFrame.sprite = LoadSpriteResource("UI/MultiplayerRestyle/avatar_frame");
		portraitFrame.preserveAspect = true;
		portraitFrame.raycastTarget = false;
		Stretch(portraitFrame.rectTransform);

		accountBannerNameText = CreateText(
			"Account Name", ((Component)accountBannerImage).transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont, 31, FontStyle.Normal, TextAnchor.LowerLeft);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(accountBannerNameText);
		accountBannerNameText.color = new Color(0.96f, 0.9f, 0.78f, 1f);
		accountBannerNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
		Outline nameOutline = ((Component)accountBannerNameText).gameObject.AddComponent<Outline>();
		nameOutline.effectColor = new Color(0f, 0f, 0f, 0.94f);
		nameOutline.effectDistance = new Vector2(2f, -2f);

		accountBannerLevelText = CreateText(
			"Account Level", ((Component)accountBannerImage).transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont, 24, FontStyle.Normal, TextAnchor.MiddleLeft);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(accountBannerLevelText);
		accountBannerLevelText.color = new Color(0.73f, 0.38f, 0.95f, 1f);
		Outline levelOutline = ((Component)accountBannerLevelText).gameObject.AddComponent<Outline>();
		levelOutline.effectColor = new Color(0f, 0f, 0f, 0.88f);
		levelOutline.effectDistance = new Vector2(1.5f, -1.5f);

		Image fillBack = CreateImage("Account Banner XP Back", ((Component)accountBannerImage).transform, new Color(0.03f, 0.018f, 0.055f, 0.86f));
		fillBack.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
		fillBack.type = Image.Type.Sliced;
		accountBannerExperienceFill = CreateImage("Account Banner XP Fill", ((Component)fillBack).transform, new Color(0.52f, 0.18f, 0.84f, 0.92f));
		accountBannerExperienceFill.type = Image.Type.Simple;
		accountBannerExperienceFill.rectTransform.anchorMin = Vector2.zero;
		accountBannerExperienceFill.rectTransform.anchorMax = new Vector2(0.015f, 1f);
		accountBannerExperienceFill.rectTransform.offsetMin = new Vector2(4f, 4f);
		accountBannerExperienceFill.rectTransform.offsetMax = new Vector2(-4f, -4f);

		accountBannerExperienceText = CreateText(
			"Account XP Text", ((Component)accountBannerImage).transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
		accountBannerExperienceText.color = new Color(0.92f, 0.86f, 0.98f, 1f);
		Outline xpOutline = ((Component)accountBannerExperienceText).gameObject.AddComponent<Outline>();
		xpOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
		xpOutline.effectDistance = new Vector2(1f, -1f);

		accountHeaderHubButton = CreateImageButton(
			"Account Header Hub Button", ((Component)accountBannerImage).transform,
			font, hubButtonSprite, string.Empty);
		AddHubButtonOutline(accountHeaderHubButton);
		((UnityEvent)accountHeaderHubButton.onClick).AddListener((UnityAction)delegate
		{
			if (IsAccountHubVisible())
			{
				return;
			}
			PlayGenericButtonClickSfx();
			if ((Object)(object)activePvpBootstrap != (Object)null)
			{
				activePvpBootstrap.CloseToHub();
				return;
			}
			ShowHubFromSinglePlayer();
		});

		accountHeaderSettingsButton = CreateImageButton(
			"Account Header Settings Button", ((Component)accountBannerImage).transform,
			font, accountHeaderSettingsSprite, string.Empty);
		AddHubButtonOutline(accountHeaderSettingsButton);
		((UnityEvent)accountHeaderSettingsButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ToggleOptionsPanel();
		});
	}

	private static void AddHubButtonOutline(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;

		AddHubGraphicOutline(button.targetGraphic);
	}

	private static void AddHubGraphicOutline(Graphic graphic)
	{
		if ((Object)(object)graphic == (Object)null)
			return;

		GameObject graphicObject = ((Component)graphic).gameObject;
		Outline outline = graphicObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
		outline.effectDistance = new Vector2(2.5f, -2.5f);
		outline.useGraphicAlpha = true;

		Shadow shadow = graphicObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
		shadow.effectDistance = new Vector2(4f, -4f);
		shadow.useGraphicAlpha = true;
	}

	private void RefreshAccountBannerLayout(bool landscape)
	{
		if ((Object)(object)accountBannerImage == (Object)null)
			return;

		RectTransform bannerRect = accountBannerImage.rectTransform;
		SetRect(
			bannerRect,
			new Vector2(0.015f, 0.885f),
			new Vector2(0.985f, 0.96f));
		bannerRect.offsetMin = Vector2.zero;
		bannerRect.offsetMax = Vector2.zero;
		((Component)accountBannerImage).transform.SetAsLastSibling();

		if ((Object)(object)accountBannerPortraitRoot != (Object)null)
			SetRect(
				accountBannerPortraitRoot,
				new Vector2(-0.012f, -0.28f),
				new Vector2(0.17f, 1.28f));
		SetRect(accountBannerNameText.rectTransform, new Vector2(0.18f, 0.54f), new Vector2(0.72f, 0.94f));
		SetRect(accountBannerLevelText.rectTransform, new Vector2(0.18f, 0.12f), new Vector2(0.26f, 0.48f));
		accountBannerLevelText.rectTransform.anchoredPosition = Vector2.zero;
		RectTransform experienceBackRect = (RectTransform)((Component)accountBannerExperienceFill).transform.parent;
		SetRect(experienceBackRect, new Vector2(0.26f, 0.19f), new Vector2(0.57f, 0.42f));
		experienceBackRect.anchoredPosition = Vector2.zero;
		SetRect(accountBannerExperienceText.rectTransform, new Vector2(0.26f, 0.19f), new Vector2(0.57f, 0.42f));
		if ((Object)(object)accountHeaderHubButton != (Object)null)
			SetRect(
				(RectTransform)((Component)accountHeaderHubButton).transform,
				new Vector2(0.80f, -0.12f), new Vector2(0.885f, 0.48f));
		if ((Object)(object)accountHeaderSettingsButton != (Object)null)
			SetRect(
				(RectTransform)((Component)accountHeaderSettingsButton).transform,
				new Vector2(0.90f, -0.12f), new Vector2(0.985f, 0.48f));
		accountBannerExperienceText.alignment = (TextAnchor)4;
		accountBannerNameText.fontSize = 31;
		accountBannerLevelText.fontSize = 24;
		accountBannerExperienceText.fontSize = 20;
		accountBannerNameText.resizeTextMaxSize = accountBannerNameText.fontSize;
		accountBannerLevelText.resizeTextMaxSize = accountBannerLevelText.fontSize;
		accountBannerExperienceText.resizeTextMaxSize = accountBannerExperienceText.fontSize;
	}

	private void RefreshAccountHoneyIndicatorLayout(bool landscape)
	{
		if ((Object)(object)accountHoneyPanelImage == (Object)null)
			return;

		RectTransform rect = accountHoneyPanelImage.rectTransform;
		rect.anchorMin = Vector2.one;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.localScale = Vector3.one;
		rect.localRotation = Quaternion.identity;
		rect.anchoredPosition = new Vector2(-232.7f, -196.6f);
		rect.sizeDelta = new Vector2(104f, 104f);
		((Component)accountHoneyPanelImage).transform.SetAsLastSibling();
		if ((Object)(object)accountHoneyAmountText != (Object)null)
		{
			RectTransform amountRect = accountHoneyAmountText.rectTransform;
			amountRect.anchorMin = Vector2.zero;
			amountRect.anchorMax = Vector2.one;
			amountRect.offsetMin = new Vector2(0f, -6.7f);
			amountRect.offsetMax = new Vector2(0f, -6.7f);
			accountHoneyAmountText.fontSize = 35;
			accountHoneyAmountText.resizeTextMaxSize = accountHoneyAmountText.fontSize;
		}
	}

	private void RefreshAccountBannerView()
	{
		if ((Object)(object)accountBannerImage == (Object)null)
			return;

		SinglePlayerProgressSave progress = singlePlayerProgressService.Progress;
		int level = Mathf.Max(1, progress.accountLevel);
		int currentXp = Mathf.Max(0, progress.accountExperience);
		int nextXp = Mathf.Max(1, progress.accountExperienceToNextLevel);
		string displayName = PlayerPrefs.GetString(PlayerHudNamePrefsKey, "Guest");

		accountBannerNameText.text = string.IsNullOrWhiteSpace(displayName) ? "Guest" : displayName.Trim();
		accountBannerLevelText.text = $"Lv. {level}";
		accountBannerExperienceText.text = $"{currentXp:n0} / {nextXp:n0}";
		float normalizedExperience = Mathf.Clamp01((float)currentXp / nextXp);
		accountBannerExperienceFill.rectTransform.anchorMax =
			new Vector2(Mathf.Max(normalizedExperience, 0.015f), 1f);
		UpdateAccountBannerInfoRows(progress);
		RefreshAccountHoneyPanelView(progress);
	}

	private void RefreshAccountHoneyPanelView(SinglePlayerProgressSave progress)
	{
		if ((Object)(object)accountHoneyAmountText != (Object)null)
			accountHoneyAmountText.text = Mathf.Max(0, progress?.honey ?? 0).ToString("n0");
	}

	private void SetAccountHubHudActive(bool active)
	{
		if (active
			&& (Object)(object)accountHoneyPanelImage == (Object)null
			&& (Object)(object)accountBannerImage != (Object)null)
		{
			Font honeyFont = AccardND.Battlefield.MmoUiTheme.BodyBoldFont
				?? AccardND.Battlefield.MmoUiTheme.BodyFont;
			CreateAccountHoneyIndicator(((Component)accountBannerImage).transform.parent, honeyFont);
			RefreshAccountHoneyIndicatorLayout(Screen.width > Screen.height);
		}
		if ((Object)(object)accountBannerImage != (Object)null)
		{
			((Component)accountBannerImage).gameObject.SetActive(active);
			if (active)
			{
				SetDeckBuilderAccountHeaderMode(false);
				((Component)accountBannerImage).transform.SetAsLastSibling();
			}
		}
		if ((Object)(object)accountHoneyPanelImage != (Object)null)
		{
			if (active)
			{
				RefreshAccountHoneyIndicatorLayout(Screen.width > Screen.height);
				Canvas honeyCanvas = ((Component)accountHoneyPanelImage).GetComponent<Canvas>();
				if ((Object)(object)honeyCanvas == (Object)null)
				{
					honeyCanvas = ((Component)accountHoneyPanelImage).gameObject.AddComponent<Canvas>();
				}
				honeyCanvas.overrideSorting = true;
				honeyCanvas.sortingOrder = 911;
				accountHoneyPanelImage.sprite = LoadHoneyPotCurrencySprite();
				accountHoneyPanelImage.preserveAspect = true;
			}
			((Component)accountHoneyPanelImage).gameObject.SetActive(active);
			if (active)
				((Component)accountHoneyPanelImage).transform.SetAsLastSibling();
		}
		if (active)
			RefreshAccountBannerView();
		if ((Object)(object)logButton != (Object)null)
			((Component)logButton).gameObject.SetActive(!active);
		if ((Object)(object)settingsButtonLabel != (Object)null)
			((Component)settingsButtonLabel).gameObject.SetActive(!active);
		if ((Object)(object)accountHeaderHubButton != (Object)null)
			accountHeaderHubButton.interactable = active && !IsAccountHubVisible();
	}

	private void SetDeckBuilderAccountHeaderMode(bool active)
	{
		if ((Object)(object)accountBannerPortraitRoot != (Object)null)
			((Component)accountBannerPortraitRoot).gameObject.SetActive(!active);
		if ((Object)(object)accountBannerNameText != (Object)null)
			((Component)accountBannerNameText).gameObject.SetActive(!active);
		if ((Object)(object)accountBannerLevelText != (Object)null)
			((Component)accountBannerLevelText).gameObject.SetActive(!active);
		if ((Object)(object)accountBannerExperienceFill != (Object)null)
			((Component)accountBannerExperienceFill).transform.parent.gameObject.SetActive(!active);
		if ((Object)(object)accountBannerExperienceText != (Object)null)
			((Component)accountBannerExperienceText).gameObject.SetActive(!active);
		if ((Object)(object)accountHeaderHubButton != (Object)null)
			((Component)accountHeaderHubButton).gameObject.SetActive(!active);
		if ((Object)(object)accountHoneyPanelImage != (Object)null)
			((Component)accountHoneyPanelImage).gameObject.SetActive(!active);
	}

	private bool IsAccountHubVisible()
	{
		return (Object)(object)modeSelectionPanel != (Object)null
			&& modeSelectionPanel.activeInHierarchy
			&& campaignHubZoomRoutine == null;
	}

	private void UpdateAccountBannerInfoRows(SinglePlayerProgressSave progress)
	{
		SetAccountBannerInfoRow(0, $"Capitolo {GetHighestUnlockedChapterNumber(progress)}");
		SetAccountBannerInfoRow(1, $"Lega {GetCachedPvpLeagueLabel()}");
		SetAccountBannerInfoRow(2, $"Collezione {CalculateCollectionPercent(progress)}%");
	}

	private void SetAccountBannerInfoRow(int index, string label)
	{
		if (index < 0 || index >= accountBannerInfoTexts.Length)
			return;
		Text text = accountBannerInfoTexts[index];
		if ((Object)(object)text != (Object)null)
			text.text = label;
	}

	private static int GetHighestUnlockedChapterNumber(SinglePlayerProgressSave progress)
	{
		int highest = 0;
		if (progress?.unlockedChapters == null)
			return highest;
		foreach (string chapterId in progress.unlockedChapters)
		{
			if (TryParseTrailingNumber(chapterId, out int number))
				highest = Mathf.Max(highest, number);
		}
		return highest;
	}

	private static string GetCachedPvpLeagueLabel()
	{
		string tier = PlayerPrefs.GetString("AccardND.PvpTier", string.Empty);
		string division = PlayerPrefs.GetString("AccardND.PvpDivision", string.Empty);
		if (string.IsNullOrWhiteSpace(tier))
			return "Non classificato";
		return string.IsNullOrWhiteSpace(division) ? tier.Trim() : $"{tier.Trim()} {division.Trim()}";
	}

	private static int CalculateCollectionPercent(SinglePlayerProgressSave progress)
	{
		if (progress == null)
			return 0;
		int unlocked = CountDistinct(progress.unlockedClasses)
			+ CountDistinct(progress.unlockedScenarios)
			+ CountDistinct(progress.unlockedSecondAbilities)
			+ (progress.hardcoreUnlocked ? 1 : 0);
		const int totalClasses = 9;
		const int totalScenarios = 4;
		const int totalSecondAbilities = 9;
		const int totalHardcoreUnlock = 1;
		const int totalCollectibles = totalClasses + totalScenarios + totalSecondAbilities + totalHardcoreUnlock;
		return Mathf.Clamp(Mathf.RoundToInt(unlocked * 100f / totalCollectibles), 0, 100);
	}

	private static int CountDistinct(List<string> values)
	{
		if (values == null || values.Count == 0)
			return 0;
		return values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Count();
	}

	private static bool TryParseTrailingNumber(string value, out int number)
	{
		number = 0;
		if (string.IsNullOrWhiteSpace(value))
			return false;
		int end = value.Length - 1;
		while (end >= 0 && char.IsDigit(value[end]))
			end--;
		if (end == value.Length - 1)
			return false;
		return int.TryParse(value.Substring(end + 1), out number);
	}

	private static Button CreateHubBannerButton(string name, Transform parent, Font font, string spriteResource, string label, Vector2 labelOffset = default)
	{
		Image image = CreateImage(name, parent, Color.white);
		image.sprite = LoadSpriteResource(spriteResource);
		image.preserveAspect = true;
		image.raycastTarget = false;
		Button button = ((Component)image).gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
		colors.pressedColor = new Color(0.82f, 0.86f, 0.92f, 1f);
		colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.72f);
		colors.colorMultiplier = 1f;
		button.colors = colors;
		AccardND.Battlefield.MmoUiTheme.AddMotion(button);
		AddHubButtonOutline(button);

		Font labelFont = AccardND.Battlefield.MmoUiTheme.LoreFont;
		if ((Object)(object)labelFont == (Object)null)
		{
			labelFont = font;
		}
		Text text = CreateText("Label", ((Component)image).transform, labelFont, 24, FontStyle.Normal, (TextAnchor)4);
		text.text = label;
		text.color = new Color(1f, 0.84f, 0.16f, 1f);
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = AccardND.Battlefield.MmoUiTheme.LoreFontMinSize;
		text.resizeTextMaxSize = 24;
		Outline outline = ((Component)text).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
		outline.effectDistance = new Vector2(2f, -2f);
		SetRect(text.rectTransform, new Vector2(0.14f, 0.25f), new Vector2(0.86f, 0.76f));
		text.rectTransform.anchoredPosition += labelOffset;
		return button;
	}

	private static string GetCurrentHubBackgroundResource()
	{
		return IsCurrentHubNight() ?"UI/Hub/bg_dark_hub" : "UI/Hub/bg_light_hub";
	}

	private static bool IsCurrentHubNight()
	{
		DateTime now = DateTime.Now;
		int hour = now.Hour;
		bool night = hour >= 19 || hour <= 7;
		if (hour == 7 && now.Minute > 0)
		{
			night = false;
		}
		return night;
	}

	private void StartTutorial()
	{
		StartTutorial(true);
	}

	private void StartTutorialFromOptions()
	{
		if ((Object)(object)optionsPanel != (Object)null)
		{
			CloseOptionsPanel();
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
		bool landscape = Screen.width > Screen.height;
		Sprite sprite = LoadSpriteResource(landscape ?$"UI/tutorial-{page}_landscape" : $"UI/tutorial-{page}");
		if ((Object)(object)sprite == (Object)null && landscape)
		{
			sprite = LoadSpriteResource($"UI/tutorial-{page}");
		}
		modeSelectionImage.sprite = sprite;
		if ((Object)(object)modeSelectionAspectFitter != (Object)null)
		{
			modeSelectionAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			modeSelectionAspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : (landscape ?1672f / 941f : 941f / 1672f);
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
		if ((Object)(object)modeSelectionSanctuaryButton != (Object)null)
		{
			((Component)modeSelectionSanctuaryButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionLibraryButton != (Object)null)
		{
			((Component)modeSelectionLibraryButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionShopButton != (Object)null)
		{
			((Component)modeSelectionShopButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionTavernButton != (Object)null)
		{
			((Component)modeSelectionTavernButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionProfileButton != (Object)null)
		{
			((Component)modeSelectionProfileButton).gameObject.SetActive(active);
		}
		if ((Object)(object)modeSelectionHallOfFameButton != (Object)null)
		{
			((Component)modeSelectionHallOfFameButton).gameObject.SetActive(active);
		}
		for (int index = 0; index < modeSelectionHotspotButtons.Count; index++)
		{
			Button hotspot = modeSelectionHotspotButtons[index];
			if ((Object)(object)hotspot != (Object)null)
			{
				((Component)hotspot).gameObject.SetActive(active);
			}
		}
		SetAccountHubHudActive(active);
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
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(image2.rectTransform, "Dialog Crest", new Vector2(0.5f, 1f), new Vector2(42f, 42f), Color.white);
		SetRect(image2.rectTransform, new Vector2(0.13f, 0.39f), new Vector2(0.87f, 0.59f));
		Text text = CreateText("Title", ((Component)image2).transform, font, 35, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text);
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

	private void ShowUnderDevelopmentPopup()
	{
		if ((Object)(object)multiplayerPopup == (Object)null)
		{
			return;
		}
		multiplayerPopup.SetActive(true);
		multiplayerPopup.transform.SetAsLastSibling();
	}

	private void ShowHallOfFame()
	{
		StartPvpMode(openLeaderboard: true);
	}

	private void ShowHubFromSinglePlayer()
	{
		if (campaignHubZoomRoutine != null)
		{
			StopCoroutine(campaignHubZoomRoutine);
			campaignHubZoomRoutine = null;
		}
		DestroyCampaignHubCinematicOverlay();
		// Home e' anche il punto di recupero della navigazione: nessun overlay aperto
		// deve sopravvivere al ritorno all'hub, altrimenti puo' restare invisibile dietro
		// altri pannelli continuando pero' a intercettare tutti i raycast.
		CloseOptionsPanel();
		if ((Object)(object)logPanel != (Object)null)
		{
			logPanel.SetActive(false);
		}
		if ((Object)(object)multiplayerPopup != (Object)null)
		{
			multiplayerPopup.SetActive(false);
		}
		if ((Object)(object)returnToMenuConfirmPanel != (Object)null)
		{
			returnToMenuConfirmPanel.SetActive(false);
		}
		if ((Object)(object)auraCodexPanel != (Object)null)
		{
			auraCodexPanel.SetActive(false);
		}
		if ((Object)(object)sanctuaryConfirmPopup != (Object)null)
		{
			sanctuaryConfirmPopup.SetActive(false);
		}
		if ((Object)(object)implementationArchivePanel != (Object)null
			|| (Object)(object)implementationArchiveBackdropPanel != (Object)null)
		{
			SetImplementationArchiveVisible(false);
		}
		if ((Object)(object)cardInspectionPanel != (Object)null && cardInspectionPanel.activeSelf)
		{
			CloseCardInspection(playSfx: false);
		}
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}
		if ((Object)(object)adventureTutorialConfirmPopup != (Object)null)
		{
			adventureTutorialConfirmPopup.SetActive(false);
		}
		if ((Object)(object)sanctuaryPanel != (Object)null)
		{
			sanctuaryPanel.SetActive(false);
		}
		if ((Object)(object)tavernPanel != (Object)null)
		{
			tavernPanel.SetActive(false);
		}
		if ((Object)(object)libraryPanel != (Object)null)
		{
			libraryPanel.SetActive(false);
		}
		if ((Object)(object)shopPanel != (Object)null)
		{
			shopPanel.SetActive(false);
		}
		if ((Object)(object)profilePanel != (Object)null)
		{
			profilePanel.SetActive(false);
			CoolProfileAds();
		}
		if ((Object)(object)guidedTutorialPanel != (Object)null)
		{
			guidedTutorialPanel.SetActive(false);
		}
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			adventureScriptedTutorialPanel.SetActive(false);
		}
		ResetHubZoomTransform();
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(true);
			modeSelectionPanel.transform.SetAsLastSibling();
		}
		modeSelectionTutorialActive = false;
		tutorialPageIndex = 0;
		SetModeSelectionButtonsActive(true);
		SetModeSelectionButtonsInteractable(true);
		SetAccountHubHudActive(true);
		PlayCurrentHubMusic();
		TryShowLevelUpRewardPopup();
		RefreshModeSelectionLayout();
		_ = RefreshTavernNotificationBadgeAsync();
		_ = LoadPendingAdRewardsAsync();
		inputLocked = true;
	}

	private void ResetHubZoomTransform()
	{
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			RectTransform hubRect = (RectTransform)modeSelectionPanel.transform;
			hubRect.localScale = Vector3.one;
			hubRect.anchoredPosition = Vector2.zero;
		}
		if ((Object)(object)modeSelectionAspectFitter != (Object)null)
		{
			((Behaviour)modeSelectionAspectFitter).enabled = true;
		}
	}

	private void ShowModeSelection()
	{
		inputLocked = true;
		modeSelectionTutorialActive = false;
		tutorialPageIndex = 0;
		ResetHubZoomTransform();
		RefreshModeSelectionLayout();
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(true);
		}
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		SetModeSelectionButtonsActive(true);
		SetAccountHubHudActive(true);
		PlayCurrentHubMusic();
		_ = RefreshTavernNotificationBadgeAsync();
		_ = LoadPendingAdRewardsAsync();
		if ((Object)(object)tutorialAdvanceButton != (Object)null)
		{
			((Component)tutorialAdvanceButton).gameObject.SetActive(false);
		}
		if ((Object)(object)multiplayerPopup != (Object)null)
		{
			multiplayerPopup.SetActive(false);
		}
		if (debugMerchantScene)
		{
			StartMerchantDebugRun();
		}
		else if (debugClassChoiceScene)
		{
			StartClassChoiceDebug();
		}
		else if (debugLootRoomScene)
		{
			StartLootRoomDebugRun();
		}
		else if (debugForceFirstRoomComposableGolem || debugForceFirstRoomMedusa || debugForceFirstRoomTrentor || debugForceFirstRoomBragus || debugForceFirstRoomPalatir)
		{
			StartCampaignMode();
		}
	}

	private void StartCampaignMode()
	{
		if ((Object)(object)modeSelectionPanel != (Object)null && modeSelectionPanel.activeInHierarchy)
		{
			PlayHubZoomThenOpen(modeSelectionCampaignButton, OpenCampaignModeSelection);
			return;
		}
		OpenCampaignModeSelection();
	}

	private void OpenCampaignModeSelection()
	{
		if ((Object)(object)roomTransition != (Object)null && !roomTransition.IsPlaying)
		{
			AnimationConfiguration animation = configuration.Animation;
			PlayTransitionSfx();
			roomTransition.Play(ShowCampaignModeSelection, animation.RoomFadeOutDuration, animation.RoomBlackHoldDuration, animation.RoomFadeInDuration);
			return;
		}
		ShowCampaignModeSelection();
	}

	private void PlayHubZoomThenOpen(Button focusButton, Action onComplete)
	{
		if ((Object)(object)modeSelectionPanel == (Object)null || !modeSelectionPanel.activeInHierarchy)
		{
			onComplete?.Invoke();
			return;
		}
		if (campaignHubZoomRoutine == null)
		{
			campaignHubZoomRoutine = StartCoroutine(PlayCampaignHubZoomThenOpen(focusButton, onComplete));
		}
	}

	private IEnumerator PlayCampaignHubZoomThenOpen(Button focusButton, Action onComplete)
	{
		RectTransform hubRect = (RectTransform)modeSelectionPanel.transform;
		// L'AspectRatioFitter in EnvelopeParent pilota anchoredPosition (la riazzera a ogni layout):
		// va sospeso per la durata della cinematica, altrimenti la panoramica verso l'hotspot
		// viene annullata e resta solo lo zoom sul centro.
		bool aspectFitterWasEnabled = (Object)(object)modeSelectionAspectFitter != (Object)null && ((Behaviour)modeSelectionAspectFitter).enabled;
		if (aspectFitterWasEnabled)
		{
			((Behaviour)modeSelectionAspectFitter).enabled = false;
		}
		Vector3 originalScale = hubRect.localScale;
		Vector2 originalPosition = hubRect.anchoredPosition;
		DestroyCampaignHubCinematicOverlay();
		campaignHubCinematicOverlay = CreateCampaignHubCinematicOverlay(((Component)hubRect).transform.parent, out CanvasGroup cinematicGroup, out Image vignette, out RectTransform topBar, out RectTransform bottomBar, out D20WireframeGraphic die, out RectTransform dieRect);
		SetModeSelectionButtonsInteractable(false);
		PlayTransitionSfx();

		bool landscape = Screen.width > Screen.height;
		Vector2 portalFocus = GetHubZoomFocus(focusButton, hubRect, landscape);
		Vector2 size = hubRect.rect.size;
		float zoomScale = 1.58f;
		// Porta l'hotspot al centro dello schermo, non semplicemente ingrandisce il centro dell'hub.
		Vector2 zoomPosition = new Vector2(
			(0.5f - portalFocus.x) * size.x * zoomScale,
			(0.5f - portalFocus.y) * size.y * zoomScale);
		// Senza limite la panoramica scoprirebbe i bordi oltre l'artwork.
		RectTransform hubParentRect = hubRect.parent as RectTransform;
		Vector2 viewportSize = (Object)(object)hubParentRect != (Object)null ? hubParentRect.rect.size : size;
		float maximumPanX = Mathf.Max(0f, (size.x * zoomScale - viewportSize.x) * 0.5f);
		float maximumPanY = Mathf.Max(0f, (size.y * zoomScale - viewportSize.y) * 0.5f);
		zoomPosition = new Vector2(
			Mathf.Clamp(zoomPosition.x, 0f - maximumPanX, maximumPanX),
			Mathf.Clamp(zoomPosition.y, 0f - maximumPanY, maximumPanY));

		yield return AnimateCampaignHubCinematicOverlay(cinematicGroup, vignette, topBar, bottomBar, die, dieRect, 0f, 0.82f, 0f, 1f, 0f, -55f, 0f, 0f, 0.78f, 1f, 0.2f);
		yield return AnimateHubZoom(hubRect, originalScale, originalPosition, Vector3.one * zoomScale, zoomPosition, 0.48f, die, dieRect, -55f, -210f, 0f, 0f, 1f, 1.08f);
		yield return AnimateCampaignHubCinematicOverlay(cinematicGroup, vignette, topBar, bottomBar, die, dieRect, 0.82f, 0.55f, 1f, 0.92f, -210f, -210f, 0f, 0f, 1.08f, 0.94f, 0.08f);
		yield return AnimateHubZoom(hubRect, Vector3.one * zoomScale, zoomPosition, Vector3.one * 0.96f, originalPosition, 0.34f, die, dieRect, -210f, -40f, 0f, 0f, 0.94f, 1f);
		yield return AnimateCampaignHubCinematicOverlay(cinematicGroup, vignette, topBar, bottomBar, die, dieRect, 0.55f, 1f, 0.92f, 1f, -40f, 95f, 0f, 1f, 1f, 1.42f, 0.16f);

		hubRect.localScale = originalScale;
		hubRect.anchoredPosition = originalPosition;
		if (aspectFitterWasEnabled && (Object)(object)modeSelectionAspectFitter != (Object)null)
		{
			((Behaviour)modeSelectionAspectFitter).enabled = true;
		}
		DestroyCampaignHubCinematicOverlay();
		campaignHubZoomRoutine = null;
		SetModeSelectionButtonsInteractable(true);
		onComplete?.Invoke();
	}

	private Vector2 GetHubZoomFocus(Button focusButton, RectTransform hubRect, bool landscape)
	{
		if ((Object)(object)focusButton == (Object)null)
			return landscape ?new Vector2(0.5f, 0.53f) : new Vector2(0.5f, 0.675f);

		// La località vera è l'hotspot disegnato sullo sfondo: il pulsante banner sta altrove,
		// quindi la camera deve puntare al centro dell'hotspot associato.
		if (modeSelectionHotspotRects.TryGetValue(focusButton, out RectTransform hotspotRect)
			&& (Object)(object)hotspotRect != (Object)null)
		{
			return GetHubRectCenterNormalized(hotspotRect, hubRect);
		}

		return GetHubRectCenterNormalized((RectTransform)((Component)focusButton).transform, hubRect);
	}

	private static Vector2 GetHubRectCenterNormalized(RectTransform rect, RectTransform hubRect)
	{
		if ((Object)(object)rect == (Object)null || (Object)(object)hubRect == (Object)null)
			return new Vector2(0.5f, 0.5f);

		Vector3[] corners = new Vector3[4];
		rect.GetWorldCorners(corners);
		Vector3 worldCenter = Vector3.Lerp(corners[0], corners[2], 0.5f);
		Vector2 localPoint = hubRect.InverseTransformPoint(worldCenter);
		Rect hub = hubRect.rect;
		return new Vector2(
			Mathf.Clamp01(Mathf.InverseLerp(hub.xMin, hub.xMax, localPoint.x)),
			Mathf.Clamp01(Mathf.InverseLerp(hub.yMin, hub.yMax, localPoint.y)));
	}

	private GameObject CreateCampaignHubCinematicOverlay(
		Transform parent,
		out CanvasGroup group,
		out Image vignette,
		out RectTransform topBar,
		out RectTransform bottomBar,
		out D20WireframeGraphic die,
		out RectTransform dieRect)
	{
		GameObject overlay = new GameObject("Campaign Hub Cinematic Fade", new Type[3]
		{
			typeof(RectTransform),
			typeof(Canvas),
			typeof(CanvasGroup)
		});
		overlay.transform.SetParent(parent, false);
		Stretch((RectTransform)overlay.transform);
		Canvas canvas = overlay.GetComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 950;
		group = overlay.GetComponent<CanvasGroup>();
		group.alpha = 0f;
		group.blocksRaycasts = false;
		group.interactable = false;

		Image fade = CreateImage("Fade", overlay.transform, new Color(0f, 0f, 0f, 0.72f));
		fade.raycastTarget = false;
		Stretch(fade.rectTransform);

		vignette = CreateImage("Vignette", overlay.transform, new Color(0.01f, 0.014f, 0.02f, 0.46f));
		vignette.raycastTarget = false;
		Stretch(vignette.rectTransform);

		Image top = CreateImage("Top Letterbox", overlay.transform, Color.black);
		top.raycastTarget = false;
		topBar = top.rectTransform;
		SetRect(topBar, new Vector2(0f, 0.89f), new Vector2(1f, 1f));
		topBar.anchoredPosition = new Vector2(0f, 120f);

		Image bottom = CreateImage("Bottom Letterbox", overlay.transform, Color.black);
		bottom.raycastTarget = false;
		bottomBar = bottom.rectTransform;
		SetRect(bottomBar, new Vector2(0f, 0f), new Vector2(1f, 0.11f));
		bottomBar.anchoredPosition = new Vector2(0f, -120f);

		GameObject dieObject = new GameObject("Transparent D20 Wireframe", new Type[2]
		{
			typeof(RectTransform),
			typeof(D20WireframeGraphic)
		});
		dieObject.transform.SetParent(overlay.transform, false);
		dieRect = (RectTransform)dieObject.transform;
		SetRect(dieRect, new Vector2(0.31f, 0.31f), new Vector2(0.69f, 0.69f));
		dieRect.localScale = Vector3.one * 0.78f;
		die = dieObject.GetComponent<D20WireframeGraphic>();
		die.color = new Color(0f, 0f, 0f, 0.94f);
		die.LineThickness = 7f;
		die.raycastTarget = false;

		overlay.transform.SetAsLastSibling();
		return overlay;
	}

	private void DestroyCampaignHubCinematicOverlay()
	{
		if ((Object)(object)campaignHubCinematicOverlay != (Object)null)
		{
			Object.Destroy(campaignHubCinematicOverlay);
			campaignHubCinematicOverlay = null;
		}
	}

	private static IEnumerator AnimateCampaignHubCinematicOverlay(
		CanvasGroup group,
		Image vignette,
		RectTransform topBar,
		RectTransform bottomBar,
		D20WireframeGraphic die,
		RectTransform dieRect,
		float fromAlpha,
		float toAlpha,
		float fromBar,
		float toBar,
		float fromRotation,
		float toRotation,
		float fromExplosion,
		float toExplosion,
		float fromDieScale,
		float toDieScale,
		float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
			t = 1f - Mathf.Pow(1f - t, 3f);
			float alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, t);
			float barT = Mathf.LerpUnclamped(fromBar, toBar, t);
			float dieScale = Mathf.LerpUnclamped(fromDieScale, toDieScale, t);
			group.alpha = alpha;
			if ((Object)(object)vignette != (Object)null)
			{
				Color color = vignette.color;
				color.a = Mathf.LerpUnclamped(0.18f, 0.54f, barT);
				vignette.color = color;
			}
			if ((Object)(object)topBar != (Object)null)
				topBar.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(120f, 0f, barT));
			if ((Object)(object)bottomBar != (Object)null)
				bottomBar.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-120f, 0f, barT));
			if ((Object)(object)die != (Object)null)
			{
				die.RotationDegrees = Mathf.LerpUnclamped(fromRotation, toRotation, t);
				die.Explosion = Mathf.LerpUnclamped(fromExplosion, toExplosion, t);
			}
			if ((Object)(object)dieRect != (Object)null)
				dieRect.localScale = Vector3.one * dieScale;
			yield return null;
		}
		group.alpha = toAlpha;
		if ((Object)(object)topBar != (Object)null)
			topBar.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(120f, 0f, toBar));
		if ((Object)(object)bottomBar != (Object)null)
			bottomBar.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-120f, 0f, toBar));
		if ((Object)(object)die != (Object)null)
		{
			die.RotationDegrees = toRotation;
			die.Explosion = toExplosion;
		}
		if ((Object)(object)dieRect != (Object)null)
			dieRect.localScale = Vector3.one * toDieScale;
	}

	private static IEnumerator AnimateHubZoom(
		RectTransform rect,
		Vector3 fromScale,
		Vector2 fromPosition,
		Vector3 toScale,
		Vector2 toPosition,
		float duration,
		D20WireframeGraphic die = null,
		RectTransform dieRect = null,
		float fromRotation = 0f,
		float toRotation = 0f,
		float fromExplosion = 0f,
		float toExplosion = 0f,
		float fromDieScale = 1f,
		float toDieScale = 1f)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
			t = t * t * (3f - 2f * t);
			rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
			rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
			if ((Object)(object)die != (Object)null)
			{
				die.RotationDegrees = Mathf.LerpUnclamped(fromRotation, toRotation, t);
				die.Explosion = Mathf.LerpUnclamped(fromExplosion, toExplosion, t);
			}
			if ((Object)(object)dieRect != (Object)null)
				dieRect.localScale = Vector3.one * Mathf.LerpUnclamped(fromDieScale, toDieScale, t);
			yield return null;
		}
		rect.localScale = toScale;
		rect.anchoredPosition = toPosition;
		if ((Object)(object)die != (Object)null)
		{
			die.RotationDegrees = toRotation;
			die.Explosion = toExplosion;
		}
		if ((Object)(object)dieRect != (Object)null)
			dieRect.localScale = Vector3.one * toDieScale;
	}

	private void SetModeSelectionButtonsInteractable(bool interactable)
	{
		SetButtonInteractable(modeSelectionCampaignButton, interactable);
		SetButtonInteractable(modeSelectionMultiplayerButton, interactable);
		SetButtonInteractable(modeSelectionTutorialButton, interactable);
		SetButtonInteractable(modeSelectionSanctuaryButton, interactable);
		SetButtonInteractable(modeSelectionLibraryButton, interactable);
		SetButtonInteractable(modeSelectionShopButton, interactable);
		SetButtonInteractable(modeSelectionTavernButton, interactable);
		SetButtonInteractable(modeSelectionProfileButton, interactable);
		SetButtonInteractable(modeSelectionHallOfFameButton, interactable);
		for (int index = 0; index < modeSelectionHotspotButtons.Count; index++)
		{
			SetButtonInteractable(modeSelectionHotspotButtons[index], interactable);
		}
	}

	private static void SetButtonInteractable(Button button, bool interactable)
	{
		if ((Object)(object)button != (Object)null)
		{
			button.interactable = interactable;
		}
	}

	private void ShowCampaignModeSelection()
	{
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		SetAccountHubHudActive(true);
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(true);
			campaignModeSelectionPanel.transform.SetAsLastSibling();
		}
		RefreshSinglePlayerProgressView();
		RefreshCampaignModeSelectionLayout();
		inputLocked = true;
		_ = EnsureServerProgressAsync();
	}

	/// <summary>
	/// Stabilisce (una volta) la connessione autoritativa al server per la progressione single
	/// player e specchia lo stato nel servizio locale usato dalla UI. Se il server non e'
	/// raggiungibile o rifiuta il login, resta attivo il servizio locale senza errori a video.
	/// </summary>
	private async System.Threading.Tasks.Task<bool> EnsureServerProgressAsync()
	{
		if (!ServerProgressEnabled)
		{
			return false;
		}
		if (ServerProgressReady)
		{
			return true;
		}
		// serverProgress non-null ma link non pronto (connessione caduta): si tenta la riconnessione.
		serverProgress = null;
		if ((Object)(object)singlePlayerServerLink == (Object)null)
		{
			singlePlayerServerLink = gameObject.AddComponent<AccardND.Network.SinglePlayerServerLink>();
			singlePlayerServerLink.Reconnected += HandleServerProgressReconnected;
		}
		AccardND.Network.ServerSinglePlayerProgressRepository repository =
			await singlePlayerServerLink.EnsureRepositoryAsync();
		if (repository == null)
		{
			return false;
		}
		serverProgress = repository;
		MirrorServerProgress();
		RefreshSinglePlayerProgressView();
		TryShowLevelUpRewardPopup();
		if ((Object)(object)adventureChapterPanel != (Object)null && adventureChapterPanel.activeSelf)
		{
			RefreshAdventureChapterList();
		}
		return true;
	}

	private void HandleServerProgressReconnected()
	{
		_ = RefreshCampaignPanelsAfterReconnectAsync();
	}

	private async System.Threading.Tasks.Task RefreshCampaignPanelsAfterReconnectAsync()
	{
		if (!await EnsureServerProgressAsync())
			return;
		MirrorServerProgress();
		RefreshAccountBannerView();
		TryShowLevelUpRewardPopup();
		if ((Object)(object)sanctuaryPanel != (Object)null && sanctuaryPanel.activeSelf)
			LoadSanctuaryFromServer();
		if ((Object)(object)shopPanel != (Object)null && shopPanel.activeSelf)
			LoadShopFromServer();
		if ((Object)(object)tavernPanel != (Object)null && tavernPanel.activeSelf)
			await RefreshTavernFromServerAsync();
		else if ((Object)(object)modeSelectionPanel != (Object)null && modeSelectionPanel.activeSelf)
			await RefreshTavernNotificationBadgeAsync();
		// La riconnessione e' il momento in cui i triplicatori saltati diventano riscuotibili:
		// e' la caduta di rete a fine run che li ha lasciati in sospeso, ed e' qui che il
		// giocatore deve vedere comparire la comunicazione.
		await LoadPendingAdRewardsAsync();
	}

	/// <summary>Copia lo stato autoritativo del server nella cache locale letta dalla UI.</summary>
	private void MirrorServerProgress()
	{
		if (serverProgress != null)
		{
			singlePlayerProgressService.ApplyAuthoritative(serverProgress.Progress);
		}
	}

	private void RefreshSinglePlayerProgressView()
	{
		if ((Object)(object)campaignModeHardcoreButtonText != (Object)null)
		{
			campaignModeHardcoreButtonText.text = "HARDCORE";
		}
		if ((Object)(object)campaignModeHardcoreEmblemImage != (Object)null)
		{
			campaignModeHardcoreEmblemImage.sprite = LoadSpriteResource(
				singlePlayerProgressService.HardcoreUnlocked
					? CampaignHardcoreEmblemResource
					: CampaignHardcoreLockedEmblemResource);
		}
		if ((Object)(object)campaignModeHardcoreVfx != (Object)null)
		{
			((Component)campaignModeHardcoreVfx).gameObject.SetActive(
				singlePlayerProgressService.HardcoreUnlocked);
		}
		if ((Object)(object)campaignModeHardcoreButton != (Object)null)
		{
			campaignModeHardcoreButton.interactable = singlePlayerProgressService.HardcoreUnlocked
				|| singlePlayerProgressService.Honey >= HardcoreUnlockHoneyCost;
		}
		RefreshAccountBannerView();
		ShowPendingClassChoiceIfAny();
	}

	private void CreateClassChoicePopup(Transform parent, Font font)
	{
		Image overlay = CreateImage("Class Choice Popup", parent, new Color(0f, 0f, 0f, 0.82f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		classChoicePopup = ((Component)overlay).gameObject;
		Canvas canvas = classChoicePopup.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 950;
		classChoicePopup.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Class Choice Dialog", ((Component)overlay).transform,
			new Color(0.012f, 0.018f, 0.032f, 0.99f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		SetRect(dialog.rectTransform, new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.78f));

		Text title = CreateText("Class Choice Title", ((Component)dialog).transform,
			AccardND.Battlefield.MmoUiTheme.LoreFont, 38, FontStyle.Normal, TextAnchor.MiddleCenter);
		title.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Adventure.ClassChoiceTitle,
			"SCEGLI UNA NUOVA CLASSE");
		title.color = new Color(0.95f, 0.79f, 0.34f);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		SetRect(title.rectTransform, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.94f));

		Text body = CreateText("Class Choice Body", ((Component)dialog).transform, font, 23,
			FontStyle.Normal, TextAnchor.MiddleCenter);
		body.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Adventure.ClassChoiceBody,
			"Hai completato il primo scenario. La classe scelta verra sbloccata permanentemente.");
		body.color = new Color(0.86f, 0.92f, 0.96f);
		body.horizontalOverflow = HorizontalWrapMode.Wrap;
		SetRect(body.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.78f));

		classChoiceButtonsRoot = new GameObject("Class Choice Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
		classChoiceButtonsRoot.SetParent(((Component)dialog).transform, false);
		SetRect(classChoiceButtonsRoot, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.6f));

		classChoiceStatusText = CreateText("Class Choice Status", ((Component)dialog).transform, font, 19,
			FontStyle.Normal, TextAnchor.MiddleCenter);
		classChoiceStatusText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(classChoiceStatusText.rectTransform, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.22f));
		classChoicePopup.SetActive(false);
	}

	private void StartAdventureMode()
	{
		ShowAdventureChapterSelection();
	}

	private void StartTutorialAdventureStage()
	{
		ShowAdventureTutorialConfirmPopup();
	}

	private void ShowAdventureTutorialConfirmPopup()
	{
		if ((Object)(object)adventureTutorialConfirmPopup == (Object)null)
		{
			ConfirmStartTutorialAdventureStage();
			return;
		}
		if ((Object)(object)adventureTutorialConfirmTitleText != (Object)null)
		{
			adventureTutorialConfirmTitleText.text = "TUTORIAL: PRIMI PASSI";
		}
		if ((Object)(object)adventureTutorialConfirmBodyText != (Object)null)
		{
			adventureTutorialConfirmBodyText.text = "Entrerai in uno stage guidato: ti verra indicato cosa toccare, i tiri saranno controllati e ogni passo spieghera le basi del gioco. Al completamento ricevi le classi base e il primo capitolo.";
		}
		adventureConfirmAction = StartAdventureScriptedTutorial;
		adventureTutorialConfirmPopup.SetActive(true);
		adventureTutorialConfirmPopup.transform.SetAsLastSibling();
	}

	private void ShowAdventureChapterConfirmPopup(string chapterId)
	{
		if ((Object)(object)adventureTutorialConfirmPopup == (Object)null)
		{
			ConfirmStartAdventureChapter(chapterId);
			return;
		}
		(string title, string body) = AdventureChapterConfirmCopy(chapterId);
		if ((Object)(object)adventureTutorialConfirmTitleText != (Object)null)
		{
			adventureTutorialConfirmTitleText.text = title;
		}
		if ((Object)(object)adventureTutorialConfirmBodyText != (Object)null)
		{
			adventureTutorialConfirmBodyText.text = body;
		}
		adventureConfirmAction = () => ConfirmStartAdventureChapter(chapterId);
		adventureTutorialConfirmPopup.SetActive(true);
		adventureTutorialConfirmPopup.transform.SetAsLastSibling();
	}

	private void HideAdventureTutorialConfirmPopup()
	{
		if ((Object)(object)adventureTutorialConfirmPopup != (Object)null)
		{
			adventureTutorialConfirmPopup.SetActive(false);
		}
		adventureConfirmAction = null;
	}

	private void BeginGuidedAdventureTutorial()
	{
		guidedTutorialStepIndex = 0;
		if ((Object)(object)guidedTutorialPanel != (Object)null)
		{
			guidedTutorialPanel.SetActive(true);
			guidedTutorialPanel.transform.SetAsLastSibling();
		}
		RefreshGuidedAdventureTutorialStep();
	}

	private void MoveGuidedTutorialStep(int direction)
	{
		int next = guidedTutorialStepIndex + direction;
		if (next >= GuidedTutorialStepCount())
		{
			EndGuidedAdventureTutorial(complete: true);
			return;
		}
		guidedTutorialStepIndex = Mathf.Clamp(next, 0, GuidedTutorialStepCount() - 1);
		RefreshGuidedAdventureTutorialStep();
	}

	private void RefreshGuidedAdventureTutorialStep()
	{
		(string title, string body) = GuidedTutorialStep(guidedTutorialStepIndex);
		if ((Object)(object)guidedTutorialTitleText != (Object)null)
		{
			guidedTutorialTitleText.text = title;
		}
		if ((Object)(object)guidedTutorialBodyText != (Object)null)
		{
			guidedTutorialBodyText.text = body;
		}
		if ((Object)(object)guidedTutorialStepText != (Object)null)
		{
			guidedTutorialStepText.text = $"PASSO {guidedTutorialStepIndex + 1}/{GuidedTutorialStepCount()}";
		}
		if ((Object)(object)guidedTutorialPreviousButton != (Object)null)
		{
			guidedTutorialPreviousButton.interactable = guidedTutorialStepIndex > 0;
		}
		if ((Object)(object)guidedTutorialNextButtonText != (Object)null)
		{
			guidedTutorialNextButtonText.text = guidedTutorialStepIndex >= GuidedTutorialStepCount() - 1 ? "COMPLETA" : "AVANTI";
		}
	}

	private static int GuidedTutorialStepCount()
	{
		return 9;
	}

	private static (string title, string body) GuidedTutorialStep(int index)
	{
		return index switch
		{
			0 => ("BENVENUTO IN ACCARD N' DIE", "Il gioco e una campagna a stanze: scegli una via, costruisci la tua formazione, affronti mostri e boss, e migliori la run con esperienza, oggetti e scelte tattiche."),
			1 => ("LA TUA FORMAZIONE", "Le carte in basso sono i tuoi personaggi. Ogni carta ha classe, forza e abilita. In combattimento dovrai scegliere chi schierare e in quale ordine farlo entrare."),
			2 => ("IL MASTER", "La CPU e il Master. In ogni stanza prepara mostri o boss diversi. Il tuo obiettivo e eliminare la formazione nemica prima che elimini la tua."),
			3 => ("CARTE E VALORI", "La forza della carta conta nei confronti e nelle ricompense. Le classi hanno ruoli diversi: alcune colpiscono forte, altre proteggono, controllano o sfruttano abilita speciali."),
			4 => ("DADI E VIGORE", "Quando attacchi, il gioco usa i dadi Vigore. Il risultato del dado si combina con le regole della carta e determina se il colpo supera la difesa."),
			5 => ("COSA PREMERE", "Quando il gioco ti chiede una scelta, tocca la carta evidenziata o il pulsante conferma. Se cambi idea, usa annulla. Nel tutorial vero gli input non corretti saranno bloccati."),
			6 => ("ABILITA", "Alcune classi hanno abilita attive. Quando il pulsante abilita si illumina, premi ABILITA e poi scegli il bersaglio richiesto: alleato o nemico, a seconda della classe."),
			7 => ("COMBATTIMENTO GUIDATO", "Nel combattimento tutorial ti faremo scegliere una carta, tirare un dado controllato, attaccare un mostro scriptato e vedere una vittoria semplice. Ogni passo verra spiegato prima di agire."),
			_ => ("RICOMPENSA", "Completando il tutorial ricevi le classi base e il primo capitolo. Il miele, che serve per scenari, classi, abilita e Hardcore, si guadagna solo con le quest giornaliere della taverna.")
		};
	}

	private void EndGuidedAdventureTutorial(bool complete)
	{
		if ((Object)(object)guidedTutorialPanel != (Object)null)
		{
			guidedTutorialPanel.SetActive(false);
		}
		if (complete)
		{
			ConfirmStartTutorialAdventureStage();
		}
	}

	private async void ConfirmStartTutorialAdventureStage()
	{
		if (!singlePlayerProgressService.TutorialCompleted)
		{
			if (ServerProgressReady)
			{
				try
				{
					await serverProgress.ClaimTutorialRewardAsync(System.Guid.NewGuid().ToString("N"));
					MirrorServerProgress();
					AppendLog("AVVENTURA - tutorial completato (server): classi base e primo capitolo sbloccati.");
					SetMessage("Tutorial completato: hai le classi base e il primo capitolo. Il miele si guadagna in taverna, con le quest del giorno.");
				}
				catch (System.Exception exception)
				{
					AppendLog($"AVVENTURA - reward tutorial rifiutata dal server: {exception.Message}");
					SetMessage(exception.Message);
				}
				RefreshAdventureChapterList();
				return;
			}

			AppendLog("AVVENTURA - completamento tutorial non registrato: server non disponibile.");
			SetMessage(GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.TutorialConnectionRequired,
				"Connessione al server necessaria per registrare il completamento del tutorial."));
			RefreshAdventureChapterList();
			return;
		}

		SetMessage("Avevi gia completato il tutorial: il primo capitolo e le classi base sono gia' tuoi.");
		AppendLog("AVVENTURA - tutorial selezionato: director guidato non ancora implementato.");
	}

	/// <summary>
	/// Tocco su un capitolo. Qui non si compra piu' niente: un capitolo si guadagna battendo
	/// il boss di quello prima, oppure si compra al Santuario. Questa schermata si limita a
	/// dire in che stato sta il capitolo e, se e' aperto, ad avviarlo.
	/// </summary>
	private void TryOpenAdventureChapter(string chapterId)
	{
		AdventureChapter chapter = AdventureChapterCatalog.Find(chapterId);
		if (chapter == null)
		{
			return;
		}

		if (!chapter.Playable)
		{
			SetMessage(GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ChapterComingSoonMessage,
				"{0} arrivera' presto: il suo boss non e' ancora pronto.",
				chapter.Title));
			return;
		}

		if (!singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, chapterId))
		{
			SetMessage(GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ChapterLockedMessage,
				"Capitolo chiuso: batti il boss del capitolo precedente, oppure compralo al Santuario per {0} miele.",
				chapter.HoneyCost));
			return;
		}

		ShowAdventureChapterConfirmPopup(chapterId);
	}

	private void ConfirmStartAdventureChapter(string chapterId)
	{
		if (!TryGetAdventureChapterConfig(chapterId, out string scenarioId, out string bossId, out string scenarioLabel))
		{
			SetMessage("Capitolo non ancora collegato.");
			AppendLog($"AVVENTURA - {chapterId} confermato ma non ancora implementato.");
			return;
		}

		campaignScenarioId = scenarioId;
		campaignScenarioBossId = bossId;
		activeAdventureChapterId = chapterId;
		defeatedBossIdsInRun.Clear();
		pendingScenarioId = scenarioId;
		currentScenarioDisplayOverride = null;
		if (!LoadScenario(RoomType.Boss, RoomDifficulty.Hard, bossId, scenarioId))
		{
			currentScenarioDisplayOverride = scenarioLabel;
			AppendLog($"AVVENTURA - scenario {scenarioLabel} non trovato nel catalogo; uso fallback visuale.");
		}
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}
		SetMessage($"{AdventureChapterDisplayName(chapterId)}: {scenarioLabel}. Prepara il mazzo nella forgia.");
		AppendLog($"AVVENTURA - {AdventureChapterDisplayName(chapterId)} avviato: scenario {scenarioLabel}, boss {bossId}.");
		StartCampaignBuilderMode();
	}

	/// <summary>
	/// Scenario e boss del capitolo. Falso per i capitoli ancora senza boss: non c'e' niente
	/// da caricare, e chiamarlo su quelli e' gia' un errore a monte.
	/// </summary>
	private static bool TryGetAdventureChapterConfig(
		string chapterId,
		out string scenarioId,
		out string bossId,
		out string scenarioLabel)
	{
		AdventureChapter chapter = AdventureChapterCatalog.Find(chapterId);
		if (chapter == null || !chapter.Playable)
		{
			scenarioId = null;
			bossId = null;
			scenarioLabel = null;
			return false;
		}

		scenarioId = chapter.ScenarioId;
		bossId = chapter.BossId;
		scenarioLabel = chapter.ScenarioLabel;
		return true;
	}

	private static string AdventureChapterDisplayName(string chapterId)
	{
		AdventureChapter chapter = AdventureChapterCatalog.Find(chapterId);
		return chapter == null ? "Capitolo" : "Capitolo " + chapter.Number;
	}

	private static (string title, string body) AdventureChapterConfirmCopy(string chapterId)
	{
		if (!TryGetAdventureChapterConfig(chapterId, out _, out string bossId, out string scenarioLabel))
		{
			return ("CAPITOLO", "Alla conferma si apre la forgia mazzo per preparare la spedizione.");
		}

		string chapterName = AdventureChapterDisplayName(chapterId);
		string bossName = BossDisplayName(bossId);
		return (
			$"{chapterName}: {scenarioLabel} di {bossName}".ToUpperInvariant(),
			$"Entrerai in {chapterName.ToLowerInvariant()}: lo scenario sara {scenarioLabel}. Alla conferma si apre la forgia mazzo per preparare la spedizione.");
	}

	private static string BossDisplayName(string bossId) =>
		AdventureChapterCatalog.BossDisplayName(bossId);

	private async void StartHardcoreMode()
	{
		if (!singlePlayerProgressService.HardcoreUnlocked)
		{
			if (ServerProgressReady)
			{
				try
				{
					await serverProgress.PurchaseHardcoreAsync();
					MirrorServerProgress();
					SetMessage("Hardcore sbloccata.");
					AppendLog("SINGLE PLAYER - Hardcore sbloccata (server).");
				}
				catch (System.Exception exception)
				{
					SetMessage(exception.Message);
					AppendLog($"SINGLE PLAYER - acquisto Hardcore rifiutato dal server: {exception.Message}");
				}
				RefreshSinglePlayerProgressView();
				return;
			}

			SetMessage(GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.HardcoreConnectionRequired,
				"Connessione al server necessaria per sbloccare Hardcore."));
			AppendLog("SINGLE PLAYER - acquisto Hardcore non eseguito: server non disponibile.");
			RefreshSinglePlayerProgressView();
			return;
		}
		StartCampaignBuilderMode();
	}

	private void StartCampaignBuilderMode()
	{
		campaignRunRewardId = System.Guid.NewGuid().ToString("N");
		// L'avvio va annunciato adesso, non a fine run: le run abbandonate a meta' non
		// arrivano mai alla reward, ed erano invisibili nelle statistiche.
		_ = NotifyCampaignRunStarted(campaignRunRewardId);
		pendingAdventureChapterClearTask = System.Threading.Tasks.Task.CompletedTask;
		pendingCampaignRewardTask = System.Threading.Tasks.Task.CompletedTask;
		pendingCampaignRewardClaimId = null;
		pendingCampaignRewardBaseAccountExperience = 0;
		pendingCampaignRewardAdClaimed = false;
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}
		inputLocked = false;
		ShowCampaignIntroHint();
		LoadBattle();
	}

	private void StartPvpMode()
	{
		StartPvpMode(openLeaderboard: false);
	}

	private void StartPvpMode(bool openLeaderboard)
	{
		PlayerPrefs.SetInt("AccardND.GuestMode", 0);
		PlayerPrefs.Save();
		// Rilascia il repository della progressione prima di passare al PvP. La sessione account
		// condivisa e il relativo socket restano aperti e vengono riusati dal bootstrap PvP.
		if ((Object)(object)singlePlayerServerLink != (Object)null)
		{
			singlePlayerServerLink.Reconnected -= HandleServerProgressReconnected;
			singlePlayerServerLink.Shutdown();
		}
		serverProgress = null;
		ReturnToStart(showModeSelection: false);
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		if (openLeaderboard)
		{
			SetAccountHubHudActive(true);
		}
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		GameObject gameObject = new GameObject("Pvp Mode");
		AccardND.PvpUi.PvpBootstrap pvpBootstrap = gameObject.AddComponent<AccardND.PvpUi.PvpBootstrap>();
		activePvpBootstrap = pvpBootstrap;
		System.Action closed = delegate
		{
			if ((Object)(object)activePvpBootstrap == (Object)(object)pvpBootstrap)
			{
				activePvpBootstrap = null;
			}
			ShowHubFromSinglePlayer();
			// Entrando nel PvP il link dell'Hub viene rilasciato. Al ritorno va ricreato
			// e risincronizzato prima di ridisegnare livello, EXP e valuta del banner.
			_ = RefreshCampaignPanelsAfterReconnectAsync();
		};
		if (openLeaderboard)
		{
			pvpBootstrap.ConfigureLeaderboard(cardDatabase, closed, this);
		}
		else
		{
			pvpBootstrap.Configure(cardDatabase, closed, this, ToggleOptionsPanel);
		}
	}

	private void CreateDeckBuilderView(Font font)
	{
		Image image = CreateImage("Initial Deck Builder", (Transform)(object)canvasRect, Color.clear);
		image.raycastTarget = true;
		deckBuilderPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 520;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image image2 = CreateImage("Deck Builder Field Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		ApplyCampaignPortalBackground(image2);
		SetRect(image2.rectTransform, Vector2.zero, Vector2.one);
		deckBuilderInnerBackgroundImage = CreateImage(
			"Screen Inner Background",
			((Component)image).transform,
			new Color(0.004f, 0.005f, 0.008f, 0.72f));
		deckBuilderInnerBackgroundImage.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
		deckBuilderInnerBackgroundImage.type = Image.Type.Sliced;
		deckBuilderInnerBackgroundImage.raycastTarget = true;
		SetRect(
			deckBuilderInnerBackgroundImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, 0.872f));
		Image frame = CreateImage("Screen Outer Frame", ((Component)image).transform, Color.white);
		deckBuilderFrameImage = frame;
		deckBuilderFrameAspectFitter = ConfigureScreenOuterFrame(frame);
		frame.color = new Color(1f, 1f, 1f, 0.92f);
		frame.raycastTarget = false;
		SetRect(frame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.872f));
		Transform deckBuilderContentRoot = ((Component)image).transform;
		deckBuilderTitlePanel = CreateImage("Deck Builder Title Panel", deckBuilderContentRoot, Color.white);
		deckBuilderTitlePanel.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		deckBuilderTitlePanel.type = Image.Type.Simple;
		deckBuilderTitlePanel.preserveAspect = false;
		deckBuilderTitlePanel.raycastTarget = false;
		SetRect(
			deckBuilderTitlePanel.rectTransform,
			new Vector2(0.08f, 0.852f),
			new Vector2(0.92f, 0.952f));
		Text text = CreateText(
			"Heading",
			((Component)deckBuilderTitlePanel).transform,
			font,
			42,
			(FontStyle)1,
			(TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(text);
		text.text = "FORGIA IL TUO MAZZO";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		deckBuilderHeadingText = text;
		SetRect(text.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		text.rectTransform.offsetMin = new Vector2(0f, -21f);
		text.rectTransform.offsetMax = new Vector2(0f, -21f);
		deckBuilderStatusText = CreateText("Budget", deckBuilderContentRoot, font, 40, (FontStyle)1, (TextAnchor)4);
		SetRect(deckBuilderStatusText.rectTransform, new Vector2(0.18f, 0.775f), new Vector2(0.82f, 0.84f));
		deckBuilderCardsRoot = new GameObject("Deck Preview Grid", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)deckBuilderCardsRoot).SetParent(deckBuilderContentRoot, false);
		SetRect(deckBuilderCardsRoot, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.745f));
		GridLayoutGroup component = ((Component)deckBuilderCardsRoot).GetComponent<GridLayoutGroup>();
		component.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		component.constraintCount = 3;
		component.spacing = new Vector2(12f, 12f);
		component.childAlignment = (TextAnchor)4;
		component.cellSize = new Vector2(210f, 210f);
		deckBuilderCardsText = CreateText("Empty Deck Hint", deckBuilderContentRoot, font, 35, (FontStyle)1, (TextAnchor)4);
		deckBuilderCardsText.color = new Color(0.88f, 0.92f, 0.96f);
		SetRect(deckBuilderCardsText.rectTransform, new Vector2(0.24f, 0.55f), new Vector2(0.76f, 0.66f));
		Button button = CreateImageButton("Buy Blind Random", deckBuilderContentRoot, font, LoadSpriteResource("UI/random_value_draw"), string.Empty);
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			BuyInitialDeckCard(DeckPurchaseMode.BlindRandom);
		});
		deckBuilderRandomButtonRect = (RectTransform)((Component)button).transform;
		((Component)button).gameObject.SetActive(false);
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.12f, 0.205f), new Vector2(0.34f, 0.345f));
		deckBuilderRandomBuyText = CreateText("Deck Random Cost", deckBuilderContentRoot, font, 34, (FontStyle)1, (TextAnchor)4);
		ConfigureDeckBuilderCostText(deckBuilderRandomBuyText);
		((Component)deckBuilderRandomBuyText).gameObject.SetActive(false);
		SetRect(deckBuilderRandomBuyText.rectTransform, new Vector2(0.19f, 0.18f), new Vector2(0.27f, 0.245f));
		deckBuilderClassGridRoot = new GameObject("Deck Class Grid", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)deckBuilderClassGridRoot).SetParent(deckBuilderContentRoot, false);
		GridLayoutGroup classGrid = ((Component)deckBuilderClassGridRoot).GetComponent<GridLayoutGroup>();
		classGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		classGrid.constraintCount = 3;
		classGrid.spacing = new Vector2(10f, 10f);
		classGrid.childAlignment = TextAnchor.MiddleCenter;
		CreateDeckBuilderClassOptions((Transform)(object)deckBuilderClassGridRoot, font);
		deckBuilderClassBuyText = CreateText("Deck Class Cost", deckBuilderContentRoot, font, 34, (FontStyle)1, (TextAnchor)4);
		ConfigureDeckBuilderCostText(deckBuilderClassBuyText);
		((Component)deckBuilderClassBuyText).gameObject.SetActive(false);
		Button button3 = CreateImageButton("Deck Class Previous", deckBuilderContentRoot, font, LoadSpriteResource("UI/left_arrow"), string.Empty);
		((UnityEvent)button3.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderClass(-1);
		});
		deckBuilderClassPreviousButtonRect = (RectTransform)((Component)button3).transform;
		((Component)button3).gameObject.SetActive(false);
		SetRect((RectTransform)((Component)button3).transform, new Vector2(0.355f, 0.18f), new Vector2(0.455f, 0.245f));
		Button button4 = CreateImageButton("Deck Class Next", deckBuilderContentRoot, font, LoadSpriteResource("UI/right_arrow"), string.Empty);
		((UnityEvent)button4.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderClass(1);
		});
		deckBuilderClassNextButtonRect = (RectTransform)((Component)button4).transform;
		((Component)button4).gameObject.SetActive(false);
		SetRect((RectTransform)((Component)button4).transform, new Vector2(0.545f, 0.18f), new Vector2(0.645f, 0.245f));
		Button button5 = CreateImageButton("Buy Selected Strength", deckBuilderContentRoot, font, LoadSpriteResource(DeckBuilderStrengthResourcePath(deckBuilderSelectedStrength)), string.Empty);
		((UnityEvent)button5.onClick).AddListener((UnityAction)delegate
		{
			BuyInitialDeckCard(DeckPurchaseMode.ChosenStrength);
		});
		deckBuilderStrengthImage = ((Component)button5).GetComponent<Image>();
		deckBuilderStrengthButtonRect = (RectTransform)((Component)button5).transform;
		((Component)button5).gameObject.SetActive(false);
		SetRect((RectTransform)((Component)button5).transform, new Vector2(0.66f, 0.205f), new Vector2(0.88f, 0.345f));
		Button button6 = CreateImageButton("Deck Strength Previous", deckBuilderContentRoot, font, LoadSpriteResource("UI/left_arrow"), string.Empty);
		((UnityEvent)button6.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderStrength(-1);
		});
		deckBuilderStrengthPreviousButtonRect = (RectTransform)((Component)button6).transform;
		((Component)button6).gameObject.SetActive(false);
		SetRect((RectTransform)((Component)button6).transform, new Vector2(0.625f, 0.18f), new Vector2(0.725f, 0.245f));
		deckBuilderStrengthBuyText = CreateText("Deck Strength Cost", deckBuilderContentRoot, font, 34, (FontStyle)1, (TextAnchor)4);
		ConfigureDeckBuilderCostText(deckBuilderStrengthBuyText);
		((Component)deckBuilderStrengthBuyText).gameObject.SetActive(false);
		SetRect(deckBuilderStrengthBuyText.rectTransform, new Vector2(0.73f, 0.18f), new Vector2(0.81f, 0.245f));
		Button button7 = CreateImageButton("Deck Strength Next", deckBuilderContentRoot, font, LoadSpriteResource("UI/right_arrow"), string.Empty);
		((UnityEvent)button7.onClick).AddListener((UnityAction)delegate
		{
			CycleDeckBuilderStrength(1);
		});
		deckBuilderStrengthNextButtonRect = (RectTransform)((Component)button7).transform;
		((Component)button7).gameObject.SetActive(false);
		SetRect((RectTransform)((Component)button7).transform, new Vector2(0.815f, 0.18f), new Vector2(0.915f, 0.245f));
		Image image4 = CreateImage("Deck Builder Toast", deckBuilderContentRoot, new Color(0.58f, 0.03f, 0.02f, 0.94f));
		image4.sprite = GetRuntimePanelSprite();
		image4.type = (Image.Type)1;
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(image4.rectTransform, "Toast Gem", new Vector2(0.5f, 1f), new Vector2(30f, 30f), new Color(1f, 0.72f, 0.72f, 0.9f));
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
		startCampaignButton = CreateButton("Start Campaign", deckBuilderContentRoot, font, "INIZIA");
		ApplyRankedPurpleCtaWithoutEffects(startCampaignButton);
		((UnityEvent)startCampaignButton.onClick).AddListener(new UnityAction(StartBuiltCampaign));
		startCampaignButtonRect = (RectTransform)((Component)startCampaignButton).transform;
		SetRect((RectTransform)((Component)startCampaignButton).transform, new Vector2(0.415f, 0.065f), new Vector2(0.585f, 0.17f));
		((Component)startCampaignButton).gameObject.SetActive(false);
		deckBuilderPanel.SetActive(false);
	}

	private void CreateDeckBuilderClassOptions(Transform parent, Font font)
	{
		deckBuilderClassOptionRects.Clear();
		deckBuilderClassOptionButtons.Clear();
		deckBuilderClassOptionImages.Clear();
		deckBuilderClassOptionClasses.Clear();
		HeroClass[] classes =
		{
			HeroClass.Barbarian,
			HeroClass.Paladin,
			HeroClass.Warrior,
			HeroClass.Mage,
			HeroClass.Necromancer,
			HeroClass.Priest,
			HeroClass.Assassin,
			HeroClass.Hunter,
			HeroClass.Rogue
		};

		for (int index = 0; index < classes.Length; index++)
		{
			Button button = CreateDeckBuilderClassAtlasButton(parent, font, classes[index]);
			deckBuilderClassOptionRects.Add((RectTransform)((Component)button).transform);
			deckBuilderClassOptionButtons.Add(button);
			Transform iconTransform = ((Component)button).transform.Find("Icon");
			deckBuilderClassOptionImages.Add(iconTransform != null ? iconTransform.GetComponent<Image>() : null);
			deckBuilderClassOptionClasses.Add(classes[index]);
		}
	}

	private Button CreateDeckBuilderClassAtlasButton(Transform parent, Font font, HeroClass heroClass)
	{
		Image hitTarget = CreateImage("Buy Class " + heroClass, parent, new Color(1f, 1f, 1f, 0.001f));
		hitTarget.raycastTarget = true;
		Button button = ((Component)hitTarget).gameObject.AddComponent<Button>();
		button.targetGraphic = hitTarget;
		AccardND.Battlefield.MmoUiTheme.ApplyButtonColors(button);
		AccardND.Battlefield.MmoUiTheme.AddMotion(button);
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			BuyInitialDeckClass(heroClass);
		});

		Image icon = CreateImage("Icon", ((Component)hitTarget).transform, Color.white);
		icon.sprite = GetClassIconSprite(heroClass, grayscale: false);
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		Vector2 iconOffset = GetDeckBuilderClassIconOffset(heroClass);
		SetRect(icon.rectTransform, new Vector2(0.08f, 0.31f) + iconOffset, new Vector2(0.92f, 0.96f) + iconOffset);

		Text label = CreateText("Label", ((Component)hitTarget).transform, font, 28, (FontStyle)1, (TextAnchor)4);
		label.text = HeroClassDisplayName(heroClass).ToUpperInvariant();
		label.color = new Color(1f, 0.84f, 0.16f, 1f);
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
		label.resizeTextForBestFit = true;
		label.resizeTextMinSize = 8;
		label.resizeTextMaxSize = 28;
		Outline outline = ((Component)label).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
		outline.effectDistance = new Vector2(1.5f, -1.5f);
		Vector2 labelOffset = GetDeckBuilderClassLabelOffset(heroClass);
		SetRect(label.rectTransform, new Vector2(0.04f, 0.04f) + labelOffset, new Vector2(0.96f, 0.24f) + labelOffset);
		return button;
	}

	/// <summary>
	/// Ritocchi a mano: i loghi non hanno tutti lo stesso peso visivo dentro il proprio ritaglio.
	/// Il Warrior non compare piu' perche' il suo offset compensava il taglio della vecchia
	/// griglia 3x3 a runtime, che ne mangiava ~15px sulla sinistra.
	/// </summary>
	private static Vector2 GetDeckBuilderClassIconOffset(HeroClass heroClass)
	{
		return heroClass switch
		{
			HeroClass.Hunter => new Vector2(0.04f, -0.04f),
			HeroClass.Paladin => new Vector2(0.04f, 0f),
			HeroClass.Priest => new Vector2(0.035f, 0f),
			_ => Vector2.zero
		};
	}

	private static Vector2 GetDeckBuilderClassLabelOffset(HeroClass heroClass)
	{
		return heroClass switch
		{
			HeroClass.Hunter => new Vector2(0.045f, -0.03f),
			HeroClass.Paladin => new Vector2(0.05f, 0f),
			HeroClass.Priest => new Vector2(0.04f, 0f),
			_ => Vector2.zero
		};
	}

	private const string ClassIconAtlasResourcePath = "UI/DeckBuilder/class_icons_atlas";

	private static Dictionary<string, Sprite> classIconSprites;
	private static readonly Dictionary<HeroClass, Sprite> classIconLockedSprites = new Dictionary<HeroClass, Sprite>();
	private static readonly Dictionary<HeroClass, Rect> classIconFallbackRects = new Dictionary<HeroClass, Rect>
	{
		{ HeroClass.Barbarian, new Rect(0f, 662f, 310f, 330f) },
		{ HeroClass.Paladin, new Rect(310f, 662f, 309f, 330f) },
		{ HeroClass.Warrior, new Rect(619f, 662f, 310f, 330f) },
		{ HeroClass.Mage, new Rect(0f, 331f, 310f, 331f) },
		{ HeroClass.Necromancer, new Rect(310f, 331f, 309f, 331f) },
		{ HeroClass.Priest, new Rect(619f, 331f, 310f, 331f) },
		{ HeroClass.Assassin, new Rect(0f, 0f, 310f, 331f) },
		{ HeroClass.Hunter, new Rect(310f, 0f, 309f, 331f) },
		{ HeroClass.Rogue, new Rect(619f, 0f, 310f, 331f) }
	};

	private static string ClassIconSpriteName(HeroClass heroClass)
	{
		return heroClass switch
		{
			HeroClass.Barbarian => "class_barbarian",
			HeroClass.Paladin => "class_paladin",
			HeroClass.Warrior => "class_warrior",
			HeroClass.Mage => "class_mage",
			HeroClass.Necromancer => "class_necromancer",
			HeroClass.Priest => "class_priest",
			HeroClass.Assassin => "class_assassin",
			HeroClass.Hunter => "class_hunter",
			HeroClass.Rogue => "class_rogue",
			_ => "class_mage"
		};
	}

	/// <summary>
	/// Gli sprite delle classi sono gia' ritagliati nell'atlas (Sprite Mode: Multiple), quindi
	/// qui resta solo un lookup per nome. La variante in grigio delle classi bloccate va ancora
	/// generata a runtime, ma una sola volta per classe invece che a ogni refresh.
	/// </summary>
	private static Sprite GetClassIconSprite(HeroClass heroClass, bool grayscale)
	{
		if (classIconSprites == null)
		{
			classIconSprites = new Dictionary<string, Sprite>();
			foreach (Sprite candidate in Resources.LoadAll<Sprite>(ClassIconAtlasResourcePath))
			{
				classIconSprites[candidate.name] = candidate;
			}
		}

		string spriteName = ClassIconSpriteName(heroClass);
		if (!classIconSprites.TryGetValue(spriteName, out Sprite sprite))
		{
			sprite = CreateClassIconFallbackSprite(heroClass);
			if ((Object)(object)sprite == (Object)null)
			{
				Debug.LogWarning($"[ClassIcons] sprite '{spriteName}' assente in {ClassIconAtlasResourcePath}: l'atlas va ritagliato in Sprite Mode Multiple con i nomi delle classi.");
				return null;
			}
			classIconSprites[spriteName] = sprite;
		}
		if (!grayscale)
		{
			return sprite;
		}
		if (classIconLockedSprites.TryGetValue(heroClass, out Sprite cached) && (Object)(object)cached != (Object)null)
		{
			return cached;
		}

		Sprite locked = CreateGrayscaleClassIcon(sprite);
		classIconLockedSprites[heroClass] = locked;
		return locked;
	}

	private static Sprite CreateGrayscaleClassIcon(Sprite source)
	{
		Texture2D atlas = source.texture;
		if ((Object)(object)atlas == (Object)null || !atlas.isReadable)
		{
			return source;
		}

		Rect rect = source.rect;
		int sourceX = Mathf.RoundToInt(rect.x);
		int sourceY = Mathf.RoundToInt(rect.y);
		int width = Mathf.RoundToInt(rect.width);
		int height = Mathf.RoundToInt(rect.height);
		Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			name = source.name + "_locked",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave
		};
		Color[] pixels = atlas.GetPixels(sourceX, sourceY, width, height);
		for (int i = 0; i < pixels.Length; i++)
		{
			Color color = pixels[i];
			float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
			pixels[i] = new Color(gray, gray, gray, color.a);
		}
		texture.SetPixels(pixels);
		texture.Apply(false, true);
		Sprite locked = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
		locked.name = source.name + "_locked";
		locked.hideFlags = HideFlags.HideAndDontSave;
		return locked;
	}

	private static Sprite CreateClassIconFallbackSprite(HeroClass heroClass)
	{
		Texture2D atlas = Resources.Load<Texture2D>(ClassIconAtlasResourcePath);
		if ((Object)(object)atlas == (Object)null || !classIconFallbackRects.TryGetValue(heroClass, out Rect rect))
		{
			return null;
		}
		float scaleX = atlas.width / 929f;
		float scaleY = atlas.height / 992f;
		Rect scaledRect = new Rect(
			Mathf.Round(rect.x * scaleX),
			Mathf.Round(rect.y * scaleY),
			Mathf.Round(rect.width * scaleX),
			Mathf.Round(rect.height * scaleY));
		Sprite sprite = Sprite.Create(atlas, scaledRect, new Vector2(0.5f, 0.5f), 100f);
		sprite.name = ClassIconSpriteName(heroClass);
		return sprite;
	}

	private void CreateInitialDraftView(Font font)
	{
		Image image = CreateImage("Initial Draft", (Transform)(object)canvasRect, Color.clear);
		image.raycastTarget = true;
		initialDraftPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 525;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image image2 = CreateImage("Initial Draft Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		ApplyCampaignPortalBackground(image2);
		SetRect(image2.rectTransform, Vector2.zero, Vector2.one);
		Image image3 = CreateImage("Screen Outer Frame", ((Component)image).transform, Color.white);
		initialDraftFrameImage = image3;
		initialDraftFrameAspectFitter = ConfigureScreenOuterFrame(image3);
		image3.color = new Color(1f, 1f, 1f, 0.92f);
		image3.raycastTarget = false;
		SetRect(image3.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.872f));
		Transform initialDraftContentRoot = ((Component)image).transform;
		initialDraftHeadingText = CreateText("Heading", initialDraftContentRoot, font, 42, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(initialDraftHeadingText);
		initialDraftHeadingText.color = new Color(0.95f, 0.79f, 0.34f);
		initialDraftStatusText = CreateText("Status", initialDraftContentRoot, font, 25, (FontStyle)1, (TextAnchor)4);
		initialDraftStatusText.color = Color.white;
		initialDraftPromptText = CreateText("Prompt", initialDraftContentRoot, font, 22, (FontStyle)1, (TextAnchor)4);
		initialDraftPromptText.color = new Color(0.88f, 0.92f, 0.96f);
		initialDraftPromptText.horizontalOverflow = HorizontalWrapMode.Wrap;
		initialDraftPromptText.verticalOverflow = VerticalWrapMode.Truncate;
		initialDraftPromptText.resizeTextForBestFit = true;
		initialDraftPromptText.resizeTextMinSize = 13;
		initialDraftPromptText.resizeTextMaxSize = 22;
		initialDraftOffersRoot = new GameObject("Draft Offer Grid", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)initialDraftOffersRoot).SetParent(initialDraftContentRoot, false);
		GridLayoutGroup offersGrid = ((Component)initialDraftOffersRoot).GetComponent<GridLayoutGroup>();
		offersGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		offersGrid.constraintCount = 3;
		offersGrid.spacing = new Vector2(10f, 10f);
		offersGrid.childAlignment = TextAnchor.MiddleCenter;
		initialDraftDeckRoot = new GameObject("Draft Deck Preview", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)initialDraftDeckRoot).SetParent(initialDraftContentRoot, false);
		GridLayoutGroup deckGrid = ((Component)initialDraftDeckRoot).GetComponent<GridLayoutGroup>();
		deckGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		deckGrid.constraintCount = 9;
		deckGrid.spacing = new Vector2(6f, 6f);
		deckGrid.childAlignment = TextAnchor.MiddleCenter;
		initialDraftDeckText = CreateText("Draft Empty Deck Hint", initialDraftContentRoot, font, 18, (FontStyle)1, (TextAnchor)4);
		initialDraftDeckText.color = new Color(0.88f, 0.92f, 0.96f);
		initialDraftDeckText.text = "Il mazzo draft apparira' qui.";
		initialDraftConfirmButton = CreateButton("Confirm Draft Picks", initialDraftContentRoot, font, "CONFERMA");
		((UnityEvent)initialDraftConfirmButton.onClick).AddListener(new UnityAction(ConfirmInitialDraftSelection));
		initialDraftConfirmButtonRect = (RectTransform)((Component)initialDraftConfirmButton).transform;
		initialDraftConfirmButtonText = ((Component)initialDraftConfirmButton).GetComponentInChildren<Text>();
		RefreshInitialDraftLayout();
		initialDraftPanel.SetActive(false);
	}

	private AspectRatioFitter ConfigureScreenOuterFrame(Image image)
	{
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(image);
		AspectRatioFitter aspectFitter = ((Component)image).GetComponent<AspectRatioFitter>() ?? ((Component)image).gameObject.AddComponent<AspectRatioFitter>();
		aspectFitter.enabled = false;
		return aspectFitter;
	}

	private void RefreshScreenOuterFrame(Image image, AspectRatioFitter aspectFitter)
	{
		if ((Object)(object)image == (Object)null)
		{
			return;
		}
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(image);
		if ((Object)(object)aspectFitter != (Object)null)
		{
			aspectFitter.enabled = false;
		}
	}

	private static void ApplyCampaignPortalBackground(Image image)
	{
		if ((Object)(object)image == (Object)null)
			return;

		Sprite sprite = LoadSpriteResource(CampaignPortalBackgroundResource);
		image.sprite = sprite;
		image.preserveAspect = true;
		image.color = new Color(0.62f, 0.56f, 0.68f, 1f);
		image.raycastTarget = false;

		AspectRatioFitter fitter = ((Component)image).GetComponent<AspectRatioFitter>()
			?? ((Component)image).gameObject.AddComponent<AspectRatioFitter>();
		fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
		fitter.aspectRatio = (Object)(object)sprite != (Object)null
			? sprite.rect.width / sprite.rect.height
			: 2f / 3f;
	}

	private static void ApplyCampaignAdventureCta(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = ((Component)button).GetComponent<Image>();
		Sprite frame = GetCampaignAdventureCtaSprite();
		if ((Object)(object)image != (Object)null && (Object)(object)frame != (Object)null)
		{
			image.sprite = frame;
			image.type = Image.Type.Simple;
			image.preserveAspect = true;
			image.color = Color.white;
			button.targetGraphic = image;

			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1f, 0.92f, 1f, 1f);
			colors.pressedColor = new Color(0.8f, 0.68f, 0.92f, 1f);
			colors.selectedColor = Color.white;
			button.colors = colors;
		}

		Text label = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
			label.fontSize = 38;
			label.resizeTextMaxSize = 38;
			label.alignment = TextAnchor.MiddleLeft;
			SetRect(label.rectTransform, new Vector2(0.45f, 0.03f), new Vector2(0.85f, 0.97f));
		}

		Image emblem = CreateImage(
			"Adventure Portal Emblem", ((Component)button).transform, Color.white);
		emblem.sprite = LoadSpriteResource(CampaignAdventureEmblemResource);
		emblem.preserveAspect = true;
		emblem.raycastTarget = false;
		SetRect(emblem.rectTransform, new Vector2(0.22f, 0.08f), new Vector2(0.42f, 0.92f));
		emblem.rectTransform.localScale = Vector3.one;

		AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)button).transform,
			new Color(0.65f, 0.2f, 0.96f, 1f));
	}

	private static void ApplyRankedPurpleCtaWithoutEffects(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = ((Component)button).GetComponent<Image>();
		Sprite frame = GetCampaignAdventureCtaSprite();
		if ((Object)(object)image != (Object)null && (Object)(object)frame != (Object)null)
		{
			image.sprite = frame;
			image.type = Image.Type.Simple;
			image.preserveAspect = true;
			image.color = Color.white;
			button.targetGraphic = image;

			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1f, 0.92f, 1f, 1f);
			colors.pressedColor = new Color(0.8f, 0.68f, 0.92f, 1f);
			colors.selectedColor = Color.white;
			button.colors = colors;
		}

		Text label = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)label == (Object)null)
			return;

		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
		label.fontSize = 30;
		label.resizeTextMaxSize = 30;
		label.alignment = TextAnchor.MiddleCenter;
		SetRect(label.rectTransform, new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.97f));

		AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)button).transform,
			new Color(0.65f, 0.2f, 0.96f, 1f));
	}

	private void ApplyCampaignHardcoreCta(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = ((Component)button).GetComponent<Image>();
		Sprite frame = GetCampaignHardcoreCtaSprite();
		if ((Object)(object)image != (Object)null && (Object)(object)frame != (Object)null)
		{
			image.sprite = frame;
			image.type = Image.Type.Simple;
			image.preserveAspect = true;
			image.color = Color.white;
			button.targetGraphic = image;

			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1f, 0.86f, 0.82f, 1f);
			colors.pressedColor = new Color(0.78f, 0.42f, 0.38f, 1f);
			colors.selectedColor = Color.white;
			button.colors = colors;
		}

		Text label = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
			label.fontSize = 38;
			label.resizeTextMaxSize = 38;
			label.alignment = TextAnchor.MiddleLeft;
			SetRect(label.rectTransform, new Vector2(0.45f, 0.03f), new Vector2(0.85f, 0.97f));
		}

		campaignModeHardcoreEmblemImage = CreateImage(
			"Hardcore Portal Emblem", ((Component)button).transform, Color.white);
		campaignModeHardcoreEmblemImage.sprite = LoadSpriteResource(CampaignHardcoreLockedEmblemResource);
		campaignModeHardcoreEmblemImage.preserveAspect = true;
		campaignModeHardcoreEmblemImage.raycastTarget = false;
		SetRect(
			campaignModeHardcoreEmblemImage.rectTransform,
			new Vector2(0.22f, 0.08f),
			new Vector2(0.42f, 0.92f));
		campaignModeHardcoreEmblemImage.rectTransform.localScale = Vector3.one;

		campaignModeHardcoreVfx = AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)button).transform,
			new Color(0.82f, 0.12f, 0.08f, 1f));
		((Component)campaignModeHardcoreVfx).gameObject.SetActive(
			singlePlayerProgressService.HardcoreUnlocked);
	}

	private static Sprite GetCampaignAdventureCtaSprite()
	{
		if ((Object)(object)campaignAdventureCtaSprite != (Object)null)
			return campaignAdventureCtaSprite;

		Texture2D texture = Resources.Load<Texture2D>(MultiplayerRankedCtaResource);
		if ((Object)(object)texture == (Object)null)
			return null;

		Rect crop = GetCampaignCtaCrop(texture);
		campaignAdventureCtaSprite = Sprite.Create(
			texture,
			crop,
			new Vector2(0.5f, 0.5f),
			100f,
			0u,
			SpriteMeshType.FullRect);
		campaignAdventureCtaSprite.name = "Campaign Adventure Ranked CTA";
		campaignAdventureCtaSprite.hideFlags = HideFlags.HideAndDontSave;
		return campaignAdventureCtaSprite;
	}

	private static Sprite GetCampaignHardcoreCtaSprite()
	{
		if ((Object)(object)campaignHardcoreCtaSprite != (Object)null)
			return campaignHardcoreCtaSprite;

		Texture2D texture = Resources.Load<Texture2D>(CampaignHardcoreCtaResource);
		if ((Object)(object)texture == (Object)null)
			return null;

		Rect crop = GetCampaignCtaCrop(texture);
		campaignHardcoreCtaSprite = Sprite.Create(
			texture,
			crop,
			new Vector2(0.5f, 0.5f),
			100f,
			0u,
			SpriteMeshType.FullRect);
		campaignHardcoreCtaSprite.name = "Campaign Hardcore Red CTA";
		campaignHardcoreCtaSprite.hideFlags = HideFlags.HideAndDontSave;
		return campaignHardcoreCtaSprite;
	}

	private static Rect GetCampaignCtaCrop(Texture2D texture)
	{
		// Le texture possono essere ridimensionate dall'importer sulla piattaforma target:
		// manteniamo il ritaglio originale 145,196,1692,400 in coordinate proporzionali.
		return new Rect(
			texture.width * (145f / 1983f),
			texture.height * (196f / 793f),
			texture.width * (1692f / 1983f),
			texture.height * (400f / 793f));
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
		StylePanel(image);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(image.rectTransform, "Combat Result Crest", new Vector2(0.5f, 1f), new Vector2(46f, 46f), Color.white);
		SetRect(image.rectTransform, new Vector2(0.2f, 0.37f), new Vector2(0.8f, 0.63f));
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 230;
		combatScoreText = CreateText("Score", ((Component)image).transform, font, 82, (FontStyle)1, (TextAnchor)4);
		combatScoreText.font = AccardND.Battlefield.MmoUiTheme.DisplayFont;
		combatScoreText.fontStyle = FontStyle.Normal;
		combatScoreText.color = Color.white;
		SetRect(combatScoreText.rectTransform, new Vector2(0.03f, 0.34f), new Vector2(0.97f, 0.98f));
		combatDiceText = CreateText("Dice Delta", ((Component)image).transform, font, 32, (FontStyle)1, (TextAnchor)4);
		combatDiceText.font = AccardND.Battlefield.MmoUiTheme.DisplayFont;
		combatDiceText.fontStyle = FontStyle.Normal;
		combatDiceText.color = new Color(0.72f, 0.9f, 1f);
		SetRect(combatDiceText.rectTransform, new Vector2(0.03f, 0.4f), new Vector2(0.97f, 0.7f));
		combatOutcomeText = CreateText("Outcome", ((Component)image).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		combatOutcomeText.font = AccardND.Battlefield.MmoUiTheme.DisplayFont;
		combatOutcomeText.fontStyle = FontStyle.Normal;
		SetRect(combatOutcomeText.rectTransform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.38f));
		combatResultRoot.SetActive(false);
	}
}
}
