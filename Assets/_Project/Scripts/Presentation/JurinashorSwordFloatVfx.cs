using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
	internal sealed class JurinashorSwordFloatVfx : MonoBehaviour
	{
		private RectTransform rect;
		private RectTransform floatingVisual;
		private Image swordGraphic;
		private Outline pulseOutline;
		private Outline outerGlowOutline;
		private Vector2 origin;
		private float phase;
		private float horizontalAmplitude;
		private float verticalAmplitude;
		private float horizontalSpeed;
		private float verticalSpeed;
		private float rotationSpeed;
		private float rotationDirection;
		private float rotationWobble;
		private Vector2 targetAnchor;
		private Vector2 anchorVelocity;
		private bool hasLayoutTarget;
		private bool summoning;
		private float summonProgress = 1f;
		private bool settlingAfterSummon;
		private float summonFinishedAt;
		private bool executionChanneling;

		public RectTransform BeamOrigin => floatingVisual != null ? floatingVisual : rect;

		private void Awake()
		{
			rect = transform as RectTransform;
			Image swordImage = null;
			foreach (Image candidate in GetComponentsInChildren<Image>(true))
			{
				if (candidate.sprite != null
					&& candidate.sprite.name.IndexOf("jurinashor_weapon", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					swordImage = candidate;
					break;
				}
			}
			if (swordImage == null)
				swordImage = GetComponentInChildren<Image>();
			swordGraphic = swordImage;
			floatingVisual = swordImage != null ? swordImage.rectTransform : rect;
			if (floatingVisual != null)
				origin = floatingVisual.anchoredPosition;
			if (swordImage != null)
			{
				pulseOutline = swordImage.gameObject.AddComponent<Outline>();
				pulseOutline.effectColor = new Color(0.38f, 1f, 0.58f, 0.82f);
				pulseOutline.effectDistance = new Vector2(5f, -5f);
				pulseOutline.useGraphicAlpha = true;
				outerGlowOutline = swordImage.gameObject.AddComponent<Outline>();
				outerGlowOutline.effectColor = new Color(0.12f, 1f, 0.42f, 0.34f);
				outerGlowOutline.effectDistance = new Vector2(11f, -11f);
				outerGlowOutline.useGraphicAlpha = true;
			}
			phase = Random.Range(0f, Mathf.PI * 2f);
			if (rect != null)
				targetAnchor = rect.anchorMin;
			horizontalAmplitude = Random.Range(5f, 12f);
			verticalAmplitude = Random.Range(10f, 19f);
			horizontalSpeed = Random.Range(0.52f, 0.91f);
			verticalSpeed = Random.Range(0.88f, 1.27f);
			rotationSpeed = Random.Range(7f, 15f);
			rotationDirection = Random.value < 0.5f ? -1f : 1f;
			rotationWobble = Random.Range(2.5f, 6.5f);
		}

		private void LateUpdate()
		{
			if (rect == null)
				return;
			if (hasLayoutTarget)
			{
				Vector2 anchor = Vector2.SmoothDamp(rect.anchorMin, targetAnchor, ref anchorVelocity, 0.28f, Mathf.Infinity, Time.unscaledDeltaTime);
				rect.anchorMin = anchor;
				rect.anchorMax = anchor;
			}
			float elapsed = Time.unscaledTime;
			float motionTime = settlingAfterSummon ? Mathf.Max(0f, elapsed - summonFinishedAt) : elapsed;
			float floatingBlend = settlingAfterSummon
				? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.72f, motionTime))
				: 1f;
			float horizontal = summoning || executionChanneling ? 0f : Mathf.Sin(motionTime * horizontalSpeed + phase) * horizontalAmplitude * floatingBlend;
			float vertical = summoning || executionChanneling ? 0f : Mathf.Sin(motionTime * verticalSpeed + phase * 1.37f) * verticalAmplitude * floatingBlend;
			vertical -= Mathf.Lerp(175f, 0f, Mathf.SmoothStep(0f, 1f, summonProgress));
			if (floatingVisual != null && !executionChanneling)
				floatingVisual.anchoredPosition = origin + new Vector2(horizontal, vertical);
			float rotation = (motionTime * rotationSpeed * rotationDirection
				+ Mathf.Sin(motionTime * horizontalSpeed * 0.73f + phase) * rotationWobble) * floatingBlend;
			if (floatingVisual != null && !summoning && !executionChanneling)
				floatingVisual.localRotation = Quaternion.Euler(0f, 0f, rotation);
			if (pulseOutline != null)
			{
				float glow = (Mathf.Sin(elapsed * verticalSpeed * 1.35f + phase + 0.9f) + 1f) * 0.5f;
				pulseOutline.effectColor = new Color(0.42f, 1f, 0.62f, Mathf.Lerp(0.58f, 0.92f, glow));
				float thickness = Mathf.Lerp(4f, 7f, glow);
				pulseOutline.effectDistance = new Vector2(thickness, -thickness);
				if (outerGlowOutline != null)
				{
					float outerThickness = Mathf.Lerp(9f, 14f, glow);
					outerGlowOutline.effectDistance = new Vector2(outerThickness, -outerThickness);
					outerGlowOutline.effectColor = new Color(0.1f, 1f, 0.4f, Mathf.Lerp(0.22f, 0.46f, glow));
				}
			}
		}

		public IEnumerator EnterExecutionPose(Vector3 targetWorldPosition)
		{
			if (floatingVisual == null)
				yield break;
			executionChanneling = true;
			Vector2 startPosition = floatingVisual.anchoredPosition;
			Quaternion startRotation = floatingVisual.localRotation;
			Vector3 direction = targetWorldPosition - floatingVisual.position;
			Quaternion targetRotation = direction.sqrMagnitude > 0.001f
				? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f)
				: Quaternion.identity;
			const float duration = 0.28f;
			for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
			{
				float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
				floatingVisual.anchoredPosition = Vector2.Lerp(startPosition, origin, p);
				floatingVisual.localRotation = Quaternion.Slerp(startRotation, targetRotation, p);
				yield return null;
			}
			floatingVisual.anchoredPosition = origin;
			floatingVisual.localRotation = targetRotation;
		}

		public void ExitExecutionPose()
		{
			executionChanneling = false;
			settlingAfterSummon = true;
			summonFinishedAt = Time.unscaledTime;
		}

		public IEnumerator PlayNecromanticSummon()
		{
			if (rect == null || floatingVisual == null)
				yield break;

			summoning = true;
			summonProgress = 0f;
			floatingVisual.localRotation = Quaternion.identity;
			Color swordBaseColor = swordGraphic != null ? swordGraphic.color : Color.white;
			if (swordGraphic != null)
				swordGraphic.color = new Color(swordBaseColor.r, swordBaseColor.g, swordBaseColor.b, 0f);

			GameObject poolObject = new GameObject("Jurinashor Necromantic Summoning Pool", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform poolRect = poolObject.GetComponent<RectTransform>();
			poolRect.SetParent(rect, false);
			poolRect.anchorMin = poolRect.anchorMax = new Vector2(0.5f, 0.5f);
			poolRect.anchoredPosition = new Vector2(0f, -76f);
			poolRect.sizeDelta = new Vector2(210f, 92f);
			poolRect.localScale = new Vector3(0.05f, 0.05f, 1f);
			poolRect.SetAsFirstSibling();
			Image poolImage = poolObject.GetComponent<Image>();
			poolImage.sprite = Resources.Load<Sprite>("UI/necromancer_terrain_aaa");
			poolImage.preserveAspect = true;
			poolImage.raycastTarget = false;
			poolImage.color = new Color(0.2f, 1f, 0.34f, 0f);

			const float openDuration = 0.30f;
			for (float elapsed = 0f; elapsed < openDuration; elapsed += Time.unscaledDeltaTime)
			{
				float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openDuration));
				poolRect.localScale = new Vector3(p, p, 1f);
				poolImage.color = new Color(0.2f, 1f, 0.34f, p * 0.88f);
				yield return null;
			}

			const float emergeDuration = 0.82f;
			for (float elapsed = 0f; elapsed < emergeDuration; elapsed += Time.unscaledDeltaTime)
			{
				summonProgress = Mathf.Clamp01(elapsed / emergeDuration);
				if (swordGraphic != null)
				{
					float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(summonProgress * 2.2f));
					swordGraphic.color = new Color(swordBaseColor.r, swordBaseColor.g, swordBaseColor.b, alpha);
				}
				float pulse = 1f + Mathf.Sin(summonProgress * Mathf.PI * 4f) * 0.06f;
				poolRect.localScale = new Vector3(pulse, pulse, 1f);
				yield return null;
			}

			summonProgress = 1f;
			if (swordGraphic != null)
				swordGraphic.color = swordBaseColor;
			const float closeDuration = 0.38f;
			for (float elapsed = 0f; elapsed < closeDuration; elapsed += Time.unscaledDeltaTime)
			{
				float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / closeDuration));
				float scale = 1f - p;
				poolRect.localScale = new Vector3(scale, scale, 1f);
				poolImage.color = new Color(0.2f, 1f, 0.34f, (1f - p) * 0.88f);
				yield return null;
			}
			Destroy(poolObject);
			summoning = false;
			settlingAfterSummon = true;
			summonFinishedAt = Time.unscaledTime;
		}

		public void SetLayoutAnchor(Vector2 anchor)
		{
			targetAnchor = anchor;
			hasLayoutTarget = true;
		}

		public static IEnumerator PlayNecromanticDeath(RectTransform sword)
		{
			if (sword == null)
				yield break;
			Sprite flame = Resources.Load<Sprite>("UI/necromancer_soul_wisp_aaa");
			GameObject burst = new GameObject("Jurinashor Sword Necromantic Flame Burst", typeof(RectTransform));
			RectTransform burstRect = burst.GetComponent<RectTransform>();
			// Il burst deve vivere fuori dalla spada: se fosse suo figlio collasserebbe
			// assieme alla lama e sembrerebbe ancora una semplice riduzione di scala.
			burstRect.SetParent(sword.parent, false);
			burstRect.anchorMin = burstRect.anchorMax = new Vector2(0.5f, 0.5f);
			burstRect.position = sword.position;
			burstRect.sizeDelta = new Vector2(300f, 300f);
			burstRect.SetAsLastSibling();

			const int count = 18;
			Image[] wisps = new Image[count];
			Vector2[] starts = new Vector2[count];
			Vector2[] velocities = new Vector2[count];
			for (int i = 0; i < count; i++)
			{
				GameObject particle = new GameObject($"Necromantic Flame {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
				RectTransform particleRect = particle.GetComponent<RectTransform>();
				particleRect.SetParent(burstRect, false);
				particleRect.anchorMin = particleRect.anchorMax = new Vector2(0.5f, 0.5f);
				float angle = Random.Range(0f, Mathf.PI * 2f);
				Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				starts[i] = direction * Random.Range(0f, 24f);
				velocities[i] = direction * Random.Range(165f, 310f) + Vector2.up * Random.Range(20f, 85f);
				particleRect.anchoredPosition = starts[i];
				float size = Random.Range(48f, 96f);
				particleRect.sizeDelta = new Vector2(size, size * Random.Range(1.25f, 1.8f));
				particleRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-28f, 28f));
				Image image = particle.GetComponent<Image>();
				image.sprite = flame;
				image.preserveAspect = true;
				image.raycastTarget = false;
				image.color = new Color(0.22f, 1f, 0.38f, 0f);
				wisps[i] = image;
			}

			Vector3 startScale = sword.localScale;
			const float duration = 0.68f;
			bool swordConsumed = false;
			for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
			{
				float p = Mathf.Clamp01(elapsed / duration);
				float flameAlpha = Mathf.Clamp01(Mathf.Min(p * 12f, (1f - p) * 2.4f));
				for (int i = 0; i < wisps.Length; i++)
				{
					if (wisps[i] == null) continue;
					RectTransform particleRect = wisps[i].rectTransform;
					particleRect.anchoredPosition = starts[i] + velocities[i] * (p * duration);
					particleRect.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.5f, p);
					wisps[i].color = new Color(0.18f, 1f, 0.32f, flameAlpha * 0.92f);
				}

				if (!swordConsumed && sword != null)
				{
					// Breve colpo di scala verso l'esterno, poi la lama viene inghiottita
					// istantaneamente dalle fiamme: nessuna animazione di rimpicciolimento.
					float flash = Mathf.Clamp01(p / 0.16f);
					sword.localScale = startScale * Mathf.Lerp(1f, 1.22f, flash);
					if (p >= 0.16f)
					{
						swordConsumed = true;
						sword.gameObject.SetActive(false);
					}
				}
				yield return null;
			}

			Destroy(burst);
		}
	}
}
