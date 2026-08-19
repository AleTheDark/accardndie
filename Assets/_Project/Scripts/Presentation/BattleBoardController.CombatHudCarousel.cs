using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using AccardND.Battlefield;
using AccardND.Localization;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const int CombatExperienceFontSize = 30;
	private RectTransform combatExperienceRoot;
	private ArcaneExperienceFillGraphic combatExperienceFill;
	private Coroutine combatExperienceFillRoutine;
	private Text combatExperienceText;
	private Image combatVigorImage;
	private Text combatVigorText;
	private BattleCardState carouselCenterCard;
	private Coroutine carouselRotationRoutine;
	private int carouselRotationFrame = -1;
	private readonly Dictionary<RectTransform, Vector2> carouselTargets = new Dictionary<RectTransform, Vector2>();
	private readonly Dictionary<RectTransform, Vector2> carouselAnimatedTargets = new Dictionary<RectTransform, Vector2>();
	private readonly List<(RectTransform Rect, Vector2 Pose)> combatPawnPoseSnapshot = new List<(RectTransform, Vector2)>();

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

		combatExperienceText = CreateText("Combat Experience Value", expRoot.transform, font, CombatExperienceFontSize,
			FontStyle.Bold, TextAnchor.MiddleCenter);
		// This HUD value must remain fixed instead of inheriting portrait scaling or best-fit resizing.
		combatExperienceText.fontSize = CombatExperienceFontSize;
		combatExperienceText.resizeTextForBestFit = false;
		combatExperienceText.resizeTextMinSize = CombatExperienceFontSize;
		combatExperienceText.resizeTextMaxSize = CombatExperienceFontSize;
		combatExperienceText.color = Color.white;
		combatExperienceText.raycastTarget = false;
		Stretch(combatExperienceText.rectTransform, 2f);
		Outline expOutline = combatExperienceText.gameObject.AddComponent<Outline>();
		expOutline.effectColor = new Color(0.01f, 0.02f, 0.06f, 0.95f);
		expOutline.effectDistance = new Vector2(1.4f, -1.4f);

		combatVigorImage = CreateImage("Combat Vigor Die", (Transform)(object)safeAreaRoot, Color.white);
		combatVigorImage.preserveAspect = true;
		combatVigorImage.raycastTarget = false;
		combatVigorText = CreateText("Combat Vigor Value", combatVigorImage.transform, font, 35,
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
		Vector2 leftResourceAnchor = portrait ? new Vector2(0.395f, 0.09f) : new Vector2(0.42f, 0.105f);
		Vector2 rightResourceAnchor = portrait ? new Vector2(0.605f, 0.09f) : new Vector2(0.58f, 0.105f);
		Vector2 leftEnemyAnchor = new Vector2(leftResourceAnchor.x, 1f - leftResourceAnchor.y);
		Vector2 rightEnemyAnchor = new Vector2(rightResourceAnchor.x, 1f - rightResourceAnchor.y);
		SetRect(combatExperienceRoot,
			portrait ? new Vector2(0.055f, 0f) : new Vector2(0.06f, 0f),
			portrait ? new Vector2(0.945f, 0.043f) : new Vector2(0.94f, 0.05f));

		if ((Object)(object)manaRuneImage != (Object)null)
		{
			RectTransform manaRect = manaRuneImage.rectTransform;
			manaRect.anchorMin = manaRect.anchorMax = leftResourceAnchor;
			manaRect.pivot = new Vector2(0.5f, 0.5f);
			manaRect.anchoredPosition = Vector2.zero;
			manaRect.sizeDelta = portrait ? new Vector2(118f, 118f) : new Vector2(138f, 138f);
			manaRect.localScale = new Vector3(1.3f, 1.3f, 1f);
		}

		if ((Object)(object)combatVigorImage != (Object)null)
		{
			RectTransform vigorRect = combatVigorImage.rectTransform;
			vigorRect.anchorMin = vigorRect.anchorMax = rightResourceAnchor;
			vigorRect.pivot = new Vector2(0.5f, 0.5f);
			vigorRect.anchoredPosition = Vector2.zero;
			vigorRect.sizeDelta = portrait ? new Vector2(110f, 110f) : new Vector2(128f, 128f);
			vigorRect.localScale = new Vector3(1.2f, 1.2f, 1f);
		}

		if ((Object)(object)enemyManaRuneImage != (Object)null)
		{
			RectTransform enemyManaRect = enemyManaRuneImage.rectTransform;
			enemyManaRect.anchorMin = enemyManaRect.anchorMax = leftEnemyAnchor;
			enemyManaRect.pivot = new Vector2(0.5f, 0.5f);
			enemyManaRect.anchoredPosition = new Vector2(0f, -24f);
			enemyManaRect.sizeDelta = portrait ? new Vector2(118f, 118f) : new Vector2(138f, 138f);
			enemyManaRect.localScale = new Vector3(1.3f, 1.3f, 1f);
		}

		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
		{
			Stretch(cpuHud.Rect, 0f);
			if ((Object)(object)cpuHud.DiceImage != (Object)null)
			{
				RectTransform cpuDiceRect = cpuHud.DiceImage.rectTransform;
				// Boss e cambi di forma possono aggiornare il dado, non spostarlo.
				if (cpuDiceRect.parent != cpuHud.Rect)
					cpuDiceRect.SetParent(cpuHud.Rect, false);
				cpuDiceRect.anchorMin = cpuDiceRect.anchorMax = rightEnemyAnchor;
				cpuDiceRect.pivot = new Vector2(0.5f, 0.5f);
				cpuDiceRect.anchoredPosition = new Vector2(0f, -23.5f);
				cpuDiceRect.sizeDelta = portrait ? new Vector2(110f, 110f) : new Vector2(128f, 128f);
				cpuDiceRect.localScale = new Vector3(1.2f, 1.2f, 1f);
			}
			if ((Object)(object)cpuHud.DiceText != (Object)null)
			{
				RectTransform cpuDiceValueRect = cpuHud.DiceText.rectTransform;
				if (cpuDiceValueRect.parent != cpuHud.DiceImage.transform)
					cpuDiceValueRect.SetParent(cpuHud.DiceImage.transform, false);
				Stretch(cpuDiceValueRect, 3f);
				cpuDiceValueRect.localScale = Vector3.one;
			}
		}
	}

	private void RefreshCombatHudRefactor()
	{
		if (runProgress != null && (Object)(object)combatExperienceText != (Object)null)
		{
			int maximum = Mathf.Max(0, runProgress.ExperiencePerLevel);
			int current = Mathf.Max(0, runProgress.CurrentExperience);
			combatExperienceText.text = maximum > 0
				? GameText.Format(GameTextKeys.Combat.ExperienceProgress, current, maximum)
				: GameText.Get(GameTextKeys.Campaign.MaxLevel);
			float fill = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 1f;
			AnimateCombatExperienceFill(fill);
		}

		if ((Object)(object)combatVigorImage != (Object)null)
		{
			int vigorSides = pvpPresentationActive && pvpState != null
				? Mathf.Max(1, pvpState.VigorDieSides)
				: EffectivePlayerHudVigorDieSides();
			combatVigorImage.sprite = LoadHudDiceSprite(vigorSides, isPlayer: true);
			combatVigorImage.color = Color.white;
			combatVigorImage.enabled = (Object)(object)combatVigorImage.sprite != (Object)null;
			if ((Object)(object)combatVigorText != (Object)null)
				combatVigorText.text = $"D{vigorSides}";
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
			combatExperienceRoot.gameObject.SetActive(
				visible && !pvpPresentationActive && !IsTutorialWarriorDuelActive);
		if ((Object)(object)combatVigorImage != (Object)null)
			combatVigorImage.gameObject.SetActive(
				visible && !waitingForCampaignBossReveal
					&& (!IsTutorialWarriorDuelActive
						|| tutorialMageDuelActive
						|| tutorialWarriorDuelStep >= TutorialWarriorDuelStep.Vigor));
	}

	/// <summary>
	/// Fotografa la posa attuale delle pedine locali prima che la griglia di
	/// formazione la riscriva. La rotazione deve partire da dove le pedine sono
	/// davvero: farla partire dalla fila appena scritta significa mostrarle in
	/// fila per un istante, ed e' esattamente il flick che si vedeva in PvP a
	/// ogni azione, dove ogni aggiornamento del server ripassa dal layout.
	/// </summary>
	private void CaptureCombatPawnPoses()
	{
		combatPawnPoseSnapshot.Clear();
		foreach (BattleCardState card in playerCards)
		{
			if (card == null || (Object)(object)card.View == (Object)null)
				continue;

			RectTransform rect = card.View.RectTransform;
			combatPawnPoseSnapshot.Add((rect, rect.anchoredPosition));
		}
	}

	private void RestoreCombatPawnPoses()
	{
		foreach ((RectTransform rect, Vector2 pose) in combatPawnPoseSnapshot)
		{
			if ((Object)(object)rect != (Object)null)
				rect.anchoredPosition = pose;
		}
		combatPawnPoseSnapshot.Clear();
	}

	private void RefreshCombatPawnCarousel(bool animate)
	{
		List<BattleCardState> allPawns = playerCards.Where(card => card != null).ToList();
		List<BattleCardState> alive = allPawns.Where(card => !card.Eliminated).ToList();
		if (alive.Count == 0)
			return;

		BattleCardState next = ResolveCarouselCenter(alive);
		carouselCenterCard = next;
		List<BattleCardState> carouselOrder = BuildCombatPawnCarouselOrder(allPawns, next);
		BuildCombatPawnCarouselTargets(carouselOrder, next);
		if (carouselTargets.Count == 0)
			return;

		// Non basta guardare la pedina centrale: una morte accoda l'eliminata in
		// fondo all'ordine lasciando il centro dov'era, e chi decideva in base al
		// solo centro faceva scivolare quella riga di colpo. Conta chi si muove
		// davvero, chiunque sia.
		if (animate && HasPendingCarouselMovement())
		{
			// Una rotazione gia' in volo verso questi stessi bersagli non si
			// riavvia: la coroutine li insegue da se', e ripartire da capo a ogni
			// aggiornamento del server - in PvP ne arrivano anche a meta' corsa -
			// la farebbe strisciare invece di arrivare.
			if (IsRoutineAlive(carouselRotationRoutine, carouselRotationFrame)
				&& IsAnimatingTowardCurrentCarouselTargets())
			{
				return;
			}

			if (carouselRotationRoutine != null)
				StopCoroutine(carouselRotationRoutine);
			carouselAnimatedTargets.Clear();
			foreach (KeyValuePair<RectTransform, Vector2> target in carouselTargets)
				carouselAnimatedTargets[target.Key] = target.Value;
			carouselRotationFrame = Time.frameCount;
			carouselRotationRoutine = StartCoroutine(AnimateCombatPawnCarousel());
			return;
		}

		// Un ricalcolo di layout non deve troncare una rotazione in volo: i
		// bersagli sono appena stati aggiornati e la coroutine ci scivola sopra
		// da sola. Solo a riposo la posa finale si scrive, e si scrive subito:
		// una versione "animata a durata zero" resterebbe comunque un frame
		// esposta a chi scrive dopo di lei.
		if (!IsRoutineAlive(carouselRotationRoutine, carouselRotationFrame))
			ApplyCombatPawnCarouselTargets();
	}

	/// <summary>
	/// Slot visivi del carosello: la pedina attiva al centro e rialzata, le
	/// altre alternate ai suoi lati partendo da sinistra. Oltre le tre pedine
	/// della formazione gli slot continuano ad allargarsi invece di accatastare
	/// tutte le pedine in eccesso sulla stessa posizione.
	/// </summary>
	private void BuildCombatPawnCarouselTargets(List<BattleCardState> order, BattleCardState center)
	{
		carouselTargets.Clear();
		if (center == null || (Object)(object)center.View == (Object)null)
			return;

		float cardWidth = Mathf.Max(100f, center.View.RectTransform.rect.width);
		float spacing = cardWidth * 1.18f;
		for (int i = 0; i < order.Count; i++)
		{
			BattleCardState pawn = order[i];
			if (pawn == null || (Object)(object)pawn.View == (Object)null)
				continue;

			int slot = CarouselSlot(i);
			RectTransform rect = pawn.View.RectTransform;
			carouselTargets[rect] = new Vector2(
				slot * spacing,
				slot == 0 ?cardWidth * 0.2f : (0f - cardWidth) * 0.16f);
		}

		ApplyCombatPawnCarouselSorting(order);
	}

	/// <summary>
	/// Riscrive l'ordine di disegno dal fondo verso il centro: ogni pedina
	/// portata in cima copre quelle gia' passate, e l'ultima a salire e' la
	/// centrale. Chiedere invece un indice numerico per pedina non funzionava:
	/// l'indice era la posizione nel carosello, non nella gerarchia, e gli
	/// spostamenti si applicavano uno sull'altro. Con tre pedine l'ultima del
	/// giro chiedeva proprio l'indice della cima e la rubava alla centrale, e
	/// il risultato cambiava con il numero di figli della fila - sei nel frame
	/// dello schieramento, dove le preview appena distrutte sono ancora li',
	/// tre dal frame dopo. Da qui il sorting che saltava durante la discesa.
	/// </summary>
	private static void ApplyCombatPawnCarouselSorting(List<BattleCardState> order)
	{
		for (int i = order.Count - 1; i >= 0; i--)
		{
			BattleCardState pawn = order[i];
			if (pawn == null || (Object)(object)pawn.View == (Object)null)
				continue;

			((Transform)pawn.View.RectTransform).SetAsLastSibling();
		}
	}

	private static int CarouselSlot(int orderIndex)
	{
		if (orderIndex == 0)
			return 0;

		int distance = (orderIndex + 1) / 2;
		return (orderIndex % 2 == 1) ?-distance : distance;
	}

	private bool IsAnimatingTowardCurrentCarouselTargets()
	{
		if (carouselAnimatedTargets.Count != carouselTargets.Count)
			return false;

		foreach (KeyValuePair<RectTransform, Vector2> target in carouselTargets)
		{
			if (!carouselAnimatedTargets.TryGetValue(target.Key, out Vector2 animated))
				return false;
			if (Vector2.Distance(animated, target.Value) > 0.5f)
				return false;
		}
		return true;
	}

	private bool HasPendingCarouselMovement()
	{
		foreach (KeyValuePair<RectTransform, Vector2> target in carouselTargets)
		{
			if ((Object)(object)target.Key == (Object)null)
				continue;
			if (Vector2.Distance(target.Key.anchoredPosition, target.Value) > 0.5f)
				return true;
		}
		return false;
	}

	private void ApplyCombatPawnCarouselTargets()
	{
		foreach (KeyValuePair<RectTransform, Vector2> target in carouselTargets)
		{
			if ((Object)(object)target.Key != (Object)null)
				target.Key.anchoredPosition = target.Value;
		}
	}

	private List<BattleCardState> BuildCombatPawnCarouselOrder(
		IReadOnlyList<BattleCardState> allPawns,
		BattleCardState center)
	{
		List<BattleCardState> ordered = new List<BattleCardState>();
		if (center != null)
			ordered.Add(center);

		// In PvP il turnOrder della campagna non viene popolato: il server resta
		// autorevole e l'ordine visivo segue l'iniziativa ricevuta nello snapshot.
		if (pvpPresentationActive && pvpState != null)
		{
			foreach (BattleCardState pawn in allPawns
				.Where(card => !card.Eliminated)
				.OrderBy(card => card.Initiative))
			{
				if (!ordered.Contains(pawn))
					ordered.Add(pawn);
			}
		}

		// Prima vengono le altre pedine vive seguendo l'ordine reale dei turni.
		if (!pvpPresentationActive && turnOrder.Count > 0)
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

	private BattleCardState ResolveCarouselCenter(IReadOnlyList<BattleCardState> alive)
	{
		if (pvpPresentationActive && pvpState != null)
		{
			BattleCardState activeLocalPawn = pvpState.ActivePlayer == pvpState.MyIndex
				? FindPvpLocalCard(pvpState.ActiveSlot)
				: null;
			if (activeLocalPawn != null && !activeLocalPawn.Eliminated)
				return activeLocalPawn;

			// Durante il turno avversario conserva il centro corrente; in questo modo
			// le pedine ruotano solo quando cambia davvero la pedina locale attiva.
			if (carouselCenterCard != null
				&& !carouselCenterCard.Eliminated
				&& alive.Contains(carouselCenterCard))
				return carouselCenterCard;

			return alive.OrderBy(card => card.Initiative).First();
		}

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

	/// <summary>
	/// Scivola verso i bersagli correnti, non verso una copia congelata: se un
	/// ricalcolo di layout li sposta a meta' corsa, la rotazione li insegue
	/// invece di essere interrotta e ripresa con uno scatto.
	/// </summary>
	private IEnumerator AnimateCombatPawnCarousel()
	{
		Dictionary<RectTransform, Vector2> starts = new Dictionary<RectTransform, Vector2>();
		foreach (RectTransform rect in carouselTargets.Keys)
		{
			if ((Object)(object)rect != (Object)null)
				starts[rect] = rect.anchoredPosition;
		}

		const float duration = 0.48f;
		float elapsed = 0f;
		bool firstFrame = true;
		while (elapsed < duration)
		{
			carouselRotationFrame = Time.frameCount;
			// Il primo giro non consuma tempo: la rotazione parte spesso subito
			// dopo un frame pesante (ricalcolo di layout, pedine appena create) e
			// il delta di quel frame la farebbe nascere gia' a un terzo di corsa.
			elapsed += firstFrame ?0f : AnimationDeltaTime();
			firstFrame = false;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			foreach (KeyValuePair<RectTransform, Vector2> target in carouselTargets)
			{
				if ((Object)(object)target.Key == (Object)null)
					continue;
				if (!starts.TryGetValue(target.Key, out Vector2 start))
				{
					// Pedina entrata in scena a rotazione gia' iniziata.
					start = target.Key.anchoredPosition;
					starts[target.Key] = start;
				}
				target.Key.anchoredPosition = Vector2.LerpUnclamped(start, target.Value, eased);
			}
			yield return null;
		}

		ApplyCombatPawnCarouselTargets();
		carouselRotationRoutine = null;
	}
}
}
