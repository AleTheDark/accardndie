using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private enum LibrarySection
	{
		Cards,
		ClassesAndAuras,
		Bestiary,
		Atlas,
		Manual
	}

	private readonly struct LibraryProgress
	{
		public int Current { get; }
		public int Total { get; }

		public LibraryProgress(int current, int total)
		{
			Total = Mathf.Max(1, total);
			Current = Mathf.Clamp(current, 0, Total);
		}
	}

	private static readonly Color LibraryGold = new(0.88f, 0.62f, 0.24f, 1f);
	private static readonly Color LibraryPurple = new(0.76f, 0.34f, 0.94f, 1f);
	private static readonly Color LibraryGreen = new(0.48f, 0.7f, 0.19f, 1f);
	private static readonly Color LibraryBlue = new(0.25f, 0.57f, 0.86f, 1f);
	private static readonly Color LibraryParchment = new(0.78f, 0.68f, 0.52f, 1f);

	private static readonly LibrarySection[] LibrarySections =
	{
		LibrarySection.Cards,
		LibrarySection.ClassesAndAuras,
		LibrarySection.Bestiary,
		LibrarySection.Atlas,
		LibrarySection.Manual
	};

	private GameObject libraryPanel;
	private Image libraryBackgroundImage;
	private AspectRatioFitter libraryBackgroundAspectFitter;
	private Image libraryScreenOuterFrameImage;
	private RectTransform libraryHeaderRoot;
	private RectTransform libraryTabsRoot;
	private RectTransform libraryOverviewViewport;
	private RectTransform libraryOverviewContent;
	private GridLayoutGroup libraryOverviewGrid;
	private GameObject libraryOverviewPanel;
	private GameObject libraryDetailPanel;
	private Text libraryDetailTitleText;
	private Text libraryDetailBodyText;
	private readonly Image[] libraryTabBackgrounds = new Image[5];
	private readonly Image[] libraryTabFrames = new Image[5];
	private readonly Text[] libraryTabLabels = new Text[5];
	private readonly Image[] libraryProgressFills = new Image[5];
	private readonly Text[] libraryProgressTexts = new Text[5];
	private LibrarySection librarySelectedSection;

	private void CreateLibraryView(Transform canvasTransform, Font fallbackFont)
	{
		if ((Object)(object)cardDatabase == (Object)null)
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");

		Image root = CreateImage("Library", canvasTransform, Color.black);
		root.raycastTarget = true;
		Stretch(root.rectTransform);
		libraryPanel = root.gameObject;
		Canvas canvas = libraryPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 900;
		libraryPanel.AddComponent<GraphicRaycaster>();

		libraryBackgroundImage = CreateImage("Library Background", root.transform, Color.white);
		libraryBackgroundImage.raycastTarget = false;
		Stretch(libraryBackgroundImage.rectTransform);
		Sprite portraitBackground = LoadSpriteResource("UI/LibraryUI/library_background_portrait");
		libraryBackgroundImage.sprite = portraitBackground;
		libraryBackgroundAspectFitter = ConfigureFittedBackground(
			libraryBackgroundImage,
			portraitBackground,
			9f / 16f);

		Image shade = CreateImage(
			"Library Veil",
			root.transform,
			new Color(0f, 1f / 255f, 4f / 255f, 0.4196078f));
		shade.raycastTarget = false;
		Stretch(shade.rectTransform);

		libraryScreenOuterFrameImage = CreateImage("Screen Outer Frame", root.transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(libraryScreenOuterFrameImage);
		SetRect(libraryScreenOuterFrameImage.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.755f));

		Image libraryHeader = CreateImage(
			"Library Header",
			root.transform,
			Color.white);
		libraryHeader.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		libraryHeader.type = Image.Type.Simple;
		libraryHeader.preserveAspect = false;
		libraryHeader.raycastTarget = false;
		libraryHeaderRoot = libraryHeader.rectTransform;

		Text title = CreateText(
			"Library Title",
			(Transform)(object)libraryHeaderRoot,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont,
			43,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		title.text = GameText.Get(GameTextKeys.Library.Title);
		title.color = new Color32(0xF2, 0xC9, 0x57, 0xFF);
		AddLibraryTextShadow(title, 2f);
		SetRect(title.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		title.rectTransform.offsetMin = new Vector2(0f, -23f);
		title.rectTransform.offsetMax = new Vector2(0f, -23f);

		libraryTabsRoot = CreateImage(
			"Library Tabs",
			root.transform,
			new Color(0.005f, 0.007f, 0.01f, 0.94f)).rectTransform;
		CreateLibraryTabs(fallbackFont);

		CreateLibraryOverview(root.transform, fallbackFont);
		CreateLibraryDetail(root.transform, fallbackFont);

		librarySelectedSection = LibrarySection.Cards;
		HighlightLibraryTab(librarySelectedSection);
		RefreshLibraryLayout();
		libraryPanel.SetActive(false);
	}

	private void CreateLibraryTabs(Font fallbackFont)
	{
		for (int index = 0; index < LibrarySections.Length; index++)
		{
			LibrarySection section = LibrarySections[index];
			Image tab = CreateImage(
				"Library Tab " + section,
				(Transform)(object)libraryTabsRoot,
				new Color(0.02f, 0.022f, 0.025f, 0.98f));
			tab.raycastTarget = true;
			Button button = tab.gameObject.AddComponent<Button>();
			button.targetGraphic = tab;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.08f, 1.04f, 1.12f, 1f);
			colors.pressedColor = new Color(0.78f, 0.7f, 0.88f, 1f);
			colors.selectedColor = Color.white;
			button.colors = colors;
			button.onClick.AddListener(new UnityAction(() =>
			{
				PlayGenericButtonClickSfx();
				ShowLibrarySection(section);
			}));

			Image frame = CreateImage(
				"Library Tab Frame",
				tab.transform,
				new Color(0.78f, 0.55f, 0.24f, 0.82f));
			frame.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
			frame.preserveAspect = false;
			frame.raycastTarget = false;
			Stretch(frame.rectTransform);

			Image icon = CreateImage(
				"Library Tab Icon",
				tab.transform,
				Color.white);
			icon.sprite = LoadSpriteResource(LibrarySectionIconPath(section));
			icon.preserveAspect = true;
			icon.raycastTarget = false;
			SetRect(icon.rectTransform, new Vector2(0.16f, 0.35f), new Vector2(0.84f, 0.94f));

			Text label = CreateText(
				"Library Tab Label",
				tab.transform,
				AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont,
				17,
				FontStyle.Normal,
				TextAnchor.MiddleCenter);
			AccardND.Battlefield.MmoUiTheme.StyleAsTitle(label);
			label.text = LibrarySectionShortTitle(section);
			label.color = LibraryGold;
			label.resizeTextForBestFit = true;
			label.resizeTextMinSize = 10;
			label.resizeTextMaxSize = 18;
			AddLibraryTextShadow(label, 1f);
			SetRect(label.rectTransform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.38f));

			libraryTabBackgrounds[index] = tab;
			libraryTabFrames[index] = frame;
			libraryTabLabels[index] = label;
		}
	}

	private void CreateLibraryOverview(Transform parent, Font fallbackFont)
	{
		libraryOverviewPanel = new GameObject("Library Overview", typeof(RectTransform));
		libraryOverviewPanel.transform.SetParent(parent, false);
		RectTransform overviewRect = (RectTransform)libraryOverviewPanel.transform;
		Stretch(overviewRect);

		GameObject scrollObject = new(
			"Library Overview Scroll",
			typeof(RectTransform),
			typeof(ScrollRect));
		scrollObject.transform.SetParent(libraryOverviewPanel.transform, false);
		RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
		Stretch(scrollRectTransform);
		ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 32f;

		Image viewport = CreateImage(
			"Library Overview Viewport",
			scrollObject.transform,
			new Color(0f, 0f, 0f, 0.001f));
		viewport.raycastTarget = true;
		Stretch(viewport.rectTransform);
		viewport.gameObject.AddComponent<RectMask2D>();
		libraryOverviewViewport = viewport.rectTransform;
		scrollRect.viewport = libraryOverviewViewport;

		GameObject content = new(
			"Library Overview Content",
			typeof(RectTransform),
			typeof(GridLayoutGroup));
		content.transform.SetParent(viewport.transform, false);
		libraryOverviewContent = (RectTransform)content.transform;
		libraryOverviewContent.anchorMin = new Vector2(0f, 1f);
		libraryOverviewContent.anchorMax = new Vector2(1f, 1f);
		libraryOverviewContent.pivot = new Vector2(0.5f, 1f);
		libraryOverviewContent.anchoredPosition = Vector2.zero;
		libraryOverviewGrid = content.GetComponent<GridLayoutGroup>();
		libraryOverviewGrid.childAlignment = TextAnchor.UpperCenter;
		libraryOverviewGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
		libraryOverviewGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
		libraryOverviewGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		libraryOverviewGrid.padding = new RectOffset(12, 12, 10, 10);
		scrollRect.content = libraryOverviewContent;

		for (int index = 0; index < LibrarySections.Length; index++)
			CreateLibraryOverviewTile(libraryOverviewContent, fallbackFont, LibrarySections[index], index);
	}

	private void CreateLibraryOverviewTile(
		Transform parent,
		Font fallbackFont,
		LibrarySection section,
		int index)
	{
		Color accent = LibrarySectionColor(section);
		Image tile = CreateImage(
			"Library Entry " + section,
			parent,
			new Color(0.012f, 0.014f, 0.016f, 0.95f));
		tile.raycastTarget = true;
		Button button = tile.gameObject.AddComponent<Button>();
		button.targetGraphic = tile;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.06f, 1.04f, 1.08f, 1f);
		colors.pressedColor = new Color(0.78f, 0.74f, 0.86f, 1f);
		button.colors = colors;
		button.onClick.AddListener(new UnityAction(() =>
		{
			PlayGenericButtonClickSfx();
			ShowLibrarySection(section);
		}));

		Image frame = CreateImage(
			"Library Entry Frame",
			tile.transform,
			new Color(0.82f, 0.57f, 0.22f, 0.82f));
		frame.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
		frame.preserveAspect = false;
		frame.raycastTarget = false;
		Stretch(frame.rectTransform);
		frame.rectTransform.offsetMin = new Vector2(6f, 6f);
		frame.rectTransform.offsetMax = new Vector2(-6f, -6f);

		Image art = CreateImage("Library Entry Art", tile.transform, Color.white);
		art.sprite = LoadSpriteResource(LibrarySectionIconPath(section));
		art.preserveAspect = true;
		art.raycastTarget = false;
		SetRect(art.rectTransform, new Vector2(0.025f, 0.08f), new Vector2(0.3f, 0.92f));

		Text title = CreateText(
			"Library Entry Title",
			tile.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont,
			31,
			FontStyle.Normal,
			TextAnchor.MiddleLeft);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		title.text = LibrarySectionTitle(section);
		title.color = accent;
		title.resizeTextForBestFit = true;
		title.resizeTextMinSize = 18;
		title.resizeTextMaxSize = 31;
		AddLibraryTextShadow(title, 1.5f);
		SetRect(title.rectTransform, new Vector2(0.33f, 0.55f), new Vector2(0.87f, 0.92f));

		Text description = CreateText(
			"Library Entry Description",
			tile.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont,
			18,
			FontStyle.Normal,
			TextAnchor.UpperLeft);
		description.text = LibrarySectionDescription(section);
		description.color = new Color(0.81f, 0.78f, 0.7f, 1f);
		description.horizontalOverflow = HorizontalWrapMode.Wrap;
		description.verticalOverflow = VerticalWrapMode.Truncate;
		description.resizeTextForBestFit = true;
		description.resizeTextMinSize = 12;
		description.resizeTextMaxSize = 19;
		SetRect(description.rectTransform, new Vector2(0.33f, 0.25f), new Vector2(0.86f, 0.6f));

		Image progressBack = CreateImage(
			"Library Entry Progress Back",
			tile.transform,
			new Color(0.025f, 0.022f, 0.018f, 0.96f));
		progressBack.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
		progressBack.type = Image.Type.Sliced;
		progressBack.raycastTarget = false;
		SetRect(progressBack.rectTransform, new Vector2(0.33f, 0.08f), new Vector2(0.68f, 0.19f));

		Image progressFill = CreateImage(
			"Library Entry Progress Fill",
			progressBack.transform,
			accent);
		progressFill.raycastTarget = false;
		progressFill.rectTransform.anchorMin = Vector2.zero;
		progressFill.rectTransform.anchorMax = new Vector2(0.02f, 1f);
		progressFill.rectTransform.offsetMin = new Vector2(3f, 3f);
		progressFill.rectTransform.offsetMax = new Vector2(-3f, -3f);

		Text progressText = CreateText(
			"Library Entry Progress Text",
			tile.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont,
			16,
			FontStyle.Bold,
			TextAnchor.MiddleLeft);
		progressText.color = accent;
		progressText.resizeTextForBestFit = true;
		progressText.resizeTextMinSize = 11;
		progressText.resizeTextMaxSize = 17;
		SetRect(progressText.rectTransform, new Vector2(0.71f, 0.055f), new Vector2(0.88f, 0.21f));

		Image arrow = CreateImage("Library Entry Arrow", tile.transform, accent);
		arrow.sprite = LoadSpriteResource("UI/right_arrow");
		arrow.preserveAspect = true;
		arrow.raycastTarget = false;
		SetRect(arrow.rectTransform, new Vector2(0.89f, 0.28f), new Vector2(0.975f, 0.72f));

		libraryProgressFills[index] = progressFill;
		libraryProgressTexts[index] = progressText;
	}

	private void CreateLibraryDetail(Transform parent, Font fallbackFont)
	{
		Image detail = CreateImage(
			"Library Detail",
			parent,
			new Color(0.008f, 0.01f, 0.014f, 0.97f));
		detail.raycastTarget = true;
		libraryDetailPanel = detail.gameObject;
		Image frame = CreateImage(
			"Library Detail Frame",
			detail.transform,
			new Color(0.86f, 0.6f, 0.23f, 0.9f));
		frame.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
		frame.preserveAspect = false;
		frame.raycastTarget = false;
		Stretch(frame.rectTransform);

		libraryDetailTitleText = CreateText(
			"Library Detail Title",
			detail.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont,
			38,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(libraryDetailTitleText);
		libraryDetailTitleText.color = LibraryGold;
		AddLibraryTextShadow(libraryDetailTitleText, 2f);
		SetRect(libraryDetailTitleText.rectTransform, new Vector2(0.16f, 0.84f), new Vector2(0.84f, 0.97f));

		Button indexButton = CreateImageButton(
			"Library Detail Index",
			detail.transform,
			fallbackFont,
			LoadSpriteResource("UI/left_arrow"),
			"INDICE");
		indexButton.onClick.AddListener(new UnityAction(() =>
		{
			PlayGenericButtonClickSfx();
			ShowLibraryOverview();
		}));
		SetRect(
			(RectTransform)indexButton.transform,
			new Vector2(0.025f, 0.855f),
			new Vector2(0.15f, 0.965f));

		GameObject scrollObject = new(
			"Library Detail Scroll",
			typeof(RectTransform),
			typeof(ScrollRect));
		scrollObject.transform.SetParent(detail.transform, false);
		SetRect(
			(RectTransform)scrollObject.transform,
			new Vector2(0.045f, 0.055f),
			new Vector2(0.955f, 0.82f));
		ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 32f;

		Image viewport = CreateImage(
			"Library Detail Viewport",
			scrollObject.transform,
			new Color(0f, 0f, 0f, 0.001f));
		viewport.raycastTarget = true;
		Stretch(viewport.rectTransform);
		viewport.gameObject.AddComponent<RectMask2D>();
		scrollRect.viewport = viewport.rectTransform;

		GameObject content = new(
			"Library Detail Content",
			typeof(RectTransform),
			typeof(VerticalLayoutGroup),
			typeof(ContentSizeFitter));
		content.transform.SetParent(viewport.transform, false);
		RectTransform contentRect = (RectTransform)content.transform;
		contentRect.anchorMin = new Vector2(0f, 1f);
		contentRect.anchorMax = new Vector2(1f, 1f);
		contentRect.pivot = new Vector2(0.5f, 1f);
		contentRect.offsetMin = Vector2.zero;
		contentRect.offsetMax = Vector2.zero;
		VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(20, 20, 14, 22);
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		libraryDetailBodyText = CreateText(
			"Library Detail Body",
			content.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont,
			22,
			FontStyle.Normal,
			TextAnchor.UpperLeft);
		libraryDetailBodyText.color = new Color(0.88f, 0.86f, 0.8f, 1f);
		libraryDetailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		libraryDetailBodyText.verticalOverflow = VerticalWrapMode.Overflow;
		libraryDetailBodyText.supportRichText = true;
		libraryDetailBodyText.raycastTarget = false;
		scrollRect.content = contentRect;
		libraryDetailPanel.SetActive(false);
	}

	private void ShowLibrary()
	{
		if ((Object)(object)libraryPanel == (Object)null)
		{
			ShowUnderDevelopmentPopup();
			return;
		}

		if ((Object)(object)modeSelectionPanel != (Object)null)
			modeSelectionPanel.SetActive(false);
		if ((Object)(object)sanctuaryPanel != (Object)null)
			sanctuaryPanel.SetActive(false);
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
			campaignModeSelectionPanel.SetActive(false);
		if ((Object)(object)adventureChapterPanel != (Object)null)
			adventureChapterPanel.SetActive(false);
		SetModeSelectionButtonsActive(false);
		SetAccountHubHudActive(true);
		libraryPanel.SetActive(true);
		libraryPanel.transform.SetAsLastSibling();
		RefreshAccountBannerView();
		RefreshLibraryProgress();
		ShowLibraryOverview();
		RefreshLibraryLayout();
	}

	private void HideLibrary()
	{
		ShowHubFromSinglePlayer();
	}

	private void ShowLibraryOverview()
	{
		if ((Object)(object)libraryOverviewPanel != (Object)null)
			libraryOverviewPanel.SetActive(true);
		if ((Object)(object)libraryDetailPanel != (Object)null)
			libraryDetailPanel.SetActive(false);
		HighlightLibraryTab(librarySelectedSection);
	}

	private void ShowLibrarySection(LibrarySection section)
	{
		librarySelectedSection = section;
		if ((Object)(object)libraryOverviewPanel != (Object)null)
			libraryOverviewPanel.SetActive(false);
		if ((Object)(object)libraryDetailPanel != (Object)null)
			libraryDetailPanel.SetActive(true);
		if ((Object)(object)libraryDetailTitleText != (Object)null)
		{
			libraryDetailTitleText.text = LibrarySectionTitle(section);
			libraryDetailTitleText.color = LibrarySectionColor(section);
		}
		if ((Object)(object)libraryDetailBodyText != (Object)null)
			libraryDetailBodyText.text = BuildLibrarySectionText(section);
		HighlightLibraryTab(section);
		Canvas.ForceUpdateCanvases();
	}

	private void RefreshLibraryLayout()
	{
		if ((Object)(object)libraryPanel == (Object)null)
			return;

		bool landscape = Screen.width > Screen.height;
		Sprite background = LoadSpriteResource(
			landscape
				? "UI/LibraryUI/library_background_landscape"
				: "UI/LibraryUI/library_background_portrait");
		libraryBackgroundImage.sprite = background;
		if ((Object)(object)libraryBackgroundAspectFitter != (Object)null)
		{
			libraryBackgroundAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			libraryBackgroundAspectFitter.aspectRatio =
				(Object)(object)background != (Object)null
					? background.rect.width / background.rect.height
					: (landscape ? 16f / 9f : 9f / 16f);
		}

		SetRect(
			libraryHeaderRoot,
			landscape ? new Vector2(0.24f, 0.73f) : new Vector2(0.03f, 0.745f),
			landscape ? new Vector2(0.76f, 0.87f) : new Vector2(0.97f, 0.865f));
		SetRect(
			libraryScreenOuterFrameImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, landscape ? 0.74f : 0.755f));
		SetRect(
			libraryTabsRoot,
			landscape ? new Vector2(0.055f, 0.585f) : new Vector2(0.018f, 0.615f),
			landscape ? new Vector2(0.945f, 0.715f) : new Vector2(0.982f, 0.735f));

		for (int index = 0; index < libraryTabBackgrounds.Length; index++)
		{
			Image tab = libraryTabBackgrounds[index];
			if ((Object)(object)tab == (Object)null)
				continue;
			float minimum = index / 5f;
			float maximum = (index + 1) / 5f;
			SetRect(
				tab.rectTransform,
				new Vector2(minimum + 0.004f, 0.035f),
				new Vector2(maximum - 0.004f, 0.965f));
			if ((Object)(object)libraryTabLabels[index] != (Object)null)
			{
				libraryTabLabels[index].fontSize = landscape ? 16 : 15;
				libraryTabLabels[index].resizeTextMaxSize = landscape ? 17 : 16;
			}
		}

		RectTransform overviewRect = (RectTransform)libraryOverviewPanel.transform;
		SetRect(
			overviewRect,
			landscape ? new Vector2(0.035f, 0.04f) : new Vector2(0.018f, 0.035f),
			landscape ? new Vector2(0.965f, 0.57f) : new Vector2(0.982f, 0.602f));
		RectTransform detailRect = (RectTransform)libraryDetailPanel.transform;
		SetRect(
			detailRect,
			landscape ? new Vector2(0.055f, 0.05f) : new Vector2(0.025f, 0.04f),
			landscape ? new Vector2(0.945f, 0.565f) : new Vector2(0.975f, 0.6f));

		Canvas.ForceUpdateCanvases();
		int columns = landscape ? 2 : 1;
		float spacing = landscape ? 22f : 20f;
		float viewportWidth = Mathf.Max(320f, libraryOverviewViewport.rect.width);
		float usableWidth = viewportWidth - libraryOverviewGrid.padding.horizontal - spacing * (columns - 1);
		float cellWidth = Mathf.Max(260f, usableWidth / columns);
		float cellHeight = landscape ? 168f : 166f;
		libraryOverviewGrid.constraintCount = columns;
		libraryOverviewGrid.spacing = new Vector2(spacing, spacing);
		libraryOverviewGrid.cellSize = new Vector2(cellWidth, cellHeight);
		int rows = Mathf.CeilToInt(LibrarySections.Length / (float)columns);
		float contentHeight =
			libraryOverviewGrid.padding.vertical
			+ rows * cellHeight
			+ Mathf.Max(0, rows - 1) * spacing;
		libraryOverviewContent.sizeDelta = new Vector2(0f, contentHeight);
		if ((Object)(object)libraryDetailBodyText != (Object)null)
		{
			libraryDetailBodyText.fontSize = landscape ? 20 : 21;
			libraryDetailBodyText.resizeTextMaxSize = libraryDetailBodyText.fontSize;
		}
	}

	private void RefreshLibraryProgress()
	{
		SinglePlayerProgressSave progress = singlePlayerProgressService.Progress;
		for (int index = 0; index < LibrarySections.Length; index++)
		{
			LibraryProgress sectionProgress = GetLibraryProgress(LibrarySections[index], progress);
			float normalized = Mathf.Clamp01((float)sectionProgress.Current / sectionProgress.Total);
			if ((Object)(object)libraryProgressFills[index] != (Object)null)
				libraryProgressFills[index].rectTransform.anchorMax =
					new Vector2(Mathf.Max(0.02f, normalized), 1f);
			if ((Object)(object)libraryProgressTexts[index] != (Object)null)
				libraryProgressTexts[index].text =
					$"{sectionProgress.Current} / {sectionProgress.Total}   {Mathf.RoundToInt(normalized * 100f)}%";
		}
	}

	private LibraryProgress GetLibraryProgress(
		LibrarySection section,
		SinglePlayerProgressSave progress)
	{
		switch (section)
		{
			case LibrarySection.Cards:
			{
				int total = cardDatabase?.Cards?.Count(card =>
					(Object)(object)card != (Object)null
					&& card.Category != CardCategory.CardBack) ?? 0;
				return new LibraryProgress(total, total);
			}
			case LibrarySection.ClassesAndAuras:
			{
				int current = progress?.unlockedClasses?
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Count() ?? 0;
				return new LibraryProgress(current, 9);
			}
			case LibrarySection.Bestiary:
			{
				string[] encounterKeys =
				{
					"boss_bragus",
					"boss_trentor",
					"boss_medusa",
					"boss_palatir",
					"miniboss_golem"
				};
				int current = encounterKeys.Count(key => singlePlayerProgressService.GetCounter(key) > 0);
				return new LibraryProgress(current, encounterKeys.Length);
			}
			case LibrarySection.Atlas:
			{
				int total = scenarioCatalog?.Scenarios?.Count(value =>
					(Object)(object)value != (Object)null) ?? 0;
				int current = progress?.unlockedScenarios?
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Count() ?? 0;
				return new LibraryProgress(current, total);
			}
			default:
				return new LibraryProgress(12, 12);
		}
	}

	private void HighlightLibraryTab(LibrarySection selected)
	{
		for (int index = 0; index < LibrarySections.Length; index++)
		{
			bool active = LibrarySections[index] == selected;
			if ((Object)(object)libraryTabBackgrounds[index] != (Object)null)
				libraryTabBackgrounds[index].color = active
					? new Color(0.12f, 0.035f, 0.18f, 0.98f)
					: new Color(0.02f, 0.022f, 0.025f, 0.98f);
			if ((Object)(object)libraryTabFrames[index] != (Object)null)
				libraryTabFrames[index].color = active
					? new Color(0.82f, 0.34f, 1f, 0.96f)
					: new Color(0.78f, 0.55f, 0.24f, 0.82f);
			if ((Object)(object)libraryTabLabels[index] != (Object)null)
				libraryTabLabels[index].color = active
					? new Color(0.94f, 0.76f, 1f, 1f)
					: LibraryGold;
		}
	}

	private string BuildLibrarySectionText(LibrarySection section)
	{
		return section switch
		{
			LibrarySection.Cards => BuildLibraryCardsText(),
			LibrarySection.ClassesAndAuras => BuildLibraryClassesText(),
			LibrarySection.Bestiary => BuildLibraryBestiaryText(),
			LibrarySection.Atlas => BuildLibraryAtlasText(),
			_ => BuildLibraryManualText()
		};
	}

	private string BuildLibraryCardsText()
	{
		if ((Object)(object)cardDatabase == (Object)null || cardDatabase.Cards == null)
			return "Il catalogo delle carte non e disponibile.";

		StringBuilder builder = new();
		builder.AppendLine("<color=#C977F0><b>CATALOGO DELLE CARTE</b></color>");
		builder.AppendLine("Consulta le carte presenti nel gioco, la loro classe, il dado Vigore e le regole speciali.");
		builder.AppendLine();

		foreach (IGrouping<CardCategory, CardDefinition> group in cardDatabase.Cards
			.Where(card => (Object)(object)card != (Object)null && card.Category != CardCategory.CardBack)
			.OrderBy(card => card.Category)
			.ThenBy(card => card.DisplayName)
			.GroupBy(card => card.Category))
		{
			builder.AppendLine($"<color=#D9A34A><b>{LibraryCardCategoryName(group.Key).ToUpperInvariant()}</b></color>");
			foreach (CardDefinition card in group)
			{
				string className = card.HasHeroClass
					? CardRulesGlossary.HeroClassName(card.HeroClass)
					: "Senza classe";
				string rules = string.IsNullOrWhiteSpace(card.RulesText)
					? string.Empty
					: $"  -  {card.RulesText}";
				builder.AppendLine(
					$"<b>{card.DisplayName}</b>  |  {className}  |  Vigore D{Mathf.Max(0, card.Strength)}{rules}");
			}
			builder.AppendLine();
		}
		return builder.ToString();
	}

	private static string BuildLibraryClassesText()
	{
		StringBuilder builder = new();
		builder.AppendLine("<color=#E2A64B><b>CLASSI, FAZIONI E AURE</b></color>");
		builder.AppendLine("Tre carte della stessa classe attivano la relativa Aura di Classe. Tre classi diverse della stessa fazione attivano l'Aura di Fazione.");
		builder.AppendLine("Una carta Fortuza, una Astuta e una Magica attivano l'Aura di Formazione.");
		builder.AppendLine();
		builder.AppendLine("<b>Priorita:</b> Aura di Classe  >  Aura di Fazione  >  Aura di Formazione.");
		builder.AppendLine();

		foreach (HeroClass heroClass in Enum.GetValues(typeof(HeroClass)))
		{
			ClassFamily family = HeroClassFamily.Of(heroClass);
			BattleAuraType aura = ClassAuraFor(heroClass);
			builder.AppendLine(
				$"<color=#D9A34A><b>{CardRulesGlossary.HeroClassName(heroClass).ToUpperInvariant()}</b></color>  -  {CardRulesGlossary.ClassFamilyName(family)}");
			builder.AppendLine(CardRulesGlossary.AbilityDescription(heroClass));
			builder.AppendLine("<b>Aura:</b> " + AuraEffectText(aura));
			builder.AppendLine();
		}

		builder.AppendLine("<color=#D9A34A><b>AURE DI FAZIONE</b></color>");
		builder.AppendLine("<b>Fortuza:</b> " + AuraEffectText(BattleAuraType.Might));
		builder.AppendLine("<b>Astuta:</b> " + AuraEffectText(BattleAuraType.Cunning));
		builder.AppendLine("<b>Magica:</b> " + AuraEffectText(BattleAuraType.Magic));
		builder.AppendLine("<b>Formazione:</b> " + AuraEffectText(BattleAuraType.Formation));
		return builder.ToString();
	}

	private string BuildLibraryBestiaryText()
	{
		if ((Object)(object)cardDatabase == (Object)null || cardDatabase.Cards == null)
			return "Il bestiario non e disponibile.";

		StringBuilder builder = new();
		builder.AppendLine("<color=#7AAF31><b>BESTIARIO</b></color>");
		builder.AppendLine("Archivio delle creature, dei miniboss e dei boss che popolano la campagna.");
		builder.AppendLine();

		foreach (CardCategory category in new[] { CardCategory.Boss, CardCategory.Monster })
		{
			builder.AppendLine(
				category == CardCategory.Boss
					? "<color=#D9A34A><b>BOSS E MINIBOSS</b></color>"
					: "<color=#D9A34A><b>CREATURE</b></color>");
			foreach (CardDefinition card in cardDatabase.Cards
				.Where(card =>
					(Object)(object)card != (Object)null
					&& card.Category == category)
				.OrderBy(card => card.DisplayName))
			{
				string className = card.HasHeroClass
					? CardRulesGlossary.HeroClassName(card.HeroClass)
					: "Senza classe";
				builder.AppendLine(
					$"<b>{card.DisplayName}</b>  |  {className}  |  Vigore D{Mathf.Max(0, card.Strength)}");
			}
			builder.AppendLine();
		}
		return builder.ToString();
	}

	private string BuildLibraryAtlasText()
	{
		if ((Object)(object)scenarioCatalog == (Object)null || scenarioCatalog.Scenarios == null)
			return "L'atlante della campagna non e disponibile.";

		StringBuilder builder = new();
		builder.AppendLine("<color=#4D93D5><b>ATLANTE DELLA CAMPAGNA</b></color>");
		builder.AppendLine("Capitoli, stanze, scenari speciali e boss vengono raccolti qui.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>TIPI DI STANZA</b></color>");
		builder.AppendLine("<b>Mostro:</b> affronta una formazione nemica.");
		builder.AppendLine("<b>Boss:</b> sconfiggi una creatura con regole uniche.");
		builder.AppendLine("<b>Mercante:</b> compra, vendi o recupera carte.");
		builder.AppendLine("<b>Tesoro:</b> ottieni una ricompensa per la run.");
		builder.AppendLine("<b>Imprevisto:</b> risolvi un evento che modifica il percorso.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>SCENARI</b></color>");

		foreach (ScenarioDefinition scenario in scenarioCatalog.Scenarios
			.Where(value => (Object)(object)value != (Object)null)
			.OrderBy(value => value.RoomType)
			.ThenBy(value => value.DisplayName))
		{
			string name = string.IsNullOrWhiteSpace(scenario.DisplayName)
				? scenario.Id
				: scenario.DisplayName;
			string boss = string.IsNullOrWhiteSpace(scenario.BossId)
				? string.Empty
				: $"  |  Boss: {scenario.BossId}";
			builder.AppendLine(
				$"<b>{name}</b>  |  {LibraryRoomTypeName(scenario.RoomType)}  |  {LibraryDifficultyName(scenario.Difficulty)}{boss}");
		}
		return builder.ToString();
	}

	private static string BuildLibraryManualText()
	{
		StringBuilder builder = new();
		builder.AppendLine("<color=#CBB18A><b>MANUALE DI ACCARD N' DIE</b></color>");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>1. OBIETTIVO</b></color>");
		builder.AppendLine("Costruisci una formazione di tre carte, sfrutta classi e aure e sconfiggi la formazione avversaria.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>2. VIGORE</b></color>");
		builder.AppendLine("Il valore della carta indica il dado Vigore usato nei confronti: D4, D6, D8, D10, D12 o D20.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>3. FAZIONI</b></color>");
		builder.AppendLine("Fortuza, Astuta e Magica determinano vantaggio e svantaggio durante i confronti.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>4. ATTACCO E DIFESA</b></color>");
		builder.AppendLine("L'attaccante sceglie un bersaglio valido. I dadi vengono tirati applicando vantaggi, malus, abilita e aure attive.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>5. ABILITA</b></color>");
		builder.AppendLine("Ogni classe possiede una regola caratteristica. Alcune abilita richiedono un bersaglio o entrano in cooldown.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>6. AURE</b></color>");
		builder.AppendLine("La composizione delle tre carte schierate determina una sola aura. Le aure non si sommano.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>7. CAMPAGNA</b></color>");
		builder.AppendLine("Supera stanze, potenzia il mazzo durante la run e raggiungi il boss del capitolo.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>8. DRAFT</b></color>");
		builder.AppendLine("Scegli il capitano e completa il mazzo selezionando le offerte iniziali.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>9. MERCANTE</b></color>");
		builder.AppendLine("Puoi aprire il banco Carte oppure il banco Oggetti. Vendita e recupero restano disponibili.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>10. CONSUMABILI</b></color>");
		builder.AppendLine("Gli oggetti nella bisaccia modificano la run e vengono preparati dal Santuario.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>11. SANTUARIO</b></color>");
		builder.AppendLine("Usa il miele per sbloccare classi, tecniche, reliquie e nuovi strumenti permanenti.");
		builder.AppendLine();
		builder.AppendLine("<color=#D9A34A><b>12. MULTIPLAYER</b></color>");
		builder.AppendLine("Prepara un loadout valido e affronta altri giocatori nelle modalita PvP disponibili.");
		return builder.ToString();
	}

	private static void AddLibraryTextShadow(Text text, float distance)
	{
		if ((Object)(object)text == (Object)null)
			return;
		Outline outline = text.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
		outline.effectDistance = new Vector2(distance, -distance);
	}

	private static string LibrarySectionIconPath(LibrarySection section)
	{
		return section switch
		{
			LibrarySection.Cards => "UI/LibraryUI/section_cards",
			LibrarySection.ClassesAndAuras => "UI/LibraryUI/section_classes_auras",
			LibrarySection.Bestiary => "UI/LibraryUI/section_bestiary",
			LibrarySection.Atlas => "UI/LibraryUI/section_atlas",
			_ => "UI/LibraryUI/section_manual"
		};
	}

	private static string LibrarySectionTitle(LibrarySection section)
	{
		return section switch
		{
			LibrarySection.Cards => "CARTE",
			LibrarySection.ClassesAndAuras => "CLASSI & AURE",
			LibrarySection.Bestiary => "BESTIARIO",
			LibrarySection.Atlas => "ATLANTE",
			_ => "MANUALE"
		};
	}

	private static string LibrarySectionShortTitle(LibrarySection section)
	{
		return section == LibrarySection.ClassesAndAuras
			? "CLASSI\n& AURE"
			: LibrarySectionTitle(section);
	}

	private static string LibrarySectionDescription(LibrarySection section)
	{
		return section switch
		{
			LibrarySection.Cards => "Catalogo delle carte presenti nel gioco.",
			LibrarySection.ClassesAndAuras => "Classi, abilita, fazioni e combinazioni di aura.",
			LibrarySection.Bestiary => "Creature, miniboss e boss della campagna.",
			LibrarySection.Atlas => "Capitoli, stanze e scenari del viaggio.",
			_ => "Regole, meccaniche e consigli di gioco."
		};
	}

	private static Color LibrarySectionColor(LibrarySection section)
	{
		return section switch
		{
			LibrarySection.Cards => LibraryPurple,
			LibrarySection.ClassesAndAuras => LibraryGold,
			LibrarySection.Bestiary => LibraryGreen,
			LibrarySection.Atlas => LibraryBlue,
			_ => LibraryParchment
		};
	}

	private static string LibraryCardCategoryName(CardCategory category)
	{
		return category switch
		{
			CardCategory.Boss => "Boss",
			CardCategory.Monster => "Creature e carte combattente",
			CardCategory.Item => "Oggetti",
			_ => category.ToString()
		};
	}

	private static string LibraryRoomTypeName(RoomType roomType)
	{
		return roomType switch
		{
			RoomType.Monster => "Mostro",
			RoomType.Boss => "Boss",
			RoomType.Merchant => "Mercante",
			RoomType.Loot => "Tesoro",
			RoomType.QuickChallenge => "Sfida Veloce",
			_ => "Qualsiasi"
		};
	}

	private static string LibraryDifficultyName(RoomDifficulty difficulty)
	{
		return difficulty switch
		{
			RoomDifficulty.Easy => "Accessibile",
			RoomDifficulty.Normal => "Normale",
			RoomDifficulty.Hard => "Diabolica",
			_ => "Qualsiasi"
		};
	}
}
}
