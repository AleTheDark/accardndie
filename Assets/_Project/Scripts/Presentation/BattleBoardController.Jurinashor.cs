using System;
using System.Collections;
using System.Linq;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using AccardND.Localization;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const int JurinashorSwordManaCost = 3;
	private const int JurinashorPhaseOneMaximumSwords = 3;
	private const int JurinashorPhaseTwoMaximumSwords = 5;
	private const int JurinashorSwordPower = 2;

	private bool IsJurinashorSword(BattleCardState card)
	{
		return card != null && (Object)(object)card.Definition != (Object)null
			&& string.Equals(card.Definition.Id, JurinashorSwordCardId, StringComparison.OrdinalIgnoreCase);
	}

	private int ActiveJurinashorSwordCount()
	{
		return cpuCards.Count(card => IsJurinashorSword(card) && !card.Eliminated);
	}

	private int JurinashorMaximumSwords => activeJurinashorBoss != null && activeJurinashorBoss.IsPhaseTwo
		? JurinashorPhaseTwoMaximumSwords
		: JurinashorPhaseOneMaximumSwords;

	private int JurinashorSwordPowerBonus(BattleCardState card)
	{
		return card != null && IsJurinashorBossDefinition(card.Definition)
			? ActiveJurinashorSwordCount() * JurinashorSwordPower
			: 0;
	}

	private bool IsJurinashorImmuneToDebuffs(BattleCardState card)
	{
		return IsJurinashorBossProxy(card)
			&& activeJurinashorBoss != null
			&& activeJurinashorBoss.IsImmuneToDebuffs;
	}

	private void CleanseJurinashorPhaseTwoMaluses(BattleCardState boss)
	{
		if (boss == null)
			return;

		boss.InhibitedTurns = 0;
		boss.WasInhibited = false;
		boss.PendingVigorStepPenalty = 0;
		boss.Petrified = false;
		boss.PermanentCombatBonus = Math.Max(0, boss.PermanentCombatBonus);
		foreach (BattleCardState hunter in playerCards.Concat(cpuCards))
		{
			if (hunter == null || !hunter.HunterMarkedTargets.Contains(boss))
				continue;
			hunter.HunterMarkedTargets.Remove(boss);
			RefreshPersistentStatus(hunter);
		}
		RefreshPersistentStatus(boss);
		boss.View?.PlayActionCallout("IMMUNITÀ", Color.white);
	}

	private void TrySummonJurinashorSwords(BattleCardState jurinashor, ManaPool pool)
	{
		if (jurinashor == null || pool == null || (Object)(object)cpuRow == (Object)null)
			return;

		CardDefinition swordDefinition = FindCardDefinition(JurinashorSwordCardId);
		if ((Object)(object)swordDefinition == (Object)null)
		{
			AppendLog("JURINASHOR - carta Spada Maledetta non trovata.");
			return;
		}

		while (pool.Current >= JurinashorSwordManaCost
			&& ActiveJurinashorSwordCount() < JurinashorMaximumSwords)
		{
			pool.Spend(JurinashorSwordManaCost);
			if (!SummonJurinashorSword(jurinashor, swordDefinition, "mana"))
				break;
			PlayEnemyManaDeltaCallout(-JurinashorSwordManaCost);
		}

		jurinashor.View?.SetStrengthValue(DisplayStrength(jurinashor));
		ApplyResponsiveLayout();
		LayoutJurinashorSwords();
		RefreshEnemyManaHud();
		UpdateInteractions();
	}

	private void TrySummonJurinashorSwordOnKill(BattleCardState jurinashor)
	{
		if (jurinashor == null || jurinashor.Eliminated
			|| ActiveJurinashorSwordCount() >= JurinashorMaximumSwords)
			return;
		CardDefinition swordDefinition = FindCardDefinition(JurinashorSwordCardId);
		if ((Object)(object)swordDefinition == (Object)null)
			return;
		SummonJurinashorSword(jurinashor, swordDefinition, "uccisione");
		ApplyResponsiveLayout();
		LayoutJurinashorSwords();
		UpdateInteractions();
	}

	private bool SummonJurinashorSword(BattleCardState jurinashor, CardDefinition definition, string reason)
	{
		if ((Object)(object)cpuRow == (Object)null
			|| ActiveJurinashorSwordCount() >= JurinashorMaximumSwords)
			return false;
		BattleCardState sword = AddCard(cpuCards, cpuRow, definition, belongsToPlayer: false, cpuCards.Count);
		if (sword == null)
			return false;
		sword.View?.ConfigureJurinashorSwordPresentation();
		sword.View?.PlayActionCallout(GameText.Get(GameTextKeys.Campaign.JurinashorSwordSummoned), new Color(0.28f, 1f, 0.38f));
		PlaySfx(jurinashorWeaponEvocationSfx);
		LayoutJurinashorSwords();
		if (sword.View != null && sword.View.RectTransform != null)
		{
			JurinashorSwordFloatVfx floatVfx = sword.View.gameObject.AddComponent<JurinashorSwordFloatVfx>();
			StartCoroutine(floatVfx.PlayNecromanticSummon());
		}
		AppendLog($"JURINASHOR - evoca Spada Maledetta per {reason}: {ActiveJurinashorSwordCount()}/{JurinashorMaximumSwords}; +{JurinashorSwordPower} Potenza.");
		jurinashor.View?.SetStrengthValue(DisplayStrength(jurinashor));
		return true;
	}

	private void LayoutJurinashorSwords()
	{
		if ((Object)(object)safeAreaRoot == (Object)null)
			return;

		BattleCardState[] swords = cpuCards
			.Where(card => IsJurinashorSword(card) && !card.Eliminated && card.View != null)
			.ToArray();
		// Con due evocazioni lasciamo più aria attorno al boss; con tre manteniamo
		// invece una distribuzione compatta che resta interamente nel campo di gioco.
		float spacing = swords.Length == 2 ? 0.30f : 0.20f;
		float startX = 0.5f - spacing * (swords.Length - 1) * 0.5f;
		for (int index = 0; index < swords.Length; index++)
		{
			RectTransform rect = swords[index].View.RectTransform;
			if (rect == null)
				continue;
			rect.SetParent(safeAreaRoot, false);
			Vector2 anchor = new Vector2(startX + spacing * index, 0.47f);
			JurinashorSwordFloatVfx floatVfx = rect.GetComponent<JurinashorSwordFloatVfx>();
			if (floatVfx != null)
			{
				floatVfx.SetLayoutAnchor(anchor);
			}
			else
			{
				rect.anchorMin = anchor;
				rect.anchorMax = anchor;
			}
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = new Vector2(0f, 37f);
			rect.sizeDelta = new Vector2(230f, 330f);
			rect.localScale = Vector3.one;
		}
		if ((Object)(object)messagePanelRect != (Object)null)
			messagePanelRect.SetAsLastSibling();
	}

	private void RefreshJurinashorSwordBonusPresentation()
	{
		BattleCardState jurinashor = cpuCards.FirstOrDefault(card => card != null
			&& !card.Eliminated
			&& IsJurinashorBossDefinition(card.Definition));
		jurinashor?.View?.SetStrengthValue(jurinashor.Card.Strength + JurinashorSwordPowerBonus(jurinashor));
	}

	private IEnumerator RemoveDefeatedJurinashorSword(BattleCardState sword)
	{
		if (!IsJurinashorSword(sword))
			yield break;

		PrototypeCardView view = sword.View;
		if ((Object)(object)view != (Object)null)
		{
			view.SetInteractable(false);
		}
		LayoutJurinashorSwords();
		RefreshJurinashorSwordBonusPresentation();
		if ((Object)(object)view != (Object)null)
			yield return PlayJurinashorSwordDeathAndRemove(view);
	}

	private IEnumerator PlayJurinashorSwordDeathAndRemove(PrototypeCardView view)
	{
		if ((Object)(object)view == (Object)null)
			yield break;
		yield return JurinashorSwordFloatVfx.PlayNecromanticDeath(view.RectTransform);
		if ((Object)(object)view != (Object)null)
		{
			view.gameObject.SetActive(false);
			Object.Destroy(view.gameObject);
		}
	}

	private IEnumerator PlayJurinashorPhaseTwoTransformation()
	{
		BattleCardState boss = cpuCards.FirstOrDefault(card => IsJurinashorBossProxy(card)
			&& (Object)(object)card.View != (Object)null);
		if (boss == null)
			yield break;

		PlayNecromancerSupremeSfx();

		Sprite silhouette = Resources.Load<Sprite>("UI/jurinashor_phase_2_contour_mask");
		GameObject effectObject = new GameObject(
			"Jurinashor Phase Two Silhouette VFX Mask",
			typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
		RectTransform effectRect = effectObject.GetComponent<RectTransform>();
		effectRect.SetParent(boss.View.RectTransform, false);
		// Usa esattamente lo stesso rettangolo del contorno interattivo della fase 2:
		// in questo modo maschera, artwork del BG e sagoma luminosa coincidono.
		effectRect.anchorMin = new Vector2(-0.512f, -0.069f);
		effectRect.anchorMax = new Vector2(1.512f, 1.524f);
		effectRect.pivot = new Vector2(0.5f, 0.5f);
		effectRect.offsetMin = Vector2.zero;
		effectRect.offsetMax = Vector2.zero;
		effectRect.SetAsLastSibling();
		Image silhouetteImage = effectObject.GetComponent<Image>();
		silhouetteImage.sprite = silhouette;
		silhouetteImage.color = Color.white;
		silhouetteImage.preserveAspect = true;
		silhouetteImage.raycastTarget = false;
		Mask silhouetteMask = effectObject.GetComponent<Mask>();
		silhouetteMask.showMaskGraphic = false;

		Image flash = CreateImage("Jurinashor Green Transformation Flash", effectRect, Color.clear);
		Stretch(flash.rectTransform);
		flash.raycastTarget = false;

		GameObject lightningObject = new GameObject(
			"Jurinashor Phase Two Green Lightning",
			typeof(RectTransform), typeof(CanvasRenderer), typeof(DarkSigilLightningVfx));
		RectTransform lightningRect = lightningObject.GetComponent<RectTransform>();
		lightningRect.SetParent(effectRect, false);
		Stretch(lightningRect);
		DarkSigilLightningVfx lightning = lightningObject.GetComponent<DarkSigilLightningVfx>();
		lightning.raycastTarget = false;
		lightning.ConfigureNecromanticTransformation();
		// I fulmini devono restare sopra al lampo verde, altrimenti il flash ne
		// attenua proprio i frame più luminosi della trasformazione.
		lightningRect.SetAsLastSibling();
		const float duration = 2.35f;
		for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
		{
			float t = Mathf.Clamp01(elapsed / duration);
			float firstFlash = Mathf.Exp(-Mathf.Pow((t - 0.12f) * 22f, 2f));
			float secondFlash = Mathf.Exp(-Mathf.Pow((t - 0.56f) * 28f, 2f));
			flash.color = new Color(0.22f, 1f, 0.34f, (firstFlash + secondFlash) * 0.34f);
			yield return null;
		}
		if ((Object)(object)effectObject != (Object)null)
			Object.Destroy(effectObject);
	}

	private JurinashorSwordFloatVfx[] ActiveJurinashorSwordVfx()
	{
		return cpuCards
			.Where(card => IsJurinashorSword(card) && !card.Eliminated
				&& (Object)(object)card.View != (Object)null)
			.Select(card => card.View.GetComponent<JurinashorSwordFloatVfx>())
			.Where(vfx => vfx != null && vfx.BeamOrigin != null)
			.ToArray();
	}

	private IEnumerator AimJurinashorSwordsAt(BattleCardState target)
	{
		if (target == null || (Object)(object)target.View == (Object)null)
			yield break;

		JurinashorSwordFloatVfx[] swords = ActiveJurinashorSwordVfx();
		if (swords.Length == 0)
			yield break;

		Vector3 targetWorld = target.View.RectTransform.TransformPoint(target.View.RectTransform.rect.center);
		foreach (JurinashorSwordFloatVfx sword in swords)
			StartCoroutine(sword.EnterExecutionPose(targetWorld));
		yield return new WaitForSecondsRealtime(0.30f);
	}

	private void ReleaseJurinashorSwordAim()
	{
		foreach (JurinashorSwordFloatVfx sword in ActiveJurinashorSwordVfx())
			if (sword != null) sword.ExitExecutionPose();
	}

	private IEnumerator PlayJurinashorSwordExecution(
		BattleCardState attacker,
		BattleCardState target,
		int attackMargin,
		bool abilityAttack,
		bool aimFirst = true)
	{
		if (target == null || (Object)(object)target.View == (Object)null
			|| (Object)(object)safeAreaRoot == (Object)null)
			yield break;

		JurinashorSwordFloatVfx[] swords = ActiveJurinashorSwordVfx();
		if (swords.Length == 0)
			yield break;

		if (aimFirst)
			yield return AimJurinashorSwordsAt(target);

		bool impactReached = false;
		Coroutine attackAnimation = StartCoroutine(PlayHunterRangedAttackIfNeeded(
			attacker,
			target,
			attackMargin,
			abilityAttack,
			onHit: () =>
			{
				impactReached = true;
				PlayResolvedAttackSfx(attacker, hit: true, abilityAttack);
			}));

		GameObject rootObject = new GameObject("Jurinashor Sword Execution Beams", typeof(RectTransform));
		RectTransform root = rootObject.GetComponent<RectTransform>();
		root.SetParent((Transform)(object)safeAreaRoot, false);
		Stretch(root);
		root.SetAsLastSibling();
		Image[] cores = new Image[swords.Length];
		Image[] glows = new Image[swords.Length];
		for (int index = 0; index < swords.Length; index++)
		{
			glows[index] = CreateImage($"Sword Beam Glow {index + 1}", root, Color.clear);
			cores[index] = CreateImage($"Sword Beam Core {index + 1}", root, Color.clear);
			foreach (Image beam in new[] { glows[index], cores[index] })
			{
				beam.raycastTarget = false;
				beam.rectTransform.anchorMin = beam.rectTransform.anchorMax = Vector2.zero;
				beam.rectTransform.pivot = new Vector2(0f, 0.5f);
			}
		}

		const float growDuration = 0.20f;
		const float maximumHold = 4f;
		float elapsed = 0f;
		while (!impactReached && elapsed < maximumHold)
		{
			elapsed += Time.unscaledDeltaTime;
			float p = Mathf.Clamp01(elapsed / growDuration);
			float growth = Mathf.SmoothStep(0f, 1f, p);
			float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 2f));
			Vector3 targetWorld = target.View.RectTransform.TransformPoint(target.View.RectTransform.rect.center);
			for (int index = 0; index < swords.Length; index++)
			{
				RectTransform sourceRect = swords[index].BeamOrigin;
				if ((Object)(object)sourceRect == (Object)null) continue;
				Vector3 sourceWorld = sourceRect.TransformPoint(sourceRect.rect.center);
				Vector2 delta = targetWorld - sourceWorld;
				float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
				// Il bordo finale del Rect coincide col centro della hitbox: il laser
				// non può oltrepassare la pedina nemmeno durante la crescita.
				float length = Mathf.Min(delta.magnitude, delta.magnitude * growth);
				RectTransform glowRect = glows[index].rectTransform;
				RectTransform coreRect = cores[index].rectTransform;
				glowRect.position = coreRect.position = sourceWorld;
				glowRect.localRotation = coreRect.localRotation = Quaternion.Euler(0f, 0f, angle);
				glowRect.sizeDelta = new Vector2(length, 30f);
				coreRect.sizeDelta = new Vector2(length, 8f + Mathf.Sin(elapsed * 34f) * 1.5f);
				glows[index].color = new Color(0.08f, 1f, 0.3f, alpha * 0.30f);
				cores[index].color = new Color(0.54f, 1f, 0.62f, alpha * 0.96f);
			}
			yield return null;
		}

		// L'impatto è il frame emesso dall'animazione d'attacco di Jurinashor.
		// Solo da questo momento il raggio si dissolve.
		const float fadeDuration = 0.14f;
		for (float fade = 0f; fade < fadeDuration; fade += Time.unscaledDeltaTime)
		{
			float alpha = 1f - Mathf.Clamp01(fade / fadeDuration);
			foreach (Image glow in glows)
				if ((Object)(object)glow != (Object)null) glow.color = new Color(0.08f, 1f, 0.3f, alpha * 0.30f);
			foreach (Image core in cores)
				if ((Object)(object)core != (Object)null) core.color = new Color(0.54f, 1f, 0.62f, alpha * 0.96f);
			yield return null;
		}
		if (attackAnimation != null)
			yield return attackAnimation;

		Object.Destroy(rootObject);
		foreach (JurinashorSwordFloatVfx sword in swords)
			if (sword != null) sword.ExitExecutionPose();
	}
}
}
