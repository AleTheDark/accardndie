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
		implementationArchiveButton = CreateImageButton("Implementation Archive Button", (Transform)(object)safeAreaRoot, font, LoadSpriteResource("UI/bag_button"), string.Empty);
		((UnityEvent)implementationArchiveButton.onClick).AddListener(new UnityAction(ToggleImplementationArchive));
		implementationArchiveButtonRect = (RectTransform)((Component)implementationArchiveButton).transform;
		SetRect(implementationArchiveButtonRect, new Vector2(0.71f, 0.902f), new Vector2(0.865f, 0.992f));
		implementationArchiveButtonLabel = CreateText("Implementation Archive Button Label", (Transform)(object)safeAreaRoot, font, 20, (FontStyle)1, (TextAnchor)4);
		implementationArchiveButtonLabel.text = "inventario";
		implementationArchiveButtonLabel.color = new Color(0.95f, 0.79f, 0.34f);
		implementationArchiveButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
		implementationArchiveButtonLabel.verticalOverflow = VerticalWrapMode.Truncate;
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(implementationArchiveButtonLabel);
		Outline implementationArchiveButtonLabelOutline = ((Component)implementationArchiveButtonLabel).gameObject.AddComponent<Outline>();
		implementationArchiveButtonLabelOutline.effectColor = new Color(0.04f, 0.02f, 0.01f, 0.95f);
		implementationArchiveButtonLabelOutline.effectDistance = new Vector2(2f, -2f);
		SetRect(implementationArchiveButtonLabel.rectTransform, new Vector2(0.695f, 0.86f), new Vector2(0.875f, 0.902f));
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
		StylePanel(image);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(image.rectTransform, "Archive Crest", new Vector2(0.5f, 1f), new Vector2(48f, 48f), Color.white);
		implementationArchivePanel = ((Component)image).gameObject;
		implementationArchivePanelRect = image.rectTransform;
		Canvas obj2 = implementationArchivePanel.AddComponent<Canvas>();
		obj2.overrideSorting = true;
		obj2.sortingOrder = 640;
		implementationArchivePanel.AddComponent<GraphicRaycaster>();
		Button button = CreateImageButton("Close Implementation Archive", ((Component)image).transform, font, cancelActionSprite, string.Empty);
		((UnityEvent)button.onClick).AddListener(new UnityAction(CloseImplementationArchive));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.86f, 0.895f), new Vector2(0.96f, 0.965f));
		Text title = CreateText("Implementation Archive Title", ((Component)image).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		title.text = "BORSA";
		title.color = new Color(0.95f, 0.79f, 0.34f);
		title.horizontalOverflow = HorizontalWrapMode.Wrap;
		title.verticalOverflow = VerticalWrapMode.Truncate;
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		SetRect(title.rectTransform, new Vector2(0.14f, 0.9f), new Vector2(0.86f, 0.972f));
		CreateImplementationZoneSection(((Component)image).transform, font, "Consumabili", new Vector2(0.05f, 0.745f), new Vector2(0.95f, 0.88f), out implementationConsumablesRoot, out implementationConsumablesEmptyText, string.Empty);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE NEL MAZZO", new Vector2(0.05f, 0.505f), new Vector2(0.95f, 0.725f), out implementationDeckRoot, out implementationDeckEmptyText);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE IN COOLDOWN", new Vector2(0.05f, 0.285f), new Vector2(0.95f, 0.485f), out implementationCooldownRoot, out implementationCooldownEmptyText);
		CreateImplementationZoneSection(((Component)image).transform, font, "CARTE AL CIMITERO", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.265f), out implementationGraveyardRoot, out implementationGraveyardEmptyText);
		SetImplementationArchiveVisible(false);
	}

	private void CreateImplementationZoneSection(Transform parent, Font font, string title, Vector2 minimum, Vector2 maximum, out RectTransform cardRoot, out Text emptyText, string emptyLabel = "Nessuna carta")
	{
		Image image = CreateImage(title + " Section", parent, new Color(0.02f, 0.04f, 0.052f, 0.72f));
		StylePanel(image);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(image.rectTransform, "Section Crest", new Vector2(0.5f, 1f), new Vector2(24f, 24f), new Color(0.8f, 0.96f, 1f, 0.72f));
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
		if (string.Equals(title, "Consumabili", StringComparison.OrdinalIgnoreCase))
		{
			component.constraint = GridLayoutGroup.Constraint.FixedRowCount;
			component.constraintCount = 1;
			component.spacing = new Vector2(10f, 0f);
		}
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
		ClearImplementationArchiveCards();
		ClearImplementationConsumables();
		PopulateImplementationConsumables();
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

	private void PopulateImplementationConsumables()
	{
		if ((Object)(object)implementationConsumablesRoot == (Object)null)
		{
			return;
		}
		CampaignConsumableType[] itemTypes =
		{
			CampaignConsumableType.Detector,
			CampaignConsumableType.SecondChance,
			CampaignConsumableType.Defrost,
			CampaignConsumableType.Empower,
			CampaignConsumableType.DoubleExp
		};
		if ((Object)(object)implementationConsumablesEmptyText != (Object)null)
		{
			((Component)implementationConsumablesEmptyText).gameObject.SetActive(false);
		}
		foreach (CampaignConsumableType itemType in itemTypes)
		{
			CreateImplementationConsumableView(itemType);
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
		cardInspectionSummaryText = CreateText("Inspection Summary", (Transform)(object)cardInspectionBookRoot, font, 30, (FontStyle)1, (TextAnchor)0);
		cardInspectionSummaryText.color = new Color(0.16f, 0.085f, 0.025f);
		cardInspectionSummaryText.horizontalOverflow = (HorizontalWrapMode)0;
		cardInspectionSummaryText.verticalOverflow = (VerticalWrapMode)0;
		cardInspectionSummaryText.resizeTextForBestFit = true;
		cardInspectionSummaryText.resizeTextMinSize = 22;
		cardInspectionSummaryText.resizeTextMaxSize = 34;
		SetRect(cardInspectionSummaryText.rectTransform, new Vector2(0.12f, 0.24f), new Vector2(0.84f, 0.615f));
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
		SetRect(cardInspectionStatusRoot, new Vector2(0.12f, 0.08f), new Vector2(0.84f, 0.45f));
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
			cardInspectionSummaryText.resizeTextMinSize = landscape ?22 : 20;
			cardInspectionSummaryText.resizeTextMaxSize = landscape ?36 : 34;
			SetRect(cardInspectionSummaryText.rectTransform, landscape ?new Vector2(0.49f, 0.27f) : new Vector2(0.12f, 0.24f), landscape ?new Vector2(0.91f, 0.875f) : new Vector2(0.84f, 0.615f));
		}
		if ((Object)(object)cardInspectionStatusRoot != (Object)null)
		{
			SetRect(cardInspectionStatusRoot, landscape ?new Vector2(0.495f, 0.085f) : new Vector2(0.12f, 0.08f), landscape ?new Vector2(0.915f, 0.255f) : new Vector2(0.84f, 0.225f));
		}
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
			SetRect((RectTransform)((Component)cardInspectionCloseButton).transform, landscape ?new Vector2(0.94f, 0.845f) : new Vector2(0.82f, 0.865f), landscape ?new Vector2(0.985f, 0.92f) : new Vector2(0.91f, 0.92f));
		}
		if ((Object)(object)cardInspectionDraftConfirmButtonRect != (Object)null)
		{
			SetRect(cardInspectionDraftConfirmButtonRect, landscape ?new Vector2(0.18f, 0.035f) : new Vector2(0.31f, 0.012f), landscape ?new Vector2(0.42f, 0.105f) : new Vector2(0.69f, 0.07f));
		}
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
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
			StartPvpMode();
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

	private void CreateCampaignModeSelectionView(Font font)
	{
		Image image = CreateImage("Campaign Mode Selection", (Transform)(object)safeAreaRoot, Color.clear);
		image.raycastTarget = true;
		campaignModeSelectionPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 905;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image image2 = CreateImage("Campaign Mode Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		image2.sprite = null;
		image2.preserveAspect = false;
		SetRect(image2.rectTransform, Vector2.zero, Vector2.one);
		Image image3 = CreateImage("Campaign Mode Frame", ((Component)image).transform, Color.white);
		campaignModeSelectionFrameImage = image3;
		campaignModeSelectionFrameAspectFitter = ConfigureResponsiveDeckBuilderFrame(image3);
		image3.color = new Color(1f, 1f, 1f, 0.92f);
		image3.raycastTarget = false;
		SetRect(image3.rectTransform, Vector2.zero, Vector2.one);
		campaignModeSelectionHeadingText = CreateText("Heading", ((Component)image).transform, font, 42, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(campaignModeSelectionHeadingText);
		campaignModeSelectionHeadingText.text = "SINGLE PLAYER";
		campaignModeSelectionHeadingText.color = new Color(0.95f, 0.79f, 0.34f);
		campaignModeSelectionPromptText = CreateText("Prompt", ((Component)image).transform, font, 27, (FontStyle)1, (TextAnchor)4);
		campaignModeSelectionPromptText.text = "SCEGLI UNA MODALITA'";
		campaignModeSelectionPromptText.color = new Color(0.88f, 0.92f, 0.96f);
		campaignModeSelectionPromptText.horizontalOverflow = HorizontalWrapMode.Wrap;
		campaignModeSelectionPromptText.resizeTextForBestFit = true;
		campaignModeSelectionPromptText.resizeTextMinSize = 16;
		campaignModeSelectionPromptText.resizeTextMaxSize = 27;
		campaignModeSelectionProgressText = CreateText("Single Player Progress", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
		campaignModeSelectionProgressText.color = new Color(0.95f, 0.79f, 0.34f);
		campaignModeSelectionProgressText.horizontalOverflow = HorizontalWrapMode.Wrap;
		campaignModeSelectionProgressText.resizeTextForBestFit = true;
		campaignModeSelectionProgressText.resizeTextMinSize = 14;
		campaignModeSelectionProgressText.resizeTextMaxSize = 22;
		Button button = CreateButton("Campaign Adventure Mode", ((Component)image).transform, font, "AVVENTURA");
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartAdventureMode();
		});
		campaignModeAdventureButton = button;
		campaignModeBuilderButtonRect = (RectTransform)((Component)button).transform;
		Button button2 = CreateButton("Campaign Hardcore Mode", ((Component)image).transform, font, "HARDCORE");
		((UnityEvent)button2.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			StartHardcoreMode();
		});
		campaignModeHardcoreButton = button2;
		campaignModeHardcoreButtonText = ((Component)button2).GetComponentInChildren<Text>();
		campaignModeDraftButtonRect = (RectTransform)((Component)button2).transform;
		CreateAdventureChapterView(font);
		RefreshCampaignModeSelectionLayout();
		campaignModeSelectionPanel.SetActive(false);
	}

	private void CreateAdventureChapterView(Font font)
	{
		Image image = CreateImage("Adventure Chapters", (Transform)(object)safeAreaRoot, new Color(0.006f, 0.008f, 0.01f, 1f));
		image.raycastTarget = true;
		adventureChapterPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas canvas = ((Component)image).gameObject.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 906;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image frame = CreateImage("Adventure Chapters Frame", ((Component)image).transform, Color.white);
		frame.color = new Color(1f, 1f, 1f, 0.92f);
		frame.raycastTarget = false;
		SetRect(frame.rectTransform, Vector2.zero, Vector2.one);
		adventureChapterHeadingText = CreateText("Adventure Heading", ((Component)image).transform, font, 40, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(adventureChapterHeadingText);
		adventureChapterHeadingText.text = "AVVENTURA";
		adventureChapterHeadingText.color = new Color(0.95f, 0.79f, 0.34f);
		adventureChapterProgressText = CreateText("Adventure Progress", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
		adventureChapterProgressText.color = new Color(0.88f, 0.92f, 0.96f);
		adventureChapterProgressText.horizontalOverflow = HorizontalWrapMode.Wrap;
		adventureChapterProgressText.resizeTextForBestFit = true;
		adventureChapterProgressText.resizeTextMinSize = 14;
		adventureChapterProgressText.resizeTextMaxSize = 22;
		adventureChapterListRoot = new GameObject("Adventure Chapter List", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)adventureChapterListRoot).SetParent(((Component)image).transform, false);
		GridLayoutGroup layout = ((Component)adventureChapterListRoot).GetComponent<GridLayoutGroup>();
		layout.spacing = new Vector2(18f, 18f);
		layout.childAlignment = (TextAnchor)1;
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 2;
		adventureChapterBackButton = CreateButton("Adventure Back", ((Component)image).transform, font, "INDIETRO");
		((UnityEvent)adventureChapterBackButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowCampaignModeSelection();
		});
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

		Text title = CreateText("Tutorial Confirm Title", ((Component)dialog).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		title.text = "TUTORIAL: PRIMI PASSI";
		title.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(title.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f));

		adventureTutorialConfirmBodyText = CreateText("Tutorial Confirm Body", ((Component)dialog).transform, font, 20, (FontStyle)1, (TextAnchor)4);
		adventureTutorialConfirmBodyText.text = "Entrerai in uno stage guidato: ti verra indicato cosa toccare, i tiri saranno controllati e ogni passo spieghera le basi del gioco. Al completamento riceverai vasetti di miele.";
		adventureTutorialConfirmBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		adventureTutorialConfirmBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		adventureTutorialConfirmBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		adventureTutorialConfirmBodyText.resizeTextForBestFit = true;
		adventureTutorialConfirmBodyText.resizeTextMinSize = 13;
		adventureTutorialConfirmBodyText.resizeTextMaxSize = 20;
		SetRect(adventureTutorialConfirmBodyText.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.7f));

		Button cancelButton = CreateButton("Cancel Tutorial Confirm", ((Component)dialog).transform, font, "ANNULLA");
		((UnityEvent)cancelButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideAdventureTutorialConfirmPopup();
		});
		SetRect((RectTransform)((Component)cancelButton).transform, new Vector2(0.08f, 0.1f), new Vector2(0.44f, 0.27f));

		Button goButton = CreateButton("Start Tutorial Confirm", ((Component)dialog).transform, font, "ANDIAMO");
		((UnityEvent)goButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideAdventureTutorialConfirmPopup();
			StartAdventureScriptedTutorial();
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

		guidedTutorialTitleText = CreateText("Guided Tutorial Title", ((Component)dialog).transform, font, 31, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(guidedTutorialTitleText);
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
		RefreshResponsiveDeckBuilderFrame(campaignModeSelectionFrameImage, campaignModeSelectionFrameAspectFitter, compact);
		SetRect(campaignModeSelectionHeadingText.rectTransform, compact ?new Vector2(0.08f, 0.76f) : new Vector2(0.16f, 0.73f), compact ?new Vector2(0.92f, 0.86f) : new Vector2(0.84f, 0.83f));
		campaignModeSelectionHeadingText.fontSize = compact ?46 : 42;
		campaignModeSelectionPromptText.fontSize = compact ?28 : 27;
		campaignModeSelectionPromptText.resizeTextMaxSize = campaignModeSelectionPromptText.fontSize;
		SetRect(campaignModeSelectionPromptText.rectTransform, compact ?new Vector2(0.1f, 0.62f) : new Vector2(0.18f, 0.595f), compact ?new Vector2(0.9f, 0.73f) : new Vector2(0.82f, 0.69f));
		if ((Object)(object)campaignModeSelectionProgressText != (Object)null)
		{
			SetRect(campaignModeSelectionProgressText.rectTransform, compact ?new Vector2(0.1f, 0.535f) : new Vector2(0.18f, 0.51f), compact ?new Vector2(0.9f, 0.61f) : new Vector2(0.82f, 0.585f));
		}
		if (compact)
		{
			SetRect(campaignModeBuilderButtonRect, new Vector2(0.19f, 0.385f), new Vector2(0.81f, 0.485f));
			SetRect(campaignModeDraftButtonRect, new Vector2(0.19f, 0.245f), new Vector2(0.81f, 0.345f));
		}
		else
		{
			SetRect(campaignModeBuilderButtonRect, new Vector2(0.22f, 0.325f), new Vector2(0.47f, 0.455f));
			SetRect(campaignModeDraftButtonRect, new Vector2(0.53f, 0.325f), new Vector2(0.78f, 0.455f));
		}
		RefreshSinglePlayerProgressView();
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
		SetRect(adventureChapterHeadingText.rectTransform, compact ?new Vector2(0.06f, 0.86f) : new Vector2(0.12f, 0.84f), compact ?new Vector2(0.94f, 0.945f) : new Vector2(0.88f, 0.925f));
		adventureChapterHeadingText.fontSize = compact ?42 : 42;
		SetRect(adventureChapterProgressText.rectTransform, compact ?new Vector2(0.06f, 0.785f) : new Vector2(0.12f, 0.765f), compact ?new Vector2(0.94f, 0.855f) : new Vector2(0.88f, 0.835f));
		SetRect(adventureChapterListRoot, compact ?new Vector2(0.025f, 0.125f) : new Vector2(0.045f, 0.13f), compact ?new Vector2(0.975f, 0.79f) : new Vector2(0.955f, 0.775f));
		ConfigureAdventureChapterGrid(compact);
		SetRect((RectTransform)((Component)adventureChapterBackButton).transform, compact ?new Vector2(0.28f, 0.055f) : new Vector2(0.37f, 0.06f), compact ?new Vector2(0.72f, 0.14f) : new Vector2(0.63f, 0.145f));
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
		float spacing = compact ? 8f : 16f;
		float width = Mathf.Max(1f, rect.width - spacing * (columns - 1));
		float height = Mathf.Max(1f, rect.height - spacing);
		float square = Mathf.Min(width / columns, height / 2f - (compact ? 48f : 56f), compact ? 224f : 298f);
		square = Mathf.Max(compact ? 140f : 188f, square);
		grid.constraintCount = columns;
		grid.spacing = new Vector2(spacing, compact ? 14f : 18f);
		grid.cellSize = new Vector2(square, square + (compact ? 52f : 60f));
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
		if ((Object)(object)adventureChapterProgressText != (Object)null)
		{
			adventureChapterProgressText.text = $"MIELE {singlePlayerProgressService.Honey}  -  scegli uno stage";
		}

		CreateAdventureChapterRow("tutorial", "Tutorial", "Primi Passi", 0, singlePlayerProgressService.TutorialCompleted, StartTutorialAdventureStage);
		CreateAdventureChapterRow("chapter-1", "Capitolo 1 - La Nebbia di Bragus", "Scenario: Nebbia", 25, singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-1"), () => TryOpenAdventureChapter("chapter-1", 25));
		CreateAdventureChapterRow("chapter-2", "Capitolo 2 - I Rampicanti di Trentor", "Scenario: Rampicanti", 75, singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-2"), () => TryOpenAdventureChapter("chapter-2", 75));
		CreateAdventureChapterRow("chapter-3", "Capitolo 3 - Gli Specchi di Medusa", "Scenario: Specchi", 120, singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-3"), () => TryOpenAdventureChapter("chapter-3", 120));
		CreateAdventureChapterRow("chapter-4", "Capitolo 4 - La Cosmica di Palatir", "Scenario: Cosmica", 180, singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-4"), () => TryOpenAdventureChapter("chapter-4", 180));
	}

	private void CreateAdventureChapterRow(string id, string title, string subtitle, int cost, bool unlocked, Action action)
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
		button.interactable = unlocked || cost == 0 || singlePlayerProgressService.Honey >= cost;
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			action?.Invoke();
		});
		bool useLockedImage = !unlocked && cost > 0;
		Image cover = CreateImage("Cover", row.transform, AdventureChapterPlaceholderColor(id, unlocked || cost == 0));
		cover.raycastTarget = false;
		if (!unlocked && cost > 0)
		{
			cover.sprite = LoadSpriteResource("UI/locked_chapter");
			cover.color = Color.white;
			cover.preserveAspect = false;
		}
		else
		{
			StylePanel(cover);
		}
		SetRect(cover.rectTransform, new Vector2(0f, 0.27f), new Vector2(1f, 1f));
		Image veil = CreateImage("Lock Veil", ((Component)cover).transform, useLockedImage ? new Color(0f, 0f, 0f, 0.12f) : (button.interactable ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.48f)));
		veil.raycastTarget = false;
		Stretch(veil.rectTransform);
		string status = cost == 0
			? (singlePlayerProgressService.TutorialCompleted ? "completato" : "ricompensa 60 miele")
			: (unlocked ? "sbloccato" : $"costo {cost} miele");
		Text coverText = CreateText("Cover Status", ((Component)cover).transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 16, (FontStyle)1, (TextAnchor)7);
		coverText.raycastTarget = false;
		coverText.text = subtitle.ToUpperInvariant() + "\n" + status.ToUpperInvariant();
		coverText.color = Color.white;
		coverText.horizontalOverflow = HorizontalWrapMode.Wrap;
		coverText.verticalOverflow = VerticalWrapMode.Truncate;
		coverText.resizeTextForBestFit = true;
		coverText.resizeTextMinSize = 10;
		coverText.resizeTextMaxSize = 16;
		Outline outline = ((Component)coverText).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		SetRect(coverText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
		Text titleText = CreateText("Title", row.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 17, (FontStyle)1, (TextAnchor)1);
		titleText.raycastTarget = false;
		titleText.text = title.ToUpperInvariant();
		titleText.color = button.interactable ? new Color(0.95f, 0.79f, 0.34f) : new Color(0.56f, 0.62f, 0.66f);
		titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
		titleText.verticalOverflow = VerticalWrapMode.Truncate;
		titleText.resizeTextForBestFit = true;
		titleText.resizeTextMinSize = 10;
		titleText.resizeTextMaxSize = 17;
		SetRect(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.24f));
		adventureChapterRows.Add(row);
	}

	private static Color AdventureChapterPlaceholderColor(string id, bool available)
	{
		Color color = id switch
		{
			"tutorial" => new Color(0.13f, 0.36f, 0.42f, 1f),
			"chapter-1" => new Color(0.45f, 0.25f, 0.08f, 1f),
			"chapter-2" => new Color(0.47f, 0.33f, 0.1f, 1f),
			"chapter-3" => new Color(0.24f, 0.19f, 0.42f, 1f),
			"chapter-4" => new Color(0.35f, 0.11f, 0.19f, 1f),
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
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
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
		if (debugForceFirstRoomComposableGolem || debugForceFirstRoomMedusa || debugForceFirstRoomTrentor || debugForceFirstRoomBragus || debugForceFirstRoomPalatir)
		{
			StartCampaignMode();
		}
	}

	private void StartCampaignMode()
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

	private void ShowCampaignModeSelection()
	{
		// Se c'e' una run salvata, riprendila invece di mostrare la scelta single player.
		if (HasResumableRun && TryStartResumedCampaign())
		{
			return;
		}
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(true);
			campaignModeSelectionPanel.transform.SetAsLastSibling();
		}
		RefreshSinglePlayerProgressView();
		RefreshCampaignModeSelectionLayout();
		inputLocked = true;
		EnsureServerProgressAsync();
	}

	/// <summary>
	/// Stabilisce (una volta) la connessione autoritativa al server per la progressione single
	/// player e specchia lo stato nel servizio locale usato dalla UI. Se il server non e'
	/// raggiungibile o rifiuta il login, resta attivo il servizio locale senza errori a video.
	/// </summary>
	private async void EnsureServerProgressAsync()
	{
		if (!ServerProgressEnabled || ServerProgressReady)
		{
			return;
		}
		// serverProgress non-null ma link non pronto (connessione caduta): si tenta la riconnessione.
		serverProgress = null;
		if ((Object)(object)singlePlayerServerLink == (Object)null)
		{
			singlePlayerServerLink = gameObject.AddComponent<AccardND.Network.SinglePlayerServerLink>();
		}
		AccardND.Network.ServerSinglePlayerProgressRepository repository =
			await singlePlayerServerLink.EnsureRepositoryAsync();
		if (repository == null)
		{
			return;
		}
		serverProgress = repository;
		MirrorServerProgress();
		RefreshSinglePlayerProgressView();
		if ((Object)(object)adventureChapterPanel != (Object)null && adventureChapterPanel.activeSelf)
		{
			RefreshAdventureChapterList();
		}
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
		if ((Object)(object)campaignModeSelectionProgressText != (Object)null)
		{
			string tutorial = singlePlayerProgressService.TutorialCompleted ? "Tutorial completato" : "Tutorial da completare";
			string hardcore = singlePlayerProgressService.HardcoreUnlocked ? "Hardcore sbloccata" : $"Hardcore: {HardcoreUnlockHoneyCost} miele";
			campaignModeSelectionProgressText.text = $"MIELE {singlePlayerProgressService.Honey}  -  {tutorial}  -  {hardcore}";
		}
		if ((Object)(object)campaignModeHardcoreButtonText != (Object)null)
		{
			campaignModeHardcoreButtonText.text = singlePlayerProgressService.HardcoreUnlocked
				? "HARDCORE"
				: $"SBLOCCA HARDCORE ({HardcoreUnlockHoneyCost})";
		}
		if ((Object)(object)campaignModeHardcoreButton != (Object)null)
		{
			campaignModeHardcoreButton.interactable = singlePlayerProgressService.HardcoreUnlocked
				|| singlePlayerProgressService.Honey >= HardcoreUnlockHoneyCost;
		}
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
		adventureTutorialConfirmPopup.SetActive(true);
		adventureTutorialConfirmPopup.transform.SetAsLastSibling();
	}

	private void HideAdventureTutorialConfirmPopup()
	{
		if ((Object)(object)adventureTutorialConfirmPopup != (Object)null)
		{
			adventureTutorialConfirmPopup.SetActive(false);
		}
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
			_ => ("RICOMPENSA", "Completando il tutorial ricevi vasetti di miele. Il miele serve per sbloccare capitoli, scenari, classi, abilita e la modalita Hardcore.")
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
					AccardND.Network.SinglePlayerRewardOutcome outcome =
						await serverProgress.ClaimTutorialRewardAsync(System.Guid.NewGuid().ToString("N"));
					MirrorServerProgress();
					AppendLog($"AVVENTURA - tutorial completato (server): +{outcome.GrantedHoney} miele.");
					SetMessage($"Tutorial completato. Hai ottenuto {outcome.GrantedHoney} vasetti di miele.");
				}
				catch (System.Exception exception)
				{
					AppendLog($"AVVENTURA - reward tutorial rifiutata dal server: {exception.Message}");
					SetMessage(exception.Message);
				}
				RefreshAdventureChapterList();
				return;
			}

			singlePlayerProgressService.SetTutorialCompleted();
			singlePlayerProgressService.AddHoney(TutorialCompletionHoneyReward);
			AppendLog($"AVVENTURA - tutorial provvisorio completato: +{TutorialCompletionHoneyReward} miele.");
			SetMessage($"Tutorial completato. Hai ottenuto {TutorialCompletionHoneyReward} vasetti di miele.");
			RefreshAdventureChapterList();
			return;
		}

		SetMessage("Tutorial gia completato. Il tutorial guidato giocabile sara il prossimo step.");
		AppendLog("AVVENTURA - tutorial selezionato: director guidato non ancora implementato.");
	}

	private async void TryOpenAdventureChapter(string chapterId, int cost)
	{
		if (!singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, chapterId))
		{
			if (ServerProgressReady)
			{
				try
				{
					await serverProgress.PurchaseUnlockAsync(SinglePlayerUnlockType.Chapter, chapterId);
					MirrorServerProgress();
					SetMessage("Capitolo sbloccato.");
					AppendLog($"AVVENTURA - {chapterId} sbloccato (server).");
				}
				catch (System.Exception exception)
				{
					SetMessage(exception.Message);
					AppendLog($"AVVENTURA - acquisto {chapterId} rifiutato dal server: {exception.Message}");
				}
				RefreshAdventureChapterList();
				return;
			}

			if (!singlePlayerProgressService.TrySpendHoney(cost))
			{
				SetMessage($"Capitolo bloccato. Servono {cost} vasetti di miele.");
				AppendLog($"AVVENTURA - acquisto {chapterId} rifiutato: miele insufficiente.");
				RefreshAdventureChapterList();
				return;
			}
			singlePlayerProgressService.Unlock(SinglePlayerUnlockType.Chapter, chapterId);
			SetMessage("Capitolo sbloccato.");
			AppendLog($"AVVENTURA - {chapterId} sbloccato per {cost} miele.");
			RefreshAdventureChapterList();
			return;
		}

		SetMessage("Capitolo selezionato. Stage e scenario fisso saranno collegati nel prossimo step.");
		AppendLog($"AVVENTURA - {chapterId} selezionato: stage config non ancora implementata.");
	}

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

			if (!singlePlayerProgressService.TrySpendHoney(HardcoreUnlockHoneyCost))
			{
				SetMessage($"Hardcore bloccata. Servono {HardcoreUnlockHoneyCost} vasetti di miele.");
				AppendLog("SINGLE PLAYER - acquisto Hardcore rifiutato: miele insufficiente.");
				RefreshSinglePlayerProgressView();
				return;
			}
			singlePlayerProgressService.SetHardcoreUnlocked();
			SetMessage("Hardcore sbloccata.");
			AppendLog($"SINGLE PLAYER - Hardcore sbloccata per {HardcoreUnlockHoneyCost} miele.");
			RefreshSinglePlayerProgressView();
			return;
		}
		StartCampaignBuilderMode();
	}

	private void StartCampaignBuilderMode()
	{
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		inputLocked = false;
		ShowCampaignIntroHint();
		LoadBattle();
	}

	private void StartPvpMode()
	{
		PlayerPrefs.SetInt("AccardND.GuestMode", 0);
		PlayerPrefs.Save();
		// Chiude la connessione di progressione single player: il PvP apre la propria sullo stesso
		// account ospite e non vogliamo due socket attivi. Alla prossima apertura del menu campagna
		// il link si riconnette. La cache locale conserva l'ultimo stato mostrato.
		if ((Object)(object)singlePlayerServerLink != (Object)null)
		{
			singlePlayerServerLink.Shutdown();
		}
		serverProgress = null;
		ReturnToStart(showModeSelection: false);
		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		GameObject gameObject = new GameObject("Pvp Mode");
		AccardND.PvpUi.PvpBootstrap pvpBootstrap = gameObject.AddComponent<AccardND.PvpUi.PvpBootstrap>();
		activePvpBootstrap = pvpBootstrap;
		pvpBootstrap.Configure(cardDatabase, delegate
		{
			if ((Object)(object)activePvpBootstrap == (Object)(object)pvpBootstrap)
			{
				activePvpBootstrap = null;
			}
			if ((Object)(object)modeSelectionPanel != (Object)null)
			{
				modeSelectionPanel.SetActive(true);
			}
		}, this);
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
		deckBuilderFrameImage = image3;
		deckBuilderFrameAspectFitter = ConfigureResponsiveDeckBuilderFrame(image3);
		image3.color = new Color(1f, 1f, 1f, 0.92f);
		image3.raycastTarget = false;
		SetRect(image3.rectTransform, Vector2.zero, Vector2.one);
		Text text = CreateText("Heading", ((Component)image).transform, font, 42, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text);
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

	private void CreateInitialDraftView(Font font)
	{
		Image image = CreateImage("Initial Draft", (Transform)(object)safeAreaRoot, Color.clear);
		image.raycastTarget = true;
		initialDraftPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, Vector2.zero, Vector2.one);
		Canvas obj = ((Component)image).gameObject.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 525;
		((Component)image).gameObject.AddComponent<GraphicRaycaster>();
		Image image2 = CreateImage("Initial Draft Background", ((Component)image).transform, new Color(0.006f, 0.008f, 0.01f, 1f));
		image2.sprite = null;
		image2.preserveAspect = false;
		SetRect(image2.rectTransform, Vector2.zero, Vector2.one);
		Image image3 = CreateImage("Initial Draft Frame", ((Component)image).transform, Color.white);
		initialDraftFrameImage = image3;
		initialDraftFrameAspectFitter = ConfigureResponsiveDeckBuilderFrame(image3);
		image3.color = new Color(1f, 1f, 1f, 0.92f);
		image3.raycastTarget = false;
		SetRect(image3.rectTransform, Vector2.zero, Vector2.one);
		initialDraftHeadingText = CreateText("Heading", ((Component)image).transform, font, 42, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(initialDraftHeadingText);
		initialDraftHeadingText.color = new Color(0.95f, 0.79f, 0.34f);
		initialDraftStatusText = CreateText("Status", ((Component)image).transform, font, 25, (FontStyle)1, (TextAnchor)4);
		initialDraftStatusText.color = Color.white;
		initialDraftPromptText = CreateText("Prompt", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
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
		((Transform)initialDraftOffersRoot).SetParent(((Component)image).transform, false);
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
		((Transform)initialDraftDeckRoot).SetParent(((Component)image).transform, false);
		GridLayoutGroup deckGrid = ((Component)initialDraftDeckRoot).GetComponent<GridLayoutGroup>();
		deckGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		deckGrid.constraintCount = 9;
		deckGrid.spacing = new Vector2(6f, 6f);
		deckGrid.childAlignment = TextAnchor.MiddleCenter;
		initialDraftDeckText = CreateText("Draft Empty Deck Hint", ((Component)image).transform, font, 18, (FontStyle)1, (TextAnchor)4);
		initialDraftDeckText.color = new Color(0.88f, 0.92f, 0.96f);
		initialDraftDeckText.text = "Il mazzo draft apparira' qui.";
		initialDraftConfirmButton = CreateButton("Confirm Draft Picks", ((Component)image).transform, font, "CONFERMA");
		((UnityEvent)initialDraftConfirmButton.onClick).AddListener(new UnityAction(ConfirmInitialDraftSelection));
		initialDraftConfirmButtonRect = (RectTransform)((Component)initialDraftConfirmButton).transform;
		initialDraftConfirmButtonText = ((Component)initialDraftConfirmButton).GetComponentInChildren<Text>();
		RefreshInitialDraftLayout();
		initialDraftPanel.SetActive(false);
	}

	private AspectRatioFitter ConfigureResponsiveDeckBuilderFrame(Image image)
	{
		bool portrait = Screen.height >= Screen.width;
		Sprite sprite = LoadSpriteResource(portrait ?"UI/deck_builder_frame_portrait_v2" : "UI/deck_builder_frame_landscape_v2");
		image.sprite = sprite;
		return ConfigureFittedBackground(image, sprite, portrait ?941f / 1672f : 1672f / 941f);
	}

	private void RefreshResponsiveDeckBuilderFrame(Image image, AspectRatioFitter aspectFitter, bool compact)
	{
		if ((Object)(object)image == (Object)null)
		{
			return;
		}
		Sprite sprite = LoadSpriteResource(compact ?"UI/deck_builder_frame_portrait_v2" : "UI/deck_builder_frame_landscape_v2");
		image.sprite = sprite;
		image.preserveAspect = true;
		if ((Object)(object)aspectFitter != (Object)null)
		{
			aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			aspectFitter.aspectRatio = (Object)(object)sprite != (Object)null ?sprite.rect.width / sprite.rect.height : (compact ?941f / 1672f : 1672f / 941f);
		}
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
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(combatScoreText);
		combatScoreText.color = Color.white;
		SetRect(combatScoreText.rectTransform, new Vector2(0.03f, 0.34f), new Vector2(0.97f, 0.98f));
		combatDiceText = CreateText("Dice Delta", ((Component)image).transform, font, 32, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(combatDiceText);
		combatDiceText.color = new Color(0.72f, 0.9f, 1f);
		SetRect(combatDiceText.rectTransform, new Vector2(0.03f, 0.4f), new Vector2(0.97f, 0.7f));
		combatOutcomeText = CreateText("Outcome", ((Component)image).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(combatOutcomeText);
		SetRect(combatOutcomeText.rectTransform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.38f));
		combatResultRoot.SetActive(false);
	}
}
}
