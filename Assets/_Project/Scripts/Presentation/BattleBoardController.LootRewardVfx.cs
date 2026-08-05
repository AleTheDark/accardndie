using System.Collections;
using System.Collections.Generic;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const float LootRewardRevealDuration = 0.95f;
	private static Sprite lootRewardParticleSprite;
	private GameObject activeLootRewardRevealRoot;
	private Coroutine activeLootRewardFireworksRoutine;

	private IEnumerator PlayLootRewardReveal(IReadOnlyList<CardDefinition> rewards)
	{
		if (rewards == null || rewards.Count == 0 || (Object)(object)safeAreaRoot == (Object)null)
			yield break;

		foreach (CardDefinition reward in rewards)
		{
			if ((Object)(object)reward == (Object)null)
				continue;

			yield return PlaySingleLootRewardReveal(reward);
		}
	}

	private IEnumerator PlaySingleLootRewardReveal(CardDefinition reward)
	{
		ClearLootRewardReveal();
		GameObject root = new GameObject("Loot Reward AAA Reveal", typeof(RectTransform), typeof(CanvasGroup));
		activeLootRewardRevealRoot = root;
		root.transform.SetParent((Transform)(object)safeAreaRoot, false);
		root.transform.SetAsLastSibling();
		RectTransform rootRect = (RectTransform)root.transform;
		Stretch(rootRect);
		rootRect.anchorMin = new Vector2(0f, 0f);
		rootRect.anchorMax = new Vector2(1f, 1f);
		rootRect.offsetMin = new Vector2(0f, 140f);
		rootRect.offsetMax = new Vector2(0f, 140f);
		rootRect.localScale = Vector3.one * 1.1f;

		CanvasGroup group = root.GetComponent<CanvasGroup>();
		group.alpha = 0f;
		group.blocksRaycasts = false;

		Image veil = CreateLootRewardImage("Loot Reward Veil", root.transform, new Color(0.015f, 0.01f, 0.03f, 0.86f));
		Stretch(veil.rectTransform);

		Image halo = CreateLootRewardImage("Loot Reward Halo", root.transform, new Color(1f, 0.74f, 0.22f, 0.32f));
		RectTransform haloRect = halo.rectTransform;
		haloRect.anchorMin = new Vector2(0.5f, 0.5f);
		haloRect.anchorMax = new Vector2(0.5f, 0.5f);
		haloRect.pivot = new Vector2(0.5f, 0.5f);
		haloRect.sizeDelta = new Vector2(760f, 760f);

		Image ring = CreateLootRewardImage("Loot Reward Ring", root.transform, new Color(0.38f, 0.95f, 1f, 0.24f));
		RectTransform ringRect = ring.rectTransform;
		ringRect.anchorMin = new Vector2(0.5f, 0.5f);
		ringRect.anchorMax = new Vector2(0.5f, 0.5f);
		ringRect.pivot = new Vector2(0.5f, 0.5f);
		ringRect.sizeDelta = new Vector2(620f, 620f);

		// Rye è già pesante e ha un dettaglio inline: il bold+italic sintetico
		// lo impasta, quindi il titolo resta in stile normale.
		Text title = CreateText("Loot Reward Title", root.transform, LootRewardTitleFont(), 38, FontStyle.Normal, TextAnchor.MiddleCenter);
		title.text = "RICOMPENSA OTTENUTA";
		title.color = new Color(1f, 0.88f, 0.48f, 1f);
		Outline titleOutline = ((Component)title).gameObject.AddComponent<Outline>();
		titleOutline.effectColor = new Color(0.28f, 0.12f, 0.02f, 0.95f);
		titleOutline.effectDistance = new Vector2(3f, -3f);
		Shadow titleShadow = ((Component)title).gameObject.AddComponent<Shadow>();
		titleShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
		titleShadow.effectDistance = new Vector2(0f, -7f);
		RectTransform titleRect = title.rectTransform;
		titleRect.anchorMin = new Vector2(0.5f, 0.72f);
		titleRect.anchorMax = new Vector2(0.5f, 0.72f);
		titleRect.pivot = new Vector2(0.5f, 0.5f);
		titleRect.anchoredPosition = new Vector2(0f, 75f);
		titleRect.sizeDelta = new Vector2(780f, 82f);
		titleRect.localScale = Vector3.one * 1.3f;

		PrototypeCardView card = PrototypeCardView.Create(root.transform, reward, configuration);
		card.SetInteractable(false);
		card.SetLayoutIgnored(true);
		card.SetAlpha(0f);
		RectTransform cardRect = card.RectTransform;
		cardRect.anchorMin = new Vector2(0.5f, 0.5f);
		cardRect.anchorMax = new Vector2(0.5f, 0.5f);
		cardRect.pivot = new Vector2(0.5f, 0.5f);
		cardRect.sizeDelta = new Vector2(330f, 492f);

		List<LootRewardParticle> particles = CreateLootRewardFireworks(root.transform);

		float elapsed = 0f;
		while (elapsed < LootRewardRevealDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / LootRewardRevealDuration);
			float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
			float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.12f) / 0.46f));
			float pulse = 1f + Mathf.Sin(t * Mathf.PI * 8f) * 0.035f;

			group.alpha = appear;
			card.SetAlpha(appear);
			cardRect.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f, -210f), new Vector2(0f, -18f), settle);
			cardRect.localScale = Vector3.one * Mathf.LerpUnclamped(1.72f, 1.34f * pulse, settle);
			cardRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-10f, 2f * Mathf.Sin(t * Mathf.PI * 2f), settle));

			haloRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1.18f + 0.08f * Mathf.Sin(t * Mathf.PI * 6f), appear);
			halo.color = new Color(1f, 0.74f, 0.22f, Mathf.Lerp(0.38f, 0.22f, t));
			ringRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.45f, 1.42f, appear);
			ringRect.localRotation = Quaternion.Euler(0f, 0f, t * 150f);
			ring.color = new Color(0.38f, 0.95f, 1f, Mathf.Lerp(0.34f, 0.12f, t));
			titleRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(101f, 75f, appear));
			title.color = new Color(1f, 0.88f, 0.48f, appear);

			UpdateLootRewardFireworks(particles, t, 0f);
			yield return null;
		}

		group.alpha = 1f;
		card.SetAlpha(1f);
		cardRect.anchoredPosition = new Vector2(0f, -18f);
		cardRect.localScale = Vector3.one * 1.34f;
		cardRect.localRotation = Quaternion.identity;
		activeLootRewardFireworksRoutine = StartCoroutine(PlayPersistentLootRewardFireworks(particles, haloRect, ringRect, halo, ring));
	}

	private void ClearLootRewardReveal()
	{
		if (activeLootRewardFireworksRoutine != null)
		{
			StopCoroutine(activeLootRewardFireworksRoutine);
			activeLootRewardFireworksRoutine = null;
		}
		if ((Object)(object)activeLootRewardRevealRoot != (Object)null)
		{
			Object.Destroy((Object)(object)activeLootRewardRevealRoot);
			activeLootRewardRevealRoot = null;
		}
	}

	private IEnumerator PlayPersistentLootRewardFireworks(
		List<LootRewardParticle> particles,
		RectTransform haloRect,
		RectTransform ringRect,
		Image halo,
		Image ring)
	{
		float clock = 0f;
		while ((Object)(object)activeLootRewardRevealRoot != (Object)null)
		{
			clock += Time.unscaledDeltaTime;
			haloRect.localScale = Vector3.one * (1.15f + 0.06f * Mathf.Sin(clock * Mathf.PI * 2.4f));
			ringRect.localScale = Vector3.one * (1.35f + 0.08f * Mathf.Sin(clock * Mathf.PI * 3.1f));
			ringRect.localRotation = Quaternion.Euler(0f, 0f, clock * 55f);
			halo.color = new Color(1f, 0.74f, 0.22f, 0.18f + 0.06f * Mathf.Sin(clock * Mathf.PI * 3f));
			ring.color = new Color(0.38f, 0.95f, 1f, 0.09f + 0.05f * Mathf.Sin(clock * Mathf.PI * 2f));
			UpdatePersistentLootRewardFireworks(particles, clock);
			yield return null;
		}
	}

	private static Image CreateLootRewardImage(string name, Transform parent, Color color)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		obj.transform.SetParent(parent, false);
		Image image = obj.GetComponent<Image>();
		image.sprite = LootRewardParticleSprite();
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	private List<LootRewardParticle> CreateLootRewardFireworks(Transform root)
	{
		var particles = new List<LootRewardParticle>(144);
		Color[] colors =
		{
			new Color(1f, 0.82f, 0.25f, 1f),
			new Color(0.25f, 0.9f, 1f, 1f),
			new Color(1f, 0.32f, 0.62f, 1f),
			new Color(0.7f, 1f, 0.42f, 1f),
			new Color(0.86f, 0.62f, 1f, 1f)
		};

		for (int side = -1; side <= 1; side += 2)
		{
			for (int i = 0; i < 72; i++)
			{
				Image image = CreateLootRewardImage("Loot Firework Spark", root, colors[i % colors.Length]);
				RectTransform rect = image.rectTransform;
				rect.anchorMin = new Vector2(0.5f, 0.5f);
				rect.anchorMax = new Vector2(0.5f, 0.5f);
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.sizeDelta = Vector2.one * Mathf.Lerp(8f, 22f, (i % 7) / 6f);
				float angle = Mathf.Lerp(-74f, 74f, (i % 20) / 19f) + (i / 20) * 12f;
				Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
				if (side < 0)
					direction.x = -Mathf.Abs(direction.x);
				else
					direction.x = Mathf.Abs(direction.x);
				particles.Add(new LootRewardParticle(
					rect,
					image,
					new Vector2(side * Random.Range(245f, 380f), Random.Range(-84f, 36f)),
					direction.normalized,
					Random.Range(170f, 500f),
					Random.Range(0f, 0.34f),
					Random.Range(0.55f, 0.95f),
					Random.Range(1.15f, 2.65f),
					Random.Range(0f, 2.65f)));
			}
		}

		return particles;
	}

	private static void UpdateLootRewardFireworks(List<LootRewardParticle> particles, float t, float exit)
	{
		for (int i = 0; i < particles.Count; i++)
		{
			LootRewardParticle particle = particles[i];
			float local = Mathf.Clamp01((t - particle.Delay) / 0.64f);
			float burst = Mathf.Sin(local * Mathf.PI);
			Vector2 drift = particle.Direction * particle.Distance * Mathf.SmoothStep(0f, 1f, local);
			drift.y -= 90f * local * local;
			particle.Rect.anchoredPosition = particle.Origin + drift;
			particle.Rect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.18f, burst);
			Color color = particle.Image.color;
			color.a = Mathf.Clamp01(burst * (1f - exit));
			particle.Image.color = color;
		}
	}

	private static void UpdatePersistentLootRewardFireworks(List<LootRewardParticle> particles, float clock)
	{
		for (int i = 0; i < particles.Count; i++)
		{
			LootRewardParticle particle = particles[i];
			float cycleTime = Mathf.Repeat(clock + particle.Phase, particle.CycleDuration);
			float local = Mathf.Clamp01((cycleTime - particle.Delay) / particle.BurstDuration);
			float active = cycleTime >= particle.Delay && cycleTime <= particle.Delay + particle.BurstDuration ? 1f : 0f;
			float burst = Mathf.Sin(local * Mathf.PI) * active;
			Vector2 drift = particle.Direction * particle.Distance * Mathf.SmoothStep(0f, 1f, local);
			drift.y -= 90f * local * local;
			particle.Rect.anchoredPosition = particle.Origin + drift;
			particle.Rect.localScale = Vector3.one * Mathf.Lerp(0.28f, 1.28f, burst);
			Color color = particle.Image.color;
			color.a = Mathf.Clamp01(burst);
			particle.Image.color = color;
		}
	}

	private static Font LootRewardTitleFont()
	{
		Font font = AccardND.Battlefield.MmoUiTheme.DisplayFont;
		if (font != null)
			return font;
		font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
	}

	private static Sprite LootRewardParticleSprite()
	{
		if ((Object)(object)lootRewardParticleSprite != (Object)null)
			return lootRewardParticleSprite;

		Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
		for (int y = 0; y < texture.height; y++)
		{
			for (int x = 0; x < texture.width; x++)
			{
				Vector2 p = new Vector2((x + 0.5f) / texture.width - 0.5f, (y + 0.5f) / texture.height - 0.5f);
				float alpha = Mathf.Clamp01(1f - p.magnitude * 2f);
				texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
			}
		}
		texture.Apply();
		lootRewardParticleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
		lootRewardParticleSprite.name = "Loot Reward Soft Particle";
		return lootRewardParticleSprite;
	}

	private sealed class LootRewardParticle
	{
		public LootRewardParticle(
			RectTransform rect,
			Image image,
			Vector2 origin,
			Vector2 direction,
			float distance,
			float delay,
			float burstDuration,
			float cycleDuration,
			float phase)
		{
			Rect = rect;
			Image = image;
			Origin = origin;
			Direction = direction;
			Distance = distance;
			Delay = delay;
			BurstDuration = burstDuration;
			CycleDuration = cycleDuration;
			Phase = phase;
		}

		public RectTransform Rect { get; }
		public Image Image { get; }
		public Vector2 Origin { get; }
		public Vector2 Direction { get; }
		public float Distance { get; set; }
		public float Delay { get; set; }
		public float BurstDuration { get; }
		public float CycleDuration { get; }
		public float Phase { get; }
	}
}
}
