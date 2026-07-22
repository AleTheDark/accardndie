using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const string HintSeenPrefsPrefix = "AccardHint_";
	private const string FleckMascotResource = "UI/Mascots/Fleck";
	private const bool FleckMouthDebug = false;

	// Chiavi degli hint contestuali "prima volta".
	private const string HintKeyCampaignIntro = "campaign_intro";
	private const string HintKeyRoomChoice = "room_choice";
	private const string HintKeyInitialDraft = "initial_draft";
	private const string HintKeyFormationDraft = "formation_draft";
	private const string HintKeyDeploymentInitiative = "deployment_initiative";
	private const string HintKeyCombat = "combat";
	private const string HintKeyMerchant = "merchant";
	private const string HintKeyFirstAura = "first_aura";
	private const string HintKeyFirstDefeat = "first_defeat";
	private const string HintKeyLevelUpVigor = "level_up_vigor";

	private readonly struct HintContent
	{
		public string Title { get; }

		public string Body { get; }

		public string[] Pages { get; }

		public HintContent(string title, string body)
		{
			Title = title;
			Body = body;
			Pages = null;
		}

		public HintContent(string title, params string[] pages)
		{
			Title = title;
			Body = pages != null && pages.Length > 0 ? pages[0] : string.Empty;
			Pages = pages;
		}
	}

	private readonly Queue<HintContent> pendingHints = new Queue<HintContent>();

	private GameObject hintPanel;

	private Text hintTitleText;

	private Text hintBodyText;

	private Text hintButtonText;

	private RectTransform hintBubbleRect;

	private RectTransform hintDialogRect;

	private Image hintMascotMouthImage;

	private Image hintMascotMouthDebugImage;

	private Image hintMascotHaloImage;

	private bool hintActive;

	private Coroutine hintTypingCoroutine;

	private Coroutine hintMouthCoroutine;

	private Coroutine hintHaloCoroutine;

	private Coroutine hintPopupTransitionCoroutine;

	private string[] hintCurrentPages;

	private int hintCurrentPageIndex;

	private string hintCurrentFullPage = string.Empty;

	private bool hintTypingActive;

	private bool hintClosing;

	private bool hintPausedTime;

	private float hintPreviousTimeScale = 1f;

	private static Sprite fleckMouthSprite;

	private static Sprite fleckMouthClosedSprite;

	private static Sprite fleckHaloSprite;

	private void OnDisable()
	{
		ResumeGameAfterHint();
	}

	private void CreateHintOverlay(Transform parent, Font font)
	{
		Image overlay = CreateImage("Hint Overlay", parent, new Color(0f, 0f, 0f, 0.74f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		hintPanel = ((Component)overlay).gameObject;
		Canvas canvas = hintPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 970;
		hintPanel.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Hint Dialog", ((Component)overlay).transform, new Color(0.01f, 0.018f, 0.028f, 0.98f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		hintDialogRect = dialog.rectTransform;
		SetRect(hintDialogRect, new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.76f));

		Image mascot = CreateImage("Fleck Mascot", ((Component)dialog).transform, Color.white);
		mascot.sprite = LoadSpriteResource(FleckMascotResource);
		mascot.preserveAspect = true;
		SetRect(mascot.rectTransform, new Vector2(0.68f, 0.18f), new Vector2(0.97f, 0.94f));

		hintMascotHaloImage = CreateImage("Fleck Halo", ((Component)mascot).transform, new Color(1f, 0.94f, 0.34f, 0.98f));
		hintMascotHaloImage.sprite = GetFleckHaloSprite();
		hintMascotHaloImage.preserveAspect = true;
		SetRect(hintMascotHaloImage.rectTransform, new Vector2(0.25f, 0.77f), new Vector2(0.59f, 0.89f));

		hintMascotMouthImage = CreateImage("Fleck Mouth", ((Component)dialog).transform, Color.white);
		hintMascotMouthImage.sprite = GetFleckMouthClosedSprite();
		hintMascotMouthImage.preserveAspect = true;
		SetRect(hintMascotMouthImage.rectTransform, new Vector2(0.685f, 0.59f), new Vector2(0.792f, 0.682f));
		((Transform)hintMascotMouthImage.rectTransform).SetAsLastSibling();
		hintMascotMouthImage.gameObject.SetActive(false);

		hintMascotMouthDebugImage = CreateImage("Fleck Mouth Debug Marker", ((Component)dialog).transform, new Color(1f, 0f, 0f, 0.72f));
		hintMascotMouthDebugImage.sprite = GetFleckMouthClosedSprite();
		hintMascotMouthDebugImage.preserveAspect = true;
		SetRect(hintMascotMouthDebugImage.rectTransform, new Vector2(0.675f, 0.58f), new Vector2(0.802f, 0.692f));
		((Transform)hintMascotMouthDebugImage.rectTransform).SetAsLastSibling();
		hintMascotMouthDebugImage.gameObject.SetActive(false);

		Image bubble = CreateImage("Fleck Speech Bubble", ((Component)dialog).transform, new Color(0.94f, 0.98f, 0.92f, 0.96f));
		bubble.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
		bubble.type = Image.Type.Sliced;
		hintBubbleRect = bubble.rectTransform;
		SetRect(hintBubbleRect, new Vector2(0.06f, 0.43f), new Vector2(0.67f, 0.62f));

		hintTitleText = CreateText("Hint Title", ((Component)dialog).transform, font, 34, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(hintTitleText);
		hintTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		hintTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
		hintTitleText.verticalOverflow = VerticalWrapMode.Truncate;
		hintTitleText.resizeTextMinSize = Mathf.Max(hintTitleText.resizeTextMinSize, 26);
		SetRect(hintTitleText.rectTransform, new Vector2(0.08f, 0.74f), new Vector2(0.66f, 0.94f));

		hintBodyText = CreateText("Hint Body", ((Component)bubble).transform, font, 24, (FontStyle)0, TextAnchor.UpperLeft);
		hintBodyText.color = Color.white;
		hintBodyText.supportRichText = true;
		Outline bodyOutline = ((Component)hintBodyText).gameObject.AddComponent<Outline>();
		bodyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
		bodyOutline.effectDistance = new Vector2(1.5f, -1.5f);
		hintBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		hintBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		hintBodyText.resizeTextForBestFit = true;
		hintBodyText.resizeTextMinSize = 18;
		hintBodyText.resizeTextMaxSize = hintBodyText.fontSize;
		SetRect(hintBodyText.rectTransform, new Vector2(0.09f, 0.08f), new Vector2(0.92f, 0.9f));

		Button gotItButton = CreateButton("Hint Dismiss", ((Component)dialog).transform, font, "HO CAPITO");
		((UnityEvent)gotItButton.onClick).AddListener(new UnityAction(DismissHint));
		SetRect((RectTransform)((Component)gotItButton).transform, new Vector2(0.32f, 0.07f), new Vector2(0.68f, 0.23f));
		hintButtonText = ((Component)gotItButton).GetComponentInChildren<Text>();

		hintPanel.SetActive(false);
	}

	private static bool HasSeenHint(string key)
	{
		return PlayerPrefs.GetInt(HintSeenPrefsPrefix + key, 0) != 0;
	}

	private static void MarkHintSeen(string key)
	{
		PlayerPrefs.SetInt(HintSeenPrefsPrefix + key, 1);
		PlayerPrefs.Save();
	}

	// Ripristina tutti gli hint: la prossima volta che le meccaniche compaiono
	// vengono di nuovo spiegate. Utile per un pulsante "rivedi i suggerimenti".
	private void ResetHints()
	{
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyCampaignIntro);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyRoomChoice);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyInitialDraft);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyFormationDraft);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyDeploymentInitiative);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyCombat);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyMerchant);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyFirstAura);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyFirstDefeat);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyLevelUpVigor);
		PlayerPrefs.Save();
	}

	private void ResetHintsFromOptions()
	{
		ResetHints();
		SetMessage("Suggerimenti ripristinati: ricompariranno alla prossima occasione.");
	}

	private void ShowHintOnce(string key, string title, string body)
	{
		ShowHintOnce(key, title, new[] { body });
	}

	private void ShowHintOnce(string key, string title, params string[] pages)
	{
		if (pvpPresentationActive)
		{
			return;
		}
		if (string.IsNullOrEmpty(key) || HasSeenHint(key))
		{
			return;
		}
		MarkHintSeen(key);
		pendingHints.Enqueue(new HintContent(title, pages));
		if (!hintActive)
		{
			ShowNextHint();
		}
	}

	private void ShowNextHint()
	{
		if (pendingHints.Count == 0 || (Object)(object)hintPanel == (Object)null)
		{
			hintActive = false;
			ResumeGameAfterHint();
			return;
		}
		HintContent content = pendingHints.Dequeue();
		hintCurrentPages = NormalizeHintPages(content);
		hintCurrentPageIndex = 0;
		if ((Object)(object)hintTitleText != (Object)null)
		{
			hintTitleText.text = content.Title;
		}
		ShowHintPage(0);
		hintActive = true;
		PauseGameForHint();
		hintClosing = false;
		hintPanel.SetActive(true);
		hintPanel.transform.SetAsLastSibling();
		if ((Object)(object)hintDialogRect != (Object)null)
		{
			((Transform)hintDialogRect).localScale = new Vector3(0.74f, 0.74f, 1f);
		}
		PlayHintPopupTransition(opening: true);
		StartFleckHalo();
	}

	private void DismissHint()
	{
		if (hintClosing)
		{
			return;
		}
		if (hintTypingActive)
		{
			CompleteHintTyping();
			return;
		}
		if (hintCurrentPages != null && hintCurrentPageIndex < hintCurrentPages.Length - 1)
		{
			ShowHintPage(hintCurrentPageIndex + 1);
			return;
		}
		CloseCurrentHint();
	}

	private void CloseCurrentHint()
	{
		if (hintClosing)
		{
			return;
		}
		hintClosing = true;
		StopHintTyping();
		StopFleckHalo();
		PlayHintPopupTransition(opening: false);
	}

	private void CompleteHintClose()
	{
		if ((Object)(object)hintPanel != (Object)null)
		{
			hintPanel.SetActive(false);
		}
		if ((Object)(object)hintDialogRect != (Object)null)
		{
			((Transform)hintDialogRect).localScale = Vector3.one;
		}
		hintClosing = false;
		hintActive = false;
		if (pendingHints.Count > 0)
		{
			ShowNextHint();
			return;
		}
		ResumeGameAfterHint();
	}

	private static string[] NormalizeHintPages(HintContent content)
	{
		if (content.Pages != null && content.Pages.Length > 0)
		{
			return content.Pages;
		}
		return new[] { content.Body ?? string.Empty };
	}

	private void ShowHintPage(int pageIndex)
	{
		if (hintCurrentPages == null || hintCurrentPages.Length == 0)
		{
			return;
		}
		hintCurrentPageIndex = Mathf.Clamp(pageIndex, 0, hintCurrentPages.Length - 1);
		if ((Object)(object)hintButtonText != (Object)null)
		{
			hintButtonText.text = "AVANTI";
		}
		string page = hintCurrentPages[hintCurrentPageIndex];
		ResizeHintBubble(page);
		StartHintTyping(page);
	}

	private void ResizeHintBubble(string page)
	{
		if ((Object)(object)hintBubbleRect == (Object)null)
		{
			return;
		}
		int length = string.IsNullOrWhiteSpace(page) ? 0 : page.Length;
		float minY = length <= 34 ? 0.43f : (length <= 82 ? 0.36f : 0.28f);
		SetRect(hintBubbleRect, new Vector2(0.06f, minY), new Vector2(0.67f, 0.62f));
	}

	private void StartHintTyping(string body)
	{
		StopHintTyping();
		if ((Object)(object)hintBodyText == (Object)null)
		{
			return;
		}
		hintBodyText.text = string.Empty;
		hintCurrentFullPage = body ?? string.Empty;
		hintTypingActive = true;
		StartFleckMouth();
		hintTypingCoroutine = StartCoroutine(TypeHintBody(body));
	}

	private void StopHintTyping()
	{
		if (hintTypingCoroutine == null)
		{
			return;
		}
		StopCoroutine(hintTypingCoroutine);
		hintTypingCoroutine = null;
		hintTypingActive = false;
		StopFleckMouth();
	}

	private void CompleteHintTyping()
	{
		StopHintTyping();
		if ((Object)(object)hintBodyText != (Object)null)
		{
			hintBodyText.text = hintCurrentFullPage;
		}
		UpdateHintButtonLabel();
	}

	private void UpdateHintButtonLabel()
	{
		if ((Object)(object)hintButtonText == (Object)null)
		{
			return;
		}
		hintButtonText.text = hintCurrentPages != null && hintCurrentPageIndex >= hintCurrentPages.Length - 1
			? "HO CAPITO"
			: "AVANTI";
	}

	private IEnumerator TypeHintBody(string body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			hintBodyText.text = string.Empty;
			hintTypingCoroutine = null;
			hintTypingActive = false;
			StopFleckMouth();
			UpdateHintButtonLabel();
			yield break;
		}

		float reveal = 0f;
		float revealSpeed = 24f;
		int totalCharacters = body.Length;
		while (reveal < totalCharacters + 5f)
		{
			reveal += Time.unscaledDeltaTime * revealSpeed;
			hintBodyText.text = BuildFadedHintText(body, reveal);
			yield return null;
		}
		hintBodyText.text = body;
		hintTypingCoroutine = null;
		hintTypingActive = false;
		StopFleckMouth();
		UpdateHintButtonLabel();
	}

	private static string BuildFadedHintText(string body, float reveal)
	{
		const float fadeWidth = 7f;
		System.Text.StringBuilder builder = new System.Text.StringBuilder(body.Length * 24);
		for (int index = 0; index < body.Length; index++)
		{
			float alpha = Mathf.Clamp01((reveal - index) / fadeWidth);
			int alphaHex = Mathf.RoundToInt(alpha * 255f);
			builder.Append("<color=#FFFFFF");
			builder.Append(alphaHex.ToString("X2"));
			builder.Append('>');
			AppendRichTextSafeCharacter(builder, body[index]);
			builder.Append("</color>");
		}
		return builder.ToString();
	}

	private static void AppendRichTextSafeCharacter(System.Text.StringBuilder builder, char character)
	{
		switch (character)
		{
			case '<':
				builder.Append("&lt;");
				break;
			case '>':
				builder.Append("&gt;");
				break;
			case '&':
				builder.Append("&amp;");
				break;
			default:
				builder.Append(character);
				break;
		}
	}

	private void StartFleckMouth()
	{
		if ((Object)(object)hintMascotMouthImage == (Object)null || hintMouthCoroutine != null)
		{
			return;
		}
		hintMouthCoroutine = StartCoroutine(AnimateFleckMouth());
	}

	private void StopFleckMouth()
	{
		if (hintMouthCoroutine != null)
		{
			StopCoroutine(hintMouthCoroutine);
			hintMouthCoroutine = null;
		}
		if ((Object)(object)hintMascotMouthImage != (Object)null)
		{
			((Transform)hintMascotMouthImage.rectTransform).localRotation = Quaternion.identity;
			((Transform)hintMascotMouthImage.rectTransform).localScale = Vector3.one;
			hintMascotMouthImage.sprite = GetFleckMouthClosedSprite();
			SetRect(hintMascotMouthImage.rectTransform, new Vector2(0.707f, 0.625f), new Vector2(0.765f, 0.653f));
			hintMascotMouthImage.gameObject.SetActive(false);
		}
		if ((Object)(object)hintMascotMouthDebugImage != (Object)null)
		{
			hintMascotMouthDebugImage.gameObject.SetActive(false);
		}
	}

	private IEnumerator AnimateFleckMouth()
	{
		bool open = false;
		while (hintTypingActive)
		{
			if ((Object)(object)hintMascotMouthImage != (Object)null)
			{
				hintMascotMouthImage.gameObject.SetActive(true);
				((Transform)hintMascotMouthImage.rectTransform).SetAsLastSibling();
				open = !open;
				hintMascotMouthImage.sprite = open ? GetFleckMouthSprite() : GetFleckMouthClosedSprite();
				hintMascotMouthImage.color = open ? Color.white : new Color(1f, 1f, 1f, 0.82f);
				SetRect(
					hintMascotMouthImage.rectTransform,
					open ? new Vector2(0.682f, 0.58f) : new Vector2(0.707f, 0.625f),
					open ? new Vector2(0.795f, 0.69f) : new Vector2(0.765f, 0.653f));
				((Transform)hintMascotMouthImage.rectTransform).localRotation = Quaternion.Euler(0f, 0f, open ? 3f : -2f);
				if (FleckMouthDebug && (Object)(object)hintMascotMouthDebugImage != (Object)null)
				{
					hintMascotMouthDebugImage.gameObject.SetActive(true);
					hintMascotMouthDebugImage.color = open ? new Color(1f, 0f, 0f, 0.75f) : new Color(0f, 1f, 1f, 0.75f);
					((Transform)hintMascotMouthDebugImage.rectTransform).SetAsLastSibling();
				}
			}
			yield return WaitForCardInspectionPause(open ? 0.11f : 0.075f);
		}
		hintMouthCoroutine = null;
	}

	private void StartFleckHalo()
	{
		if ((Object)(object)hintMascotHaloImage == (Object)null || hintHaloCoroutine != null)
		{
			return;
		}
		hintHaloCoroutine = StartCoroutine(AnimateFleckHalo());
	}

	private void StopFleckHalo()
	{
		if (hintHaloCoroutine != null)
		{
			StopCoroutine(hintHaloCoroutine);
			hintHaloCoroutine = null;
		}
		if ((Object)(object)hintMascotHaloImage != (Object)null)
		{
			((Transform)hintMascotHaloImage.rectTransform).localRotation = Quaternion.identity;
			((Transform)hintMascotHaloImage.rectTransform).localScale = Vector3.one;
		}
	}

	private IEnumerator AnimateFleckHalo()
	{
		while ((Object)(object)hintPanel != (Object)null && hintPanel.activeSelf)
		{
			if ((Object)(object)hintMascotHaloImage != (Object)null)
			{
				float pulse = (Mathf.Sin(Time.unscaledTime * 4.2f) + 1f) * 0.5f;
				float easedPulse = Mathf.SmoothStep(0f, 1f, pulse);
				float scale = Mathf.Lerp(0.94f, 1.08f, easedPulse);
				Transform haloTransform = (Transform)hintMascotHaloImage.rectTransform;
				haloTransform.localRotation = Quaternion.identity;
				haloTransform.localScale = new Vector3(scale, scale, 1f);
			}
			yield return null;
		}
		hintHaloCoroutine = null;
	}

	private static Sprite GetFleckMouthSprite()
	{
		if ((Object)(object)fleckMouthSprite != (Object)null)
		{
			return fleckMouthSprite;
		}
		const int width = 64;
		const int height = 40;
		Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			name = "fleck_mouth_open",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.DontSave
		};
		Color32[] pixels = new Color32[width * height];
		Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				float dx = (x - center.x) / 27f;
				float dy = (y - center.y) / 15f;
				float oval = dx * dx + dy * dy;
				float lowerMask = Mathf.SmoothStep(-0.88f, -0.2f, dy);
				float inner = Mathf.Clamp01((1f - oval) * 1.6f) * lowerMask;
				float lip = Mathf.Exp(-Mathf.Pow((oval - 0.78f) / 0.16f, 2f)) * lowerMask;
				float upperSoft = Mathf.Exp(-Mathf.Pow((dy + 0.42f) / 0.2f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(dx));
				float shine = Mathf.Exp(-Mathf.Pow((dx + 0.25f) / 0.18f, 2f) - Mathf.Pow((dy + 0.06f) / 0.22f, 2f)) * 0.55f;
				float alpha = Mathf.Clamp01(inner * 0.92f + lip * 0.82f + upperSoft * 0.35f);
				byte a = (byte)Mathf.RoundToInt(alpha * 255f);
				byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(12f, 255f, Mathf.Clamp01(lip + shine)));
				byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(4f, 238f, Mathf.Clamp01(lip * 0.75f + shine)));
				byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(3f, 120f, Mathf.Clamp01(lip * 0.55f + shine)));
				pixels[y * width + x] = new Color32(r, g, b, a);
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		fleckMouthSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
		fleckMouthSprite.name = "Fleck Mouth Open";
		fleckMouthSprite.hideFlags = HideFlags.DontSave;
		return fleckMouthSprite;
	}

	private static Sprite GetFleckMouthClosedSprite()
	{
		if ((Object)(object)fleckMouthClosedSprite != (Object)null)
		{
			return fleckMouthClosedSprite;
		}
		const int width = 64;
		const int height = 20;
		Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			name = "fleck_mouth_closed",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.DontSave
		};
		Color32[] pixels = new Color32[width * height];
		Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				float dx = (x - center.x) / 28f;
				float dy = (y - center.y) / 4.5f;
				float curveY = Mathf.Sin((dx + 1f) * Mathf.PI * 0.5f) * 0.28f;
				float line = Mathf.Exp(-Mathf.Pow((dy - curveY) / 0.42f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(dx));
				float glow = Mathf.Exp(-Mathf.Pow((dy - curveY) / 1.1f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(dx)) * 0.32f;
				byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(line + glow));
				pixels[y * width + x] = new Color32(70, 26, 8, alpha);
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		fleckMouthClosedSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
		fleckMouthClosedSprite.name = "Fleck Mouth Closed";
		fleckMouthClosedSprite.hideFlags = HideFlags.DontSave;
		return fleckMouthClosedSprite;
	}

	private static Sprite GetFleckHaloSprite()
	{
		if ((Object)(object)fleckHaloSprite != (Object)null)
		{
			return fleckHaloSprite;
		}
		const int width = 128;
		const int height = 48;
		Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			name = "fleck_halo",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.DontSave
		};
		Color32[] pixels = new Color32[width * height];
		Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				float dx = (x - center.x) / 56f;
				float dy = (y - center.y) / 15f;
				float radius = Mathf.Sqrt(dx * dx + dy * dy);
				float ring = Mathf.Exp(-Mathf.Pow((radius - 1f) / 0.12f, 2f));
				float glow = Mathf.Exp(-Mathf.Pow((radius - 1f) / 0.34f, 2f)) * 0.32f;
				byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(ring * 0.95f + glow));
				pixels[y * width + x] = new Color32(255, 225, 72, alpha);
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		fleckHaloSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
		fleckHaloSprite.name = "Fleck Halo";
		fleckHaloSprite.hideFlags = HideFlags.DontSave;
		return fleckHaloSprite;
	}

	private void PauseGameForHint()
	{
		if (hintPausedTime)
		{
			return;
		}
		hintPreviousTimeScale = Time.timeScale;
		Time.timeScale = 0f;
		hintPausedTime = true;
	}

	private void ResumeGameAfterHint()
	{
		if (!hintPausedTime)
		{
			return;
		}
		Time.timeScale = hintPreviousTimeScale;
		hintPausedTime = false;
	}

	private bool IsHintBlockingGame()
	{
		return hintActive
			|| hintClosing
			|| pendingHints.Count > 0
			|| ((Object)(object)hintPanel != (Object)null && hintPanel.activeSelf);
	}

	private IEnumerator WaitForHintToClose()
	{
		while (IsHintBlockingGame())
		{
			yield return null;
		}
	}

	private void PlayHintPopupTransition(bool opening)
	{
		if (hintPopupTransitionCoroutine != null)
		{
			StopCoroutine(hintPopupTransitionCoroutine);
			hintPopupTransitionCoroutine = null;
		}
		hintPopupTransitionCoroutine = StartCoroutine(AnimateHintPopupTransition(opening));
	}

	private IEnumerator AnimateHintPopupTransition(bool opening)
	{
		if ((Object)(object)hintDialogRect == (Object)null)
		{
			hintPopupTransitionCoroutine = null;
			if (!opening)
			{
				CompleteHintClose();
			}
			yield break;
		}

		float duration = opening ? 0.22f : 0.16f;
		float elapsed = 0f;
		float from = opening ? 0.74f : ((Transform)hintDialogRect).localScale.x;
		float to = opening ? 1f : 0.74f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = opening ? EaseOutBack(t) : EaseInCubic(t);
			float scale = Mathf.LerpUnclamped(from, to, eased);
			((Transform)hintDialogRect).localScale = new Vector3(scale, scale, 1f);
			yield return null;
		}
		((Transform)hintDialogRect).localScale = new Vector3(to, to, 1f);
		hintPopupTransitionCoroutine = null;
		if (!opening)
		{
			CompleteHintClose();
		}
	}

	private static float EaseOutBack(float t)
	{
		const float overshoot = 1.45f;
		float shifted = t - 1f;
		return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
	}

	private static float EaseInCubic(float t)
	{
		return t * t * t;
	}

	private void ShowCampaignIntroHint()
	{
		HintContent content = CreateCampaignIntroHintContent();
		ShowHintOnce(HintKeyCampaignIntro, content.Title, content.Pages);
	}

	private void ShowRoomChoiceHint()
	{
		HintContent content = CreateRoomChoiceHintContent();
		ShowHintOnce(HintKeyRoomChoice, content);
	}

	private void ShowInitialDraftHint()
	{
		HintContent content = CreateInitialDraftHintContent();
		ShowHintOnce(HintKeyInitialDraft, content);
	}

	private void ShowFormationDraftHint()
	{
		HintContent content = CreateFormationDraftHintContent();
		ShowHintOnce(HintKeyFormationDraft, content);
	}

	private void ShowDeploymentInitiativeHint()
	{
		HintContent content = CreateDeploymentInitiativeHintContent();
		ShowHintOnce(HintKeyDeploymentInitiative, content);
	}

	private void ShowCombatHint()
	{
		HintContent content = CreateCombatHintContent();
		ShowHintOnce(HintKeyCombat, content);
	}

	private void ShowFirstAuraHint(BattleAuraType aura)
	{
		if (aura == BattleAuraType.None)
		{
			return;
		}
		HintContent content = CreateAuraHintContent(aura);
		ShowHintOnce(HintKeyFirstAura, content);
	}

	private void ShowFirstDefeatHint()
	{
		HintContent content = CreateFirstDefeatHintContent();
		ShowHintOnce(HintKeyFirstDefeat, content);
	}

	private void ShowLevelUpVigorHint()
	{
		HintContent content = CreateLevelUpVigorHintContent();
		ShowHintOnce(HintKeyLevelUpVigor, content);
	}

	private void ShowMerchantHint()
	{
		HintContent content = CreateMerchantHintContent();
		ShowHintOnce(HintKeyMerchant, content);
	}

	private void ShowHintOnce(string key, HintContent content)
	{
		ShowHintOnce(key, content.Title, NormalizeHintPages(content));
	}

	private HintContent CreateCampaignIntroHintContent()
	{
		return new HintContent(
			"FLECK",
			"Ciao, io sono Fleck.",
			"Questa non e' la mia vera forma.",
			"Ero un potente evocatore.",
			"Poi la grotta del Master mi ha sconfitto.",
			"Ora aiuto chi entra dopo di me.",
			"Prima cosa: forgia il tuo mazzo.",
			"Spendi essenza per scegliere le carte.",
			"Quando il mazzo e' pronto, premi INIZIA.");
	}

	private HintContent CreateInitialDraftHintContent()
	{
		return new HintContent(
			"DRAFT DEL MAZZO",
			"Scegli un capitano che guidera' le prossime offerte.",
			"Tocca una pedina per leggerla e sceglierla.",
			"Riempi un mazzo da 9 carte.");
	}

	private HintContent CreateFormationDraftHintContent()
	{
		return new HintContent(
			"SCHIERA LA TUA FORMAZIONE",
			"Ti vengono mostrate solo 6 carte casuali dal tuo mazzo.\n" +
			"Scegli 3 carte da mandare in campo.\n" +
			"Valuta in base a sinergie tra classi e avversari che hanno schierato prima di te.");
	}

	private HintContent CreateDeploymentInitiativeHintContent()
	{
		return new HintContent(
			"CHI SCHIERA E ATTACCA PER PRIMO?",
			"Per determinare l'ordine di gioco si tireranno 3 D20.",
			"In base ai risultati si stabilisce l'ordine.",
			"In ordine crescente si schierano le carte.",
			"In ordine decrescente si attacca.",
			"Un valore basso e' quindi piu sfigato di uno piu alto.");
	}

	private HintContent CreateCombatHintContent()
	{
		return new HintContent(
			"COMBATTIMENTO",
			"Si combatte a turni.",
			"Nel tuo turno puoi usare l'abilita e attaccare.",
			"L'attaccante e il difensore tirano il dado Vigore.",
			"Chi fa la somma piu alta, tra forza della carta e dado vigore, vince.");
	}

	private HintContent CreateAuraHintContent(BattleAuraType aura)
	{
		return new HintContent(
			"AURA ATTIVA",
			"Hai attivato: " + AuraDisplayName(aura) + ".",
			"Le aure sono bonus di squadra.",
			"Si attivano con carte in sinergia.",
			"Effetto: " + AuraEffectText(aura),
			"Le trovi tutte in Opzioni > AURE.");
	}

	private HintContent CreateFirstDefeatHintContent()
	{
		return new HintContent(
			"SCONFITTA",
			"Non e' finita.",
			"Puoi riprovare la stessa stanza.",
			"Userai le carte ancora disponibili.",
			"Premi RIPROVA STANZA per continuare.");
	}

	private HintContent CreateLevelUpVigorHintContent()
	{
		return new HintContent(
			"LEVEL UP",
			"Quando sali di livello aumenta il tuo dado Vigore.",
			"Il dado Vigore si somma alla forza delle carte in combattimento.",
			"Piu il dado e' grande, piu puoi tirare risultati forti.");
	}

	private HintContent CreateRoomChoiceHintContent()
	{
		return new HintContent(
			"SCEGLI LA VIA",
			"Ogni porta nasconde una stanza.",
			"Puoi trovare mostri, tesori o mercanti.",
			"Tocca una porta per proseguire.");
	}

	private HintContent CreateMerchantHintContent()
	{
		return new HintContent(
			"MERCANTE",
			"Qui puoi comprare nuove carte.",
			"Puoi scegliere per classe o valore.",
			"Puoi anche recuperare carte dal cimitero.",
			"Quando hai finito, premi CONTINUA.");
	}

	public void ShowAllHintsForDebug()
	{
		if ((Object)(object)hintPanel != (Object)null)
		{
			hintPanel.SetActive(false);
		}
		StopHintTyping();
		pendingHints.Clear();
		hintActive = false;
		ResumeGameAfterHint();

		pendingHints.Enqueue(CreateCampaignIntroHintContent());
		pendingHints.Enqueue(CreateInitialDraftHintContent());
		pendingHints.Enqueue(CreateFormationDraftHintContent());
		pendingHints.Enqueue(CreateDeploymentInitiativeHintContent());
		pendingHints.Enqueue(CreateCombatHintContent());
		pendingHints.Enqueue(CreateAuraHintContent(BattleAuraType.Formation));
		pendingHints.Enqueue(CreateFirstDefeatHintContent());
		pendingHints.Enqueue(CreateLevelUpVigorHintContent());
		pendingHints.Enqueue(CreateRoomChoiceHintContent());
		pendingHints.Enqueue(CreateMerchantHintContent());
		ShowNextHint();
	}
}
}
