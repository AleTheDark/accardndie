using System.Collections.Generic;
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
	private Text manaRuneText;
	private bool manaRuneSpriteResolved;
	private static Sprite proceduralManaGem;
	private static Sprite proceduralManaAura;
	private static Sprite proceduralManaRing;

	private Font manaHudFont;
	private int manaDisplayedValue = -1;
	private Coroutine manaValueTweenRoutine;
	private int manaDeltaCalloutIndex;
	private readonly HashSet<GameObject> manaDeltaCallouts = new HashSet<GameObject>();

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

		manaRuneText = CreateText("Mana Rune Value", ((Component)manaRuneImage).transform, font, 55, (FontStyle)1, (TextAnchor)4);
		manaRuneText.color = Color.white;
		manaRuneText.raycastTarget = false;
		manaRuneText.resizeTextForBestFit = true;
		manaRuneText.resizeTextMinSize = 14;
		manaRuneText.resizeTextMaxSize = 55;
		manaRuneText.text = Mathf.Max(0, BattlePlayerManaCurrent).ToString();
		RectTransform valueRect = manaRuneText.rectTransform;
		valueRect.anchorMin = new Vector2(0f, 0f);
		valueRect.anchorMax = new Vector2(1f, 1f);
		valueRect.offsetMin = new Vector2(6f, 14f);
		valueRect.offsetMax = new Vector2(-6f, 2f);
		valueRect.SetAsLastSibling();

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
		auraRect.anchorMin = auraRect.anchorMax = new Vector2(0.5f, 0.5f);
		auraRect.pivot = new Vector2(0.5f, 0.5f);
		auraRect.anchoredPosition = Vector2.zero;
		auraRect.sizeDelta = new Vector2(101f, 101f);
		auraRect.localScale = Vector3.one * 2f;
		auraRect.SetAsFirstSibling();

		((MonoBehaviour)this).StartCoroutine(ManaRuneAuraRoutine());
	}

	/// <summary>Esplosione di energia riservata agli effetti che riempiono il mana, come la Riserva del Paladino.</summary>
	private void PlayManaRuneEnergyBurst(bool enemy = false)
	{
		Image rune = enemy ? enemyManaRuneImage : manaRuneImage;
		if (rune == null || safeAreaRoot == null || !BattleManaHudEnabled || !((Behaviour)this).isActiveAndEnabled)
			return;

		Color energy = enemy ? new Color(1f, 0.24f, 0.06f, 1f) : new Color(0.12f, 0.72f, 1f, 1f);
		((MonoBehaviour)this).StartCoroutine(ManaRuneEnergyBurstRoutine(rune.rectTransform, energy, enemy ? "Enemy Mana Rune Reserve Burst" : "Mana Rune Reserve Burst"));
	}

	private System.Collections.IEnumerator ManaRuneEnergyBurstRoutine(RectTransform rune, Color energy, string effectName)
	{
		GameObject burstObject = CreateImage(effectName + " Core", (Transform)(object)safeAreaRoot, energy).gameObject;
		Image burst = burstObject.GetComponent<Image>();
		burst.sprite = BuildManaAuraSprite();
		burst.raycastTarget = false;
		RectTransform burstRect = burst.rectTransform;
		burstRect.anchorMin = burstRect.anchorMax = new Vector2(0.5f, 0.5f);
		burstRect.pivot = new Vector2(0.5f, 0.5f);
		burstRect.sizeDelta = new Vector2(150f, 150f);

		GameObject ringObject = CreateImage(effectName + " Ring", (Transform)(object)safeAreaRoot, energy).gameObject;
		Image ring = ringObject.GetComponent<Image>();
		ring.sprite = BuildManaRingSprite();
		ring.raycastTarget = false;
		RectTransform ringRect = ring.rectTransform;
		ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
		ringRect.pivot = new Vector2(0.5f, 0.5f);
		ringRect.sizeDelta = new Vector2(112f, 112f);

		const float duration = 0.58f;
		float elapsed = 0f;
		while (elapsed < duration && rune != null)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			float expand = Mathf.SmoothStep(0f, 1f, progress);
			float alpha = Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 2.4f));
			Vector3 center = rune.position;
			burstRect.position = center;
			burstRect.localScale = Vector3.one * Mathf.Lerp(0.45f, 2.65f, expand);
			burst.color = new Color(energy.r, energy.g, energy.b, alpha * 0.72f);
			ringRect.position = center;
			ringRect.localScale = Vector3.one * Mathf.Lerp(0.38f, 3.2f, expand);
			ringRect.localRotation = Quaternion.Euler(0f, 0f, progress * 260f);
			ring.color = new Color(1f, 0.93f, 0.54f, alpha);
			yield return null;
		}

		Object.Destroy(burstObject);
		Object.Destroy(ringObject);
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
			manaRuneAuraImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.86f, 2.2f, pulse);
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

		bool visible = BattleManaHudEnabled
			&& !waitingForCampaignBossReveal
			&& (!IsTutorialWarriorDuelActive
				|| tutorialMageDuelActive
				|| tutorialWarriorDuelStep >= TutorialWarriorDuelStep.Mana);
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

	private void SetPresentedManaValue(int value)
	{
		if (manaValueTweenRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(manaValueTweenRoutine);
			manaValueTweenRoutine = null;
		}

		manaDisplayedValue = Mathf.Max(0, value);
		if (manaRuneText != null)
			manaRuneText.text = manaDisplayedValue.ToString();
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
			46,
			(FontStyle)1,
			(TextAnchor)4);
		label.text = delta > 0 ? $"+{delta}" : delta.ToString();
		label.color = ManaDeltaColor;
		label.raycastTarget = false;
		label.horizontalOverflow = HorizontalWrapMode.Overflow;
		label.verticalOverflow = VerticalWrapMode.Overflow;
		GameObject labelObject = ((Component)label).gameObject;
		manaDeltaCallouts.Add(labelObject);

		Outline outline = ((Component)label).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.02f, 0.05f, 0.12f, 0.95f);
		outline.effectDistance = new Vector2(3f, -3f);
		outline.useGraphicAlpha = true;

		RectTransform rect = label.rectTransform;
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
		rect.pivot = new Vector2(0.5f, 0f);
		rect.sizeDelta = new Vector2(180f, 64f);

		// Variazioni ravvicinate non si sovrappongono perfettamente.
		float lane = (manaDeltaCalloutIndex++ % 3) * 12f;
		RectTransform runeRect = manaRuneImage.rectTransform;
		RectTransform safeRect = safeAreaRoot;
		Vector3 runeCenterLocal = safeRect.InverseTransformPoint(
			runeRect.TransformPoint(runeRect.rect.center));
		float x = runeCenterLocal.x - safeRect.rect.xMin + lane;
		float startY = runeCenterLocal.y - safeRect.rect.yMin;
		float endY = startY + 110f;
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
			manaDeltaCallouts.Remove(labelObject);
			Object.Destroy(labelObject);
		}
	}

	private void ClearManaDeltaCallouts()
	{
		foreach (GameObject callout in manaDeltaCallouts)
		{
			if (callout == null)
				continue;

			callout.SetActive(false);
			Object.Destroy(callout);
		}
		manaDeltaCallouts.Clear();
		manaDeltaCalloutIndex = 0;
	}
}
}
