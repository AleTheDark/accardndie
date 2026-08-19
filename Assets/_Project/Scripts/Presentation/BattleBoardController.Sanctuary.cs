using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	// Schermata del Santuario: converte la progressione di campagna in sblocchi permanenti.
	// Catalogo, costi e prove arrivano gia' valutati dal server (sanctuary.get): qui si
	// disegna soltanto, cosi' un ritocco di bilanciamento non richiede una nuova build.

	private enum SanctuaryAltar
	{
		Classes,
		Techniques,
		Relics
	}

	private GameObject sanctuaryPanel;

	private Image sanctuaryBackgroundImage;

	private Image sanctuaryScreenOuterFrameImage;

	private Image sanctuaryTitlePanel;

	private Text sanctuaryHeadingText;

	private Text sanctuarySubtitleText;

	private Text sanctuaryStatusText;

	private Image sanctuaryDiscoveryPanelImage;

	private Image sanctuaryDiscoveryIconImage;

	private Text sanctuaryDiscoveryTitleText;

	private Text sanctuaryDiscoveryCountText;

	private Text sanctuaryDiscoveryPercentText;

	private Image sanctuaryDiscoveryProgressFillImage;



	private Image sanctuaryListViewportImage;

	private ScrollRect sanctuaryScrollRect;

	private RectTransform sanctuaryListRoot;

	private readonly List<Button> sanctuaryAltarButtons = new List<Button>();

	private readonly List<Image> sanctuaryAltarIcons = new List<Image>();

	private readonly List<GameObject> sanctuaryCards = new List<GameObject>();

	private SanctuaryAltar sanctuaryActiveAltar = SanctuaryAltar.Techniques;

	private SanctuaryData sanctuaryData;

	private bool sanctuaryLoading;

	private bool sanctuaryUsesPrefabLayout;

	private GameObject sanctuaryConfirmPopup;

	private Image sanctuaryConfirmDialogImage;

	private Text sanctuaryConfirmTitleText;

	private Text sanctuaryConfirmBodyText;

	private Button sanctuaryConfirmButton;

	private Text sanctuaryConfirmButtonText;

	private SanctuaryEntryData sanctuaryPendingEntry;

	private bool sanctuaryPurchasing;

	// Esito dell'ultima offerta. Sopravvive al ricaricamento del catalogo, altrimenti il
	// messaggio generico dell'altare cancellerebbe subito l'unico riscontro dell'acquisto.
	private string sanctuaryNotice;

	private static readonly Color SanctuaryGold = new(0.95f, 0.79f, 0.34f);

	private static readonly Color SanctuaryDim = new(0.56f, 0.62f, 0.66f);

	private static readonly Color SanctuaryOwned = new(0.55f, 0.85f, 0.6f);

	// --- Costruzione ---

	private void CreateSanctuaryView(Font font)
	{
		GameObject sanctuaryPrefab = Resources.Load<GameObject>("UI/Prefabs/SanctuaryRoom");
		if ((Object)(object)sanctuaryPrefab != (Object)null)
		{
			CreateSanctuaryViewFromPrefab(sanctuaryPrefab);
			return;
		}

		Image root = CreateImage("Sanctuary", (Transform)(object)canvasRect, new Color(0.006f, 0.008f, 0.01f, 1f));
		root.raycastTarget = true;
		sanctuaryPanel = ((Component)root).gameObject;
		SetRect(root.rectTransform, Vector2.zero, Vector2.one);
		Canvas canvas = ((Component)root).gameObject.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 900;
		((Component)root).gameObject.AddComponent<GraphicRaycaster>();

		Image background = CreateImage("Sanctuary Background", ((Component)root).transform, Color.white);
		sanctuaryBackgroundImage = background;
		background.sprite = LoadSpriteResource("UI/Sanctuary/sanctuary_background");
		background.type = Image.Type.Simple;
		background.color = new Color(0.84f, 0.87f, 0.94f, 0.96f);
		background.raycastTarget = false;
		SetRect(background.rectTransform, Vector2.zero, Vector2.one);
		if ((Object)(object)background.sprite != (Object)null)
		{
			AspectRatioFitter backgroundFitter = ((Component)background).gameObject.AddComponent<AspectRatioFitter>();
			backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			backgroundFitter.aspectRatio = background.sprite.rect.width / Mathf.Max(1f, background.sprite.rect.height);
		}

		Image backgroundVeil = CreateImage("Sanctuary Background Veil", ((Component)root).transform, new Color(0f, 1f / 255f, 4f / 255f, 0.4196078f));
		backgroundVeil.raycastTarget = false;
		SetRect(backgroundVeil.rectTransform, Vector2.zero, Vector2.one);

		sanctuaryScreenOuterFrameImage = CreateImage("Screen Outer Frame", ((Component)root).transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(sanctuaryScreenOuterFrameImage);
		SetRect(sanctuaryScreenOuterFrameImage.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.775f));

		// Il contenuto vive direttamente sul fondale, dentro la cornice perimetrale,
		// mentre l'account HUD condiviso galleggia sopra.
		Transform content = ((Component)root).transform;

		sanctuaryTitlePanel = CreateImage("Sanctuary Title Panel", content, Color.white);
		sanctuaryTitlePanel.raycastTarget = false;
		sanctuaryTitlePanel.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		sanctuaryTitlePanel.type = Image.Type.Simple;
		sanctuaryTitlePanel.preserveAspect = false;

		sanctuaryHeadingText = CreateText(
			"Sanctuary Heading",
			((Component)sanctuaryTitlePanel).transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? font,
			48,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(sanctuaryHeadingText);
		sanctuaryHeadingText.text = GameText.Get(GameTextKeys.Sanctuary.Title);
		sanctuaryHeadingText.color = SanctuaryGold;
		AddSanctuaryTextOutline(sanctuaryHeadingText);
		SetRect(sanctuaryHeadingText.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		sanctuaryHeadingText.rectTransform.offsetMin = new Vector2(0f, -23f);
		sanctuaryHeadingText.rectTransform.offsetMax = new Vector2(0f, -23f);

		sanctuarySubtitleText = CreateText(
			"Sanctuary Subtitle",
			((Component)sanctuaryTitlePanel).transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			15,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		sanctuarySubtitleText.text = string.Empty;
		sanctuarySubtitleText.color = new Color(0.82f, 0.68f, 0.4f);
		sanctuarySubtitleText.raycastTarget = false;
		sanctuarySubtitleText.resizeTextForBestFit = true;
		sanctuarySubtitleText.resizeTextMinSize = 10;
		sanctuarySubtitleText.resizeTextMaxSize = 15;
		SetRect(sanctuarySubtitleText.rectTransform, new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.34f));
		((Component)sanctuarySubtitleText).gameObject.SetActive(false);

		CreateSanctuaryAltarButton(content, font, SanctuaryAltarLabel(SanctuaryAltar.Classes), SanctuaryAltar.Classes);
		CreateSanctuaryAltarButton(content, font, SanctuaryAltarLabel(SanctuaryAltar.Techniques), SanctuaryAltar.Techniques);
		CreateSanctuaryAltarButton(content, font, SanctuaryAltarLabel(SanctuaryAltar.Relics), SanctuaryAltar.Relics);

		sanctuaryDiscoveryPanelImage = CreateImage("Sanctuary Discovery Summary", content, Color.white);
		sanctuaryDiscoveryPanelImage.sprite = LoadSpriteResource("UI/Sanctuary/sanctuary_tab_frame_v2");
		sanctuaryDiscoveryPanelImage.type = Image.Type.Simple;
		sanctuaryDiscoveryPanelImage.preserveAspect = false;
		sanctuaryDiscoveryPanelImage.raycastTarget = false;

		sanctuaryDiscoveryIconImage = CreateImage(
			"Discovery Icon",
			((Component)sanctuaryDiscoveryPanelImage).transform,
			new Color(0.9f, 0.76f, 1f));
		sanctuaryDiscoveryIconImage.preserveAspect = true;
		sanctuaryDiscoveryIconImage.raycastTarget = false;
		SetRect(sanctuaryDiscoveryIconImage.rectTransform, new Vector2(0.035f, 0.16f), new Vector2(0.155f, 0.84f));

		sanctuaryDiscoveryTitleText = CreateText(
			"Discovery Title",
			((Component)sanctuaryDiscoveryPanelImage).transform,
			AccardND.Battlefield.MmoUiTheme.TitleBoldFont,
			16,
			FontStyle.Normal,
			TextAnchor.MiddleLeft);
		sanctuaryDiscoveryTitleText.color = SanctuaryGold;
		sanctuaryDiscoveryTitleText.raycastTarget = false;
		AddSanctuaryTextOutline(sanctuaryDiscoveryTitleText);
		SetRect(sanctuaryDiscoveryTitleText.rectTransform, new Vector2(0.18f, 0.58f), new Vector2(0.72f, 0.9f));

		sanctuaryStatusText = CreateText(
			"Discovery Description",
			((Component)sanctuaryDiscoveryPanelImage).transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			12,
			FontStyle.Normal,
			TextAnchor.UpperLeft);
		sanctuaryStatusText.color = new Color(0.82f, 0.81f, 0.78f);
		sanctuaryStatusText.raycastTarget = false;
		sanctuaryStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
		sanctuaryStatusText.verticalOverflow = VerticalWrapMode.Truncate;
		sanctuaryStatusText.resizeTextForBestFit = true;
		sanctuaryStatusText.resizeTextMinSize = 8;
		sanctuaryStatusText.resizeTextMaxSize = 12;
		SetRect(sanctuaryStatusText.rectTransform, new Vector2(0.18f, 0.12f), new Vector2(0.72f, 0.6f));

		sanctuaryDiscoveryCountText = CreateText(
			"Discovery Count",
			((Component)sanctuaryDiscoveryPanelImage).transform,
			AccardND.Battlefield.MmoUiTheme.TitleBoldFont,
			17,
			FontStyle.Normal,
			TextAnchor.MiddleRight);
		sanctuaryDiscoveryCountText.color = new Color(0.93f, 0.91f, 0.88f);
		sanctuaryDiscoveryCountText.raycastTarget = false;
		AddSanctuaryTextOutline(sanctuaryDiscoveryCountText);
		SetRect(sanctuaryDiscoveryCountText.rectTransform, new Vector2(0.73f, 0.56f), new Vector2(0.96f, 0.91f));

		sanctuaryDiscoveryPercentText = CreateText(
			"Discovery Percent",
			((Component)sanctuaryDiscoveryPanelImage).transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			11,
			FontStyle.Bold,
			TextAnchor.MiddleCenter);
		sanctuaryDiscoveryPercentText.color = new Color(0.75f, 0.39f, 0.96f);
		sanctuaryDiscoveryPercentText.raycastTarget = false;
		SetRect(sanctuaryDiscoveryPercentText.rectTransform, new Vector2(0.78f, 0.36f), new Vector2(0.96f, 0.58f));

		Image progressTrack = CreateImage(
			"Discovery Progress Track",
			((Component)sanctuaryDiscoveryPanelImage).transform,
			new Color(0.12f, 0.035f, 0.19f, 0.95f));
		progressTrack.raycastTarget = false;
		SetRect(progressTrack.rectTransform, new Vector2(0.72f, 0.14f), new Vector2(0.96f, 0.25f));

		sanctuaryDiscoveryProgressFillImage = CreateImage(
			"Discovery Progress Fill",
			((Component)progressTrack).transform,
			new Color(0.48f, 0.16f, 0.82f, 1f));
		sanctuaryDiscoveryProgressFillImage.raycastTarget = false;
		SetRect(sanctuaryDiscoveryProgressFillImage.rectTransform, Vector2.zero, new Vector2(0.01f, 1f));
		((Component)sanctuaryDiscoveryPanelImage).gameObject.SetActive(false);

		Image viewport = CreateImage("Sanctuary Viewport", content, new Color(0.005f, 0.007f, 0.012f, 0.18f));
		sanctuaryListViewportImage = viewport;
		viewport.rectTransform.localScale = new Vector3(0.95f, 0.95f, 1f);
		viewport.sprite = null;
		viewport.type = Image.Type.Simple;
		viewport.raycastTarget = true;
		((Component)viewport).gameObject.AddComponent<RectMask2D>();
		sanctuaryScrollRect = ((Component)viewport).gameObject.AddComponent<ScrollRect>();
		sanctuaryScrollRect.viewport = viewport.rectTransform;
		sanctuaryScrollRect.horizontal = false;
		sanctuaryScrollRect.vertical = true;
		sanctuaryScrollRect.inertia = true;
		sanctuaryScrollRect.decelerationRate = 0.16f;
		sanctuaryScrollRect.scrollSensitivity = 42f;
		sanctuaryScrollRect.movementType = ScrollRect.MovementType.Clamped;

		sanctuaryListRoot = new GameObject("Sanctuary List", new Type[2]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)sanctuaryListRoot).SetParent(((Component)viewport).transform, false);
		sanctuaryListRoot.anchorMin = new Vector2(0f, 1f);
		sanctuaryListRoot.anchorMax = new Vector2(1f, 1f);
		sanctuaryListRoot.pivot = new Vector2(0.5f, 1f);
		sanctuaryListRoot.anchoredPosition = Vector2.zero;
		sanctuaryListRoot.sizeDelta = Vector2.zero;
		GridLayoutGroup layout = ((Component)sanctuaryListRoot).GetComponent<GridLayoutGroup>();
		layout.spacing = new Vector2(16f, 16f);
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 3;
		ContentSizeFitter contentFitter = ((Component)sanctuaryListRoot).gameObject.AddComponent<ContentSizeFitter>();
		contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		sanctuaryScrollRect.content = sanctuaryListRoot;

		CreateSanctuaryConfirmPopup(((Component)root).transform, font);
		RefreshSanctuaryLayout();
		sanctuaryPanel.SetActive(false);
	}

	private void CreateSanctuaryViewFromPrefab(GameObject prefab)
	{
		sanctuaryPanel = Object.Instantiate(prefab, (Transform)(object)canvasRect, false);
		sanctuaryPanel.name = "Sanctuary";
		sanctuaryUsesPrefabLayout = true;

		RectTransform rootRect = sanctuaryPanel.GetComponent<RectTransform>();
		SetRect(rootRect, Vector2.zero, Vector2.one);
		Canvas rootCanvas = sanctuaryPanel.GetComponent<Canvas>();
		if ((Object)(object)rootCanvas == (Object)null)
		{
			rootCanvas = sanctuaryPanel.AddComponent<Canvas>();
		}
		rootCanvas.overrideSorting = true;
		rootCanvas.sortingOrder = 900;
		if ((Object)(object)sanctuaryPanel.GetComponent<GraphicRaycaster>() == (Object)null)
		{
			sanctuaryPanel.AddComponent<GraphicRaycaster>();
		}

		sanctuaryBackgroundImage = SanctuaryPrefabComponent<Image>("Sanctuary Background");
		sanctuaryScreenOuterFrameImage = SanctuaryPrefabComponent<Image>("Screen Outer Frame");
		sanctuaryTitlePanel = SanctuaryPrefabComponent<Image>("Sanctuary Title Panel");
		sanctuaryHeadingText = SanctuaryPrefabComponent<Text>("Sanctuary Heading");
		sanctuarySubtitleText = SanctuaryPrefabComponent<Text>("Sanctuary Subtitle");
		if ((Object)(object)sanctuaryHeadingText != (Object)null)
		{
			sanctuaryHeadingText.text = GameText.Get(GameTextKeys.Sanctuary.Title);
			sanctuaryHeadingText.fontSize = 48;
			sanctuaryHeadingText.resizeTextMaxSize = 48;
			sanctuaryHeadingText.alignment = TextAnchor.MiddleCenter;
			SetRect(sanctuaryHeadingText.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
			sanctuaryHeadingText.rectTransform.offsetMin = new Vector2(0f, -23f);
			sanctuaryHeadingText.rectTransform.offsetMax = new Vector2(0f, -23f);
		}
		if ((Object)(object)sanctuarySubtitleText != (Object)null)
		{
			sanctuarySubtitleText.text = string.Empty;
			((Component)sanctuarySubtitleText).gameObject.SetActive(false);
		}
		sanctuaryDiscoveryPanelImage = SanctuaryPrefabComponent<Image>("Sanctuary Discovery Summary");
		sanctuaryDiscoveryIconImage = SanctuaryPrefabComponent<Image>("Discovery Icon");
		sanctuaryDiscoveryTitleText = SanctuaryPrefabComponent<Text>("Discovery Title");
		sanctuaryStatusText = SanctuaryPrefabComponent<Text>("Discovery Description");
		sanctuaryDiscoveryCountText = SanctuaryPrefabComponent<Text>("Discovery Count");
		sanctuaryDiscoveryPercentText = SanctuaryPrefabComponent<Text>("Discovery Percent");
		sanctuaryDiscoveryProgressFillImage = SanctuaryPrefabComponent<Image>("Discovery Progress Fill");
		if ((Object)(object)sanctuaryDiscoveryPanelImage != (Object)null)
		{
			((Component)sanctuaryDiscoveryPanelImage).gameObject.SetActive(false);
		}
		sanctuaryListViewportImage = SanctuaryPrefabComponent<Image>("Sanctuary Viewport");
		sanctuaryScrollRect = SanctuaryPrefabComponent<ScrollRect>("Sanctuary Viewport");
		sanctuaryListRoot = SanctuaryPrefabComponent<RectTransform>("Sanctuary List");
		if ((Object)(object)sanctuaryScrollRect != (Object)null)
		{
			sanctuaryScrollRect.viewport = sanctuaryListViewportImage.rectTransform;
			sanctuaryScrollRect.content = sanctuaryListRoot;
		}

		sanctuaryAltarButtons.Clear();
		sanctuaryAltarIcons.Clear();
		BindSanctuaryPrefabAltar(SanctuaryAltar.Classes);
		BindSanctuaryPrefabAltar(SanctuaryAltar.Techniques);
		BindSanctuaryPrefabAltar(SanctuaryAltar.Relics);

		sanctuaryConfirmPopup = SanctuaryPrefabObject("Sanctuary Confirm Popup");
		Image popupOverlay = SanctuaryPrefabComponent<Image>("Sanctuary Confirm Popup");
		if ((Object)(object)popupOverlay != (Object)null)
		{
			popupOverlay.sprite = null;
			popupOverlay.type = Image.Type.Simple;
			popupOverlay.color = new Color(0f, 0f, 0f, 0.68f);
		}
		sanctuaryConfirmDialogImage = SanctuaryPrefabComponent<Image>("Sanctuary Confirm Dialog");
		if ((Object)(object)sanctuaryConfirmDialogImage != (Object)null)
		{
			sanctuaryConfirmDialogImage.color = new Color(0.012f, 0.018f, 0.032f, 0.98f);
			StylePanel(sanctuaryConfirmDialogImage);
		}
		Image confirmCrest = SanctuaryPrefabComponent<Image>("Sanctuary Confirm Crest");
		if ((Object)(object)confirmCrest != (Object)null)
		{
			confirmCrest.sprite = AccardND.Battlefield.MmoUiTheme.GetGemSprite();
			confirmCrest.type = Image.Type.Simple;
			confirmCrest.preserveAspect = true;
			confirmCrest.color = Color.white;
			confirmCrest.raycastTarget = false;
		}
		sanctuaryConfirmTitleText = SanctuaryPrefabComponent<Text>("Sanctuary Confirm Title");
		sanctuaryConfirmBodyText = SanctuaryPrefabComponent<Text>("Sanctuary Confirm Body");
		StyleSanctuaryConfirmTypography();
		Button cancelButton = SanctuaryPrefabComponent<Button>("Sanctuary Confirm Cancel");
		if ((Object)(object)cancelButton != (Object)null)
		{
			Text cancelLabel = ((Component)cancelButton).GetComponentInChildren<Text>();
			if ((Object)(object)cancelLabel != (Object)null)
				cancelLabel.text = GameText.Get(GameTextKeys.Common.Cancel);
			ApplySanctuaryCampaignCta(cancelButton, "UI/CampaignRestyle/campaign_cta_back_red");
			ScaleSanctuaryConfirmButton(cancelButton);
			cancelButton.onClick.RemoveAllListeners();
			((UnityEvent)cancelButton.onClick).AddListener((UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				HideSanctuaryConfirmPopup();
			});
		}
		sanctuaryConfirmButton = SanctuaryPrefabComponent<Button>("Sanctuary Confirm Accept");
		if ((Object)(object)sanctuaryConfirmButton != (Object)null)
		{
			ApplySanctuaryCampaignCta(
				sanctuaryConfirmButton,
				"UI/CampaignRestyle/campaign_cta_orange");
			ScaleSanctuaryConfirmButton(sanctuaryConfirmButton);
			sanctuaryConfirmButton.onClick.RemoveAllListeners();
			((UnityEvent)sanctuaryConfirmButton.onClick).AddListener((UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				ConfirmSanctuaryPurchase();
			});
			sanctuaryConfirmButtonText = ((Component)sanctuaryConfirmButton).GetComponentInChildren<Text>();
			if ((Object)(object)sanctuaryConfirmButtonText != (Object)null)
				sanctuaryConfirmButtonText.text = GameText.Get(GameTextKeys.Sanctuary.OfferHoney);
		}

		HideSanctuaryConfirmPopup();
		RefreshSanctuaryAltarButtons();
		RefreshSanctuaryDiscoverySummary();
		ConfigureSanctuaryGrid(IsCompactLayout(
			Mathf.Max(1f, safeAreaRoot.rect.width) / Mathf.Max(1f, safeAreaRoot.rect.height),
			configuration.ResponsiveLayout));
		sanctuaryPanel.SetActive(false);
	}

	private void BindSanctuaryPrefabAltar(SanctuaryAltar altar)
	{
		Button button = SanctuaryPrefabComponent<Button>("Sanctuary Altar " + altar);
		if ((Object)(object)button == (Object)null)
		{
			return;
		}

		SanctuaryAltar boundAltar = altar;
		button.onClick.RemoveAllListeners();
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			SelectSanctuaryAltar(boundAltar);
		});
		sanctuaryAltarButtons.Add(button);
		Text altarLabel = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)altarLabel != (Object)null)
			altarLabel.text = SanctuaryAltarLabel(altar);
		Transform iconTransform = FindSanctuaryPrefabTransform(((Component)button).transform, "Icon");
		sanctuaryAltarIcons.Add(
			(Object)(object)iconTransform != (Object)null ? ((Component)iconTransform).GetComponent<Image>() : null);
		StyleSanctuaryAltarButton(button);
		AttachSanctuaryAltarVfx(button);
	}

	private static string SanctuaryAltarEmblemResource(SanctuaryAltar altar) => altar switch
	{
		SanctuaryAltar.Classes => "UI/Sanctuary/sanctuary_classes_emblem_aaa",
		SanctuaryAltar.Techniques => "UI/Sanctuary/sanctuary_techniques_emblem_aaa",
		_ => "UI/Sanctuary/sanctuary_relics_emblem_aaa"
	};

	private T SanctuaryPrefabComponent<T>(string objectName) where T : Component
	{
		GameObject target = SanctuaryPrefabObject(objectName);
		return (Object)(object)target != (Object)null ? target.GetComponent<T>() : null;
	}

	private GameObject SanctuaryPrefabObject(string objectName)
	{
		if ((Object)(object)sanctuaryPanel == (Object)null)
		{
			return null;
		}
		Transform found = FindSanctuaryPrefabTransform(sanctuaryPanel.transform, objectName);
		return (Object)(object)found != (Object)null ? ((Component)found).gameObject : null;
	}

	private static Transform FindSanctuaryPrefabTransform(Transform root, string objectName)
	{
		if ((Object)(object)root == (Object)null)
		{
			return null;
		}
		if (root.name == objectName)
		{
			return root;
		}
		for (int index = 0; index < root.childCount; index++)
		{
			Transform found = FindSanctuaryPrefabTransform(root.GetChild(index), objectName);
			if ((Object)(object)found != (Object)null)
			{
				return found;
			}
		}
		return null;
	}

	private void CreateSanctuaryConfirmPopup(Transform parent, Font font)
	{
		Image overlay = CreateImage("Sanctuary Confirm Popup", parent, new Color(0f, 0f, 0f, 0.68f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		sanctuaryConfirmPopup = ((Component)overlay).gameObject;
		Canvas canvas = sanctuaryConfirmPopup.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 921;
		sanctuaryConfirmPopup.AddComponent<GraphicRaycaster>();

		sanctuaryConfirmDialogImage = CreateImage("Sanctuary Confirm Dialog", ((Component)overlay).transform, new Color(0.012f, 0.018f, 0.032f, 0.98f));
		sanctuaryConfirmDialogImage.raycastTarget = true;
		StylePanel(sanctuaryConfirmDialogImage);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(sanctuaryConfirmDialogImage.rectTransform, "Sanctuary Confirm Crest", new Vector2(0.5f, 1f), new Vector2(42f, 42f), Color.white);
		SetRect(sanctuaryConfirmDialogImage.rectTransform, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f));

		sanctuaryConfirmTitleText = CreateText("Sanctuary Confirm Title", ((Component)sanctuaryConfirmDialogImage).transform, font, 50, FontStyle.Normal, TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(sanctuaryConfirmTitleText);
		sanctuaryConfirmTitleText.color = SanctuaryGold;
		SetRect(sanctuaryConfirmTitleText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f));

		sanctuaryConfirmBodyText = CreateText("Sanctuary Confirm Body", ((Component)sanctuaryConfirmDialogImage).transform, font, 40, FontStyle.Bold, TextAnchor.MiddleCenter);
		sanctuaryConfirmBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		sanctuaryConfirmBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		sanctuaryConfirmBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		sanctuaryConfirmBodyText.resizeTextForBestFit = true;
		sanctuaryConfirmBodyText.resizeTextMinSize = 18;
		sanctuaryConfirmBodyText.resizeTextMaxSize = 40;
		SetRect(sanctuaryConfirmBodyText.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.7f));
		StyleSanctuaryConfirmTypography();

		Button cancelButton = CreateButton(
			"Sanctuary Confirm Cancel",
			((Component)sanctuaryConfirmDialogImage).transform,
			font,
			GameText.Get(GameTextKeys.Common.Cancel));
		ApplySanctuaryCampaignCta(cancelButton, "UI/CampaignRestyle/campaign_cta_back_red");
		ScaleSanctuaryConfirmButton(cancelButton);
		((UnityEvent)cancelButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideSanctuaryConfirmPopup();
		});
		SetRect((RectTransform)((Component)cancelButton).transform, new Vector2(0.08f, 0.1f), new Vector2(0.44f, 0.27f));

		sanctuaryConfirmButton = CreateButton(
			"Sanctuary Confirm Accept",
			((Component)sanctuaryConfirmDialogImage).transform,
			font,
			GameText.Get(GameTextKeys.Sanctuary.OfferHoney));
		ApplySanctuaryCampaignCta(
			sanctuaryConfirmButton,
			"UI/CampaignRestyle/campaign_cta_orange");
		ScaleSanctuaryConfirmButton(sanctuaryConfirmButton);
		sanctuaryConfirmButtonText = ((Component)sanctuaryConfirmButton).GetComponentInChildren<Text>();
		((UnityEvent)sanctuaryConfirmButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ConfirmSanctuaryPurchase();
		});
		SetRect((RectTransform)((Component)sanctuaryConfirmButton).transform, new Vector2(0.56f, 0.1f), new Vector2(0.92f, 0.27f));

		sanctuaryConfirmPopup.SetActive(false);
	}

	private static void ApplySanctuaryCampaignCta(Button button, string spriteResource)
	{
		ApplyMerchantCampaignCta(button, spriteResource);
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = Color.white;
		colors.pressedColor = Color.white;
		colors.selectedColor = Color.white;
		colors.disabledColor = Color.white;
		colors.colorMultiplier = 1f;
		button.colors = colors;
	}

	private static void ScaleSanctuaryConfirmButton(Button button)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		((Component)button).transform.localScale = Vector3.one * 1.2f;
	}

	private void StyleSanctuaryConfirmTypography()
	{
		if ((Object)(object)sanctuaryConfirmTitleText != (Object)null)
		{
			Font titleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC")
				?? AccardND.Battlefield.MmoUiTheme.TitleFont;
			if ((Object)(object)titleFont != (Object)null)
			{
				sanctuaryConfirmTitleText.font = titleFont;
			}
			sanctuaryConfirmTitleText.fontSize = 50;
			sanctuaryConfirmTitleText.fontStyle = FontStyle.Normal;
			sanctuaryConfirmTitleText.resizeTextForBestFit = false;
		}
		if ((Object)(object)sanctuaryConfirmBodyText != (Object)null)
		{
			sanctuaryConfirmBodyText.fontSize = 40;
			sanctuaryConfirmBodyText.resizeTextForBestFit = false;
		}
	}

	private void StyleSanctuaryConfirmDialogForActiveAltar()
	{
		if ((Object)(object)sanctuaryConfirmDialogImage == (Object)null)
		{
			return;
		}
		// Classi, tecniche e reliquie condividono sempre il pannello MMO.
		// L'assegnazione esplicita evita di ereditare cover del Santuario dal prefab
		// o da una precedente apertura del popup.
		sanctuaryConfirmDialogImage.sprite = AccardND.Battlefield.MmoUiTheme.GetPanelSprite();
		sanctuaryConfirmDialogImage.type = Image.Type.Sliced;
		sanctuaryConfirmDialogImage.preserveAspect = false;
		sanctuaryConfirmDialogImage.color = new Color(1f, 1f, 1f, 0.98f);
	}

	private static string SanctuaryAltarLabel(SanctuaryAltar altar)
	{
		return altar switch
		{
			SanctuaryAltar.Classes => GameText.Get(GameTextKeys.Sanctuary.AltarClasses),
			SanctuaryAltar.Techniques => GameText.Get(GameTextKeys.Sanctuary.AltarTechniques),
			// Il tab nasce clonando quello delle Classi: non deve poter ereditare
			// una voce localizzata obsoleta dal template.
			_ => GameText.Get(GameTextKeys.Sanctuary.AltarRelics)
		};
	}

	private void CreateSanctuaryAltarButton(Transform parent, Font font, string label, SanctuaryAltar altar)
	{
		Button button = CreateButton("Sanctuary Altar " + altar, parent, font, label);
		Image background = ((Component)button).GetComponent<Image>();
		background.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ranked_cta_frame_v3");
		background.type = Image.Type.Simple;
		background.preserveAspect = false;
		Image icon = CreateImage("Icon", ((Component)button).transform, SanctuaryGold);
		icon.sprite = LoadSpriteResource(altar switch
		{
			SanctuaryAltar.Classes => "UI/Sanctuary/sanctuary_classes_emblem_aaa",
			SanctuaryAltar.Techniques => "UI/Sanctuary/sanctuary_techniques_emblem_aaa",
			_ => "UI/Sanctuary/sanctuary_relics_emblem_aaa"
		});
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		SetRect(icon.rectTransform, new Vector2(0.08f, 0.2f), new Vector2(0.29f, 0.8f));

		Text labelText = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)labelText != (Object)null)
		{
			Font altarFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
			if ((Object)(object)altarFont != (Object)null)
			{
				labelText.font = altarFont;
			}
			labelText.fontSize = 28;
			labelText.resizeTextForBestFit = true;
			labelText.resizeTextMinSize = 14;
			labelText.resizeTextMaxSize = 28;
			labelText.alignment = TextAnchor.MiddleCenter;
			SetRect(labelText.rectTransform, new Vector2(0.34f, 0.08f), new Vector2(0.9f, 0.92f));
		}
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			SelectSanctuaryAltar(altar);
		});
		sanctuaryAltarButtons.Add(button);
		sanctuaryAltarIcons.Add(icon);
		AttachSanctuaryAltarVfx(button);
	}

	private static void StyleSanctuaryAltarButton(Button button)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		Text label = ((Component)button).GetComponentInChildren<Text>();
		Font altarFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
		if ((Object)(object)label != (Object)null)
		{
			if ((Object)(object)altarFont != (Object)null)
			{
				label.font = altarFont;
			}
			label.fontSize = 28;
			label.resizeTextForBestFit = true;
			label.resizeTextMinSize = 14;
			label.resizeTextMaxSize = 28;
			label.alignment = TextAnchor.MiddleCenter;
			SetRect(label.rectTransform, new Vector2(0.34f, 0.08f), new Vector2(0.9f, 0.92f));
		}
		Transform icon = FindSanctuaryPrefabTransform(((Component)button).transform, "Icon");
		if ((Object)(object)icon != (Object)null)
		{
			SetRect((RectTransform)icon, new Vector2(0.12f, 0.14f), new Vector2(0.36f, 0.86f));
		}
	}

	private static void AttachSanctuaryAltarVfx(Button button)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}

		Color tint = new Color(0.62f, 0.18f, 1f);
		Transform existingTransform = FindSanctuaryPrefabTransform(
			((Component)button).transform,
			"Sanctuary Altar VFX");
		var effect = (Object)(object)existingTransform != (Object)null
			? ((Component)existingTransform).GetComponent<AccardND.PvpUi.PvpUiVfx>()
			: null;
		if ((Object)(object)effect == (Object)null)
		{
			effect = AccardND.PvpUi.PvpUiVfx.CreatePulseButton(
				(RectTransform)((Component)button).transform,
				tint);
			effect.name = "Sanctuary Altar VFX";
		}
		effect.SetTint(tint, 0.72f);
	}

	// --- Navigazione ---

	private void ShowSanctuary()
	{
		if ((Object)(object)sanctuaryPanel == (Object)null)
		{
			ShowUnderDevelopmentPopup();
			return;
		}

		if ((Object)(object)modeSelectionPanel != (Object)null)
		{
			modeSelectionPanel.SetActive(false);
		}
		SetAccountHubHudActive(true);
		EnsureSanctuarySharedHudSorting();
		sanctuaryActiveAltar = SanctuaryAltar.Techniques;
		sanctuaryNotice = null;
		HideSanctuaryConfirmPopup();
		sanctuaryPanel.SetActive(true);
		sanctuaryPanel.transform.SetAsLastSibling();
		RefreshAccountBannerView();
		RefreshSanctuaryLayout();
		RefreshSanctuaryList();
		LoadSanctuaryFromServer();
		TryStartPendingTutorialTour(AccardND.GameData.TutorialSurface.HubSanctuary);
	}

	private void HideSanctuary()
	{
		HideSanctuaryConfirmPopup();
		if ((Object)(object)sanctuaryPanel != (Object)null)
		{
			sanctuaryPanel.SetActive(false);
		}
		ShowHubFromSinglePlayer();
	}

	private void EnsureSanctuarySharedHudSorting()
	{
		if ((Object)(object)accountBannerImage != (Object)null)
		{
			Canvas headerCanvas = ((Component)accountBannerImage).GetComponent<Canvas>();
			if ((Object)(object)headerCanvas != (Object)null)
			{
				headerCanvas.overrideSorting = true;
				headerCanvas.sortingOrder = 910;
			}
		}

		if ((Object)(object)accountHoneyPanelImage != (Object)null)
		{
			GameObject honeyPanel = ((Component)accountHoneyPanelImage).gameObject;
			Canvas honeyCanvas = honeyPanel.GetComponent<Canvas>();
			if ((Object)(object)honeyCanvas == (Object)null)
			{
				honeyCanvas = honeyPanel.AddComponent<Canvas>();
			}
			honeyCanvas.overrideSorting = true;
			honeyCanvas.sortingOrder = 911;
			if ((Object)(object)honeyPanel.GetComponent<GraphicRaycaster>() == (Object)null)
			{
				honeyPanel.AddComponent<GraphicRaycaster>();
			}
		}
	}

	private void SelectSanctuaryAltar(SanctuaryAltar altar)
	{
		HideSanctuaryConfirmPopup();
		sanctuaryActiveAltar = altar;
		RefreshSanctuaryLayout();
		RefreshSanctuaryList();
		// Prima si costruiscono le carte del nuovo altare, poi il tour passa alla tappa
		// successiva: cosi' lo spotlight puo' trovare subito la carta Mago appena apparsa.
		NotifyGuidedTourTargetTapped();
		if ((Object)(object)sanctuaryScrollRect != (Object)null)
		{
			Canvas.ForceUpdateCanvases();
			sanctuaryScrollRect.verticalNormalizedPosition = 1f;
		}
	}

	/// <summary>
	/// Scarica il catalogo valutato dal server. Senza connessione la schermata resta
	/// visitabile ma vuota: mostrare costi da una cache locale darebbe numeri che il
	/// server potrebbe poi rifiutare.
	/// </summary>
	private async void LoadSanctuaryFromServer()
	{
		if (sanctuaryLoading)
		{
			return;
		}

		sanctuaryLoading = true;
		SetSanctuaryStatus(GameText.Get(GameTextKeys.Sanctuary.Loading));
		try
		{
			// Il link di progressione nasce all'apertura del menu campagna: chi arriva al
			// Santuario direttamente dall'hub non lo ha ancora, quindi va stabilito qui
			// invece di dichiarare subito il forfait.
			if (await EnsureServerProgressAsync())
			{
				sanctuaryData = await serverProgress.GetSanctuaryAsync();
				AppendLog(GameText.Format(GameTextKeys.Sanctuary.CatalogReceivedLog, sanctuaryData?.entries?.Length ?? 0));
			}
			else
			{
				sanctuaryData = null;
				sanctuaryNotice = AccardND.Network.AccountServerSession.IsReconnecting
					? GameText.Get(GameTextKeys.Sanctuary.Reconnecting)
					: GameText.Get(GameTextKeys.Sanctuary.Offline);
				AppendLog(GameText.Get(GameTextKeys.Sanctuary.NoConnectionLog));
			}
		}
		catch (Exception exception)
		{
			sanctuaryData = null;
			AppendLog(GameText.Format(GameTextKeys.Sanctuary.CatalogFailedLog, exception.Message));
			sanctuaryNotice = GameText.Format(GameTextKeys.Sanctuary.Unavailable, exception.Message);
		}
		finally
		{
			sanctuaryLoading = false;
		}
		RefreshSanctuaryList();
	}

	// --- Contenuto ---

	private void RefreshSanctuaryList()
	{
		ClearSanctuaryCards();
		if ((Object)(object)sanctuaryListRoot == (Object)null)
		{
			return;
		}

		RefreshSanctuaryAltarButtons();
		RefreshSanctuaryDiscoverySummary();
		RefreshAccountBannerView();
		if ((Object)(object)accountHoneyAmountText != (Object)null)
		{
			int honey = sanctuaryData != null ? sanctuaryData.honey : singlePlayerProgressService.Honey;
			accountHoneyAmountText.text = honey.ToString("n0");
		}

		if (sanctuaryLoading)
		{
			return;
		}

		if (sanctuaryData?.entries == null)
		{
			SetSanctuaryStatus(ConsumeSanctuaryNotice() ?? "Nessun catalogo ricevuto. Torna indietro e riprova.");
			return;
		}

		int shown = 0;
		foreach (SanctuaryEntryData entry in sanctuaryData.entries)
		{
			if (entry == null || !BelongsToActiveAltar(entry))
			{
				continue;
			}
			CreateSanctuaryCard(entry);
			shown++;
		}

		if (shown == 0)
		{
			SetSanctuaryStatus(GameText.Get(GameTextKeys.Sanctuary.EmptyAltar));
			return;
		}

		SetSanctuaryStatus(ConsumeSanctuaryNotice() ?? DefaultSanctuaryStatus());
		Canvas.ForceUpdateCanvases();
		if ((Object)(object)sanctuaryScrollRect != (Object)null)
		{
			sanctuaryScrollRect.verticalNormalizedPosition = 1f;
		}
	}

	private bool BelongsToActiveAltar(SanctuaryEntryData entry) => sanctuaryActiveAltar switch
	{
		SanctuaryAltar.Classes => entry.type == "class",
		SanctuaryAltar.Techniques => entry.type == "secondAbility",
		// I consumabili sono tutti nel negozio; qui restano gli ampliamenti permanenti.
		_ => entry.type == "slot"
	};

	private string DefaultSanctuaryStatus()
	{
		if (sanctuaryActiveAltar == SanctuaryAltar.Classes)
		{
			return GameText.Get(GameTextKeys.Sanctuary.ClassesStatus);
		}
		if (sanctuaryActiveAltar == SanctuaryAltar.Techniques)
		{
			return GameText.Get(GameTextKeys.Sanctuary.TechniquesStatus);
		}
		return GameText.Format(GameTextKeys.Sanctuary.RelicsStatus, sanctuaryData?.bagSlots ?? 0);
	}

	private string ConsumeSanctuaryNotice()
	{
		if (string.IsNullOrEmpty(sanctuaryNotice))
		{
			return null;
		}
		string notice = sanctuaryNotice;
		sanctuaryNotice = null;
		return notice;
	}

	private void CreateSanctuaryCard(SanctuaryEntryData entry)
	{
		GameObject card = new GameObject("Sanctuary " + entry.id, new Type[3]
		{
			typeof(RectTransform),
			typeof(Image),
			typeof(Button)
		});
		card.transform.SetParent((Transform)(object)sanctuaryListRoot, false);
		Image hitTarget = card.GetComponent<Image>();
		hitTarget.color = new Color(1f, 1f, 1f, 0.001f);
		hitTarget.raycastTarget = true;
		Button button = card.GetComponent<Button>();
		button.targetGraphic = hitTarget;
		// Cliccabile solo quando l'offerta ha senso: le prove mancanti sono gia' scritte
		// sulla carta, quindi un click che apre un popup per dire "non puoi" sarebbe rumore.
		button.interactable = CanOfferSanctuaryEntry(entry);
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ShowSanctuaryConfirmPopup(entry);
		});

		Image cover = CreateImage("Cover", card.transform, Color.white);
		cover.raycastTarget = false;
		bool relicCard = entry.type != "class" && entry.type != "secondAbility";
		cover.sprite = relicCard
			? AccardND.Battlefield.MmoUiTheme.GetPanelSprite()
			: LoadSpriteResource("UI/Sanctuary/santuary_items");
		cover.type = relicCard ? Image.Type.Sliced : Image.Type.Simple;
		cover.preserveAspect = false;
		cover.color = entry.owned || entry.available
			? Color.white
			: new Color(0.56f, 0.56f, 0.6f, 0.98f);
		HeroClass heroClass = HeroClass.Mage;
		bool classIconCard = entry.type == "class" && TryGetSanctuaryHeroClass(entry, out heroClass);
		bool techniqueCard = entry.type == "secondAbility";
		SetRect(cover.rectTransform, Vector2.zero, Vector2.one);

		// Ogni voce usa lo stesso contenitore verticale: nove cornici per altare, in
		// griglia 3x3. Tutti i contenuti vivono sopra la texture fornita per la carta.
		Transform contentRoot = cover.transform;
		Image veil = CreateImage("Veil", contentRoot,
			entry.owned || entry.available ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.42f));
		veil.raycastTarget = false;
		Stretch(veil.rectTransform);

		if (classIconCard)
		{
			CreateSanctuaryClassIconCard(entry, contentRoot, heroClass);
			sanctuaryCards.Add(card);
			return;
		}

		if (techniqueCard)
		{
			CreateSanctuaryTechniqueRow(entry, cover.transform);
		}
		else
		{
			CreateSanctuaryRelicCard(entry, cover.transform);
		}

		sanctuaryCards.Add(card);
	}

	private void CreateSanctuaryTechniqueRow(SanctuaryEntryData entry, Transform parent)
	{
		Font sanctuaryNameFont = Resources.Load<Font>("Fonts/IMFellEnglishSC")
			?? AccardND.Battlefield.MmoUiTheme.TitleBoldFont;
		Font sanctuaryStatusFont = Resources.Load<Font>("Fonts/Alegreya")
			?? AccardND.Battlefield.MmoUiTheme.BodyFont;
		Text nameText = CreateText(
			"Name",
			parent,
			sanctuaryNameFont,
			26,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		nameText.text = (entry.name ?? entry.id).ToUpperInvariant();
		nameText.color = SanctuaryGold;
		nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
		nameText.verticalOverflow = VerticalWrapMode.Truncate;
		nameText.resizeTextForBestFit = true;
		nameText.resizeTextMinSize = 9;
		nameText.resizeTextMaxSize = 26;
		AddSanctuaryTextOutline(nameText);
		nameText.rectTransform.anchorMin = new Vector2(0.06f, 0.79f);
		nameText.rectTransform.anchorMax = new Vector2(0.94f, 0.96f);
		nameText.rectTransform.offsetMin = new Vector2(0f, -21f);
		nameText.rectTransform.offsetMax = new Vector2(0f, -21f);

		Image icon = CreateImage("Icon", parent, Color.white);
		icon.sprite = GetSanctuaryEntrySprite(entry);
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		icon.rectTransform.anchorMin = new Vector2(0.14f, 0.29f);
		icon.rectTransform.anchorMax = new Vector2(0.86f, 0.79f);
		icon.rectTransform.offsetMin = new Vector2(0f, -19f);
		icon.rectTransform.offsetMax = new Vector2(0f, -19f);
		((Component)icon).gameObject.SetActive((Object)(object)icon.sprite != (Object)null);

		GameObject costPlateObject = new GameObject("Cost Plate", typeof(RectTransform));
		costPlateObject.transform.SetParent(parent, false);
		RectTransform costPlate = costPlateObject.GetComponent<RectTransform>();
		SetRect(costPlate, new Vector2(0.05f, 0.055f), new Vector2(0.95f, 0.27f));
		Text statusText = CreateText(
			"Status",
			costPlate.transform,
			sanctuaryStatusFont,
			28,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		statusText.raycastTarget = false;
		statusText.text = SanctuaryCardStatus(entry);
		statusText.color = SanctuaryCardStatusColor(entry);
		statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
		statusText.verticalOverflow = VerticalWrapMode.Truncate;
		statusText.resizeTextForBestFit = true;
		statusText.resizeTextMinSize = 10;
		statusText.resizeTextMaxSize = 28;
		AddSanctuaryTextOutline(statusText);
		SetRect(statusText.rectTransform, Vector2.zero, Vector2.one);
	}

	private void CreateSanctuaryRelicCard(SanctuaryEntryData entry, Transform parent)
	{
		Font sanctuaryNameFont = Resources.Load<Font>("Fonts/IMFellEnglishSC")
			?? AccardND.Battlefield.MmoUiTheme.TitleBoldFont;
		Font sanctuaryStatusFont = Resources.Load<Font>("Fonts/Alegreya")
			?? AccardND.Battlefield.MmoUiTheme.BodyFont;
		Text nameText = CreateText(
			"Name",
			parent,
			sanctuaryNameFont,
			26,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		nameText.text = (entry.name ?? entry.id).ToUpperInvariant();
		nameText.color = SanctuaryGold;
		nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
		nameText.verticalOverflow = VerticalWrapMode.Truncate;
		AddSanctuaryTextOutline(nameText);
		nameText.rectTransform.anchorMin = new Vector2(0.06f, 0.79f);
		nameText.rectTransform.anchorMax = new Vector2(0.94f, 0.96f);
		nameText.rectTransform.offsetMin = new Vector2(0f, -17f);
		nameText.rectTransform.offsetMax = new Vector2(0f, -17f);

		Image icon = CreateImage("Icon", parent, Color.white);
		icon.sprite = GetSanctuaryEntrySprite(entry);
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		SetRect(icon.rectTransform, new Vector2(0.18f, 0.43f), new Vector2(0.82f, 0.8f));
		((Component)icon).gameObject.SetActive((Object)(object)icon.sprite != (Object)null);

		Text descriptionText = CreateText(
			"Description",
			parent,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			30,
			FontStyle.Normal,
			TextAnchor.UpperCenter);
		descriptionText.text = entry.description ?? string.Empty;
		descriptionText.color = new Color(0.86f, 0.82f, 0.72f);
		descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
		descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
		descriptionText.resizeTextForBestFit = true;
		descriptionText.resizeTextMinSize = 12;
		descriptionText.resizeTextMaxSize = 30;
		SetRect(descriptionText.rectTransform, new Vector2(0.07f, 0.22f), new Vector2(0.93f, 0.44f));

		Text requirementText = CreateText(
			"Requirements",
			parent,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			12,
			FontStyle.Bold,
			TextAnchor.MiddleCenter);
		requirementText.text = SanctuaryRequirementSummary(entry);
		requirementText.color = entry.requirementsMet ? SanctuaryOwned : SanctuaryDim;
		requirementText.horizontalOverflow = HorizontalWrapMode.Wrap;
		requirementText.verticalOverflow = VerticalWrapMode.Truncate;
		SetRect(requirementText.rectTransform, new Vector2(0.07f, 0.1f), new Vector2(0.93f, 0.24f));

		GameObject costPlateObject = new GameObject("Cost Plate", typeof(RectTransform));
		costPlateObject.transform.SetParent(parent, false);
		RectTransform costPlate = costPlateObject.GetComponent<RectTransform>();
		SetRect(costPlate, new Vector2(0.05f, 0.055f), new Vector2(0.95f, 0.27f));

		Text statusText = CreateText(
			"Status",
			costPlate.transform,
			sanctuaryStatusFont,
			28,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		statusText.raycastTarget = false;
		statusText.text = SanctuaryCardStatus(entry);
		statusText.color = SanctuaryCardStatusColor(entry);
		statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
		statusText.verticalOverflow = VerticalWrapMode.Truncate;
		statusText.resizeTextForBestFit = true;
		statusText.resizeTextMinSize = 10;
		statusText.resizeTextMaxSize = 28;
		AddSanctuaryTextOutline(statusText);
		SetRect(statusText.rectTransform, Vector2.zero, Vector2.one);
	}

	private void CreateSanctuaryClassIconCard(SanctuaryEntryData entry, Transform parent, HeroClass heroClass)
	{
		Image icon = CreateImage("Icon", parent, Color.white);
		icon.sprite = GetClassIconSprite(heroClass, grayscale: !entry.owned && !entry.available);
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		SetRect(icon.rectTransform, new Vector2(0.1f, 0.39f), new Vector2(0.9f, 0.91f));

		Font classNameFont = Resources.Load<Font>("Fonts/IMFellEnglishSC")
			?? AccardND.Battlefield.MmoUiTheme.TitleBoldFont;
		Text label = CreateText("Name", parent, classNameFont, 30, FontStyle.Normal, TextAnchor.MiddleCenter);
		label.raycastTarget = false;
		label.text = HeroClassDisplayName(heroClass).ToUpperInvariant();
		label.color = SanctuaryGold;
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
		label.resizeTextForBestFit = true;
		label.resizeTextMinSize = 8;
		label.resizeTextMaxSize = 30;
		AddSanctuaryTextOutline(label);
		label.rectTransform.anchorMin = new Vector2(0.04f, 0.27f);
		label.rectTransform.anchorMax = new Vector2(0.96f, 0.4f);
		label.rectTransform.offsetMin = new Vector2(0f, -21.6f);
		label.rectTransform.offsetMax = new Vector2(0f, -21.6f);

		Font sanctuaryStatusFont = Resources.Load<Font>("Fonts/Alegreya")
			?? AccardND.Battlefield.MmoUiTheme.BodyFont;
		Text statusText = CreateText("Status", parent, sanctuaryStatusFont, 28, FontStyle.Normal, TextAnchor.MiddleCenter);
		statusText.raycastTarget = false;
		string requirement = SanctuaryRequirementSummary(entry);
		statusText.text = string.IsNullOrWhiteSpace(requirement)
			? SanctuaryCardStatus(entry)
			: $"{SanctuaryCardStatus(entry)}\n{requirement}";
		statusText.color = SanctuaryCardStatusColor(entry);
		statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
		statusText.verticalOverflow = VerticalWrapMode.Truncate;
		statusText.resizeTextForBestFit = true;
		statusText.resizeTextMinSize = 10;
		statusText.resizeTextMaxSize = 28;
		AddSanctuaryTextOutline(statusText);
		SetRect(statusText.rectTransform, new Vector2(0.05f, 0.055f), new Vector2(0.95f, 0.27f));
	}

	private Sprite GetSanctuaryEntrySprite(SanctuaryEntryData entry)
	{
		string value = $"{entry?.id} {entry?.name}".ToLowerInvariant();
		string resourcePath;
		if (entry?.id == "merchant-upgrade-relic-1")
			resourcePath = "UI/merchant_upgrade_relic_1";
		else if (entry?.id == "merchant-upgrade-relic-2")
			resourcePath = "UI/merchant_upgrade_relic_2";
		else if (value.Contains("detector"))
			resourcePath = "UI/detector_item";
		else if (value.Contains("double_exp") || value.Contains("doppia exp") || value.Contains("double exp"))
			resourcePath = "UI/double_exp_item";
		else if (value.Contains("empower"))
			resourcePath = "UI/empower_item";
		else if (value.Contains("second_chance") || value.Contains("seconda chance") || value.Contains("second chance"))
			resourcePath = "UI/second_chance_item";
		else if (value.Contains("ruby") || value.Contains("rubino"))
			resourcePath = "UI/ruby_seal_item";
		else if (value.Contains("mana-5") || value.Contains("mana +5"))
			resourcePath = "UI/mana_gain_5_item";
		else if (value.Contains("mana-10") || value.Contains("mana +10"))
			resourcePath = "UI/mana_gain_10_item";
		else if (value.Contains("jolly"))
			resourcePath = "UI/jolly_item";
		else if (value.Contains("blackheart") || value.Contains("blackhearth"))
			resourcePath = "UI/blackhearth_icon";
		else if (entry?.type == "slot")
			resourcePath = "UI/bag_button";
		else if (entry?.type == "secondAbility")
			resourcePath = "UI/ability_secondary_button";
		else
			resourcePath = "UI/attachment_button";
		return LoadSpriteResource(resourcePath);
	}

	// --- Acquisto ---

	/// <summary>
	/// Vero quando l'offerta e' proponibile: voce acquistabile, prove superate, non gia'
	/// posseduta. Il miele insufficiente non blocca il click: la conferma lo dice, cosi'
	/// il giocatore vede quanto gli manca invece di trovarsi una carta morta.
	/// </summary>
	private bool CanOfferSanctuaryEntry(SanctuaryEntryData entry) =>
		entry != null
		&& entry.available
		&& entry.requirementsMet
		&& !entry.owned
		&& IsTutorialSanctuaryPurchaseAllowed(entry);

	private bool IsTutorialSanctuaryPurchaseAllowed(SanctuaryEntryData entry)
	{
		if (!IsTutorialOnboardingActive())
			return true;

		string requiredClassId = TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow());
		return entry != null
			&& !string.IsNullOrEmpty(requiredClassId)
			&& entry.type == "class"
			&& string.Equals(entry.id, requiredClassId, StringComparison.OrdinalIgnoreCase);
	}

	private int SanctuaryHoney() =>
		sanctuaryData != null ? sanctuaryData.honey : singlePlayerProgressService.Honey;

	private void ShowSanctuaryConfirmPopup(SanctuaryEntryData entry)
	{
		if (!CanOfferSanctuaryEntry(entry) || (Object)(object)sanctuaryConfirmPopup == (Object)null)
		{
			return;
		}

		sanctuaryPendingEntry = entry;
		int honey = SanctuaryHoney();
		bool affordable = honey >= entry.honeyCost;
		StyleSanctuaryConfirmDialogForActiveAltar();

		if ((Object)(object)sanctuaryConfirmTitleText != (Object)null)
		{
			sanctuaryConfirmTitleText.text = (entry.name ?? entry.id).ToUpperInvariant();
		}
		if ((Object)(object)sanctuaryConfirmBodyText != (Object)null)
		{
			string offer = entry.type switch
			{
				"item" => GameText.Format(GameTextKeys.Sanctuary.OfferItem, entry.name, entry.honeyCost),
				"slot" => GameText.Format(GameTextKeys.Sanctuary.OfferSlot, entry.honeyCost),
				// La tecnica si apprende una volta e resta: vale per ogni carta di quella classe.
				"secondAbility" => GameText.Format(GameTextKeys.Sanctuary.OfferTechnique, entry.honeyCost),
				_ => GameText.Format(GameTextKeys.Sanctuary.OfferClass, entry.honeyCost)
			};
			string purchaseBody = affordable
				? GameText.Format(GameTextKeys.Sanctuary.HoneyAvailable, offer, honey)
				: GameText.Format(GameTextKeys.Sanctuary.HoneyInsufficientBody, entry.honeyCost, honey);
			bool tutorialMagePurchase = entry.type == "class"
				&& string.Equals(entry.id, "mage", StringComparison.OrdinalIgnoreCase)
				&& string.Equals(
					TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow()),
					"mage",
					StringComparison.OrdinalIgnoreCase);
			bool tutorialRoguePurchase = entry.type == "class"
				&& string.Equals(entry.id, "rogue", StringComparison.OrdinalIgnoreCase)
				&& string.Equals(
					TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow()),
					"rogue",
					StringComparison.OrdinalIgnoreCase);
			sanctuaryConfirmBodyText.text = tutorialRoguePurchase
				? $"Hai superato il tutorial mago. Offri {entry.honeyCost} vasetti di miele al santuario per sbloccare questa classe.\n\nMiele disponibile: {honey}."
				: tutorialMagePurchase
				? $"Hai superato il tutorial guerriero. Offri {entry.honeyCost} vasetti di miele al santuario per sbloccare questa classe.\n\nMiele disponibile: {honey}."
				: entry.type == "secondAbility" && !string.IsNullOrWhiteSpace(entry.description)
					? $"{entry.description.Trim()}\n\n{purchaseBody}"
					: purchaseBody;
		}
		if ((Object)(object)sanctuaryConfirmButton != (Object)null)
		{
			if (affordable)
			{
				ApplySanctuaryCampaignCta(
					sanctuaryConfirmButton,
					"UI/CampaignRestyle/campaign_cta_orange");
			}
			else
			{
				ApplySanctuaryCampaignCta(
					sanctuaryConfirmButton,
					"UI/CampaignRestyle/campaign_cta_orange");
			}
			sanctuaryConfirmButton.interactable = affordable && !sanctuaryPurchasing;
		}
		if ((Object)(object)sanctuaryConfirmButtonText != (Object)null)
		{
			sanctuaryConfirmButtonText.text = affordable
				? GameText.Get(GameTextKeys.Sanctuary.OfferHoney)
				: GameText.Get(GameTextKeys.Sanctuary.HoneyInsufficient);
		}

		sanctuaryConfirmPopup.SetActive(true);
		sanctuaryConfirmPopup.transform.SetAsLastSibling();
		string guidedClassId = TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow());
		if (IsGuidedTourActive
			&& string.Equals(entry.id, guidedClassId, StringComparison.OrdinalIgnoreCase)
			&& (Object)(object)sanctuaryConfirmButton != (Object)null)
		{
			MoveAdventureTutorialSpotlight(
				(RectTransform)((Component)sanctuaryConfirmButton).transform);
		}
	}

	private void HideSanctuaryConfirmPopup()
	{
		string closedEntryId = sanctuaryPendingEntry?.id;
		if ((Object)(object)sanctuaryConfirmPopup != (Object)null)
		{
			sanctuaryConfirmPopup.SetActive(false);
		}
		sanctuaryPendingEntry = null;
		string guidedClassId = TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow());
		if (IsGuidedTourActive
			&& !string.IsNullOrEmpty(closedEntryId)
			&& string.Equals(closedEntryId, guidedClassId, StringComparison.OrdinalIgnoreCase))
		{
			MoveAdventureTutorialSpotlight(SanctuaryEntryRect(closedEntryId));
		}
	}

	/// <summary>
	/// Chiede l'acquisto al server. Costo e prove li ricontrolla il server: qui si mostra
	/// solo l'esito. Al successo si ricarica il catalogo invece di aggiustare la carta a
	/// mano, cosi' lo stato mostrato resta quello autoritativo.
	/// </summary>
	private async void ConfirmSanctuaryPurchase()
	{
		SanctuaryEntryData entry = sanctuaryPendingEntry;
		if (entry == null || sanctuaryPurchasing)
		{
			return;
		}
		if (!IsTutorialSanctuaryPurchaseAllowed(entry))
		{
			string requiredClassId = TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow());
			string requiredName = requiredClassId == "rogue" ? "il Ladro" : "il Mago";
			SetSanctuaryStatus(requiredClassId == null
				? "Completa il prossimo passo del tutorial prima di fare altri acquisti."
				: $"Durante il tutorial puoi acquistare soltanto {requiredName}.");
			HideSanctuaryConfirmPopup();
			return;
		}

		if (!ServerProgressReady)
		{
			SetSanctuaryStatus(GameText.Get(GameTextKeys.Sanctuary.ConnectionRequired));
			HideSanctuaryConfirmPopup();
			return;
		}

		sanctuaryPurchasing = true;
		if ((Object)(object)sanctuaryConfirmButton != (Object)null)
		{
			sanctuaryConfirmButton.interactable = false;
		}

		try
		{
			await serverProgress.PurchaseUnlockAsync(SanctuaryUnlockTypeOf(entry), entry.id);
			bool purchasedTutorialMage = SanctuaryUnlockTypeOf(entry) == SinglePlayerUnlockType.Class
				&& string.Equals(entry.id, "mage", StringComparison.OrdinalIgnoreCase);
			bool purchasedTutorialRogue = SanctuaryUnlockTypeOf(entry) == SinglePlayerUnlockType.Class
				&& string.Equals(entry.id, "rogue", StringComparison.OrdinalIgnoreCase);
			sanctuaryNotice = GameText.Format(GameTextKeys.Sanctuary.EntryOwned, entry.name);
			MirrorServerProgress();
			RefreshSinglePlayerProgressView();
			AppendLog(GameText.Format(GameTextKeys.Sanctuary.PurchasedLog, entry.id, entry.honeyCost));
			HideSanctuaryConfirmPopup();
			// Se un tour d'acquisto stava aspettando proprio questa classe, prosegue da qui:
			// e' l'acquisto vero a sbloccare la tappa, non una finta conferma del tutorial.
			if (SanctuaryUnlockTypeOf(entry) == AccardND.GameData.SinglePlayerUnlockType.Class)
			{
				NotifyTutorialClassPurchased(entry.id);
			}
			if (purchasedTutorialMage || purchasedTutorialRogue)
			{
				// La navigazione non deve dipendere dal fatto che il tour d'acquisto fosse
				// ancora vivo (puo' non esserlo dopo una riconnessione). L'acquisto server
				// riuscito e' da solo la condizione definitiva per chiudere il Santuario.
				if ((Object)(object)sanctuaryPanel != (Object)null)
					sanctuaryPanel.SetActive(false);
				if (purchasedTutorialRogue)
					StartRogueTutorialNavigationTour();
				else
					StartMageTutorialNavigationTour();
			}
		}
		catch (Exception exception)
		{
			AppendLog(GameText.Format(GameTextKeys.Sanctuary.PurchaseRejectedLog, entry.id, exception.Message));
			sanctuaryNotice = exception.Message;
			HideSanctuaryConfirmPopup();
		}
		finally
		{
			sanctuaryPurchasing = false;
		}

		// Il catalogo cambia comunque, anche in caso di rifiuto: il server potrebbe avere
		// uno stato piu' recente di quello che il client aveva in mano.
		LoadSanctuaryFromServer();
	}

	private static AccardND.GameData.SinglePlayerUnlockType SanctuaryUnlockTypeOf(SanctuaryEntryData entry) => entry.type switch
	{
		"secondAbility" => AccardND.GameData.SinglePlayerUnlockType.SecondAbility,
		"slot" => AccardND.GameData.SinglePlayerUnlockType.Slot,
		_ => AccardND.GameData.SinglePlayerUnlockType.Class
	};

	private static void AddSanctuaryTextOutline(Text text)
	{
		Outline outline = ((Component)text).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
	}

	private static string SanctuaryCardStatus(SanctuaryEntryData entry)
	{
		if (entry.owned)
		{
			return GameText.Get(GameTextKeys.Sanctuary.CardOwned);
		}
		if (!entry.available)
		{
			// Le starter hanno costo zero e si prendono col tutorial; le tecniche ancora
			// senza effetto mostrano il prezzo perche' il giocatore sappia cosa lo aspetta.
			return entry.honeyCost > 0
				? GameText.Format(GameTextKeys.Sanctuary.CardComingSoon, entry.honeyCost)
				: GameText.Get(GameTextKeys.Sanctuary.CardFromTutorial);
		}
		return GameText.Format(GameTextKeys.Sanctuary.CardHoneyCost, entry.honeyCost);
	}

	private static Color SanctuaryCardStatusColor(SanctuaryEntryData entry)
	{
		if (entry.owned)
		{
			return SanctuaryOwned;
		}
		return entry.available && entry.requirementsMet ? SanctuaryGold : SanctuaryDim;
	}

	private static bool TryGetSanctuaryHeroClass(SanctuaryEntryData entry, out HeroClass heroClass)
	{
		if (entry != null)
		{
			if (TryParseSanctuaryHeroClass(entry.id, out heroClass))
			{
				return true;
			}
			if (TryParseSanctuaryHeroClass(entry.name, out heroClass))
			{
				return true;
			}
		}

		heroClass = HeroClass.Mage;
		return false;
	}

	private static bool TryParseSanctuaryHeroClass(string value, out HeroClass heroClass)
	{
		string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
		normalized = normalized.Replace("class-", string.Empty).Replace("class_", string.Empty).Replace("class.", string.Empty);
		normalized = normalized.Replace("classe-", string.Empty).Replace("classe_", string.Empty).Replace("classe.", string.Empty);
		switch (normalized)
		{
			case "assassin":
			case "assassino":
				heroClass = HeroClass.Assassin;
				return true;
			case "warrior":
			case "guerriero":
				heroClass = HeroClass.Warrior;
				return true;
			case "mage":
			case "mago":
				heroClass = HeroClass.Mage;
				return true;
			case "paladin":
			case "paladino":
				heroClass = HeroClass.Paladin;
				return true;
			case "rogue":
			case "ladro":
				heroClass = HeroClass.Rogue;
				return true;
			case "hunter":
			case "cacciatore":
				heroClass = HeroClass.Hunter;
				return true;
			case "barbarian":
			case "barbaro":
				heroClass = HeroClass.Barbarian;
				return true;
			case "necromancer":
			case "necromante":
				heroClass = HeroClass.Necromancer;
				return true;
			case "priest":
			case "sacerdote":
				heroClass = HeroClass.Priest;
				return true;
			default:
				heroClass = HeroClass.Mage;
				return false;
		}
	}

	/// <summary>
	/// Riassunto delle prove con il progresso. Le prove restano visibili anche quando sono
	/// lontane: e' il modo piu' diretto di dire al giocatore che c'e' altro da fare.
	/// </summary>
	private static string SanctuaryRequirementSummary(SanctuaryEntryData entry)
	{
		if (entry.owned)
		{
			return string.Empty;
		}
		if (entry.requirements == null || entry.requirements.Length == 0)
		{
			// La descrizione ha uno spazio dedicato nelle carte di tecniche e reliquie:
			// qui restano soltanto le prove valutate dal server, senza duplicare il testo.
			return string.Empty;
		}

		var lines = new List<string>(entry.requirements.Length);
		foreach (SanctuaryRequirementData requirement in entry.requirements)
		{
			if (requirement == null)
			{
				continue;
			}
			string mark = requirement.met ? "OK" : $"{requirement.current}/{requirement.threshold}";
			lines.Add($"{requirement.description} ({mark})");
		}
		return string.Join("\n", lines);
	}

	private void RefreshSanctuaryAltarButtons()
	{
		for (int index = 0; index < sanctuaryAltarButtons.Count; index++)
		{
			Button button = sanctuaryAltarButtons[index];
			if ((Object)(object)button == (Object)null)
			{
				continue;
			}
			bool active = index == (int)sanctuaryActiveAltar;
			button.interactable = !active;
			Image background = button.targetGraphic as Image;
			if ((Object)(object)background != (Object)null)
			{
				background.sprite = LoadSpriteResource("UI/Sanctuary/sanctuary_tab_frame_v2");
				background.type = Image.Type.Simple;
				background.preserveAspect = false;
			}
			ColorBlock colors = button.colors;
			colors.normalColor = active ? Color.white : new Color(0.72f, 0.72f, 0.74f);
			colors.highlightedColor = Color.white;
			colors.pressedColor = new Color(0.78f, 0.7f, 0.9f);
			colors.disabledColor = active ? Color.white : new Color(0.72f, 0.72f, 0.74f);
			button.colors = colors;

			Text label = ((Component)button).GetComponentInChildren<Text>();
			if ((Object)(object)label != (Object)null)
			{
				label.color = active ? Color.white : SanctuaryGold;
			}
			if (index < sanctuaryAltarIcons.Count && (Object)(object)sanctuaryAltarIcons[index] != (Object)null)
			{
				sanctuaryAltarIcons[index].color = active ? new Color(0.86f, 0.72f, 1f) : SanctuaryGold;
			}
		}
		RefreshSanctuaryTutorialGates();
	}

	private void RefreshSanctuaryDiscoverySummary()
	{
		int discovered = 0;
		int total = 0;
		if (sanctuaryData?.entries != null)
		{
			foreach (SanctuaryEntryData entry in sanctuaryData.entries)
			{
				if (entry == null || !BelongsToActiveAltar(entry))
				{
					continue;
				}

				total++;
				if (entry.owned)
				{
					discovered++;
				}
			}
		}

		float progress = total > 0 ? (float)discovered / total : 0f;
		if ((Object)(object)sanctuaryDiscoveryTitleText != (Object)null)
		{
			sanctuaryDiscoveryTitleText.text = sanctuaryActiveAltar switch
			{
				SanctuaryAltar.Classes => GameText.Get(GameTextKeys.Sanctuary.DiscoveryClasses),
				SanctuaryAltar.Techniques => GameText.Get(GameTextKeys.Sanctuary.DiscoveryTechniques),
				_ => GameText.Get(GameTextKeys.Sanctuary.DiscoveryRelics)
			};
		}
		if ((Object)(object)sanctuaryDiscoveryCountText != (Object)null)
		{
			sanctuaryDiscoveryCountText.text = $"{discovered} / {total}";
		}
		if ((Object)(object)sanctuaryDiscoveryPercentText != (Object)null)
		{
			sanctuaryDiscoveryPercentText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
		}
		if ((Object)(object)sanctuaryDiscoveryProgressFillImage != (Object)null)
		{
			RectTransform fillRect = sanctuaryDiscoveryProgressFillImage.rectTransform;
			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = new Vector2(Mathf.Max(0.01f, progress), 1f);
			fillRect.offsetMin = Vector2.zero;
			fillRect.offsetMax = Vector2.zero;
		}
		if ((Object)(object)sanctuaryDiscoveryIconImage != (Object)null)
		{
			sanctuaryDiscoveryIconImage.sprite = LoadSpriteResource(sanctuaryActiveAltar switch
			{
				SanctuaryAltar.Classes => "UI/Sanctuary/sanctuary_classes_emblem_aaa",
				SanctuaryAltar.Techniques => "UI/Sanctuary/sanctuary_techniques_emblem_aaa",
				_ => "UI/Sanctuary/sanctuary_relics_emblem_aaa"
			});
		}
	}

	private void SetSanctuaryStatus(string message)
	{
		if ((Object)(object)sanctuaryStatusText != (Object)null)
		{
			sanctuaryStatusText.text = message ?? string.Empty;
		}
	}

	private void ClearSanctuaryCards()
	{
		for (int index = sanctuaryCards.Count - 1; index >= 0; index--)
		{
			if ((Object)(object)sanctuaryCards[index] != (Object)null)
			{
				Object.Destroy((Object)(object)sanctuaryCards[index]);
			}
		}
		sanctuaryCards.Clear();
	}

	// --- Layout ---

	private void RefreshSanctuaryLayout()
	{
		if ((Object)(object)sanctuaryPanel == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
		{
			return;
		}
		Rect safeRect = safeAreaRoot.rect;
		float width = Mathf.Max(1f, safeRect.width);
		float height = Mathf.Max(1f, safeRect.height);
		bool compact = IsCompactLayout(width / height, configuration.ResponsiveLayout);
		bool landscape = width > height;
		RefreshAccountBannerLayout(landscape);
		RefreshAccountHoneyIndicatorLayout(landscape);
		EnsureSanctuarySharedHudSorting();

		// Nel prefab gli anchor e le dimensioni della stanza sono authored nell'Editor:
		// il codice aggiorna soltanto la griglia in base allo spazio realmente disponibile.
		if (sanctuaryUsesPrefabLayout)
		{
			LayoutSanctuaryAltarButtons(0.06f, 0.94f, 0.012f, 0.685f, 0.775f, -62f);
			ConfigureSanctuaryGrid(compact);
			return;
		}

		float left = compact ? 0.038f : 0.075f;
		float right = compact ? 0.962f : 0.925f;
		SetRect(
			sanctuaryTitlePanel.rectTransform,
			new Vector2(0.08f, 0.785f),
			new Vector2(0.92f, 0.9f));
		SetRect(
			sanctuaryScreenOuterFrameImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.78f : 0.77f));

		sanctuaryHeadingText.fontSize = 48;
		sanctuaryHeadingText.resizeTextMaxSize = sanctuaryHeadingText.fontSize;

		float altarTop = compact ? 0.785f : 0.775f;
		float altarBottom = compact ? 0.695f : 0.685f;
		LayoutSanctuaryAltarButtons(left, right, 0.012f, altarBottom, altarTop, 0f);

		SetRect(
			sanctuaryListViewportImage.rectTransform,
			new Vector2(left, compact ? 0.02f : 0.025f),
			new Vector2(right, compact ? 0.685f : 0.675f));
		ConfigureSanctuaryGrid(compact);

	}

	private void LayoutSanctuaryAltarButtons(
		float left, float right, float gap, float bottom, float top, float verticalOffset)
	{
		int columns = Mathf.Max(1, sanctuaryAltarButtons.Count);
		float buttonWidth = (right - left - gap * (columns - 1)) / columns;
		for (int index = 0; index < sanctuaryAltarButtons.Count; index++)
		{
			Button button = sanctuaryAltarButtons[index];
			if ((Object)(object)button == (Object)null)
			{
				continue;
			}
			float start = left + (buttonWidth + gap) * index;
			RectTransform buttonRect = (RectTransform)((Component)button).transform;
			buttonRect.anchorMin = new Vector2(start, bottom);
			buttonRect.anchorMax = new Vector2(start + buttonWidth, top);
			buttonRect.offsetMin = new Vector2(0f, verticalOffset);
			buttonRect.offsetMax = new Vector2(0f, verticalOffset);
		}
	}

	private void ConfigureSanctuaryGrid(bool compact)
	{
		if ((Object)(object)sanctuaryListRoot == (Object)null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		GridLayoutGroup grid = ((Component)sanctuaryListRoot).GetComponent<GridLayoutGroup>();
		if ((Object)(object)grid == (Object)null || (Object)(object)sanctuaryListViewportImage == (Object)null)
		{
			return;
		}
		Rect rect = sanctuaryListViewportImage.rectTransform.rect;
		int columns = 3;
		float spacing = compact ? 8f : 12f;
		int padding = compact ? 8 : 12;
		float usableWidth = Mathf.Max(
			1f,
			rect.width - padding * 2f - spacing * (columns - 1));
		float cellWidth = usableWidth / columns;
		float cellHeight = sanctuaryActiveAltar switch
		{
			SanctuaryAltar.Classes => cellWidth * 1.31f,
			SanctuaryAltar.Techniques => cellWidth * 1.31f,
			_ => cellWidth * 1.31f
		};
		grid.constraintCount = columns;
		grid.spacing = new Vector2(spacing, spacing);
		grid.cellSize = new Vector2(cellWidth, cellHeight);
		grid.padding = new RectOffset(padding, padding, padding, padding);
	}
}
}
