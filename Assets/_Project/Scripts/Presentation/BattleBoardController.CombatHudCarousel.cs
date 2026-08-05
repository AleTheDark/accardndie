using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private RectTransform combatExperienceRoot;
	private ArcaneExperienceFillGraphic combatExperienceFill;
	private Coroutine combatExperienceFillRoutine;
	private Text combatExperienceText;
	private Image combatVigorImage;
	private Text combatVigorText;
	private BattleCardState carouselCenterCard;
	private Coroutine carouselRotationRoutine;

	private void CreateCombatHudRefactor(Font font)
	{
		Image expRoot = CreateImage("Combat Experience Bar", (Transform)(object)safeAreaRoot,
			new Color(0.012f, 0.025f, 0.045f, 0.96f));
		StylePanel(expRoot);
		combatExperienceRoot = expRoot.rectTransform;

		Image maskImage = CreateImage("Combat Experience Mask", expRoot.transform, Color.white);
		Stretch(maskImage.rectTransform, 4f);
		maskImage.raycastTarget = false;
		Mask mask = maskImage.gameObject.AddComponent<Mask>();
		mask.showMaskGraphic = false;
		GameObject fillObject = new GameObject("Combat Experience Fill", typeof(RectTransform),
			typeof(CanvasRenderer), typeof(ArcaneExperienceFillGraphic));
		fillObject.transform.SetParent(maskImage.transform, false);
		combatExperienceFill = fillObject.GetComponent<ArcaneExperienceFillGraphic>();
		SetRect(combatExperienceFill.rectTransform, Vector2.zero, new Vector2(0f, 1f));
		combatExperienceFill.raycastTarget = false;

		combatExperienceText = CreateText("Combat Experience Value", expRoot.transform, font, 18,
			FontStyle.Bold, TextAnchor.MiddleCenter);
		combatExperienceText.color = Color.white;
		combatExperienceText.raycastTarget = false;
		Stretch(combatExperienceText.rectTransform, 2f);
		Outline expOutline = combatExperienceText.gameObject.AddComponent<Outline>();
		expOutline.effectColor = new Color(0.01f, 0.02f, 0.06f, 0.95f);
		expOutline.effectDistance = new Vector2(1.4f, -1.4f);

		combatVigorImage = CreateImage("Combat Vigor Die", (Transform)(object)safeAreaRoot, Color.white);
		combatVigorImage.preserveAspect = true;
		combatVigorImage.raycastTarget = false;
		combatVigorText = CreateText("Combat Vigor Value", combatVigorImage.transform, font, 20,
			FontStyle.Bold, TextAnchor.MiddleCenter);
		combatVigorText.color = Color.white;
		combatVigorText.raycastTarget = false;
		Stretch(combatVigorText.rectTransform, 3f);
		Outline vigorOutline = combatVigorText.gameObject.AddComponent<Outline>();
		vigorOutline.effectColor = new Color(0.02f, 0.02f, 0.05f, 0.95f);
		vigorOutline.effectDistance = new Vector2(1.5f, -1.5f);

		ApplyCombatHudRefactorLayout();
		RefreshCombatHudRefactor();
	}

	private void ApplyCombatHudRefactorLayout()
	{
		if ((Object)(object)combatExperienceRoot == (Object)null)
			return;

		bool portrait = Screen.height > Screen.width;
		SetRect(combatExperienceRoot,
			portrait ? new Vector2(0.055f, 0f) : new Vector2(0.06f, 0f),
			portrait ? new Vector2(0.945f, 0.043f) : new Vector2(0.94f, 0.05f));

		if ((Object)(object)manaRuneImage != (Object)null)
		{
			RectTransform manaRect = manaRuneImage.rectTransform;
			manaRect.anchorMin = manaRect.anchorMax = portrait ? new Vector2(0.395f, 0.09f) : new Vector2(0.42f, 0.105f);
			manaRect.pivot = new Vector2(0.5f, 0.5f);
			manaRect.anchoredPosition = Vector2.zero;
			manaRect.sizeDelta = portrait ? new Vector2(118f, 118f) : new Vector2(138f, 138f);
			manaRect.localScale = new Vector3(1.3f, 1.3f, 1f);
		}

		if ((Object)(object)combatVigorImage != (Object)null)
		{
			RectTransform vigorRect = combatVigorImage.rectTransform;
			vigorRect.anchorMin = vigorRect.anchorMax = portrait ? new Vector2(0.605f, 0.09f) : new Vector2(0.58f, 0.105f);
			vigorRect.pivot = new Vector2(0.5f, 0.5f);
			vigorRect.anchoredPosition = Vector2.zero;
			vigorRect.sizeDelta = portrait ? new Vector2(110f, 110f) : new Vector2(128f, 128f);
			vigorRect.localScale = new Vector3(1.2f, 1.2f, 1f);
		}
	}

	private void RefreshCombatHudRefactor()
	{
		if (runProgress != null && (Object)(object)combatExperienceText != (Object)null)
		{
			int maximum = Mathf.Max(0, runProgress.ExperiencePerLevel);
			int current = Mathf.Max(0, runProgress.CurrentExperience);
			combatExperienceText.text = maximum > 0 ? $"{current} / {maximum} EXP" : "LIVELLO MASSIMO";
			float fill = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 1f;
			AnimateCombatExperienceFill(fill);
		}

		if (playerHud != null && (Object)(object)combatVigorImage != (Object)null)
		{
			combatVigorImage.sprite = playerHud.DiceImage.sprite;
			combatVigorImage.color = playerHud.DiceImage.color;
			combatVigorText.text = playerHud.DiceText.text;
		}
	}

	private void AnimateCombatExperienceFill(float targetFill)
	{
		if ((Object)(object)combatExperienceFill == (Object)null)
			return;

		targetFill = Mathf.Clamp01(targetFill);
		if (combatExperienceFillRoutine != null)
			StopCoroutine(combatExperienceFillRoutine);
		combatExperienceFillRoutine = StartCoroutine(AnimateCombatExperienceFillRoutine(targetFill));
	}

	private IEnumerator AnimateCombatExperienceFillRoutine(float targetFill)
	{
		RectTransform fillRect = combatExperienceFill.rectTransform;
		float startFill = fillRect.anchorMax.x;
		float distance = Mathf.Abs(targetFill - startFill);
		float duration = Mathf.Lerp(0.28f, 1.05f, Mathf.SmoothStep(0f, 1f, distance));
		float elapsed = 0f;

		while (elapsed < duration && (Object)(object)combatExperienceFill != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float normalized = Mathf.Clamp01(elapsed / duration);
			float eased = normalized * normalized * (3f - 2f * normalized);
			fillRect.anchorMax = new Vector2(Mathf.LerpUnclamped(startFill, targetFill, eased), 1f);
			yield return null;
		}

		if ((Object)(object)combatExperienceFill != (Object)null)
			fillRect.anchorMax = new Vector2(targetFill, 1f);
		combatExperienceFillRoutine = null;
	}

	private void SetCombatHudRefactorVisible(bool visible)
	{
		if ((Object)(object)combatExperienceRoot != (Object)null)
			combatExperienceRoot.gameObject.SetActive(visible);
		if ((Object)(object)combatVigorImage != (Object)null)
			combatVigorImage.gameObject.SetActive(visible);
	}

	private void RefreshCombatPawnCarousel(bool animate)
	{
		List<BattleCardState> allPawns = playerCards.Where(card => card != null).ToList();
		List<BattleCardState> alive = allPawns.Where(card => !card.Eliminated).ToList();
		if (alive.Count == 0)
			return;

		BattleCardState next = ResolveCarouselCenter(alive);
		bool changed = carouselCenterCard != null && carouselCenterCard != next;
		carouselCenterCard = next;
		List<BattleCardState> carouselOrder = BuildCombatPawnCarouselOrder(allPawns, next);
		if (carouselRotationRoutine != null)
			StopCoroutine(carouselRotationRoutine);
		carouselRotationRoutine = StartCoroutine(AnimateCombatPawnCarousel(carouselOrder, next, animate && changed));
	}

	private List<BattleCardState> BuildCombatPawnCarouselOrder(
		IReadOnlyList<BattleCardState> allPawns,
		BattleCardState center)
	{
		List<BattleCardState> ordered = new List<BattleCardState>();
		if (center != null)
			ordered.Add(center);

		// Prima vengono le altre pedine vive seguendo l'ordine reale dei turni.
		if (turnOrder.Count > 0)
		{
			int centerTurnIndex = turnOrder.IndexOf(center);
			for (int offset = 1; offset <= turnOrder.Count; offset++)
			{
				int index = centerTurnIndex >= 0
					? (centerTurnIndex + offset) % turnOrder.Count
					: offset - 1;
				BattleCardState candidate = turnOrder[index];
				if (candidate != null && candidate.BelongsToPlayer && !candidate.Eliminated && !ordered.Contains(candidate))
					ordered.Add(candidate);
			}
		}

		foreach (BattleCardState pawn in allPawns)
		{
			if (!pawn.Eliminated && !ordered.Contains(pawn))
				ordered.Add(pawn);
		}
		// Le eliminate sono sempre accodate: non possono piu' restare nello slot centrale.
		foreach (BattleCardState pawn in allPawns)
		{
			if (pawn.Eliminated && !ordered.Contains(pawn))
				ordered.Add(pawn);
		}
		return ordered;
	}

	/// <summary>
	/// Durante lo schieramento conserva l'ordine logico delle carte ma assegna
	/// esplicitamente gli slot visivi: prima a destra, seconda a sinistra e
	/// terza al centro. La transizione successiva puo' quindi essere verticale.
	/// </summary>
	private void ApplyReverseDeploymentPawnOrder()
	{
		if (playerCards.Count < 2)
			return;

		List<RectTransform> rects = playerCards
			.Where(card => card != null && card.View != null && !card.Eliminated)
			.Select(card => card.View.RectTransform)
			.ToList();
		if (rects.Count < 2)
			return;

		List<float> horizontalPositions = rects
			.Select(rect => rect.anchoredPosition.x)
			.OrderBy(value => value)
			.ToList();
		if (rects.Count == 3)
		{
			SetDeploymentPawnHorizontalPosition(rects[0], horizontalPositions[2]);
			SetDeploymentPawnHorizontalPosition(rects[1], horizontalPositions[0]);
			SetDeploymentPawnHorizontalPosition(rects[2], horizontalPositions[1]);
			return;
		}

		for (int i = 0; i < rects.Count; i++)
			SetDeploymentPawnHorizontalPosition(rects[i], horizontalPositions[horizontalPositions.Count - 1 - i]);
	}

	private static void SetDeploymentPawnHorizontalPosition(RectTransform rect, float horizontalPosition)
	{
		Vector2 position = rect.anchoredPosition;
		position.x = horizontalPosition;
		rect.anchoredPosition = position;
	}

	private BattleCardState ResolveCarouselCenter(IReadOnlyList<BattleCardState> alive)
	{
		if (turnOrder.Count > 0)
		{
			for (int offset = 0; offset < turnOrder.Count; offset++)
			{
				BattleCardState candidate = turnOrder[(currentTurnIndex + offset) % turnOrder.Count];
				if (candidate != null && candidate.BelongsToPlayer && !candidate.Eliminated)
					return candidate;
			}
		}
		return alive[0];
	}

	private IEnumerator AnimateCombatPawnCarousel(List<BattleCardState> alive,
		BattleCardState center, bool animate)
	{
		float cardWidth = Mathf.Max(100f, center.View.RectTransform.rect.width);
		float spacing = cardWidth * 1.18f;
		Dictionary<RectTransform, Vector2> starts = new Dictionary<RectTransform, Vector2>();
		Dictionary<RectTransform, Vector2> targets = new Dictionary<RectTransform, Vector2>();
		for (int i = 0; i < alive.Count; i++)
		{
			int slot = i == 0 ? 0 : (i == 1 ? -1 : 1);
			RectTransform rect = alive[i].View.RectTransform;
			starts[rect] = rect.anchoredPosition;
			targets[rect] = new Vector2(slot * spacing, slot == 0 ? cardWidth * 0.2f : -cardWidth * 0.16f);
			rect.SetSiblingIndex(slot == 0 ? rect.parent.childCount - 1 : i);
		}

		float duration = animate ? 0.48f : 0f;
		float elapsed = 0f;
		do
		{
			float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			foreach (KeyValuePair<RectTransform, Vector2> item in targets)
				item.Key.anchoredPosition = Vector2.LerpUnclamped(starts[item.Key], item.Value, eased);
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}
		while (elapsed < duration);

		foreach (KeyValuePair<RectTransform, Vector2> item in targets)
			item.Key.anchoredPosition = item.Value;
		carouselRotationRoutine = null;
	}
}
}
