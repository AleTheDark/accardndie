using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private static readonly Vector2 BattleResourceIconSize = new Vector2(83f, 83f);
	private const float BattleResourceIconScale = 2f;

	/// <summary>
	/// Contatore del mana in basso a sinistra: la runa con dentro il numero bianco.
	/// L'arte sta in Resources/UI/mana_icon; se manca, il widget si disegna comunque
	/// con una gemma procedurale, cosi' l'HUD resta provabile senza asset.
	/// </summary>
	private static readonly string[] ManaIconSpritePaths =
	{
		"UI/mana_icon",
		"UI/Battle/mana_icon",
		"UI/Battle/mana_rune",
		"UI/Common/mana_icon"
	};

	private Image manaRuneImage;
	private Image manaRuneAuraImage;
	private Image manaRuneOutlineImage;
	private Text manaRuneText;
	private bool manaRuneSpriteResolved;
	private static Sprite proceduralManaGem;
	private static Sprite proceduralManaAura;
	private static Sprite proceduralManaRing;

	private Font manaHudFont;
	private int manaDisplayedValue = -1;
	private Coroutine manaValueTweenRoutine;
	private int manaDeltaCalloutIndex;

	private static readonly Color ManaDeltaColor = new Color(0.35f, 0.66f, 1f);

	private void CreateManaHudView(Font font)
	{
		if ((Object)(object)safeAreaRoot == (Object)null || manaRuneImage != null)
		{
			return;
		}
		manaHudFont = font;

		manaRuneImage = CreateImage("Mana Rune", (Transform)(object)safeAreaRoot, Color.white);
		manaRuneImage.preserveAspect = true;
		manaRuneImage.raycastTarget = false;
		// Posizione calibrata a mano nell'editor. SetRect() lavora in ancore normalizzate
		// e azzererebbe gli offset, quindi qui si scrive direttamente sul RectTransform.
		RectTransform runeRect = manaRuneImage.rectTransform;
		runeRect.anchorMin = runeRect.anchorMax = new Vector2(0f, 0f);
		runeRect.pivot = new Vector2(0f, 0f);
		runeRect.anchoredPosition = new Vector2(22f, 6f);
		runeRect.sizeDelta = BattleResourceIconSize;
		runeRect.localScale = Vector3.one * BattleResourceIconScale;
		ApplyManaRuneSprite();

		manaRuneText = CreateText("Mana Rune Value", ((Component)manaRuneImage).transform, font, 34, (FontStyle)1, (TextAnchor)4);
		manaRuneText.color = Color.white;
		manaRuneText.raycastTarget = false;
		manaRuneText.resizeTextForBestFit = true;
		manaRuneText.resizeTextMinSize = 14;
		manaRuneText.resizeTextMaxSize = 44;
		RectTransform valueRect = manaRuneText.rectTransform;
		valueRect.anchorMin = new Vector2(0f, 0f);
		valueRect.anchorMax = new Vector2(1f, 1f);
		valueRect.offsetMin = new Vector2(6f, 10.3f);
		valueRect.offsetMax = new Vector2(-6f, -1.7f);

		// Contorno scuro: il numero bianco deve restare leggibile sopra il blu chiaro
		// delle sfaccettature della gemma.
		Outline valueOutline = ((Component)manaRuneText).gameObject.AddComponent<Outline>();
		valueOutline.effectColor = new Color(0.02f, 0.05f, 0.12f, 0.9f);
		valueOutline.effectDistance = new Vector2(1.6f, -1.6f);
		valueOutline.useGraphicAlpha = true;

		RefreshManaHud();
	}

	private void ApplyManaRuneSprite()
	{
		if (manaRuneSpriteResolved || manaRuneImage == null)
		{
			return;
		}
		manaRuneSpriteResolved = true;

		manaRuneImage.color = Color.white;
		manaRuneImage.sprite = LoadManaIconSprite() ?? BuildProceduralManaGem();
		CreateManaRuneAura();
	}

	private void CreateManaRuneAura()
	{
		if (manaRuneAuraImage != null || manaRuneImage == null)
		{
			return;
		}

		manaRuneAuraImage = CreateImage("Mana Rune Pulsing Aura", manaRuneImage.transform, new Color(0.05f, 0.48f, 1f, 0.3f));
		manaRuneAuraImage.sprite = BuildManaAuraSprite();
		manaRuneAuraImage.raycastTarget = false;
		RectTransform auraRect = manaRuneAuraImage.rectTransform;
		auraRect.anchorMin = auraRect.anchorMax = Vector2.zero;
		auraRect.pivot = new Vector2(0.5f, 0.5f);
		auraRect.anchoredPosition = new Vector2(41.5f, 42.5f);
		auraRect.sizeDelta = new Vector2(101f, 101f);
		auraRect.SetAsFirstSibling();

		manaRuneOutlineImage = CreateImage("Mana Rune Circular Outline", manaRuneImage.transform, new Color(0.28f, 0.78f, 1f, 0.9f));
		manaRuneOutlineImage.sprite = BuildManaRingSprite();
		manaRuneOutlineImage.raycastTarget = false;
		RectTransform outlineRect = manaRuneOutlineImage.rectTransform;
		outlineRect.anchorMin = outlineRect.anchorMax = Vector2.zero;
		outlineRect.pivot = new Vector2(0.5f, 0.5f);
		outlineRect.anchoredPosition = new Vector2(41.5f, 41.5f);
		outlineRect.sizeDelta = new Vector2(92f, 92f);
		outlineRect.SetSiblingIndex(1);
		((MonoBehaviour)this).StartCoroutine(ManaRuneAuraRoutine());
	}

	private System.Collections.IEnumerator ManaRuneAuraRoutine()
	{
		while (manaRuneAuraImage != null)
		{
			float pulse = (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f;
			float sparkle = Mathf.Clamp01(
				Mathf.Sin(Time.unscaledTime * 8.7f) * 0.5f +
				Mathf.Sin(Time.unscaledTime * 13.1f + 1.4f) * 0.35f + 0.3f);
			manaRuneAuraImage.color = new Color(
				Mathf.Lerp(0.08f, 0.18f, sparkle),
				Mathf.Lerp(0.56f, 0.72f, sparkle),
				1f,
				Mathf.Lerp(0.25f, 0.46f, pulse) + sparkle * 0.06f);
			manaRuneAuraImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.93f, 1.1f, pulse);
			if (manaRuneOutlineImage != null)
			{
				manaRuneOutlineImage.color = new Color(0.28f, 0.78f, 1f, Mathf.Lerp(0.72f, 1f, pulse));
			}
			yield return null;
		}
	}

	private static Sprite BuildManaAuraSprite()
	{
		if (proceduralManaAura != null) return proceduralManaAura;
		const int size = 128;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		Vector2 center = Vector2.one * (size - 1) * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
				float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.15f);
				texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}
		texture.Apply();
		proceduralManaAura = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
		return proceduralManaAura;
	}

	private static Sprite BuildManaRingSprite()
	{
		if (proceduralManaRing != null) return proceduralManaRing;
		const int size = 128;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		Vector2 center = Vector2.one * (size - 1) * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
				float outer = 1f - Mathf.SmoothStep(0.94f, 1f, distance);
				float inner = Mathf.SmoothStep(0.875f, 0.93f, distance);
				texture.SetPixel(x, y, new Color(1f, 1f, 1f, outer * inner));
			}
		}
		texture.Apply();
		proceduralManaRing = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
		return proceduralManaRing;
	}

	/// <summary>
	/// Passa da LoadSpriteResource e non da Resources.Load&lt;Sprite&gt;: se il PNG e'
	/// importato come Texture invece che come Sprite, il primo lo converte comunque
	/// mentre il secondo tornerebbe null e ricadremmo sulla gemma procedurale.
	/// </summary>
	private Sprite LoadManaIconSprite()
	{
		foreach (string path in ManaIconSpritePaths)
		{
			Sprite sprite = LoadSpriteResource(path);
			if ((Object)(object)sprite != (Object)null)
			{
				return sprite;
			}
		}
		return null;
	}

	/// <summary>
	/// Gemma tonda disegnata a runtime, usata finche' l'arte vera non e' nel progetto.
	/// Meglio di un riquadro con la cornice: e' rotonda e sfaccettata, quindi legge
	/// come una gemma anche senza asset.
	/// </summary>
	private static Sprite BuildProceduralManaGem()
	{
		if ((Object)(object)proceduralManaGem != (Object)null)
		{
			return proceduralManaGem;
		}

		const int size = 128;
		const float radius = size * 0.5f;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};

		Color deep = new Color(0.05f, 0.25f, 0.78f);
		Color bright = new Color(0.35f, 0.75f, 1f);

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = x - radius + 0.5f;
				float dy = y - radius + 0.5f;
				float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
				if (distance > 1f)
				{
					texture.SetPixel(x, y, Color.clear);
					continue;
				}

				// Sfaccettatura: settori angolari che alternano due tonalita' di blu,
				// piu' un bordo chiaro che stacca la gemma dallo sfondo scuro.
				float angle = Mathf.Atan2(dy, dx);
				float facet = Mathf.Abs(Mathf.Sin(angle * 6f)) * 0.28f;
				Color color = Color.Lerp(bright, deep, Mathf.Clamp01(distance * 0.85f + facet - 0.1f));
				if (distance > 0.9f)
				{
					color = Color.Lerp(color, bright, (distance - 0.9f) / 0.1f);
				}
				// Antialiasing sul bordo esterno.
				color.a = distance > 0.97f ? Mathf.InverseLerp(1f, 0.97f, distance) : 1f;
				texture.SetPixel(x, y, color);
			}
		}

		texture.Apply();
		proceduralManaGem = Sprite.Create(
			texture,
			new Rect(0f, 0f, size, size),
			new Vector2(0.5f, 0.5f));
		return proceduralManaGem;
	}

	/// <summary>
	/// Il contatore riguarda solo la campagna: in PvP la riserva arriva dal server
	/// e la disegna la sua UI, non questa.
	/// </summary>
	private void RefreshManaHud()
	{
		if (manaRuneImage == null)
		{
			return;
		}

		bool visible = BattleManaHudEnabled;
		if (((Component)manaRuneImage).gameObject.activeSelf != visible)
		{
			((Component)manaRuneImage).gameObject.SetActive(visible);
		}
		if (!visible || manaRuneText == null)
		{
			return;
		}

		AnimateManaValueTo(BattlePlayerManaCurrent);
	}

	/// <summary>
	/// Il numero scorre invece di saltare, con la stessa ease-out cubica che usano i
	/// totali del confronto: cosi' due variazioni ravvicinate si leggono come due
	/// passaggi e non come un unico salto.
	/// </summary>
	private void AnimateManaValueTo(int target)
	{
		if (manaDisplayedValue < 0 || !((Behaviour)this).isActiveAndEnabled)
		{
			manaDisplayedValue = target;
			manaRuneText.text = target.ToString();
			return;
		}
		if (manaDisplayedValue == target)
		{
			return;
		}

		if (manaValueTweenRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(manaValueTweenRoutine);
		}
		manaValueTweenRoutine = ((MonoBehaviour)this).StartCoroutine(ManaValueTweenRoutine(target));
	}

	private System.Collections.IEnumerator ManaValueTweenRoutine(int target)
	{
		int start = manaDisplayedValue;
		// Piu' punti da percorrere, piu' tempo, ma entro limiti stretti: il contatore
		// non deve mai far aspettare il turno.
		float duration = Mathf.Clamp(Mathf.Abs(target - start) * 0.12f, 0.18f, 0.5f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			manaDisplayedValue = Mathf.RoundToInt(Mathf.Lerp(start, target, eased));
			if (manaRuneText != null)
			{
				manaRuneText.text = manaDisplayedValue.ToString();
			}
			yield return null;
		}

		manaDisplayedValue = target;
		if (manaRuneText != null)
		{
			manaRuneText.text = target.ToString();
		}
		manaValueTweenRoutine = null;
	}

	/// <summary>
	/// Etichetta +x / -x che sale dalla runa, come i callout delle azioni sulle pedine.
	/// Serve a rendere leggibili i contributi singoli quando piu' variazioni si
	/// sommano nello stesso momento (paghi l'attacco e poi recuperi a fine turno).
	/// </summary>
	private void PlayManaDeltaCallout(int delta)
	{
		if (delta == 0 || manaRuneImage == null || !BattleManaHudEnabled || !((Behaviour)this).isActiveAndEnabled)
		{
			return;
		}
		if (delta > 0)
		{
			battleSfx?.PlayManaGain();
		}
		((MonoBehaviour)this).StartCoroutine(ManaDeltaCalloutRoutine(delta));
	}

	private System.Collections.IEnumerator ManaDeltaCalloutRoutine(int delta)
	{
		// Appeso alla Safe Area e non alla runa: la runa ha scala 2, e i figli
		// erediterebbero il raddoppio falsando dimensioni e distanza di salita.
		Text label = CreateText(
			"Mana Delta Callout",
			(Transform)(object)safeAreaRoot,
			manaHudFont,
			34,
			(FontStyle)1,
			(TextAnchor)4);
		label.text = delta > 0 ? $"+{delta}" : delta.ToString();
		label.color = ManaDeltaColor;
		label.raycastTarget = false;
		label.horizontalOverflow = HorizontalWrapMode.Overflow;
		label.verticalOverflow = VerticalWrapMode.Overflow;

		Outline outline = ((Component)label).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.02f, 0.05f, 0.12f, 0.95f);
		outline.effectDistance = new Vector2(1.8f, -1.8f);
		outline.useGraphicAlpha = true;

		RectTransform rect = label.rectTransform;
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
		rect.pivot = new Vector2(0.5f, 0f);
		rect.sizeDelta = new Vector2(140f, 46f);

		// Variazioni ravvicinate non si sovrappongono perfettamente.
		float lane = (manaDeltaCalloutIndex++ % 3) * 12f;
		float x = 105f + lane;
		const float startY = 190f;
		const float endY = 300f;
		const float duration = 0.8f;

		float elapsed = 0f;
		while (elapsed < duration && label != null)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			float rise = Mathf.SmoothStep(0f, 1f, progress);
			rect.anchoredPosition = new Vector2(x, Mathf.Lerp(startY, endY, rise));

			Color color = ManaDeltaColor;
			// Piena per il primo terzo, poi dissolve: il numero si legge prima di sparire.
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
