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
	private static Canvas CreateCanvas()
	{
		GameObject val = new GameObject("Battle Canvas", new Type[3]
		{
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster)
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
	{
		GameObject val = new GameObject(name, new Type[2]
		{
			typeof(RectTransform),
			typeof(HorizontalLayoutGroup)
		});
		val.transform.SetParent(parent, false);
		RectTransform val2 = (RectTransform)val.transform;
		val2.anchorMin = anchor;
		val2.anchorMax = anchor;
		val2.pivot = new Vector2(0.5f, 0.5f);
		val2.sizeDelta = new Vector2(1050f, 285f);
		HorizontalLayoutGroup component = val.GetComponent<HorizontalLayoutGroup>();
		component.spacing = 34f;
		component.childAlignment = (TextAnchor)4;
		component.childControlWidth = true;
		component.childControlHeight = true;
		component.childForceExpandWidth = true;
		component.childForceExpandHeight = true;
		return val2;
	}

	private static Image CreateImage(string name, Transform parent, Color color)
	{
		GameObject val = new GameObject(name, new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image)
		});
		val.transform.SetParent(parent, false);
		Image component = val.GetComponent<Image>();
		component.color = color;
		component.raycastTarget = false;
		return component;
	}

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
		component.font = font;
		component.fontSize = size;
		component.fontStyle = style;
		component.alignment = alignment;
		component.color = Color.white;
		component.raycastTarget = false;
		component.resizeTextForBestFit = true;
		component.resizeTextMinSize = 10;
		component.resizeTextMaxSize = size;
		return component;
	}

	private static Button CreateButton(string name, Transform parent, Font font, string label)
	{
		Image image = CreateImage(name, parent, new Color(0.04f, 0.42f, 0.48f, 0.98f));
		StylePanel(image);
		image.raycastTarget = true;
		Button button = ((Component)image).gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
		colors.pressedColor = new Color(0.72f, 0.9f, 0.92f);
		colors.disabledColor = new Color(0.38f, 0.48f, 0.5f, 0.72f);
		colors.colorMultiplier = 1f;
		button.colors = colors;
		Text text = CreateText("Label", ((Component)image).transform, font, 20, (FontStyle)1, (TextAnchor)4);
		text.text = label;
		Stretch(text.rectTransform);
		return button;
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
	{
		Image image = CreateImage(name, parent, Color.clear);
		image.raycastTarget = true;
		Button button = ((Component)image).gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
		colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
		colors.disabledColor = Color.clear;
		colors.colorMultiplier = 1f;
		button.colors = colors;
		return button;
	}

	private static void StylePanel(Image image)
	{
		image.sprite = GetRuntimePanelSprite();
		image.type = Image.Type.Sliced;
	}

	private static Sprite GetRuntimePanelSprite()
	{
		if ((Object)(object)runtimePanelSprite != (Object)null)
		{
			return runtimePanelSprite;
		}
		Texture2D val = new Texture2D(32, 32, (TextureFormat)4, false)
		{
			name = "Runtime Rounded UI Panel",
			filterMode = (FilterMode)1,
			wrapMode = (TextureWrapMode)1,
			hideFlags = (HideFlags)61
		};
		Color32[] array = (Color32[])(object)new Color32[1024];
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				float num = Mathf.Max(7f - (float)j, (float)j - 24f);
				float num2 = Mathf.Max(7f - (float)i, (float)i - 24f);
				float num3 = Mathf.Max(0f, num);
				float num4 = Mathf.Max(0f, num2);
				float num5 = Mathf.Sqrt(num3 * num3 + num4 * num4);
				byte b = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(7.5f - num5));
				array[i * 32 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, b);
			}
		}
		val.SetPixels32(array);
		val.Apply(false, true);
		runtimePanelSprite = Sprite.Create(val, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f, 0u, (SpriteMeshType)0, new Vector4(9f, 9f, 9f, 9f));
		((Object)runtimePanelSprite).name = "Runtime Rounded UI Panel";
		((Object)runtimePanelSprite).hideFlags = (HideFlags)61;
		return runtimePanelSprite;
	}

	private static Sprite GetHelpAuraSprite()
	{
		if ((Object)(object)helpAuraSprite != (Object)null)
		{
			return helpAuraSprite;
		}
		Texture2D val = new Texture2D(128, 128, (TextureFormat)4, false)
		{
			name = "help_aura",
			filterMode = (FilterMode)1,
			wrapMode = (TextureWrapMode)1,
			hideFlags = (HideFlags)61
		};
		Color32[] array = (Color32[])(object)new Color32[16384];
		Vector2 val2 = default(Vector2);
		val2 = new Vector2(63.5f, 63.5f);
		for (int i = 0; i < 128; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				float num = Vector2.Distance(new Vector2((float)j, (float)i), val2) / 64f;
				float num2 = Mathf.Exp(0f - Mathf.Pow((num - 0.72f) / 0.15f, 2f));
				float num3 = Mathf.Clamp01(1f - num) * 0.18f;
				byte b = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(num2 * 0.75f + num3));
				array[i * 128 + j] = new Color32(byte.MaxValue, (byte)205, (byte)48, b);
			}
		}
		val.SetPixels32(array);
		val.Apply(false, true);
		helpAuraSprite = Sprite.Create(val, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
		((Object)helpAuraSprite).name = "help_aura";
		((Object)helpAuraSprite).hideFlags = (HideFlags)61;
		return helpAuraSprite;
	}

	private static void Stretch(RectTransform rect, float padding = 0f)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(padding, padding);
		rect.offsetMax = new Vector2(0f - padding, 0f - padding);
	}

	private static AspectRatioFitter ConfigureFittedBackground(Image image, Sprite sprite, float fallbackAspectRatio)
	{
		image.preserveAspect = true;
		AspectRatioFitter obj = ((Component)image).GetComponent<AspectRatioFitter>() ?? ((Component)image).gameObject.AddComponent<AspectRatioFitter>();
		obj.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
		float aspectRatio;
		if (!((Object)(object)sprite != (Object)null))
		{
			aspectRatio = fallbackAspectRatio;
		}
		else
		{
			Rect rect = sprite.rect;
			float width = rect.width;
			rect = sprite.rect;
			aspectRatio = width / rect.height;
		}
		obj.aspectRatio = aspectRatio;
		return obj;
	}

	private static void SetRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
	{
		rect.anchorMin = minimum;
		rect.anchorMax = maximum;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}
}
}
