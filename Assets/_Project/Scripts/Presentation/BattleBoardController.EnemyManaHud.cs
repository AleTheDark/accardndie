using System.Collections.Generic;
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
	private Text enemyManaRuneText;
	private int enemyManaDisplayedValue = -1;
	private Coroutine enemyManaValueTweenRoutine;
	private int enemyManaDeltaCalloutIndex;
	private readonly HashSet<GameObject> enemyManaDeltaCallouts = new HashSet<GameObject>();
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
			55,
			(FontStyle)1,
			(TextAnchor)4);
		enemyManaRuneText.color = Color.white;
		enemyManaRuneText.raycastTarget = false;
		enemyManaRuneText.resizeTextForBestFit = true;
		enemyManaRuneText.resizeTextMinSize = 14;
		enemyManaRuneText.resizeTextMaxSize = 55;
		enemyManaRuneText.text = Mathf.Max(0, BattleCpuManaCurrent).ToString();

		RectTransform valueRect = enemyManaRuneText.rectTransform;
		valueRect.anchorMin = Vector2.zero;
		valueRect.anchorMax = Vector2.one;
		valueRect.offsetMin = new Vector2(6f, 14f);
		valueRect.offsetMax = new Vector2(-6f, 2f);
		valueRect.SetAsLastSibling();

		Outline outline = ((Component)enemyManaRuneText).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.02f, 0.05f, 0.12f, 0.9f);
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
		auraRect.anchorMin = auraRect.anchorMax = new Vector2(0.5f, 0.5f);
		auraRect.pivot = new Vector2(0.5f, 0.5f);
		auraRect.anchoredPosition = new Vector2(1.627f, 4.712f);
		auraRect.sizeDelta = new Vector2(75.2543f, 74.5754f);
		auraRect.localScale = Vector3.one * 1.9762f;
		auraRect.SetAsFirstSibling();

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
			enemyManaRuneAuraImage.rectTransform.localScale = Vector3.one * (1.9762f * Mathf.Lerp(0.93f, 1.1f, pulse));
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

		// Bragus non possiede una riserva mana. Nella debug di Seraphel, inoltre,
		// la runa nemica deve apparire insieme al reveal del boss e non durante lo
		// schieramento del giocatore sul fondale Lux ancora vuoto.
		bool waitingForSeraphelReveal = debugForceFirstRoomSeraphel
			&& !seraphelBossPresentationActive;
		bool visible = BattleManaHudEnabled
			&& !waitingForCampaignBossReveal
			&& !deploymentDraftActive
			&& (pvpPresentationActive || roundNumber > 0 || IsTutorialWarriorDuelActive)
			&& !bragusBossPresentationActive
			&& !waitingForSeraphelReveal
			&& (!IsTutorialWarriorDuelActive
				|| tutorialMageDuelActive
				|| tutorialWarriorDuelStep >= TutorialWarriorDuelStep.Mana);
		// Il dado dei boss e' figlio del contenitore della runa. Bragus non usa mana,
		// ma il contenitore deve restare attivo per poter mostrare il suo dado Vigore.
		bool showBragusDice = bragusBossPresentationActive;
		bool showEnemyHudContainer = visible || showBragusDice;
		if (((Component)enemyManaRuneImage).gameObject.activeSelf != showEnemyHudContainer)
		{
			((Component)enemyManaRuneImage).gameObject.SetActive(showEnemyHudContainer);
		}
		enemyManaRuneImage.enabled = visible;
		if (enemyManaRuneText != null)
			((Component)enemyManaRuneText).gameObject.SetActive(visible);
		if (enemyManaRuneAuraImage != null)
			((Component)enemyManaRuneAuraImage).gameObject.SetActive(visible);

		// Il dado Vigore della CPU fa parte dello stesso blocco informativo della runa:
		// non deve anticiparla durante schieramento/reveal né restare visibile quando
		// la riserva mana nemica è nascosta.
		if (cpuHud != null && (Object)(object)cpuHud.DiceImage != (Object)null)
		{
			bool showCpuDice = (visible || showBragusDice)
				&& (Object)(object)cpuHud.DiceImage.sprite != (Object)null
				&& (!IsTutorialWarriorDuelActive
					|| tutorialMageDuelActive
					|| tutorialWarriorDuelStep >= TutorialWarriorDuelStep.Vigor);
			((Component)cpuHud.DiceImage).gameObject.SetActive(showCpuDice);
			if ((Object)(object)cpuHud.DiceText != (Object)null)
				((Component)cpuHud.DiceText).gameObject.SetActive(showCpuDice);
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

	private void SetPresentedEnemyManaValue(int value)
	{
		if (enemyManaValueTweenRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(enemyManaValueTweenRoutine);
			enemyManaValueTweenRoutine = null;
		}

		enemyManaDisplayedValue = Mathf.Max(0, value);
		if (enemyManaRuneText != null)
			enemyManaRuneText.text = enemyManaDisplayedValue.ToString();
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
		GameObject labelObject = ((Component)label).gameObject;
		enemyManaDeltaCallouts.Add(labelObject);

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
		const float duration = 0.8f;

		float elapsed = 0f;
		while (elapsed < duration && label != null && enemyManaRuneImage != null)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			float rise = Mathf.SmoothStep(0f, 1f, progress);

			// Calcolato dalla posizione corrente della runa a ogni frame: il callout
			// resta agganciato anche dopo cambi di layout, risoluzione o orientamento.
			RectTransform runeRect = enemyManaRuneImage.rectTransform;
			RectTransform safeRect = safeAreaRoot;
			Vector3 runeCenterLocal = safeRect.InverseTransformPoint(
				runeRect.TransformPoint(runeRect.rect.center));
			float x = runeCenterLocal.x - safeRect.rect.xMin + lane;
			float runeYFromTop = runeCenterLocal.y - safeRect.rect.yMax;
			rect.anchoredPosition = new Vector2(x, runeYFromTop - Mathf.Lerp(0f, 110f, rise));

			Color color = EnemyManaDeltaColor;
			color.a = progress < 0.34f ? 1f : 1f - Mathf.InverseLerp(0.34f, 1f, progress);
			label.color = color;
			yield return null;
		}

		if (label != null)
		{
			enemyManaDeltaCallouts.Remove(labelObject);
			Object.Destroy(labelObject);
		}
	}

	/// <summary>
	/// StopAllCoroutines non esegue la coda delle coroutine interrotte. I callout sono
	/// quindi tracciati esplicitamente, cosi' nessun testo puo' sopravvivere a un cambio
	/// stanza, al ritorno all'hub o all'avvio di una nuova avventura.
	/// </summary>
	private void ClearEnemyManaDeltaCallouts()
	{
		foreach (GameObject callout in enemyManaDeltaCallouts)
		{
			if (callout == null)
				continue;

			callout.SetActive(false);
			Object.Destroy(callout);
		}
		enemyManaDeltaCallouts.Clear();
		enemyManaDeltaCalloutIndex = 0;
	}
}
}
