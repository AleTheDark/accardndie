using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.GameData;
using AccardND.NetProtocol;
using AccardND.PvpUi;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private enum PvpPresentationTargetMode
    {
        None,
        Attack,
        Ability,
        Attachment
    }

    private bool pvpPresentationActive;
    private PvpBootstrap activePvpBootstrap;
    private PvpClientMatchState pvpState;
    private IReadOnlyList<LoadoutCardDto> pvpLoadout;
    private IBattlePresentationActions pvpActions;
    private readonly List<PrototypeCardView> pvpHandViews = new();
    private readonly Dictionary<BattleCardState, int> pvpCardSlots = new();
    private readonly Dictionary<BattleCardState, int> pvpCardLives = new();
    private readonly Dictionary<BattleCardState, string> pvpCardIds = new();
    private PvpPresentationTargetMode pvpTargetMode = PvpPresentationTargetMode.None;
    private readonly List<string> pvpTimelineOrderKeys = new();
    private Coroutine pvpTimelineSlideRoutine;

    // Regia: gli eventi del server vengono recitati in sequenza dalle stesse
    // animazioni della campagna, poi lo stato autoritativo riallinea la scena.
    private readonly Queue<BattlePresentationEvent> pvpPendingEvents = new();
    private readonly Dictionary<string, Queue<PvpDeploymentPose>> pvpDeploymentPoses = new();
    private Coroutine pvpRegiaRoutine;
    private bool pvpDeploymentIntroPlayed;
    private string pvpHandSignature = string.Empty;
    private int pvpPendingDeployHandIndex = -1;
    private string pvpPendingDeployCardName = string.Empty;
    private int pvpPendingPaladinRedirectPlayer = -1;
    private int pvpPendingPaladinRedirectSlot = -1;

    // Selezione del round decisivo: il giocatore sceglie N carte fra tutte quelle
    // del loadout (indici loadout). Restano selezionate finché non conferma.
    private readonly List<int> pvpDecisiveSelection = new();
    private readonly List<int> pvpDecisiveViewIndices = new();
    private bool pvpDecisiveSubmitted;

    private readonly struct PvpDeploymentPose
    {
        public PvpDeploymentPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    public void ShowPvpMatch(
        AccardND.NetProtocol.PvpClientMatchState state,
        System.Collections.Generic.IReadOnlyList<AccardND.NetProtocol.LoadoutCardDto> myLoadout,
        AccardND.Battlefield.IBattlePresentationActions actions)
    {
        pvpPresentationActive = true;
        pvpState = state;
        pvpLoadout = myLoadout;
        pvpActions = actions;
        pvpTargetMode = PvpPresentationTargetMode.None;
        pvpPendingDeployHandIndex = -1;
        pvpPendingDeployCardName = string.Empty;
        ClearPendingPaladinRedirect();
        ResetPvpDecisiveSelection();
        StopPvpRegia();
        pvpDeploymentIntroPlayed = false;
        ClearPvpHand();
        pvpCardSlots.Clear();
        pvpCardLives.Clear();
        pvpCardIds.Clear();
        pvpDeploymentPoses.Clear();

        if ((Object)(object)modeSelectionPanel != (Object)null)
            modeSelectionPanel.SetActive(false);
        if ((Object)(object)roomChoicePanel != (Object)null)
            roomChoicePanel.SetActive(false);
        if ((Object)(object)deckBuilderPanel != (Object)null)
            deckBuilderPanel.SetActive(false);
        if ((Object)(object)initialDraftPanel != (Object)null)
            initialDraftPanel.SetActive(false);
        if ((Object)(object)campaignModeSelectionPanel != (Object)null)
            campaignModeSelectionPanel.SetActive(false);

        SetBattlefieldSurfaceVisible(true);
        // La label formazione è parte della chrome campagna: nel PvP resta vuota.
        if ((Object)(object)playerTitleText != (Object)null)
            playerTitleText.text = string.Empty;
        RenderPvpMatch();
    }

    public void UpdatePvpMatch(
        AccardND.NetProtocol.PvpClientMatchState state,
        System.Collections.Generic.IReadOnlyList<AccardND.NetProtocol.LoadoutCardDto> myLoadout,
        System.Collections.Generic.IReadOnlyList<AccardND.Battlefield.BattlePresentationEvent> events)
    {
        if (!pvpPresentationActive)
            return;

        pvpState = state;
        pvpLoadout = myLoadout;
        if (events != null)
        {
            foreach (BattlePresentationEvent battleEvent in events)
            {
                if (battleEvent != null)
                    pvpPendingEvents.Enqueue(battleEvent);
            }
        }

        if (pvpRegiaRoutine == null)
            pvpRegiaRoutine = StartCoroutine(PlayPvpRegiaRoutine());
    }

    public void HidePvpMatch()
    {
        if (!pvpPresentationActive)
            return;

        pvpPresentationActive = false;
        pvpState = null;
        pvpLoadout = null;
        pvpActions = null;
        pvpTargetMode = PvpPresentationTargetMode.None;
        pvpPendingDeployHandIndex = -1;
        pvpPendingDeployCardName = string.Empty;
        ResetPvpDecisiveSelection();
        StopPvpRegia();

        ClearPvpHand();
        DestroyCardViews(playerCards);
        DestroyCardViews(cpuCards);
        DestroyPrototypeViews(pvpHandViews);
        ClearCardRowChildren(playerRow);
        ClearCardRowChildren(cpuRow);
        ClearCardRowChildren(playerHandRow);
        pvpCardSlots.Clear();
        pvpCardLives.Clear();
        pvpCardIds.Clear();
        pvpDeploymentPoses.Clear();
        ClearInitiativeTimeline();
        SetMessage("Multiplayer chiuso.");
        deploymentDraftActive = false;
        SetTurnBanner(playerTurn: true, "PREPARAZIONE");
        if ((Object)(object)playerTitleText != (Object)null)
            playerTitleText.text = "LA TUA FORMAZIONE";
        SetBattlefieldSurfaceVisible(false);
    }

    private void AbandonActivePvpSession()
    {
        if ((Object)(object)activePvpBootstrap != (Object)null)
        {
            PvpBootstrap bootstrap = activePvpBootstrap;
            activePvpBootstrap = null;
            bootstrap.CloseFromMainMenu();
            return;
        }

        HidePvpMatch();
    }

    private void RenderPvpMatch()
    {
        if (!pvpPresentationActive || pvpState == null || cardDatabase == null)
        {
            Debug.LogWarning($"[PvP] Render saltato: attivo={pvpPresentationActive}, stato={(pvpState != null)}, cardDatabase={(cardDatabase != null)}");
            return;
        }

        Debug.Log($"[PvP] Render: fase {pvpState.Phase}, io G{pvpState.MyIndex}, schiera G{pvpState.DeployPlayer}, "
            + $"turno battaglia G{pvpState.ActivePlayer}/slot {pvpState.ActiveSlot}, mano {pvpState.Hand.Count}, "
            + $"board {pvpState.Boards[0].Count}-{pvpState.Boards[1].Count}, tocca a me: {IsPvpLocalActionTurn()}");

        currentRoomType = RoomType.Monster;
        draftActive = false;
        // Il layout campagna usa questo flag per tenere pannello messaggi e mano
        // nella posizione dello schieramento: replichiamo le stesse fasi.
        deploymentDraftActive = pvpState.Phase == PvpClientPhase.Deployment
            || pvpState.Phase == PvpClientPhase.DecisiveSelection
            || pvpState.Phase == PvpClientPhase.Waiting;
        inputLocked = !IsPvpLocalActionTurn();
        gameFinished = pvpState.Phase == PvpClientPhase.Finished;
        roundNumber = pvpState.MatchRound;
        selectedPlayerIndex = pvpState.ActiveSlot;
        playerAura = ToBattleAura(pvpState.Auras[pvpState.MyIndex >= 0 ? pvpState.MyIndex : 0]);
        cpuAura = ToBattleAura(pvpState.Auras[OpponentIndex()]);

        SyncPvpBoard(pvpState.MyIndex, playerCards, playerRow, belongsToPlayer: true);
        SyncPvpBoard(OpponentIndex(), cpuCards, cpuRow, belongsToPlayer: false);
        LinkPvpMarkedTargets();
        RefreshPvpCardVisuals(playerCards);
        RefreshPvpCardVisuals(cpuCards);
        if (pvpState.Phase == PvpClientPhase.DecisiveSelection)
            BuildPvpDecisiveHand();
        else
            BuildPvpHand();
        RefreshPvpHud();
        RefreshPvpMessage();
        RefreshPvpTurnBanner();
        ApplyResponsiveLayout();
        RefreshPvpTimeline();
        ApplyHandFan();
        ConfigurePvpActionOverlays();
    }

    private void RefreshPvpTurnBanner()
    {
        if (pvpState == null)
            return;

        switch (pvpState.Phase)
        {
            case PvpClientPhase.Deployment:
                SetTurnBanner(pvpState.IsMyDeployTurn, "SCHIERAMENTO  -  DAL PIU BASSO AL PIU ALTO");
                break;
            case PvpClientPhase.DecisiveSelection:
                SetTurnBanner(
                    playerTurn: !pvpDecisiveSubmitted,
                    pvpDecisiveSubmitted ? "ROUND DECISIVO  -  IN ATTESA" : "ROUND DECISIVO  -  SCEGLI LE CARTE");
                break;
            case PvpClientPhase.Battle:
                SetTurnBanner(
                    pvpState.IsMyBattleTurn,
                    $"ROUND {pvpState.MatchRound}  -  " + (pvpState.IsMyBattleTurn ? "TUO TURNO" : "TURNO AVVERSARIO"));
                break;
            case PvpClientPhase.Finished:
                if (!pvpState.EndedByForfeit && !pvpState.HasEliminatedFormation)
                {
                    SetTurnBanner(playerTurn: true, "RISULTATO IN VERIFICA");
                    break;
                }
                SetTurnBanner(
                    pvpState.Winner == pvpState.MyIndex,
                    pvpState.Winner == pvpState.MyIndex ? "VITTORIA!" : "SCONFITTA");
                break;
            default:
                SetTurnBanner(playerTurn: true, "PREPARAZIONE");
                break;
        }
    }

    private void SyncPvpBoard(int player, List<BattleCardState> destination, RectTransform row, bool belongsToPlayer)
    {
        if (pvpState == null || player < 0 || player >= pvpState.Boards.Length)
            return;

        List<PvpClientCard> board = pvpState.Boards[player];

        // Rimuove le pedine sparite dallo stato (es. nuovo round): le altre restano
        // vive, così le animazioni in corso non vengono interrotte da un rebuild.
        for (int index = destination.Count - 1; index >= 0; index--)
        {
            BattleCardState card = destination[index];
            bool stillPresent = card != null
                && pvpCardSlots.TryGetValue(card, out int slot)
                && pvpCardIds.TryGetValue(card, out string cardId)
                && board.Any(source => source.Slot == slot && source.CardId == cardId);
            if (stillPresent)
                continue;

            if (card != null && (Object)(object)card.View != (Object)null)
                Object.Destroy(((Component)card.View).gameObject);
            if (card != null)
            {
                pvpCardSlots.Remove(card);
                pvpCardLives.Remove(card);
                pvpCardIds.Remove(card);
            }
            destination.RemoveAt(index);
        }

        foreach (PvpClientCard source in board.OrderBy(card => card.Slot))
        {
            BattleCardState existing = FindPvpCardByServerSlot(destination, source.Slot);
            if (existing != null)
            {
                existing.Initiative = source.Initiative;
                existing.Eliminated = source.Eliminated || source.Lives <= 0;
                existing.IsSpirit = source.IsSpirit;
                existing.InhibitedTurns = source.Inhibited ? 1 : 0;
                existing.AbilityArmed = source.AbilityArmed;
                existing.AbilityUsed = source.AbilityUsed;
                existing.MightAuraCombatBonus = source.MightAuraBonus;
                existing.PermanentCombatBonus = source.PermanentBonus - source.MightAuraBonus + source.Strength - existing.Definition.Strength;
                existing.PendingAttackBonus = source.PendingBonus;
                existing.PendingAttackBonusKind = ToPendingAttackBonusKind(source.PendingBonusKind);
                existing.PendingVigorStepPenalty = source.DiePenaltySteps;
                pvpCardLives[existing] = Mathf.Max(0, source.Lives);
                continue;
            }

            CardDefinition definition = FindPvpDefinition(source.CardId);
            if ((Object)(object)definition == (Object)null)
            {
                Debug.LogWarning($"[PvP] Carta schierata '{source.CardId}' non trovata nel CardDatabase: pedina non visibile!");
                continue;
            }

            PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)row, definition, configuration);
            var card = new BattleCardState(definition, view, belongsToPlayer)
            {
                Initiative = source.Initiative,
                Eliminated = source.Eliminated || source.Lives <= 0,
                IsSpirit = source.IsSpirit,
                InhibitedTurns = source.Inhibited ? 1 : 0,
                AbilityArmed = source.AbilityArmed,
                AbilityUsed = source.AbilityUsed,
                PermanentCombatBonus = source.PermanentBonus - source.MightAuraBonus + source.Strength - definition.Strength,
                MightAuraCombatBonus = source.MightAuraBonus,
                PendingAttackBonus = source.PendingBonus,
                PendingAttackBonusKind = ToPendingAttackBonusKind(source.PendingBonusKind),
                PendingVigorStepPenalty = source.DiePenaltySteps
            };

            int capturedSlot = source.Slot;
            pvpCardSlots[card] = capturedSlot;
            pvpCardLives[card] = Mathf.Max(0, source.Lives);
            pvpCardIds[card] = source.CardId;
            if (belongsToPlayer)
                view.Button.onClick.AddListener(new UnityAction(() => HandlePvpPlayerCardClick(capturedSlot)));
            else
                view.Button.onClick.AddListener(new UnityAction(() => HandlePvpOpponentCardClick(capturedSlot)));

            destination.Add(card);
        }
    }

    private void LinkPvpMarkedTargets()
    {
        LinkPvpMarkedTargetsForBoard(pvpState?.Boards[OpponentIndex()], playerCards, cpuCards);
        LinkPvpMarkedTargetsForBoard(pvpState?.Boards[pvpState.MyIndex], cpuCards, playerCards);
    }

    private void LinkPvpMarkedTargetsForBoard(
        IReadOnlyList<PvpClientCard> markedTargetsBoard,
        List<BattleCardState> sameSide,
        List<BattleCardState> oppositeSide)
    {
        if (markedTargetsBoard == null)
            return;

        BattleCardState hunter = sameSide.FirstOrDefault(card => card != null && card.Card.HeroClass == HeroClass.Hunter);
        if (hunter == null)
            return;

        foreach (PvpClientCard source in markedTargetsBoard)
        {
            if (source == null || !source.Marked)
                continue;

            BattleCardState target = FindPvpCardByServerSlot(oppositeSide, source.Slot);
            if (target != null)
                hunter.MarkedTarget = target;
        }
    }

    private void BuildPvpHand()
    {
        // Come in campagna: la mano resta visibile per tutto lo schieramento,
        // ma è cliccabile solo quando è il proprio turno di calare.
        if (pvpState == null || pvpState.Phase != PvpClientPhase.Deployment)
        {
            if (pvpHandViews.Count > 0)
                ClearPvpHand();
            return;
        }

        bool myTurn = pvpState.IsMyDeployTurn;
        bool hideUntilIntro = ShouldHidePvpHandUntilIntro();
        string signature = BuildPvpHandSignature();
        if (!myTurn || signature != pvpHandSignature)
        {
            pvpPendingDeployHandIndex = -1;
            pvpPendingDeployCardName = string.Empty;
            RefreshPvpDeploymentPendingUi();
        }

        // Ricostruire la mano a ogni render raddoppierebbe i figli della riga nello
        // stesso frame (Destroy è differito ma childCount conta gli inattivi), e
        // ApplyResponsiveLayout dimensionerebbe le carte a metà. Come in campagna
        // la mano si ricostruisce solo quando cambia davvero il contenuto.
        if (signature == pvpHandSignature && pvpHandViews.Count > 0)
        {
            foreach (PrototypeCardView existing in pvpHandViews)
            {
                if ((Object)(object)existing != (Object)null)
                {
                    existing.SetInteractable(myTurn && !hideUntilIntro);
                    existing.SetDraftSelected(myTurn && !hideUntilIntro && pvpPendingDeployHandIndex >= 0
                        && pvpHandViews.IndexOf(existing) == pvpPendingDeployHandIndex);
                    if (hideUntilIntro)
                        existing.SetAlpha(0f);
                }
            }
            RefreshPvpDeploymentPendingUi();
            ApplyHandFan();
            return;
        }

        Debug.Log($"[PvP] Rebuild mano ({pvpState.Hand.Count} carte), schiera G{pvpState.DeployPlayer}, cliccabile: {myTurn}.");
        ClearPvpHand();
        pvpHandSignature = signature;
        for (int position = 0; position < pvpState.Hand.Count; position++)
        {
            int captured = position;
            CardDefinition definition = FindPvpDefinition(pvpState.Hand[position].DefinitionId);
            if ((Object)(object)definition == (Object)null)
            {
                Debug.LogWarning($"[PvP] Carta della mano '{pvpState.Hand[position].DefinitionId}' non trovata nel CardDatabase.");
                continue;
            }

            PrototypeCardView view = PrototypeCardView.Create((Transform)(object)playerHandRow, definition, configuration);
            view.SetInteractable(myTurn && !hideUntilIntro);
            if (hideUntilIntro)
                view.SetAlpha(0f);
            string capturedDefinitionId = pvpState.Hand[position].DefinitionId;
            view.Button.onClick.AddListener(new UnityAction(() =>
            {
                if (pvpState == null || !pvpState.IsMyDeployTurn)
                    return;
                SelectPvpDeploymentCard(captured, capturedDefinitionId, view);
            }));
            pvpHandViews.Add(view);
        }
        RefreshPvpDeploymentPendingUi();
        ApplyHandFan();
    }

    // La mano resta nascosta per tutta la fase di schieramento finché l'intro non
    // la fa entrare: NON richiede DeploymentOrder popolato, perché al DeploymentStarted
    // le iniziative non sono ancora arrivate e la mano verrebbe mostrata in anticipo
    // (causa delle "carte doppie" e dell'apparizione prima dell'animazione d'ingresso).
    private bool ShouldHidePvpHandUntilIntro() =>
        pvpState != null
        && pvpState.Phase == PvpClientPhase.Deployment
        && !pvpDeploymentIntroPlayed;

    // --- Round decisivo: scelta delle carte fra tutto il loadout ---

    // Mostra tutte le carte del loadout come toggle: il giocatore ne seleziona N
    // (DecisiveRequiredCount) e conferma, oppure attende l'avversario dopo l'invio.
    private void BuildPvpDecisiveHand()
    {
        if (pvpState == null || pvpState.Phase != PvpClientPhase.DecisiveSelection || pvpLoadout == null)
        {
            if (pvpHandViews.Count > 0)
                ClearPvpHand();
            return;
        }

        int required = Mathf.Max(1, pvpState.DecisiveRequiredCount);
        string signature = "decisive|" + BuildPvpLoadoutSignature();
        if (signature != pvpHandSignature || pvpHandViews.Count == 0)
        {
            ClearPvpHand();
            pvpHandSignature = signature;
            pvpDecisiveViewIndices.Clear();
            for (int index = 0; index < pvpLoadout.Count; index++)
            {
                LoadoutCardDto entry = pvpLoadout[index];
                CardDefinition definition = entry != null ? FindPvpDefinition(entry.definitionId) : null;
                if ((Object)(object)definition == (Object)null)
                {
                    Debug.LogWarning($"[PvP] Carta del loadout decisivo '{entry?.definitionId}' non trovata nel CardDatabase.");
                    continue;
                }

                int capturedLoadoutIndex = index;
                PrototypeCardView view = PrototypeCardView.Create((Transform)(object)playerHandRow, definition, configuration);
                view.Button.onClick.AddListener(new UnityAction(() => TogglePvpDecisiveCard(capturedLoadoutIndex)));
                pvpHandViews.Add(view);
                pvpDecisiveViewIndices.Add(capturedLoadoutIndex);
            }
        }

        RefreshPvpDecisiveSelectionUi(required);
        ApplyHandFan();
    }

    private void TogglePvpDecisiveCard(int loadoutIndex)
    {
        if (pvpState == null || pvpState.Phase != PvpClientPhase.DecisiveSelection || pvpDecisiveSubmitted)
            return;

        int required = Mathf.Max(1, pvpState.DecisiveRequiredCount);
        if (pvpDecisiveSelection.Contains(loadoutIndex))
        {
            pvpDecisiveSelection.Remove(loadoutIndex);
        }
        else if (pvpDecisiveSelection.Count < required)
        {
            pvpDecisiveSelection.Add(loadoutIndex);
        }
        else
        {
            return;
        }

        RefreshPvpDecisiveSelectionUi(required);
    }

    private void RefreshPvpDecisiveSelectionUi(int required)
    {
        for (int index = 0; index < pvpHandViews.Count; index++)
        {
            PrototypeCardView view = pvpHandViews[index];
            if ((Object)(object)view == (Object)null)
                continue;

            int loadoutIndex = index < pvpDecisiveViewIndices.Count ? pvpDecisiveViewIndices[index] : -1;
            view.ClearActionOverlay();
            view.SetDraftSelected(pvpDecisiveSelection.Contains(loadoutIndex));
            view.SetInteractable(!pvpDecisiveSubmitted);
        }

        bool ready = !pvpDecisiveSubmitted && pvpDecisiveSelection.Count == required;
        if ((Object)(object)confirmActionButton != (Object)null)
        {
            ((Component)confirmActionButton).gameObject.SetActive(!pvpDecisiveSubmitted);
            confirmActionButton.interactable = ready;
            if ((Object)(object)confirmActionButtonText != (Object)null)
                confirmActionButtonText.text = "CONFERMA SCELTA";
        }
        if ((Object)(object)cancelActionButton != (Object)null)
            ((Component)cancelActionButton).gameObject.SetActive(!pvpDecisiveSubmitted && pvpDecisiveSelection.Count > 0);

        SetMessage(PvpDecisiveMessage());
        UpdateMessageTextLayout();
    }

    private string PvpDecisiveMessage()
    {
        if (pvpState == null)
            return "ROUND DECISIVO";
        if (pvpDecisiveSubmitted)
            return "Selezione inviata: in attesa dell'avversario...";

        int required = Mathf.Max(1, pvpState.DecisiveRequiredCount);
        int total = pvpLoadout?.Count ?? 0;
        return $"ROUND DECISIVO: scegli {required} carte fra tutte e {total} ({pvpDecisiveSelection.Count}/{required}).";
    }

    private bool TryConfirmPvpDecisiveSelection()
    {
        if (!pvpPresentationActive
            || pvpState == null
            || pvpState.Phase != PvpClientPhase.DecisiveSelection
            || pvpDecisiveSubmitted)
        {
            return false;
        }

        int required = Mathf.Max(1, pvpState.DecisiveRequiredCount);
        if (pvpDecisiveSelection.Count != required)
            return false;

        pvpDecisiveSubmitted = true;
        int[] indices = pvpDecisiveSelection.ToArray();
        pvpActions?.SubmitDecisive(indices);
        RefreshPvpDecisiveSelectionUi(required);
        RefreshPvpTurnBanner();
        return true;
    }

    private bool TryClearPvpDecisiveSelection()
    {
        if (!pvpPresentationActive
            || pvpState == null
            || pvpState.Phase != PvpClientPhase.DecisiveSelection
            || pvpDecisiveSubmitted
            || pvpDecisiveSelection.Count == 0)
        {
            return false;
        }

        pvpDecisiveSelection.Clear();
        RefreshPvpDecisiveSelectionUi(Mathf.Max(1, pvpState.DecisiveRequiredCount));
        return true;
    }

    private void ResetPvpDecisiveSelection()
    {
        pvpDecisiveSelection.Clear();
        pvpDecisiveViewIndices.Clear();
        pvpDecisiveSubmitted = false;
    }

    private string BuildPvpLoadoutSignature()
    {
        if (pvpLoadout == null)
            return string.Empty;
        var builder = new System.Text.StringBuilder();
        foreach (LoadoutCardDto card in pvpLoadout)
            builder.Append(card?.definitionId).Append('|');
        return builder.ToString();
    }

    private void SelectPvpDeploymentCard(int handIndex, string definitionId, PrototypeCardView view)
    {
        if (pvpState == null || !pvpState.IsMyDeployTurn || handIndex < 0 || handIndex >= pvpHandViews.Count)
            return;

        pvpPendingDeployHandIndex = handIndex;
        CardDefinition definition = FindPvpDefinition(definitionId);
        pvpPendingDeployCardName = (Object)(object)definition != (Object)null
            ? definition.DisplayName
            : definitionId;

        for (int index = 0; index < pvpHandViews.Count; index++)
        {
            PrototypeCardView handView = pvpHandViews[index];
            if ((Object)(object)handView == (Object)null)
                continue;

            handView.SetDraftSelected(index == pvpPendingDeployHandIndex);
            handView.SetInteractable(true);
        }

        RefreshPvpDeploymentPendingUi();
        int initiative = CurrentPvpDeploymentInitiative();
        SetMessage(initiative > 0
            ? $"INIZIATIVA {initiative}: confermi {pvpPendingDeployCardName} in campo?"
            : $"Confermi {pvpPendingDeployCardName} in campo?");
    }

    private int CurrentPvpDeploymentInitiative()
    {
        if (pvpState == null)
            return 0;

        int deployedCount = pvpState.Boards[0].Count + pvpState.Boards[1].Count;
        return deployedCount >= 0 && deployedCount < pvpState.DeploymentOrder.Count
            ? pvpState.DeploymentOrder[deployedCount].Initiative
            : 0;
    }

    private bool TryConfirmPvpPendingDeployment()
    {
        if (!pvpPresentationActive
            || pvpState == null
            || !pvpState.IsMyDeployTurn
            || pvpPendingDeployHandIndex < 0
            || pvpPendingDeployHandIndex >= pvpHandViews.Count)
        {
            return false;
        }

        PrototypeCardView view = pvpHandViews[pvpPendingDeployHandIndex];
        if ((Object)(object)view == (Object)null)
            return false;

        string definitionId = pvpPendingDeployHandIndex >= 0 && pvpPendingDeployHandIndex < pvpState.Hand.Count
            ? pvpState.Hand[pvpPendingDeployHandIndex].DefinitionId
            : string.Empty;
        CapturePvpDeploymentPose(definitionId, view);
        foreach (PrototypeCardView handView in pvpHandViews)
        {
            if ((Object)(object)handView == (Object)null)
                continue;

            handView.SetSelected(false);
            handView.SetInteractable(false);
        }
        int confirmedIndex = pvpPendingDeployHandIndex;
        pvpPendingDeployHandIndex = -1;
        pvpPendingDeployCardName = string.Empty;
        RefreshPvpDeploymentPendingUi();
        pvpActions?.Deploy(confirmedIndex);
        return true;
    }

    private bool TryCancelPvpPendingDeployment()
    {
        if (!pvpPresentationActive || pvpPendingDeployHandIndex < 0)
            return false;

        pvpPendingDeployHandIndex = -1;
        pvpPendingDeployCardName = string.Empty;
        foreach (PrototypeCardView handView in pvpHandViews)
        {
            if ((Object)(object)handView == (Object)null)
                continue;

            handView.SetSelected(false);
            handView.SetInteractable(pvpState != null && pvpState.IsMyDeployTurn);
        }
        RefreshPvpDeploymentPendingUi();
        RefreshPvpMessage();
        return true;
    }

    private void RefreshPvpDeploymentPendingUi()
    {
        bool showPendingChoice = pvpPresentationActive
            && pvpState != null
            && pvpState.IsMyDeployTurn
            && pvpPendingDeployHandIndex >= 0;
        HidePvpSharedPendingActionButtons();
        RefreshPvpDeploymentCardOverlay(showPendingChoice);
        UpdateMessageTextLayout();
    }

    private void HidePvpSharedPendingActionButtons()
    {
        if ((Object)(object)confirmActionButton != (Object)null)
        {
            ((Component)confirmActionButton).gameObject.SetActive(false);
            confirmActionButton.interactable = false;
        }
        if ((Object)(object)cancelActionButton != (Object)null)
            ((Component)cancelActionButton).gameObject.SetActive(false);
    }

    private void RefreshPvpDeploymentCardOverlay(bool showPendingChoice)
    {
        foreach (PrototypeCardView handView in pvpHandViews)
        {
            if ((Object)(object)handView != (Object)null)
                handView.ClearActionOverlay();
        }

        if (!showPendingChoice
            || pvpPendingDeployHandIndex < 0
            || pvpPendingDeployHandIndex >= pvpHandViews.Count)
        {
            return;
        }

        PrototypeCardView selected = pvpHandViews[pvpPendingDeployHandIndex];
        if ((Object)(object)selected == (Object)null)
            return;

        selected.ShowConfirmInfoActions(
            confirmActionSprite,
            infoActionSprite,
            new UnityAction(() => TryConfirmPvpPendingDeployment()),
            new UnityAction(() => ShowPvpPendingDeploymentInspection()));
    }

    private void ShowPvpPendingDeploymentInspection()
    {
        if (!pvpPresentationActive
            || pvpState == null
            || pvpPendingDeployHandIndex < 0
            || pvpPendingDeployHandIndex >= pvpState.Hand.Count)
        {
            return;
        }

        CardDefinition definition = FindPvpDefinition(pvpState.Hand[pvpPendingDeployHandIndex].DefinitionId);
        if ((Object)(object)definition != (Object)null)
            ShowCardInspection(definition);
    }

    private string BuildPvpHandSignature()
    {
        if (pvpState == null)
            return string.Empty;
        var builder = new System.Text.StringBuilder();
        foreach ((int _, string definitionId) in pvpState.Hand)
            builder.Append(definitionId).Append('|');
        return builder.ToString();
    }

    private void ClearPvpHand()
    {
        foreach (PrototypeCardView view in pvpHandViews)
        {
            if ((Object)(object)view == (Object)null)
                continue;

            GameObject viewObject = ((Component)view).gameObject;
            viewObject.SetActive(false);
            // Sgancia subito dalla riga: così ApplyResponsiveLayout non conta le
            // pedine morte (Destroy avviene solo a fine frame).
            ((Transform)view.RectTransform).SetParent(null, false);
            Object.Destroy((Object)(object)viewObject);
        }
        pvpHandViews.Clear();
        pvpDecisiveViewIndices.Clear();
        pvpHandSignature = string.Empty;
    }

    private void RefreshPvpCardVisuals(IEnumerable<BattleCardState> cards)
    {
        foreach (BattleCardState card in cards)
        {
            if (card == null || (Object)(object)card.View == (Object)null)
                continue;

            RefreshPersistentStatus(card);
            card.View.SetInitiative(card.Initiative);
            card.View.SetSelected(IsPvpActiveLocalCard(card));
            Color? targetHintColor = PvpTargetHintColor(card);
            card.View.SetTargetHint(targetHintColor.HasValue, targetHintColor.GetValueOrDefault());
            card.View.SetTurnAura(IsPvpActiveCard(card), card.BelongsToPlayer);
            int lives = pvpCardLives.TryGetValue(card, out int value) ? value : card.Eliminated ? 0 : 2;
            card.View.SetLifeIcons(lives, 2);
            card.View.SetInteractable(true);
        }
    }

    private Color? PvpTargetHintColor(BattleCardState card)
    {
        if (pvpState == null
            || !pvpState.IsMyBattleTurn
            || pvpTargetMode != PvpPresentationTargetMode.Attack
            || card == null
            || card.BelongsToPlayer
            || card.Eliminated)
        {
            return null;
        }

        BattleCardState active = FindPvpLocalCard(pvpState.ActiveSlot);
        if (active == null)
            return null;

        return ClassMatchup.Compare(active.Card.HeroClass, card.Card.HeroClass) switch
        {
            MatchupResult.Advantage => new Color(0.2f, 1f, 0.35f, 1f),
            MatchupResult.Disadvantage => new Color(1f, 0.22f, 0.18f, 1f),
            _ => new Color(1f, 0.82f, 0.18f, 1f)
        };
    }

    private void ConfigurePvpActionOverlays()
    {
        foreach (BattleCardState card in playerCards.Concat(cpuCards))
            card?.View.ClearActionOverlay();

        if (pvpState == null || !pvpState.IsMyBattleTurn)
            return;

        BattleCardState active = FindPvpLocalCard(pvpState.ActiveSlot);
        if (active == null || active.Eliminated)
            return;

        Sprite attack = LoadSpriteResource("UI/attack_button");
        Sprite ability = LoadSpriteResource("UI/ability_button");
        Sprite attachment = LoadSpriteResource("UI/attachment_button");
        Sprite cancel = LoadSpriteResource("UI/cancel_button");

        if (pvpTargetMode != PvpPresentationTargetMode.None)
        {
            active.View.ShowCancelAction(cancel, new UnityAction(() =>
            {
                pvpTargetMode = PvpPresentationTargetMode.None;
                RenderPvpMatch();
            }));
            return;
        }

        bool canUseAbility = BattlePresentationViewStateMapper.HasActivatableAbility(active.Card.HeroClass)
            && !active.AbilityUsed
            && !active.AbilityArmed;
        bool canAttach = active.Card.Strength >= 2 && active.Card.Strength < 5;
        if (canUseAbility && canAttach)
        {
            active.View.ShowTripleActions(
                attack, new UnityAction(ActivatePvpAttack),
                ability, new UnityAction(ActivatePvpAbility),
                attachment, new UnityAction(ActivatePvpAttachment));
        }
        else if (canUseAbility)
        {
            active.View.ShowDualActions(
                attack, new UnityAction(ActivatePvpAttack),
                ability, new UnityAction(ActivatePvpAbility));
        }
        else if (canAttach)
        {
            active.View.ShowDualActions(
                attack, new UnityAction(ActivatePvpAttack),
                attachment, new UnityAction(ActivatePvpAttachment));
        }
        else
        {
            active.View.ShowClassAction(attack, new UnityAction(ActivatePvpAttack));
        }
    }

    private void ActivatePvpAttack()
    {
        pvpTargetMode = pvpTargetMode == PvpPresentationTargetMode.Attack
            ? PvpPresentationTargetMode.None
            : PvpPresentationTargetMode.Attack;
        RenderPvpMatch();
    }

    private void ActivatePvpAbility()
    {
        BattleCardState active = FindPvpLocalCard(pvpState.ActiveSlot);
        if (active == null || active.AbilityUsed || active.AbilityArmed)
            return;
        if (active != null && active.Card.HeroClass == HeroClass.Warrior)
        {
            pvpActions?.UseAbility(false, pvpState.ActiveSlot);
            pvpTargetMode = PvpPresentationTargetMode.None;
            return;
        }

        pvpTargetMode = PvpPresentationTargetMode.Ability;
        RenderPvpMatch();
    }

    private void ActivatePvpAttachment()
    {
        pvpTargetMode = PvpPresentationTargetMode.Attachment;
        RenderPvpMatch();
    }

    private void HandlePvpPlayerCardClick(int slot)
    {
        BattleCardState card = FindPvpLocalCard(slot);
        if (card == null)
            return;

        if (pvpState != null && pvpState.IsMyBattleTurn && pvpTargetMode == PvpPresentationTargetMode.Attachment)
        {
            pvpActions?.Attach(slot);
            pvpTargetMode = PvpPresentationTargetMode.None;
            return;
        }

        if (pvpState != null && pvpState.IsMyBattleTurn && pvpTargetMode == PvpPresentationTargetMode.Ability)
        {
            pvpActions?.UseAbility(false, slot);
            pvpTargetMode = PvpPresentationTargetMode.None;
            return;
        }

        ShowCardInspection(card);
    }

    private void HandlePvpOpponentCardClick(int slot)
    {
        BattleCardState card = FindPvpOpponentCard(slot);
        if (card == null)
            return;

        if (pvpState != null && pvpState.IsMyBattleTurn)
        {
            if (pvpTargetMode == PvpPresentationTargetMode.Ability)
            {
                pvpActions?.UseAbility(true, slot);
                pvpTargetMode = PvpPresentationTargetMode.None;
                return;
            }

            if (pvpTargetMode == PvpPresentationTargetMode.Attack)
            {
                pvpActions?.Attack(slot);
                pvpTargetMode = PvpPresentationTargetMode.None;
                return;
            }
        }

        ShowCardInspection(card);
    }

    private void RefreshPvpHud()
    {
        if (playerHud == null || cpuHud == null || pvpState == null)
            return;

        ConfigurePlayerHudStandardPresentation(playerHud);

        RefreshCombatantHud(
            playerHud,
            isPlayer: true,
            ResolvePlayerHudDisplayName(),
            pvpState.MyIndex >= 0 ? $"G{pvpState.MyIndex}" : "PVP",
            $"{PvpActiveCount(playerCards)}/{playerCards.Count} ATTIVI",
            playerCards.Count == 0 ? 0f : (float)PvpActiveCount(playerCards) / playerCards.Count,
            Mathf.Max(1, pvpState.VigorDieSides),
            pvpState.Hand.Count,
            0,
            PvpDefeatedCount(playerCards));

        RefreshCombatantHud(
            cpuHud,
            isPlayer: false,
            string.IsNullOrWhiteSpace(pvpState.OpponentName) ? "AVVERSARIO" : pvpState.OpponentName.ToUpperInvariant(),
            $"G{OpponentIndex()}",
            $"{PvpActiveCount(cpuCards)}/{cpuCards.Count} ATTIVI",
            cpuCards.Count == 0 ? 0f : (float)PvpActiveCount(cpuCards) / cpuCards.Count,
            Mathf.Max(1, pvpState.VigorDieSides),
            cpuCards.Count,
            0,
            PvpDefeatedCount(cpuCards));

        if ((Object)(object)topInfoText != (Object)null)
            topInfoText.text = $"PVP  |  ROUND {pvpState.MatchRound}  |  VIGORE D{pvpState.VigorDieSides}  |  TU {pvpState.Wins[pvpState.MyIndex]} - {pvpState.Wins[OpponentIndex()]} {pvpState.OpponentName.ToUpperInvariant()}";
    }

    private void RefreshPvpTimeline()
    {
        RestoreTimelineBaseRect();
        List<string> previousTimelineOrder = new List<string>(pvpTimelineOrderKeys);
        if ((Object)(object)initiativeTimelineRoot == (Object)null || pvpState == null)
        {
            StopTimelineSlideAnimation();
            ClearInitiativeTimeline();
            pvpTimelineOrderKeys.Clear();
            ResizeTimelineTiles(0);
            return;
        }

        if (pvpState.Phase == PvpClientPhase.Deployment)
        {
            StopTimelineSlideAnimation();
            ClearInitiativeTimeline();
            pvpTimelineOrderKeys.Clear();
            List<PvpClientDeploymentToken> deploymentTokens = pvpState.DeploymentOrder
                .OrderBy(token => token.Order)
                .ToList();
            int activeOrder = CurrentPvpDeploymentOrder(deploymentTokens);
            float timelineTileSize = GetTimelineTileSize(deploymentTokens.Count);
            foreach (PvpClientDeploymentToken token in deploymentTokens)
            {
                AddPvpTimelineTile(token.Player, token.Initiative, token.Order == activeOrder, timelineTileSize);
                pvpTimelineOrderKeys.Add($"deploy:{token.Player}:{token.Order}:{token.Initiative}");
            }
            ResizeTimelineTiles(deploymentTokens.Count);
            return;
        }

        List<BattleCardState> timelineCards = playerCards
            .Concat(cpuCards)
            .Where(card => card != null && !card.Eliminated)
            .OrderByDescending(card => card.Initiative)
            .ThenByDescending(card => PvpTieBreakerFor(card))
            .ToList();
        List<BattleCardState> rotatedTimelineCards = RotatePvpTimelineFromActive(timelineCards).ToList();
        List<string> currentTimelineOrder = rotatedTimelineCards.Select(PvpTimelineKeyFor).ToList();
        if (pvpTimelineSlideRoutine != null && previousTimelineOrder.SequenceEqual(currentTimelineOrder))
            return;

        StopTimelineSlideAnimation();
        ClearInitiativeTimeline();
        pvpTimelineOrderKeys.Clear();

        float battleTileSize = GetTimelineTileSize(timelineCards.Count);
        foreach (BattleCardState card in rotatedTimelineCards)
        {
            AddPvpTimelineTile(card.BelongsToPlayer ?pvpState.MyIndex : OpponentIndex(), card.Initiative, IsPvpActiveCard(card), battleTileSize, card);
            string timelineKey = PvpTimelineKeyFor(card);
            currentTimelineOrder.Add(timelineKey);
            pvpTimelineOrderKeys.Add(timelineKey);
        }
        ResizeTimelineTiles(timelineCards.Count);
        TryPlayPvpTimelineSlide(previousTimelineOrder, currentTimelineOrder);
    }

    private int CurrentPvpDeploymentOrder(IReadOnlyList<PvpClientDeploymentToken> deploymentTokens)
    {
        if (pvpState == null || deploymentTokens == null || deploymentTokens.Count == 0)
            return -1;

        int deployedCount = pvpState.Boards[0].Count + pvpState.Boards[1].Count;
        return deployedCount >= 0 && deployedCount < deploymentTokens.Count
            ? deploymentTokens[deployedCount].Order
            : -1;
    }

    private IEnumerable<BattleCardState> RotatePvpTimelineFromActive(IReadOnlyList<BattleCardState> cards)
    {
        if (cards == null || cards.Count == 0)
            yield break;

        int startIndex = 0;
        for (int index = 0; index < cards.Count; index++)
        {
            if (IsPvpActiveCard(cards[index]))
            {
                startIndex = index;
                break;
            }
        }

        for (int offset = 0; offset < cards.Count; offset++)
            yield return cards[(startIndex + offset) % cards.Count];
    }

    private int PvpTieBreakerFor(BattleCardState card)
    {
        if (pvpState == null || card == null)
            return 0;

        int player = card.BelongsToPlayer ? pvpState.MyIndex : OpponentIndex();
        int slot = pvpCardSlots.TryGetValue(card, out int serverSlot) ? serverSlot : 0;
        return player * 100 + slot;
    }

    private string PvpTimelineKeyFor(BattleCardState card)
    {
        if (card == null)
            return string.Empty;

        int player = pvpState != null && card.BelongsToPlayer ? pvpState.MyIndex : OpponentIndex();
        int slot = pvpCardSlots.TryGetValue(card, out int serverSlot) ? serverSlot : 0;
        string cardId = pvpCardIds.TryGetValue(card, out string id) ? id : card.Card?.Name ?? "card";
        return $"{player}:{slot}:{cardId}";
    }

    private void TryPlayPvpTimelineSlide(IReadOnlyList<string> previousOrder, IReadOnlyList<string> currentOrder)
    {
        if ((Object)(object)initiativeTimelineRoot == (Object)null || previousOrder == null || currentOrder == null)
            return;
        if (previousOrder.Count < 2 || currentOrder.Count < 1 || currentOrder.Count > previousOrder.Count)
            return;
        if (previousOrder.SequenceEqual(currentOrder) || previousOrder[0] == currentOrder[0])
            return;

        int finishedNewIndex = IndexOfTimelineKey(currentOrder, previousOrder[0]);
        if (finishedNewIndex != currentOrder.Count - 1)
            return;

        pvpTimelineSlideRoutine = StartCoroutine(PlayPvpTimelineSlide(previousOrder.ToList(), currentOrder.ToList()));
    }

    private void StopTimelineSlideAnimation()
    {
        if (pvpTimelineSlideRoutine != null)
        {
            StopCoroutine(pvpTimelineSlideRoutine);
            pvpTimelineSlideRoutine = null;
        }

        RestoreTimelineLayoutAfterSlide();
    }

    private void RestoreTimelineLayoutAfterSlide()
    {
        if ((Object)(object)initiativeTimelineRoot == (Object)null)
            return;

        for (int index = ((Transform)initiativeTimelineRoot).childCount - 1; index >= 0; index--)
        {
            Transform child = ((Transform)initiativeTimelineRoot).GetChild(index);
            if (child.name == "Timeline Slide VFX")
            {
                ((Component)child).gameObject.SetActive(false);
                Object.Destroy((Object)(object)((Component)child).gameObject);
                continue;
            }

            CanvasGroup canvasGroup = ((Component)child).GetComponent<CanvasGroup>();
            if ((Object)(object)canvasGroup != (Object)null)
                canvasGroup.alpha = 1f;
            child.localScale = Vector3.one;
            child.localRotation = Quaternion.identity;
        }

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator PlayPvpTimelineSlide(IReadOnlyList<string> previousOrder, IReadOnlyList<string> currentOrder)
    {
        Canvas.ForceUpdateCanvases();
        int count = currentOrder.Count;
        float tileSize = GetTimelineTileSize(count);
        float previousTileSize = GetTimelineTileSize(previousOrder.Count);
        Vector2[] previousPositions = GetTimelineLocalPositions(previousOrder.Count, previousTileSize);
        Vector2[] targetPositions = GetTimelineLocalPositions(count, tileSize);
        if (targetPositions.Length < 2)
        {
            pvpTimelineSlideRoutine = null;
            yield break;
        }

        List<RectTransform> tiles = new List<RectTransform>(count);
        List<Vector2> startPositions = new List<Vector2>(count);
        for (int childIndex = 0; childIndex < ((Transform)initiativeTimelineRoot).childCount && tiles.Count < count; childIndex++)
        {
            Transform child = ((Transform)initiativeTimelineRoot).GetChild(childIndex);
            if (!((Component)child).gameObject.activeSelf || IsTransientTimelineObject(child))
                continue;

            int index = tiles.Count;
            RectTransform tile = (RectTransform)(object)child;
            tiles.Add(tile);
            int previousIndex = IndexOfTimelineKey(previousOrder, currentOrder[index]);
            startPositions.Add(previousIndex >= 0 && previousIndex < previousPositions.Length ?previousPositions[previousIndex] : targetPositions[index]);
            tile.anchoredPosition = startPositions[index];
            tile.sizeDelta = new Vector2(tileSize, tileSize);
        }

        if (tiles.Count < count)
        {
            ResizeTimelineTiles(count);
            pvpTimelineSlideRoutine = null;
            yield break;
        }

        RectTransform finishedTile = tiles[tiles.Count - 1];
        int finishedPreviousIndex = IndexOfTimelineKey(previousOrder, currentOrder[count - 1]);
        Vector2 finishedStart = finishedPreviousIndex >= 0 && finishedPreviousIndex < previousPositions.Length
            ?previousPositions[finishedPreviousIndex]
            : targetPositions[0];
        Vector2 finishedEnd = targetPositions[targetPositions.Length - 1];
        bool vertical = IsTimelineVerticalLayout();
        float orbitDistance = Mathf.Max(tileSize * 1.65f, 72f);
        Vector2 outward = vertical ? Vector2.left : Vector2.up;
        GameObject aura = CreateTimelineSlideAura();

        float duration = 0.62f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            for (int index = 0; index < tiles.Count; index++)
            {
                RectTransform tile = tiles[index];
                if ((Object)(object)tile == (Object)null)
                    continue;

                if (tile == finishedTile)
                {
                    Vector2 line = Vector2.LerpUnclamped(finishedStart, finishedEnd, eased);
                    float arc = Mathf.Sin(t * Mathf.PI) * orbitDistance;
                    tile.anchoredPosition = line + outward * arc;
                    tile.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.94f, t);
                    tile.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(vertical ? -18f : 18f, 360f, eased));
                }
                else
                {
                    tile.anchoredPosition = Vector2.LerpUnclamped(startPositions[index], targetPositions[index], eased);
                    tile.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.05f);
                    tile.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * (vertical ? 2.5f : -2.5f));
                }
            }

            AnimateTimelineSlideAura(aura, finishedStart, finishedEnd, outward, orbitDistance, t);
            yield return null;
        }

        for (int index = 0; index < count; index++)
        {
            RectTransform tile = tiles[index];
            if ((Object)(object)tile == (Object)null)
                continue;
            tile.anchoredPosition = targetPositions[index];
            tile.localScale = Vector3.one;
            tile.localRotation = Quaternion.identity;
        }

        if ((Object)(object)aura != (Object)null)
            Object.Destroy(aura);
        pvpTimelineSlideRoutine = null;
    }

    private GameObject CreateTimelineSlideAura()
    {
        if ((Object)(object)initiativeTimelineRoot == (Object)null)
            return null;

        GameObject root = new GameObject("Timeline Slide VFX", typeof(RectTransform));
        root.transform.SetParent((Transform)(object)initiativeTimelineRoot, false);
        root.transform.SetAsLastSibling();
        LayoutElement rootLayout = root.AddComponent<LayoutElement>();
        rootLayout.ignoreLayout = true;
        RectTransform rootRect = (RectTransform)(object)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;

        for (int i = 0; i < 11; i++)
        {
            Image spark = CreateImage("Rune Spark", root.transform, new Color(1f, 0.78f, 0.24f, 0.88f));
            spark.raycastTarget = false;
            RectTransform sparkRect = spark.rectTransform;
            float size = i % 3 == 0 ? 10f : 6f;
            sparkRect.sizeDelta = new Vector2(size, size);
            sparkRect.localRotation = Quaternion.Euler(0f, 0f, i * 32.7f);
        }

        return root;
    }

    private static int IndexOfTimelineKey(IReadOnlyList<string> order, string key)
    {
        if (order == null)
            return -1;

        for (int index = 0; index < order.Count; index++)
        {
            if (order[index] == key)
                return index;
        }

        return -1;
    }

    private void AnimateTimelineSlideAura(GameObject aura, Vector2 start, Vector2 end, Vector2 outward, float orbitDistance, float t)
    {
        if ((Object)(object)aura == (Object)null)
            return;

        RectTransform root = (RectTransform)(object)aura.transform;
        Vector2 center = Vector2.LerpUnclamped(start, end, 1f - Mathf.Pow(1f - t, 3f)) + outward * Mathf.Sin(t * Mathf.PI) * orbitDistance;
        root.anchoredPosition = center;
        root.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.35f, t);
        root.localRotation = Quaternion.Euler(0f, 0f, t * 420f);

        for (int i = 0; i < aura.transform.childCount; i++)
        {
            RectTransform spark = (RectTransform)(object)aura.transform.GetChild(i);
            float angle = (i / Mathf.Max(1f, aura.transform.childCount)) * Mathf.PI * 2f + t * Mathf.PI * 3f;
            float radius = Mathf.Lerp(12f, 42f + i * 1.8f, Mathf.Sin(t * Mathf.PI));
            spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Image image = ((Component)spark).GetComponent<Image>();
            if ((Object)(object)image != (Object)null)
                image.color = new Color(1f, Mathf.Lerp(0.54f, 0.95f, t), 0.18f, Mathf.Sin(t * Mathf.PI) * 0.85f);
        }
    }

    private void AddPvpTimelineTile(int player, int initiative, bool active, float timelineTileSize, BattleCardState inspectedState = null)
    {
        bool belongsToPlayer = player == pvpState.MyIndex;
        Image tile = CreateImage("PVP Timeline", (Transform)(object)initiativeTimelineRoot,
            active ? new Color(0.72f, 0.48f, 0.12f, 0.98f)
                : belongsToPlayer ? new Color(0.04f, 0.42f, 0.48f, 0.95f) : new Color(0.5f, 0.1f, 0.12f, 0.95f));
        LayoutElement layout = ((Component)tile).gameObject.AddComponent<LayoutElement>();
        ConfigureTimelineTileLayout(layout, timelineTileSize);
        if (inspectedState != null)
        {
            Outline factionOutline = ((Component)tile).gameObject.AddComponent<Outline>();
            factionOutline.effectColor = belongsToPlayer ?new Color(0.1f, 0.82f, 1f, 0.95f) : new Color(1f, 0.16f, 0.12f, 0.95f);
            factionOutline.effectDistance = new Vector2(2.2f, -2.2f);
            Image portrait = CreateImage("Portrait", ((Component)tile).transform, Color.white);
            portrait.sprite = inspectedState.Definition.Artwork;
            portrait.preserveAspect = false;
            portrait.raycastTarget = false;
            SetRect(portrait.rectTransform, new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f));
            tile.raycastTarget = true;
            Button button = ((Component)tile).gameObject.AddComponent<Button>();
            button.targetGraphic = (Graphic)(object)tile;
            ((UnityEvent)button.onClick).AddListener((UnityAction)delegate
            {
                if (CanInspectBattleCard(inspectedState))
                {
                    ShowCardInspection(inspectedState);
                }
            });
            return;
        }
        Text text = CreateText("Turn", ((Component)tile).transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.text = belongsToPlayer ? "TU" : "AVV";
        Stretch(text.rectTransform, 2f);
    }

    private void RefreshPvpMessage()
    {
        string message = pvpState.Phase switch
        {
            PvpClientPhase.DecisiveSelection => PvpDecisiveMessage(),
            PvpClientPhase.Deployment when pvpState.IsMyDeployTurn => "Scegli una carta dalla tua mano da schierare.",
            PvpClientPhase.Deployment => "L'avversario sta schierando...",
            PvpClientPhase.Battle when pvpState.IsMyBattleTurn => "Il tuo turno: scegli un'azione sulla pedina attiva.",
            PvpClientPhase.Battle => "Turno dell'avversario...",
            PvpClientPhase.Finished when !pvpState.EndedByForfeit && !pvpState.HasEliminatedFormation =>
                pvpState.Log.Count > 0 ? pvpState.Log[pvpState.Log.Count - 1] : "Risultato in verifica...",
            PvpClientPhase.Finished => pvpState.Winner == pvpState.MyIndex ? "VITTORIA!" : "SCONFITTA",
            _ => pvpState.Log.Count > 0 ? pvpState.Log[pvpState.Log.Count - 1] : "In attesa..."
        };
        if ((Object)(object)messageText != (Object)null)
            messageText.text = message;
    }

    /// <summary>
    /// Regia del match: consuma gli eventi del server in sequenza e li recita
    /// con le animazioni della campagna. Alla fine riallinea la scena allo
    /// stato autoritativo con un render.
    /// </summary>
    private IEnumerator PlayPvpRegiaRoutine()
    {
        while (pvpPendingEvents.Count > 0)
        {
            if (!pvpPresentationActive)
            {
                pvpPendingEvents.Clear();
                break;
            }

            BattlePresentationEvent battleEvent = pvpPendingEvents.Dequeue();
            switch (battleEvent.Type)
            {
                case "RoundStarted":
                {
                    pvpDeploymentIntroPlayed = false;
                    ResetPvpDecisiveSelection();
                    RenderPvpMatch();
                    break;
                }

                case "DeployTurn":
                {
                    // Al primo turno di schieramento lo stato contiene già tutte le
                    // iniziative: prima le carte entrano in mano, poi il tiro dadi.
                    if (!pvpDeploymentIntroPlayed
                        && pvpState != null
                        && pvpState.Phase == PvpClientPhase.Deployment
                        && pvpState.DeploymentOrder.Count > 0)
                    {
                        pvpDeploymentIntroPlayed = true;
                        yield return PlayPvpDeploymentIntroRoutine();
                    }
                    break;
                }

                case "CardDeployed":
                {
                    RenderPvpMatch();
                    Canvas.ForceUpdateCanvases();
                    PlayPawnEnteringBattlefieldSfx(FindPvpDefinition(battleEvent.CardId));
                    PlayPvpDeploymentAnimation(battleEvent);
                    yield return WaitForCardInspectionPause(
                        Mathf.Max(0.2f, configuration.Animation.CardDeployDuration * 0.7f));
                    break;
                }

                case "AttackResolved":
                {
                    yield return PlayPvpDuelRoutine(battleEvent);
                    RenderPvpMatch();
                    break;
                }

                case "ProtectionTriggered":
                {
                    if (battleEvent.Redirected)
                    {
                        pvpPendingPaladinRedirectPlayer = battleEvent.Player;
                        pvpPendingPaladinRedirectSlot = battleEvent.Slot;
                    }
                    else
                    {
                        ClearPendingPaladinRedirect();
                    }
                    break;
                }

                case "AbilityUsed":
                {
                    yield return PlayPvpAbilityUsedRoutine(battleEvent);
                    RenderPvpMatch();
                    break;
                }

                case "SpiritExpired":
                case "AttachmentApplied":
                {
                    BattleCardState fallen = FindPvpCardForPlayerSlot(battleEvent.Player, battleEvent.Slot);
                    BattleCardState target = FindPvpCardForPlayerSlot(battleEvent.TargetPlayer, battleEvent.TargetSlot);
                    if (string.Equals(battleEvent.Type, "AttachmentApplied", System.StringComparison.Ordinal)
                        && fallen != null
                        && target != null
                        && (Object)(object)fallen.View != (Object)null
                        && (Object)(object)target.View != (Object)null
                        && (Object)(object)battleAnimationPlayer != (Object)null)
                    {
                        yield return battleAnimationPlayer.PlayTargetLine(
                            fallen.View,
                            target.View,
                            AttachmentTargetLineColor);
                    }
                    if (fallen != null && (Object)(object)fallen.View != (Object)null)
                        yield return fallen.View.PlayDefeatAnimation(killerHeroClass: fallen.Card.HeroClass);
                    RenderPvpMatch();
                    break;
                }

                case "CardRevived":
                {
                    RenderPvpMatch();
                    BattleCardState revived = FindPvpCardForPlayerSlot(battleEvent.Player, battleEvent.Slot);
                    if (revived != null && (Object)(object)revived.View != (Object)null)
                    {
                        revived.View.SetAlpha(1f);
                        if ((Object)(object)battleAnimationPlayer != (Object)null)
                            yield return battleAnimationPlayer.PlayNecromancerReviveSkullConvergence(revived.View);
                        revived.View.PlayRevealAnimation(configuration.Animation.CpuCardRevealDuration);
                    }
                    break;
                }
            }
        }

        RenderPvpMatch();
        pvpRegiaRoutine = null;
    }

    /// <summary>
    /// Intro dello schieramento come in campagna: le carte entrano in mano una
    /// alla volta, poi i dadi iniziativa rotolano e si fermano sui valori già
    /// decisi dal server, volando infine sulla timeline.
    /// </summary>
    private IEnumerator PlayPvpDeploymentIntroRoutine()
    {
        Debug.Log($"[PvP] >>> INTRO schieramento (tiro iniziativa) - round {pvpState?.MatchRound}. Se questo log compare 2+ volte per round, è il bug del doppio tiro.");
        RenderPvpMatch();
        ClearInitiativeTimeline();
        ResizeTimelineTiles(0);
        SetMessage("Le carte entrano in mano, poi iniziera' lo schieramento.");
        yield return PlayPvpHandEntranceRoutine();

        // Riusa la coroutine campagna: deploymentOrder viene riempita coi token
        // del server e svuotata subito dopo per non sporcare lo stato campagna.
        deploymentOrder.Clear();
        foreach (PvpClientDeploymentToken token in pvpState.DeploymentOrder.OrderBy(entry => entry.Order))
            deploymentOrder.Add(new DeploymentToken(token.Player == pvpState.MyIndex, token.Initiative, token.Order));
        SetMessage("Iniziativa pronta: i valori piu' bassi schierano per primi.");
        Debug.Log($"[PvP] ###### SUONO DADI ORA (tiro iniziativa) - board {pvpState.Boards[0].Count}-{pvpState.Boards[1].Count} ######");
        yield return PlayDeploymentInitiativeDiceRoll(configuration.Gameplay.InitiativeDieSides, "AVV");
        Debug.Log("[PvP] ###### FINE TIRO DADI ######");
        deploymentOrder.Clear();

        RenderPvpMatch();
    }

    private IEnumerator PlayPvpAbilityUsedRoutine(BattlePresentationEvent battleEvent)
    {
        if (battleEvent == null || !battleEvent.HasAbilityClass)
            yield break;

        if (battleEvent.AbilityClass != HeroClass.Assassin
            && battleEvent.AbilityClass != HeroClass.Mage
            && battleEvent.AbilityClass != HeroClass.Hunter
            && battleEvent.AbilityClass != HeroClass.Paladin
            && battleEvent.AbilityClass != HeroClass.Priest)
            yield break;

        BattleCardState caster = FindPvpCardForPlayerSlot(battleEvent.Player, battleEvent.Slot);
        BattleCardState target = FindPvpCardForPlayerSlot(battleEvent.TargetPlayer, battleEvent.TargetSlot);
        if (target == null
            || (Object)(object)target.View == (Object)null
            || (Object)(object)battleAnimationPlayer == (Object)null)
        {
            yield break;
        }

        ClearPvpActionSelectionForDuel();
        if (caster != null && (Object)(object)caster.View != (Object)null)
        {
            yield return battleAnimationPlayer.PlayTargetLine(
                caster.View,
                target.View,
                AbilityTargetLineColor);
        }
        PlayClassAbilitySfx(battleEvent.AbilityClass);
        if (battleEvent.AbilityClass == HeroClass.Assassin)
            yield return battleAnimationPlayer.PlayAssassinInhibitSmoke(target.View);
        else if (battleEvent.AbilityClass == HeroClass.Mage)
        {
            int baseDieSides = pvpState != null ? pvpState.VigorDieSides : configuration.Gameplay.VigorDieSides;
            int appliedSteps = Mathf.Max(1, battleEvent.AbilityMagnitude);
            int currentSteps = Mathf.Max(appliedSteps, target.PendingVigorStepPenalty);
            int startDieSides = LowerVigorDieBySteps(baseDieSides, Mathf.Max(0, currentSteps - appliedSteps));
            int endDieSides = LowerVigorDieBySteps(baseDieSides, currentSteps);
            yield return PlayMageVigorConstellation(
                target,
                startDieSides,
                endDieSides);
        }
        else if (battleEvent.AbilityClass == HeroClass.Hunter)
            yield return battleAnimationPlayer.PlayHunterMarkReticle(target.View);
        else if (battleEvent.AbilityClass == HeroClass.Paladin)
            yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(target.View);
        else if (caster != null && (Object)(object)caster.View != (Object)null)
            yield return battleAnimationPlayer.PlayPriestBlessing(caster.View, target.View, battleEvent.AbilityMagnitude);
    }

    private IEnumerator PlayPvpHandEntranceRoutine()
    {
        if (pvpHandViews.Count == 0
            || (Object)(object)playerHandRow == (Object)null
            || (Object)(object)safeAreaRoot == (Object)null)
        {
            yield break;
        }

        foreach (PrototypeCardView handView in pvpHandViews)
        {
            if ((Object)(object)handView != (Object)null)
            {
                handView.SetInteractable(false);
                handView.SetAlpha(0f);
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
        Rect safeBounds = safeAreaRoot.rect;

        if (initialDelay > 0f)
            yield return WaitForCardInspectionPause(initialDelay);

        for (int index = 0; index < pvpHandViews.Count; index++)
        {
            PrototypeCardView realView = pvpHandViews[index];
            if ((Object)(object)realView == (Object)null)
                continue;

            Vector2 target = RectCenterInSafeArea(realView.RectTransform);
            Quaternion targetRotation =
                Quaternion.Inverse(((Transform)safeAreaRoot).rotation) * ((Transform)realView.RectTransform).rotation;
            Vector2 size = RectSizeInSafeArea(realView.RectTransform);
            draftEntranceAnimatingViews.Add(realView);

            GameObject overlayObject = Object.Instantiate(
                ((Component)realView).gameObject, (Transform)(object)safeAreaRoot, false);
            NormalizeDraftEntranceClone(overlayObject);
            PrototypeCardView overlayView = overlayObject.GetComponent<PrototypeCardView>();
            Button overlayButton = overlayObject.GetComponent<Button>();
            if ((Object)(object)overlayButton != (Object)null)
                overlayButton.interactable = false;
            overlayView.SetLayoutIgnored(true);
            draftEntranceOverlayObjects.Add(overlayObject);

            RectTransform animatedRect = overlayView.RectTransform;
            animatedRect.anchorMin = new Vector2(0.5f, 0.5f);
            animatedRect.anchorMax = new Vector2(0.5f, 0.5f);
            animatedRect.pivot = new Vector2(0.5f, 0.5f);
            animatedRect.sizeDelta = size;
            Vector2 start = new Vector2(
                safeBounds.xMax + Mathf.Max(1f, size.x) * 0.9f,
                Mathf.Lerp(0f, target.y, 0.35f));
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
                animatedRect.anchoredPosition = Vector2.LerpUnclamped(start, Vector2.zero, progress);
                ((Transform)animatedRect).localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, entranceScale, progress);
                overlayView.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (enterDuration * 0.42f))));
                yield return null;
            }
            animatedRect.anchoredPosition = Vector2.zero;
            ((Transform)animatedRect).localScale = Vector3.one * entranceScale;
            overlayView.SetAlpha(1f);
            if (holdDuration > 0f)
                yield return WaitForCardInspectionPause(holdDuration);

            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settleDuration));
                animatedRect.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, target, progress);
                ((Transform)animatedRect).localRotation =
                    Quaternion.SlerpUnclamped(Quaternion.identity, targetRotation, progress);
                ((Transform)animatedRect).localScale = Vector3.one * Mathf.LerpUnclamped(entranceScale, 1f, progress);
                yield return null;
            }

            realView.SetAlpha(1f);
            draftEntranceAnimatingViews.Remove(realView);
            draftEntranceOverlayObjects.Remove(overlayObject);
            Object.Destroy(overlayObject);
            if (betweenCardsDelay > 0f && index < pvpHandViews.Count - 1)
                yield return WaitForCardInspectionPause(betweenCardsDelay);
        }

        ApplyResponsiveLayout();
        Canvas.ForceUpdateCanvases();
        ApplyHandFan();
        // NON sbloccare qui: dopo l'ingresso viene ancora il tiro iniziativa. La mano
        // si sblocca solo col render finale dell'intro (dopo il tiro), così la sequenza
        // è entrano → tiro → schieri e il suono dei dadi non si sovrappone allo schieramento.
        foreach (PrototypeCardView handView in pvpHandViews)
        {
            if ((Object)(object)handView != (Object)null)
                handView.SetInteractable(false);
        }
    }

    private IEnumerator PlayPvpDuelRoutine(BattlePresentationEvent battleEvent)
    {
        BattleCardState attacker = FindPvpCardForPlayerSlot(battleEvent.Player, battleEvent.Slot);
        BattleCardState defender = FindPvpCardForPlayerSlot(battleEvent.TargetPlayer, battleEvent.TargetSlot);
        if (attacker == null || defender == null
            || (Object)(object)attacker.View == (Object)null
            || (Object)(object)defender.View == (Object)null
            || (Object)(object)battleAnimationPlayer == (Object)null)
        {
            yield break;
        }

        VigorRollResult attackerRoll = BattlePresentationAnimationPlayer.BuildRoll(
            battleEvent.AttackerDieSides,
            battleEvent.AttackerRollFirst,
            battleEvent.AttackerRollSecond,
            battleEvent.AttackerRollHasSecond,
            battleEvent.AttackerRollSelected,
            battleEvent.AttackerRollSelectionMode,
            battleEvent.AttackerRollFirstBeforeReroll,
            battleEvent.AttackerRollSecondBeforeReroll);
        VigorRollResult defenderRoll = BattlePresentationAnimationPlayer.BuildRoll(
            battleEvent.DefenderDieSides,
            battleEvent.DefenderRollFirst,
            battleEvent.DefenderRollSecond,
            battleEvent.DefenderRollHasSecond,
            battleEvent.DefenderRollSelected,
            battleEvent.DefenderRollSelectionMode,
            battleEvent.DefenderRollFirstBeforeReroll,
            battleEvent.DefenderRollSecondBeforeReroll);

        ClearPvpActionSelectionForDuel();
        if (battleEvent.Certainty == CombatCertainty.Impossible)
        {
            ShowPvpAttackSummary(battleEvent, attacker, defender, attackerRoll, defenderRoll);
            ClearPendingPaladinRedirect();
            yield break;
        }

        RectTransform duelRoot = (Object)(object)safeAreaRoot != (Object)null ? safeAreaRoot : canvasRect;
        Vector3 attackerPoint = PvpDuelWorldPoint(duelRoot, 0.30f);
        Vector3 defenderPoint = PvpDuelWorldPoint(duelRoot, 0.58f);
        yield return battleAnimationPlayer.PlayTargetLine(
            attacker.View,
            defender.View,
            AttackTargetLineColor);
        if (ShouldReplayPaladinRedirectProtection(defender))
        {
            PlayClassAbilitySfx(HeroClass.Paladin);
            yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(defender.View);
        }
        ClearPendingPaladinRedirect();
        battleAnimationPlayer.PlayDuelAtPoints(
            configuration,
            diceCatalog,
            attacker.View,
            defender.View,
            attackerRoll,
            defenderRoll,
            battleEvent.AttackerDieSides,
            battleEvent.DefenderDieSides,
            battleEvent.Player == pvpState.MyIndex ? "ATTACCO" : "ATTACCO AVV",
            battleEvent.TargetPlayer == pvpState.MyIndex ? "TUA DIFESA" : "DIFESA",
            battleEvent.DefenderEliminated,
            attackerPoint,
            defenderPoint,
            () =>
            {
                ShowPvpAttackSummary(battleEvent, attacker, defender, attackerRoll, defenderRoll);
                PlayPvpAttackResolvedSfx(battleEvent);
            },
            () => SetMessagePanelHiddenForDuel(hidden: true),
            () => SetMessagePanelHiddenForDuel(hidden: false),
            defenderHit: battleEvent.DefenderLostLife || battleEvent.DefenderEliminated,
            attackerTotal: battleEvent.AttackerTotal,
            defenderTotal: battleEvent.DefenderTotal,
            attackerHeroClass: battleEvent.HasHeroClass ? battleEvent.HeroClass : attacker.Card.HeroClass);
        yield return new WaitWhile(() =>
            (Object)(object)battleAnimationPlayer != (Object)null && battleAnimationPlayer.IsBusy);
    }

    private static Vector3 PvpDuelWorldPoint(RectTransform root, float normalizedX)
    {
        if ((Object)(object)root == (Object)null)
            return Vector3.zero;

        Rect rect = root.rect;
        float x = Mathf.Lerp(rect.xMin, rect.xMax, normalizedX);
        float y = Mathf.Lerp(rect.yMin, rect.yMax, 0.51f);
        return root.TransformPoint(new Vector3(x, y, 0f));
    }

    private void ClearPvpActionSelectionForDuel()
    {
        pvpTargetMode = PvpPresentationTargetMode.None;
        foreach (BattleCardState card in playerCards.Concat(cpuCards))
            card?.View?.ClearActionOverlay();
    }

    private void ShowPvpAttackSummary(
        BattlePresentationEvent battleEvent,
        BattleCardState attacker,
        BattleCardState defender,
        VigorRollResult attackerRoll,
        VigorRollResult defenderRoll)
    {
        if (battleEvent == null || attacker == null || defender == null)
            return;

        if (battleEvent.Certainty == CombatCertainty.Impossible)
        {
            var impossibleModifiers = CombatModifiers.None;
            string impossibleMessage = FormatImpossibleAttackDetailed(
                attacker,
                defender,
                battleEvent.AttackerDieSides,
                battleEvent.DefenderDieSides,
                impossibleModifiers) + " Resiste.";
            AppendLog(impossibleMessage);
            SetBattlefieldMessage($"{defender.Card.Name} resiste all'attacco di {attacker.Card.Name}.");
            return;
        }

        if (attackerRoll.FirstRoll <= 0 || defenderRoll.FirstRoll <= 0)
        {
            string incompleteMessage =
                $"Evento PvP incompleto: mancano i tiri per {attacker.Card.Name} contro {defender.Card.Name} " +
                $"(certezza: {battleEvent.Certainty}, D{battleEvent.AttackerDieSides}/D{battleEvent.DefenderDieSides}).";
            AppendLog(incompleteMessage);
            SetBattlefieldMessage("Errore replay PvP: tiri mancanti.");
            return;
        }

        int attackerTotal = battleEvent.AttackerTotal != 0
            ? battleEvent.AttackerTotal
            : attacker.Card.Strength + attackerRoll.SelectedRoll;
        int defenderTotal = battleEvent.DefenderTotal != 0
            ? battleEvent.DefenderTotal
            : defender.Card.Strength + defenderRoll.SelectedRoll;
        var result = new CombatResult(attackerRoll, defenderRoll, attackerTotal, defenderTotal);
        var modifiers = new CombatModifiers(
            attackerRoll.HasSecondRoll && attackerRoll.SelectionMode == VigorSelectionMode.Sum,
            defenderRoll.HasSecondRoll,
            attackerFlatBonus: attackerTotal - attacker.Card.Strength - attackerRoll.SelectedRoll,
            defenderFlatBonus: defenderTotal - defender.Card.Strength - defenderRoll.SelectedRoll);
        string actor = battleEvent.Player == pvpState?.MyIndex ? "TU" : "AVVERSARIO";
        string message = FormatResultDetailed(actor, attacker, defender, result, modifiers);
        if (battleEvent.Overkill)
            message = "OVERKILL! " + message;
        AppendLog(message);
        SetBattlefieldMessage((battleEvent.Overkill ? "OVERKILL! " : string.Empty) + FormatResultSummary(attacker, defender, result));
    }

    private void PlayPvpAttackResolvedSfx(BattlePresentationEvent battleEvent)
    {
        if (battleEvent == null)
            return;

        bool hit = battleEvent.DefenderLostLife || battleEvent.DefenderEliminated;
        if (battleEvent.HasHeroClass && !(hit && battleEvent.HeroClass == HeroClass.Rogue))
            battleSfx?.PlayAttackResult(
                battleEvent.HeroClass,
                hit);
        if (battleEvent.DefenderEliminated && !battleEvent.BecameSpirit)
            battleSfx?.PlayDeath();
    }

    private void PlayPvpDeploymentAnimation(BattlePresentationEvent battleEvent)
    {
        BattleCardState card = FindPvpCardForPlayerSlot(battleEvent.Player, battleEvent.Slot);
        if (card == null || (Object)(object)card.View == (Object)null)
            return;

        if (pvpState != null
            && battleEvent.Player == pvpState.MyIndex
            && TryPopPvpDeploymentPose(battleEvent.CardId, out PvpDeploymentPose pose))
        {
            card.View.PlayDeploymentAnimation(pose.Position, pose.Rotation, configuration.Animation.CardDeployDuration);
        }
        else
        {
            card.View.PlayRevealAnimation(configuration.Animation.CpuCardRevealDuration);
        }
    }

    private void CapturePvpDeploymentPose(string definitionId, PrototypeCardView view)
    {
        if (string.IsNullOrWhiteSpace(definitionId) || (Object)(object)view == (Object)null)
            return;

        if (!pvpDeploymentPoses.TryGetValue(definitionId, out Queue<PvpDeploymentPose> poses))
        {
            poses = new Queue<PvpDeploymentPose>();
            pvpDeploymentPoses[definitionId] = poses;
        }
        poses.Enqueue(new PvpDeploymentPose(view.RectTransform.position, view.RectTransform.rotation));
    }

    private bool TryPopPvpDeploymentPose(string definitionId, out PvpDeploymentPose pose)
    {
        pose = default;
        if (string.IsNullOrWhiteSpace(definitionId)
            || !pvpDeploymentPoses.TryGetValue(definitionId, out Queue<PvpDeploymentPose> poses)
            || poses.Count == 0)
        {
            return false;
        }

        pose = poses.Dequeue();
        if (poses.Count == 0)
            pvpDeploymentPoses.Remove(definitionId);
        return true;
    }

    private BattleCardState FindPvpCardForPlayerSlot(int player, int slot)
    {
        if (pvpState == null)
            return null;
        return player == pvpState.MyIndex ? FindPvpLocalCard(slot) : FindPvpOpponentCard(slot);
    }

    private bool ShouldReplayPaladinRedirectProtection(BattleCardState defender)
    {
        if (defender == null
            || pvpState == null
            || defender.Card.HeroClass != HeroClass.Paladin
            || pvpPendingPaladinRedirectPlayer < 0
            || pvpPendingPaladinRedirectSlot < 0)
        {
            return false;
        }

        int defenderPlayer = defender.BelongsToPlayer ? pvpState.MyIndex : OpponentIndex();
        int defenderSlot = pvpCardSlots.TryGetValue(defender, out int slot) ? slot : -1;
        return defenderPlayer == pvpPendingPaladinRedirectPlayer
            && defenderSlot == pvpPendingPaladinRedirectSlot;
    }

    private void ClearPendingPaladinRedirect()
    {
        pvpPendingPaladinRedirectPlayer = -1;
        pvpPendingPaladinRedirectSlot = -1;
    }

    private void StopPvpRegia()
    {
        if (pvpRegiaRoutine != null)
        {
            StopCoroutine(pvpRegiaRoutine);
            pvpRegiaRoutine = null;
        }
        pvpPendingEvents.Clear();
        ClearPendingPaladinRedirect();
    }

    private void ClearInitiativeTimeline()
    {
        campaignTimelineOrderKeys.Clear();
        if ((Object)(object)initiativeTimelineRoot == (Object)null)
            return;

        for (int index = ((Transform)initiativeTimelineRoot).childCount - 1; index >= 0; index--)
        {
            GameObject childObject = ((Component)((Transform)initiativeTimelineRoot).GetChild(index)).gameObject;
            childObject.SetActive(false);
            Object.Destroy((Object)(object)childObject);
        }
    }

    private CardDefinition FindPvpDefinition(string definitionId)
    {
        if (cardDatabase == null || string.IsNullOrWhiteSpace(definitionId))
            return null;

        CardDefinition definition = cardDatabase.FindById(definitionId);
        return definition != null ? definition : cardDatabase.FindById(definitionId.Replace('_', '-'));
    }

    private BattleCardState FindPvpLocalCard(int slot) =>
        FindPvpCardByServerSlot(playerCards, slot);

    private BattleCardState FindPvpOpponentCard(int slot) =>
        FindPvpCardByServerSlot(cpuCards, slot);

    private bool IsPvpActiveLocalCard(BattleCardState card) =>
        pvpState != null
        && pvpState.IsMyBattleTurn
        && card != null
        && card.BelongsToPlayer
        && pvpCardSlots.TryGetValue(card, out int slot)
        && slot == pvpState.ActiveSlot;

    private bool IsPvpActiveCard(BattleCardState card)
    {
        if (pvpState == null || card == null || pvpState.Phase != PvpClientPhase.Battle)
            return false;

        int player = card.BelongsToPlayer ? pvpState.MyIndex : OpponentIndex();
        int slot = pvpCardSlots.TryGetValue(card, out int serverSlot) ? serverSlot : -1;
        return pvpState.ActivePlayer == player && pvpState.ActiveSlot == slot;
    }

    private BattleCardState FindPvpCardByServerSlot(IEnumerable<BattleCardState> cards, int slot) =>
        cards.FirstOrDefault(card => card != null
            && pvpCardSlots.TryGetValue(card, out int serverSlot)
            && serverSlot == slot);

    private bool IsPvpLocalActionTurn() =>
        pvpState != null && (pvpState.IsMyDeployTurn || pvpState.IsMyBattleTurn);

    private int OpponentIndex() =>
        pvpState == null || pvpState.MyIndex < 0 ? 1 : 1 - pvpState.MyIndex;

    private static int PvpActiveCount(IEnumerable<BattleCardState> cards) =>
        cards.Count(card => card != null && !card.Eliminated);

    private static int PvpDefeatedCount(IEnumerable<BattleCardState> cards) =>
        cards.Count(card => card != null && card.Eliminated);

    private static PendingAttackBonusKind ToPendingAttackBonusKind(PvpPendingBonusKind kind) =>
        kind switch
        {
            PvpPendingBonusKind.Blessing => PendingAttackBonusKind.Blessing,
            PvpPendingBonusKind.Fury => PendingAttackBonusKind.Fury,
            _ => PendingAttackBonusKind.None
        };

    private static BattleAuraType ToBattleAura(PvpAuraType aura) =>
        aura switch
        {
            PvpAuraType.Formation => BattleAuraType.Formation,
            PvpAuraType.Might => BattleAuraType.Might,
            PvpAuraType.Cunning => BattleAuraType.Cunning,
            PvpAuraType.Magic => BattleAuraType.Magic,
            PvpAuraType.Warrior => BattleAuraType.Warrior,
            PvpAuraType.Barbarian => BattleAuraType.Barbarian,
            PvpAuraType.Paladin => BattleAuraType.Paladin,
            PvpAuraType.Rogue => BattleAuraType.Rogue,
            PvpAuraType.Assassin => BattleAuraType.Assassin,
            PvpAuraType.Hunter => BattleAuraType.Hunter,
            PvpAuraType.Mage => BattleAuraType.Mage,
            PvpAuraType.Necromancer => BattleAuraType.Necromancer,
            PvpAuraType.Priest => BattleAuraType.Priest,
            _ => BattleAuraType.None
        };
}
}
