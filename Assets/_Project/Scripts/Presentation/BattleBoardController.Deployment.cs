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
		ResetRoundAuraUsage();
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
		((Component)confirmFormationButton).gameObject.SetActive(!flag);
		if ((Object)(object)confirmFormationButtonText != (Object)null)
		{
			confirmFormationButtonText.text = "CONFERMA FORMAZIONE";
		}
		confirmFormationButton.interactable = false;
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
		draftEntranceAnimatingViews.Clear();
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
			yield return (object)new WaitForSecondsRealtime(initialDelay);
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
				yield return (object)new WaitForSecondsRealtime(holdDuration);
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
				yield return (object)new WaitForSecondsRealtime(betweenCardsDelay);
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
					draftViews[i].SetSelected(i == pendingDeploymentIndex);
					draftViews[i].SetInteractable(!flag);
				}
				if ((Object)(object)confirmFormationButtonText != (Object)null)
				{
					confirmFormationButtonText.text = "OK";
				}
				((Component)confirmFormationButton).gameObject.SetActive(true);
				confirmFormationButton.interactable = true;
				((Component)cancelActionButton).gameObject.SetActive(true);
				RefreshCardActionOverlays();
				SetMessage($"INIZIATIVA {deploymentToken.Initiative}: confermi {draftCandidates[index].DisplayName} in campo?");
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
			draftViews[j].SetSelected(selectedDraftCards.Contains(j));
		}
		confirmFormationButton.interactable = selectedDraftCards.Count == configuration.Gameplay.FormationSize;
		SetMessage($"Formazione: {selectedDraftCards.Count}/{configuration.Gameplay.FormationSize} carte selezionate.");
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
		int num = ((survivingCpuFormation.Count > 0) ?survivingCpuFormation.Count : ((currentRoomType == RoomType.Boss) ?configuration.Progression.BossFormationSize : formationSize));
		int initiativeDieSides = configuration.Gameplay.InitiativeDieSides;
		HashSet<int> usedInitiatives = new HashSet<int>();
		for (int i = 0; i < formationSize; i++)
		{
			deploymentOrder.Add(new DeploymentToken(belongsToPlayer: true, RollUniqueInitiative(initiativeDieSides, usedInitiatives), random.NextInclusive(1, 10000)));
		}
		for (int j = 0; j < num; j++)
		{
			deploymentOrder.Add(new DeploymentToken(belongsToPlayer: false, RollUniqueInitiative(initiativeDieSides, usedInitiatives), random.NextInclusive(1, 10000)));
		}
		deploymentOrder.Sort(delegate(DeploymentToken left, DeploymentToken right)
		{
			int num3 = left.Initiative.CompareTo(right.Initiative);
			return (num3 == 0) ?left.TieBreaker.CompareTo(right.TieBreaker) : num3;
		});
		int num2 = (from card in cardDatabase.Cards
			where (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat
			select card.Id into id
			where !string.IsNullOrWhiteSpace(id)
			select id).Distinct().Count();
		cpuDeploymentHand.Clear();
		if (survivingCpuFormation.Count > 0)
		{
			cpuDeploymentHand.AddRange(survivingCpuFormation);
		}
		else if (currentRoomType == RoomType.Boss)
		{
			cpuDeploymentHand.AddRange(DrawBossFormationForCurrentCombat());
		}
		else
		{
			cpuDeploymentHand.AddRange(formationDraftService.DrawCandidates(cardDatabase.Cards, Mathf.Min(configuration.DeckBuilding.CombatHandSize, num2)));
		}
		currentDeploymentIndex = 0;
		foreach (DeploymentToken item in deploymentOrder)
		{
			AppendLog(string.Format("INIZIATIVA SCHIERAMENTO {0} - D{1} = {2}", item.BelongsToPlayer ?"TU" : "CPU", initiativeDieSides, item.Initiative));
		}
		SetTurnBanner(playerTurn: true, "SCHIERAMENTO  -  DAL PIU BASSO AL PIU ALTO");
		RefreshInitiativeDisplay();
		RefreshDeploymentTimeline();
		SetMessage("Iniziative di schieramento: i valori piu bassi calano per primi.");
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceResultHold);
		ProcessNextDeploymentToken();
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
		if ((Object)(object)confirmFormationButton != (Object)null)
		{
			((Component)confirmFormationButton).gameObject.SetActive(false);
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
			ConfirmFormation();
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
			SetMessage($"INIZIATIVA {deploymentToken.Initiative}: scegli una carta dalla tua mano da schierare.");
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
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
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
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuCardRevealDuration);
		currentDeploymentIndex++;
		ProcessNextDeploymentToken();
	}

	private IEnumerator ContinueDeploymentAfterDelay(float delay)
	{
		yield return (object)new WaitForSecondsRealtime(delay);
		ProcessNextDeploymentToken();
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
			for (int num = ((Transform)initiativeTimelineRoot).childCount - 1; num >= 0; num--)
			{
				Object.Destroy((Object)(object)((Component)((Transform)initiativeTimelineRoot).GetChild(num)).gameObject);
			}
			Font builtinResource = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			float timelineTileSize = GetTimelineTileSize();
			for (int i = 0; i < deploymentOrder.Count; i++)
			{
				DeploymentToken deploymentToken = deploymentOrder[i];
				bool flag = i == currentDeploymentIndex;
				Image image = CreateImage(deploymentToken.BelongsToPlayer ?"Deploy TU" : "Deploy CPU", (Transform)(object)initiativeTimelineRoot, flag ?new Color(0.72f, 0.48f, 0.12f, 0.98f) : (deploymentToken.BelongsToPlayer ?new Color(0.04f, 0.42f, 0.48f, 0.95f) : new Color(0.5f, 0.1f, 0.12f, 0.95f)));
				LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
				layoutElement.minWidth = timelineTileSize;
				layoutElement.preferredWidth = timelineTileSize;
				layoutElement.flexibleWidth = 0f;
				Text text = CreateText("Token", ((Component)image).transform, builtinResource, 18, (FontStyle)1, (TextAnchor)4);
				text.text = string.Format("{0}\n{1}", deploymentToken.BelongsToPlayer ?"TU" : "CPU", deploymentToken.Initiative);
				Stretch(text.rectTransform, 2f);
			}
		}
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
		PrototypeCardView prototypeCardView2 = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)playerRow, draftCandidates[num], configuration);
		MakeDeploymentPreviewInspectable(prototypeCardView2, draftCandidates[num]);
		prototypeCardView2.SetSelected(selected: true);
		prototypeCardView2.SetAlpha(0f);
		playerDeploymentPreviewViews.Add(prototypeCardView2);
		prototypeCardView.SetSelected(selected: false);
		prototypeCardView.SetInteractable(interactable: false);
		prototypeCardView.SetAlpha(0f);
		prototypeCardView.SetLayoutIgnored(ignored: true);
		foreach (PrototypeCardView draftView in draftViews)
		{
			draftView.SetInteractable(interactable: false);
		}
		((Component)confirmFormationButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		RefreshCardActionOverlays();
		AppendLog($"SCHIERAMENTO TU - {draftCandidates[num].DisplayName}, iniziativa {deploymentToken.Initiative}");
		inputLocked = true;
		ApplyResponsiveLayout();
		Canvas.ForceUpdateCanvases();
		PlayPawnEnteringBattlefieldSfx(draftCandidates[num]);
		((MonoBehaviour)this).StartCoroutine(PlayDeploymentMorph(draftCandidates[num], position, rotation, startSize, prototypeCardView2, configuration.Animation.CardDeployDuration));
		currentDeploymentIndex++;
		((MonoBehaviour)this).StartCoroutine(ContinueDeploymentAfterDelay(configuration.Animation.CardDeployDuration));
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
		if (pendingDeploymentIndex >= 0)
		{
			pendingDeploymentIndex = -1;
			foreach (PrototypeCardView draftView in draftViews)
			{
				bool flag = selectedDraftCards.Contains(draftViews.IndexOf(draftView));
				draftView.SetSelected(selected: false);
				draftView.SetInteractable(!flag);
			}
			((Component)confirmFormationButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			RefreshCardActionOverlays();
			ProcessNextDeploymentToken();
		}
		else if (pendingAbilityUser != null)
		{
			BattleCardState battleCardState = pendingAbilityUser;
			pendingAbilityUser = null;
			activeAttachmentSource = null;
			((Component)confirmFormationButton).gameObject.SetActive(false);
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

	private void ConfirmFormation()
	{
		if (pendingDeploymentIndex >= 0)
		{
			ConfirmPendingDeployment();
		}
		else if (pendingAbilityUser != null)
		{
			ConfirmPendingAbility();
		}
		else
		{
			if (!draftActive || selectedDraftCards.Count != configuration.Gameplay.FormationSize)
			{
				return;
			}
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
			((Component)confirmFormationButton).gameObject.SetActive(false);
			playerTitleText.text = ((campaignDeck != null) ?string.Empty : "LA TUA FORMAZIONE");
			DestroyCardViews(playerCards);
			DestroyCardViews(cpuCards);
			ClearCardRowChildren(playerRow);
			ClearCardRowChildren(cpuRow);
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				BattleCardState battleCardState = AddCard(playerCards, playerRow, list[num2], belongsToPlayer: true, num2, (num2 < list2.Count) ?list2[num2] : null);
				if (deploymentInitiativesReady && num2 < selectedPlayerDeploymentInitiatives.Count)
				{
					battleCardState.Initiative = selectedPlayerDeploymentInitiatives[num2];
				}
			}
			List<CardDefinition> list3 = (deploymentInitiativesReady ?new List<CardDefinition>(selectedCpuDeploymentCards) : ((currentRoomType == RoomType.Boss) ?DrawBossFormationForCurrentCombat() : DrawMonsterFormationForCurrentCombat()));
			initialCpuFormation.Clear();
			initialCpuFormation.AddRange(list3);
			survivingCpuFormation.Clear();
			for (int num3 = 0; num3 < list3.Count; num3++)
			{
				BattleCardState battleCardState2 = AddCard(cpuCards, cpuRow, list3[num3], belongsToPlayer: false, num3);
				if (deploymentInitiativesReady && num3 < selectedCpuDeploymentInitiatives.Count)
				{
					battleCardState2.Initiative = selectedCpuDeploymentInitiatives[num3];
				}
			}
			ApplyResponsiveLayout();
			RestoreBattlefieldCardVisibility();
			StartBattle();
		}
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
