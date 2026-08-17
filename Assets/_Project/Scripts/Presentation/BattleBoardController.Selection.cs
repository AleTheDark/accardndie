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
	private Text seraphelBossNameText;
	private Text trentorBossNameText;
	private Text bragusBossNameText;
	private Text jurinashorBossNameText;
	private Text palatirBossNameText;
	private Text medusaBossNameText;
	private Text composableGolemBossNameText;
	private bool seraphelEntranceVfxPlayed;
	private bool seraphelEntranceVfxRunning;
	private GameObject seraphelRevealHealthRoot;

	private BattleCardState AddCard(ICollection<BattleCardState> destination, RectTransform row, CardDefinition definition, bool belongsToPlayer, int index, CampaignCardInstance campaignCard = null)
	{
		if ((Object)(object)definition == (Object)null
			|| (!definition.CanEnterCombat && !IsMedusaBossDefinition(definition) && !IsTrentorBossDefinition(definition) && !IsBragusBossDefinition(definition) && !IsSeraphelBossDefinition(definition) && !IsPalatirBossDefinition(definition))
			|| (Object)(object)row == (Object)null)
		{
			return null;
		}
		PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)row, definition, configuration);
		if (!belongsToPlayer && IsBragusBossDefinition(definition))
		{
			prototypeCardView.ConfigureBragusBackdropPresentation();
		}
		else if (!belongsToPlayer && IsJurinashorBossDefinition(definition))
		{
			prototypeCardView.ConfigureJurinashorBackdropPresentation();
		}
		else if (!belongsToPlayer && IsTrentorBossDefinition(definition))
		{
			prototypeCardView.ConfigureTrentorBackdropPresentation();
		}
		else if (!belongsToPlayer && IsSeraphelBossDefinition(definition))
		{
			prototypeCardView.ConfigureSeraphelBackdropPresentation();
		}
		else if (!belongsToPlayer && IsPalatirBossDefinition(definition))
		{
			prototypeCardView.ConfigurePalatirBackdropPresentation();
		}
		BattleCardState state = new BattleCardState(definition, prototypeCardView, belongsToPlayer, campaignCard);
		if (belongsToPlayer)
		{
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				HandlePlayerCardClick(state);
			});
		}
		else
		{
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				HandleCpuCardClick(index);
			});
		}
		destination.Add(state);
		if (!belongsToPlayer && IsComposableGolemDefinition(definition))
		{
			RefreshComposableGolemPawn(state);
			EnsureBossName(ref composableGolemBossNameText, "StorgBot");
		}
		if (!belongsToPlayer && IsMedusaBossDefinition(definition))
		{
			activeMedusaBoss ??= new MedusaBoss(random);
			RefreshMedusaBossPawn(state);
			EnsureBossName(ref medusaBossNameText, "Medusa");
		}
		if (!belongsToPlayer && IsTrentorBossDefinition(definition))
		{
			activeTrentorBoss ??= new TrentorBoss(random);
			PlayTrentorJoinBattlefieldSfx();
			RefreshTrentorBossPawn(state);
			ActivateTrentorBossRoomPresentation(state);
		}
		if (!belongsToPlayer && IsBragusBossDefinition(definition))
		{
			activeBragusBoss ??= new BragusBoss(random);
			RefreshBragusBossPawn(state);
			ActivateBragusBossRoomPresentation(state);
		}
		if (!belongsToPlayer && IsJurinashorBossDefinition(definition))
		{
			activeJurinashorBoss ??= new JurinashorBoss();
			RefreshJurinashorBossPawn(state);
			ActivateJurinashorBossRoomPresentation(state);
		}
		if (!belongsToPlayer && IsPalatirBossDefinition(definition))
		{
			activePalatirBoss ??= new PalatirBoss(random);
			RefreshPalatirBossPawn(state);
			ActivatePalatirBossRoomPresentation(state);
		}
		if (!belongsToPlayer && IsSeraphelBossDefinition(definition))
		{
			activeSeraphelBoss ??= CreateSeraphelForCurrentRoom();
			RefreshSeraphelBossPawn(state);
			ActivateSeraphelBossRoomPresentation(state);
		}
		return state;
	}

	private void ActivateTrentorBossRoomPresentation(BattleCardState trentor)
	{
		HideAllBossNames();
		trentorBossPresentationActive = true;
		RefreshScenarioBackground();

		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
			((Component)cpuHud.Rect).gameObject.SetActive(false);

		if (trentor != null && (Object)(object)trentor.View != (Object)null
			&& (Object)(object)safeAreaRoot != (Object)null)
		{
			trentor.View.MoveHealthBarToScreen(
				safeAreaRoot,
				new Vector2(0.075f, 0.935f),
				new Vector2(0.925f, 0.970f));
			UpdateTrentorBossHealthBar(trentor);
		}
		EnsureTrentorBossName();
		RefreshEnemyManaHud();
		ApplySeraphelExclusiveLayout();
	}

	private void ActivateBragusBossRoomPresentation(BattleCardState bragus)
	{
		HideAllBossNames();
		bragusBossPresentationActive = true;
		RefreshScenarioBackground();

		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
			((Component)cpuHud.Rect).gameObject.SetActive(false);

		if (bragus != null && (Object)(object)bragus.View != (Object)null
			&& (Object)(object)safeAreaRoot != (Object)null)
		{
			// Occupa esattamente la fascia superiore precedentemente usata dal CPU HUD.
			bragus.View.MoveHealthBarToScreen(
				safeAreaRoot,
				new Vector2(0.075f, 0.935f),
				new Vector2(0.925f, 0.970f));
			UpdateBragusBossHealthBar(bragus);
		}

		EnsureBossName(ref bragusBossNameText, "Bragus");
		RefreshEnemyManaHud();
		ApplySeraphelExclusiveLayout();
	}

	private void ActivatePalatirBossRoomPresentation(BattleCardState palatir)
	{
		HideAllBossNames();
		RefreshScenarioBackground();
		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
			((Component)cpuHud.Rect).gameObject.SetActive(false);
		if (palatir != null && (Object)(object)palatir.View != (Object)null && (Object)(object)safeAreaRoot != (Object)null)
		{
			palatir.View.MoveHealthBarToScreen(safeAreaRoot, new Vector2(0.075f, 0.935f), new Vector2(0.925f, 0.970f));
			UpdatePalatirBossHealthBar(palatir);
		}
		EnsureBossName(ref palatirBossNameText, "Palatir");
		RefreshEnemyManaHud();
		ApplySeraphelExclusiveLayout();
	}

	private void ActivateJurinashorBossRoomPresentation(BattleCardState jurinashor)
	{
		HideAllBossNames();
		jurinashorBossPresentationActive = true;
		RefreshScenarioBackground();

		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
			((Component)cpuHud.Rect).gameObject.SetActive(false);
		if (jurinashor != null && (Object)(object)jurinashor.View != (Object)null
			&& (Object)(object)safeAreaRoot != (Object)null)
		{
			jurinashor.View.MoveHealthBarToScreen(
				safeAreaRoot,
				new Vector2(0.075f, 0.935f),
				new Vector2(0.925f, 0.970f));
			UpdateJurinashorBossHealthBar(jurinashor);
		}

		EnsureBossName(ref jurinashorBossNameText, "Jurinashor");
		RefreshEnemyManaHud();
		ApplySeraphelExclusiveLayout();
	}

	private void ActivateSeraphelBossRoomPresentation(BattleCardState seraphel)
	{
		HideAllBossNames();
		seraphelBossPresentationActive = true;
		RefreshScenarioBackground();
		if ((Object)(object)seraphelRevealHealthRoot != (Object)null)
		{
			Object.Destroy(seraphelRevealHealthRoot);
			seraphelRevealHealthRoot = null;
		}

		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
			((Component)cpuHud.Rect).gameObject.SetActive(false);

		if (seraphel != null && (Object)(object)seraphel.View != (Object)null
			&& (Object)(object)safeAreaRoot != (Object)null)
		{
			seraphel.View.MoveHealthBarToScreen(
				safeAreaRoot,
				new Vector2(0.075f, 0.935f),
				new Vector2(0.925f, 0.970f));
			RefreshSeraphelBossPawn(seraphel);
		}

		EnsureSeraphelBossName();
		RefreshEnemyManaHud();
		ApplySeraphelExclusiveLayout();
		if (!seraphelEntranceVfxPlayed)
		{
			seraphelEntranceVfxPlayed = true;
			seraphelEntranceVfxRunning = true;
			((MonoBehaviour)this).StartCoroutine(PlaySeraphelSpiralCrossEntrance());
		}
	}

	private void PrepareSeraphelRevealHud()
	{
		HideAllBossNames();
		seraphelBossPresentationActive = true;
		EnsureSeraphelBossName();
		RefreshEnemyManaHud();
		ApplySeraphelExclusiveLayout();

		// Durante il reveal la proxy di combattimento non esiste ancora. Creiamo una
		// view temporanea dello stesso tipo del boss, così la barra è esattamente quella
		// definitiva (stessa cornice, riempimento e tipografia), non una sua imitazione.
		if ((Object)(object)seraphelRevealHealthRoot == (Object)null && (Object)(object)safeAreaRoot != (Object)null)
		{
			CardDefinition definition = FindCardDefinition(SeraphelBossCardId);
			if ((Object)(object)definition != (Object)null)
			{
				Transform previewParent = (Object)(object)cpuRow != (Object)null
					? (Transform)(object)cpuRow
					: (Transform)(object)safeAreaRoot;
				PrototypeCardView revealView = PrototypeCardView.CreateBattlefieldPreview(previewParent, definition, configuration);
				revealView.ConfigureSeraphelBackdropPresentation();
				revealView.MoveHealthBarToScreen(safeAreaRoot,
					new Vector2(0.075f, 0.935f), new Vector2(0.925f, 0.970f));
				revealView.SetClassHealthBar(SeraphelBoss.DefaultHitPoints, SeraphelBoss.DefaultHitPoints);
				seraphelRevealHealthRoot = ((Component)revealView).gameObject;
			}
			if ((Object)(object)seraphelBossNameText != (Object)null)
				((Component)seraphelBossNameText).transform.SetAsLastSibling();
		}

		if (!seraphelEntranceVfxPlayed)
		{
			seraphelEntranceVfxPlayed = true;
			seraphelEntranceVfxRunning = true;
			((MonoBehaviour)this).StartCoroutine(PlaySeraphelSpiralCrossEntrance());
		}
	}

	private IEnumerator PlaySeraphelSpiralCrossEntrance()
	{
		if ((Object)(object)safeAreaRoot == (Object)null)
		{
			seraphelEntranceVfxRunning = false;
			yield break;
		}

		GameObject rootObject = new GameObject("Seraphel Spiral Cross Entrance", typeof(RectTransform), typeof(CanvasGroup));
		RectTransform root = rootObject.GetComponent<RectTransform>();
		root.SetParent((Transform)(object)safeAreaRoot, false);
		Stretch(root);
		root.SetAsLastSibling();
		CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;
		Sprite crossSprite = Resources.Load<Sprite>("UI/priest_purification_cross");
		if ((Object)(object)crossSprite == (Object)null)
		{
			Debug.LogWarning("SERAPHEL REVEAL - asset UI/priest_purification_cross non trovato.");
			Object.Destroy(rootObject);
			seraphelEntranceVfxRunning = false;
			yield break;
		}

		const int crossCount = 32;
		RectTransform[] crosses = new RectTransform[crossCount];
		Image[] crossImages = new Image[crossCount];
		Image[] glowImages = new Image[crossCount];
		for (int i = 0; i < crossCount; i++)
		{
			Image crossImage = CreateImage($"Priest Purification Cross {i + 1}", root, Color.clear);
			crossImage.sprite = crossSprite;
			crossImage.preserveAspect = true;
			crossImage.raycastTarget = false;
			RectTransform cross = crossImage.rectTransform;
			cross.SetParent(root, false);
			cross.anchorMin = cross.anchorMax = new Vector2(0.5f, 0.5f);
			cross.pivot = new Vector2(0.5f, 0.5f);
			cross.sizeDelta = new Vector2(64f, 64f);
			crosses[i] = cross;
			crossImages[i] = crossImage;

			Image glow = CreateImage("Purification Cross Glow", cross, Color.clear);
			glow.sprite = crossSprite;
			glow.preserveAspect = true;
			glow.raycastTarget = false;
			Stretch(glow.rectTransform);
			glow.rectTransform.localScale = Vector3.one * 1.85f;
			glow.transform.SetAsFirstSibling();
			glowImages[i] = glow;
		}

		const float duration = 2.65f;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)root != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float time = Mathf.Clamp01(elapsed / duration);
			for (int i = 0; i < crossCount; i++)
			{
				float phase = i / (float)crossCount;
				float local = Mathf.Repeat(time * 1.05f + phase, 1f);
				float angle = phase * Mathf.PI * 5f + time * Mathf.PI * 4.6f;
				float radius = Mathf.Lerp(305f, 125f, local);
				float y = Mathf.Lerp(-190f, 520f, local);
				crosses[i].anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * 42f);
				crosses[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(angle) * 18f);
				float pulse = 1f + Mathf.Sin((time + phase) * 24f) * 0.16f;
				float scale = Mathf.Lerp(0.62f, 1.42f, local) * pulse;
				crosses[i].localScale = Vector3.one * scale;
				float alpha = Mathf.Sin(local * Mathf.PI) * Mathf.SmoothStep(0f, 1f, Mathf.Min(time * 5f, (1f - time) * 5f));
				crossImages[i].color = new Color(
					1f,
					Mathf.Lerp(0.78f, 1f, local),
					Mathf.Lerp(0.32f, 0.9f, local),
					alpha);
				glowImages[i].color = new Color(1f, 0.78f, 0.22f, alpha * (0.22f + 0.14f * pulse));
				glowImages[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.65f, 2.15f, pulse - 0.84f);
			}
			yield return null;
		}

		if ((Object)(object)rootObject != (Object)null)
			Object.Destroy(rootObject);
		seraphelEntranceVfxRunning = false;
	}

	private IEnumerator PlaySeraphelPhaseTwoElectricFury()
	{
		if ((Object)(object)safeAreaRoot == (Object)null)
			yield break;

		GameObject rootObject = new GameObject("Seraphel Phase Two Electric Fury", typeof(RectTransform), typeof(CanvasGroup));
		RectTransform root = rootObject.GetComponent<RectTransform>();
		root.SetParent((Transform)(object)safeAreaRoot, false);
		Stretch(root);
		root.SetAsLastSibling();
		CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;

		Image aura = CreateImage("White Ascension Aura", root, new Color(1f, 1f, 1f, 0f));
		aura.sprite = BuildManaAuraSprite();
		aura.raycastTarget = false;
		aura.rectTransform.anchorMin = aura.rectTransform.anchorMax = new Vector2(0.5f, 0.64f);
		aura.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		aura.rectTransform.sizeDelta = new Vector2(620f, 820f);

		Image flash = CreateImage("White Transformation Flash", root, new Color(1f, 1f, 1f, 0f));
		flash.raycastTarget = false;
		Stretch(flash.rectTransform);

		const int boltCount = 14;
		const int segmentsPerBolt = 4;
		RectTransform[,] segments = new RectTransform[boltCount, segmentsPerBolt];
		Image[,] segmentImages = new Image[boltCount, segmentsPerBolt];
		for (int bolt = 0; bolt < boltCount; bolt++)
		{
			for (int segment = 0; segment < segmentsPerBolt; segment++)
			{
				Image line = CreateImage($"White Lightning {bolt + 1}-{segment + 1}", root, Color.clear);
				line.raycastTarget = false;
				RectTransform rect = line.rectTransform;
				rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.64f);
				rect.pivot = new Vector2(0f, 0.5f);
				segments[bolt, segment] = rect;
				segmentImages[bolt, segment] = line;
			}
		}

		const float duration = 2.45f;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)root != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float envelope = Mathf.Sin(t * Mathf.PI);
			float fury = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((1f - t) * 5f);
			float pulse = 0.82f + Mathf.Abs(Mathf.Sin(elapsed * 15f)) * 0.32f;
			aura.color = new Color(1f, 1f, 1f, envelope * (0.22f + pulse * 0.18f));
			aura.rectTransform.localScale = new Vector3(pulse, pulse * 1.08f, 1f);
			flash.color = new Color(1f, 1f, 1f,
				(Mathf.Exp(-Mathf.Pow((t - 0.16f) * 20f, 2f)) + Mathf.Exp(-Mathf.Pow((t - 0.62f) * 28f, 2f))) * 0.38f);

			for (int bolt = 0; bolt < boltCount; bolt++)
			{
				float angle = bolt / (float)boltCount * Mathf.PI * 2f + elapsed * (bolt % 2 == 0 ? 0.8f : -0.65f);
				Vector2 point = new Vector2(Mathf.Cos(angle) * 245f, Mathf.Sin(angle) * 340f);
				for (int segment = 0; segment < segmentsPerBolt; segment++)
				{
					float seed = bolt * 7.13f + segment * 3.71f;
					Vector2 next = point * 0.72f + new Vector2(
						Mathf.Sin(elapsed * 23f + seed) * 38f,
						Mathf.Cos(elapsed * 19f + seed) * 46f);
					Vector2 delta = next - point;
					RectTransform rect = segments[bolt, segment];
					rect.anchoredPosition = point;
					rect.sizeDelta = new Vector2(delta.magnitude, 3f + (bolt % 3));
					rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
					float flicker = Mathf.Clamp01(Mathf.Sin(elapsed * 31f + seed) * 3f);
					segmentImages[bolt, segment].color = new Color(1f, 1f, 1f, fury * flicker);
					point = next;
				}
			}
			yield return null;
		}

		if ((Object)(object)rootObject != (Object)null)
			Object.Destroy(rootObject);
	}

	private IEnumerator PlaySeraphelRegenerationVfx()
	{
		if ((Object)(object)safeAreaRoot == (Object)null)
			yield break;

		GameObject rootObject = new GameObject("Seraphel Regenerating Light", typeof(RectTransform), typeof(CanvasGroup));
		RectTransform root = rootObject.GetComponent<RectTransform>();
		root.SetParent((Transform)(object)safeAreaRoot, false);
		Stretch(root);
		root.SetAsLastSibling();
		CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;

		Sprite crossSprite = Resources.Load<Sprite>("UI/priest_purification_cross");
		Image aura = CreateImage("Regenerating Light Aura", root, Color.clear);
		aura.sprite = BuildManaAuraSprite();
		aura.raycastTarget = false;
		aura.rectTransform.anchorMin = aura.rectTransform.anchorMax = new Vector2(0.5f, 0.64f);
		aura.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		aura.rectTransform.sizeDelta = new Vector2(500f, 690f);

		const int particleCount = 18;
		RectTransform[] particles = new RectTransform[particleCount];
		Image[] particleImages = new Image[particleCount];
		for (int index = 0; index < particleCount; index++)
		{
			Image particle = CreateImage($"Regenerating Cross {index + 1}", root, Color.clear);
			particle.sprite = crossSprite;
			particle.preserveAspect = true;
			particle.raycastTarget = false;
			RectTransform rect = particle.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.64f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(52f, 52f);
			particles[index] = rect;
			particleImages[index] = particle;
		}

		const float duration = 1.55f;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)root != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float time = Mathf.Clamp01(elapsed / duration);
			float envelope = Mathf.Sin(time * Mathf.PI);
			float pulse = 0.9f + Mathf.Sin(elapsed * 13f) * 0.12f;
			aura.color = new Color(0.78f, 1f, 0.62f, envelope * 0.42f);
			aura.rectTransform.localScale = Vector3.one * (0.82f + time * 0.38f) * pulse;

			for (int index = 0; index < particleCount; index++)
			{
				float phase = index / (float)particleCount;
				float local = Mathf.Repeat(time * 1.18f + phase, 1f);
				float angle = phase * Mathf.PI * 6f + time * Mathf.PI * 2.8f;
				float radius = Mathf.Lerp(255f, 62f, local);
				float y = Mathf.Lerp(-310f, 275f, local);
				particles[index].anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, y);
				particles[index].localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg * 0.16f);
				float particleScale = Mathf.Lerp(0.65f, 1.25f, local) * pulse;
				particles[index].localScale = Vector3.one * particleScale;
				float alpha = Mathf.Sin(local * Mathf.PI) * envelope;
				particleImages[index].color = Color.Lerp(
					new Color(0.4f, 1f, 0.48f, alpha),
					new Color(1f, 0.94f, 0.55f, alpha),
					local);
			}
			yield return null;
		}

		if ((Object)(object)rootObject != (Object)null)
			Object.Destroy(rootObject);
	}

	private IEnumerator PlaySeraphelSealRay(PrototypeCardView sourceView, PrototypeCardView targetView)
	{
		if ((Object)(object)safeAreaRoot == (Object)null
			|| (Object)(object)sourceView == (Object)null
			|| (Object)(object)targetView == (Object)null)
			yield break;

		RectTransform sourceRect = ((Component)sourceView).transform as RectTransform;
		RectTransform targetRect = ((Component)targetView).transform as RectTransform;
		if ((Object)(object)sourceRect == (Object)null || (Object)(object)targetRect == (Object)null)
			yield break;

		// La punta del bastone si trova nel quadrante alto-sinistro dell'artwork di Seraphel.
		// I bounds relativi alla Safe Area mantengono il punto corretto anche con risoluzioni diverse.
		Bounds sourceBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safeAreaRoot, sourceRect);
		Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safeAreaRoot, targetRect);
		Vector2 start = new Vector2(
			Mathf.Lerp(sourceBounds.min.x, sourceBounds.max.x, 0.20f),
			Mathf.Lerp(sourceBounds.min.y, sourceBounds.max.y, 0.86f));
		Vector2 end = new Vector2(targetBounds.center.x, targetBounds.center.y);
		Vector2 delta = end - start;
		float length = Mathf.Max(1f, delta.magnitude);
		Vector2 direction = delta / length;
		Vector2 perpendicular = new Vector2(-direction.y, direction.x);
		float angleDegrees = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

		GameObject rootObject = new GameObject("Seraphel Seal Ray", typeof(RectTransform), typeof(CanvasGroup));
		RectTransform root = rootObject.GetComponent<RectTransform>();
		root.SetParent((Transform)(object)safeAreaRoot, false);
		Stretch(root);
		root.SetAsLastSibling();
		CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;

		Sprite runeSprite = Resources.Load<Sprite>("UI/priest_purification_cross");
		Image staffGlow = CreateImage("Staff Tip Blue Pulse", root, Color.clear);
		staffGlow.sprite = BuildManaAuraSprite();
		staffGlow.raycastTarget = false;
		staffGlow.rectTransform.anchorMin = staffGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		staffGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		staffGlow.rectTransform.anchoredPosition = start;
		staffGlow.rectTransform.sizeDelta = new Vector2(155f, 155f);

		Image rayGlow = CreateImage("Seal Ray Blue Glow", root, new Color(0.1f, 0.58f, 1f, 0f));
		Image rayCore = CreateImage("Seal Ray White Core", root, new Color(0.78f, 0.94f, 1f, 0f));
		Image[] rays = { rayGlow, rayCore };
		float[] rayWidths = { 24f, 6f };
		for (int index = 0; index < rays.Length; index++)
		{
			RectTransform rect = rays[index].rectTransform;
			rays[index].raycastTarget = false;
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0f, 0.5f);
			rect.anchoredPosition = start;
			rect.sizeDelta = new Vector2(0f, rayWidths[index]);
			rect.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
		}

		const int runeCount = 20;
		RectTransform[] runeRects = new RectTransform[runeCount];
		Image[] runeImages = new Image[runeCount];
		for (int index = 0; index < runeCount; index++)
		{
			Image rune = CreateImage($"Orbiting Sacred Rune {index + 1}", root, Color.clear);
			rune.sprite = runeSprite;
			rune.preserveAspect = true;
			rune.raycastTarget = false;
			RectTransform rect = rune.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(36f, 36f);
			runeRects[index] = rect;
			runeImages[index] = rune;
		}

		const float duration = 1.2f;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)root != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float time = Mathf.Clamp01(elapsed / duration);
			float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.22f));
			float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 0.76f) / 0.24f));
			float pulse = 0.88f + Mathf.Sin(elapsed * 20f) * 0.18f;

			staffGlow.color = new Color(0.2f, 0.72f, 1f, fade * (0.58f + pulse * 0.2f));
			staffGlow.rectTransform.localScale = Vector3.one * pulse;
			rayGlow.color = new Color(0.08f, 0.55f, 1f, 0.48f * fade);
			rayCore.color = new Color(0.84f, 0.96f, 1f, 0.94f * fade);
			rayGlow.rectTransform.sizeDelta = new Vector2(length * reveal, rayWidths[0]);
			rayCore.rectTransform.sizeDelta = new Vector2(length * reveal, rayWidths[1]);

			for (int index = 0; index < runeCount; index++)
			{
				float phase = index / (float)runeCount;
				float along = Mathf.Repeat(phase + time * 0.42f, 1f) * reveal;
				float spiralAngle = along * Mathf.PI * 8f + time * Mathf.PI * 5f;
				float radius = 25f + Mathf.Sin(phase * Mathf.PI * 2f) * 7f;
				runeRects[index].anchoredPosition = Vector2.Lerp(start, end, along)
					+ perpendicular * Mathf.Sin(spiralAngle) * radius;
				runeRects[index].localRotation = Quaternion.Euler(0f, 0f, -spiralAngle * Mathf.Rad2Deg * 0.35f);
				float depth = 0.72f + (Mathf.Cos(spiralAngle) + 1f) * 0.22f;
				runeRects[index].localScale = Vector3.one * depth;
				float edgeFade = Mathf.Sin(Mathf.Clamp01(along) * Mathf.PI);
				runeImages[index].color = new Color(0.42f, 0.82f, 1f, edgeFade * fade * reveal);
			}
			yield return null;
		}

		if ((Object)(object)rootObject != (Object)null)
			Object.Destroy(rootObject);
	}

	private IEnumerator PlaySeraphelSealApplicationVfx(PrototypeCardView targetView, int sealCount)
	{
		if ((Object)(object)safeAreaRoot == (Object)null || (Object)(object)targetView == (Object)null)
			yield break;

		RectTransform targetRect = ((Component)targetView).transform as RectTransform;
		Sprite sealSprite = Resources.Load<Sprite>("UI/seraphel_judgment_seal");
		if ((Object)(object)targetRect == (Object)null || (Object)(object)sealSprite == (Object)null)
			yield break;

		Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safeAreaRoot, targetRect);
		Vector2 center = new Vector2(targetBounds.center.x, targetBounds.center.y);
		float diameter = Mathf.Clamp(Mathf.Max(targetBounds.size.x, targetBounds.size.y) * 1.42f, 190f, 330f);

		GameObject rootObject = new GameObject("Seraphel Sacred Seal Application", typeof(RectTransform), typeof(CanvasGroup));
		RectTransform root = rootObject.GetComponent<RectTransform>();
		root.SetParent((Transform)(object)safeAreaRoot, false);
		Stretch(root);
		root.SetAsLastSibling();
		CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;

		Image bloom = CreateImage("Judgment Impact Bloom", root, Color.clear);
		bloom.sprite = BuildManaAuraSprite();
		bloom.raycastTarget = false;
		bloom.rectTransform.anchorMin = bloom.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		bloom.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		bloom.rectTransform.anchoredPosition = center;
		bloom.rectTransform.sizeDelta = new Vector2(diameter * 1.35f, diameter * 1.35f);

		const int layerCount = 3;
		Image[] layers = new Image[layerCount];
		for (int index = 0; index < layerCount; index++)
		{
			Image layer = CreateImage($"Judgment Seal Layer {index + 1}", root, Color.clear);
			layer.sprite = sealSprite;
			layer.preserveAspect = true;
			layer.raycastTarget = false;
			RectTransform rect = layer.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = center;
			rect.sizeDelta = new Vector2(diameter, diameter);
			layers[index] = layer;
		}

		const int sparkCount = 14;
		Image[] sparks = new Image[sparkCount];
		for (int index = 0; index < sparkCount; index++)
		{
			Image spark = CreateImage($"Judgment Rune Spark {index + 1}", root, Color.clear);
			spark.sprite = sealSprite;
			spark.preserveAspect = true;
			spark.raycastTarget = false;
			RectTransform rect = spark.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(30f, 30f);
			sparks[index] = spark;
		}

		const float duration = 1.05f;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)root != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float time = Mathf.Clamp01(elapsed / duration);
			float arrival = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.25f));
			float holdFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 0.66f) / 0.34f));
			float impact = Mathf.Exp(-Mathf.Pow((time - 0.25f) * 12f, 2f));
			bloom.color = new Color(0.34f, 0.78f, 1f, (impact * 0.72f + holdFade * 0.12f));
			bloom.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.35f, arrival);

			for (int index = 0; index < layerCount; index++)
			{
				float delay = index * 0.075f;
				float local = Mathf.Clamp01((time - delay) / 0.28f);
				float scale = Mathf.Lerp(1.75f + index * 0.16f, 1f + index * 0.055f, Mathf.SmoothStep(0f, 1f, local));
				layers[index].rectTransform.localScale = Vector3.one * scale;
				layers[index].rectTransform.localRotation = Quaternion.Euler(0f, 0f,
					(index % 2 == 0 ? 1f : -1f) * Mathf.Lerp(95f + index * 35f, 0f, local));
				float alpha = Mathf.Sin(local * Mathf.PI * 0.5f) * holdFade * (index == 0 ? 1f : 0.48f);
				Color tint = index == 0
					? new Color(0.72f, 0.93f, 1f, alpha)
					: new Color(0.35f, 0.72f, 1f, alpha);
				layers[index].color = tint;
			}

			for (int index = 0; index < sparkCount; index++)
			{
				float phase = index / (float)sparkCount;
				float sparkTime = Mathf.Clamp01((time - 0.18f) / 0.72f);
				float angle = phase * Mathf.PI * 2f + time * Mathf.PI * (index % 2 == 0 ? 2.4f : -2.4f);
				float radius = diameter * Mathf.Lerp(0.12f, 0.68f, sparkTime);
				sparks[index].rectTransform.anchoredPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
				sparks[index].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
				sparks[index].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.72f, sparkTime);
				float alpha = Mathf.Sin(sparkTime * Mathf.PI) * holdFade;
				sparks[index].color = Color.Lerp(
					new Color(0.4f, 0.84f, 1f, alpha),
					new Color(1f, 0.88f, 0.42f, alpha),
					(index + Mathf.Max(1, sealCount)) % 3 == 0 ? 0.7f : 0.1f);
			}
			yield return null;
		}

		if ((Object)(object)rootObject != (Object)null)
			Object.Destroy(rootObject);
	}

	private void EnsureSeraphelBossName()
	{
		if ((Object)(object)seraphelBossNameText != (Object)null)
		{
			((Component)seraphelBossNameText).gameObject.SetActive(true);
			return;
		}
		if ((Object)(object)safeAreaRoot == (Object)null)
			return;

		Font lifeCraft = Resources.Load<Font>("Fonts/LifeCraft_Font");
		if ((Object)(object)lifeCraft == (Object)null)
			lifeCraft = AccardND.Battlefield.MmoUiTheme.BodyFont;
		seraphelBossNameText = CreateText(
			"Seraphel Boss Name",
			(Transform)(object)safeAreaRoot,
			lifeCraft,
			30,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		seraphelBossNameText.text = "Seraphel";
		seraphelBossNameText.color = Color.white;
		Outline outline = ((Component)seraphelBossNameText).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		outline.useGraphicAlpha = true;
		((Component)seraphelBossNameText).transform.SetAsLastSibling();
	}

	private void EnsureTrentorBossName()
	{
		if ((Object)(object)trentorBossNameText != (Object)null)
		{
			((Component)trentorBossNameText).gameObject.SetActive(true);
			return;
		}
		if ((Object)(object)safeAreaRoot == (Object)null)
			return;

		Font lifeCraft = Resources.Load<Font>("Fonts/LifeCraft_Font");
		if ((Object)(object)lifeCraft == (Object)null)
			lifeCraft = AccardND.Battlefield.MmoUiTheme.BodyFont;
		trentorBossNameText = CreateText(
			"Trentor Boss Name",
			(Transform)(object)safeAreaRoot,
			lifeCraft,
			30,
			FontStyle.Normal,
			TextAnchor.MiddleCenter);
		trentorBossNameText.text = "Trentor";
		trentorBossNameText.color = Color.white;
		Outline outline = ((Component)trentorBossNameText).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		outline.useGraphicAlpha = true;
		((Component)trentorBossNameText).transform.SetAsLastSibling();
	}

	private void EnsureBossName(ref Text bossNameText, string bossName)
	{
		if ((Object)(object)bossNameText != (Object)null)
		{
			((Component)bossNameText).gameObject.SetActive(true);
			return;
		}
		if ((Object)(object)safeAreaRoot == (Object)null)
			return;
		Font lifeCraft = Resources.Load<Font>("Fonts/LifeCraft_Font");
		if ((Object)(object)lifeCraft == (Object)null)
			lifeCraft = AccardND.Battlefield.MmoUiTheme.BodyFont;
		bossNameText = CreateText($"{bossName} Boss Name", (Transform)(object)safeAreaRoot,
			lifeCraft, 30, FontStyle.Normal, TextAnchor.MiddleCenter);
		bossNameText.text = bossName;
		bossNameText.color = Color.white;
		Outline outline = ((Component)bossNameText).gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(2f, -2f);
		outline.useGraphicAlpha = true;
		((Component)bossNameText).transform.SetAsLastSibling();
	}

	/// <summary>
	/// Il controller sopravvive ai riavvii della Boss Debug: ogni scenario deve
	/// partire senza etichette o HUD temporanei appartenenti al boss precedente.
	/// Le etichette vengono conservate e riutilizzate, ma restano inattive finche'
	/// la relativa presentazione non viene inizializzata di nuovo.
	/// </summary>
	private void ResetBossPresentationUi()
	{
		HideAllBossNames();

		if ((Object)(object)seraphelRevealHealthRoot != (Object)null)
		{
			Object.Destroy(seraphelRevealHealthRoot);
			seraphelRevealHealthRoot = null;
		}
	}

	private void HideAllBossNames()
	{
		SetBossNameActive(seraphelBossNameText, false);
		SetBossNameActive(trentorBossNameText, false);
		SetBossNameActive(bragusBossNameText, false);
		SetBossNameActive(jurinashorBossNameText, false);
		SetBossNameActive(palatirBossNameText, false);
		SetBossNameActive(medusaBossNameText, false);
		SetBossNameActive(composableGolemBossNameText, false);
	}

	private static void SetBossNameActive(Text bossNameText, bool active)
	{
		if ((Object)(object)bossNameText != (Object)null)
			((Component)bossNameText).gameObject.SetActive(active);
	}

	private void ApplySeraphelExclusiveLayout()
	{
		bool composableGolemPresentationActive = activeComposableGolem != null
			&& !activeComposableGolem.IsDefeated;
		bool medusaBossPresentationActive = activeMedusaBoss != null
			&& !activeMedusaBoss.IsDefeated;
		bool palatirBossPresentationActive = activePalatirBoss != null && !activePalatirBoss.IsDefeated;
		if ((!seraphelBossPresentationActive && !trentorBossPresentationActive && !bragusBossPresentationActive && !jurinashorBossPresentationActive
			&& !palatirBossPresentationActive && !composableGolemPresentationActive
			&& !medusaBossPresentationActive)
			|| (Object)(object)safeAreaRoot == (Object)null)
			return;

		// Il layout HUD generale riposiziona runa e dado nelle colonne centrali.
		// Applicalo prima, poi sovrascrivilo con la disposizione verticale dei boss.
		ApplyCombatHudRefactorLayout();

		if (seraphelBossPresentationActive && (Object)(object)seraphelBossNameText != (Object)null)
			SetRect(seraphelBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));
		if (trentorBossPresentationActive && (Object)(object)trentorBossNameText != (Object)null)
			SetRect(trentorBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));
		if (bragusBossPresentationActive && (Object)(object)bragusBossNameText != (Object)null)
			SetRect(bragusBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));
		if (jurinashorBossPresentationActive && (Object)(object)jurinashorBossNameText != (Object)null)
			SetRect(jurinashorBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));
		if (palatirBossPresentationActive && (Object)(object)palatirBossNameText != (Object)null)
			SetRect(palatirBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));
		if (medusaBossPresentationActive && (Object)(object)medusaBossNameText != (Object)null)
			SetRect(medusaBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));
		if (composableGolemPresentationActive && (Object)(object)composableGolemBossNameText != (Object)null)
			SetRect(composableGolemBossNameText.rectTransform, new Vector2(0.18f, 0.968f), new Vector2(0.82f, 0.997f));

		if ((Object)(object)enemyManaRuneImage != (Object)null)
		{
			RectTransform enemyRuneRect = enemyManaRuneImage.rectTransform;
			enemyRuneRect.anchorMin = enemyRuneRect.anchorMax = new Vector2(0f, 1f);
			enemyRuneRect.pivot = new Vector2(0f, 1f);
			enemyRuneRect.anchoredPosition = new Vector2(22f, -102f);
			enemyRuneRect.sizeDelta = new Vector2(83f, 83f);
			enemyRuneRect.localScale = new Vector3(2f, 2f, 2f);
		}

		if (cpuHud != null)
		{
			if ((Object)(object)cpuHud.DiceImage != (Object)null)
			{
				RectTransform diceRect = cpuHud.DiceImage.rectTransform;
				Transform diceParent = (Object)(object)enemyManaRuneImage != (Object)null
					? ((Component)enemyManaRuneImage).transform
					: (Transform)(object)safeAreaRoot;
				if (diceRect.parent != diceParent)
					diceRect.SetParent(diceParent, false);
				// Essendo figlio della runa, il dado resta sotto di essa fin dal primo
				// passaggio di layout e non dipende più dalle coordinate della Safe Area.
				diceRect.anchorMin = diceRect.anchorMax = new Vector2(0f, 0f);
				diceRect.pivot = new Vector2(0f, 1f);
				diceRect.sizeDelta = new Vector2(83f, 83f);
				// Azzera sempre la rotazione ereditata dal vecchio CPU HUD.
				diceRect.localRotation = Quaternion.identity;
				diceRect.anchoredPosition3D = new Vector3(10.5f, -8f, 0f);
				diceRect.localScale = new Vector3(0.75f, 0.75f, 1f);
				int enemyDieSides = composableGolemPresentationActive
					? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsComposableGolemProxy), activeComposableGolem.ActiveForm.VigorDieSides)
					: medusaBossPresentationActive
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsMedusaBossProxy), MedusaBoss.DefaultVigorDieSides)
					: trentorBossPresentationActive && activeTrentorBoss != null
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsTrentorBossProxy), TrentorBoss.DefaultVigorDieSides)
					: bragusBossPresentationActive && activeBragusBoss != null
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsBragusBossProxy), BragusBoss.DefaultVigorDieSides)
					: palatirBossPresentationActive
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsPalatirBossProxy), PalatirBoss.DefaultVigorDieSides)
					: activeSeraphelBoss != null
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsSeraphelBossProxy), activeSeraphelBoss.VigorDieSides)
						: SeraphelBoss.PhaseOneVigorDieSides;
				cpuHud.DiceImage.sprite = LoadHudDiceSprite(enemyDieSides, isPlayer: false);
				cpuHud.DiceImage.preserveAspect = true;
				cpuHud.DiceImage.enabled = (Object)(object)cpuHud.DiceImage.sprite != (Object)null;
				((Component)cpuHud.DiceImage).gameObject.SetActive(true);
			}
			if ((Object)(object)cpuHud.DiceText != (Object)null)
			{
				RectTransform diceTextRect = cpuHud.DiceText.rectTransform;
				Transform diceParent = (Object)(object)cpuHud.DiceImage != (Object)null
					? ((Component)cpuHud.DiceImage).transform
					: (Transform)(object)safeAreaRoot;
				if (diceTextRect.parent != diceParent)
					diceTextRect.SetParent(diceParent, false);
				// Stessa impostazione di Combat Vigor Value.
				Stretch(diceTextRect, 3f);
				diceTextRect.localScale = Vector3.one;
				diceTextRect.SetAsLastSibling();
				cpuHud.DiceText.alignment = TextAnchor.MiddleCenter;
				cpuHud.DiceText.fontSize = IsBossFightHudActive() ?35 : 40;
				cpuHud.DiceText.resizeTextForBestFit = false;
				cpuHud.DiceText.fontStyle = FontStyle.Bold;
				cpuHud.DiceText.color = Color.white;
				cpuHud.DiceText.raycastTarget = false;
				int displayedEnemyDie = composableGolemPresentationActive
					? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsComposableGolemProxy), activeComposableGolem.ActiveForm.VigorDieSides)
					: medusaBossPresentationActive
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsMedusaBossProxy), MedusaBoss.DefaultVigorDieSides)
					: trentorBossPresentationActive && activeTrentorBoss != null
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsTrentorBossProxy), TrentorBoss.DefaultVigorDieSides)
					: bragusBossPresentationActive && activeBragusBoss != null
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsBragusBossProxy), BragusBoss.DefaultVigorDieSides)
					: palatirBossPresentationActive
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsPalatirBossProxy), PalatirBoss.DefaultVigorDieSides)
					: activeSeraphelBoss != null
						? EffectiveVigorDieSides(cpuCards?.FirstOrDefault(IsSeraphelBossProxy), activeSeraphelBoss.VigorDieSides)
						: SeraphelBoss.PhaseOneVigorDieSides;
				cpuHud.DiceText.text = $"D{displayedEnemyDie}";
				Outline diceValueOutline = ((Component)cpuHud.DiceText).GetComponent<Outline>();
				if ((Object)(object)diceValueOutline == (Object)null)
					diceValueOutline = ((Component)cpuHud.DiceText).gameObject.AddComponent<Outline>();
				diceValueOutline.effectColor = new Color(0.02f, 0.02f, 0.05f, 0.95f);
				diceValueOutline.effectDistance = new Vector2(1.5f, -1.5f);
				diceValueOutline.useGraphicAlpha = true;
				((Component)cpuHud.DiceText).gameObject.SetActive(true);
			}
			if ((Object)(object)cpuHud.Rect != (Object)null)
				((Component)cpuHud.Rect).gameObject.SetActive(false);
		}

		if ((Object)(object)logButton != (Object)null)
		{
			RectTransform optionsRect = (RectTransform)((Component)logButton).transform;
			optionsRect.anchorMin = new Vector2(0.81f, 0.917f);
			optionsRect.anchorMax = new Vector2(0.982f, 0.995f);
			optionsRect.offsetMin = new Vector2(0f, -106f);
			optionsRect.offsetMax = new Vector2(0f, -106f);
		}

		if ((Object)(object)implementationArchiveButton != (Object)null)
		{
			RectTransform archiveRect = (RectTransform)((Component)implementationArchiveButton).transform;
			archiveRect.anchorMin = new Vector2(0.81f, 0.885f);
			archiveRect.anchorMax = new Vector2(0.982f, 0.965f);
			archiveRect.offsetMin = new Vector2(0f, -186f);
			archiveRect.offsetMax = new Vector2(0f, -186f);
		}
	}

	private static bool IsComposableGolemDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, ComposableGolemCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsMedusaBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, MedusaBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsTrentorBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, TrentorBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsBragusBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, BragusBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsJurinashorBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, JurinashorBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSeraphelBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& (string.Equals(definition.Id, SeraphelBossCardId, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(definition.Id, SeraphelPhaseTwoCardId, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsPalatirBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private void RefreshBragusBossPawn(BattleCardState bragus)
	{
		if (bragus == null || (Object)(object)bragus.View == (Object)null || activeBragusBoss == null)
		{
			return;
		}
		bragus.View.SetStrengthValue(BragusBoss.CardStrength);
		UpdateBragusBossHealthBar(bragus);
		RefreshPersistentStatus(bragus);
	}

	private void RefreshPalatirBossPawn(BattleCardState palatir)
	{
		if (palatir == null || (Object)(object)palatir.View == (Object)null || activePalatirBoss == null)
		{
			return;
		}
		palatir.View.SetStrengthValue(PalatirBoss.CardStrength);
		palatir.View.SetPalatirShields(activePalatirBoss.ActiveShields);
		UpdatePalatirBossHealthBar(palatir);
		RefreshPersistentStatus(palatir);
	}

	private void UpdatePalatirBossHealthBar(BattleCardState palatir)
	{
		if (palatir == null || (Object)(object)palatir.View == (Object)null || activePalatirBoss == null)
		{
			return;
		}

		palatir.View.SetClassHealthBar(activePalatirBoss.HitPoints, activePalatirBoss.MaxHitPoints);
	}

	private void UpdateBragusBossHealthBar(BattleCardState bragus)
	{
		if (bragus == null || (Object)(object)bragus.View == (Object)null || activeBragusBoss == null)
		{
			return;
		}

		bragus.View.SetClassHealthBar(activeBragusBoss.HitPoints, activeBragusBoss.MaxHitPoints);
	}

	private void RefreshTrentorBossPawn(BattleCardState trentor)
	{
		if (trentor == null || (Object)(object)trentor.View == (Object)null || activeTrentorBoss == null)
		{
			return;
		}
		trentor.View.SetStrengthValue(TrentorBoss.CardStrength);
		UpdateTrentorBossHealthBar(trentor);
		RefreshPersistentStatus(trentor);
	}

	private void UpdateTrentorBossHealthBar(BattleCardState trentor)
	{
		if (trentor == null || (Object)(object)trentor.View == (Object)null || activeTrentorBoss == null)
		{
			return;
		}

		trentor.View.SetClassHealthBar(activeTrentorBoss.HitPoints, activeTrentorBoss.MaxHitPoints);
	}

	private void RefreshMedusaBossPawn(BattleCardState medusa)
	{
		if (medusa == null || (Object)(object)medusa.View == (Object)null || activeMedusaBoss == null)
		{
			return;
		}
		medusa.View.SetStrengthValue(MedusaBoss.CardStrength);
		medusa.View.SetHealthBarName("Medusa");
		UpdateMedusaBossHealthBar(medusa);
		RefreshPersistentStatus(medusa);
	}

	private void UpdateMedusaBossHealthBar(BattleCardState medusa)
	{
		if (medusa == null || (Object)(object)medusa.View == (Object)null || activeMedusaBoss == null)
		{
			return;
		}

		medusa.View.SetClassHealthBar(activeMedusaBoss.HitPoints, activeMedusaBoss.MaxHitPoints);
	}

	// La pedina del Golem usa la grafica standard delle carte: barra HP Warrior
	// e badge di stato colorato in base alla forma attiva.
	private void RefreshComposableGolemPawn(BattleCardState golem)
	{
		if (golem == null || (Object)(object)golem.View == (Object)null || activeComposableGolem == null)
		{
			return;
		}
		ComposableGolemForm activeForm = activeComposableGolem.ActiveForm.Form;
		golem.View.SetComposableGolemForm(
			activeForm,
			actionColor: GolemFormColor(activeForm));
		golem.View.SetStrengthValue(activeComposableGolem.ActiveForm.Power);
		golem.View.SetHealthBarName("StorgBot");
		UpdateComposableGolemHealthBar(golem);
		RefreshPersistentStatus(golem);
	}

	private void UpdateComposableGolemHealthBar(BattleCardState golem)
	{
		if (golem == null || (Object)(object)golem.View == (Object)null || activeComposableGolem == null)
		{
			return;
		}

		golem.View.SetClassHealthBar(activeComposableGolem.HitPoints, activeComposableGolem.MaxHitPoints);
	}

	private void MakeDeploymentPreviewInspectable(PrototypeCardView preview, CardDefinition definition)
	{
		if ((Object)(object)preview == (Object)null || (Object)(object)definition == (Object)null)
		{
			return;
		}
		preview.SetInteractable(interactable: true);
		((UnityEvent)preview.Button.onClick).AddListener((UnityAction)delegate
		{
			if (CanInspectDeploymentPreview())
			{
				ShowCardInspection(definition);
			}
		});
	}

	private bool CanInspectDeploymentPreview()
	{
		if (draftActive && deploymentDraftActive && (Object)(object)cardInspectionPanel != (Object)null)
		{
			return !cardInspectionPanel.activeSelf;
		}
		return false;
	}

	private void ShowPendingDeploymentInspection()
	{
		if (!draftActive
			|| !deploymentDraftActive
			|| pendingDeploymentIndex < 0
			|| pendingDeploymentIndex >= draftCandidates.Count)
		{
			return;
		}

		ShowCardInspection(draftCandidates[pendingDeploymentIndex]);
	}

	private void HandlePlayerCardClick(BattleCardState state)
	{
		LogInspectionState("CLICK_PLAYER", state);
		if (CanUsePlayerCardAction(state))
		{
			LogInspectionState("CLICK_PLAYER_ROUTED_TO_TARGET", state);
			SelectPlayerAbilityTarget(state);
		}
		else if (CanInspectBattleCard(state))
		{
			LogInspectionState("CLICK_PLAYER_OPEN_INSPECTION", state);
			ShowCardInspection(state);
		}
		else
		{
			LogInspectionState("CLICK_PLAYER_REJECTED", state);
		}
	}

	private void RefreshJurinashorBossPawn(BattleCardState jurinashor)
	{
		if (jurinashor == null || jurinashor.View == null || activeJurinashorBoss == null)
			return;
		jurinashor.View.SetStrengthValue(JurinashorBoss.CardStrength + JurinashorSwordPowerBonus(jurinashor));
		jurinashor.View.SetJurinashorPhaseTwoContour(activeJurinashorBoss.IsPhaseTwo);
		UpdateJurinashorBossHealthBar(jurinashor);
	}

	private void UpdateJurinashorBossHealthBar(BattleCardState jurinashor)
	{
		if (jurinashor?.View == null || activeJurinashorBoss == null)
			return;
		jurinashor.View.SetClassHealthBar(activeJurinashorBoss.HitPoints, activeJurinashorBoss.MaxHitPoints);
	}

	private void RefreshSeraphelBossPawn(BattleCardState boss)
	{
		if (boss == null || boss.View == null || activeSeraphelBoss == null) return;
		boss.View.SetStrengthValue(activeSeraphelBoss.Strength);
		boss.View.SetSeraphelPhaseTwoContour(activeSeraphelBoss.IsPhaseTwo);
		boss.View.SetClassHealthBar(activeSeraphelBoss.HitPoints, activeSeraphelBoss.MaxHitPoints);
	}

	private void HandleCpuCardClick(int index)
	{
		BattleCardState state = ((index >= 0 && index < cpuCards.Count) ?cpuCards[index] : null);
		if (!TutorialWarriorDuelAllowsEnemyTarget(state))
			return;
		if (adventureScriptedTutorialActive && adventureScriptedTutorialStep == 5
			&& attackTargetingActive && !IsAdventureTutorialStrengthFourTarget(state))
			return;
		LogInspectionState("CLICK_CPU", state);
		if (CanUseCpuCardAction(index))
		{
			LogInspectionState("CLICK_CPU_ROUTED_TO_TARGET", state);
			SelectCpuTarget(index);
		}
		else if (CanInspectBattleCard(state))
		{
			LogInspectionState("CLICK_CPU_OPEN_INSPECTION", state);
			ShowCardInspection(state);
		}
		else
		{
			LogInspectionState("CLICK_CPU_REJECTED", state);
		}
	}

	/// <summary>
	/// Diagnostica temporanea per il blocco delle inspection dopo una suprema istantanea.
	/// E' intenzionalmente event-driven: stampa solo su suprema/click, non a ogni frame.
	/// </summary>
	private void LogInspectionState(string context, BattleCardState state = null)
	{
		string cardName = state?.Card != null ? state.Card.Name : "<null>";
		string pendingName = pendingAbilityUser?.Card != null ? pendingAbilityUser.Card.Name : "<null>";
		string abilityName = activeAbilityUser?.Card != null ? activeAbilityUser.Card.Name : "<null>";
		string attachmentName = activeAttachmentSource?.Card != null ? activeAttachmentSource.Card.Name : "<null>";
		bool inspectionPanelActive = (Object)(object)cardInspectionPanel != (Object)null
			&& cardInspectionPanel.activeSelf;

		Debug.LogWarning(
			$"[INSPECTION-STATE] {context} frame={Time.frameCount} card='{cardName}' "
			+ $"inputLocked={inputLocked} gameFinished={gameFinished} draftActive={draftActive} "
			+ $"deploymentDraftActive={deploymentDraftActive} attackTargeting={attackTargetingActive} "
			+ $"abilityTargetMode={abilityTargetMode} activeAbility='{abilityName}' "
			+ $"pendingAbility='{pendingName}' attachment='{attachmentName}' "
			+ $"pendingDeploymentIndex={pendingDeploymentIndex} selectedPlayerIndex={selectedPlayerIndex} "
			+ $"inspectionPanelActive={inspectionPanelActive} suppressUntilFrame={suppressCardInspectionUntilFrame} "
			+ $"pvpActive={pvpPresentationActive} pvpTargetMode={pvpTargetMode}");
	}

	private bool CanInspectBattleCard(BattleCardState state)
	{
		// Le spade evocate sono bersagli di combattimento, non carte ispezionabili.
		if (IsJurinashorSword(state))
		{
			return false;
		}
		if (Time.frameCount <= suppressCardInspectionUntilFrame)
		{
			return false;
		}
		if (state != null && !draftActive && !deploymentDraftActive && !attackTargetingActive && pendingAbilityUser == null && pendingDeploymentIndex < 0 && abilityTargetMode == AbilityTargetMode.None && (Object)(object)cardInspectionPanel != (Object)null)
		{
			return !cardInspectionPanel.activeSelf;
		}
		return false;
	}

	private bool CanUsePlayerCardAction(BattleCardState card)
	{
		if (card != null && !inputLocked && !gameFinished)
		{
			if ((abilityTargetMode != AbilityTargetMode.NecromancerAlly || !CanReviveWithNecromancer(card)) && (abilityTargetMode != AbilityTargetMode.PaladinAlly || card.Eliminated || activeAbilityUser == null) && (abilityTargetMode != AbilityTargetMode.PriestAlly || card.Eliminated))
			{
				if (abilityTargetMode == AbilityTargetMode.AttachmentAlly)
				{
					return CanTargetAttachment(activeAttachmentSource, card);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private bool CanUseCpuCardAction(int index)
	{
		if (IsAdventureTutorialWaitingForAdvantageContinue())
		{
			return false;
		}
		if (index >= 0 && index < cpuCards.Count && !inputLocked && !gameFinished && pendingAbilityUser == null && pendingDeploymentIndex < 0 && selectedPlayerIndex >= 0 && !cpuCards[index].Eliminated && turnOrder.Count > 0 && turnOrder[currentTurnIndex].BelongsToPlayer)
		{
			if (adventureScriptedTutorialActive && adventureScriptedTutorialStep == 5
				&& !IsAdventureTutorialStrengthFourTarget(cpuCards[index]))
				return false;
			if (!attackTargetingActive && abilityTargetMode != AbilityTargetMode.AssassinEnemy && abilityTargetMode != AbilityTargetMode.MageEnemy && abilityTargetMode != AbilityTargetMode.RogueSupremeEnemy)
			{
				return abilityTargetMode == AbilityTargetMode.HunterEnemy && !IsHunterMarked(cpuCards[index]);
			}
			return true;
		}
		return false;
	}

	private void SelectCpuTarget(int index)
	{
		BattleCardState tutorialTarget = index >= 0 && index < cpuCards.Count ? cpuCards[index] : null;
		if (!TutorialWarriorDuelAllowsEnemyTarget(tutorialTarget))
			return;
		if (IsAdventureTutorialWaitingForAdvantageContinue())
		{
			return;
		}
		if (inputLocked || gameFinished || selectedPlayerIndex < 0 || index >= cpuCards.Count || cpuCards[index].Eliminated)
		{
			return;
		}
		if (abilityTargetMode == AbilityTargetMode.RogueSupremeEnemy && activeAbilityUser != null)
		{
			ResolveCampaignRogueSupreme(activeAbilityUser, cpuCards[index]);
			abilityTargetMode = AbilityTargetMode.None;
			activeAbilityUser = null;
			ClearTargetHints();
			RefreshCardActionOverlays();
			UpdateInteractions();
			return;
		}
		if (abilityTargetMode == AbilityTargetMode.AssassinEnemy && activeAbilityUser != null)
		{
			BattleCardState battleCardState = cpuCards[index];
			if ((IsSeraphelBossProxy(battleCardState) && activeSeraphelBoss.IsImmuneToDebuffs)
				|| IsJurinashorImmuneToDebuffs(battleCardState))
			{
				battleCardState.View?.PlayActionCallout("IMMUNITÀ", Color.white);
				SetMessage($"IMMUNITÀ: {battleCardState.Card.Name} è immune all'inibizione.");
				RestoreActionsAfterImmuneAbilityTarget();
				return;
			}
			battleCardState.InhibitedTurns = Math.Max(battleCardState.InhibitedTurns, 1);
			battleCardState.WasInhibited = true;
			if (playerAura == BattleAuraType.Assassin)
			{
				ReducePower(battleCardState, 1);
			}
			activeAbilityUser.AbilityArmed = false;
			MarkAbilityUsed(activeAbilityUser);
			RefreshPersistentStatus(battleCardState);
			PlayClassAbilitySfx(HeroClass.Assassin);
			if ((Object)(object)battleAnimationPlayer != (Object)null
				&& (Object)(object)activeAbilityUser.View != (Object)null
				&& (Object)(object)battleCardState.View != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, battleCardState.View, AbilityTargetLineColor));
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayAssassinSmokeBomb(
					activeAbilityUser.View,
					battleCardState.View,
					() => !gameFinished && battleCardState.InhibitedTurns > 0));
			}
			string text = ((playerAura == BattleAuraType.Assassin) ?" e gli infligge -1 permanente" : string.Empty);
			SetMessage("ASSASSINO: " + activeAbilityUser.Card.Name + " inibisce " + battleCardState.Card.Name + text + ". Saltera il prossimo turno.");
			TriggerMagicAuraAfterAbility();
			abilityTargetMode = AbilityTargetMode.None;
			ClearTargetHints();
			RefreshAbilityButton(activeAbilityUser);
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			UpdateInteractions();
		}
		else if (abilityTargetMode == AbilityTargetMode.MageEnemy && activeAbilityUser != null)
		{
			BattleCardState battleCardState2 = cpuCards[index];
			if ((IsSeraphelBossProxy(battleCardState2) && activeSeraphelBoss.IsImmuneToDebuffs)
				|| IsJurinashorImmuneToDebuffs(battleCardState2))
			{
				battleCardState2.View?.PlayActionCallout("IMMUNITÀ", Color.white);
				SetMessage($"IMMUNITÀ: {battleCardState2.Card.Name} è immune al malus Vigore.");
				RestoreActionsAfterImmuneAbilityTarget();
				return;
			}
			int num = 1;
			int baseDieSides = IsComposableGolemProxy(battleCardState2) && activeComposableGolem != null
				? activeComposableGolem.ActiveForm.VigorDieSides
				: IsSeraphelBossProxy(battleCardState2) && activeSeraphelBoss != null
					? activeSeraphelBoss.VigorDieSides
				: tutorialMageDuelActive
					? 6
					: runProgress != null ? runProgress.MasterVigorDieSides : configuration.Gameplay.VigorDieSides;
			int startDieSides = EffectiveVigorDieSides(battleCardState2, baseDieSides);
			battleCardState2.PendingVigorStepPenalty = StackMageVigorPenalty(
				battleCardState2.PendingVigorStepPenalty,
				baseDieSides);
			int endDieSides = EffectiveVigorDieSides(battleCardState2, baseDieSides);
			activeAbilityUser.AbilityArmed = false;
			MarkAbilityUsed(activeAbilityUser);
			RefreshPersistentStatus(battleCardState2);
			PlayClassAbilitySfx(HeroClass.Mage);
			if ((Object)(object)battleAnimationPlayer != (Object)null
				&& (Object)(object)activeAbilityUser.View != (Object)null
				&& (Object)(object)battleCardState2.View != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, battleCardState2.View, AbilityTargetLineColor));
			}
			((MonoBehaviour)this).StartCoroutine(PlayMageVigorConstellation(
				battleCardState2,
				startDieSides,
				endDieSides));
			SetMessage($"Grazie all'abilita del mago, il prossimo dado Vigore di {battleCardState2.Card.Name} scende di {num} step: usera un D{endDieSides}.");
			TriggerMagicAuraAfterAbility();
			abilityTargetMode = AbilityTargetMode.None;
			ClearTargetHints();
			RefreshAbilityButton(activeAbilityUser);
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			UpdateInteractions();
			// La selezione del bersaglio e' parte della lezione pratica del Mago: il click
			// valido deve far avanzare il copione dall'abilita' all'attacco successivo.
			if (tutorialMageDuelActive)
				NotifyAdventureTutorial(AdventureTutorialAction.EnemyTargeted);
		}
		else if (abilityTargetMode == AbilityTargetMode.HunterEnemy && activeAbilityUser != null)
		{
			BattleCardState battleCardState3 = cpuCards[index];
			if ((IsSeraphelBossProxy(battleCardState3) && activeSeraphelBoss.IsImmuneToDebuffs)
				|| IsJurinashorImmuneToDebuffs(battleCardState3))
			{
				battleCardState3.View?.PlayActionCallout("IMMUNITÀ", Color.white);
				SetMessage($"IMMUNITÀ: {battleCardState3.Card.Name} è immune al Marchio del Cacciatore.");
				RestoreActionsAfterImmuneAbilityTarget();
				return;
			}
			if (IsHunterMarked(battleCardState3))
			{
				SetMessage($"CACCIATORE: {battleCardState3.Card.Name} e gia marcato. Scegli un altro bersaglio.");
				UpdateInteractions();
				return;
			}
			if (activeAbilityUser.MarkedTarget != null && !activeAbilityUser.MarkedTarget.Eliminated)
			{
				RefreshPersistentStatus(activeAbilityUser.MarkedTarget);
			}
			activeAbilityUser.MarkedTarget = battleCardState3;
			activeAbilityUser.AbilityArmed = false;
			MarkAbilityUsed(activeAbilityUser);
			RefreshPersistentStatus(battleCardState3);
			PlayClassAbilitySfx(HeroClass.Hunter);
			if ((Object)(object)battleAnimationPlayer != (Object)null
				&& (Object)(object)activeAbilityUser.View != (Object)null
				&& (Object)(object)battleCardState3.View != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, battleCardState3.View, AbilityTargetLineColor));
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayHunterMarkReticle(battleCardState3.View));
			}
			SetMessage($"CACCIATORE: {activeAbilityUser.Card.Name} marca {battleCardState3.Card.Name}. Bersaglio marcato: chi lo attacca prende +{HunterMarkValueFor(activeAbilityUser)}.");
			TriggerMagicAuraAfterAbility();
			abilityTargetMode = AbilityTargetMode.None;
			ClearTargetHints();
			RefreshAbilityButton(activeAbilityUser);
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			UpdateInteractions();
		}
		else if (attackTargetingActive)
		{
			if (IsShieldedByInvisibility(cpuCards[index]))
			{
				SetMessage("Quel bersaglio non e raggiungibile: e invisibile.");
				UpdateInteractions();
				return;
			}
			attackTargetingActive = false;
			BattleCardState battleCardState4 = turnOrder[currentTurnIndex];
			MatchupResult matchupResult = IsPalatirBossProxy(cpuCards[index])
				? MatchupResult.Neutral
				: ClassMatchup.Compare(battleCardState4.Card.HeroClass, cpuCards[index].Card.HeroClass);
			AppendLog("BERSAGLIO SCELTO - " + battleCardState4.Card.Name + " attacca " + cpuCards[index].Card.Name + " " + $"({matchupResult})");
			NotifyAdventureTutorial(AdventureTutorialAction.EnemyTargeted);
			((MonoBehaviour)this).StartCoroutine(ExecutePlayerTurn(index));
		}
	}

	private void RestoreActionsAfterImmuneAbilityTarget()
	{
		BattleCardState abilityUser = activeAbilityUser;
		if (abilityUser != null)
		{
			abilityUser.AbilityArmed = false;
			abilityUser.AbilityUsedThisTurn = false;
			RefreshPersistentStatus(abilityUser);
		}

		abilityTargetMode = AbilityTargetMode.None;
		activeAbilityUser = null;
		ClearTargetHints();
		((Component)cancelActionButton).gameObject.SetActive(false);
		RefreshCardActionOverlays();
		UpdateInteractions();
	}

	private void SelectPlayerAbilityTarget(BattleCardState target)
	{
		if (abilityTargetMode == AbilityTargetMode.PaladinAlly
			&& Time.frameCount <= suppressPaladinTargetSelectionUntilFrame)
		{
			return;
		}

		if (inputLocked || target == null)
		{
			return;
		}
		if (abilityTargetMode == AbilityTargetMode.AttachmentAlly && activeAttachmentSource != null)
		{
			if (CanTargetAttachment(activeAttachmentSource, target))
			{
				((MonoBehaviour)this).StartCoroutine(ExecuteAttachment(activeAttachmentSource, target));
			}
		}
		else
		{
			if (activeAbilityUser == null)
			{
				return;
			}
			if (abilityTargetMode == AbilityTargetMode.NecromancerAlly && CanReviveWithNecromancer(target))
			{
				target.Eliminated = false;
				target.RevivedRound = 0;
				MoveTurnAfter(activeAbilityUser, target);
				target.View.ResetState();
				target.View.SetInitiative(target.Initiative);
				RefreshPersistentStatus(target);
				ApplyPlayerAuraVisuals(appendLog: false);
				MarkAbilityUsed(activeAbilityUser);
				PlayClassAbilitySfx(HeroClass.Necromancer);
				if ((Object)(object)battleAnimationPlayer != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, target.View, AbilityTargetLineColor));
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayNecromancerReviveSkullConvergence(target.View));
				}
				SetMessage("NECROMANTE: " + activeAbilityUser.Card.Name + " rialza " + target.Card.Name + ". Agira subito dopo il Necromante.");
				TriggerMagicAuraAfterAbility();
			}
			else if (abilityTargetMode == AbilityTargetMode.PaladinAlly && !target.Eliminated)
			{
				if (!TrySpendCampaignPrimaryMana(activeAbilityUser))
					return;
				activeAbilityUser.AbilityArmed = true;
				activeAbilityUser.ProtectedAlly = target;
				RefreshPersistentStatus(activeAbilityUser);
				PlayClassAbilitySfx(HeroClass.Paladin);
				if ((Object)(object)battleAnimationPlayer != (Object)null
					&& (Object)(object)activeAbilityUser.View != (Object)null
					&& (Object)(object)target.View != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, target.View, AbilityTargetLineColor));
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayPaladinProtectionConstellation(target.View));
				}
				string text = ((target == activeAbilityUser) ?"si proteggera: vantaggio al prossimo tiro difesa." : ("proteggera " + target.Card.Name + ": se viene attaccato deviera il colpo su di se e difendera con vantaggio."));
				SetMessage("PALADINO: " + activeAbilityUser.Card.Name + " " + text);
			}
			else
			{
				if (abilityTargetMode != AbilityTargetMode.PriestAlly || target.Eliminated)
				{
					return;
				}
				int num = ((playerAura == BattleAuraType.Priest) ?(configuration.ClassBalance.PriestBlessingBonus + 1) : configuration.ClassBalance.PriestBlessingBonus);
				target.PendingAttackBonus += num;
				if (target.PendingAttackBonusKind != PendingAttackBonusKind.Fury)
				{
					target.PendingAttackBonusKind = PendingAttackBonusKind.Blessing;
				}
				RefreshPersistentStatus(target);
				MarkAbilityUsed(activeAbilityUser);
				PlayClassAbilitySfx(HeroClass.Priest);
				if ((Object)(object)battleAnimationPlayer != (Object)null
					&& (Object)(object)activeAbilityUser.View != (Object)null
					&& (Object)(object)target.View != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, target.View, AbilityTargetLineColor));
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayPriestBlessing(activeAbilityUser.View, target.View, num));
				}
				SetMessage($"SACERDOTE: {activeAbilityUser.Card.Name} benedice {target.Card.Name} con +{num}.");
				TriggerMagicAuraAfterAbility();
			}
			BattleCardState card = activeAbilityUser;
			abilityTargetMode = AbilityTargetMode.None;
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			RefreshAbilityButton(card);
			RefreshInitiativeDisplay();
			UpdateInteractions();
		}
	}

	private void MoveTurnAfter(BattleCardState actor, BattleCardState target)
	{
		if (actor == null || target == null || turnOrder.Count == 0)
		{
			return;
		}
		int num = turnOrder.IndexOf(actor);
		if (num < 0)
		{
			num = Mathf.Clamp(currentTurnIndex, 0, turnOrder.Count - 1);
		}
		int num2 = turnOrder.IndexOf(target);
		if (num2 >= 0)
		{
			turnOrder.RemoveAt(num2);
			if (num2 < num)
			{
				num--;
			}
		}
		int index = Mathf.Clamp(num + 1, 0, turnOrder.Count);
		turnOrder.Insert(index, target);
		currentTurnIndex = Mathf.Clamp(num, 0, turnOrder.Count - 1);
	}
}
}
