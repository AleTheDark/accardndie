using System;
using System.Collections.Generic;
using System.Linq;
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
	private GameObject shopEmptyState;
	private Text shopEmptyText;
	private Button shopSanctuaryButton;
	private readonly List<GameObject> shopDynamicObjects = new();
	private bool shopLoading;
	private bool shopPurchasing;

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

		Image backdrop = CreateImage("Shop Backdrop", root.transform, new Color(0.015f, 0.012f, 0.02f, 1f));
		backdrop.sprite = LoadSpriteResource("UI/Shop/shop_background");
		backdrop.type = Image.Type.Simple;
		backdrop.preserveAspect = true;
		backdrop.color = Color.white;
		SetRect(backdrop.rectTransform, Vector2.zero, Vector2.one);
		if ((Object)(object)backdrop.sprite != (Object)null)
		{
			AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
			fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			fitter.aspectRatio = backdrop.sprite.rect.width / Mathf.Max(1f, backdrop.sprite.rect.height);
		}

		Image veil = CreateImage("Shop Veil", root.transform, new Color(0f, 0f, 0.01f, 0.18f));
		SetRect(veil.rectTransform, new Vector2(0.018f, 0.018f), new Vector2(0.982f, 0.785f));

		Image frame = CreateImage("Shop Outer Frame", root.transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(frame);
		SetRect(frame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));

		Image titlePlaque = CreateImage("Shop Title Plaque", root.transform, Color.white);
		titlePlaque.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		titlePlaque.type = Image.Type.Sliced;
		SetRect(titlePlaque.rectTransform, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.9f));

		Text title = CreateText("Shop Title", titlePlaque.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont, 48, FontStyle.Normal, TextAnchor.MiddleCenter);
		title.text = "NEGOZIO";
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		title.color = ShopGold;
		SetRect(title.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		title.rectTransform.offsetMin = new Vector2(0f, -23f);
		title.rectTransform.offsetMax = new Vector2(0f, -23f);

		shopStatusText = CreateText("Shop Status", root.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont ?? fallbackFont, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
		shopStatusText.color = ShopBody;
		SetRect(shopStatusText.rectTransform, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.785f));

		shopOffersRoot = CreateShopSection(root.transform, fallbackFont, "OFFERTE DEL MERCANTE",
			new Vector2(0.055f, 0.465f), new Vector2(0.945f, 0.715f));
		shopCatalogRoot = CreateShopSection(root.transform, fallbackFont, "CATALOGO",
			new Vector2(0.055f, 0.125f), new Vector2(0.945f, 0.445f));

		Image emptyPanel = CreateImage("Shop Empty Rock Panel", root.transform, Color.white);
		shopEmptyState = emptyPanel.gameObject;
		emptyPanel.sprite = LoadSpriteResource("UI/Common/merchant_rock_panel_aaa");
		emptyPanel.type = Image.Type.Sliced;
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
		SetRect(shopEmptyText.rectTransform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.72f));
		shopSanctuaryButton = CreateButton("Shop Go Sanctuary", emptyPanel.transform, fallbackFont, "VAI AL SANTUARIO");
		ApplyRankedPurpleCtaWithoutEffects(shopSanctuaryButton);
		shopSanctuaryButton.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideShop(openHub: false);
			ShowSanctuary();
			SelectSanctuaryAltar(SanctuaryAltar.Relics);
		});
		// Stesso formato della CTA "Prepara la bisaccia", ridotto del 10%.
		SetRect((RectTransform)shopSanctuaryButton.transform, new Vector2(0.194f, 0.14425f), new Vector2(0.806f, 0.22075f));

		Button bag = CreateButton("Shop Prepare Bag", emptyPanel.transform, fallbackFont, "PREPARA LA BISACCIA");
		ApplyRankedPurpleCtaWithoutEffects(bag);
		Text bagLabel = bag.GetComponentInChildren<Text>();
		if ((Object)(object)bagLabel != (Object)null)
		{
			bagLabel.text = "PREPARA LA BISACCIA";
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

		shopPanel.SetActive(false);
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

	private RectTransform CreateShopSection(
		Transform parent, Font font, string heading, Vector2 minimum, Vector2 maximum)
	{
		Image panel = CreateImage("Shop " + heading, parent, Color.white);
		panel.sprite = LoadSpriteResource(heading.StartsWith("OFFERTE")
			? "UI/Common/merchant_offer_rock_panel_aaa"
			: "UI/Common/merchant_rock_panel_aaa");
		panel.type = Image.Type.Sliced;
		SetRect(panel.rectTransform, minimum, maximum);
		Text label = CreateText(heading + " Label", panel.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? font, 20, FontStyle.Normal, TextAnchor.MiddleLeft);
		label.text = heading;
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
		label.color = ShopGold;
		SetRect(label.rectTransform, new Vector2(0.035f, 0.86f), new Vector2(0.96f, 1.02f));
		Image content = CreateImage(heading + " Content", panel.transform, Color.clear);
		SetRect(content.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.84f));
		return content.rectTransform;
	}

	private void ShowShop()
	{
		if ((Object)(object)shopPanel == (Object)null)
			return;
		if ((Object)(object)modeSelectionPanel != (Object)null)
			modeSelectionPanel.SetActive(false);
		SetAccountHubHudActive(true);
		EnsureSanctuarySharedHudSorting();
		shopPanel.SetActive(true);
		shopPanel.transform.SetAsLastSibling();
		RefreshAccountBannerView();
		RefreshShop();
		LoadShopFromServer();
	}

	private void HideShop(bool openHub = true)
	{
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
		SetShopStatus("Il mercante sta sistemando gli scaffali...");
		try
		{
			if (await EnsureServerProgressAsync())
				sanctuaryData = await serverProgress.GetSanctuaryAsync();
			else
				SetShopStatus(AccardND.Network.AccountServerSession.IsReconnecting
					? "Riconnessione in corso: il negozio si aggiornerà automaticamente."
					: "Il negozio è chiuso: serve una connessione al server.");
		}
		catch (Exception exception)
		{
			SetShopStatus("Il mercante si è preso una pausa: " + exception.Message);
			AppendLog("NEGOZIO - caricamento fallito: " + exception.Message);
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
		SanctuaryEntryData[] entries = sanctuaryData?.entries?
			.Where(entry => entry.type == "item" && entry.owned).ToArray() ?? Array.Empty<SanctuaryEntryData>();
		bool empty = entries.Length == 0;
		shopEmptyState?.SetActive(empty && !shopLoading);
		if ((Object)(object)shopOffersRoot != (Object)null)
			shopOffersRoot.parent.gameObject.SetActive(!empty);
		if ((Object)(object)shopCatalogRoot != (Object)null)
			shopCatalogRoot.parent.gameObject.SetActive(!empty);
		if (empty)
		{
			if (!shopLoading)
				SetShopStatus(string.Empty);
			return;
		}

		SetShopStatus($"Miele disponibile: {sanctuaryData.honey:n0}  •  Prezzi bassi e quantità ancora più basse!");
		ShopOfferData[] offers = sanctuaryData.shopOffers ?? Array.Empty<ShopOfferData>();
		for (int index = 0; index < offers.Length; index++)
		{
			ShopOfferData offer = offers[index];
			SanctuaryEntryData entry = entries.FirstOrDefault(candidate => candidate.id == offer.itemId);
			if (entry != null)
				CreateShopTile(shopOffersRoot, entry, offer, index, Math.Max(1, offers.Length));
		}
		for (int index = 0; index < entries.Length; index++)
			CreateShopTile(shopCatalogRoot, entries[index], null, index, 3);
	}

	private void CreateShopTile(
		RectTransform parent, SanctuaryEntryData entry, ShopOfferData offer, int index, int columns)
	{
		int row = index / columns;
		int column = index % columns;
		float gap = 0.018f;
		float width = (1f - gap * (columns - 1)) / columns;
		float height = parent == shopOffersRoot ? 1f : 0.47f;
		float top = 1f - row * (height + 0.055f);
		Image tile = CreateImage("Shop Item " + entry.id, parent, new Color(0.025f, 0.035f, 0.045f, 0.96f));
		tile.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
		tile.type = Image.Type.Sliced;
		SetRect(tile.rectTransform,
			new Vector2(column * (width + gap), top - height),
			new Vector2(column * (width + gap) + width, top));
		shopDynamicObjects.Add(tile.gameObject);

		Image icon = CreateImage(entry.id + " Icon", tile.transform, Color.white);
		icon.sprite = ShopItemSprite(entry.id);
		icon.preserveAspect = true;
		SetRect(icon.rectTransform, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.94f));

		Text name = CreateText(entry.id + " Name", tile.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
		name.text = entry.name.ToUpperInvariant();
		name.color = ShopGold;
		SetRect(name.rectTransform, new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.43f));

		int owned = sanctuaryData.stash?.FirstOrDefault(item => item.itemId == entry.id)?.count ?? 0;
		int cost = offer?.offerCost ?? entry.copyCost;
		string price = offer == null
			? $"SCORTA {owned}  •  {cost} MIELE"
			: offer.remaining > 0
				? $"<color=#888888>{offer.regularCost}</color> → {cost} MIELE\n-{offer.discountPercent}%  •  {offer.remaining} RIMASTI"
				: "ESAURITO";
		Text info = CreateText(entry.id + " Price", tile.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
		info.supportRichText = true;
		info.text = price;
		info.color = offer != null && offer.remaining > 0 ? new Color(0.55f, 0.9f, 0.62f) : ShopBody;
		SetRect(info.rectTransform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.27f));

		Button button = tile.gameObject.AddComponent<Button>();
		button.targetGraphic = tile;
		bool available = !shopPurchasing && cost > 0 && sanctuaryData.honey >= cost && (offer == null || offer.remaining > 0);
		button.interactable = available;
		string offerId = offer?.offerId;
		button.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			BuyShopItem(entry, offerId);
		});
	}

	private async void BuyShopItem(SanctuaryEntryData entry, string offerId)
	{
		if (shopPurchasing || entry == null)
			return;
		shopPurchasing = true;
		SetShopStatus($"Il mercante prepara {entry.name}...");
		RefreshShop();
		try
		{
			sanctuaryData = await serverProgress.BuySanctuaryItemAsync(entry.id, offerId);
			SetShopStatus($"Affare fatto! {entry.name} è nella tua scorta.");
			RefreshAccountBannerView();
		}
		catch (Exception exception)
		{
			SetShopStatus("Affare saltato: " + exception.Message);
			AppendLog("NEGOZIO - acquisto fallito: " + exception.Message);
		}
		finally
		{
			shopPurchasing = false;
		}
		RefreshShop();
	}

	private Sprite ShopItemSprite(string itemId)
	{
		return itemId switch
		{
			"detector" => LoadSpriteResource("UI/detector_item"),
			"defrost" => LoadSpriteResource("UI/defrost_item"),
			"double-exp" => LoadSpriteResource("UI/double_exp_item"),
			"empower" => LoadSpriteResource("UI/empower_item"),
			"sigillo-rubino" => LoadSpriteResource("UI/ruby_seal_item"),
			"second-chance" => LoadSpriteResource("UI/second_chance_item"),
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
