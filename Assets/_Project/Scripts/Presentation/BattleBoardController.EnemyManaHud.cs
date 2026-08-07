using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private static readonly string[] EnemyManaIconSpritePaths =
	{
		"UI/enemy_mana_icon",
		"UI/Battle/enemy_mana_icon",
		"UI/Common/enemy_mana_icon"
	};

	private Image enemyManaRuneImage;
	private Image enemyManaRuneAuraImage;
	private Image enemyManaRuneOutlineImage;
	private Text enemyManaRuneText;
	private int enemyManaDisplayedValue = -1;
	private Coroutine enemyManaValueTweenRoutine;
	private int enemyManaDeltaCalloutIndex;
	private static readonly Color EnemyManaDeltaColor = new Color(1f, 0.32f, 0.16f);

	/// <summary>
	/// Speculare alla runa del giocatore: stessa dimensione e stesso numero interno,
	/// ma ancorata in alto a sinistra accanto al riquadro della CPU.
	/// </summary>
	private void CreateEnemyManaHudView(Font font)
	{
		if ((Object)(object)safeAreaRoot == (Object)null || enemyManaRuneImage != null)
		{
			return;
		}

		enemyManaRuneImage = CreateImage("Enemy Mana Rune", (Transform)(object)safeAreaRoot, Color.white);
		enemyManaRuneImage.preserveAspect = true;
		enemyManaRuneImage.raycastTarget = false;
		enemyManaRuneImage.sprite = LoadEnemyManaIconSprite();

		RectTransform runeRect = enemyManaRuneImage.rectTransform;
		runeRect.anchorMin = runeRect.anchorMax = new Vector2(0f, 1f);
		runeRect.pivot = new Vector2(0f, 1f);
		runeRect.anchoredPosition = new Vector2(22f, -5f);
		runeRect.sizeDelta = BattleResourceIconSize;
		runeRect.localScale = Vector3.one * BattleResourceIconScale;
		CreateEnemyManaRuneAura();

		enemyManaRuneText = CreateText(
			"Enemy Mana Rune Value",
			((Component)enemyManaRuneImage).transform,
			font,
			46,
			(FontStyle)1,
			(TextAnchor)4);
		enemyManaRuneText.color = Color.white;
		enemyManaRuneText.raycastTarget = false;
		enemyManaRuneText.resizeTextForBestFit = true;
		enemyManaRuneText.resizeTextMinSize = 14;
		enemyManaRuneText.resizeTextMaxSize = 44;

		RectTransform valueRect = enemyManaRuneText.rectTransform;
		valueRect.anchorMin = Vector2.zero;
		valueRect.anchorMax = Vector2.one;
		valueRect.offsetMin = new Vector2(6f, 10.3f);
		valueRect.offsetMax = new Vector2(-6f, -1.7f);

		Outline outline = ((Component)enemyManaRuneText).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.12f, 0.01f, 0.01f, 0.95f);
		outline.effectDistance = new Vector2(1.6f, -1.6f);
		outline.useGraphicAlpha = true;

		RefreshEnemyManaHud();
	}

	private void CreateEnemyManaRuneAura()
	{
		if (enemyManaRuneAuraImage != null || enemyManaRuneImage == null)
		{
			return;
		}
		enemyManaRuneAuraImage = CreateImage("Enemy Mana Pulsing Aura", enemyManaRuneImage.transform, new Color(1f, 0.03f, 0.01f, 0.34f));
		enemyManaRuneAuraImage.sprite = BuildManaAuraSprite();
		enemyManaRuneAuraImage.raycastTarget = false;
		RectTransform auraRect = enemyManaRuneAuraImage.rectTransform;
		auraRect.anchorMin = auraRect.anchorMax = new Vector2(0f, 1f);
		auraRect.pivot = new Vector2(0.5f, 0.5f);
		auraRect.anchoredPosition = new Vector2(41.5f, -43f);
		auraRect.sizeDelta = new Vector2(103f, 103f);
		auraRect.SetAsFirstSibling();

		enemyManaRuneOutlineImage = CreateImage("Enemy Mana Rune Circular Outline", enemyManaRuneImage.transform, new Color(1f, 0.28f, 0.08f, 0.92f));
		enemyManaRuneOutlineImage.sprite = BuildManaRingSprite();
		enemyManaRuneOutlineImage.raycastTarget = false;
		RectTransform outlineRect = enemyManaRuneOutlineImage.rectTransform;
		outlineRect.anchorMin = outlineRect.anchorMax = new Vector2(0f, 1f);
		outlineRect.pivot = new Vector2(0.5f, 0.5f);
		outlineRect.anchoredPosition = new Vector2(41.5f, -41.5f);
		outlineRect.sizeDelta = new Vector2(92f, 92f);
		outlineRect.SetSiblingIndex(1);
		((MonoBehaviour)this).StartCoroutine(EnemyManaRuneAuraRoutine());
	}

	private System.Collections.IEnumerator EnemyManaRuneAuraRoutine()
	{
		while (enemyManaRuneAuraImage != null)
		{
			float pulse = (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f;
			float sparkle = Mathf.Clamp01(
				Mathf.Sin(Time.unscaledTime * 9.3f + 0.8f) * 0.5f +
				Mathf.Sin(Time.unscaledTime * 14.2f) * 0.35f + 0.3f);
			enemyManaRuneAuraImage.color = new Color(
				1f,
				Mathf.Lerp(0.07f, 0.2f, sparkle),
				Mathf.Lerp(0.025f, 0.07f, sparkle),
				Mathf.Lerp(0.27f, 0.5f, pulse) + sparkle * 0.06f);
			enemyManaRuneAuraImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.93f, 1.1f, pulse);
			if (enemyManaRuneOutlineImage != null)
			{
				enemyManaRuneOutlineImage.color = new Color(1f, 0.28f, 0.08f, Mathf.Lerp(0.74f, 1f, pulse));
			}
			yield return null;
		}
	}

	private Sprite LoadEnemyManaIconSprite()
	{
		foreach (string path in EnemyManaIconSpritePaths)
		{
			Sprite sprite = LoadSpriteResource(path);
			if ((Object)(object)sprite != (Object)null)
			{
				return sprite;
			}
		}
		return null;
	}

	private void RefreshEnemyManaHud()
	{
		if (enemyManaRuneImage == null)
		{
			return;
		}

		bool visible = BattleManaHudEnabled;
		if (((Component)enemyManaRuneImage).gameObject.activeSelf != visible)
		{
			((Component)enemyManaRuneImage).gameObject.SetActive(visible);
		}
		if (!visible || enemyManaRuneText == null)
		{
			return;
		}

		AnimateEnemyManaValueTo(BattleCpuManaCurrent);
	}

	private void AnimateEnemyManaValueTo(int target)
	{
		if (enemyManaDisplayedValue < 0 || !((Behaviour)this).isActiveAndEnabled)
		{
			enemyManaDisplayedValue = target;
			enemyManaRuneText.text = target.ToString();
			return;
		}
		if (enemyManaDisplayedValue == target)
		{
			return;
		}

		if (enemyManaValueTweenRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(enemyManaValueTweenRoutine);
		}
		enemyManaValueTweenRoutine = ((MonoBehaviour)this).StartCoroutine(EnemyManaValueTweenRoutine(target));
	}

	private System.Collections.IEnumerator EnemyManaValueTweenRoutine(int target)
	{
		int start = enemyManaDisplayedValue;
		float duration = Mathf.Clamp(Mathf.Abs(target - start) * 0.12f, 0.18f, 0.5f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			enemyManaDisplayedValue = Mathf.RoundToInt(Mathf.Lerp(start, target, eased));
			if (enemyManaRuneText != null)
			{
				enemyManaRuneText.text = enemyManaDisplayedValue.ToString();
			}
			yield return null;
		}

		enemyManaDisplayedValue = target;
		if (enemyManaRuneText != null)
		{
			enemyManaRuneText.text = target.ToString();
		}
		enemyManaValueTweenRoutine = null;
	}

	private void PlayEnemyManaDeltaCallout(int delta)
	{
		if (delta == 0 || enemyManaRuneImage == null || !BattleManaHudEnabled || !((Behaviour)this).isActiveAndEnabled)
		{
			return;
		}
		((MonoBehaviour)this).StartCoroutine(EnemyManaDeltaCalloutRoutine(delta));
	}

	private System.Collections.IEnumerator EnemyManaDeltaCalloutRoutine(int delta)
	{
		Text label = CreateText(
			"Enemy Mana Delta Callout",
			(Transform)(object)safeAreaRoot,
			manaHudFont,
			34,
			(FontStyle)1,
			(TextAnchor)4);
		label.text = delta > 0 ? $"+{delta}" : delta.ToString();
		label.color = EnemyManaDeltaColor;
		label.raycastTarget = false;
		label.horizontalOverflow = HorizontalWrapMode.Overflow;
		label.verticalOverflow = VerticalWrapMode.Overflow;

		Outline outline = ((Component)label).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.12f, 0.01f, 0.01f, 0.95f);
		outline.effectDistance = new Vector2(3f, -3f);
		outline.useGraphicAlpha = true;

		Shadow shadow = ((Component)label).gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0.02f, 0f, 0f, 0.82f);
		shadow.effectDistance = new Vector2(4.5f, -4.5f);
		shadow.useGraphicAlpha = true;

		RectTransform rect = label.rectTransform;
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = new Vector2(180f, 64f);

		float lane = (enemyManaDeltaCalloutIndex++ % 3) * 12f;
		float x = 105f + lane;
		const float startY = -150f;
		const float endY = -260f;
		const float duration = 0.8f;

		float elapsed = 0f;
		while (elapsed < duration && label != null)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			float rise = Mathf.SmoothStep(0f, 1f, progress);
			rect.anchoredPosition = new Vector2(x, Mathf.Lerp(startY, endY, rise));

			Color color = EnemyManaDeltaColor;
			color.a = progress < 0.34f ? 1f : 1f - Mathf.InverseLerp(0.34f, 1f, progress);
			label.color = color;
			yield return null;
		}

		if (label != null)
		{
			Object.Destroy(((Component)label).gameObject);
		}
	}
}
}
