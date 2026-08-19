using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
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
	private Vector2 playerRowTransitionTargetAnchorMin;
	private Vector2 playerRowTransitionTargetAnchorMax;
	private Vector2 playerRowTransitionTargetSize;
	private Vector2 playerRowTransitionTargetPosition;
	private bool playerRowTransitionRetargeted;
	private int playerRowTransitionFrame = -1;
	private Coroutine battlefieldPawnGlideCoroutine;

	/// <summary>
	/// Il morph della carta che diventa pedina. Chi fa cominciare la battaglia
	/// deve aspettarlo: il suo tempo e' nominale, ma il frame che lo crea non
	/// viene contato e sull'ultima carta lo sfora quasi sempre.
	/// </summary>
	private Coroutine deploymentMorphCoroutine;
	private int deploymentMorphFrame = -1;

	private void BeginFormationDraft()
	{
		waitingForCampaignBossReveal = !pvpPresentationActive && currentRoomType == RoomType.Boss;
		// La moneta del turno compare solo quando tutte le iniziative hanno
		// terminato il volo nella timeline.
		SetTurnCoinSuppressed(suppressed: true);
		ClearDraftEntranceState();
		SetCombatChromeVisible(visible: true);
		draftActive = true;
		inputLocked = true;
		selectedDraftCards.Clear();
		selectedPlayerDeploymentIndices.Clear();
		selectedCpuDeploymentCards.Clear();
		selectedPlayerDeploymentInitiatives.Clear();
		selectedPlayerDeploymentTokens.Clear();
		selectedCpuDeploymentInitiatives.Clear();
		selectedCpuDeploymentTokens.Clear();
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
		if (adventureScriptedTutorialActive)
		{
			List<CardDefinition> tutorialHand = BuildTutorialPlayerDeck();
			List<CampaignCardRestoreEntry> entries = new List<CampaignCardRestoreEntry>();
			for (int i = 0; i < tutorialHand.Count; i++)
			{
				CardDefinition card = tutorialHand[i];
				entries.Add(new CampaignCardRestoreEntry(card, CampaignCardZone.Hand, i + 1));
				draftCandidates.Add(card);
			}
			campaignDeck?.RestoreFrom(entries, tutorialHand.Count + 1);
			if (campaignDeck != null)
			{
				draftCampaignCards.AddRange(campaignDeck.Cards);
			}
			if (draftCandidates.Count < configuration.Gameplay.FormationSize)
			{
				draftActive = false;
				inputLocked = true;
				SetMessage("Tutorial non disponibile: mano scriptata incompleta.");
				EndAdventureScriptedTutorial(complete: false);
				return;
			}
		}
		else if (campaignDeck != null)
		{
			draftCampaignCards.AddRange(campaignDeck.DrawCombatHand(
				random,
				configuration.DeckBuilding.CombatHandSize,
				configuration.Gameplay.FormationSize));
			draftCandidates.AddRange(draftCampaignCards.Select((CampaignCardInstance card) => card.Definition));
			if (draftCandidates.Count < configuration.Gameplay.FormationSize)
			{
				draftActive = false;
				inputLocked = true;
				gameFinished = true;
				SetTurnBanner(
					playerTurn: false,
					GameText.Get(GameTextKeys.Campaign.DeckExhaustedBanner),
					defeat: true,
					campaignEnded: true);
				SetMessage(GameText.Format(GameTextKeys.Campaign.NotEnoughCards, draftCandidates.Count, configuration.Gameplay.FormationSize, campaignDeck.GraveyardCount));
				pendingCampaignRewardTask = ClaimCampaignRunAccountReward(completed: false);
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
				if (DeploymentHandSwipeSelector.ShouldSuppressClick(prototypeCardView.Button))
				{
					return;
				}
				// Il selettore swipe sopprime il click quando ha gia' gestito il
				// rilascio. Se invece il gesto non viene riconosciuto, il Button
				// resta un percorso di fallback valido anche nello schieramento.
				ToggleDraftCard(capturedIndex);
			});
			prototypeCardView.ClearDragHandlers();
			ConfigureCampaignDeploymentHandSwipe(prototypeCardView, capturedIndex);
			// La mano resta completamente non interagibile finche' l'animazione di
			// ingresso non e' terminata e lo stato di gioco non abilita la scelta.
			prototypeCardView.SetInteractable(interactable: false);
			prototypeCardView.SetAlpha(0f);
			draftViews.Add(prototypeCardView);
		}
		if ((Object)(object)playerTitleText != (Object)null)
		{
			playerTitleText.text = GameText.Format(GameTextKeys.Campaign.ChooseFormationCards, configuration.Gameplay.FormationSize);
		}
		bool flag = campaignDeck != null && (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss);
		if (flag)
		{
			if ((Object)(object)playerTitleText != (Object)null)
			{
				playerTitleText.text = string.Empty;
			}
		}
		((Component)confirmActionButton).gameObject.SetActive(!flag);
		if ((Object)(object)confirmActionButtonText != (Object)null)
		{
			confirmActionButtonText.text = GameText.Get(GameTextKeys.Common.Confirm);
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
		draftEntranceAnimationVersion++;
		activeDraftEntranceCards = 0;
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
		int animationVersion = ++draftEntranceAnimationVersion;
		activeDraftEntranceCards = 0;
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
			activeDraftEntranceCards++;
			((MonoBehaviour)this).StartCoroutine(AnimateDraftEntranceCard(realView, targets[i], targetRotations[i], sizes[i], safeBounds, enterDuration, holdDuration, settleDuration, entranceScale, animationVersion));
			if (betweenCardsDelay > 0f && i < count - 1)
			{
				yield return WaitForCardInspectionPause(enterDuration + holdDuration + betweenCardsDelay);
			}
		}
		while (activeDraftEntranceCards > 0 && animationVersion == draftEntranceAnimationVersion)
		{
			yield return null;
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

	private IEnumerator AnimateDraftEntranceCard(PrototypeCardView realView, Vector2 target, Quaternion targetRotation,
		Vector2 size, Rect safeBounds, float enterDuration, float holdDuration, float settleDuration,
		float entranceScale, int animationVersion)
	{
		GameObject overlayObject = Object.Instantiate(((Component)realView).gameObject, (Transform)(object)safeAreaRoot, false);
		overlayObject.name = ((Object)((Component)realView).gameObject).name + "-entrance";
		NormalizeDraftEntranceClone(overlayObject);
		PrototypeCardView overlayView = overlayObject.GetComponent<PrototypeCardView>();
		Button overlayButton = overlayObject.GetComponent<Button>();
		if ((Object)(object)overlayButton != (Object)null) overlayButton.interactable = false;
		overlayView.SetLayoutIgnored(ignored: true);
		overlayView.SetAlpha(0f);
		draftEntranceOverlayObjects.Add(overlayObject);
		RectTransform animatedRect = overlayView.RectTransform;
		animatedRect.anchorMin = animatedRect.anchorMax = animatedRect.pivot = new Vector2(0.5f, 0.5f);
		animatedRect.sizeDelta = size;
		Vector2 start = new Vector2(safeBounds.xMax + Mathf.Max(1f, size.x) * 0.9f, Mathf.Lerp(0f, target.y, 0.35f));
		animatedRect.anchoredPosition = start;
		((Transform)animatedRect).localRotation = Quaternion.identity;
		((Transform)animatedRect).localScale = Vector3.one * 0.82f;
		PlayDrawCardSfx();
		float elapsed = 0f;
		while (elapsed < enterDuration && animationVersion == draftEntranceAnimationVersion)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / enterDuration));
			animatedRect.anchoredPosition = Vector2.LerpUnclamped(start, Vector2.zero, progress);
			((Transform)animatedRect).localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, entranceScale, progress);
			overlayView.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (enterDuration * 0.42f))));
			yield return null;
		}
		if (animationVersion == draftEntranceAnimationVersion)
		{
			animatedRect.anchoredPosition = Vector2.zero;
			((Transform)animatedRect).localScale = Vector3.one * entranceScale;
			overlayView.SetAlpha(1f);
			if (holdDuration > 0f) yield return WaitForCardInspectionPause(holdDuration);
			elapsed = 0f;
			while (elapsed < settleDuration && animationVersion == draftEntranceAnimationVersion)
			{
				elapsed += Time.unscaledDeltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settleDuration));
				animatedRect.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, target, progress);
				((Transform)animatedRect).localRotation = Quaternion.SlerpUnclamped(Quaternion.identity, targetRotation, progress);
				((Transform)animatedRect).localScale = Vector3.one * Mathf.LerpUnclamped(entranceScale, 1f, progress);
				yield return null;
			}
			realView.SetAlpha(1f);
			((Transform)realView.RectTransform).localScale = Vector3.one;
			((Transform)realView.RectTransform).localRotation = targetRotation;
		}
		draftEntranceAnimatingViews.Remove(realView);
		draftEntranceOverlayObjects.Remove(overlayObject);
		if ((Object)(object)overlayObject != (Object)null) Object.Destroy((Object)(object)overlayObject);
		if (animationVersion == draftEntranceAnimationVersion) activeDraftEntranceCards--;
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
		if (!draftActive || inputLocked || index < 0 || index >= draftViews.Count)
		{
			return;
		}
		if (deploymentDraftActive)
		{
			if (inputLocked || selectedDraftCards.Contains(index))
			{
				return;
			}
			if (!IsAdventureTutorialDraftCardAllowed(index))
			{
				return;
			}
			DeploymentToken deploymentToken = deploymentOrder[currentDeploymentIndex];
			if (deploymentToken.BelongsToPlayer)
			{
				pendingDeploymentIndex = index;
				ShowDeploymentMatchupHints(draftCandidates[index]);
				for (int i = 0; i < draftViews.Count; i++)
				{
					bool flag = selectedDraftCards.Contains(i);
					draftViews[i].SetDraftSelected(i == pendingDeploymentIndex);
					draftViews[i].SetInteractable(!flag);
				}
				SetAdventureTutorialDraftInteractivityForConfirmation();
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
		if (adventureScriptedTutorialActive)
		{
			int[] playerInitiatives = { 6, 10, 17 };
			int[] cpuInitiatives = { 1, 5, 8 };
			for (int i = 0; i < formationSize; i++)
			{
				deploymentOrder.Add(new DeploymentToken(belongsToPlayer: true, playerInitiatives[Mathf.Min(i, playerInitiatives.Length - 1)], i));
			}
			for (int j = 0; j < cpuDeploymentCount; j++)
			{
				deploymentOrder.Add(new DeploymentToken(belongsToPlayer: false, cpuInitiatives[Mathf.Min(j, cpuInitiatives.Length - 1)], 100 + j));
			}
		}
		else
		{
			RollDeploymentInitiatives(formationSize, cpuDeploymentCount, initiativeDieSides, usedInitiatives);
			AvoidRepeatedCampaignRetryInitiatives(formationSize, cpuDeploymentCount, initiativeDieSides, usedInitiatives);
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
		SetTurnCoinSuppressed(suppressed: true);
		SetTurnBanner(playerTurn: true, "SCHIERAMENTO");
		RefreshInitiativeDisplay();
		ClearDeploymentTimeline();
		SetMessage($"Tiro iniziativa: {formationSize} D20 per te e {cpuDeploymentCount} D20 per il Master.");
		yield return PlayDeploymentInitiativeDiceRoll(initiativeDieSides);
		RefreshDeploymentTimeline();
		NotifyAdventureTutorial(AdventureTutorialAction.InitiativeRolled);
		SetMessage("Iniziative di schieramento: i valori piu bassi calano per primi.");
		yield return WaitForCardInspectionPause(Mathf.Max(0.2f, configuration.Animation.DiceResultHold * 0.45f));
		if (adventureScriptedTutorialActive && adventureScriptedTutorialStep == 2 && !adventureScriptedTutorialStepAcknowledged)
		{
			yield break;
		}
		ProcessNextDeploymentToken();
	}

	private void RollDeploymentInitiatives(int formationSize, int cpuDeploymentCount, int initiativeDieSides, HashSet<int> usedInitiatives)
	{
		usedInitiatives.Clear();
		deploymentOrder.Clear();
		for (int i = 0; i < formationSize; i++)
		{
			// L'indice del ciclo e' il "1º/2º/3º dado d'iniziativa" dei talenti: qui i
			// tiri sono ancora nell'ordine in cui escono, e questa e' l'unica riga in
			// cui quell'identita' esiste. Subito dopo il sort la perde per sempre.
			DeploymentToken token = new DeploymentToken(belongsToPlayer: true, RollUniqueInitiative(initiativeDieSides, usedInitiatives), random.NextInclusive(1, 10000));
			ApplyInitiativeTalentsToDeploymentToken(token, i);
			deploymentOrder.Add(token);
		}
		for (int i = 0; i < cpuDeploymentCount; i++)
		{
			deploymentOrder.Add(new DeploymentToken(belongsToPlayer: false, RollUniqueInitiative(initiativeDieSides, usedInitiatives), random.NextInclusive(1, 10000)));
		}
	}

	private void AvoidRepeatedCampaignRetryInitiatives(int formationSize, int cpuDeploymentCount, int initiativeDieSides, HashSet<int> usedInitiatives)
	{
		int[] previous = campaignRetryPreviousPlayerInitiatives;
		campaignRetryPreviousPlayerInitiatives = null;
		if (previous == null || previous.Length != formationSize)
		{
			return;
		}

		const int maxRerolls = 32;
		for (int attempt = 0; attempt < maxRerolls && PlayerInitiativesMatch(previous); attempt++)
		{
			RollDeploymentInitiatives(formationSize, cpuDeploymentCount, initiativeDieSides, usedInitiatives);
		}

		if (!PlayerInitiativesMatch(previous))
		{
			return;
		}

		// Garantisce il vincolo anche con una sorgente casuale deterministica che
		// continui a restituire la stessa sequenza (utile anche nei test).
		int playerIndex = deploymentOrder.FindIndex(token => token.BelongsToPlayer);
		if (playerIndex < 0)
		{
			return;
		}
		DeploymentToken playerToken = deploymentOrder[playerIndex];
		usedInitiatives.Remove(playerToken.Initiative);
		for (int initiative = 1; initiative <= initiativeDieSides; initiative++)
		{
			if (initiative != playerToken.Initiative && usedInitiatives.Add(initiative))
			{
				// Cambia il numero, non il dado: il token sostituito e' sempre il
				// primo del giocatore, quindi si porta dietro i suoi talenti.
				DeploymentToken replacement = new DeploymentToken(true, initiative, playerToken.TieBreaker)
				{
					TalentInitiativeBonus = playerToken.TalentInitiativeBonus,
					OpensTheFight = playerToken.OpensTheFight,
				};
				deploymentOrder[playerIndex] = replacement;
				break;
			}
		}
	}

	/// <summary>
	/// Porta in battaglia il dado dello schieramento tutto intero: bonus dei talenti,
	/// "Apertura" e - soprattutto - il tie-breaker con cui la timeline ha gia' sciolto
	/// le parita' davanti al giocatore.
	///
	/// Il tie-breaker e' la parte che sembra un dettaglio e non lo e'. I tiri sono unici,
	/// ma il bonus dei talenti no: un +3 su un 5 pareggia il 8 di chiunque altro, e a
	/// quel punto l'ordine lo decide il tie-breaker. Ritirarlo a battaglia iniziata
	/// significa rigiocarsi a testa o croce due pedine che il giocatore ha appena visto
	/// sistemarsi nella timeline.
	/// </summary>
	private static void ApplyDeploymentTokenToCard(BattleCardState card, IReadOnlyList<DeploymentToken> tokens, int deploymentIndex)
	{
		if (card == null || tokens == null || deploymentIndex < 0 || deploymentIndex >= tokens.Count)
		{
			return;
		}
		DeploymentToken token = tokens[deploymentIndex];
		if (token == null)
		{
			return;
		}
		card.InitiativeTalentBonus = token.TalentInitiativeBonus;
		card.OpensTheFight = token.OpensTheFight;
		card.TieBreaker = token.TieBreaker;
	}

	/// <summary>
	/// Attacca al dado i talenti del ramo Iniziativa. <paramref name="dieSlot"/> e' la
	/// posizione del tiro nella sequenza del giocatore (0 = 1º dado), non la posizione
	/// nella fila: e' quello che il ramo dei talenti chiama "1º/2º/3º dado".
	/// </summary>
	private void ApplyInitiativeTalentsToDeploymentToken(DeploymentToken token, int dieSlot)
	{
		if (token == null || !token.BelongsToPlayer)
		{
			return;
		}
		token.TalentInitiativeBonus = TalentRunModifiers.InitiativeBonus(dieSlot, ActiveTalents);
		token.OpensTheFight = ActiveTalents.opensEveryFight && dieSlot == 0;
	}

	/// <summary>
	/// L'ordine di discesa in campo a talenti applicati: si schiera dal numero piu'
	/// basso, e chi ha "Apertura" scende per ultimo perche' in battaglia agisce per primo.
	/// </summary>
	private static int CompareDeploymentTokensByEffectiveInitiative(DeploymentToken left, DeploymentToken right)
	{
		if (left.OpensTheFight != right.OpensTheFight)
		{
			return left.OpensTheFight ?1 : -1;
		}
		int compared = left.EffectiveInitiative.CompareTo(right.EffectiveInitiative);
		return (compared == 0) ?left.TieBreaker.CompareTo(right.TieBreaker) : compared;
	}

	private bool PlayerInitiativesMatch(int[] previous)
	{
		return deploymentOrder
			.Where(token => token.BelongsToPlayer)
			.Select(token => token.Initiative)
			.OrderBy(initiative => initiative)
			.SequenceEqual(previous);
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

		if (adventureScriptedTutorialActive)
		{
			cpuDeploymentHand.AddRange(BuildTutorialCpuDeploymentHand());
			return;
		}

		RoomDifficultyRules rules = RoomDifficultyRules.For(pendingRoomDifficulty);
		List<CardDefinition> allowedCards = cardDatabase.Cards
			.Where(card => (Object)(object)card != (Object)null
				&& (card.Category != CardCategory.Monster
					|| (card.CanEnterCombat && card.Strength <= rules.MaximumMonsterCardStrength)))
			.ToList();
		int monsterPoolCount = (from card in allowedCards
			where card.Category == CardCategory.Monster && card.CanEnterCombat
			select card.Id into id
			where !string.IsNullOrWhiteSpace(id)
			select id).Distinct().Count();
		cpuDeploymentHand.AddRange(formationDraftService.DrawCandidates(
			allowedCards,
			Mathf.Min(configuration.DeckBuilding.CombatHandSize, monsterPoolCount)));

		int formationSize = configuration.Gameplay.FormationSize;
		List<List<CardDefinition>> validFormations = BuildFormationCandidates(cpuDeploymentHand, formationSize)
			.Where(formation =>
			{
				int power = formation.Sum(card => card.Strength);
				return power >= rules.MinimumFormationPower && power <= rules.MaximumFormationPower;
			})
			.ToList();
		if (validFormations.Count == 0)
		{
			List<CardDefinition> allMonsters = cardDatabase.Cards
				.Where(card => (Object)(object)card != (Object)null
					&& card.Category == CardCategory.Monster
					&& card.CanEnterCombat
					&& card.Strength <= rules.MaximumMonsterCardStrength)
				.ToList();
			validFormations = BuildFormationCandidates(allMonsters, formationSize)
				.Where(formation =>
				{
					int power = formation.Sum(card => card.Strength);
					return power >= rules.MinimumFormationPower && power <= rules.MaximumFormationPower;
				})
				.ToList();
		}
		if (validFormations.Count > 0)
		{
			List<CardDefinition> guaranteedFormation = validFormations[random.NextInclusive(0, validFormations.Count - 1)];
			foreach (CardDefinition card in guaranteedFormation)
			{
				if (!cpuDeploymentHand.Contains(card))
					cpuDeploymentHand.Add(card);
			}
		}
		else
		{
			AppendLog($"SCHIERAMENTO CPU - nessuna combinazione valida per {rules.DisplayName} " +
				$"({rules.MinimumFormationPower}-{rules.MaximumFormationPower}).");
		}
	}

	private void ProcessNextDeploymentToken()
	{
		if (!deploymentDraftActive)
		{
			return;
		}
		pendingDeploymentIndex = -1;
		ClearTargetHints();
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
			if (adventureScriptedTutorialActive)
			{
				NotifyAdventureTutorial(AdventureTutorialAction.DeploymentCompleted);
				return;
			}
			CompleteDeploymentAndStartBattle();
			return;
		}
		DeploymentToken deploymentToken = deploymentOrder[currentDeploymentIndex];
		SetTurnBanner(
			deploymentToken.BelongsToPlayer,
			deploymentToken.BelongsToPlayer
				? "SCHIERAMENTO  -  IL TUO TURNO"
				: "SCHIERAMENTO  -  TURNO CPU");
		RefreshDeploymentTimeline();
		if (deploymentToken.BelongsToPlayer)
		{
			inputLocked = false;
			for (int i = 0; i < draftViews.Count; i++)
			{
				draftViews[i].SetInteractable(!selectedDraftCards.Contains(i));
			}
			SetMessage("Scegli una carta dalla tua mano da schierare.");
			NotifyAdventureTutorial(AdventureTutorialAction.PlayerDeploymentTurnStarted);
			ShowAdventureTutorialDeploymentChoiceSpotlight();
		}
		else
		{
			inputLocked = true;
			SetMessage($"INIZIATIVA CPU {deploymentToken.Initiative}: il Master sceglie una carta...");
			((MonoBehaviour)this).StartCoroutine(ExecuteCpuDeployment(deploymentToken));
		}
	}

	private void CompleteDeploymentAndStartBattle()
	{
		if (!deploymentDraftActive && deploymentInitiativesReady)
		{
			return;
		}
		deploymentDraftActive = false;
		deploymentInitiativesReady = true;
		foreach (PrototypeCardView cpuDeploymentPreviewView in cpuDeploymentPreviewViews)
		{
			Object.Destroy((Object)(object)((Component)cpuDeploymentPreviewView).gameObject);
		}
		cpuDeploymentPreviewViews.Clear();
		FinalizeDeploymentAndStartBattle();
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
		token.DeployedCard = cardDefinition;
		selectedCpuDeploymentCards.Add(cardDefinition);
		selectedCpuDeploymentInitiatives.Add(token.Initiative);
		selectedCpuDeploymentTokens.Add(token);
		cpuDeploymentHand.Remove(cardDefinition);
		bool deployingBragus = IsBragusBossDefinition(cardDefinition);
		bool deployingJurinashor = IsJurinashorBossDefinition(cardDefinition);
		bool deployingTrentor = IsTrentorBossDefinition(cardDefinition);
		bool deployingSeraphel = IsSeraphelBossDefinition(cardDefinition);
		bool deployingComposableGolem = IsComposableGolemDefinition(cardDefinition);
		bool deployingBoss = deployingComposableGolem
			|| IsMedusaBossDefinition(cardDefinition)
			|| deployingBragus
			|| deployingJurinashor
			|| deployingTrentor
			|| IsPalatirBossDefinition(cardDefinition)
			|| deployingSeraphel;
		bool deployingBackdropBoss = deployingBragus || deployingJurinashor || deployingTrentor || deployingSeraphel;
		bool backdropBossDeploysLast = deployingBackdropBoss
			&& currentDeploymentIndex + 1 >= deploymentOrder.Count;
		Dictionary<RectTransform, Vector2> battlefieldPawnPoses = CaptureBattlefieldPawnPoses(cpuRow);
		PrototypeCardView prototypeCardView = null;
		if (deployingBackdropBoss)
		{
			// I boss integrati nello scenario non entrano come carta: la loro
			// apparizione coincide con il cambio di background.
			// La proxy invisibile necessaria al combattimento viene creata dopo il deployment.
		}
		else
		{
			prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)cpuRow, cardDefinition, configuration);
			MakeDeploymentPreviewInspectable(prototypeCardView, cardDefinition);
			cpuDeploymentPreviewViews.Add(prototypeCardView);
		}
		AppendLog($"SCHIERAMENTO CPU - {cardDefinition.DisplayName}, iniziativa {token.Initiative}");
		ApplyResponsiveLayout();
		if (deployingComposableGolem)
		{
			// Il Golem e' una pedina 3D, non un boss dipinto nel fondale.
			// Ripristina sempre lo scenario corrente anche se una precedente sessione
			// aveva lasciato attiva la presentazione di Bragus o Trentor.
			bragusBossPresentationActive = false;
			jurinashorBossPresentationActive = false;
			trentorBossPresentationActive = false;
			seraphelBossPresentationActive = false;
			RefreshScenarioBackground();
		}
		if (deployingBackdropBoss)
		{
			bragusBossPresentationActive = deployingBragus;
			jurinashorBossPresentationActive = deployingJurinashor;
			trentorBossPresentationActive = deployingTrentor;
			seraphelBossPresentationActive = deployingSeraphel;
			if (deployingSeraphel)
				PrepareSeraphelRevealHud();
			if (!backdropBossDeploysLast)
				TransitionToScenarioBackground();
			if (deployingBragus)
				PlayMusic(bossBragusSoundtrack);
		}
		if (deployingBoss && waitingForCampaignBossReveal)
		{
			waitingForCampaignBossReveal = false;
			SetCombatHudRefactorVisible(combatChromeVisible);
			RefreshPlayerHud();
			RefreshCpuHud();
		}
		Canvas.ForceUpdateCanvases();
		StartBattlefieldPawnGlide(battlefieldPawnPoses);
		PlayPawnEnteringBattlefieldSfx(cardDefinition);
		if (prototypeCardView != null)
		{
			prototypeCardView.PlayRevealAnimation(configuration.Animation.CpuCardRevealDuration);
			yield return WaitForCardInspectionPause(Mathf.Min(configuration.Animation.CpuCardRevealDuration, 0.35f));
		}
		currentDeploymentIndex++;
		if (currentDeploymentIndex >= deploymentOrder.Count)
		{
			SetMessage("Schieramento completato: inizia il combattimento.");
		}
		ProcessNextDeploymentToken();
		if (backdropBossDeploysLast)
			((MonoBehaviour)this).StartCoroutine(TransitionToBackdropBossBackgroundAfterPawnLayout());
	}

	private IEnumerator TransitionToBackdropBossBackgroundAfterPawnLayout()
	{
		// Se il boss chiude lo schieramento, il layout di battaglia sposta la fila
		// del giocatore. Aspettiamo che finisca prima di avviare il cambio scenario:
		// le due animazioni nello stesso frame causavano flicker sulle pedine.
		while (playerBattlefieldRowTransitionCoroutine != null)
			yield return null;

		// Separa anche i due commit grafici su frame distinti.
		yield return null;
		TransitionToScenarioBackground();
	}

	private IEnumerator ContinueDeploymentAfterDelay(float delay)
	{
		yield return WaitForCardInspectionPause(delay);
		// Sull'ultima carta il token successivo e' la battaglia, che si porta via
		// la preview e fa scendere la fila. Se il morph e' ancora in volo si
		// ritrova il bersaglio spostato sotto: la pedina si posa in un punto e
		// quella vera e' gia' altrove. Si aspetta che abbia finito davvero.
		while (IsRoutineAlive(deploymentMorphCoroutine, deploymentMorphFrame))
		{
			yield return null;
		}
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
		bool firstFrame = true;
		while (elapsed < duration)
		{
			elapsed += firstFrame ?0f : AnimationDeltaTime();
			firstFrame = false;
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
		RoomDifficultyRules difficultyRules = RoomDifficultyRules.For(pendingRoomDifficulty);
		int selectedPower = selectedCpuDeploymentCards.Sum(card => card.Strength);
		int remainingAfterChoice = Mathf.Max(0, configuration.Gameplay.FormationSize - selectedCpuDeploymentCards.Count - 1);
		List<CardDefinition> legalCards = cpuDeploymentHand.Where(candidate =>
		{
			int powerAfterChoice = selectedPower + candidate.Strength;
			IEnumerable<CardDefinition> remaining = cpuDeploymentHand.Where(card => card != candidate);
			int minimumReachable = powerAfterChoice + remaining.OrderBy(card => card.Strength).Take(remainingAfterChoice).Sum(card => card.Strength);
			int maximumReachable = powerAfterChoice + remaining.OrderByDescending(card => card.Strength).Take(remainingAfterChoice).Sum(card => card.Strength);
			return minimumReachable <= difficultyRules.MaximumFormationPower
				&& maximumReachable >= difficultyRules.MinimumFormationPower;
		}).ToList();
		IReadOnlyList<CardDefinition> candidates = legalCards;
		if (candidates.Count == 0)
		{
			// Nessuna carta in mano tiene la formazione dentro la banda di potenza della
			// stanza: si schiera comunque, ma la deviazione va tracciata perche' e' il
			// sintomo di una mano generata male, non una scelta della CPU.
			candidates = cpuDeploymentHand;
			AppendLog($"SCHIERAMENTO CPU - nessuna carta rispetta la banda {difficultyRules.DisplayName} " +
				$"({difficultyRules.MinimumFormationPower}-{difficultyRules.MaximumFormationPower}): si ripiega sulla mano completa.");
		}
		CardDefinition result = candidates[0];
		int num = int.MinValue;
		foreach (CardDefinition item in candidates)
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
			// I dadi minori sono in fondo alla timeline e il loro turno parte da li'.
			for (int i = deploymentOrder.Count - 1; i >= 0; i--)
			{
				DeploymentToken deploymentToken = deploymentOrder[i];
				bool flag = i == currentDeploymentIndex;
				Image image = CreateImage(deploymentToken.BelongsToPlayer ?"Deploy TU" : "Deploy CPU", (Transform)(object)initiativeTimelineRoot, flag ?new Color(0.72f, 0.48f, 0.12f, 0.98f) : (deploymentToken.BelongsToPlayer ?new Color(0.04f, 0.42f, 0.48f, 0.95f) : new Color(0.5f, 0.1f, 0.12f, 0.95f)));
				LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
				ConfigureTimelineTileLayout(layoutElement, timelineTileSize);
				if (deploymentToken.DeployedCard != null)
				{
					Image portrait = CreateImage("Portrait", ((Component)image).transform, Color.white);
					portrait.sprite = deploymentToken.DeployedCard.Artwork;
					portrait.preserveAspect = false;
					portrait.raycastTarget = false;
					SetRect(portrait.rectTransform, new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f));
				}
				else
				{
					Text text = CreateText("Token", ((Component)image).transform, builtinResource, 35, (FontStyle)1, (TextAnchor)4);
					text.text = $"{deploymentOrder.Count - i}\u00B0";
					text.resizeTextForBestFit = false;
					text.fontSize = 35;
					Stretch(text.rectTransform, 2f);
				}
			}
			ResizeTimelineTiles(deploymentOrder.Count);
			if (adventureScriptedTutorialActive && adventureScriptedTutorialStep < 2)
			{
				SetAdventureTutorialTimelineVisible(visible: false);
			}
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
		SetTurnCoinSuppressed(suppressed: true);
		if ((Object)(object)safeAreaRoot == (Object)null || deploymentOrder.Count == 0)
		{
			SetTurnCoinSuppressed(suppressed: false);
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
			SetTurnCoinSuppressed(suppressed: false);
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
		// I dadi sono in posa col numero tirato: e' qui che i talenti entrano in scena,
		// prima che la timeline diventi 1º, 2º, 3º.
		yield return RevealDeploymentInitiativeTalents(rectByToken, GetTimelineTileSize(deploymentOrder.Count));
		foreach (RectTransform rectTransform in diceRects)
		{
			if ((Object)(object)rectTransform != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)rectTransform).gameObject);
			}
		}
		SetTurnCoinSuppressed(suppressed: false);

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

		// I dadi sono in posa col numero tirato: e' qui che i talenti entrano in scena,
		// prima che la timeline diventi 1º, 2º, 3º.
		yield return RevealDeploymentInitiativeTalents(rectByToken, GetTimelineTileSize(deploymentOrder.Count));
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

	/// <summary>
	/// Il momento in cui i talenti d'iniziativa si vedono. I dadi sono appena atterrati
	/// nella timeline col numero tirato: qui si accende il "+N" su quelli potenziati e,
	/// se il bonus basta a scavalcare il vicino, li si guarda scambiarsi di posto. Solo
	/// dopo la timeline diventa 1º, 2º, 3º: l'ordine che il giocatore legge alla fine e'
	/// quello vero, e l'ha visto formarsi invece di trovarselo ribaltato a battaglia
	/// iniziata.
	/// </summary>
	private IEnumerator RevealDeploymentInitiativeTalents(IReadOnlyDictionary<DeploymentToken, RectTransform> rectByToken, float tileSize)
	{
		if (rectByToken == null || rectByToken.Count == 0 || (Object)(object)safeAreaRoot == (Object)null)
		{
			yield break;
		}
		Dictionary<DeploymentToken, RectTransform> badgeByToken = new Dictionary<DeploymentToken, RectTransform>();
		List<RectTransform> badgeRects = new List<RectTransform>();
		foreach (DeploymentToken token in deploymentOrder)
		{
			if (token.TalentInitiativeBonus <= 0 && !token.OpensTheFight)
			{
				continue;
			}
			if (!rectByToken.TryGetValue(token, out RectTransform dieRect) || (Object)(object)dieRect == (Object)null)
			{
				continue;
			}
			RectTransform badgeRect = CreateDeploymentTalentBadge(token, dieRect, tileSize);
			if ((Object)(object)badgeRect != (Object)null)
			{
				badgeByToken[token] = badgeRect;
				badgeRects.Add(badgeRect);
			}
		}
		if (badgeRects.Count == 0)
		{
			yield break;
		}
		AppendLog("TALENTI INIZIATIVA - " + DescribeDeploymentInitiativeTalents());
		yield return PlayDeploymentTalentBadgePop(badgeRects);
		List<DeploymentToken> reordered = new List<DeploymentToken>(deploymentOrder);
		reordered.Sort(CompareDeploymentTokensByEffectiveInitiative);
		if (!reordered.SequenceEqual(deploymentOrder))
		{
			deploymentOrder.Clear();
			deploymentOrder.AddRange(reordered);
			yield return SlideDeploymentDiceToNewOrder(rectByToken, badgeByToken);
		}
		else
		{
			yield return WaitForCardInspectionPause(0.35f);
		}
		foreach (RectTransform badgeRect in badgeRects)
		{
			if ((Object)(object)badgeRect != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)badgeRect).gameObject);
			}
		}
	}

	private string DescribeDeploymentInitiativeTalents()
	{
		List<string> parts = new List<string>();
		foreach (DeploymentToken token in deploymentOrder)
		{
			if (token.TalentInitiativeBonus <= 0 && !token.OpensTheFight)
			{
				continue;
			}
			string opens = token.OpensTheFight ?" (Apertura)" : string.Empty;
			parts.Add($"dado {token.Initiative} +{token.TalentInitiativeBonus} = {token.EffectiveInitiative}{opens}");
		}
		return string.Join(", ", parts);
	}

	private RectTransform CreateDeploymentTalentBadge(DeploymentToken token, RectTransform dieRect, float tileSize)
	{
		float size = Mathf.Max(28f, tileSize);
		Image background = CreateImage("Talent Initiative Badge", (Transform)(object)safeAreaRoot, new Color(0.72f, 0.48f, 0.12f, 0.96f));
		RectTransform badgeRect = background.rectTransform;
		badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
		badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
		badgeRect.pivot = new Vector2(0.5f, 0.5f);
		badgeRect.sizeDelta = new Vector2(size * (token.OpensTheFight ?2.15f : 1.05f), size * 0.62f);
		// Alla sinistra del dado: la timeline sta sul bordo, sopra e sotto ci sono gli
		// altri dadi e il badge coprirebbe proprio i vicini che deve far scavalcare.
		badgeRect.anchoredPosition = dieRect.anchoredPosition + new Vector2((0f - size) * 0.95f, 0f);
		background.raycastTarget = false;
		Outline outline = ((Component)background).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(1f, 0.86f, 0.25f, 0.9f);
		outline.effectDistance = new Vector2(2f, -2f);
		string label = token.OpensTheFight
			? (token.TalentInitiativeBonus > 0 ?$"+{token.TalentInitiativeBonus} APERTURA" : "APERTURA")
			: $"+{token.TalentInitiativeBonus}";
		Text text = CreateText("Value", ((Component)background).transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 26, (FontStyle)1, (TextAnchor)4);
		text.text = label;
		text.color = Color.white;
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 12;
		text.resizeTextMaxSize = 28;
		text.raycastTarget = false;
		Stretch(text.rectTransform, 2f);
		((Transform)badgeRect).localScale = Vector3.zero;
		return badgeRect;
	}

	private IEnumerator PlayDeploymentTalentBadgePop(List<RectTransform> badgeRects)
	{
		const float popDuration = 0.28f;
		float elapsed = 0f;
		while (elapsed < popDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / popDuration);
			// Sfora l'uno e rientra: il badge deve farsi notare, e' l'unica cosa che
			// spiega il riordino che sta per succedere.
			float scale = Mathf.LerpUnclamped(0f, 1f, 1f - Mathf.Pow(1f - t, 3f)) * (1f + Mathf.Sin(t * Mathf.PI) * 0.22f);
			foreach (RectTransform badgeRect in badgeRects)
			{
				if ((Object)(object)badgeRect != (Object)null)
				{
					((Transform)badgeRect).localScale = Vector3.one * scale;
				}
			}
			yield return null;
		}
		foreach (RectTransform badgeRect in badgeRects)
		{
			if ((Object)(object)badgeRect != (Object)null)
			{
				((Transform)badgeRect).localScale = Vector3.one;
			}
		}
		yield return WaitForCardInspectionPause(0.75f);
	}

	/// <summary>
	/// Lo scambio vero e proprio: ogni dado scivola alla sua nuova casella con una
	/// pancia laterale, cosi' due dadi che si scambiano non si attraversano.
	/// </summary>
	private IEnumerator SlideDeploymentDiceToNewOrder(
		IReadOnlyDictionary<DeploymentToken, RectTransform> rectByToken,
		IReadOnlyDictionary<DeploymentToken, RectTransform> badgeByToken)
	{
		Vector2[] targetPositions = GetDeploymentTimelineTargetPositions(deploymentOrder.Count);
		List<RectTransform> rects = new List<RectTransform>();
		List<RectTransform> badges = new List<RectTransform>();
		List<Vector2> badgeOffsets = new List<Vector2>();
		List<Vector2> starts = new List<Vector2>();
		List<Vector2> targets = new List<Vector2>();
		for (int i = 0; i < deploymentOrder.Count && i < targetPositions.Length; i++)
		{
			DeploymentToken token = deploymentOrder[i];
			if (!rectByToken.TryGetValue(token, out RectTransform rectTransform) || (Object)(object)rectTransform == (Object)null)
			{
				continue;
			}
			rects.Add(rectTransform);
			starts.Add(rectTransform.anchoredPosition);
			targets.Add(targetPositions[i]);
			// Il badge non e' figlio del dado (il dado 3D si disegna per conto suo):
			// viaggia agganciato allo scarto che aveva quando e' comparso.
			RectTransform badgeRect = badgeByToken != null && badgeByToken.TryGetValue(token, out RectTransform found) ?found : null;
			badges.Add(badgeRect);
			badgeOffsets.Add((Object)(object)badgeRect != (Object)null
				? badgeRect.anchoredPosition - rectTransform.anchoredPosition
				: Vector2.zero);
		}
		if (rects.Count == 0)
		{
			yield break;
		}
		bool vertical = IsTimelineVerticalLayout();
		Vector2 bulge = vertical ?Vector2.left : Vector2.up;
		float bulgeDistance = Mathf.Max(18f, GetTimelineTileSize(deploymentOrder.Count) * 0.55f);
		const float slideDuration = 0.5f;
		float elapsed = 0f;
		while (elapsed < slideDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / slideDuration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			for (int i = 0; i < rects.Count; i++)
			{
				if ((Object)(object)rects[i] == (Object)null)
				{
					continue;
				}
				float direction = Mathf.Sign((targets[i] - starts[i]).y);
				if (Mathf.Approximately(direction, 0f))
				{
					direction = 1f;
				}
				Vector2 arc = bulge * (Mathf.Sin(t * Mathf.PI) * bulgeDistance * direction);
				Vector2 pose = Vector2.LerpUnclamped(starts[i], targets[i], eased) + arc;
				rects[i].anchoredPosition = pose;
				if ((Object)(object)badges[i] != (Object)null)
				{
					badges[i].anchoredPosition = pose + badgeOffsets[i];
				}
			}
			yield return null;
		}
		for (int i = 0; i < rects.Count; i++)
		{
			if ((Object)(object)rects[i] != (Object)null)
			{
				rects[i].anchoredPosition = targets[i];
			}
			if ((Object)(object)badges[i] != (Object)null)
			{
				badges[i].anchoredPosition = targets[i] + badgeOffsets[i];
			}
		}
		yield return WaitForCardInspectionPause(0.45f);
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
			// La timeline di schieramento procede ancora dal tiro piu' basso al piu'
			// alto, ma visivamente i risultati bassi devono stare in basso.
			int visualIndex = IsTimelineVerticalLayout() ? count - 1 - i : i;
			Vector3 worldPosition = ((Transform)initiativeTimelineRoot).TransformPoint(localPositions[visualIndex]);
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
		ClearTargetHints();
		Dictionary<RectTransform, Vector2> battlefieldPawnPoses = CaptureBattlefieldPawnPoses(playerRow);
		DeploymentToken deploymentToken = deploymentOrder[currentDeploymentIndex];
		deploymentToken.DeployedCard = draftCandidates[num];
		selectedDraftCards.Add(num);
		selectedPlayerDeploymentIndices.Add(num);
		selectedPlayerDeploymentInitiatives.Add(deploymentToken.Initiative);
		selectedPlayerDeploymentTokens.Add(deploymentToken);
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
		if (adventureScriptedTutorialActive)
		{
			MoveAdventureTutorialSpotlight(null);
		}
		NotifyAdventureTutorial(AdventureTutorialAction.DeploymentConfirmed);
		inputLocked = true;
		ApplyResponsiveLayout();
		Canvas.ForceUpdateCanvases();
		StartBattlefieldPawnGlide(battlefieldPawnPoses);
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
		deploymentMorphCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayDeploymentMorph(draftCandidates[num], position, rotation, startSize, prototypeCardView2, configuration.Animation.CardDeployDuration));
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
		// La pedina sta sotto e resta piena: incrociare le due opacita' faceva
		// scendere l'insieme sotto l'opaco a meta' strada (due meta' trasparenti
		// non fanno un intero), ed era il lampo che si vedeva sulla carta che si
		// trasforma. Cosi' la carta si dissolve scoprendo la pedina, e sul campo
		// non compare mai un buco.
		PrepareMorphFace(tokenFace, 1f);
		((Transform)tokenFace.RectTransform).SetAsFirstSibling();
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
		// Un margine sul tempo dello schieramento: il morph e il timer che fa
		// proseguire la deployment partono insieme e durano uguale, e chiudere in
		// pareggio significa giocarsi l'ultima carta ai dadi. Chiudendo prima, la
		// pedina e' gia' posata quando la battaglia comincia.
		float safeDuration = Mathf.Max(0.001f, duration * 0.92f);
		bool firstFrame = true;
		// L'ultimo giro scrive la posa esatta di arrivo e non cede il frame: la
		// pedina definitiva prende il posto dell'overlay mentre i due coincidono
		// al pixel, e lo scambio non si vede.
		//
		// La pulizia sta nel finally perche' questo morph corre alla pari con il
		// timer che fa proseguire lo schieramento: sull'ultima carta la battaglia
		// puo' cominciare - e portarsi via la preview - mentre l'overlay e'
		// ancora in volo. Se la coroutine muore li' senza smontarlo, l'overlay
		// resta appeso al Safe Area e la pedina si vede sdoppiata.
		try
		{
			while (true)
			{
				if ((Object)(object)overlayRoot == (Object)null || (Object)(object)finalPreview == (Object)null)
				{
					break;
				}
				deploymentMorphFrame = Time.frameCount;
				// Il frame che ha creato le due facce e ricalcolato il layout e' il
				// piu' lungo della sequenza: contarne il delta faceva nascere il
				// morph gia' a un quinto di strada.
				elapsed += firstFrame ?0f : AnimationDeltaTime();
				firstFrame = false;
				float num = Mathf.Clamp01(elapsed / safeDuration);
				float num2 = Mathf.SmoothStep(0f, 1f, num);
				float num3 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.28f) / 0.58f));
				overlayRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, num2);
				overlayRect.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, num3);
				((Transform)overlayRect).localRotation = Quaternion.SlerpUnclamped(Quaternion.Inverse(((Transform)safeAreaRoot).rotation) * startWorldRotation, Quaternion.identity, num2);
				((Transform)overlayRect).localScale = Vector3.one * Mathf.LerpUnclamped(1.03f, 1f, num2);
				cardFace.SetAlpha(1f - num3);
				// La pedina definitiva sale sotto l'overlay, che resta opaco: del suo
				// corpo non si vede nulla, ma il bordo di selezione che sborda si
				// accende in dissolvenza invece di comparire di colpo allo scambio.
				finalPreview.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.72f) / 0.28f)));
				if (num >= 1f)
				{
					break;
				}
				yield return null;
			}
		}
		finally
		{
			// Object.Destroy rimuove l'overlay soltanto a fine frame. Se prima
			// rendiamo visibile la pedina definitiva, entrambe vengono disegnate
			// sovrapposte per un frame e producono un lampo. E' piu' evidente per
			// le carte laterali della mano, che arrivano anche da una rotazione.
			// Spegni l'overlay subito: la distruzione differita diventa invisibile.
			if ((Object)(object)overlayGroup != (Object)null)
			{
				overlayGroup.alpha = 0f;
			}
			if ((Object)(object)finalPreview != (Object)null)
			{
				finalPreview.SetAlpha(1f);
				((Transform)finalPreview.RectTransform).localScale = Vector3.one;
				((Transform)finalPreview.RectTransform).localRotation = Quaternion.identity;
				finalPreview.SetLayoutIgnored(ignored: true);
			}
			if ((Object)(object)overlayRoot != (Object)null)
			{
				Object.Destroy((Object)(object)overlayRoot);
			}
			deploymentMorphCoroutine = null;
		}
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
			ClearTargetHints();
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
			battleCardState.AbilityUsedThisTurn = false;
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
					battleCardState2.AbilityUsedThisTurn = false;
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
				battleCardState4.AbilityUsedThisTurn = false;
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
				battleCardState5.AbilityUsedThisTurn = false;
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
		if ((Object)(object)playerTitleText != (Object)null)
		{
			playerTitleText.text = campaignDeck != null
				? string.Empty
				: GameText.Get(GameTextKeys.Campaign.YourFormation);
		}
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
				ApplyDeploymentTokenToCard(battleCardState, selectedPlayerDeploymentTokens, num2);
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
					ApplyDeploymentTokenToCard(battleCardState2, selectedCpuDeploymentTokens, num3);
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

		// Le pedine che stanno ancora scivolando qui vengono distrutte e rifatte:
		// la scivolata non ha piu' niente da portare a destinazione, e lasciarla
		// viva significa solo un'altra mano sulle pedine nuove.
		StopBattlefieldPawnGlide();
		DestroyPrototypeViews(draftViews);
		draftViews.Clear();
		DestroyPrototypeViews(playerDeploymentPreviewViews);
		playerDeploymentPreviewViews.Clear();
		draftCandidates.Clear();
		draftCampaignCards.Clear();
		selectedDraftCards.Clear();
		draftActive = false;
		((Component)confirmActionButton).gameObject.SetActive(false);
		if ((Object)(object)playerTitleText != (Object)null)
		{
			playerTitleText.text = campaignDeck != null
				? string.Empty
				: GameText.Get(GameTextKeys.Campaign.YourFormation);
		}

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
			{
				state.Initiative = selectedPlayerDeploymentInitiatives[index];
				ApplyDeploymentTokenToCard(state, selectedPlayerDeploymentTokens, index);
			}
		}

		List<CardDefinition> cpuFormation = new List<CardDefinition>(selectedCpuDeploymentCards);
		initialCpuFormation.Clear();
		initialCpuFormation.AddRange(cpuFormation);
		survivingCpuFormation.Clear();
		for (int index = 0; index < cpuFormation.Count; index++)
		{
			BattleCardState state = AddCard(cpuCards, cpuRow, cpuFormation[index], belongsToPlayer: false, index);
			if (state != null && index < selectedCpuDeploymentInitiatives.Count)
			{
				state.Initiative = selectedCpuDeploymentInitiatives[index];
				ApplyDeploymentTokenToCard(state, selectedCpuDeploymentTokens, index);
			}
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

	private void StartPlayerBattlefieldRowTransition(Vector2 startAnchorMin, Vector2 startAnchorMax, Vector2 startSize, Vector2 startPosition, float delay = 0f)
	{
		if ((Object)(object)playerRow == (Object)null)
		{
			return;
		}
		if (playerBattlefieldRowTransitionCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(playerBattlefieldRowTransitionCoroutine);
		}
		playerRowTransitionTargetAnchorMin = playerRow.anchorMin;
		playerRowTransitionTargetAnchorMax = playerRow.anchorMax;
		playerRowTransitionTargetSize = playerRow.sizeDelta;
		playerRowTransitionTargetPosition = playerRow.anchoredPosition;
		if (Vector2.Distance(startAnchorMin, playerRowTransitionTargetAnchorMin) < 0.001f
			&& Vector2.Distance(startAnchorMax, playerRowTransitionTargetAnchorMax) < 0.001f)
		{
			playerBattlefieldRowTransitionCoroutine = null;
			return;
		}
		playerRowTransitionRetargeted = false;
		playerRowTransitionFrame = Time.frameCount;
		playerRow.anchorMin = startAnchorMin;
		playerRow.anchorMax = startAnchorMax;
		playerRow.sizeDelta = startSize;
		playerRow.anchoredPosition = startPosition;
		playerBattlefieldRowTransitionCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayPlayerBattlefieldRowTransition(delay));
	}

	/// <summary>
	/// Un ricalcolo di layout mentre la fila e' in volo le riscrive ancore e
	/// misure ai valori finali: il tween, che aveva gia' catturato i suoi
	/// estremi, il frame dopo la riportava indietro. Qui il valore appena
	/// calcolato diventa il nuovo bersaglio e la fila torna alla posa
	/// interpolata, cosi' la corsa prosegue da dove era invece di rimbalzare.
	/// </summary>
	private void RetargetPlayerBattlefieldRowTransition(Vector2 poseAnchorMin, Vector2 poseAnchorMax, Vector2 poseSize, Vector2 posePosition)
	{
		if (!IsRoutineAlive(playerBattlefieldRowTransitionCoroutine, playerRowTransitionFrame)
			|| (Object)(object)playerRow == (Object)null)
		{
			return;
		}
		playerRowTransitionTargetAnchorMin = playerRow.anchorMin;
		playerRowTransitionTargetAnchorMax = playerRow.anchorMax;
		playerRowTransitionTargetSize = playerRow.sizeDelta;
		playerRowTransitionTargetPosition = playerRow.anchoredPosition;
		playerRow.anchorMin = poseAnchorMin;
		playerRow.anchorMax = poseAnchorMax;
		playerRow.sizeDelta = poseSize;
		playerRow.anchoredPosition = posePosition;
		playerRowTransitionRetargeted = true;
	}

	private void StopPlayerBattlefieldRowTransition()
	{
		if (playerBattlefieldRowTransitionCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(playerBattlefieldRowTransitionCoroutine);
			playerBattlefieldRowTransitionCoroutine = null;
		}
	}

	private IEnumerator PlayPlayerBattlefieldRowTransition(float delay)
	{
		if (delay > 0f)
			yield return new WaitForSecondsRealtime(delay);

		float duration = Mathf.Clamp(configuration.Animation.CardDeployDuration * 0.55f, 0.22f, 0.42f);
		Vector2 startAnchorMin = playerRow.anchorMin;
		Vector2 startAnchorMax = playerRow.anchorMax;
		Vector2 startSize = playerRow.sizeDelta;
		Vector2 startPosition = playerRow.anchoredPosition;
		playerRowTransitionRetargeted = false;
		float elapsed = 0f;
		bool firstFrame = true;
		while (elapsed < duration)
		{
			if ((Object)(object)playerRow == (Object)null)
			{
				playerBattlefieldRowTransitionCoroutine = null;
				yield break;
			}
			playerRowTransitionFrame = Time.frameCount;
			if (playerRowTransitionRetargeted)
			{
				// Il bersaglio e' cambiato sotto: si riparte dalla posa attuale
				// sul tempo che resta, senza salti e senza allungare la corsa.
				duration = Mathf.Max(duration - elapsed, 0.08f);
				elapsed = 0f;
				startAnchorMin = playerRow.anchorMin;
				startAnchorMax = playerRow.anchorMax;
				startSize = playerRow.sizeDelta;
				startPosition = playerRow.anchoredPosition;
				playerRowTransitionRetargeted = false;
				firstFrame = true;
			}
			// Il frame che fa partire (o ribersaglia) la corsa e' quello del
			// ricalcolo di layout: contarne il delta significherebbe saltare in
			// avanti appena cominciata.
			elapsed += firstFrame ?0f : AnimationDeltaTime();
			firstFrame = false;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			playerRow.anchorMin = Vector2.LerpUnclamped(startAnchorMin, playerRowTransitionTargetAnchorMin, eased);
			playerRow.anchorMax = Vector2.LerpUnclamped(startAnchorMax, playerRowTransitionTargetAnchorMax, eased);
			playerRow.sizeDelta = Vector2.LerpUnclamped(startSize, playerRowTransitionTargetSize, eased);
			playerRow.anchoredPosition = Vector2.LerpUnclamped(startPosition, playerRowTransitionTargetPosition, eased);
			yield return null;
		}
		playerRow.anchorMin = playerRowTransitionTargetAnchorMin;
		playerRow.anchorMax = playerRowTransitionTargetAnchorMax;
		playerRow.sizeDelta = playerRowTransitionTargetSize;
		playerRow.anchoredPosition = playerRowTransitionTargetPosition;
		playerBattlefieldRowTransitionCoroutine = null;
	}

	/// <summary>
	/// Fotografa dove stanno le pedine gia' in campo. Ogni nuovo schieramento
	/// ricentra la fila, e senza una posa di partenza le pedine precedenti si
	/// teletrasportano nel frame in cui la nuova comincia il suo morph. Chi sta
	/// gia' animando il proprio ingresso resta fuori: la sua posizione ha
	/// gia' un padrone.
	/// </summary>
	private static Dictionary<RectTransform, Vector2> CaptureBattlefieldPawnPoses(params RectTransform[] rows)
	{
		Dictionary<RectTransform, Vector2> poses = new Dictionary<RectTransform, Vector2>();
		if (rows == null)
		{
			return poses;
		}
		foreach (RectTransform row in rows)
		{
			if ((Object)(object)row == (Object)null)
			{
				continue;
			}
			for (int index = 0; index < ((Transform)row).childCount; index++)
			{
				RectTransform child = ((Transform)row).GetChild(index) as RectTransform;
				if ((Object)(object)child == (Object)null || !((Component)child).gameObject.activeSelf)
				{
					continue;
				}
				PrototypeCardView view = ((Component)child).GetComponent<PrototypeCardView>();
				if ((Object)(object)view != (Object)null && (view.IsPlayingMotion || view.IsDragging))
				{
					continue;
				}
				poses[child] = child.anchoredPosition;
			}
		}
		return poses;
	}

	private void StartBattlefieldPawnGlide(IReadOnlyDictionary<RectTransform, Vector2> startPoses)
	{
		if (startPoses == null || startPoses.Count == 0)
		{
			return;
		}
		if (battlefieldPawnGlideCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(battlefieldPawnGlideCoroutine);
		}
		battlefieldPawnGlideCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayBattlefieldPawnGlide(startPoses));
	}

	/// <summary>
	/// Lascia le pedine dove sono adesso. Serve a chi prende il loro comando a
	/// meta' corsa: la posa finale non va scritta, o l'animazione che subentra
	/// partirebbe da un salto.
	/// </summary>
	private void StopBattlefieldPawnGlide()
	{
		if (battlefieldPawnGlideCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(battlefieldPawnGlideCoroutine);
			battlefieldPawnGlideCoroutine = null;
		}
	}

	private IEnumerator PlayBattlefieldPawnGlide(IReadOnlyDictionary<RectTransform, Vector2> startPoses)
	{
		Dictionary<RectTransform, Vector2> moved = new Dictionary<RectTransform, Vector2>();
		foreach (KeyValuePair<RectTransform, Vector2> pose in startPoses)
		{
			if ((Object)(object)pose.Key == (Object)null)
			{
				continue;
			}
			Vector2 target = pose.Key.anchoredPosition;
			if (Vector2.Distance(pose.Value, target) < 0.5f)
			{
				continue;
			}
			moved[pose.Key] = target;
			pose.Key.anchoredPosition = pose.Value;
		}
		if (moved.Count == 0)
		{
			battlefieldPawnGlideCoroutine = null;
			yield break;
		}
		float duration = Mathf.Clamp(configuration.Animation.CardDeployDuration * 0.45f, 0.18f, 0.32f);
		float elapsed = 0f;
		bool firstFrame = true;
		while (elapsed < duration)
		{
			// La scivolata nasce nel frame che ha ricalcolato il layout e creato
			// la nuova pedina: quel frame e' lungo, e il suo delta - contato
			// subito - farebbe cominciare la corsa gia' a un quarto di strada.
			elapsed += firstFrame ?0f : AnimationDeltaTime();
			firstFrame = false;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			foreach (KeyValuePair<RectTransform, Vector2> pair in moved)
			{
				if ((Object)(object)pair.Key != (Object)null)
				{
					pair.Key.anchoredPosition = Vector2.LerpUnclamped(startPoses[pair.Key], pair.Value, eased);
				}
			}
			yield return null;
		}
		foreach (KeyValuePair<RectTransform, Vector2> pair in moved)
		{
			if ((Object)(object)pair.Key != (Object)null)
			{
				pair.Key.anchoredPosition = pair.Value;
			}
		}
		battlefieldPawnGlideCoroutine = null;
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

	private void RestoreBattlefieldCardVisibility(IEnumerable<BattleCardState> cards)
	{
		foreach (BattleCardState card in cards)
		{
			RestoreBattlefieldPreviewVisibility(card?.View);
		}
	}

	private void RestoreBattlefieldPreviewVisibility(PrototypeCardView view)
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
