using System;
using System.Collections.Generic;
using AccardND.GameCore;
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

	private Text sanctuaryStatusText;



	private Image sanctuaryListViewportImage;

	private ScrollRect sanctuaryScrollRect;

	private RectTransform sanctuaryListRoot;

	private Button sanctuaryBackButton;

	private readonly List<Button> sanctuaryAltarButtons = new List<Button>();

	private readonly List<Image> sanctuaryAltarIcons = new List<Image>();

	private readonly List<GameObject> sanctuaryCards = new List<GameObject>();

	private SanctuaryAltar sanctuaryActiveAltar = SanctuaryAltar.Classes;

	private SanctuaryData sanctuaryData;

	private bool sanctuaryLoading;

	private GameObject sanctuaryConfirmPopup;

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

		Image backgroundVeil = CreateImage("Sanctuary Background Veil", ((Component)root).transform, new Color(0f, 0.005f, 0.015f, 0.42f));
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

		sanctuaryHeadingText = CreateText("Sanctuary Heading", ((Component)sanctuaryTitlePanel).transform, font, 40, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(sanctuaryHeadingText);
		sanctuaryHeadingText.text = "SANTUARIO";
		sanctuaryHeadingText.color = SanctuaryGold;
		AddSanctuaryTextOutline(sanctuaryHeadingText);
		SetRect(sanctuaryHeadingText.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		sanctuaryHeadingText.rectTransform.offsetMin = new Vector2(0f, -18f);
		sanctuaryHeadingText.rectTransform.offsetMax = new Vector2(0f, -18f);

		CreateSanctuaryAltarButton(content, font, "CLASSI", SanctuaryAltar.Classes);
		CreateSanctuaryAltarButton(content, font, "TECNICHE", SanctuaryAltar.Techniques);
		CreateSanctuaryAltarButton(content, font, "RELIQUIE", SanctuaryAltar.Relics);

		Image viewport = CreateImage("Sanctuary Viewport", content, new Color(0.005f, 0.007f, 0.012f, 0.18f));
		sanctuaryListViewportImage = viewport;
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

		sanctuaryBackButton = CreateButton("Sanctuary Back", content, font, "INDIETRO");
		((UnityEvent)sanctuaryBackButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideSanctuary();
		});

		CreateSanctuaryConfirmPopup(((Component)root).transform, font);
		RefreshSanctuaryLayout();
		sanctuaryPanel.SetActive(false);
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

		Image dialog = CreateImage("Sanctuary Confirm Dialog", ((Component)overlay).transform, new Color(0.012f, 0.018f, 0.032f, 0.98f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(dialog.rectTransform, "Sanctuary Confirm Crest", new Vector2(0.5f, 1f), new Vector2(42f, 42f), Color.white);
		SetRect(dialog.rectTransform, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f));

		sanctuaryConfirmTitleText = CreateText("Sanctuary Confirm Title", ((Component)dialog).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(sanctuaryConfirmTitleText);
		sanctuaryConfirmTitleText.color = SanctuaryGold;
		SetRect(sanctuaryConfirmTitleText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f));

		sanctuaryConfirmBodyText = CreateText("Sanctuary Confirm Body", ((Component)dialog).transform, font, 20, (FontStyle)1, (TextAnchor)4);
		sanctuaryConfirmBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		sanctuaryConfirmBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		sanctuaryConfirmBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		sanctuaryConfirmBodyText.resizeTextForBestFit = true;
		sanctuaryConfirmBodyText.resizeTextMinSize = 13;
		sanctuaryConfirmBodyText.resizeTextMaxSize = 20;
		SetRect(sanctuaryConfirmBodyText.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.7f));

		Button cancelButton = CreateButton("Sanctuary Confirm Cancel", ((Component)dialog).transform, font, "ANNULLA");
		((UnityEvent)cancelButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			HideSanctuaryConfirmPopup();
		});
		SetRect((RectTransform)((Component)cancelButton).transform, new Vector2(0.08f, 0.1f), new Vector2(0.44f, 0.27f));

		sanctuaryConfirmButton = CreateButton("Sanctuary Confirm Accept", ((Component)dialog).transform, font, "OFFRI IL MIELE");
		sanctuaryConfirmButtonText = ((Component)sanctuaryConfirmButton).GetComponentInChildren<Text>();
		((UnityEvent)sanctuaryConfirmButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			ConfirmSanctuaryPurchase();
		});
		SetRect((RectTransform)((Component)sanctuaryConfirmButton).transform, new Vector2(0.56f, 0.1f), new Vector2(0.92f, 0.27f));

		sanctuaryConfirmPopup.SetActive(false);
	}

	private void CreateSanctuaryAltarButton(Transform parent, Font font, string label, SanctuaryAltar altar)
	{
		Button button = CreateButton("Sanctuary Altar " + altar, parent, font, label);
		Image background = ((Component)button).GetComponent<Image>();
		background.sprite = LoadSpriteResource("UI/Sanctuary/sanctuary_tab_frame_v2");
		background.type = Image.Type.Simple;
		background.preserveAspect = false;
		Image icon = CreateImage("Icon", ((Component)button).transform, SanctuaryGold);
		icon.sprite = LoadSpriteResource(altar switch
		{
			SanctuaryAltar.Classes => "UI/deck_icon",
			SanctuaryAltar.Techniques => "UI/warrior_sword",
			_ => "UI/paladin_holy_crest"
		});
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		SetRect(icon.rectTransform, new Vector2(0.08f, 0.2f), new Vector2(0.29f, 0.8f));

		Text labelText = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)labelText != (Object)null)
		{
			SetRect(labelText.rectTransform, new Vector2(0.28f, 0.06f), new Vector2(0.96f, 0.94f));
		}
		((UnityEvent)button.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			SelectSanctuaryAltar(altar);
		});
		sanctuaryAltarButtons.Add(button);
		sanctuaryAltarIcons.Add(icon);
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
		sanctuaryActiveAltar = SanctuaryAltar.Classes;
		sanctuaryNotice = null;
		HideSanctuaryConfirmPopup();
		sanctuaryPanel.SetActive(true);
		sanctuaryPanel.transform.SetAsLastSibling();
		RefreshAccountBannerView();
		RefreshSanctuaryLayout();
		RefreshSanctuaryList();
		LoadSanctuaryFromServer();
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
				headerCanvas.sortingOrder = 901;
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
			honeyCanvas.sortingOrder = 902;
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
		SetSanctuaryStatus("Consulto l'alveare...");
		try
		{
			// Il link di progressione nasce all'apertura del menu campagna: chi arriva al
			// Santuario direttamente dall'hub non lo ha ancora, quindi va stabilito qui
			// invece di dichiarare subito il forfait.
			if (await EnsureServerProgressAsync())
			{
				sanctuaryData = await serverProgress.GetSanctuaryAsync();
				AppendLog($"SANTUARIO - catalogo ricevuto: {sanctuaryData?.entries?.Length ?? 0} voci.");
			}
			else
			{
				sanctuaryData = null;
				sanctuaryNotice = "Santuario non disponibile offline: serve la connessione al server.";
				AppendLog("SANTUARIO - nessuna connessione al server.");
			}
		}
		catch (Exception exception)
		{
			sanctuaryData = null;
			AppendLog($"SANTUARIO - catalogo non ricevuto: {exception.Message}");
			sanctuaryNotice = "Il Santuario non risponde: " + exception.Message;
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
			SetSanctuaryStatus("Nessuna voce in questo altare.");
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
		// L'altare delle Reliquie tiene insieme oggetti e slot: gli slot sono il contenitore
		// degli oggetti, separarli in due schermate spezzerebbe la lettura.
		_ => entry.type == "item" || entry.type == "slot"
	};

	private string DefaultSanctuaryStatus()
	{
		if (sanctuaryActiveAltar == SanctuaryAltar.Classes)
		{
			return "Ogni classe chiede una prova guadagnata giocando, oltre al miele.";
		}
		if (sanctuaryActiveAltar == SanctuaryAltar.Techniques)
		{
			return "Le tecniche sono in preparazione: visibili, non ancora acquistabili.";
		}
		// Qui si sblocca il diritto di comprare: le copie si prendono al negozio.
		return $"Sblocca gli oggetti per renderli acquistabili al negozio. Slot bisaccia: {sanctuaryData?.bagSlots ?? 0}.";
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
		cover.sprite = LoadSpriteResource(
			entry.type == "secondAbility"
				? "UI/Sanctuary/sanctuary_tab_frame_v2"
				: "UI/Sanctuary/sanctuary_card_frame_v2");
		cover.type = Image.Type.Simple;
		cover.preserveAspect = false;
		cover.color = entry.owned || entry.available
			? Color.white
			: new Color(0.56f, 0.56f, 0.6f, 0.98f);
		HeroClass heroClass = HeroClass.Mage;
		bool classIconCard = entry.type == "class" && TryGetSanctuaryHeroClass(entry, out heroClass);
		bool techniqueCard = entry.type == "secondAbility";
		Vector2 coverMin = Vector2.zero;
		Vector2 coverMax = Vector2.one;
		if (techniqueCard)
		{
			coverMin = new Vector2(-0.04f, -0.75f);
			coverMax = new Vector2(1.04f, 1.75f);
		}
		else if (classIconCard)
		{
			// La texture della carta include ampi margini trasparenti: espandendo solo
			// la cover, la cornice visibile riempie la cella senza oltrepassare la griglia.
			coverMin = new Vector2(-0.28f, -0.15f);
			coverMax = new Vector2(1.28f, 1.15f);
		}
		SetRect(
			cover.rectTransform,
			coverMin,
			coverMax);

		Transform contentRoot = techniqueCard || classIconCard
			? card.transform
			: ((Component)cover).transform;
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
			CreateSanctuaryTechniqueRow(entry, contentRoot);
		}
		else
		{
			CreateSanctuaryRelicCard(entry, ((Component)cover).transform);
		}

		sanctuaryCards.Add(card);
	}

	private void CreateSanctuaryTechniqueRow(SanctuaryEntryData entry, Transform parent)
	{
		Image iconBack = CreateImage("Icon Back", parent, Color.clear);
		iconBack.sprite = null;
		iconBack.type = Image.Type.Simple;
		SetRect(iconBack.rectTransform, new Vector2(0.018f, 0.1f), new Vector2(0.17f, 0.9f));

		Image icon = CreateImage("Icon", ((Component)iconBack).transform, Color.white);
		icon.sprite = GetSanctuaryEntrySprite(entry);
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		Stretch(icon.rectTransform, 8f);
		((Component)icon).gameObject.SetActive((Object)(object)icon.sprite != (Object)null);

		Text nameText = CreateText(
			"Name",
			parent,
			AccardND.Battlefield.MmoUiTheme.TitleBoldFont,
			20,
			FontStyle.Normal,
			TextAnchor.MiddleLeft);
		nameText.text = (entry.name ?? entry.id).ToUpperInvariant();
		nameText.color = SanctuaryGold;
		AddSanctuaryTextOutline(nameText);
		SetRect(nameText.rectTransform, new Vector2(0.19f, 0.6f), new Vector2(0.72f, 0.91f));

		Text descriptionText = CreateText(
			"Description",
			parent,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			15,
			FontStyle.Normal,
			TextAnchor.UpperLeft);
		descriptionText.text = entry.description ?? string.Empty;
		descriptionText.color = new Color(0.86f, 0.83f, 0.77f);
		descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
		descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
		descriptionText.resizeTextForBestFit = true;
		descriptionText.resizeTextMinSize = 11;
		descriptionText.resizeTextMaxSize = 15;
		SetRect(descriptionText.rectTransform, new Vector2(0.19f, 0.27f), new Vector2(0.72f, 0.62f));

		Text requirementText = CreateText(
			"Requirements",
			parent,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			13,
			FontStyle.Bold,
			TextAnchor.LowerLeft);
		requirementText.text = SanctuaryRequirementSummary(entry);
		requirementText.color = entry.requirementsMet ? SanctuaryOwned : SanctuaryDim;
		requirementText.horizontalOverflow = HorizontalWrapMode.Wrap;
		requirementText.verticalOverflow = VerticalWrapMode.Truncate;
		SetRect(requirementText.rectTransform, new Vector2(0.19f, 0.08f), new Vector2(0.72f, 0.3f));

		Text statusText = CreateText(
			"Status",
			parent,
			AccardND.Battlefield.MmoUiTheme.TitleBoldFont,
			16,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		statusText.text = SanctuaryCardStatus(entry);
		statusText.color = SanctuaryCardStatusColor(entry);
		statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
		statusText.verticalOverflow = VerticalWrapMode.Truncate;
		AddSanctuaryTextOutline(statusText);
		SetRect(statusText.rectTransform, new Vector2(0.73f, 0.14f), new Vector2(0.975f, 0.86f));
	}

	private void CreateSanctuaryRelicCard(SanctuaryEntryData entry, Transform parent)
	{
		Text nameText = CreateText(
			"Name",
			parent,
			AccardND.Battlefield.MmoUiTheme.TitleBoldFont,
			17,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		nameText.text = (entry.name ?? entry.id).ToUpperInvariant();
		nameText.color = SanctuaryGold;
		nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
		nameText.verticalOverflow = VerticalWrapMode.Truncate;
		AddSanctuaryTextOutline(nameText);
		SetRect(nameText.rectTransform, new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.96f));

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
			14,
			FontStyle.Normal,
			TextAnchor.UpperCenter);
		descriptionText.text = entry.description ?? string.Empty;
		descriptionText.color = new Color(0.86f, 0.82f, 0.72f);
		descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
		descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
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

		Text statusText = CreateText(
			"Status",
			parent,
			AccardND.Battlefield.MmoUiTheme.TitleBoldFont,
			14,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		statusText.text = SanctuaryCardStatus(entry);
		statusText.color = SanctuaryCardStatusColor(entry);
		AddSanctuaryTextOutline(statusText);
		SetRect(statusText.rectTransform, new Vector2(0.06f, 0.015f), new Vector2(0.94f, 0.12f));
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
		Text label = CreateText("Label", parent, classNameFont, 19, FontStyle.Normal, TextAnchor.MiddleCenter);
		label.raycastTarget = false;
		label.text = HeroClassDisplayName(heroClass).ToUpperInvariant();
		label.color = SanctuaryGold;
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
		label.resizeTextForBestFit = true;
		label.resizeTextMinSize = 8;
		label.resizeTextMaxSize = 19;
		AddSanctuaryTextOutline(label);
		SetRect(label.rectTransform, new Vector2(0.04f, 0.27f), new Vector2(0.96f, 0.4f));

		Text statusText = CreateText("Status", parent, AccardND.Battlefield.MmoUiTheme.BodyFont, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
		statusText.raycastTarget = false;
		string requirement = SanctuaryRequirementSummary(entry);
		statusText.text = string.IsNullOrWhiteSpace(requirement)
			? SanctuaryCardStatus(entry)
			: $"{SanctuaryCardStatus(entry)}\n{requirement}";
		statusText.color = SanctuaryCardStatusColor(entry);
		statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
		statusText.verticalOverflow = VerticalWrapMode.Truncate;
		statusText.resizeTextForBestFit = true;
		statusText.resizeTextMinSize = 8;
		statusText.resizeTextMaxSize = 13;
		AddSanctuaryTextOutline(statusText);
		SetRect(statusText.rectTransform, new Vector2(0.05f, 0.055f), new Vector2(0.95f, 0.27f));
	}

	private Sprite GetSanctuaryEntrySprite(SanctuaryEntryData entry)
	{
		string value = $"{entry?.id} {entry?.name}".ToLowerInvariant();
		string resourcePath;
		if (value.Contains("detector"))
			resourcePath = "UI/detector_item";
		else if (value.Contains("defrost"))
			resourcePath = "UI/defrost_item";
		else if (value.Contains("double_exp") || value.Contains("doppia exp") || value.Contains("double exp"))
			resourcePath = "UI/double_exp_item";
		else if (value.Contains("empower"))
			resourcePath = "UI/empower_item";
		else if (value.Contains("second_chance") || value.Contains("seconda chance") || value.Contains("second chance"))
			resourcePath = "UI/second_chance_item";
		else if (value.Contains("ruby") || value.Contains("rubino"))
			resourcePath = "UI/ruby_seal_item";
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
	private static bool CanOfferSanctuaryEntry(SanctuaryEntryData entry) =>
		entry != null && entry.available && entry.requirementsMet && !entry.owned;

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

		if ((Object)(object)sanctuaryConfirmTitleText != (Object)null)
		{
			sanctuaryConfirmTitleText.text = (entry.name ?? entry.id).ToUpperInvariant();
		}
		if ((Object)(object)sanctuaryConfirmBodyText != (Object)null)
		{
			string offer = entry.type switch
			{
				"item" => $"Sblocca {entry.name} per {entry.honeyCost} vasetti di miele: da quel momento il negozio potra' vendertelo. Lo sblocco non ti da' una copia.",
				"slot" => $"Apri uno slot in piu' nella bisaccia per {entry.honeyCost} vasetti di miele.",
				_ => $"Hai superato le prove. Offri {entry.honeyCost} vasetti di miele all'alveare per ricordare questa classe."
			};
			sanctuaryConfirmBodyText.text = affordable
				? $"{offer}\n\nMiele disponibile: {honey}."
				: $"Servono {entry.honeyCost} vasetti di miele e ne hai {honey}.\n\nTorna quando ne avrai abbastanza.";
		}
		if ((Object)(object)sanctuaryConfirmButton != (Object)null)
		{
			sanctuaryConfirmButton.interactable = affordable && !sanctuaryPurchasing;
		}
		if ((Object)(object)sanctuaryConfirmButtonText != (Object)null)
		{
			sanctuaryConfirmButtonText.text = affordable ? "OFFRI IL MIELE" : "MIELE INSUFFICIENTE";
		}

		sanctuaryConfirmPopup.SetActive(true);
		sanctuaryConfirmPopup.transform.SetAsLastSibling();
	}

	private void HideSanctuaryConfirmPopup()
	{
		if ((Object)(object)sanctuaryConfirmPopup != (Object)null)
		{
			sanctuaryConfirmPopup.SetActive(false);
		}
		sanctuaryPendingEntry = null;
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

		if (!ServerProgressReady)
		{
			SetSanctuaryStatus("Serve la connessione al server per offrire il miele.");
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
			sanctuaryNotice = string.Equals(entry.type, "item", StringComparison.Ordinal)
				? $"{entry.name} sbloccato: ora il negozio puo' vendertelo."
				: $"{entry.name} ora e' tua.";
			MirrorServerProgress();
			RefreshSinglePlayerProgressView();
			AppendLog($"SANTUARIO - {entry.id} acquistato per {entry.honeyCost} miele.");
			HideSanctuaryConfirmPopup();
		}
		catch (Exception exception)
		{
			AppendLog($"SANTUARIO - acquisto {entry.id} rifiutato: {exception.Message}");
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
		"item" => AccardND.GameData.SinglePlayerUnlockType.Item,
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
			return "OTTENUTA";
		}
		if (!entry.available)
		{
			// Le starter hanno costo zero e si prendono col tutorial; le tecniche mostrano
			// il prezzo perche' il giocatore sappia cosa lo aspetta.
			return entry.honeyCost > 0 ? $"IN ARRIVO - {entry.honeyCost} MIELE" : "DAL TUTORIAL";
		}
		return $"{entry.honeyCost} MIELE";
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
		RefreshAccountHoneyPanelLayout(landscape);
		EnsureSanctuarySharedHudSorting();

		float left = compact ? 0.055f : 0.1f;
		float right = compact ? 0.945f : 0.9f;
		SetRect(
			sanctuaryTitlePanel.rectTransform,
			new Vector2(left + 0.012f, compact ? 0.765f : 0.755f),
			new Vector2(right - 0.012f, compact ? 0.885f : 0.88f));
		SetRect(
			sanctuaryScreenOuterFrameImage.rectTransform,
			new Vector2(0.008f, 0.008f),
			new Vector2(0.992f, compact ? 0.775f : 0.765f));

		sanctuaryHeadingText.fontSize = compact ? 37 : 42;
		sanctuaryHeadingText.resizeTextMaxSize = sanctuaryHeadingText.fontSize;

		float altarTop = compact ? 0.752f : 0.745f;
		float altarBottom = compact ? 0.682f : 0.675f;
		float span = (right - left) / 3f;
		for (int index = 0; index < sanctuaryAltarButtons.Count; index++)
		{
			Button button = sanctuaryAltarButtons[index];
			if ((Object)(object)button == (Object)null)
			{
				continue;
			}
			float start = left + span * index;
			SetRect((RectTransform)((Component)button).transform,
				new Vector2(start + 0.006f, altarBottom),
				new Vector2(start + span - 0.006f, altarTop));
		}

		SetRect(
			sanctuaryListViewportImage.rectTransform,
			new Vector2(left, compact ? 0.165f : 0.165f),
			new Vector2(right, compact ? 0.674f : 0.667f));
		ConfigureSanctuaryGrid(compact);

		SetRect((RectTransform)((Component)sanctuaryBackButton).transform,
			compact ? new Vector2(0.31f, 0.075f) : new Vector2(0.37f, 0.072f),
			compact ? new Vector2(0.69f, 0.15f) : new Vector2(0.63f, 0.15f));
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
		int columns = sanctuaryActiveAltar == SanctuaryAltar.Techniques ? 1 : 3;
		float spacing = compact ? 8f : 12f;
		int padding = sanctuaryActiveAltar == SanctuaryAltar.Techniques
			? (compact ? 6 : 10)
			: (compact ? 10 : 14);
		float usableWidth = Mathf.Max(
			1f,
			rect.width - padding * 2f - spacing * (columns - 1));
		float cellWidth = usableWidth / columns;
		float cellHeight = sanctuaryActiveAltar switch
		{
			SanctuaryAltar.Classes => cellWidth * 1.04f,
			SanctuaryAltar.Techniques => compact ? 174f : 190f,
			_ => cellWidth * 1.22f
		};
		grid.constraintCount = columns;
		grid.spacing = new Vector2(spacing, spacing);
		grid.cellSize = new Vector2(cellWidth, cellHeight);
		grid.padding = new RectOffset(padding, padding, padding, padding);
	}
}
}
