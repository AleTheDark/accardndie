using System;
using System.Collections.Generic;
using System.Linq;
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
	private GameObject shopPanel;
	private Text shopStatusText;
	private RectTransform shopOffersRoot;
	private RectTransform shopCatalogRoot;
	private RectTransform shopPremiumRoot;
	private GameObject shopScrollSection;
	private Image shopScrollViewport;
	private RectTransform shopScrollContent;
	private ScrollRect shopScrollRect;
	private GameObject shopEmptyState;
	private Text shopEmptyText;
	private Button shopSanctuaryButton;
	private GameObject shopPurchaseConfirmation;
	private Text shopPurchaseConfirmationText;
	private Button shopPurchaseConfirmButton;
	private SanctuaryEntryData pendingShopPurchaseEntry;
	private string pendingShopPurchaseOfferId;
	private readonly List<GameObject> shopDynamicObjects = new();
	private bool shopLoading;
	private bool shopPurchasing;

	private const int ShopGridColumns = 3;
	private const float ShopOfferGap = 0.018f;

	private static readonly Color ShopGold = new(0.95f, 0.79f, 0.34f);
	private static readonly Color ShopBody = new(0.84f, 0.88f, 0.91f);

	private void CreateShopView(Font fallbackFont)
	{
		Image root = CreateImage("Shop", (Transform)(object)canvasRect, new Color(0.006f, 0.008f, 0.012f, 1f));
		root.raycastTarget = true;
		shopPanel = root.gameObject;
		SetRect(root.rectTransform, Vector2.zero, Vector2.one);
		Canvas canvas = root.gameObject.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 900;
		root.gameObject.AddComponent<GraphicRaycaster>();

		Image backdropViewport = CreateImage("Shop Backdrop Viewport", root.transform, Color.clear);
		backdropViewport.raycastTarget = false;
		SetRect(backdropViewport.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));
		backdropViewport.gameObject.AddComponent<RectMask2D>();

		Image backdrop = CreateImage("Shop Backdrop", backdropViewport.transform, new Color(1f, 1f, 1f, 0.6f));
		backdrop.sprite = LoadSpriteResource("UI/Shop/shop_background");
		backdrop.type = Image.Type.Simple;
		backdrop.preserveAspect = true;
		backdrop.color = new Color(1f, 1f, 1f, 0.6f);
		backdrop.raycastTarget = false;
		SetRect(backdrop.rectTransform, Vector2.zero, Vector2.one);
		if ((Object)(object)backdrop.sprite != (Object)null)
		{
			AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
			fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			fitter.aspectRatio = backdrop.sprite.rect.width / Mathf.Max(1f, backdrop.sprite.rect.height);
		}

		Image veil = CreateImage("Shop Veil", root.transform, new Color(0f, 1f / 255f, 4f / 255f, 0.6f));
		veil.raycastTarget = false;
		SetRect(veil.rectTransform, Vector2.zero, Vector2.one);

		Image frame = CreateImage("Shop Outer Frame", root.transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(frame);
		SetRect(frame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));

		Image titlePlaque = CreateImage("Shop Title Plaque", root.transform, Color.white);
		titlePlaque.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		titlePlaque.type = Image.Type.Sliced;
		SetRect(titlePlaque.rectTransform, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.9f));

		Text title = CreateText("Shop Title", titlePlaque.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont, 48, FontStyle.Normal, TextAnchor.MiddleCenter);
		SetLocalizedText(title, GameTextKeys.Merchant.ShopTitle, "NEGOZIO");
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		title.color = ShopGold;
		SetRect(title.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		title.rectTransform.offsetMin = new Vector2(0f, -23f);
		title.rectTransform.offsetMax = new Vector2(0f, -23f);

		shopStatusText = CreateText("Shop Status", root.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
		shopStatusText.color = ShopBody;
		SetRect(shopStatusText.rectTransform, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.785f));

		shopOffersRoot = CreateShopSection(root.transform, fallbackFont, GameText.GetOrFallbackSilent(GameTextKeys.Merchant.ShopOffers, "OFFERTE DEL MERCANTE"),
			new Vector2(0.04f, 0.465f), new Vector2(0.96f, 0.715f));
		CreateShopScrollSection(root.transform, fallbackFont,
			new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.455f));

		Image emptyPanel = CreateImage("Shop Empty Panel", root.transform, Color.clear);
		shopEmptyState = emptyPanel.gameObject;
		SetRect(emptyPanel.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));
		emptyPanel.rectTransform.offsetMin = new Vector2(0f, 0.0001f);
		emptyPanel.rectTransform.offsetMax = new Vector2(0f, -22.0389f);
		// Il pannello riempie la cornice, ma resta immediatamente sotto di essa:
		// testo e CTA sono figli del pannello, l'ornamento dorato rimane in primo piano.
		emptyPanel.transform.SetSiblingIndex(frame.transform.GetSiblingIndex());
		shopEmptyText = CreateText("Shop Empty Copy", emptyPanel.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont, 40, FontStyle.Normal, TextAnchor.MiddleCenter);
		shopEmptyText.fontSize = 40;
		shopEmptyText.resizeTextForBestFit = false;
		shopEmptyText.resizeTextMinSize = 40;
		shopEmptyText.resizeTextMaxSize = 40;
		shopEmptyText.text =
			"VENDERE ARIA NON È ANCORA UN GRANDE AFFARE.\n\n" +
			"Fai un salto al Santuario e scopri qualche oggetto:\nal resto penserà il mercante.";
		shopEmptyText.color = ShopBody;
		SetLocalizedText(shopEmptyText, GameTextKeys.Merchant.ShopEmptyBody,
			"VENDERE ARIA NON È ANCORA UN GRANDE AFFARE.\n\nFai un salto al Santuario e scopri qualche oggetto:\nal resto penserà il mercante.");
		SetRect(shopEmptyText.rectTransform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.72f));
		shopSanctuaryButton = CreateButton("Shop Go Sanctuary", emptyPanel.transform, fallbackFont, "VAI AL SANTUARIO");
		SetLocalizedButtonLabel(shopSanctuaryButton, GameTextKeys.Merchant.ShopGoSanctuary, "VAI AL SANTUARIO");
		ApplyShopCampaignCta(shopSanctuaryButton);
		shopSanctuaryButton.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideShop(openHub: false);
			ShowSanctuary();
			SelectSanctuaryAltar(SanctuaryAltar.Relics);
		});
		// Larghezza ridotta del 10% rispetto alla CTA della bisaccia; altezza aumentata del 10%.
		SetRect((RectTransform)shopSanctuaryButton.transform, new Vector2(0.194f, 0.140425f), new Vector2(0.806f, 0.224575f));

		Button bag = CreateButton("Shop Prepare Bag", emptyPanel.transform, fallbackFont, "PREPARA LA BISACCIA");
		SetLocalizedButtonLabel(bag, GameTextKeys.Merchant.ShopPrepareBag, "PREPARA LA BISACCIA");
		ApplyRankedPurpleCtaWithoutEffects(bag);
		Text bagLabel = bag.GetComponentInChildren<Text>();
		if ((Object)(object)bagLabel != (Object)null)
		{
		bagLabel.text = GameText.GetOrFallbackSilent(GameTextKeys.Merchant.ShopPrepareBag, "PREPARA LA BISACCIA");
			bagLabel.color = Color.white;
		}
		bag.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideShop(openHub: false);
			ShowSanctuary();
			SelectSanctuaryAltar(SanctuaryAltar.Relics);
		});
		SetRect((RectTransform)bag.transform, new Vector2(0.16f, 0.045f), new Vector2(0.84f, 0.13f));

		CreateShopPurchaseConfirmation(root.transform, fallbackFont);

		shopPanel.SetActive(false);
	}

	private void CreateShopPurchaseConfirmation(Transform parent, Font fallbackFont)
	{
		Image overlay = CreateImage("Shop Purchase Confirmation", parent, new Color(0f, 0f, 0f, 0.82f));
		shopPurchaseConfirmation = overlay.gameObject;
		overlay.raycastTarget = true;
		SetRect(overlay.rectTransform, Vector2.zero, Vector2.one);

		Image dialog = CreateImage("Shop Purchase Dialog", overlay.transform, Color.white);
		dialog.sprite = LoadSpriteResource("UI/Sanctuary/santuary_items");
		dialog.type = Image.Type.Simple;
		dialog.preserveAspect = false;
		SetRect(dialog.rectTransform, new Vector2(0.18f, 0.27f), new Vector2(0.82f, 0.7f));

		Text title = CreateText("Shop Purchase Title", dialog.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont, 30,
			FontStyle.Normal, TextAnchor.MiddleCenter);
		SetLocalizedText(title, GameTextKeys.Merchant.ShopConfirmPurchase, "CONFERMA ACQUISTO");
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		title.color = ShopGold;
		SetRect(title.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.94f));

		shopPurchaseConfirmationText = CreateText("Shop Purchase Copy", dialog.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont, 22,
			FontStyle.Normal, TextAnchor.MiddleCenter);
		shopPurchaseConfirmationText.color = ShopBody;
		SetRect(shopPurchaseConfirmationText.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.72f));

		Button cancel = CreateButton("Shop Purchase Cancel", dialog.transform, fallbackFont, "ANNULLA");
		ApplyShopCancelCta(cancel);
		Text cancelLabel = cancel.GetComponentInChildren<Text>();
		if ((Object)(object)cancelLabel != (Object)null)
		{
			cancelLabel.fontSize = 26;
			cancelLabel.resizeTextForBestFit = false;
			SetRect(cancelLabel.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
		}
		cancel.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideShopPurchaseConfirmation();
		});
		SetRect((RectTransform)cancel.transform, new Vector2(0.10f, 0.08f), new Vector2(0.47f, 0.27f));

		shopPurchaseConfirmButton = CreateButton("Shop Purchase Confirm", dialog.transform, fallbackFont, "CONFERMA");
		ApplyShopCampaignCta(shopPurchaseConfirmButton);
		Text confirmLabel = shopPurchaseConfirmButton.GetComponentInChildren<Text>();
		if ((Object)(object)confirmLabel != (Object)null)
		{
			confirmLabel.fontSize = 26;
			confirmLabel.resizeTextForBestFit = false;
			SetRect(confirmLabel.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
		}
		shopPurchaseConfirmButton.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ConfirmShopPurchase();
		});
		SetRect((RectTransform)shopPurchaseConfirmButton.transform, new Vector2(0.53f, 0.08f), new Vector2(0.90f, 0.27f));

		shopPurchaseConfirmation.SetActive(false);
	}

	private static void ApplyShopCampaignCta(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;
		Image image = button.GetComponent<Image>();
		Sprite sprite = LoadSpriteResource("UI/CampaignRestyle/campaign_cta_blue");
		if ((Object)(object)image != (Object)null && (Object)(object)sprite != (Object)null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Simple;
			image.preserveAspect = false;
			image.color = Color.white;
			button.targetGraphic = image;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(0.86f, 0.96f, 1f, 1f);
			colors.pressedColor = new Color(0.48f, 0.72f, 0.9f, 1f);
			button.colors = colors;
		}
		Text label = button.GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
			label.fontSize = 28;
			label.resizeTextMaxSize = 28;
			label.color = Color.white;
			SetRect(label.rectTransform, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f));
		}
		AccardND.PvpUi.PvpUiVfx.CreatePulseButton(
			(RectTransform)button.transform, new Color(0.18f, 0.72f, 1f, 1f));
	}

	private static void ApplyShopCancelCta(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = button.GetComponent<Image>();
		Sprite sprite = LoadSpriteResource("UI/CampaignRestyle/campaign_cta_back_red");
		if ((Object)(object)image != (Object)null && (Object)(object)sprite != (Object)null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Simple;
			image.preserveAspect = false;
			image.color = Color.white;
			button.targetGraphic = image;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1f, 0.9f, 0.9f, 1f);
			colors.pressedColor = new Color(0.82f, 0.58f, 0.58f, 1f);
			button.colors = colors;
		}

		Text label = button.GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
			label.fontSize = 28;
			label.resizeTextMaxSize = 28;
			label.color = Color.white;
			SetRect(label.rectTransform, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f));
		}
	}

	private RectTransform CreateShopSection(
		Transform parent, Font font, string heading, Vector2 minimum, Vector2 maximum)
	{
		GameObject panel = new("Shop " + heading, typeof(RectTransform));
		panel.transform.SetParent(parent, false);
		RectTransform panelRect = (RectTransform)panel.transform;
		SetRect(panelRect, minimum, maximum);
		Text label = CreateText(heading + " Label", panel.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? font, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
		label.text = heading;
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
		label.fontSize = 40;
		label.resizeTextForBestFit = false;
		label.resizeTextMinSize = 40;
		label.resizeTextMaxSize = 40;
		label.color = ShopGold;
		// Tieni entrambe le intestazioni dentro la cornice di pietra. Quella delle
		// offerte va leggermente piu' in basso per compensare il bordo superiore.
		bool isOffersHeading = heading.StartsWith("OFFERTE");
		if (isOffersHeading)
		{
			label.rectTransform.anchorMin = new Vector2(0.035f, 0.79f);
			label.rectTransform.anchorMax = new Vector2(0.965f, 0.95f);
			label.rectTransform.offsetMin = new Vector2(0f, 30f);
			label.rectTransform.offsetMax = new Vector2(0f, 30f);
		}
		else
		{
			SetRect(label.rectTransform, new Vector2(0.035f, 0.83f), new Vector2(0.965f, 0.99f));
		}
		Image content = CreateImage(heading + " Content", panel.transform, Color.clear);
		SetRect(content.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.84f));
		return content.rectTransform;
	}

	/// <summary>
	/// Meta' inferiore del negozio: catalogo e premium scorrono insieme dentro un solo
	/// viewport, cosi' le sezioni possono crescere senza schiacciare i frame degli item.
	/// </summary>
	private void CreateShopScrollSection(Transform parent, Font font, Vector2 minimum, Vector2 maximum)
	{
		shopScrollViewport = CreateImage("Shop Scroll Viewport", parent, Color.clear);
		shopScrollSection = ((Component)shopScrollViewport).gameObject;
		shopScrollViewport.raycastTarget = true;
		SetRect(shopScrollViewport.rectTransform, minimum, maximum);
		shopScrollSection.AddComponent<RectMask2D>();

		shopScrollRect = shopScrollSection.AddComponent<ScrollRect>();
		shopScrollRect.viewport = shopScrollViewport.rectTransform;
		shopScrollRect.horizontal = false;
		shopScrollRect.vertical = true;
		shopScrollRect.inertia = true;
		shopScrollRect.decelerationRate = 0.16f;
		shopScrollRect.scrollSensitivity = 42f;
		shopScrollRect.movementType = ScrollRect.MovementType.Clamped;

		shopScrollContent = new GameObject("Shop Scroll Content", typeof(RectTransform))
			.GetComponent<RectTransform>();
		shopScrollContent.SetParent(shopScrollViewport.transform, false);
		shopScrollContent.anchorMin = new Vector2(0f, 1f);
		shopScrollContent.anchorMax = new Vector2(1f, 1f);
		shopScrollContent.pivot = new Vector2(0.5f, 1f);
		shopScrollContent.anchoredPosition = Vector2.zero;
		shopScrollContent.sizeDelta = Vector2.zero;
		VerticalLayoutGroup column = shopScrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
		column.childAlignment = TextAnchor.UpperCenter;
		column.childControlWidth = true;
		column.childControlHeight = true;
		column.childForceExpandWidth = true;
		column.childForceExpandHeight = false;
		column.spacing = 6f;
		ContentSizeFitter fitter = shopScrollContent.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		shopScrollRect.content = shopScrollContent;

		CreateShopScrollHeading(font, "Shop Catalog Heading",
			GameTextKeys.Merchant.ShopCatalog, "CATALOGO");
		shopCatalogRoot = CreateShopScrollGrid("Shop Catalog Grid");
		CreateShopScrollHeading(font, "Shop Premium Heading",
			GameTextKeys.Merchant.ShopPremium, "PREMIUM");
		shopPremiumRoot = CreateShopScrollGrid("Shop Premium Grid");
	}

	private void CreateShopScrollHeading(Font font, string name, string textKey, string fallback)
	{
		Text heading = CreateText(name, shopScrollContent,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? font, 40, FontStyle.Normal, TextAnchor.MiddleCenter);
		SetLocalizedText(heading, textKey, fallback);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(heading);
		heading.fontSize = 40;
		heading.resizeTextForBestFit = false;
		heading.resizeTextMinSize = 40;
		heading.resizeTextMaxSize = 40;
		heading.color = ShopGold;
		heading.raycastTarget = false;
		LayoutElement element = ((Component)heading).gameObject.AddComponent<LayoutElement>();
		element.minHeight = 52f;
		element.preferredHeight = 52f;
	}

	private RectTransform CreateShopScrollGrid(string name)
	{
		RectTransform grid = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup))
			.GetComponent<RectTransform>();
		grid.SetParent(shopScrollContent, false);
		GridLayoutGroup layout = ((Component)grid).GetComponent<GridLayoutGroup>();
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = ShopGridColumns;
		return grid;
	}

	private void ShowShop()
	{
		if ((Object)(object)shopPanel == (Object)null)
		{
			Debug.LogWarning("[TUTORIAL SHOP] ShowShop annullato: shopPanel nullo.");
			return;
		}
		TutorialFlowState flow = CurrentTutorialFlow();
		Debug.Log($"[TUTORIAL SHOP] ShowShop; moduli={flow.CompletedModules}; "
			+ $"shopTourVisto={flow.ShopTourSeen}; tourAttivo={IsGuidedTourActive}; "
			+ $"tourPendente={TutorialGate.PendingTourSurface(flow)?.ToString() ?? "nessuno"}.");
		if ((Object)(object)modeSelectionPanel != (Object)null)
			modeSelectionPanel.SetActive(false);
		SetAccountHubHudActive(true);
		EnsureSanctuarySharedHudSorting();
		shopPanel.SetActive(true);
		shopPanel.transform.SetAsLastSibling();
		RefreshAccountBannerView();
		RefreshShop();
		LoadShopFromServer();
		_ = SyncEntitlementsAsync();
		TryStartPendingTutorialTour(AccardND.GameData.TutorialSurface.HubShop);
		Debug.Log($"[TUTORIAL SHOP] ShowShop completato; pannelloAttivo={shopPanel.activeInHierarchy}; "
			+ $"tourAttivoDopoTentativo={IsGuidedTourActive}.");
	}

	private void HideShop(bool openHub = true)
	{
		HideShopPurchaseConfirmation();
		if ((Object)(object)shopPanel != (Object)null)
			shopPanel.SetActive(false);
		if (openHub)
			ShowHubFromSinglePlayer();
	}

	private async void LoadShopFromServer()
	{
		if (shopLoading)
			return;
		shopLoading = true;
		SetShopStatus(GameText.GetOrFallbackSilent(
			GameTextKeys.Merchant.ShopLoading,
			"Il mercante sta sistemando gli scaffali..."));
		try
		{
			if (await EnsureServerProgressAsync())
				sanctuaryData = await serverProgress.GetSanctuaryAsync();
			else
				SetShopStatus(AccardND.Network.AccountServerSession.IsReconnecting
					? GameText.GetOrFallbackSilent(
						GameTextKeys.Merchant.ShopReconnecting,
						"Riconnessione in corso: il negozio si aggiornerà automaticamente.")
					: GameText.GetOrFallbackSilent(
						GameTextKeys.Merchant.ShopConnectionRequired,
						"Il negozio è chiuso: serve una connessione al server."));
		}
		catch (Exception exception)
		{
			SetShopStatus(GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopLoadFailed,
				"Il mercante si è preso una pausa: {0}",
				exception.Message));
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopLoadFailedLog,
				"NEGOZIO - caricamento fallito: {0}",
				exception.Message));
		}
		finally
		{
			shopLoading = false;
		}
		RefreshShop();
	}

	private void RefreshShop()
	{
		ClearShopDynamicObjects();
		SanctuaryEntryData[] entries = sanctuaryData?.shopCatalog ?? Array.Empty<SanctuaryEntryData>();
		bool empty = entries.Length == 0;
		shopEmptyState?.SetActive(empty && !shopLoading);
		if ((Object)(object)shopOffersRoot != (Object)null)
			shopOffersRoot.parent.gameObject.SetActive(!empty);
		shopScrollSection?.SetActive(!empty);
		if (empty)
		{
			if (!shopLoading)
				SetShopStatus(string.Empty);
			return;
		}

		SetShopStatus(string.Empty);
		ShopOfferData[] offers = sanctuaryData.shopOffers ?? Array.Empty<ShopOfferData>();
		for (int index = 0; index < offers.Length; index++)
		{
			ShopOfferData offer = offers[index];
			SanctuaryEntryData entry = entries.FirstOrDefault(candidate => candidate.id == offer.itemId);
			if (entry != null)
				CreateShopOfferTile(entry, offer, index, Math.Max(1, offers.Length));
		}
		foreach (SanctuaryEntryData entry in entries)
			CreateShopCatalogTile(entry);
		foreach (AccardND.Iap.IapProduct premium in VisiblePremiumProducts())
			CreateShopPremiumTile(premium);
		RefreshShopLayout();
		if ((Object)(object)shopScrollRect != (Object)null && (Object)(object)shopScrollContent != (Object)null)
		{
			// Ricostruisci prima di riportare la lista in cima: il fitter deve gia'
			// conoscere l'altezza delle nuove celle, altrimenti lo scroll parte storto.
			LayoutRebuilder.ForceRebuildLayoutImmediate(shopScrollContent);
			shopScrollRect.verticalNormalizedPosition = 1f;
		}
	}

	/// <summary>Frame condiviso da offerte, catalogo e premium: e' quello degli item del Santuario.</summary>
	private Image CreateShopFrameTile(RectTransform parent, string name)
	{
		Image tile = CreateImage(name, parent, Color.white);
		tile.sprite = LoadSpriteResource("UI/Sanctuary/santuary_items");
		tile.type = Image.Type.Simple;
		tile.preserveAspect = false;
		shopDynamicObjects.Add(tile.gameObject);
		return tile;
	}

	private void FillShopTile(
		Image tile, string id, Sprite iconSprite, string title, string info, Color infoColor,
		bool interactable, UnityAction onClick)
	{
		Image icon = CreateImage(id + " Icon", tile.transform, Color.white);
		icon.sprite = iconSprite;
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		SetRect(icon.rectTransform, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.94f));

		Text name = CreateText(id + " Name", tile.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont, 25, FontStyle.Normal, TextAnchor.MiddleCenter);
		name.fontSize = 25;
		name.text = title;
		name.color = ShopGold;
		name.raycastTarget = false;
		SetRect(name.rectTransform, new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.43f));

		Text price = CreateText(id + " Price", tile.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont, 25, FontStyle.Bold, TextAnchor.MiddleCenter);
		price.supportRichText = true;
		price.text = info;
		price.color = infoColor;
		price.raycastTarget = false;
		SetRect(price.rectTransform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.27f));

		Button button = tile.gameObject.AddComponent<Button>();
		tile.raycastTarget = true;
		button.targetGraphic = tile;
		button.interactable = interactable;
		button.onClick.AddListener(onClick);
	}

	private void CreateShopOfferTile(SanctuaryEntryData entry, ShopOfferData offer, int index, int columns)
	{
		bool isSingleOffer = columns == 1;
		int row = index / columns;
		int column = index % columns;
		int layoutColumns = isSingleOffer ? ShopGridColumns : columns;
		float width = (1f - ShopOfferGap * (layoutColumns - 1)) / layoutColumns;
		const float height = 1f;
		float top = 1f - row * (height + 0.055f);
		float left = isSingleOffer
			? (1f - width) * 0.5f
			: column * (width + ShopOfferGap);
		Image tile = CreateShopFrameTile(shopOffersRoot, "Shop Item " + entry.id);
		SetRect(tile.rectTransform,
			new Vector2(left, top - height),
			new Vector2(left + width, top));

		int cost = offer.offerCost;
		string price = offer.remaining > 0
			? GameText.GetLocalizedFallback(
				GameTextKeys.Merchant.ShopOfferAvailable,
				"<color=#888888>{0}</color> → {1} MIELE\n-{2}%  •  {3} RIMASTI",
				"<color=#888888>{0}</color> → {1} HONEY\n-{2}%  •  {3} LEFT",
				offer.regularCost, cost, offer.discountPercent, offer.remaining)
			: GameText.GetLocalizedFallback(GameTextKeys.Merchant.ShopOfferSoldOut, "ESAURITO", "SOLD OUT");
		string offerId = offer.offerId;
		FillShopTile(tile, entry.id, ShopItemSprite(entry.id), ShopItemName(entry).ToUpperInvariant(), price,
			offer.remaining > 0 ? new Color(0.55f, 0.9f, 0.62f) : ShopBody,
			!shopPurchasing && cost > 0 && offer.remaining > 0
				&& IsTutorialSurfaceOpen(AccardND.GameData.TutorialSurface.ShopOffers),
			(UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				ShowShopPurchaseConfirmation(entry, offerId, cost);
			});
	}

	private void CreateShopCatalogTile(SanctuaryEntryData entry)
	{
		Image tile = CreateShopFrameTile(shopCatalogRoot, "Shop Item " + entry.id);
		int owned = sanctuaryData.stash?.FirstOrDefault(item => item.itemId == entry.id)?.count ?? 0;
		int cost = entry.copyCost;
		FillShopTile(tile, entry.id, ShopItemSprite(entry.id), ShopItemName(entry).ToUpperInvariant(),
			GameText.GetLocalizedFallback(
				GameTextKeys.Merchant.ShopStockPrice,
				"SCORTA {0}  •  {1} MIELE",
				"STASH {0}  •  {1} HONEY",
				owned, cost), ShopBody,
			!shopPurchasing && cost > 0 && IsTutorialSurfaceOpen(AccardND.GameData.TutorialSurface.ShopCatalog),
			(UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				ShowShopPurchaseConfirmation(entry, null, cost);
			});
	}

	private void CreateShopPremiumTile(AccardND.Iap.IapProduct premium)
	{
		string id = AccardND.Iap.IapProducts.IdOf(premium);
		Image tile = CreateShopFrameTile(shopPremiumRoot, "Shop Premium " + id);
		string info = PremiumInfoLine(premium, out Color infoColor, out bool interactable);
		FillShopTile(tile, id, LoadSpriteResource(PremiumIconResource(premium)), PremiumTitle(premium),
			info, infoColor, interactable,
			(UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				BuyPremium(premium);
			});
	}

	/// <summary>
	/// Allinea le celle di catalogo e premium alle proporzioni del frame usato dalle
	/// offerte, cosi' le tre sezioni mostrano lo stesso identico riquadro.
	/// </summary>
	private void RefreshShopLayout()
	{
		if ((Object)(object)shopScrollViewport == (Object)null
			|| (Object)(object)shopPanel == (Object)null
			|| !shopPanel.activeInHierarchy)
			return;
		Canvas.ForceUpdateCanvases();
		float viewportWidth = Mathf.Max(1f, shopScrollViewport.rectTransform.rect.width);
		float spacing = Mathf.Max(6f, viewportWidth * ShopOfferGap);
		int padding = Mathf.RoundToInt(Mathf.Max(4f, viewportWidth * 0.012f));
		float cellWidth = Mathf.Max(
			1f,
			(viewportWidth - padding * 2f - spacing * (ShopGridColumns - 1)) / ShopGridColumns);
		Vector2 cellSize = new(cellWidth, cellWidth * ShopTileHeightRatio());
		ApplyShopGridLayout(shopCatalogRoot, cellSize, spacing, padding);
		ApplyShopGridLayout(shopPremiumRoot, cellSize, spacing, padding);
	}

	private static void ApplyShopGridLayout(RectTransform grid, Vector2 cellSize, float spacing, int padding)
	{
		if ((Object)(object)grid == (Object)null)
			return;
		GridLayoutGroup layout = ((Component)grid).GetComponent<GridLayoutGroup>();
		if ((Object)(object)layout == (Object)null)
			return;
		layout.constraintCount = ShopGridColumns;
		layout.spacing = new Vector2(spacing, spacing);
		layout.cellSize = cellSize;
		layout.padding = new RectOffset(padding, padding, padding, padding);
	}

	private float ShopTileHeightRatio()
	{
		if ((Object)(object)shopOffersRoot != (Object)null)
		{
			Rect offers = shopOffersRoot.rect;
			float offerWidth = (offers.width - offers.width * ShopOfferGap * (ShopGridColumns - 1)) / ShopGridColumns;
			if (offerWidth > 1f && offers.height > 1f)
				return Mathf.Clamp(offers.height / offerWidth, 0.35f, 1.6f);
		}
		return 0.75f;
	}

	private void ShowShopPurchaseConfirmation(SanctuaryEntryData entry, string offerId, int cost)
	{
		if (shopPurchasing || entry == null || (Object)(object)shopPurchaseConfirmation == (Object)null)
			return;

		pendingShopPurchaseEntry = entry;
		pendingShopPurchaseOfferId = offerId;
		int currentHoney = sanctuaryData != null ? sanctuaryData.honey : singlePlayerProgressService.Honey;
		int remainingHoney = Mathf.Max(0, currentHoney - cost);
		string itemName = ShopItemName(entry);
		string itemDescription = ShopItemDescription(entry);
		shopPurchaseConfirmationText.text = GameText.GetLocalizedFallback(
			"shop.confirm.body",
			"Vuoi comprare {0} per {1:n0} miele?\n\nEffetto: {2}\n\nSaldo dopo l'acquisto: {3:n0}",
			"Do you want to buy {0} for {1:n0} honey?\n\nEffect: {2}\n\nBalance after purchase: {3:n0}",
			itemName, cost, itemDescription, remainingHoney);
		shopPurchaseConfirmButton.interactable = currentHoney >= cost;
		shopPurchaseConfirmation.SetActive(true);
		shopPurchaseConfirmation.transform.SetAsLastSibling();
	}

	private void HideShopPurchaseConfirmation()
	{
		pendingShopPurchaseEntry = null;
		pendingShopPurchaseOfferId = null;
		if ((Object)(object)shopPurchaseConfirmation != (Object)null)
			shopPurchaseConfirmation.SetActive(false);
	}

	private void ConfirmShopPurchase()
	{
		SanctuaryEntryData entry = pendingShopPurchaseEntry;
		string offerId = pendingShopPurchaseOfferId;
		HideShopPurchaseConfirmation();
		BuyShopItem(entry, offerId);
	}

	private async void BuyShopItem(SanctuaryEntryData entry, string offerId)
	{
		if (shopPurchasing || entry == null)
			return;
		shopPurchasing = true;
		RefreshShop();
		SetShopStatus(GameText.GetLocalizedFallback(
			GameTextKeys.Merchant.ShopPreparingPurchase,
			"Il mercante prepara {0}...",
			"The merchant is preparing {0}...",
			ShopItemName(entry)));
		string finalStatus;
		try
		{
			sanctuaryData = await serverProgress.BuySanctuaryItemAsync(entry.id, offerId);
			SyncShopHoneyToHud();
			finalStatus = GameText.GetLocalizedFallback(
				"shop.message.purchase_success",
				"Affare fatto! {0} è nella tua scorta.",
				"Deal complete! {0} is now in your stash.",
				ShopItemName(entry));
			RefreshAccountBannerView();
		}
		catch (Exception exception)
		{
			finalStatus = GameText.GetLocalizedFallback(
				"shop.message.purchase_failed",
				"Affare saltato: {0}", "Purchase failed: {0}", exception.Message);
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopPurchaseFailedLog,
				"NEGOZIO - acquisto fallito: {0}",
				exception.Message));
		}
		finally
		{
			shopPurchasing = false;
		}
		RefreshShop();
		SetShopStatus(finalStatus);
	}

	private void SyncShopHoneyToHud()
	{
		if (sanctuaryData == null)
			return;

		SinglePlayerProgressSave localSnapshot = JsonUtility.FromJson<SinglePlayerProgressSave>(
			JsonUtility.ToJson(singlePlayerProgressService.Progress));
		localSnapshot.honey = Mathf.Max(0, sanctuaryData.honey);
		singlePlayerProgressService.ApplyAuthoritative(localSnapshot);

		if (serverProgress != null)
		{
			SinglePlayerProgressSave serverSnapshot = JsonUtility.FromJson<SinglePlayerProgressSave>(
				JsonUtility.ToJson(serverProgress.Progress));
			serverSnapshot.honey = localSnapshot.honey;
			serverProgress.ApplyAuthoritative(serverSnapshot);
		}
	}

	private static string ShopConsumableLocalizationId(string itemId) => itemId switch
	{
		"double-exp" => "double_experience",
		"sigillo-rubino" => "ruby_seal",
		"second-chance" => "second_chance",
		"mana-5" => "mana_5",
		"mana-10" => "mana_10",
		_ => itemId
	};

	private static string ShopItemName(SanctuaryEntryData entry)
	{
		if (entry == null)
			return string.Empty;
		string english = entry.id switch
		{
			"detector" => "Detector",
			"double-exp" => "Double EXP",
			"empower" => "Empower",
			"sigillo-rubino" => "Ruby Seal",
			"second-chance" => "Second Chance",
			"mana-5" => "Mana +5",
			"mana-10" => "Mana +10",
			"jolly" => "Wild Card",
			_ => entry.name
		};
		return GameText.GetLocalizedFallback(
			GameTextKeys.Consumables.Name(ShopConsumableLocalizationId(entry.id)),
			entry.name, english);
	}

	private static string ShopItemDescription(SanctuaryEntryData entry)
	{
		if (entry == null)
			return string.Empty;
		string italian = string.IsNullOrWhiteSpace(entry.description) ? entry.name : entry.description.Trim();
		string english = entry.id switch
		{
			"detector" => "Reveals the contents of all three doors at the next path choice.",
			"double-exp" => "Doubles all experience earned in the next room.",
			"empower" => "Increases your attack Vigor die by one step for the current or next room. Cannot be used in Boss or Miniboss rooms.",
			"sigillo-rubino" => "Permanently grants +2 Power to a deployed pawn. Each card can receive only one Ruby Seal.",
			"second-chance" => "Revives every card in the graveyard and returns it to the deck. Cannot be used during battle.",
			"mana-5" => "Restores 5 mana.",
			"mana-10" => "Restores 10 mana.",
			"jolly" => "A campaign consumable with a flexible effect.",
			_ => italian
		};
		return GameText.GetLocalizedFallback(
			GameTextKeys.Consumables.Description(ShopConsumableLocalizationId(entry.id)),
			italian, english);
	}

	private Sprite ShopItemSprite(string itemId)
	{
		return itemId switch
		{
			"detector" => LoadSpriteResource("UI/detector_item"),
			"double-exp" => LoadSpriteResource("UI/double_exp_item"),
			"empower" => LoadSpriteResource("UI/empower_item"),
			"sigillo-rubino" => LoadSpriteResource("UI/ruby_seal_item"),
			"second-chance" => LoadSpriteResource("UI/second_chance_item"),
			"mana-5" => LoadSpriteResource("UI/mana_gain_5_item"),
			"mana-10" => LoadSpriteResource("UI/mana_gain_10_item"),
			"jolly" => LoadSpriteResource("UI/jolly_item"),
			_ => LoadSpriteResource("UI/info_button")
		};
	}

	private void SetShopStatus(string value)
	{
		if ((Object)(object)shopStatusText != (Object)null)
			shopStatusText.text = value ?? string.Empty;
	}

	private void ClearShopDynamicObjects()
	{
		foreach (GameObject item in shopDynamicObjects)
			if ((Object)(object)item != (Object)null)
				Object.Destroy(item);
		shopDynamicObjects.Clear();
	}
}
}
