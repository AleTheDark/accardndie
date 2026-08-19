using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.UiKit;
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
	private enum PrimaryActionLabel
	{
		Continue,
		Advance,
		RetryRoom
	}

	private static void SetPrimaryActionLabel(Text target, PrimaryActionLabel label)
	{
		if ((Object)(object)target == (Object)null)
			return;

		string key = label switch
		{
			PrimaryActionLabel.Advance => GameTextKeys.Campaign.Advance,
			PrimaryActionLabel.RetryRoom => GameTextKeys.Campaign.RetryRoom,
			_ => GameTextKeys.Common.Continue
		};
		string italianFallback = label switch
		{
			PrimaryActionLabel.Advance => "VAI AVANTI!",
			PrimaryActionLabel.RetryRoom => "RIPROVA STANZA",
			_ => "CONTINUA"
		};
		string fallback = GameText.GetOrFallbackSilent(key, italianFallback);
		target.text = fallback;
		global::AccardND.Battlefield.EditableRuntimeText.BindLocalized(target, key, fallback);
	}

	private static void SetLocalizedText(Text target, string key, string italianFallback)
	{
		if ((Object)(object)target == (Object)null)
			return;

		target.text = GameText.GetOrFallbackSilent(key, italianFallback);
		EditableRuntimeText.BindLocalized(target, key, italianFallback);
	}

	private static void SetLocalizedButtonLabel(Button button, string key, string italianFallback)
	{
		if ((Object)(object)button == (Object)null)
			return;

		SetLocalizedText(button.GetComponentInChildren<Text>(), key, italianFallback);
	}

	private static Canvas CreateCanvas()
	{
		GameObject val = new GameObject("Battle Canvas", new Type[4]
		{
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster),
			typeof(AdaptiveCanvasScaler)
		});
		Canvas component = val.GetComponent<Canvas>();
		component.renderMode = (RenderMode)0;
		component.sortingOrder = 100;
		CanvasScaler component2 = val.GetComponent<CanvasScaler>();
		component2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component2.referenceResolution = new Vector2(1920f, 1080f);
		component2.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		component2.matchWidthOrHeight = 0.5f;
		return component;
	}

	private static void EnsureEventSystem()
	{
		if (!((Object)(object)Object.FindAnyObjectByType<EventSystem>() != (Object)null))
		{
			new GameObject("EventSystem", new Type[2]
			{
				typeof(EventSystem),
				typeof(InputSystemUIInputModule)
			}).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
		}
	}

	private static RectTransform CreateCardRow(string name, Transform parent, Vector2 anchor)
		=> Ui.CreateCardRow(name, parent, anchor);

	private static Image CreateImage(string name, Transform parent, Color color)
		=> Ui.CreateImage(name, parent, color);

	private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor alignment)
	{
		GameObject val = new GameObject(name, new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Text)
		});
		val.transform.SetParent(parent, false);
		Text component = val.GetComponent<Text>();
		int responsiveSize = ResponsiveTextSize(size);
		component.font = font;
		component.fontSize = responsiveSize;
		component.fontStyle = style;
		component.alignment = alignment;
		component.color = Color.white;
		component.raycastTarget = false;
		component.resizeTextForBestFit = true;
		component.resizeTextMinSize = ResponsiveTextMinSize(responsiveSize);
		component.resizeTextMaxSize = responsiveSize;
		global::AccardND.Battlefield.EditableRuntimeText.Bind(component);
		return component;
	}

	private static int ResponsiveTextSize(int size)
		=> Ui.ResponsiveTextSize(size);

	private static int ResponsiveTextMinSize(int size)
		=> Ui.ResponsiveTextMinSize(size);

	private static Button CreateButton(string name, Transform parent, Font font, string label)
	{
		Image image = CreateImage(name, parent, Color.white);
		MmoUiTheme.ButtonVariant variant = ResolveBattleButtonVariant(name, label);
		image.sprite = MmoUiTheme.GetButtonSprite(variant);
		image.type = Image.Type.Sliced;
		image.raycastTarget = true;
		Button button = ((Component)image).gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		MmoUiTheme.ApplyButtonColors(button);
		MmoUiTheme.AddMotion(button);
		Text text = CreateText("Label", ((Component)image).transform, font, 20, (FontStyle)1, (TextAnchor)4);
		text.text = label;
		global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: label);
		MmoUiTheme.StyleAsTitle(text);
		text.color = Color.Lerp(Color.white, MmoUiTheme.AccentOf(variant), 0.16f);
		Outline labelOutline = ((Component)text).gameObject.AddComponent<Outline>();
		labelOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
		labelOutline.effectDistance = new Vector2(1.5f, -1.5f);
		Stretch(text.rectTransform, 6f);
        if (MmoUiTheme.IsBackButton(name, label))
            MmoUiTheme.ApplyBackButtonStyle(button, text);
        else if (MmoUiTheme.IsLightButton(name, label))
            MmoUiTheme.ApplyLightButtonStyle(button, text);
		return button;
	}

	private static MmoUiTheme.ButtonVariant ResolveBattleButtonVariant(string name, string label)
	{
		string semanticName = (name ?? string.Empty).ToUpperInvariant();
		if (semanticName.Contains("CANCEL") || semanticName.Contains("CLOSE") || semanticName.Contains("BACK") || semanticName.Contains("RETURN"))
			return MmoUiTheme.ButtonVariant.Crimson;
		if (semanticName.Contains("CONFIRM") || semanticName.Contains("SAVE") || semanticName.Contains("START") || semanticName.Contains("CONTINUE"))
			return MmoUiTheme.ButtonVariant.Emerald;
		if (semanticName.Contains("DRAFT") || semanticName.Contains("PROFILE") || semanticName.Contains("PVP") || semanticName.Contains("MULTIPLAYER"))
			return MmoUiTheme.ButtonVariant.Violet;
		if (semanticName.Contains("BUILDER") || semanticName.Contains("BAG") || semanticName.Contains("LOADOUT"))
			return MmoUiTheme.ButtonVariant.Gold;

		string value = ((name ?? string.Empty) + " " + (label ?? string.Empty)).ToUpperInvariant();
		if (value.Contains("ANNULLA") || value.Contains("CANCEL") || value.Contains("CLOSE") || value.Contains("CHIUDI") || value.Contains("INDIETRO"))
			return MmoUiTheme.ButtonVariant.Crimson;
		if (value.Contains("START CAMPAIGN") || value.Contains("INIZIA"))
			return MmoUiTheme.ButtonVariant.Violet;
		if (value.Contains("CONFERMA") || value.Contains("SALVA") || value.Contains("OK") || value.Contains("START") || value.Contains("CONTINUA"))
			return MmoUiTheme.ButtonVariant.Emerald;
		if (value.Contains("DRAFT") || value.Contains("PROFILO") || value.Contains("PVP") || value.Contains("MULTIPLAYER"))
			return MmoUiTheme.ButtonVariant.Violet;
		if (value.Contains("BUILDER") || value.Contains("BORSA") || value.Contains("LOADOUT"))
			return MmoUiTheme.ButtonVariant.Gold;
		return MmoUiTheme.ButtonVariant.Arcane;
	}

	private static void ApplyBattleButtonVariant(Button button, MmoUiTheme.ButtonVariant variant)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = ((Component)button).GetComponent<Image>();
		if ((Object)(object)image != (Object)null)
		{
			image.sprite = MmoUiTheme.GetButtonSprite(variant);
			image.type = Image.Type.Sliced;
			image.preserveAspect = false;
			image.color = Color.white;
			button.targetGraphic = image;
		}

		MmoUiTheme.ApplyButtonColors(button);
		Text label = ((Component)button).GetComponentInChildren<Text>(true);
		if ((Object)(object)label != (Object)null)
		{
			MmoUiTheme.StyleAsTitle(label);
			label.fontSize = ResponsiveTextSize(20);
			label.resizeTextMaxSize = label.fontSize;
			label.resizeTextMinSize = ResponsiveTextMinSize(label.fontSize);
			label.alignment = TextAnchor.MiddleCenter;
			label.color = Color.Lerp(Color.white, MmoUiTheme.AccentOf(variant), 0.16f);
			Stretch(label.rectTransform, 6f);
		}
	}

	private static Button CreateImageButton(string name, Transform parent, Font font, Sprite sprite, string label)
	{
		switch (name)
		{
		case "Buy Blind Random":
		case "Buy Selected Class":
		case "Buy Selected Strength":
			if (!string.IsNullOrWhiteSpace(label) && label.Any(char.IsDigit))
			{
				label = new string(label.Where(char.IsDigit).ToArray());
			}
			break;
		}
		Image image = CreateImage(name, parent, Color.white);
		image.sprite = sprite;
		image.preserveAspect = true;
		image.raycastTarget = true;
		Button button = ((Component)image).gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
		colors.pressedColor = new Color(0.78f, 0.86f, 0.92f);
		colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.72f);
		colors.colorMultiplier = 1f;
		button.colors = colors;
		if (string.IsNullOrWhiteSpace(label))
		{
			return button;
		}
		bool flag = IsDeckBuilderChoiceButton(name);
		bool flag2 = IsMerchantImageButton(name);
		Text text = CreateText("Label", ((Component)image).transform, font, 18, (FontStyle)1, (TextAnchor)4);
		text.text = label;
		global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: label);
		text.color = Color.white;
		text.horizontalOverflow = flag2 ?HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
		text.verticalOverflow = (VerticalWrapMode)0;
		if (flag)
		{
			text.fontSize = name == "Buy Selected Class" ?22 : 18;
			text.resizeTextMinSize = name == "Buy Selected Class" ?12 : 10;
			text.resizeTextMaxSize = name == "Buy Selected Class" ?22 : 18;
		}
		if (flag2)
		{
			text.fontSize = 18;
			text.resizeTextMinSize = 10;
			text.resizeTextMaxSize = 18;
		}
		Outline outline = ((Component)text).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		SetRect(text.rectTransform, flag ?(name == "Buy Selected Class" ?new Vector2(0.06f, 0.76f) : new Vector2(0.08f, 0.78f)) : (flag2 ?new Vector2(-0.08f, -0.18f) : new Vector2(0.72f, 0.03f)), flag ?(name == "Buy Selected Class" ?new Vector2(0.94f, 0.96f) : new Vector2(0.92f, 0.98f)) : (flag2 ?new Vector2(1.08f, 0.12f) : new Vector2(0.95f, 0.23f)));
		return button;
	}

	private static bool IsDeckBuilderChoiceButton(string name)
	{
		switch (name)
		{
		default:
			return name == "Buy Selected Strength";
		case "Buy Blind Random":
		case "Cycle Class":
		case "Buy Selected Class":
			return true;
		}
	}

	private static bool IsMerchantImageButton(string name)
	{
		return name.StartsWith("Merchant ", StringComparison.Ordinal);
	}

	private static Button CreateTransparentButton(string name, Transform parent)
		=> Ui.CreateTransparentButton(name, parent);

	private static void StylePanel(Image image)
	{
		image.sprite = GetRuntimePanelSprite();
		image.type = Image.Type.Sliced;
		image.color = new Color(1f, 1f, 1f, image.color.a);
	}

	private static Sprite GetRuntimePanelSprite()
	{
		return MmoUiTheme.GetPanelSprite();
	}

	private static Sprite GetHelpAuraSprite()
		=> ProceduralSprites.HelpAura();

	private static void Stretch(RectTransform rect, float padding = 0f)
		=> Ui.Stretch(rect, padding);

	private static AspectRatioFitter ConfigureFittedBackground(Image image, Sprite sprite, float fallbackAspectRatio)
		=> Ui.ConfigureFittedBackground(image, sprite, fallbackAspectRatio);

	private static void SetRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
		=> Ui.SetRect(rect, minimum, maximum);
}
}
