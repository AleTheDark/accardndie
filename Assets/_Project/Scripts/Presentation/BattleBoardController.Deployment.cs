using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.NetProtocol;
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
	private void BeginFormationDraft()
	{
		ClearDraftEntranceState();
		SetCombatChromeVisible(visible: true);
		draftActive = true;
		inputLocked = true;
		selectedDraftCards.Clear();
		selectedPlayerDeploymentIndices.Clear();
		selectedCpuDeploymentCards.Clear();
		selectedPlayerDeploymentInitiatives.Clear();
		selectedCpuDeploymentInitiatives.Clear();
		deploymentOrder.Clear();
		foreach (PrototypeCardView playerDeploymentPreviewView in playerDeploymentPreviewViews)
		{
			if ((Object)(object)playerDeploymentPreviewView != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)playerDeploymentPreviewView).gameObject);
			}
		}
		playerDeploymentPreviewViews.Clear();
		deploymentDraftActive = false;
		deploymentInitiativesReady = false;
		playerAura = BattleAuraType.None;
		cpuAura = BattleAuraType.None;
		formationAuraUsed = false;
		draftCandidates.Clear();
		draftCampaignCards.Clear();
		if (campaignDeck != null)
		{
			draftCampaignCards.AddRange(campaignDeck.DrawCombatHand(random, configuration.DeckBuilding.CombatHandSize));
			draftCandidates.AddRange(draftCampaignCards.Select((CampaignCardInstance card) => card.Definition));
			if (draftCandidates.Count < configuration.Gameplay.FormationSize)
			{
				draftActive = false;
				inputLocked = true;
				gameFinished = true;
				SetTurnBanner(playerTurn: false, "CAMPAGNA TERMINATA  -  MAZZO ESAURITO");
				SetMessage($"Non hai abbastanza carte disponibili: {draftCandidates.Count}/" + $"{configuration.Gameplay.FormationSize}. Cimitero: {campaignDeck.GraveyardCount}. " + "Ritorno all'inizio tra 5 secondi.");
				((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
				return;
			}
		}
		else
		{
			draftCandidates.AddRange(formationDraftService.DrawCandidates(cardDatabase.Cards, configuration.Gameplay.DraftCandidateCount));
		}
		ShowFormationDraftHint();
		for (int num = 0; num < draftCandidates.Count; num++)
		{
			int capturedIndex = num;
			PrototypeCardView prototypeCardView = PrototypeCardView.Create((Transform)(object)playerHandRow, draftCandidates[num], configuration);
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				ToggleDraftCard(capturedIndex);
			});
			prototypeCardView.ClearDragHandlers();
			prototypeCardView.SetInteractable(campaignDeck == null || currentRoomType != RoomType.Monster);
			prototypeCardView.SetAlpha(0f);
			draftViews.Add(prototypeCardView);
		}
		playerTitleText.text = $"SCEGLI {configuration.Gameplay.FormationSize} CARTE";
		bool flag = campaignDeck != null && (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss);
		if (flag)
		{
			playerTitleText.text = string.Empty;
		}
		((Component)confirmActionButton).gameObject.SetActive(!flag);
		if ((Object)(object)confirmActionButtonText != (Object)null)
		{
			confirmActionButtonText.text = "CONFERMA";
		}
		confirmActionButton.interactable = false;
		RefreshInitiativeDisplay();
		if (flag)
		{
			SetMessage("Le carte entrano in mano, poi iniziera lo schieramento.");
		}
		else
		{
			SetMessage("Prepara la formazione: seleziona le carte che vuoi portare in battaglia.");
		}
		ApplyResponsiveLayout();
		StartDraftHandEntrance(flag);
	}

	private void StartDraftHandEntrance(bool beginInitiativeDeploymentAfterEntrance)
	{
		if (draftEntranceCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(draftEntranceCoroutine);
		}
		draftEntranceCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayDraftHandEntrance(beginInitiativeDeploymentAfterEntrance));
	}

	private void ClearDraftEntranceState()
	{
		if (draftEntranceCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(draftEntranceCoroutine);
		}
		StopPlayerBattlefieldRowTransition();
		StopHandRedealAnimation();
		draftEntranceAnimatingViews.Clear();
		handRelayoutAnimatingViews.Clear();
		for (int i = draftEntranceOverlayObjects.Count - 1; i >= 0; i--)
		{
			GameObject overlay = draftEntranceOverlayObjects[i];
			if ((Object)(object)overlay != (Object)null)
			{
				Object.Destroy((Object)(object)overlay);
			}
		}
		draftEntranceOverlayObjects.Clear();
		draftEntranceCoroutine = null;
	}

	private IEnumerator PlayDraftHandEntrance(bool beginInitiativeDeploymentAfterEntrance)
	{
		yield return WaitForHintToClose();
		draftEntranceAnimatingViews.Clear();
		if (draftViews.Count == 0 || (Object)(object)playerHandRow == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
		{
			draftEntranceCoroutine = null;
			if (beginInitiativeDeploymentAfterEntrance)
			{
				((MonoBehaviour)this).StartCoroutine(BeginInitiativeDeployment());
			}
			yield break;
		}
		foreach (PrototypeCardView view in draftViews)
		{
			if ((Object)(object)view != (Object)null)
			{
				view.SetInteractable(interactable: false);
				view.SetAlpha(0f);
			}
		}
		Canvas.ForceUpdateCanvases();
		ApplyHandFan();
		Canvas.ForceUpdateCanvases();
		AnimationConfiguration animation = configuration.Animation;
		float enterDuration = Mathf.Max(0.08f, animation.DraftCardEnterDuration);
		float holdDuration = Mathf.Max(0f, animation.DraftCardCenterHold);
		float settleDuration = Mathf.Max(0.08f, animation.DraftCardSettleDuration);
		float initialDelay = Mathf.Max(0f, animation.DraftCardEntranceInitialDelay);
		float entranceScale = Mathf.Max(1f, animation.DraftCardEntranceScale);
		float betweenCardsDelay = Mathf.Max(0f, animation.DraftCardEntranceStagger);
		int count = draftViews.Count;
		Vector2[] targets = new Vector2[count];
		Quaternion[] targetRotations = new Quaternion[count];
		Vector2[] sizes = new Vector2[count];
		Rect safeBounds = safeAreaRoot.rect;
		for (int i = 0; i < count; i++)
		{
			PrototypeCardView view = draftViews[i];
			if ((Object)(object)view == (Object)null)
			{
				continue;
			}
			RectTransform rect = view.RectTransform;
			targets[i] = RectCenterInSafeArea(rect);
			targetRotations[i] = Quaternion.Inverse(((Transform)safeAreaRoot).rotation) * ((Transform)rect).rotation;
			sizes[i] = RectSizeInSafeArea(rect);
			draftEntranceAnimatingViews.Add(view);
		}
		if (initialDelay > 0f)
		{
			yield return WaitForCardInspectionPause(initialDelay);
		}
		for (int i = 0; i < count; i++)
		{
			PrototypeCardView realView = draftViews[i];
			if ((Object)(object)realView == (Object)null)
			{
				continue;
			}
			GameObject overlayObject = Object.Instantiate(((Component)realView).gameObject, (Transform)(object)safeAreaRoot, false);
			overlayObject.name = ((Object)((Component)realView).gameObject).name + "-entrance";
			NormalizeDraftEntranceClone(overlayObject);
			PrototypeCardView overlayView = overlayObject.GetComponent<PrototypeCardView>();
			Button overlayButton = overlayObject.GetComponent<Button>();
			if ((Object)(object)overlayButton != (Object)null)
			{
				overlayButton.interactable = false;
			}
			overlayView.SetLayoutIgnored(ignored: true);
			overlayView.SetAlpha(0f);
			draftEntranceOverlayObjects.Add(overlayObject);
			RectTransform animatedRect = overlayView.RectTransform;
			animatedRect.anchorMin = new Vector2(0.5f, 0.5f);
			animatedRect.anchorMax = new Vector2(0.5f, 0.5f);
			animatedRect.pivot = new Vector2(0.5f, 0.5f);
			animatedRect.sizeDelta = sizes[i];
			float cardWidth = Mathf.Max(1f, sizes[i].x);
			Vector2 start = new Vector2(safeBounds.xMax + cardWidth * 0.9f, Mathf.Lerp(0f, targets[i].y, 0.35f));
			Vector2 center = new Vector2(0f, 0f);
			animatedRect.anchoredPosition = start;
			((Transform)animatedRect).localRotation = Quaternion.identity;
			((Transform)animatedRect).localScale = Vector3.one * 0.82f;
			overlayView.SetAlpha(0f);
			PlayDrawCardSfx();
			float elapsed = 0f;
			while (elapsed < enterDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / enterDuration));
				animatedRect.anchoredPosition = Vector2.LerpUnclamped(start, center, progress);
				((Transform)animatedRect).localRotation = Quaternion.identity;
				((Transform)animatedRect).localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, entranceScale, progress);
				overlayView.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (enterDuration * 0.42f))));
				yield return null;
			}
			animatedRect.anchoredPosition = center;
			((Transform)animatedRect).localRotation = Quaternion.identity;
			((Transform)animatedRect).localScale = Vector3.one * entranceScale;
			overlayView.SetAlpha(1f);
			if (holdDuration > 0f)
			{
				yield return WaitForCardInspectionPause(holdDuration);
			}
			elapsed = 0f;
			while (elapsed < settleDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float settleProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settleDuration));
				float scaleEase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settleDuration));
				animatedRect.anchoredPosition = Vector2.LerpUnclamped(center, targets[i], settleProgress);
				((Transform)animatedRect).localRotation = Quaternion.SlerpUnclamped(Quaternion.identity, targetRotations[i], settleProgress);
				((Transform)animatedRect).localScale = Vector3.one * Mathf.LerpUnclamped(entranceScale, 1f, scaleEase);
				overlayView.SetAlpha(1f);
				yield return null;
			}
			realView.SetAlpha(1f);
			((Transform)realView.RectTransform).localScale = Vector3.one;
			((Transform)realView.RectTransform).localRotation = targetRotations[i];
			draftEntranceAnimatingViews.Remove(realView);
			draftEntranceOverlayObjects.Remove(overlayObject);
			Object.Destroy((Object)(object)overlayObject);
			if (betweenCardsDelay > 0f && i < count - 1)
			{
				yield return WaitForCardInspectionPause(betweenCardsDelay);
			}
		}
		draftEntranceOverlayObjects.Clear();
		ApplyResponsiveLayout();
		Canvas.ForceUpdateCanvases();
		ApplyHandFan();
		draftEntranceAnimatingViews.Clear();
		draftEntranceCoroutine = null;
		if (beginInitiativeDeploymentAfterEntrance)
		{
			((MonoBehaviour)this).StartCoroutine(BeginInitiativeDeployment());
		}
		else
		{
			for (int i = 0; i < draftViews.Count; i++)
			{
				PrototypeCardView view = draftViews[i];
				if ((Object)(object)view != (Object)null)
				{
					view.SetInteractable(!selectedDraftCards.Contains(i));
				}
			}
			NotifyAdventureTutorial(AdventureTutorialAction.DraftReady);
		}
	}

	private static void NormalizeDraftEntranceClone(GameObject overlayObject)
	{
		if ((Object)(object)overlayObject == (Object)null)
		{
			return;
		}
		Canvas[] canvases = overlayObject.GetComponentsInChildren<Canvas>(includeInactive: true);
		foreach (Canvas canvas in canvases)
		{
			Object.DestroyImmediate((Object)(object)canvas);
		}
		GraphicRaycaster[] raycasters = overlayObject.GetComponentsInChildren<GraphicRaycaster>(includeInactive: true);
		foreach (GraphicRaycaster raycaster in raycasters)
		{
			Object.DestroyImmediate((Object)(object)raycaster);
		}
		DisableCloneChild(overlayObject.transform, "Card Action Overlay");
		DisableCloneChild(overlayObject.transform, "Card Dice");
	}

	private static void DisableCloneChild(Transform root, string childName)
	{
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (string.Equals(((Object)((Component)child).gameObject).name, childName, StringComparison.OrdinalIgnoreCase))
			{
				((Component)child).gameObject.SetActive(false);
			}
			DisableCloneChild(child, childName);
		}
	}

	private void ToggleDraftCard(int index)
	{
		if (!draftActive || index < 0 || index >= draftViews.Count)
		{
			return;
		}
		if (deploymentDraftActive)
		{
			if (inputLocked || selectedDraftCards.Contains(index))
			{
				return;
			}
			DeploymentToken deploymentToken = deploymentOrder[currentDeploymentIndex];
			if (deploymentToken.BelongsToPlayer)
			{
				pendingDeploymentIndex = index;
				for (int i = 0; i < draftViews.Count; i++)
				{
					bool flag = selectedDraftCards.Contains(i);
					draftViews[i].SetDraftSelected(i == pendingDeploymentIndex);
					draftViews[i].SetInteractable(!flag);
				}
				RefreshCardActionOverlays();
				SetMessage($"INIZIATIVA {deploymentToken.Initiative}: confermi {draftCandidates[index].DisplayName} in campo?");
				NotifyAdventureTutorial(AdventureTutorialAction.DeploymentCardSelected);
			}
			return;
		}
		if (!selectedDraftCards.Remove(index))
		{
			if (selectedDraftCards.Count >= configuration.Gameplay.FormationSize)
			{
				return;
			}
			selectedDraftCards.Add(index);
		}
		for (int j = 0; j < draftViews.Count; j++)
		{
			draftViews[j].SetDraftSelected(selectedDraftCards.Contains(j));
		}
		confirmActionButton.interactable = selectedDraftCards.Count == configuration.Gameplay.FormationSize;
		SetMessage($"Formazione: {selectedDraftCards.Count}/{configuration.Gameplay.FormationSize} carte selezionate.");
		NotifyAdventureTutorial(AdventureTutorialAction.DraftCardSelected);
	}

	private int RollUniqueInitiative(int dieSides, HashSet<int> usedInitiatives)
	{
		if (usedInitiatives == null)
		{
			throw new ArgumentNullException("usedInitiatives");
		}
		dieSides = Mathf.Max(1, dieSides);
		for (int i = 0; i < dieSides * 3; i++)
		{
			int num = random.NextInclusive(1, dieSides);
			if (usedInitiatives.Add(num))
			{
				return num;
			}
		}
		for (int j = 1; j <= dieSides; j++)
		{
			if (usedInitiatives.Add(j))
			{
				return j;
			}
		}
		int k;
		for (k = dieSides + 1; !usedInitiatives.Add(k); k++)
		{
		}
		return k;
	}

	private static int AssignUniqueLastInitiative(HashSet<int> usedInitiatives)
	{
		if (usedInitiatives == null)
		{
			throw new ArgumentNullException("usedInitiatives");
		}
		int num = 0;
		while (!usedInitiatives.Add(num))
		{
			num--;
		}
		return num;
	}

	private IEnumerator BeginInitiativeDeployment()
	{
		deploymentDraftActive = true;
		inputLocked = true;
		int formationSize = configuration.Gameplay.FormationSize;
		BuildCpuDeploymentHand();
		int cpuDeploymentCount = UsesBossStyleDeployment() || survivingCpuFormation.Count > 0
			?cpuDeploymentHand.Count
			:formationSize;
		int initiativeDieSides = configuration.Gameplay.InitiativeDieSides;
		HashSet<int> usedInitiatives = new HashSet<int>();
		for (int i = 0; i < formationSize; i++)
		{
			deploymentOrder.Add(new DeploymentToken(belongsToPlayer: true, RollUniqueInitiative(initiativeDieSides, usedInitiatives), random.NextInclusive(1, 10000)));
		}
		for (int j = 0; j < cpuDeploymentCount; j++)
		{
			deploymentOrder.Add(new DeploymentToken(belongsToPlayer: false, RollUniqueInitiative(initiativeDieSides, usedInitiatives), random.NextInclusive(1, 10000)));
		}
		deploymentOrder.Sort(delegate(DeploymentToken left, DeploymentToken right)
		{
			int num3 = left.Initiative.CompareTo(right.Initiative);
			return (num3 == 0) ?left.TieBreaker.CompareTo(right.TieBreaker) : num3;
		});
		currentDeploymentIndex = 0;
		foreach (DeploymentToken item in deploymentOrder)
		{
			AppendLog(string.Format("INIZIATIVA SCHIERAMENTO {0} - D{1} = {2}", item.BelongsToPlayer ?"TU" : "CPU", initiativeDieSides, item.Initiative));
		}
		SetTurnBanner(playerTurn: true, "SCHIERAMENTO");
		RefreshInitiativeDisplay();
		ClearDeploymentTimeline();
		ShowDeploymentInitiativeHint();
		yield return WaitForHintToClose();
		SetMessage($"Tiro iniziativa: {formationSize} D20 per te e {cpuDeploymentCount} D20 per il Master.");
		yield return PlayDeploymentInitiativeDiceRoll(initiativeDieSides);
		RefreshDeploymentTimeline();
		SetMessage("Iniziative di schieramento: i valori piu bassi calano per primi.");
		yield return WaitForCardInspectionPause(Mathf.Max(0.2f, configuration.Animation.DiceResultHold * 0.45f));
		ProcessNextDeploymentToken();
	}

	private void BuildCpuDeploymentHand()
	{
		cpuDeploymentHand.Clear();
		if (survivingCpuFormation.Count > 0)
		{
			cpuDeploymentHand.AddRange(survivingCpuFormation);
			return;
		}

		if (UsesBossStyleDeployment())
		{
			cpuDeploymentHand.AddRange(BuildCpuFormationForCurrentCombat());
			return;
		}

		int monsterPoolCount = (from card in cardDatabase.Cards
			where (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat
			select card.Id into id
			where !string.IsNullOrWhiteSpace(id)
			select id).Distinct().Count();
		cpuDeploymentHand.AddRange(formationDraftService.DrawCandidates(
			cardDatabase.Cards,
			Mathf.Min(configuration.DeckBuilding.CombatHandSize, monsterPoolCount)));
	}

	private void ProcessNextDeploymentToken()
	{
		if (!deploymentDraftActive)
		{
			return;
		}
		pendingDeploymentIndex = -1;
		if ((Object)(object)cancelActionButton != (Object)null)
		{
			((Component)cancelActionButton).gameObject.SetActive(false);
		}
		if ((Object)(object)confirmActionButton != (Object)null)
		{
			((Component)confirmActionButton).gameObject.SetActive(false);
		}
		RefreshCardActionOverlays();
		if (currentDeploymentIndex >= deploymentOrder.Count)
		{
			deploymentDraftActive = false;
			deploymentInitiativesReady = true;
			foreach (PrototypeCardView cpuDeploymentPreviewView in cpuDeploymentPreviewViews)
			{
				Object.Destroy((Object)(object)((Component)cpuDeploymentPreviewView).gameObject);
			}
			cpuDeploymentPreviewViews.Clear();
			FinalizeDeploymentAndStartBattle();
			return;
		}
		DeploymentToken deploymentToken = deploymentOrder[currentDeploymentIndex];
		RefreshDeploymentTimeline();
		if (deploymentToken.BelongsToPlayer)
		{
			inputLocked = false;
			for (int i = 0; i < draftViews.Count; i++)
			{
				draftViews[i].SetInteractable(!selectedDraftCards.Contains(i));
			}
			SetMessage("Scegli una carta dalla tua mano da schierare.");
		}
		else
		{
			inputLocked = true;
			SetMessage($"INIZIATIVA CPU {deploymentToken.Initiative}: il Master sceglie una carta...");
			((MonoBehaviour)this).StartCoroutine(ExecuteCpuDeployment(deploymentToken));
		}
	}

	private IEnumerator ExecuteCpuDeployment(DeploymentToken token)
	{
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		if (cpuDeploymentHand.Count == 0)
		{
			AppendLog($"SCHIERAMENTO CPU - nessuna carta disponibile per iniziativa {token.Initiative}; token saltato.");
			currentDeploymentIndex++;
			ProcessNextDeploymentToken();
			yield break;
		}
		CardDefinition cardDefinition = ChooseAdaptiveCpuDeploymentCard();
		selectedCpuDeploymentCards.Add(cardDefinition);
		selectedCpuDeploymentInitiatives.Add(token.Initiative);
		cpuDeploymentHand.Remove(cardDefinition);
		PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)cpuRow, cardDefinition, configuration);
		MakeDeploymentPreviewInspectable(prototypeCardView, cardDefinition);
		cpuDeploymentPreviewViews.Add(prototypeCardView);
		AppendLog($"SCHIERAMENTO CPU - {cardDefinition.DisplayName}, iniziativa {token.Initiative}");
		ApplyResponsiveLayout();
		Canvas.ForceUpdateCanvases();
		PlayPawnEnteringBattlefieldSfx(cardDefinition);
		prototypeCardView.PlayRevealAnimation(configuration.Animation.CpuCardRevealDuration);
		yield return WaitForCardInspectionPause(Mathf.Min(configuration.Animation.CpuCardRevealDuration, 0.35f));
		currentDeploymentIndex++;
		if (currentDeploymentIndex >= deploymentOrder.Count)
		{
			SetMessage("Schieramento completato: inizia il combattimento.");
		}
		ProcessNextDeploymentToken();
	}

	private IEnumerator ContinueDeploymentAfterDelay(float delay)
	{
		yield return WaitForCardInspectionPause(delay);
		ProcessNextDeploymentToken();
	}

	private void StartHandRedealAnimation(IReadOnlyDictionary<PrototypeCardView, HandRedealPose> startPoses)
	{
		if (startPoses == null || startPoses.Count == 0)
		{
			return;
		}
		StopHandRedealAnimation();
		handRelayoutCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayHandRedealAnimation(startPoses));
	}

	private void StopHandRedealAnimation()
	{
		if (handRelayoutCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(handRelayoutCoroutine);
			handRelayoutCoroutine = null;
		}
		foreach (PrototypeCardView view in handRelayoutAnimatingViews)
		{
			if ((Object)(object)view != (Object)null)
			{
				view.SetLayoutIgnored(ignored: false);
			}
		}
		handRelayoutAnimatingViews.Clear();
	}

	private IEnumerator PlayHandRedealAnimation(IReadOnlyDictionary<PrototypeCardView, HandRedealPose> startPoses)
	{
		handRelayoutAnimatingViews.Clear();
		Dictionary<PrototypeCardView, HandRedealPose> targetPoses = new Dictionary<PrototypeCardView, HandRedealPose>();
		foreach (KeyValuePair<PrototypeCardView, HandRedealPose> pair in startPoses)
		{
			PrototypeCardView view = pair.Key;
			if ((Object)(object)view == (Object)null || (Object)(object)view.RectTransform == (Object)null || selectedDraftCards.Contains(draftViews.IndexOf(view)))
			{
				continue;
			}
			targetPoses[view] = new HandRedealPose(view.RectTransform.position, ((Transform)view.RectTransform).rotation);
			handRelayoutAnimatingViews.Add(view);
			view.SetLayoutIgnored(ignored: true);
			view.RectTransform.position = pair.Value.WorldPosition;
			((Transform)view.RectTransform).rotation = pair.Value.WorldRotation;
		}
		if (targetPoses.Count == 0)
		{
			handRelayoutAnimatingViews.Clear();
			handRelayoutCoroutine = null;
			yield break;
		}
		float duration = Mathf.Clamp(configuration.Animation.CardDeployDuration * 0.38f, 0.16f, 0.28f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			foreach (KeyValuePair<PrototypeCardView, HandRedealPose> pair in startPoses)
			{
				PrototypeCardView view = pair.Key;
				if ((Object)(object)view == (Object)null || !targetPoses.TryGetValue(view, out HandRedealPose target))
				{
					continue;
				}
				view.RectTransform.position = Vector3.LerpUnclamped(pair.Value.WorldPosition, target.WorldPosition, eased);
				((Transform)view.RectTransform).rotation = Quaternion.SlerpUnclamped(pair.Value.WorldRotation, target.WorldRotation, eased);
			}
			yield return null;
		}
		foreach (PrototypeCardView view in targetPoses.Keys)
		{
			if ((Object)(object)view == (Object)null)
			{
				continue;
			}
			view.SetLayoutIgnored(ignored: false);
		}
		handRelayoutAnimatingViews.Clear();
		ApplyResponsiveLayout();
		Canvas.ForceUpdateCanvases();
		ApplyHandFan();
		handRelayoutCoroutine = null;
	}

	private CardDefinition ChooseAdaptiveCpuDeploymentCard()
	{
		CardDefinition result = cpuDeploymentHand[0];
		int num = int.MinValue;
		foreach (CardDefinition item in cpuDeploymentHand)
		{
			int num2 = item.Strength * 10 + random.NextInclusive(0, 5);
			foreach (int selectedPlayerDeploymentIndex in selectedPlayerDeploymentIndices)
			{
				num2 += ClassMatchup.Compare(item.HeroClass, draftCandidates[selectedPlayerDeploymentIndex].HeroClass) switch
				{
					MatchupResult.Disadvantage => -15, 
					MatchupResult.Advantage => 30, 
					_ => 0, 
				};
			}
			if (num2 > num)
			{
				result = item;
				num = num2;
			}
		}
		return result;
	}

	private void RefreshDeploymentTimeline()
	{
		if (!((Object)(object)initiativeTimelineRoot == (Object)null))
		{
			RestoreTimelineBaseRect();
			ClearDeploymentTimeline();
			Font builtinResource = AccardND.Battlefield.MmoUiTheme.BodyFont;
			float timelineTileSize = GetTimelineTileSize(deploymentOrder.Count);
			for (int i = 0; i < deploymentOrder.Count; i++)
			{
				DeploymentToken deploymentToken = deploymentOrder[i];
				bool flag = i == currentDeploymentIndex;
				Image image = CreateImage(deploymentToken.BelongsToPlayer ?"Deploy TU" : "Deploy CPU", (Transform)(object)initiativeTimelineRoot, flag ?new Color(0.72f, 0.48f, 0.12f, 0.98f) : (deploymentToken.BelongsToPlayer ?new Color(0.04f, 0.42f, 0.48f, 0.95f) : new Color(0.5f, 0.1f, 0.12f, 0.95f)));
				LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
				ConfigureTimelineTileLayout(layoutElement, timelineTileSize);
				Text text = CreateText("Token", ((Component)image).transform, builtinResource, 18, (FontStyle)1, (TextAnchor)4);
				text.text = deploymentToken.BelongsToPlayer ?"TU" : "CPU";
				Stretch(text.rectTransform, 2f);
			}
			ResizeTimelineTiles(deploymentOrder.Count);
		}
	}

	private void ClearDeploymentTimeline()
	{
		if ((Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return;
		}
		for (int num = ((Transform)initiativeTimelineRoot).childCount - 1; num >= 0; num--)
		{
			GameObject childObject = ((Component)((Transform)initiativeTimelineRoot).GetChild(num)).gameObject;
			childObject.SetActive(false);
			Object.Destroy((Object)(object)childObject);
		}
	}

	private IEnumerator PlayDeploymentInitiativeDiceRoll(int dieSides, string opponentLabel = "CPU")
	{
		if ((Object)(object)safeAreaRoot == (Object)null || deploymentOrder.Count == 0)
		{
			yield break;
		}
		Canvas.ForceUpdateCanvases();
		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		List<RectTransform> diceRects = new List<RectTransform>();
		List<Image> diceImages = new List<Image>();
		List<Text> diceTexts = new List<Text>();
		List<Sprite[]> diceFrameSets = new List<Sprite[]>();
		List<Sprite> diceEndSprites = new List<Sprite>();
		Sprite[] playerDiceFrames = LoadDiceUiRollFrames("Dice");
		Sprite[] cpuDiceFrames = LoadDiceUiRollFrames("Brown_Dice");
		Sprite playerDiceEnd = LoadDiceUiSprite("Dice_End_1");
		Sprite cpuDiceEnd = LoadDiceUiSprite("Brown_Dice_End_1");
		Sprite catalogDiceEnd = LoadCatalogDiceSprite(dieSides, dieSides);
		if ((Object)(object)playerDiceEnd == (Object)null)
		{
			playerDiceEnd = catalogDiceEnd;
		}
		if ((Object)(object)cpuDiceEnd == (Object)null)
		{
			cpuDiceEnd = catalogDiceEnd;
		}
		if (playerDiceFrames.Length == 0 && (Object)(object)playerDiceEnd != (Object)null)
		{
			playerDiceFrames = new[] { playerDiceEnd };
		}
		if (cpuDiceFrames.Length == 0 && (Object)(object)cpuDiceEnd != (Object)null)
		{
			cpuDiceFrames = new[] { cpuDiceEnd };
		}
		Rect safeRect = safeAreaRoot.rect;
		float width = Mathf.Max(1f, safeRect.width);
		float height = Mathf.Max(1f, safeRect.height);
		float diceSize = Mathf.Clamp(Mathf.Min(width, height) * 0.105f, 54f, 92f);
		if (Dice3DRollView.IsSupported(dieSides))
		{
			yield return PlayDeploymentInitiativeDiceRoll3D(dieSides, opponentLabel, diceSize, width, height);
			yield break;
		}
		List<DeploymentToken> playerTokens = deploymentOrder.Where((DeploymentToken token) => token.BelongsToPlayer).ToList();
		List<DeploymentToken> cpuTokens = deploymentOrder.Where((DeploymentToken token) => !token.BelongsToPlayer).ToList();
		Dictionary<DeploymentToken, RectTransform> rectByToken = new Dictionary<DeploymentToken, RectTransform>();
		Dictionary<DeploymentToken, Image> imageByToken = new Dictionary<DeploymentToken, Image>();
		CreateDeploymentInitiativeDice(playerTokens, belongsToPlayer: true);
		CreateDeploymentInitiativeDice(cpuTokens, belongsToPlayer: false);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		float rollDuration = Mathf.Max(0.65f, configuration.Animation.DiceRollDuration * 0.72f);
		float elapsed = 0f;
		while (elapsed < rollDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			for (int i = 0; i < diceRects.Count; i++)
			{
				RectTransform rectTransform = diceRects[i];
				Image image = diceImages[i];
				if ((Object)(object)rectTransform == (Object)null || (Object)(object)image == (Object)null)
				{
					continue;
				}
				Sprite[] frames = i < diceFrameSets.Count ?diceFrameSets[i] : Array.Empty<Sprite>();
				if (frames.Length > 0)
				{
					int frameIndex = Mathf.Abs(Mathf.FloorToInt((elapsed * 18f) + i * 2.37f)) % frames.Length;
					image.sprite = frames[frameIndex];
				}
				rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((elapsed * 16f) + i) * 10f);
				rectTransform.localScale = Vector3.one * (1f + Mathf.Sin((elapsed * 22f) + i) * 0.045f);
			}
			yield return null;
		}
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		for (int i = 0; i < deploymentOrder.Count; i++)
		{
			DeploymentToken token = deploymentOrder[i];
			if (!imageByToken.TryGetValue(token, out Image image) || (Object)(object)image == (Object)null)
			{
				continue;
			}
			int imageIndex = diceImages.IndexOf(image);
			Sprite endSprite = imageIndex >= 0 && imageIndex < diceEndSprites.Count ?diceEndSprites[imageIndex] : null;
			if ((Object)(object)endSprite != (Object)null)
			{
				image.sprite = endSprite;
			}
		}
		foreach (Text text in diceTexts)
		{
			if ((Object)(object)text != (Object)null)
			{
				text.gameObject.SetActive(true);
			}
		}
		yield return WaitForCardInspectionPause(1f);
		ResizeTimelineTiles(deploymentOrder.Count);
		Canvas.ForceUpdateCanvases();
		Vector2[] targetPositions = GetDeploymentTimelineTargetPositions(deploymentOrder.Count);
		List<Vector2> starts = new List<Vector2>(deploymentOrder.Count);
		for (int i = 0; i < deploymentOrder.Count; i++)
		{
			starts.Add(rectByToken.TryGetValue(deploymentOrder[i], out RectTransform rectTransform) && (Object)(object)rectTransform != (Object)null ?rectTransform.anchoredPosition : Vector2.zero);
		}
		float flyDuration = 0.32f;
		for (int i = 0; i < deploymentOrder.Count; i++)
		{
			if (!rectByToken.TryGetValue(deploymentOrder[i], out RectTransform rectTransform) || (Object)(object)rectTransform == (Object)null)
			{
				continue;
			}
			Vector2 start = i < starts.Count ?starts[i] : rectTransform.anchoredPosition;
			Vector2 target = i < targetPositions.Length ?targetPositions[i] : start;
			elapsed = 0f;
			while (elapsed < flyDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / flyDuration);
				float eased = 1f - Mathf.Pow(1f - t, 3f);
				rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
				rectTransform.sizeDelta = Vector2.LerpUnclamped(new Vector2(diceSize, diceSize), new Vector2(GetTimelineTileSize(), GetTimelineTileSize()), eased);
				rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.58f, eased);
				yield return null;
			}
			rectTransform.anchoredPosition = target;
			rectTransform.sizeDelta = new Vector2(GetTimelineTileSize(), GetTimelineTileSize());
			rectTransform.localScale = Vector3.one * 0.58f;
		}
		foreach (RectTransform rectTransform in diceRects)
		{
			if ((Object)(object)rectTransform != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)rectTransform).gameObject);
			}
		}

		void CreateDeploymentInitiativeDice(List<DeploymentToken> tokens, bool belongsToPlayer)
		{
			int count = tokens.Count;
			if (count <= 0)
			{
				return;
			}
			float rowY = belongsToPlayer ?0.405f : 0.565f;
			float startX = 0.5f - Mathf.Min(0.24f, 0.085f * (count - 1));
			float stepX = count <= 1 ?0f : Mathf.Min(0.17f, 0.48f / (count - 1));
			for (int i = 0; i < count; i++)
			{
				DeploymentToken token = tokens[i];
				GameObject diceObject = new GameObject((belongsToPlayer ?"Player" :"CPU") + " Initiative Die", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
				diceObject.transform.SetParent((Transform)(object)safeAreaRoot, false);
				diceObject.transform.SetAsLastSibling();
				RectTransform rectTransform = (RectTransform)diceObject.transform;
				rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
				rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
				rectTransform.pivot = new Vector2(0.5f, 0.5f);
				rectTransform.sizeDelta = new Vector2(diceSize, diceSize);
				rectTransform.anchoredPosition = AnchorToSafeAreaPosition(new Vector2(startX + stepX * i, rowY));
				Image image = diceObject.GetComponent<Image>();
				image.color = Color.white;
				image.preserveAspect = true;
				image.raycastTarget = false;
				Sprite[] frames = belongsToPlayer ?playerDiceFrames :cpuDiceFrames;
				Sprite endSprite = belongsToPlayer ?playerDiceEnd :cpuDiceEnd;
				image.sprite = frames.Length > 0 ?frames[Mathf.Abs(i) % frames.Length] : endSprite;
				Text text = CreateText("Initiative Value", diceObject.transform, font, 22, (FontStyle)1, (TextAnchor)4);
				text.text = $"{(belongsToPlayer ?"TU" :opponentLabel)}\n{token.Initiative}";
				text.color = Color.white;
				text.resizeTextForBestFit = true;
				text.resizeTextMinSize = 12;
				text.resizeTextMaxSize = 24;
				Stretch(text.rectTransform, 2f);
				text.gameObject.SetActive(false);
				diceRects.Add(rectTransform);
				diceImages.Add(image);
				diceTexts.Add(text);
				diceFrameSets.Add(frames);
				diceEndSprites.Add(endSprite);
				rectByToken[token] = rectTransform;
				imageByToken[token] = image;
			}
		}

		Vector2 AnchorToSafeAreaPosition(Vector2 anchor)
		{
			return new Vector2((anchor.x - 0.5f) * width, (anchor.y - 0.5f) * height);
		}
	}

	private IEnumerator PlayDeploymentInitiativeDiceRoll3D(int dieSides, string opponentLabel, float diceSize, float width, float height)
	{
		// D20 d'iniziativa più grandi del tiro standard: il risultato si legge
		// direttamente sulla faccia del dado, senza testo di appoggio.
		diceSize = Mathf.Clamp(Mathf.Min(width, height) * 0.16f, 84f, 150f);
		List<RectTransform> diceRects = new List<RectTransform>();
		List<Dice3DRollView> diceViews = new List<Dice3DRollView>();
		Dictionary<DeploymentToken, RectTransform> rectByToken = new Dictionary<DeploymentToken, RectTransform>();
		Dictionary<DeploymentToken, Dice3DRollView> viewByToken = new Dictionary<DeploymentToken, Dice3DRollView>();
		RectTransform playerBoard = CreateInvisibleDiceBoard("Player Initiative Dice Board", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.48f));
		RectTransform opponentBoard = CreateInvisibleDiceBoard("Opponent Initiative Dice Board", new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.96f));

		CreateDeploymentInitiativeDice3D(deploymentOrder.Where((DeploymentToken token) => token.BelongsToPlayer).ToList(), belongsToPlayer: true, playerBoard);
		CreateDeploymentInitiativeDice3D(deploymentOrder.Where((DeploymentToken token) => !token.BelongsToPlayer).ToList(), belongsToPlayer: false, opponentBoard);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		float rollDuration = Mathf.Max(0.75f, configuration.Animation.DiceRollDuration * 0.9f);
		for (int i = 0; i < deploymentOrder.Count; i++)
		{
			DeploymentToken token = deploymentOrder[i];
			if (viewByToken.TryGetValue(token, out Dice3DRollView diceView) && (Object)(object)diceView != (Object)null)
			{
				HeroClass tint = token.BelongsToPlayer ? HeroClass.Mage : HeroClass.Assassin;
				diceView.StartScriptedRoll(dieSides, tint, token.Initiative, rollDuration);
				// Tinte dedicate all'iniziativa: blu pieno per il giocatore,
				// rosso pieno per l'avversario.
				diceView.OverrideGlow(
					token.BelongsToPlayer ? new Color(0.15f, 0.4f, 1f) : new Color(0.95f, 0.12f, 0.15f),
					token.BelongsToPlayer ? "iniziativa-blu" : "iniziativa-rosso");
			}
		}
		yield return WaitForCardInspectionPause(rollDuration);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);

		yield return WaitForCardInspectionPause(1f);

		ResizeTimelineTiles(deploymentOrder.Count);
		Canvas.ForceUpdateCanvases();
		Vector2[] targetPositions = GetDeploymentTimelineTargetPositions(deploymentOrder.Count);
		List<Vector2> starts = new List<Vector2>(deploymentOrder.Count);
		for (int i = 0; i < deploymentOrder.Count; i++)
		{
			starts.Add(rectByToken.TryGetValue(deploymentOrder[i], out RectTransform rectTransform) && (Object)(object)rectTransform != (Object)null
				? rectTransform.anchoredPosition
				: Vector2.zero);
		}

		float flyDuration = 0.32f;
		float elapsed = 0f;
		for (int i = 0; i < deploymentOrder.Count; i++)
		{
			if (!rectByToken.TryGetValue(deploymentOrder[i], out RectTransform rectTransform) || (Object)(object)rectTransform == (Object)null)
				continue;

			Vector2 start = i < starts.Count ? starts[i] : rectTransform.anchoredPosition;
			Vector2 target = i < targetPositions.Length ? targetPositions[i] : start;
			elapsed = 0f;
			while (elapsed < flyDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / flyDuration);
				float eased = 1f - Mathf.Pow(1f - t, 3f);
				rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
				rectTransform.sizeDelta = Vector2.LerpUnclamped(new Vector2(diceSize, diceSize), new Vector2(GetTimelineTileSize(), GetTimelineTileSize()), eased);
				rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.58f, eased);
				yield return null;
			}
			rectTransform.anchoredPosition = target;
			rectTransform.sizeDelta = new Vector2(GetTimelineTileSize(), GetTimelineTileSize());
			rectTransform.localScale = Vector3.one * 0.58f;
		}

		foreach (RectTransform rectTransform in diceRects)
		{
			if ((Object)(object)rectTransform != (Object)null)
				Object.Destroy((Object)(object)((Component)rectTransform).gameObject);
		}
		if ((Object)(object)playerBoard != (Object)null)
			Object.Destroy((Object)(object)((Component)playerBoard).gameObject);
		if ((Object)(object)opponentBoard != (Object)null)
			Object.Destroy((Object)(object)((Component)opponentBoard).gameObject);

		void CreateDeploymentInitiativeDice3D(List<DeploymentToken> tokens, bool belongsToPlayer, RectTransform board)
		{
			int count = tokens.Count;
			if (count <= 0)
				return;

			float rowY = belongsToPlayer ? 0.405f : 0.565f;
			float startX = 0.5f - Mathf.Min(0.24f, 0.085f * (count - 1));
			float stepX = count <= 1 ? 0f : Mathf.Min(0.17f, 0.48f / (count - 1));
			for (int i = 0; i < count; i++)
			{
				DeploymentToken token = tokens[i];
				GameObject diceObject = new GameObject((belongsToPlayer ? "Player" : "Opponent") + " Initiative Die 3D", typeof(RectTransform));
				diceObject.transform.SetParent((Transform)(object)safeAreaRoot, false);
				diceObject.transform.SetAsLastSibling();
				RectTransform rectTransform = (RectTransform)diceObject.transform;
				rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
				rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
				rectTransform.pivot = new Vector2(0.5f, 0.5f);
				rectTransform.sizeDelta = new Vector2(diceSize, diceSize);
				rectTransform.anchoredPosition = AnchorToSafeAreaPosition(new Vector2(startX + stepX * i, rowY));

				Dice3DRollView diceView = Dice3DRollView.Create(rectTransform);
				diceView.SetBounceArea(board, null);

				diceRects.Add(rectTransform);
				diceViews.Add(diceView);
				rectByToken[token] = rectTransform;
				viewByToken[token] = diceView;
			}
		}

		RectTransform CreateInvisibleDiceBoard(string name, Vector2 minimum, Vector2 maximum)
		{
			RectTransform board = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
			board.SetParent((Transform)(object)safeAreaRoot, false);
			SetRect(board, minimum, maximum);
			return board;
		}

		Vector2 AnchorToSafeAreaPosition(Vector2 anchor)
		{
			return new Vector2((anchor.x - 0.5f) * width, (anchor.y - 0.5f) * height);
		}
	}

	private Vector2[] GetDeploymentTimelineTargetPositions(int count)
	{
		Vector2[] positions = new Vector2[Mathf.Max(0, count)];
		if (count <= 0)
		{
			return positions;
		}
		if ((Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return positions;
		}
		float tileSize = GetTimelineTileSize(count);
		Vector2[] localPositions = GetTimelineLocalPositions(count, tileSize);
		for (int i = 0; i < count; i++)
		{
			Vector3 worldPosition = ((Transform)initiativeTimelineRoot).TransformPoint(localPositions[i]);
			positions[i] = (Object)(object)safeAreaRoot != (Object)null
				?(Vector2)((Transform)safeAreaRoot).InverseTransformPoint(worldPosition)
				: (Vector2)worldPosition;
		}
		return positions;
	}

	private static Sprite[] LoadDiceUiRollFrames(string prefix)
	{
		return Array.Empty<Sprite>();
	}

	private static Sprite LoadDiceUiSprite(string spriteName)
	{
		return null;
	}

	private static Sprite LoadCatalogDiceSprite(int sides, int result)
	{
		return null;
	}

	private void ConfirmPendingDeployment()
	{
		int num = pendingDeploymentIndex;
		if (!deploymentDraftActive || num < 0 || num >= draftViews.Count)
		{
			return;
		}
		pendingDeploymentIndex = -1;
		DeploymentToken deploymentToken = deploymentOrder[currentDeploymentIndex];
		selectedDraftCards.Add(num);
		selectedPlayerDeploymentIndices.Add(num);
		selectedPlayerDeploymentInitiatives.Add(deploymentToken.Initiative);
		PrototypeCardView prototypeCardView = draftViews[num];
		Vector3 position = ((Component)prototypeCardView).transform.position;
		Quaternion rotation = ((Component)prototypeCardView).transform.rotation;
		Vector2 startSize = RectSizeInSafeArea(prototypeCardView.RectTransform);
		Dictionary<PrototypeCardView, HandRedealPose> handStartPoses = new Dictionary<PrototypeCardView, HandRedealPose>();
		for (int i = 0; i < draftViews.Count; i++)
		{
			PrototypeCardView view = draftViews[i];
			if (i == num || selectedDraftCards.Contains(i) || (Object)(object)view == (Object)null || (Object)(object)view.RectTransform == (Object)null || view.RectTransform.parent != (Transform)(object)playerHandRow)
			{
				continue;
			}
			handStartPoses[view] = new HandRedealPose(view.RectTransform.position, ((Transform)view.RectTransform).rotation);
		}
		PrototypeCardView prototypeCardView2 = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)playerRow, draftCandidates[num], configuration);
		MakeDeploymentPreviewInspectable(prototypeCardView2, draftCandidates[num]);
		prototypeCardView2.SetSelected(selected: true);
		prototypeCardView2.SetAlpha(0f);
		playerDeploymentPreviewViews.Add(prototypeCardView2);
		prototypeCardView.SetSelected(selected: false);
		prototypeCardView.SetInteractable(interactable: false);
		prototypeCardView.SetAlpha(0f);
		prototypeCardView.SetLayoutIgnored(ignored: true);
		((Transform)prototypeCardView.RectTransform).SetParent((Transform)(object)safeAreaRoot, true);
		foreach (PrototypeCardView draftView in draftViews)
		{
			draftView.SetInteractable(interactable: false);
		}
		((Component)confirmActionButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		RefreshCardActionOverlays();
		AppendLog($"SCHIERAMENTO TU - {draftCandidates[num].DisplayName}, iniziativa {deploymentToken.Initiative}");
		NotifyAdventureTutorial(AdventureTutorialAction.DeploymentConfirmed);
		inputLocked = true;
		ApplyResponsiveLayout();
		Canvas.ForceUpdateCanvases();
		ApplyHandFan();
		if (selectedPlayerDeploymentIndices.Count >= configuration.Gameplay.FormationSize)
		{
			HideRemainingDeploymentHand();
		}
		else
		{
			StartHandRedealAnimation(handStartPoses);
		}
		PlayPawnEnteringBattlefieldSfx(draftCandidates[num]);
		((MonoBehaviour)this).StartCoroutine(PlayDeploymentMorph(draftCandidates[num], position, rotation, startSize, prototypeCardView2, configuration.Animation.CardDeployDuration));
		currentDeploymentIndex++;
		((MonoBehaviour)this).StartCoroutine(ContinueDeploymentAfterDelay(configuration.Animation.CardDeployDuration));
	}

	private void HideRemainingDeploymentHand()
	{
		StopHandRedealAnimation();
		foreach (PrototypeCardView draftView in draftViews)
		{
			if ((Object)(object)draftView == (Object)null || selectedDraftCards.Contains(draftViews.IndexOf(draftView)))
			{
				continue;
			}
			draftView.SetSelected(selected: false);
			draftView.SetInteractable(interactable: false);
			draftView.SetAlpha(0f);
			draftView.SetLayoutIgnored(ignored: true);
		}
		if ((Object)(object)playerTitleText != (Object)null)
		{
			playerTitleText.text = string.Empty;
		}
	}

	private IEnumerator PlayDeploymentMorph(CardDefinition definition, Vector3 startWorldPosition, Quaternion startWorldRotation, Vector2 startSize, PrototypeCardView finalPreview, float duration)
	{
		if ((Object)(object)safeAreaRoot == (Object)null || (Object)(object)definition == (Object)null || (Object)(object)finalPreview == (Object)null)
		{
			if ((Object)(object)finalPreview != (Object)null)
			{
				finalPreview.SetAlpha(1f);
			}
			yield break;
		}
		GameObject overlayRoot = new GameObject(definition.Id + "-deployment-morph", new Type[2]
		{
			typeof(RectTransform),
			typeof(CanvasGroup)
		});
		overlayRoot.transform.SetParent((Transform)(object)safeAreaRoot, false);
		overlayRoot.transform.SetAsLastSibling();
		RectTransform overlayRect = (RectTransform)overlayRoot.transform;
		overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
		overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
		overlayRect.pivot = new Vector2(0.5f, 0.5f);
		PrototypeCardView cardFace = PrototypeCardView.Create((Transform)(object)overlayRect, definition, configuration);
		PrototypeCardView tokenFace = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)overlayRect, definition, configuration);
		PrepareMorphFace(cardFace, 1f);
		PrepareMorphFace(tokenFace, 0f);
		Canvas.ForceUpdateCanvases();
		RectTransform rectTransform = finalPreview.RectTransform;
		Vector2 startPosition = WorldToSafeAreaPosition(startWorldPosition);
		Vector2 targetPosition = RectCenterInSafeArea(rectTransform);
		Vector2 targetSize = RectSizeInSafeArea(rectTransform);
		if (startSize.x <= 1f || startSize.y <= 1f)
		{
			startSize = targetSize;
		}
		overlayRect.anchoredPosition = startPosition;
		overlayRect.sizeDelta = startSize;
		((Transform)overlayRect).rotation = startWorldRotation;
		CanvasGroup overlayGroup = overlayRoot.GetComponent<CanvasGroup>();
		overlayGroup.alpha = 1f;
		float elapsed = 0f;
		float safeDuration = Mathf.Max(0.001f, duration);
		while (elapsed < safeDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float num = Mathf.Clamp01(elapsed / safeDuration);
			float num2 = Mathf.SmoothStep(0f, 1f, num);
			float num3 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.28f) / 0.58f));
			overlayRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, num2);
			overlayRect.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, num3);
			((Transform)overlayRect).localRotation = Quaternion.SlerpUnclamped(Quaternion.Inverse(((Transform)safeAreaRoot).rotation) * startWorldRotation, Quaternion.identity, num2);
			((Transform)overlayRect).localScale = Vector3.one * Mathf.LerpUnclamped(1.03f, 1f, num2);
			cardFace.SetAlpha(1f - num3);
			tokenFace.SetAlpha(num3);
			finalPreview.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.72f) / 0.28f)));
			overlayGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01((num - 0.9f) / 0.1f));
			yield return null;
		}
		finalPreview.SetAlpha(1f);
		((Transform)finalPreview.RectTransform).localScale = Vector3.one;
		((Transform)finalPreview.RectTransform).localRotation = Quaternion.identity;
		finalPreview.SetLayoutIgnored(ignored: true);
		Object.Destroy((Object)(object)overlayRoot);
	}

	private static void PrepareMorphFace(PrototypeCardView view, float alpha)
	{
		view.SetInteractable(interactable: false);
		view.SetAlpha(alpha);
		view.SetLayoutIgnored(ignored: true);
		RectTransform rectTransform = view.RectTransform;
		Stretch(rectTransform);
		((Transform)rectTransform).localRotation = Quaternion.identity;
		((Transform)rectTransform).localScale = Vector3.one;
	}

	private Vector2 WorldToSafeAreaPosition(Vector3 worldPosition)
	{
		Vector3 localPosition = ((Transform)safeAreaRoot).InverseTransformPoint(worldPosition);
		return localPosition;
	}

	private Vector2 RectCenterInSafeArea(RectTransform rect)
	{
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		rect.GetWorldCorners(array);
		Vector3 val = (array[0] + array[2]) * 0.5f;
		Vector3 localCenter = ((Transform)safeAreaRoot).InverseTransformPoint(val);
		return localCenter;
	}

	private Vector2 RectSizeInSafeArea(RectTransform rect)
	{
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		rect.GetWorldCorners(array);
		Vector2 bottomLeft = ((Transform)safeAreaRoot).InverseTransformPoint(array[0]);
		Vector2 topLeft = ((Transform)safeAreaRoot).InverseTransformPoint(array[1]);
		Vector2 topRight = ((Transform)safeAreaRoot).InverseTransformPoint(array[2]);
		float width = Vector2.Distance(topLeft, topRight);
		float height = Vector2.Distance(bottomLeft, topLeft);
		return new Vector2(width, height);
	}

	private void CancelPendingAction()
	{
		if (pvpPresentationActive && pvpState != null && pvpState.Phase == PvpClientPhase.DecisiveSelection)
		{
			TryClearPvpDecisiveSelection();
			return;
		}
		if (pendingDeploymentIndex >= 0)
		{
			pendingDeploymentIndex = -1;
			foreach (PrototypeCardView draftView in draftViews)
			{
				bool flag = selectedDraftCards.Contains(draftViews.IndexOf(draftView));
				draftView.SetSelected(selected: false);
				draftView.SetInteractable(!flag);
			}
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			RefreshCardActionOverlays();
			ProcessNextDeploymentToken();
		}
		else if (pendingAbilityUser != null)
		{
			BattleCardState battleCardState = pendingAbilityUser;
			pendingAbilityUser = null;
			activeAttachmentSource = null;
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			SetMessage("Abilita annullata: " + battleCardState.Card.Name + " non usa nulla.");
			RefreshAbilityButton(battleCardState);
			RefreshAttachmentButton(battleCardState);
			UpdateInteractions();
		}
		else if (attackTargetingActive)
		{
			attackTargetingActive = false;
			((Component)cancelActionButton).gameObject.SetActive(false);
			ClearTargetHints();
			if (selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
			{
				BattleCardState battleCardState2 = playerCards[selectedPlayerIndex];
				if (battleCardState2.Card.HeroClass == HeroClass.Warrior && battleCardState2.AbilityArmed && !battleCardState2.AbilityUsed)
				{
					battleCardState2.AbilityArmed = false;
					RefreshPersistentStatus(battleCardState2);
				}
				SetMessage("Attacco annullato: " + battleCardState2.Card.Name + " torna alla scelta dell'azione.");
			}
			RefreshCardActionOverlays();
			UpdateInteractions();
		}
		else if (activeAttachmentSource != null)
		{
			BattleCardState battleCardState3 = activeAttachmentSource;
			activeAttachmentSource = null;
			abilityTargetMode = AbilityTargetMode.None;
			((Component)cancelActionButton).gameObject.SetActive(false);
			SetMessage("Attachment annullato: " + battleCardState3.Card.Name + " torna alla scelta del bersaglio.");
			SetActiveTurnAura(battleCardState3);
			RefreshAttachmentButton(battleCardState3);
			UpdateInteractions();
		}
		else if (activeAbilityUser != null || abilityTargetMode != AbilityTargetMode.None)
		{
			BattleCardState battleCardState4 = activeAbilityUser;
			attackTargetingActive = false;
			if (battleCardState4 != null)
			{
				battleCardState4.AbilityArmed = false;
				battleCardState4.ProtectedAlly = null;
				RefreshPersistentStatus(battleCardState4);
			}
			activeAbilityUser = null;
			abilityTargetMode = AbilityTargetMode.None;
			((Component)cancelActionButton).gameObject.SetActive(false);
			if (battleCardState4 != null)
			{
				SetMessage("Abilita annullata: " + battleCardState4.Card.Name + " torna alla scelta del bersaglio.");
				RefreshAbilityButton(battleCardState4);
			}
			UpdateInteractions();
		}
		else if (selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState5 = playerCards[selectedPlayerIndex];
			if (battleCardState5.AbilityArmed && !battleCardState5.AbilityUsed)
			{
				battleCardState5.AbilityArmed = false;
				battleCardState5.ProtectedAlly = null;
				RefreshPersistentStatus(battleCardState5);
				((Component)cancelActionButton).gameObject.SetActive(false);
				SetMessage("Abilita annullata: " + battleCardState5.Card.Name + " torna alla scelta del bersaglio.");
				RefreshAbilityButton(battleCardState5);
				UpdateInteractions();
			}
		}
	}

	private void HandleConfirmAction()
	{
		if (pvpPresentationActive && pvpState != null && pvpState.Phase == PvpClientPhase.DecisiveSelection)
		{
			TryConfirmPvpDecisiveSelection();
			return;
		}
		if (pendingDeploymentIndex >= 0)
		{
			ConfirmPendingDeployment();
			return;
		}
		if (pendingAbilityUser != null)
		{
			ConfirmPendingAbility();
			return;
		}
		ConfirmDraftSelection();
	}

	private void ConfirmDraftSelection()
	{
		if (!draftActive
			|| deploymentDraftActive
			|| selectedDraftCards.Count != configuration.Gameplay.FormationSize)
		{
			return;
		}
		NotifyAdventureTutorial(AdventureTutorialAction.DraftConfirmed);
		List<CardDefinition> list = new List<CardDefinition>();
		IEnumerable<int> enumerable2;
		if (!deploymentInitiativesReady)
		{
			IEnumerable<int> enumerable = selectedDraftCards.OrderBy((int index) => index);
			enumerable2 = enumerable;
		}
		else
		{
			IEnumerable<int> enumerable = selectedPlayerDeploymentIndices;
			enumerable2 = enumerable;
		}
		IEnumerable<int> enumerable3 = enumerable2;
		foreach (int item in enumerable3)
		{
			list.Add(draftCandidates[item]);
		}
		List<CampaignCardInstance> list2 = new List<CampaignCardInstance>();
		if (campaignDeck != null)
		{
			foreach (int item2 in enumerable3)
			{
				list2.Add(draftCampaignCards[item2]);
			}
		}
		playerReserve.Clear();
		for (int num = 0; num < draftCandidates.Count; num++)
		{
			if (!selectedDraftCards.Contains(num))
			{
				playerReserve.Add(draftCandidates[num]);
			}
		}
		initialPlayerReserve.Clear();
		initialPlayerReserve.AddRange(playerReserve);
		initialPlayerFormation.Clear();
		initialPlayerFormation.AddRange(list);
		initialPlayerCampaignFormation.Clear();
		initialPlayerCampaignFormation.AddRange(list2);
		if (campaignDeck != null)
		{
			foreach (int item3 in enumerable3)
			{
				campaignDeck.Deploy(draftCampaignCards[item3]);
			}
			campaignDeck.ReturnHandToDeck();
		}
		foreach (PrototypeCardView draftView in draftViews)
		{
			Object.Destroy((Object)(object)((Component)draftView).gameObject);
		}
		draftViews.Clear();
		foreach (PrototypeCardView playerDeploymentPreviewView in playerDeploymentPreviewViews)
		{
			if ((Object)(object)playerDeploymentPreviewView != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)playerDeploymentPreviewView).gameObject);
			}
		}
		playerDeploymentPreviewViews.Clear();
		draftCandidates.Clear();
		draftCampaignCards.Clear();
		selectedDraftCards.Clear();
		draftActive = false;
		((Component)confirmActionButton).gameObject.SetActive(false);
		playerTitleText.text = ((campaignDeck != null) ?string.Empty : "LA TUA FORMAZIONE");
		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			BattleCardState battleCardState = AddCard(playerCards, playerRow, list[num2], belongsToPlayer: true, num2, (num2 < list2.Count) ?list2[num2] : null);
			if (battleCardState != null && deploymentInitiativesReady && num2 < selectedPlayerDeploymentInitiatives.Count)
			{
				battleCardState.Initiative = selectedPlayerDeploymentInitiatives[num2];
			}
			else if (battleCardState == null)
			{
				AppendLog($"SCHIERAMENTO - impossibile creare la pedina player per {list[num2]?.DisplayName ?? "carta sconosciuta"}.");
			}
		}
		List<CardDefinition> list3 = deploymentInitiativesReady
				?new List<CardDefinition>(selectedCpuDeploymentCards)
				:BuildCpuFormationForCurrentCombat();
			initialCpuFormation.Clear();
			initialCpuFormation.AddRange(list3);
			survivingCpuFormation.Clear();
			for (int num3 = 0; num3 < list3.Count; num3++)
			{
				BattleCardState battleCardState2 = AddCard(cpuCards, cpuRow, list3[num3], belongsToPlayer: false, num3);
				if (battleCardState2 != null && deploymentInitiativesReady && num3 < selectedCpuDeploymentInitiatives.Count)
				{
					battleCardState2.Initiative = selectedCpuDeploymentInitiatives[num3];
				}
				else if (battleCardState2 == null)
				{
					AppendLog($"SCHIERAMENTO - impossibile creare la pedina CPU per {list3[num3]?.DisplayName ?? "carta sconosciuta"}.");
				}
			}
			bool animatePlayerRowToBattlePosition = deploymentInitiativesReady && (Object)(object)playerRow != (Object)null;
			Vector2 playerRowStartAnchorMin = animatePlayerRowToBattlePosition ?playerRow.anchorMin : Vector2.zero;
			Vector2 playerRowStartAnchorMax = animatePlayerRowToBattlePosition ?playerRow.anchorMax : Vector2.zero;
			Vector2 playerRowStartSize = animatePlayerRowToBattlePosition ?playerRow.sizeDelta : Vector2.zero;
			Vector2 playerRowStartPosition = animatePlayerRowToBattlePosition ?playerRow.anchoredPosition : Vector2.zero;
			ApplyResponsiveLayout();
			if (animatePlayerRowToBattlePosition)
			{
				StartPlayerBattlefieldRowTransition(
					playerRowStartAnchorMin,
					playerRowStartAnchorMax,
					playerRowStartSize,
					playerRowStartPosition);
			}
			RestoreBattlefieldCardVisibility();
			StartBattle();
	}

	private void FinalizeDeploymentAndStartBattle()
	{
		if (!draftActive || selectedPlayerDeploymentIndices.Count != configuration.Gameplay.FormationSize)
		{
			AppendLog("SCHIERAMENTO - impossibile iniziare: formazione player incompleta.");
			return;
		}

		List<CardDefinition> playerFormation = new List<CardDefinition>();
		List<CampaignCardInstance> campaignFormation = new List<CampaignCardInstance>();
		foreach (int index in selectedPlayerDeploymentIndices)
		{
			if (index < 0 || index >= draftCandidates.Count)
				continue;

			playerFormation.Add(draftCandidates[index]);
			if (campaignDeck != null && index < draftCampaignCards.Count)
				campaignFormation.Add(draftCampaignCards[index]);
		}

		if (playerFormation.Count != configuration.Gameplay.FormationSize)
		{
			AppendLog("SCHIERAMENTO - impossibile iniziare: indici player non validi.");
			return;
		}

		playerReserve.Clear();
		for (int index = 0; index < draftCandidates.Count; index++)
		{
			if (!selectedDraftCards.Contains(index))
				playerReserve.Add(draftCandidates[index]);
		}
		initialPlayerReserve.Clear();
		initialPlayerReserve.AddRange(playerReserve);
		initialPlayerFormation.Clear();
		initialPlayerFormation.AddRange(playerFormation);
		initialPlayerCampaignFormation.Clear();
		initialPlayerCampaignFormation.AddRange(campaignFormation);

		if (campaignDeck != null)
		{
			foreach (int index in selectedPlayerDeploymentIndices)
			{
				if (index >= 0 && index < draftCampaignCards.Count)
					campaignDeck.Deploy(draftCampaignCards[index]);
			}
			campaignDeck.ReturnHandToDeck();
		}

		DestroyPrototypeViews(draftViews);
		draftViews.Clear();
		DestroyPrototypeViews(playerDeploymentPreviewViews);
		playerDeploymentPreviewViews.Clear();
		draftCandidates.Clear();
		draftCampaignCards.Clear();
		selectedDraftCards.Clear();
		draftActive = false;
		((Component)confirmActionButton).gameObject.SetActive(false);
		playerTitleText.text = campaignDeck != null ?string.Empty : "LA TUA FORMAZIONE";

		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);

		for (int index = 0; index < playerFormation.Count; index++)
		{
			BattleCardState state = AddCard(
				playerCards,
				playerRow,
				playerFormation[index],
				belongsToPlayer: true,
				index,
				index < campaignFormation.Count ?campaignFormation[index] : null);
			if (state != null && index < selectedPlayerDeploymentInitiatives.Count)
				state.Initiative = selectedPlayerDeploymentInitiatives[index];
		}

		List<CardDefinition> cpuFormation = new List<CardDefinition>(selectedCpuDeploymentCards);
		initialCpuFormation.Clear();
		initialCpuFormation.AddRange(cpuFormation);
		survivingCpuFormation.Clear();
		for (int index = 0; index < cpuFormation.Count; index++)
		{
			BattleCardState state = AddCard(cpuCards, cpuRow, cpuFormation[index], belongsToPlayer: false, index);
			if (state != null && index < selectedCpuDeploymentInitiatives.Count)
				state.Initiative = selectedCpuDeploymentInitiatives[index];
		}

		bool animatePlayerRowToBattlePosition = (Object)(object)playerRow != (Object)null;
		Vector2 playerRowStartAnchorMin = animatePlayerRowToBattlePosition ?playerRow.anchorMin : Vector2.zero;
		Vector2 playerRowStartAnchorMax = animatePlayerRowToBattlePosition ?playerRow.anchorMax : Vector2.zero;
		Vector2 playerRowStartSize = animatePlayerRowToBattlePosition ?playerRow.sizeDelta : Vector2.zero;
		Vector2 playerRowStartPosition = animatePlayerRowToBattlePosition ?playerRow.anchoredPosition : Vector2.zero;
		ApplyResponsiveLayout();
		if (animatePlayerRowToBattlePosition)
		{
			StartPlayerBattlefieldRowTransition(
				playerRowStartAnchorMin,
				playerRowStartAnchorMax,
				playerRowStartSize,
				playerRowStartPosition);
		}
		RestoreBattlefieldCardVisibility();
		StartBattle();
	}

	private void StartPlayerBattlefieldRowTransition(Vector2 startAnchorMin, Vector2 startAnchorMax, Vector2 startSize, Vector2 startPosition)
	{
		if ((Object)(object)playerRow == (Object)null)
		{
			return;
		}
		if (playerBattlefieldRowTransitionCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(playerBattlefieldRowTransitionCoroutine);
		}
		Vector2 targetAnchorMin = playerRow.anchorMin;
		Vector2 targetAnchorMax = playerRow.anchorMax;
		Vector2 targetSize = playerRow.sizeDelta;
		Vector2 targetPosition = playerRow.anchoredPosition;
		if (Vector2.Distance(startAnchorMin, targetAnchorMin) < 0.001f && Vector2.Distance(startAnchorMax, targetAnchorMax) < 0.001f)
		{
			playerBattlefieldRowTransitionCoroutine = null;
			return;
		}
		playerRow.anchorMin = startAnchorMin;
		playerRow.anchorMax = startAnchorMax;
		playerRow.sizeDelta = startSize;
		playerRow.anchoredPosition = startPosition;
		playerBattlefieldRowTransitionCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayPlayerBattlefieldRowTransition(
			targetAnchorMin,
			targetAnchorMax,
			targetSize,
			targetPosition));
	}

	private void StopPlayerBattlefieldRowTransition()
	{
		if (playerBattlefieldRowTransitionCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(playerBattlefieldRowTransitionCoroutine);
			playerBattlefieldRowTransitionCoroutine = null;
		}
	}

	private IEnumerator PlayPlayerBattlefieldRowTransition(Vector2 targetAnchorMin, Vector2 targetAnchorMax, Vector2 targetSize, Vector2 targetPosition)
	{
		float duration = Mathf.Clamp(configuration.Animation.CardDeployDuration * 0.55f, 0.22f, 0.42f);
		Vector2 startAnchorMin = playerRow.anchorMin;
		Vector2 startAnchorMax = playerRow.anchorMax;
		Vector2 startSize = playerRow.sizeDelta;
		Vector2 startPosition = playerRow.anchoredPosition;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			if ((Object)(object)playerRow == (Object)null)
			{
				playerBattlefieldRowTransitionCoroutine = null;
				yield break;
			}
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			playerRow.anchorMin = Vector2.LerpUnclamped(startAnchorMin, targetAnchorMin, eased);
			playerRow.anchorMax = Vector2.LerpUnclamped(startAnchorMax, targetAnchorMax, eased);
			playerRow.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, eased);
			playerRow.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
			yield return null;
		}
		playerRow.anchorMin = targetAnchorMin;
		playerRow.anchorMax = targetAnchorMax;
		playerRow.sizeDelta = targetSize;
		playerRow.anchoredPosition = targetPosition;
		playerBattlefieldRowTransitionCoroutine = null;
	}

	private static void ClearCardRowChildren(RectTransform row)
	{
		if ((Object)(object)row == (Object)null)
		{
			return;
		}
		for (int index = row.childCount - 1; index >= 0; index--)
		{
			GameObject child = ((Component)row.GetChild(index)).gameObject;
			child.SetActive(false);
			Object.Destroy((Object)(object)child);
		}
	}

	private void RestoreBattlefieldCardVisibility()
	{
		RestoreBattlefieldCardVisibility(playerCards);
		RestoreBattlefieldCardVisibility(cpuCards);
		foreach (PrototypeCardView playerDeploymentPreviewView in playerDeploymentPreviewViews)
		{
			RestoreBattlefieldPreviewVisibility(playerDeploymentPreviewView);
		}
		foreach (PrototypeCardView cpuDeploymentPreviewView in cpuDeploymentPreviewViews)
		{
			RestoreBattlefieldPreviewVisibility(cpuDeploymentPreviewView);
		}
	}

	private static void RestoreBattlefieldCardVisibility(IEnumerable<BattleCardState> cards)
	{
		foreach (BattleCardState card in cards)
		{
			RestoreBattlefieldPreviewVisibility(card?.View);
		}
	}

	private static void RestoreBattlefieldPreviewVisibility(PrototypeCardView view)
	{
		if (!((Object)(object)view == (Object)null))
		{
			((Component)view).gameObject.SetActive(true);
			view.SetAlpha(1f);
			((Transform)view.RectTransform).localScale = Vector3.one;
			((Transform)view.RectTransform).localRotation = Quaternion.identity;
			view.SetLayoutIgnored(ignored: true);
		}
	}
}
}
