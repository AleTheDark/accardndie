using System;
using System.Collections.Generic;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const float MerchantCardSize = 132f;

	private void CreateMerchantView(Transform canvasTransform, Font font)
	{
		GameObject merchantPanelObject = new GameObject("Merchant Panel", typeof(RectTransform));
		merchantPanelObject.transform.SetParent(canvasTransform, false);
		RectTransform image = (RectTransform)merchantPanelObject.transform;
		merchantPanel = ((Component)image).gameObject;
		SetRect(image, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.97f));
		Canvas obj = merchantPanel.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 420;
		merchantPanel.AddComponent<GraphicRaycaster>();

		Image innerFrame = CreateImage(
			"Screen Inner Background",
			((Component)image).transform,
			new Color(0f, 0f, 0f, 1f));
		innerFrame.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
		innerFrame.type = Image.Type.Sliced;
		innerFrame.raycastTarget = true;
		SetRect(innerFrame.rectTransform, new Vector2(0.018f, 0.018f), new Vector2(0.982f, 0.978f));

		Image outerFrame = CreateImage("Merchant Outer Frame", ((Component)image).transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(outerFrame);
		outerFrame.color = new Color(1f, 1f, 1f, 0.96f);
		SetRect(outerFrame.rectTransform, new Vector2(0.004f, 0.004f), new Vector2(0.996f, 0.996f));

		Image titlePanel = CreateImage("Merchant Title Panel", ((Component)image).transform, Color.white);
		titlePanel.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		titlePanel.type = Image.Type.Simple;
		titlePanel.preserveAspect = false;
		titlePanel.raycastTarget = false;
		SetRect(titlePanel.rectTransform, new Vector2(0.14f, 0.895f), new Vector2(0.86f, 0.985f));

		Text text = CreateText("Merchant Title", ((Component)image).transform, font, 38, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(text);
		text.text = "MERCATO";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(text.rectTransform, new Vector2(0.19f, 0.905f), new Vector2(0.81f, 0.975f));
		text.rectTransform.offsetMin = new Vector2(0f, -29f);
		text.rectTransform.offsetMax = new Vector2(0f, -29f);

		Image experienceIcon = CreateImage("Merchant Experience Icon", ((Component)image).transform, Color.white);
		experienceIcon.sprite = LoadSpriteResource("UI/Common/merchant_experience_icon");
		experienceIcon.preserveAspect = true;
		experienceIcon.raycastTarget = false;
		SetRect(experienceIcon.rectTransform, new Vector2(0.065f, 0.795f), new Vector2(0.19f, 0.9f));

		merchantStatusText = CreateText("Merchant Status", ((Component)image).transform, font, 22, FontStyle.Normal, TextAnchor.MiddleLeft);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(merchantStatusText);
		merchantStatusText.color = new Color(0.86f, 0.94f, 0.96f);
		SetRect(merchantStatusText.rectTransform, new Vector2(0.18f, 0.805f), new Vector2(0.52f, 0.89f));
		Button button = CreateImageButton("Close Merchant", ((Component)image).transform, font, cancelActionSprite, string.Empty);
		((UnityEvent)button.onClick).AddListener(new UnityAction(CloseMerchantPanel));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.875f, 0.905f), new Vector2(0.955f, 0.975f));

		merchantCardsTabButton = CreateButton("Merchant Tab Cards", ((Component)image).transform, font, "CARTE");
		((UnityEvent)merchantCardsTabButton.onClick).AddListener((UnityAction)delegate
		{
			SelectMerchantBranch(MerchantBranch.Cards);
		});
		merchantCardsTabText = ((Component)merchantCardsTabButton).GetComponentInChildren<Text>();
		ApplyMerchantCampaignCta(merchantCardsTabButton, "UI/CampaignRestyle/campaign_cta_blue");
		merchantCardsTabLockImage = CreateMerchantLockIcon(merchantCardsTabButton, "Merchant Cards Lock");
		CreateMerchantTabIcon(merchantCardsTabButton, "Merchant Cards Icon", "UI/deck_icon");
		merchantCardsTabText.fontSize = 30;
		merchantCardsTabText.resizeTextMaxSize = 30;
		SetRect(merchantCardsTabText.rectTransform, new Vector2(0.25f, 0.06f), new Vector2(0.86f, 0.94f));
		merchantCardsTabVfx = AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)merchantCardsTabButton).transform,
			new Color(0.48f, 0.68f, 0.16f, 1f));
		merchantCardsTabVfx.SetSweepScale(new Vector3(0.3167f, 1f, 1f));
		SetRect((RectTransform)((Component)merchantCardsTabButton).transform, new Vector2(0.08f, 0.715f), new Vector2(0.5f, 0.79f));
		merchantItemsTabButton = CreateButton("Merchant Tab Items", ((Component)image).transform, font, "OGGETTI");
		((UnityEvent)merchantItemsTabButton.onClick).AddListener((UnityAction)delegate
		{
			SelectMerchantBranch(MerchantBranch.Items);
		});
		merchantItemsTabText = ((Component)merchantItemsTabButton).GetComponentInChildren<Text>();
		ApplyMerchantCampaignCta(merchantItemsTabButton, "UI/CampaignRestyle/campaign_cta_blue");
		merchantItemsTabLockImage = CreateMerchantLockIcon(merchantItemsTabButton, "Merchant Items Lock");
		CreateMerchantTabIcon(merchantItemsTabButton, "Merchant Items Icon", "UI/bag_button");
		merchantItemsTabText.fontSize = 30;
		merchantItemsTabText.resizeTextMaxSize = 30;
		SetRect(merchantItemsTabText.rectTransform, new Vector2(0.18f, 0.06f), new Vector2(0.82f, 0.94f));
		merchantItemsTabVfx = AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)merchantItemsTabButton).transform,
			new Color(0.48f, 0.68f, 0.16f, 1f));
		merchantItemsTabVfx.SetSweepScale(new Vector3(0.3167f, 1f, 1f));
		SetRect((RectTransform)((Component)merchantItemsTabButton).transform, new Vector2(0.5f, 0.715f), new Vector2(0.92f, 0.79f));

		merchantShelfRoot = new GameObject("Merchant Shelf", new Type[2]
		{
			typeof(RectTransform),
			typeof(HorizontalLayoutGroup)
		}).GetComponent<RectTransform>();
		((Transform)merchantShelfRoot).SetParent(((Component)image).transform, false);
		SetRect(merchantShelfRoot, new Vector2(0.035f, 0.405f), new Vector2(0.965f, 0.655f));
		HorizontalLayoutGroup shelfLayout = ((Component)merchantShelfRoot).GetComponent<HorizontalLayoutGroup>();
		shelfLayout.spacing = 18f;
		shelfLayout.padding = new RectOffset(6, 6, 4, 4);
		shelfLayout.childAlignment = (TextAnchor)4;
		shelfLayout.childControlWidth = true;
		shelfLayout.childControlHeight = true;
		shelfLayout.childForceExpandWidth = true;
		shelfLayout.childForceExpandHeight = true;

		merchantSellText = CreateText(
			"Merchant Context Action",
			((Component)image).transform,
			AccardND.Battlefield.MmoUiTheme.LoreFont,
			24,
			FontStyle.Normal,
			TextAnchor.MiddleLeft);
		merchantSellText.color = new Color(0.94f, 0.88f, 0.76f);
		merchantSellText.lineSpacing = 1.08f;
		merchantSellText.resizeTextForBestFit = true;
		merchantSellText.resizeTextMinSize = 18;
		merchantSellText.resizeTextMaxSize = 24;
		SetRect(merchantSellText.rectTransform, new Vector2(0.085f, 0.33f), new Vector2(0.64f, 0.405f));
		merchantSellText.rectTransform.offsetMin = new Vector2(12f, 4f);
		merchantSellText.rectTransform.offsetMax = new Vector2(-10f, -4f);
		merchantRecoverButton = CreateButton("Merchant Recover Button", ((Component)image).transform, font, "RECUPERA");
		((UnityEvent)merchantRecoverButton.onClick).AddListener(new UnityAction(RecoverSelectedMerchantCard));
		ApplyMerchantCampaignCta(merchantRecoverButton, "UI/CampaignRestyle/campaign_cta_olive");
		SetRect((RectTransform)((Component)merchantRecoverButton).transform, new Vector2(0.65f, 0.335f), new Vector2(0.925f, 0.4f));
		merchantSellButton = CreateButton("Merchant Sell Button", ((Component)image).transform, font, "VENDI");
		((UnityEvent)merchantSellButton.onClick).AddListener(new UnityAction(SellSelectedMerchantCard));
		ApplyMerchantCampaignCta(merchantSellButton, "UI/CampaignRestyle/campaign_cta_blue");
		SetRect((RectTransform)((Component)merchantSellButton).transform, new Vector2(0.65f, 0.335f), new Vector2(0.925f, 0.4f));

		merchantDeckTabButton = CreateMerchantOwnedCardsTab(
			((Component)image).transform,
			"Merchant Deck Tab",
			"MAZZO 0",
			() => SelectMerchantOwnedCardsTab(showGraveyard: false),
			22);
		merchantDeckTabText = ((Component)merchantDeckTabButton).GetComponentInChildren<Text>();
		ApplyMerchantCampaignCta(merchantDeckTabButton, "UI/CampaignRestyle/campaign_cta_olive");
		merchantDeckTabVfx = AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)merchantDeckTabButton).transform,
			new Color(0.48f, 0.68f, 0.16f, 1f));
		merchantDeckTabVfx.SetSweepScale(new Vector3(0.3167f, 1f, 1f));
		SetRect(
			(RectTransform)((Component)merchantDeckTabButton).transform,
			new Vector2(0.12f, 0.275f),
			new Vector2(0.495f, 0.335f));

		merchantGraveyardTabButton = CreateMerchantOwnedCardsTab(
			((Component)image).transform,
			"Merchant Graveyard Tab",
			"CIMITERO 0",
			() => SelectMerchantOwnedCardsTab(showGraveyard: true),
			22);
		merchantGraveyardTabText = ((Component)merchantGraveyardTabButton).GetComponentInChildren<Text>();
		ApplyMerchantCampaignCta(merchantGraveyardTabButton, "UI/CampaignRestyle/campaign_cta_dark_gray");
		merchantGraveyardTabVfx = AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
			(RectTransform)((Component)merchantGraveyardTabButton).transform,
			new Color(0.48f, 0.68f, 0.16f, 1f));
		merchantGraveyardTabVfx.SetSweepScale(new Vector3(0.3167f, 1f, 1f));
		SetRect(
			(RectTransform)((Component)merchantGraveyardTabButton).transform,
			new Vector2(0.505f, 0.275f),
			new Vector2(0.88f, 0.335f));

		CreateMerchantCardSection(
			((Component)image).transform,
			font,
			new Vector2(0.06f, 0.018f),
			new Vector2(0.94f, 0.27f),
			out merchantDeckCardsRoot,
			out merchantDeckEmptyText);
		merchantGraveyardCardsRoot = merchantDeckCardsRoot;
		merchantGraveyardEmptyText = merchantDeckEmptyText;
		merchantPanel.SetActive(false);
	}

	private static Button CreateMerchantOwnedCardsTab(
		Transform parent,
		string name,
		string label,
		UnityAction onClick,
		int fontSize)
	{
		Image image = CreateImage(name, parent, Color.white);
		image.type = Image.Type.Sliced;
		image.pixelsPerUnitMultiplier = 1f;
		image.raycastTarget = true;
		Button button = ((Component)image).gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		if (onClick != null)
		{
			((UnityEvent)button.onClick).AddListener(onClick);
		}
		AccardND.Battlefield.MmoUiTheme.ApplyButtonColors(button);
		AccardND.Battlefield.MmoUiTheme.AddMotion(button);

		Text text = CreateText(
			"Label",
			((Component)image).transform,
			AccardND.Battlefield.MmoUiTheme.LoreFont,
			fontSize,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		text.text = label;
		SetRect(text.rectTransform, Vector2.zero, Vector2.one);
		text.rectTransform.offsetMin = new Vector2(10f, 2f);
		text.rectTransform.offsetMax = new Vector2(-10f, -2f);
		SetMerchantOwnedCardsTabActive(button, null, active: false);
		return button;
	}

	private static void ApplyMerchantCampaignCta(Button button, string spriteResource)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		Image image = ((Component)button).GetComponent<Image>();
		Sprite sprite = LoadSpriteResource(spriteResource);
		if ((Object)(object)image != (Object)null && (Object)(object)sprite != (Object)null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Simple;
			image.preserveAspect = true;
			image.color = Color.white;
			button.targetGraphic = image;
		}
		Text label = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
			label.resizeTextForBestFit = true;
			label.resizeTextMinSize = AccardND.Battlefield.MmoUiTheme.LoreFontMinSize;
		}
	}

	private static Image CreateMerchantLockIcon(Button button, string name)
	{
		Image lockImage = CreateImage(name, ((Component)button).transform, Color.white);
		lockImage.sprite = LoadSpriteResource(CampaignHardcoreLockedEmblemResource);
		lockImage.preserveAspect = true;
		lockImage.raycastTarget = false;
		SetRect(lockImage.rectTransform, new Vector2(0.7f, 0.2f), new Vector2(0.82f, 0.8f));
		((Component)lockImage).gameObject.SetActive(false);
		return lockImage;
	}

	private static Image CreateMerchantTabIcon(
		Button button,
		string name,
		string spriteResource)
	{
		Image icon = CreateImage(name, ((Component)button).transform, Color.white);
		icon.sprite = LoadSpriteResource(spriteResource);
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		SetRect(icon.rectTransform, new Vector2(0.09f, 0.17f), new Vector2(0.23f, 0.83f));
		Text label = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			SetRect(label.rectTransform, new Vector2(0.28f, 0.06f), new Vector2(0.9f, 0.94f));
		}
		return icon;
	}

	private static void SetMerchantOwnedCardsTabActive(
		Button tab,
		AccardND.PvpUi.PvpUiVfx vfx,
		bool active)
	{
		if ((Object)(object)tab == (Object)null)
		{
			return;
		}
		Image image = ((Component)tab).GetComponent<Image>();
		ApplyMerchantCampaignCta(
			tab,
			active
				? "UI/CampaignRestyle/campaign_cta_olive"
				: "UI/CampaignRestyle/campaign_cta_dark_gray");
		if ((Object)(object)image != (Object)null)
		{
			image.color = Color.white;
		}
		Text label = ((Component)tab).GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			label.color = active
				? new Color(0.98f, 0.87f, 0.64f, 1f)
				: new Color(0.78f, 0.74f, 0.68f, 1f);
		}
		if ((Object)(object)vfx != (Object)null)
		{
			((Component)vfx).gameObject.SetActive(active);
		}
	}

	// --- Slot della vetrina ---

	private Image CreateMerchantShelfSlot(string name)
	{
		Image slot = CreateImage(name, (Transform)(object)merchantShelfRoot, Color.clear);
		slot.raycastTarget = false;

		// La cornice è volutamente più larga della cella del layout: in questo modo
		// tutta l'offerta (titolo, pedina, prezzo e CTA) rimane dentro il pannello.
		Image frame = CreateImage("Offer Rock Frame", ((Component)slot).transform, Color.white);
		frame.sprite = LoadSpriteResource("UI/Common/merchant_offer_rock_panel_aaa");
		frame.type = Image.Type.Sliced;
		frame.pixelsPerUnitMultiplier = 1f;
		frame.raycastTarget = false;
		SetRect(frame.rectTransform, new Vector2(-0.015f, -0.135f), new Vector2(1.015f, 1.135f));
		((Transform)frame.rectTransform).SetAsFirstSibling();

		merchantShelfViews.Add(((Component)slot).gameObject);
		return slot;
	}

	private void BuildMerchantCardSlot(MerchantCardOffer offer)
	{
		bool locked = IsMerchantBranchLocked(MerchantBranch.Cards);
		bool available = !offer.Sold && !locked && !IsMerchantDeckFull();
		Image slot = CreateMerchantShelfSlot(offer.Mystery ? "Merchant Offer Mystery" : "Merchant Offer Card");
		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		if (offer.Mystery)
		{
			Image mystery = CreateImage("Mystery Icon", ((Component)slot).transform, offer.Sold ? new Color(0.5f, 0.54f, 0.56f, 0.7f) : Color.white);
			mystery.sprite = LoadSpriteResource("UI/random_value_draw");
			mystery.preserveAspect = true;
			mystery.raycastTarget = false;
			SetRect(mystery.rectTransform, new Vector2(0.18f, 0.3f), new Vector2(0.82f, 0.82f));
		}
		else if ((Object)(object)offer.Definition != (Object)null)
		{
			PrototypeCardView cardView = PrototypeCardView.CreateBattlefieldPreview(((Component)slot).transform, offer.Definition, configuration);
			cardView.SetInteractable(interactable: true);
			CardDefinition inspected = offer.Definition;
			((UnityEvent)cardView.Button.onClick).AddListener((UnityAction)delegate
			{
				ShowCardInspection(inspected);
			});
			SetRect(cardView.RectTransform, new Vector2(0.12f, 0.3f), new Vector2(0.88f, 0.82f));
		}
		Text label = CreateText("Offer Title", ((Component)slot).transform, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
		label.color = new Color(0.9f, 0.95f, 0.96f);
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.text = offer.Mystery
			? "CARTA IGNOTA"
			: CardDisplayNames.MarketName(offer.Definition).ToUpperInvariant();
		SetRect(label.rectTransform, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.97f));
		Text price = CreateText("Offer Price", ((Component)slot).transform, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(price);
		price.color = new Color(0.92f, 0.82f, 0.64f);
		price.text = offer.Cost + " EXP";
		SetRect(price.rectTransform, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.3f));
		Button buy = CreateButton("Merchant Offer Buy", ((Component)slot).transform, font, MerchantOfferButtonLabel(offer.Sold, locked, IsMerchantDeckFull()));
		ApplyMerchantCampaignCta(buy, "UI/CampaignRestyle/campaign_cta_blue");
		MerchantCardOffer captured = offer;
		((UnityEvent)buy.onClick).AddListener((UnityAction)delegate
		{
			BuyMerchantCardOffer(captured);
		});
		buy.interactable = available;
		SetRect((RectTransform)((Component)buy).transform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.2f));
		DimMerchantSlot(slot, !available);
	}

	private void BuildMerchantItemSlot(MerchantItemOffer offer)
	{
		bool locked = IsMerchantBranchLocked(MerchantBranch.Items);
		bool available = !offer.Sold && !locked;
		Image slot = CreateMerchantShelfSlot("Merchant Offer Item");
		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		Image icon = CreateImage("Item Icon", ((Component)slot).transform, Color.white);
		icon.sprite = LoadSpriteResource("UI/" + CampaignConsumableResourceName(offer.ItemType));
		icon.preserveAspect = true;
		icon.raycastTarget = true;
		Button inspect = ((Component)icon).gameObject.AddComponent<Button>();
		inspect.targetGraphic = icon;
		CampaignConsumableType inspectedType = offer.ItemType;
		((UnityEvent)inspect.onClick).AddListener((UnityAction)delegate
		{
			ShowCampaignConsumableInspection(inspectedType);
		});
		SetRect(icon.rectTransform, new Vector2(0.18f, 0.3f), new Vector2(0.82f, 0.82f));
		Text label = CreateText("Offer Title", ((Component)slot).transform, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
		label.color = new Color(0.9f, 0.95f, 0.96f);
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.text = CampaignConsumableName(offer.ItemType).ToUpperInvariant();
		SetRect(label.rectTransform, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.97f));
		Text price = CreateText("Offer Price", ((Component)slot).transform, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(price);
		price.color = new Color(0.92f, 0.82f, 0.64f);
		price.text = offer.Cost + " EXP";
		SetRect(price.rectTransform, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.3f));
		Button buy = CreateButton("Merchant Offer Buy", ((Component)slot).transform, font, MerchantOfferButtonLabel(offer.Sold, locked, deckFull: false));
		ApplyMerchantCampaignCta(buy, "UI/CampaignRestyle/campaign_cta_blue");
		MerchantItemOffer captured = offer;
		((UnityEvent)buy.onClick).AddListener((UnityAction)delegate
		{
			BuyMerchantItemOffer(captured);
		});
		buy.interactable = available;
		SetRect((RectTransform)((Component)buy).transform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.2f));
		DimMerchantSlot(slot, !available);
	}

	private static string MerchantOfferButtonLabel(bool sold, bool locked, bool deckFull)
	{
		if (sold)
		{
			return "PRESO";
		}
		if (locked)
		{
			return "CHIUSO";
		}
		if (deckFull)
		{
			return "MAZZO PIENO";
		}
		return "COMPRA";
	}

	private static void DimMerchantSlot(Image slot, bool dimmed)
	{
		CanvasGroup group = ((Component)slot).gameObject.AddComponent<CanvasGroup>();
		group.alpha = dimmed ? 0.55f : 1f;
	}

	private void CreateMerchantCardSection(Transform parent, Font font, Vector2 minimum, Vector2 maximum, out RectTransform cardRoot, out Text emptyText)
	{
		Image image = CreateImage("Merchant Owned Cards Section", parent, Color.white);
		image.sprite = LoadSpriteResource("UI/Common/merchant_rock_panel_aaa");
		image.type = Image.Type.Sliced;
		image.pixelsPerUnitMultiplier = 1f;
		image.color = Color.white;
		image.raycastTarget = true;
		SetRect(image.rectTransform, minimum, maximum);
		GameObject val = new GameObject("Merchant Owned Cards Viewport", new Type[4]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image),
			typeof(Mask)
		});
		val.transform.SetParent(((Component)image).transform, false);
		RectTransform val2 = (RectTransform)val.transform;
		SetRect(val2, new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.96f));
		Image component = val.GetComponent<Image>();
		component.color = new Color(0.08f, 0.065f, 0.052f, 0.025f);
		component.raycastTarget = true;
		val.GetComponent<Mask>().showMaskGraphic = false;
		cardRoot = new GameObject("Merchant Owned Cards", new Type[3]
		{
			typeof(RectTransform),
			typeof(GridLayoutGroup),
			typeof(ContentSizeFitter)
		}).GetComponent<RectTransform>();
		((Transform)cardRoot).SetParent(val.transform, false);
		cardRoot.anchorMin = new Vector2(0f, 1f);
		cardRoot.anchorMax = new Vector2(1f, 1f);
		cardRoot.pivot = new Vector2(0.5f, 1f);
		cardRoot.offsetMin = Vector2.zero;
		cardRoot.offsetMax = Vector2.zero;
		GridLayoutGroup component2 = ((Component)cardRoot).GetComponent<GridLayoutGroup>();
		component2.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		component2.constraintCount = 4;
		component2.spacing = new Vector2(18f, 16f);
		component2.padding = new RectOffset(22, 22, 18, 16);
		component2.childAlignment = (TextAnchor)1;
		component2.cellSize = new Vector2(MerchantCardSize, MerchantCardSize);
		((Component)cardRoot).GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		ScrollRect scrollRect = ((Component)image).gameObject.AddComponent<ScrollRect>();
		scrollRect.viewport = val2;
		scrollRect.content = cardRoot;
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		emptyText = CreateText("Merchant Owned Cards Empty", ((Component)image).transform, font, 16, (FontStyle)2, (TextAnchor)4);
		emptyText.text = "Nessuna carta";
		emptyText.color = new Color(0.68f, 0.76f, 0.78f);
		SetRect(emptyText.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
	}

	private static void StyleMerchantRockPanel(Image image, bool addCracks)
	{
		if ((Object)(object)image == (Object)null)
		{
			return;
		}
		image.sprite = AccardND.Battlefield.MmoUiTheme.GetPanelSprite();
		image.type = Image.Type.Sliced;
		image.color = new Color(0.42f, 0.34f, 0.27f, image.color.a);
		if (!addCracks)
		{
			return;
		}
		for (int index = 0; index < 9; index++)
		{
			Image crack = CreateImage(
				"Rock Crack " + index,
				((Component)image).transform,
				index % 2 == 0
					? new Color(0.035f, 0.028f, 0.022f, 0.5f)
					: new Color(0.52f, 0.4f, 0.25f, 0.12f));
			crack.raycastTarget = false;
			RectTransform rect = crack.rectTransform;
			float x = 0.09f + ((index * 37) % 79) / 100f;
			float y = 0.12f + ((index * 53) % 71) / 100f;
			rect.anchorMin = new Vector2(x, y);
			rect.anchorMax = new Vector2(x, y);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(42f + index * 5f, index % 3 == 0 ? 2f : 1f);
			rect.localEulerAngles = new Vector3(0f, 0f, -58f + index * 17f);
		}
	}
}
}
