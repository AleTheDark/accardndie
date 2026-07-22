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
	private const float MerchantCardSize = 72f;

	private void CreateMerchantView(Transform canvasTransform, Font font)
	{
		Image image = CreateImage("Merchant Panel", canvasTransform, new Color(0.008f, 0.016f, 0.024f, 0.96f));
		StylePanel(image);
		merchantPanel = ((Component)image).gameObject;
		SetRect(image.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.9f));
		Canvas obj = merchantPanel.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 420;
		merchantPanel.AddComponent<GraphicRaycaster>();
		Text text = CreateText("Merchant Title", ((Component)image).transform, font, 38, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text);
		text.text = "MERCANTE";
		text.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(text.rectTransform, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f));
		merchantStatusText = CreateText("Merchant Status", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
		merchantStatusText.color = new Color(0.86f, 0.94f, 0.96f);
		SetRect(merchantStatusText.rectTransform, new Vector2(0.08f, 0.81f), new Vector2(0.92f, 0.89f));
		Button button = CreateImageButton("Close Merchant", ((Component)image).transform, font, cancelActionSprite, string.Empty);
		((UnityEvent)button.onClick).AddListener(new UnityAction(CloseMerchantPanel));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.86f, 0.895f), new Vector2(0.96f, 0.965f));
		merchantRandomBuyButton = CreateImageButton("Merchant Buy Random", ((Component)image).transform, font, LoadSpriteResource("UI/random_value_draw"), "RANDOM");
		((UnityEvent)merchantRandomBuyButton.onClick).AddListener((UnityAction)delegate
		{
			BuyMerchantCard(MerchantBuyMode.Random);
		});
		SetRect((RectTransform)((Component)merchantRandomBuyButton).transform, new Vector2(0.08f, 0.63f), new Vector2(0.25f, 0.79f));
		Button button2 = CreateImageButton("Merchant Class", ((Component)image).transform, font, LoadSpriteResource(DeckBuilderClassResourcePath(merchantSelectedClass)), HeroClassDisplayName(merchantSelectedClass).ToUpperInvariant());
		((UnityEvent)button2.onClick).AddListener(new UnityAction(CycleMerchantClass));
		merchantClassImage = ((Component)button2).GetComponent<Image>();
		merchantClassText = ((Component)button2).GetComponentInChildren<Text>();
		SetRect((RectTransform)((Component)button2).transform, new Vector2(0.3f, 0.69f), new Vector2(0.43f, 0.79f));
		merchantClassBuyButton = CreateImageButton("Merchant Buy Class", ((Component)image).transform, font, LoadSpriteResource("UI/buy_card"), "CLASSE");
		((UnityEvent)merchantClassBuyButton.onClick).AddListener((UnityAction)delegate
		{
			BuyMerchantCard(MerchantBuyMode.Class);
		});
		SetRect((RectTransform)((Component)merchantClassBuyButton).transform, new Vector2(0.45f, 0.69f), new Vector2(0.58f, 0.79f));
		Button button3 = CreateImageButton("Merchant Strength", ((Component)image).transform, font, LoadSpriteResource(DeckBuilderStrengthResourcePath(merchantSelectedStrength)), string.Empty);
		((UnityEvent)button3.onClick).AddListener(new UnityAction(CycleMerchantStrength));
		merchantStrengthImage = ((Component)button3).GetComponent<Image>();
		SetRect((RectTransform)((Component)button3).transform, new Vector2(0.3f, 0.56f), new Vector2(0.43f, 0.66f));
		merchantStrengthBuyButton = CreateImageButton("Merchant Buy Strength", ((Component)image).transform, font, LoadSpriteResource("UI/buy_card"), "VALORE");
		((UnityEvent)merchantStrengthBuyButton.onClick).AddListener((UnityAction)delegate
		{
			BuyMerchantCard(MerchantBuyMode.Strength);
		});
		SetRect((RectTransform)((Component)merchantStrengthBuyButton).transform, new Vector2(0.45f, 0.56f), new Vector2(0.58f, 0.66f));
		merchantSellText = CreateText("Merchant Sell", ((Component)image).transform, font, 22, (FontStyle)1, (TextAnchor)4);
		merchantSellText.color = new Color(0.9f, 0.95f, 0.96f);
		SetRect(merchantSellText.rectTransform, new Vector2(0.62f, 0.61f), new Vector2(0.9f, 0.78f));
		merchantRecoverButton = CreateImageButton("Merchant Recover Button", ((Component)image).transform, font, LoadSpriteResource("UI/buy_card"), "RECUPERA");
		((UnityEvent)merchantRecoverButton.onClick).AddListener(new UnityAction(RecoverSelectedMerchantCard));
		SetRect((RectTransform)((Component)merchantRecoverButton).transform, new Vector2(0.62f, 0.55f), new Vector2(0.74f, 0.66f));
		merchantSellButton = CreateImageButton("Merchant Sell Button", ((Component)image).transform, font, LoadSpriteResource("UI/buy_card"), "VENDI");
		((UnityEvent)merchantSellButton.onClick).AddListener(new UnityAction(SellSelectedMerchantCard));
		SetRect((RectTransform)((Component)merchantSellButton).transform, new Vector2(0.78f, 0.55f), new Vector2(0.9f, 0.66f));
		CreateMerchantCardSection(((Component)image).transform, font, "CARTE NEL MAZZO", new Vector2(0.06f, 0.285f), new Vector2(0.94f, 0.51f), out merchantDeckCardsRoot, out merchantDeckEmptyText);
		CreateMerchantCardSection(((Component)image).transform, font, "CARTE AL CIMITERO", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.265f), out merchantGraveyardCardsRoot, out merchantGraveyardEmptyText);
		merchantPanel.SetActive(false);
	}

	private void CreateMerchantCardSection(Transform parent, Font font, string title, Vector2 minimum, Vector2 maximum, out RectTransform cardRoot, out Text emptyText)
	{
		Image image = CreateImage(title + " Merchant Section", parent, new Color(0.018f, 0.035f, 0.046f, 0.88f));
		StylePanel(image);
		SetRect(image.rectTransform, minimum, maximum);
		Text text = CreateText(title, ((Component)image).transform, font, 17, (FontStyle)1, (TextAnchor)3);
		text.text = title;
		text.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(text.rectTransform, new Vector2(0.035f, 0.79f), new Vector2(0.965f, 0.965f));
		GameObject val = new GameObject(title + " Viewport", new Type[4]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image),
			typeof(Mask)
		});
		val.transform.SetParent(((Component)image).transform, false);
		RectTransform val2 = (RectTransform)val.transform;
		SetRect(val2, new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.765f));
		Image component = val.GetComponent<Image>();
		component.color = new Color(1f, 1f, 1f, 0.02f);
		component.raycastTarget = true;
		val.GetComponent<Mask>().showMaskGraphic = false;
		cardRoot = new GameObject(title + " Cards", new Type[3]
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
		component2.constraintCount = 6;
		component2.spacing = new Vector2(10f, 8f);
		component2.padding = new RectOffset(14, 14, 8, 8);
		component2.childAlignment = (TextAnchor)1;
		component2.cellSize = new Vector2(MerchantCardSize, MerchantCardSize);
		((Component)cardRoot).GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		ScrollRect scrollRect = ((Component)image).gameObject.AddComponent<ScrollRect>();
		scrollRect.viewport = val2;
		scrollRect.content = cardRoot;
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		emptyText = CreateText(title + " Empty", ((Component)image).transform, font, 16, (FontStyle)2, (TextAnchor)4);
		emptyText.text = "Nessuna carta";
		emptyText.color = new Color(0.68f, 0.76f, 0.78f);
		SetRect(emptyText.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.72f));
	}
}
}
