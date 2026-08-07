using System;
using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
/// <summary>
/// Pannello delle opzioni. Le voci stanno in una colonna con layout automatico
/// invece che su ancoraggi calcolati a mano: così una riga che compare o sparisce
/// (la privacy, che si vede solo dove il consenso e' stato raccolto) non obbliga a
/// ricalcolare la posizione di tutte le altre, e il pannello si adatta da solo.
/// </summary>
public sealed partial class BattleBoardController
{
	private const float OptionsSectionHeight = 38f;

	private const float OptionsRowHeight = 78f;

	private const float OptionsActionRowHeight = 88f;

	private const float OptionsLanguageItemHeight = 66f;

	private static readonly Color OptionsGold = new Color(0.95f, 0.79f, 0.34f);

	private static readonly Color OptionsLabel = new Color(0.82f, 0.9f, 0.92f);

	private RectTransform optionsPanelRect;

	private Button languageComboButton;

	private Text languageComboLabel;

	private RectTransform languageComboRect;

	private GameObject languageDropdownOverlay;

	private RectTransform languageDropdownList;

	private float languageDropdownHeight;

	private readonly List<LanguageComboItem> languageComboItems = new List<LanguageComboItem>();

	/// <summary>Una voce del menu a tendina della lingua, con il pallino da accendere.</summary>
	private readonly struct LanguageComboItem
	{
		public LanguageComboItem(string code, Image marker)
		{
			Code = code;
			Marker = marker;
		}

		public string Code { get; }

		public Image Marker { get; }
	}

	/// <summary>
	/// Tutto il pannello vive sulla radice del canvas, non dentro la safe area: quella
	/// viene riancorata di continuo dal layout responsive e un modale centrato al suo
	/// interno finirebbe fuori asse rispetto allo schermo.
	/// </summary>
	private void CreateOptionsPanel(Transform canvasRoot, Font font)
	{
		Image backdrop = CreateImage("Options Backdrop", canvasRoot, new Color(0f, 0f, 0f, 0.72f));
		backdrop.raycastTarget = true;
		Stretch(backdrop.rectTransform);
		optionsBackdropPanel = backdrop.gameObject;
		Button backdropButton = optionsBackdropPanel.AddComponent<Button>();
		backdropButton.transition = Selectable.Transition.None;
		backdropButton.onClick.AddListener(CloseOptionsPanel);
		Canvas backdropCanvas = optionsBackdropPanel.AddComponent<Canvas>();
		backdropCanvas.overrideSorting = true;
		backdropCanvas.sortingOrder = 980;
		optionsBackdropPanel.AddComponent<GraphicRaycaster>();
		optionsBackdropPanel.SetActive(false);

		Image panel = CreateImage("Options Panel", canvasRoot, new Color(0.008f, 0.014f, 0.022f, 0.97f));
		StylePanel(panel);
		panel.raycastTarget = true;
		optionsPanel = panel.gameObject;
		optionsPanelRect = panel.rectTransform;
		optionsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
		optionsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
		optionsPanelRect.pivot = new Vector2(0.5f, 0.5f);
		optionsPanelRect.anchoredPosition = Vector2.zero;
		optionsPanelRect.sizeDelta = new Vector2(880f, 700f);

		Canvas panelCanvas = optionsPanel.AddComponent<Canvas>();
		panelCanvas.overrideSorting = true;
		panelCanvas.sortingOrder = 981;
		optionsPanel.AddComponent<GraphicRaycaster>();

		VerticalLayoutGroup column = optionsPanel.AddComponent<VerticalLayoutGroup>();
		column.padding = new RectOffset(34, 34, 26, 26);
		column.spacing = 12f;
		column.childAlignment = TextAnchor.UpperCenter;
		column.childControlWidth = true;
		column.childControlHeight = true;
		column.childForceExpandWidth = true;
		column.childForceExpandHeight = false;

		ContentSizeFitter fitter = optionsPanel.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		CreateOptionsTitle(optionsPanel.transform, font);
		CreateOptionsSection(optionsPanel.transform, font, "Options Audio Section", GameTextKeys.Options.SectionAudio, "AUDIO");
		CreateAudioOptionRow(
			optionsPanel.transform,
			font,
			"SFX",
			GameTextKeys.Options.SfxVolume,
			"EFFETTI",
			DecreaseSfxVolume,
			IncreaseSfxVolume,
			ToggleSfxMute,
			out sfxVolumeText,
			out sfxMuteButton,
			out sfxMuteButtonText);
		CreateAudioOptionRow(
			optionsPanel.transform,
			font,
			"Music",
			GameTextKeys.Options.MusicVolume,
			"MUSICA",
			DecreaseMusicVolume,
			IncreaseMusicVolume,
			ToggleMusicMute,
			out musicVolumeText,
			out musicMuteButton,
			out musicMuteButtonText);

		CreateOptionsSection(optionsPanel.transform, font, "Options Language Section", GameTextKeys.Options.SectionLanguage, "LINGUA");
		CreateLanguageRow(optionsPanel.transform, font);

		CreateOptionsSection(optionsPanel.transform, font, "Options Game Section", GameTextKeys.Options.SectionGame, "GIOCO");
		CreateOptionsShortcutRow(optionsPanel.transform, font);
		CreateOptionsActionRow(optionsPanel.transform, font);

		CreateLanguageDropdown(canvasRoot, font);

		SetOptionsPanelVisible(false);
		RefreshSfxOptionsUi();
		RefreshMusicOptionsUi();
		RefreshLanguageOptionsUi();

		GameText.LocaleChanged -= HandleOptionsLocaleChanged;
		GameText.LocaleChanged += HandleOptionsLocaleChanged;
	}

	private void OnDestroy()
	{
		GameText.LocaleChanged -= HandleOptionsLocaleChanged;
	}

	/// <summary>
	/// I testi fissi si riaggiornano da soli tramite EditableRuntimeText; quelli che
	/// dipendono dallo stato (volume, muto, resa) vanno riscritti a mano.
	/// </summary>
	private void HandleOptionsLocaleChanged()
	{
		RefreshSfxOptionsUi();
		RefreshMusicOptionsUi();
		RefreshLanguageOptionsUi();
		RefreshOptionsMainMenuButton();
	}

	private void CreateOptionsTitle(Transform parent, Font font)
	{
		Text title = CreateText("Options Title", parent, font, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
		MmoUiTheme.StyleAsTitle(title);
		title.color = OptionsGold;
		title.text = GameText.GetOrFallbackSilent(GameTextKeys.Options.Title, "OPZIONI");
		EditableRuntimeText.BindLocalized(title, GameTextKeys.Options.Title, "OPZIONI");
		SetOptionsCellSize(title, 0f, 1f, 58f);
	}

	private static void CreateOptionsSection(
		Transform parent,
		Font font,
		string name,
		string key,
		string italianFallback)
	{
		GameObject root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
		root.transform.SetParent(parent, false);

		LayoutElement element = root.GetComponent<LayoutElement>();
		element.minHeight = OptionsSectionHeight;
		element.preferredHeight = OptionsSectionHeight;

		Text label = CreateText(name + " Label", root.transform, font, 16, FontStyle.Bold, TextAnchor.LowerLeft);
		label.color = OptionsGold;
		label.text = GameText.GetOrFallbackSilent(key, italianFallback);
		EditableRuntimeText.BindLocalized(label, key, italianFallback);
		Stretch(label.rectTransform);

		Image rule = CreateImage(name + " Rule", root.transform, new Color(0.95f, 0.79f, 0.34f, 0.26f));
		RectTransform ruleRect = rule.rectTransform;
		ruleRect.anchorMin = new Vector2(0f, 0f);
		ruleRect.anchorMax = new Vector2(1f, 0f);
		ruleRect.pivot = new Vector2(0.5f, 0f);
		ruleRect.offsetMin = Vector2.zero;
		ruleRect.offsetMax = new Vector2(0f, 2f);
	}

	/// <summary>Riga "etichetta - + valore + muto", identica per effetti e musica.</summary>
	private void CreateAudioOptionRow(
		Transform parent,
		Font font,
		string name,
		string labelKey,
		string labelItalianFallback,
		UnityEngine.Events.UnityAction decrease,
		UnityEngine.Events.UnityAction increase,
		UnityEngine.Events.UnityAction toggleMute,
		out Text valueText,
		out Button muteButton,
		out Text muteButtonText)
	{
		RectTransform row = CreateOptionsRow(name + " Volume Row", parent, OptionsRowHeight);

		Text label = CreateText(name + " Volume Label", row, font, 17, FontStyle.Bold, TextAnchor.MiddleLeft);
		label.color = OptionsLabel;
		label.text = GameText.GetOrFallbackSilent(labelKey, labelItalianFallback);
		EditableRuntimeText.BindLocalized(label, labelKey, labelItalianFallback);
		SetOptionsCellSize(label, 200f, 1f);

		Button downButton = CreateButton(name + " Volume Down", row, font, "-");
		downButton.onClick.AddListener(decrease);
		SetOptionsCellSize(downButton, 84f, 0f);

		valueText = CreateText(name + " Volume Value", row, font, 19, FontStyle.Bold, TextAnchor.MiddleCenter);
		valueText.color = OptionsGold;
		SetOptionsCellSize(valueText, 130f, 0f);
		KeepTextDynamic(valueText);

		Button upButton = CreateButton(name + " Volume Up", row, font, "+");
		upButton.onClick.AddListener(increase);
		SetOptionsCellSize(upButton, 84f, 0f);

		muteButton = CreateButton(name + " Mute", row, font, "MUTE");
		muteButton.onClick.AddListener(toggleMute);
		SetOptionsCellSize(muteButton, 160f, 0f);
		muteButtonText = muteButton.GetComponentInChildren<Text>();
		KeepTextDynamic(muteButtonText);
	}

	private void CreateLanguageRow(Transform parent, Font font)
	{
		RectTransform row = CreateOptionsRow("Language Row", parent, OptionsRowHeight);

		Text label = CreateText("Language Label", row, font, 17, FontStyle.Bold, TextAnchor.MiddleLeft);
		label.color = OptionsLabel;
		label.text = GameText.GetOrFallbackSilent(GameTextKeys.Options.LanguageLabel, "LINGUA DI GIOCO");
		EditableRuntimeText.BindLocalized(label, GameTextKeys.Options.LanguageLabel, "LINGUA DI GIOCO");
		SetOptionsCellSize(label, 260f, 1f);

		languageComboButton = CreateButton("Language Combo", row, font, string.Empty);
		ApplyBattleButtonVariant(languageComboButton, MmoUiTheme.ButtonVariant.Arcane);
		languageComboButton.onClick.AddListener(ToggleLanguageDropdown);
		languageComboRect = (RectTransform)languageComboButton.transform;
		SetOptionsCellSize(languageComboButton, 320f, 0f);

		languageComboLabel = languageComboButton.GetComponentInChildren<Text>();
		languageComboLabel.alignment = TextAnchor.MiddleLeft;
		SetRect(languageComboLabel.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.8f, 0.88f));
		KeepTextDynamic(languageComboLabel);

		Image caret = CreateImage("Language Combo Caret", languageComboButton.transform, OptionsGold);
		caret.sprite = MmoUiTheme.GetCaretSprite();
		caret.preserveAspect = true;
		SetRect(caret.rectTransform, new Vector2(0.84f, 0.38f), new Vector2(0.93f, 0.62f));
	}

	private void CreateOptionsShortcutRow(Transform parent, Font font)
	{
		RectTransform row = CreateOptionsRow("Options Shortcut Row", parent, OptionsActionRowHeight);

		Button logButton = CreateOptionsButton(
			"Options Open Log",
			row,
			font,
			GameTextKeys.Options.Log,
			"REGISTRO",
			MmoUiTheme.ButtonVariant.Gold);
		logButton.onClick.AddListener(OpenLogFromOptions);
		SetOptionsCellSize(logButton, 220f, 1f);

		Button auraButton = CreateOptionsButton(
			"Options Open Aura Codex",
			row,
			font,
			GameTextKeys.Options.AuraCodex,
			"AURE",
			MmoUiTheme.ButtonVariant.Arcane);
		auraButton.onClick.AddListener(OpenAuraCodexFromOptions);
		SetOptionsCellSize(auraButton, 220f, 1f);

		optionsPrivacyButton = CreateOptionsButton(
			"Options Privacy",
			row,
			font,
			GameTextKeys.Options.Privacy,
			"PRIVACY",
			MmoUiTheme.ButtonVariant.Violet);
		optionsPrivacyButton.onClick.AddListener(ShowPrivacyOptions);
		SetOptionsCellSize(optionsPrivacyButton, 220f, 1f);
		optionsPrivacyButton.gameObject.SetActive(false);
	}

	private void CreateOptionsActionRow(Transform parent, Font font)
	{
		RectTransform row = CreateOptionsRow("Options Action Row", parent, OptionsActionRowHeight);

		optionsMainMenuButton = CreateOptionsButton(
			"Options Main Menu",
			row,
			font,
			GameTextKeys.Options.MainMenu,
			"MENU",
			MmoUiTheme.ButtonVariant.Violet);
		optionsMainMenuButton.onClick.AddListener(ReturnToMainMenuFromOptions);
		SetOptionsCellSize(optionsMainMenuButton, 220f, 1f);
		optionsMainMenuButtonText = optionsMainMenuButton.GetComponentInChildren<Text>();
		KeepTextDynamic(optionsMainMenuButtonText);

		Button logoutButton = CreateOptionsButton(
			"Options Logout",
			row,
			font,
			GameTextKeys.Options.Logout,
			"LOGOUT",
			MmoUiTheme.ButtonVariant.Gold);
		logoutButton.onClick.AddListener(LogoutFromOptions);
		SetOptionsCellSize(logoutButton, 220f, 1f);

		Button closeButton = CreateOptionsButton(
			"Close Options",
			row,
			font,
			GameTextKeys.Common.Close,
			"CHIUDI",
			MmoUiTheme.ButtonVariant.Crimson);
		closeButton.onClick.AddListener(CloseOptionsPanel);
		SetOptionsCellSize(closeButton, 220f, 1f);
	}

	// ---------------------------------------------------------------- lingua

	/// <summary>
	/// La tendina vive su un suo strato a tutto schermo: deve poter uscire dai bordi
	/// del pannello, e un click fuori dalla lista deve chiuderla senza chiudere le opzioni.
	/// </summary>
	private void CreateLanguageDropdown(Transform parent, Font font)
	{
		Image blocker = CreateImage("Language Dropdown Overlay", parent, new Color(0f, 0f, 0f, 0.01f));
		blocker.raycastTarget = true;
		Stretch(blocker.rectTransform);
		languageDropdownOverlay = blocker.gameObject;
		Button blockerButton = languageDropdownOverlay.AddComponent<Button>();
		blockerButton.transition = Selectable.Transition.None;
		blockerButton.onClick.AddListener(CloseLanguageDropdown);
		Canvas overlayCanvas = languageDropdownOverlay.AddComponent<Canvas>();
		overlayCanvas.overrideSorting = true;
		overlayCanvas.sortingOrder = 985;
		languageDropdownOverlay.AddComponent<GraphicRaycaster>();

		Image list = CreateImage("Language Dropdown List", languageDropdownOverlay.transform, new Color(0.012f, 0.02f, 0.03f, 0.99f));
		StylePanel(list);
		list.raycastTarget = true;
		languageDropdownList = list.rectTransform;
		languageDropdownList.anchorMin = new Vector2(0.5f, 0.5f);
		languageDropdownList.anchorMax = new Vector2(0.5f, 0.5f);

		VerticalLayoutGroup listColumn = list.gameObject.AddComponent<VerticalLayoutGroup>();
		listColumn.padding = new RectOffset(10, 10, 10, 10);
		listColumn.spacing = 6f;
		listColumn.childAlignment = TextAnchor.UpperCenter;
		listColumn.childControlWidth = true;
		listColumn.childControlHeight = true;
		listColumn.childForceExpandWidth = true;
		listColumn.childForceExpandHeight = false;

		IReadOnlyList<GameText.LanguageOption> languages = GameText.AvailableLanguages();
		languageComboItems.Clear();
		for (int index = 0; index < languages.Count; index++)
		{
			GameText.LanguageOption language = languages[index];
			Button item = CreateButton("Language Item " + language.Code, languageDropdownList, font, language.DisplayName);
			ApplyBattleButtonVariant(item, MmoUiTheme.ButtonVariant.Arcane);
			string code = language.Code;
			item.onClick.AddListener(() => SelectLanguage(code));
			SetOptionsCellSize(item, 0f, 1f, OptionsLanguageItemHeight);

			Text itemLabel = item.GetComponentInChildren<Text>();
			itemLabel.alignment = TextAnchor.MiddleLeft;
			SetRect(itemLabel.rectTransform, new Vector2(0.1f, 0.12f), new Vector2(0.94f, 0.88f));
			KeepTextDynamic(itemLabel);

			Image marker = CreateImage("Language Item Marker", item.transform, OptionsGold);
			marker.sprite = MmoUiTheme.GetSolidCircleSprite();
			marker.preserveAspect = true;
			SetRect(marker.rectTransform, new Vector2(0.035f, 0.4f), new Vector2(0.075f, 0.6f));
			languageComboItems.Add(new LanguageComboItem(code, marker));
		}

		languageDropdownHeight = languages.Count * OptionsLanguageItemHeight
			+ Mathf.Max(0, languages.Count - 1) * listColumn.spacing
			+ listColumn.padding.top
			+ listColumn.padding.bottom;

		// Finché nel progetto c'è una sola Locale la tendina mostra una voce sola:
		// resta apribile, così si vede qual è la lingua attiva.
		languageComboButton.interactable = languages.Count > 0;
		languageDropdownOverlay.SetActive(false);
	}

	private void ToggleLanguageDropdown()
	{
		if (languageDropdownOverlay == null)
			return;

		if (languageDropdownOverlay.activeSelf)
		{
			CloseLanguageDropdown();
			return;
		}

		languageDropdownOverlay.SetActive(true);
		languageDropdownOverlay.transform.SetAsLastSibling();
		PositionLanguageDropdown();
	}

	private void CloseLanguageDropdown()
	{
		if (languageDropdownOverlay != null)
			languageDropdownOverlay.SetActive(false);
	}

	private void PositionLanguageDropdown()
	{
		if (languageDropdownList == null || languageComboRect == null || languageDropdownOverlay == null)
			return;

		// La tendina si ancora al bordo del selettore: va misurato dopo che il layout
		// del pannello si e' assestato, altrimenti si legge la posizione del frame prima.
		Canvas.ForceUpdateCanvases();

		RectTransform overlayRect = (RectTransform)languageDropdownOverlay.transform;
		Vector3[] corners = new Vector3[4];
		languageComboRect.GetWorldCorners(corners);
		Vector2 bottomLeft = overlayRect.InverseTransformPoint(corners[0]);
		Vector2 topRight = overlayRect.InverseTransformPoint(corners[2]);

		float width = Mathf.Max(260f, topRight.x - bottomLeft.x);
		languageDropdownList.sizeDelta = new Vector2(width, languageDropdownHeight);

		// Se sotto non ci sta, la tendina si apre verso l'alto.
		float overlayBottom = -overlayRect.rect.height * 0.5f;
		bool openDownwards = bottomLeft.y - 6f - languageDropdownHeight >= overlayBottom;
		languageDropdownList.pivot = new Vector2(0f, openDownwards ? 1f : 0f);
		languageDropdownList.anchoredPosition = new Vector2(
			bottomLeft.x,
			openDownwards ? bottomLeft.y - 6f : topRight.y + 6f);
	}

	private void SelectLanguage(string localeCode)
	{
		CloseLanguageDropdown();
		if (string.Equals(localeCode, GameText.CurrentLocaleCode, StringComparison.OrdinalIgnoreCase))
			return;

		PlayGenericButtonClickSfx();
		if (!GameText.TrySelectLocale(localeCode))
			return;

		// TrySelectLocale scatena LocaleChanged, ma se la locale era gia' quella
		// attiva l'evento non arriva: l'etichetta va comunque riallineata.
		RefreshLanguageOptionsUi();
	}

	private void RefreshLanguageOptionsUi()
	{
		string current = GameText.CurrentLocaleCode;
		if (languageComboLabel != null)
		{
			string name = GameText.CurrentLanguageName;
			languageComboLabel.text = string.IsNullOrWhiteSpace(name)
				? GameText.GetOrFallbackSilent(GameTextKeys.Options.LanguageUnavailable, "NON DISPONIBILE")
				: name;
		}

		for (int index = 0; index < languageComboItems.Count; index++)
		{
			LanguageComboItem item = languageComboItems[index];
			if (item.Marker != null)
				item.Marker.gameObject.SetActive(string.Equals(item.Code, current, StringComparison.OrdinalIgnoreCase));
		}
	}

	// --------------------------------------------------------------- helper

	private static RectTransform CreateOptionsRow(string name, Transform parent, float height)
	{
		GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		row.transform.SetParent(parent, false);

		HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = 12f;
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = true;

		LayoutElement element = row.GetComponent<LayoutElement>();
		element.minHeight = height;
		element.preferredHeight = height;
		return (RectTransform)row.transform;
	}

	private static Button CreateOptionsButton(
		string name,
		Transform parent,
		Font font,
		string key,
		string italianFallback,
		MmoUiTheme.ButtonVariant variant)
	{
		Button button = CreateButton(name, parent, font, GameText.GetOrFallbackSilent(key, italianFallback));
		ApplyBattleButtonVariant(button, variant);

		Text label = button.GetComponentInChildren<Text>();
		if (label != null)
			EditableRuntimeText.BindLocalized(label, key, italianFallback);
		return button;
	}

	private static void SetOptionsCellSize(
		Component target,
		float preferredWidth,
		float flexibleWidth,
		float preferredHeight = 0f)
	{
		LayoutElement element = target.gameObject.GetComponent<LayoutElement>();
		if (element == null)
			element = target.gameObject.AddComponent<LayoutElement>();

		element.preferredWidth = preferredWidth > 0f ? preferredWidth : -1f;
		element.minWidth = preferredWidth > 0f ? Mathf.Min(preferredWidth, 72f) : -1f;
		element.flexibleWidth = flexibleWidth;
		if (preferredHeight > 0f)
		{
			element.minHeight = preferredHeight;
			element.preferredHeight = preferredHeight;
		}
	}

	/// <summary>
	/// Il testo di questo controllo lo scriviamo noi a runtime: senza questa marcatura
	/// il binding lo riporterebbe al valore catturato alla creazione a ogni cambio lingua.
	/// </summary>
	private static void KeepTextDynamic(Text target)
	{
		if (target != null)
			EditableRuntimeText.BindLocalized(target, string.Empty, string.Empty);
	}

	/// <summary>Il pannello e' un modale centrato: cambia solo quanto e' largo.</summary>
	private void ApplyOptionsPanelLayout(bool portrait)
	{
		if (optionsPanelRect == null)
			return;

		optionsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
		optionsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
		optionsPanelRect.pivot = new Vector2(0.5f, 0.5f);
		optionsPanelRect.anchoredPosition = Vector2.zero;

		// Al primo passaggio il canvas puo' non avere ancora una larghezza: in quel
		// caso resta la misura di riferimento, che il layout successivo corregge.
		float available = optionsPanelRect.parent is RectTransform parentRect ? parentRect.rect.width : 0f;
		float width = portrait ? 960f : 900f;
		if (available > 1f)
		{
			width = Mathf.Min(width, available * (portrait ? 0.94f : 0.52f));
		}
		optionsPanelRect.sizeDelta = new Vector2(width, optionsPanelRect.sizeDelta.y);

		CloseLanguageDropdown();
	}
}
}
