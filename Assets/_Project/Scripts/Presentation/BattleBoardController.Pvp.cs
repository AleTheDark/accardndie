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

    // Regia: gli eventi del server vengono recitati in sequenza dalle stesse
    // animazioni della campagna, poi lo stato autoritativo riallinea la scena.
    private readonly Queue<BattlePresentationEvent> pvpPendingEvents = new();
    private readonly Dictionary<string, Queue<PvpDeploymentPose>> pvpDeploymentPoses = new();
    private Coroutine pvpRegiaRoutine;
    private bool pvpDeploymentIntroPlayed;
    private string pvpHandSignature = string.Empty;
    private int pvpPendingDeployHandIndex = -1;
    private string pvpPendingDeployCardName = string.Empty;

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
                existing.PermanentCombatBonus = source.PermanentBonus + source.Strength - existing.Definition.Strength;
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
                PermanentCombatBonus = source.PermanentBonus + source.Strength - definition.Strength,
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
                    existing.SetSelected(myTurn && !hideUntilIntro && pvpPendingDeployHandIndex >= 0
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
            view.SetSelected(pvpDecisiveSelection.Contains(loadoutIndex));
            view.SetInteractable(!pvpDecisiveSubmitted);
        }

        bool ready = !pvpDecisiveSubmitted && pvpDecisiveSelection.Count == required;
        if ((Object)(object)confirmFormationButton != (Object)null)
        {
            ((Component)confirmFormationButton).gameObject.SetActive(!pvpDecisiveSubmitted);
            confirmFormationButton.interactable = ready;
            if ((Object)(object)confirmFormationButtonText != (Object)null)
                confirmFormationButtonText.text = "CONFERMA SCELTA";
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

            handView.SetSelected(index == pvpPendingDeployHandIndex);
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
        if ((Object)(object)confirmFormationButton != (Object)null)
        {
            ((Component)confirmFormationButton).gameObject.SetActive(false);
            confirmFormationButton.interactable = false;
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

        selected.ShowConfirmCancelActions(
            confirmActionSprite,
            cancelActionSprite,
            new UnityAction(() => TryConfirmPvpPendingDeployment()),
            new UnityAction(() => TryCancelPvpPendingDeployment()));
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

        RefreshCombatantHud(
            playerHud,
            "PLAYER",
            pvpState.MyIndex >= 0 ? $"G{pvpState.MyIndex}" : "PVP",
            $"{PvpActiveCount(playerCards)}/{playerCards.Count} ATTIVI",
            playerCards.Count == 0 ? 0f : (float)PvpActiveCount(playerCards) / playerCards.Count,
            Mathf.Max(1, pvpState.VigorDieSides),
            pvpState.Hand.Count,
            0,
            PvpDefeatedCount(playerCards));

        RefreshCombatantHud(
            cpuHud,
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
        ClearInitiativeTimeline();
        if ((Object)(object)initiativeTimelineRoot == (Object)null || pvpState == null)
        {
            ResizeTimelineTiles(0);
            return;
        }

        if (pvpState.Phase == PvpClientPhase.Deployment)
        {
            List<PvpClientDeploymentToken> deploymentTokens = pvpState.DeploymentOrder
                .OrderBy(token => token.Order)
                .ToList();
            int activeOrder = CurrentPvpDeploymentOrder(deploymentTokens);
            float timelineTileSize = GetTimelineTileSize(deploymentTokens.Count);
            foreach (PvpClientDeploymentToken token in deploymentTokens)
                AddPvpTimelineTile(token.Player, token.Initiative, token.Order == activeOrder, timelineTileSize);
            ResizeTimelineTiles(deploymentTokens.Count);
            return;
        }

        List<BattleCardState> timelineCards = playerCards
            .Concat(cpuCards)
            .Where(card => card != null && !card.Eliminated)
            .OrderByDescending(card => card.Initiative)
            .ThenByDescending(card => PvpTieBreakerFor(card))
            .ToList();
        float battleTileSize = GetTimelineTileSize(timelineCards.Count);
        foreach (BattleCardState card in RotatePvpTimelineFromActive(timelineCards))
        {
            AddPvpTimelineTile(card.BelongsToPlayer ?pvpState.MyIndex : OpponentIndex(), card.Initiative, IsPvpActiveCard(card), battleTileSize);
        }
        ResizeTimelineTiles(timelineCards.Count);
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

    private void AddPvpTimelineTile(int player, int initiative, bool active, float timelineTileSize)
    {
        Image tile = CreateImage("PVP Timeline", (Transform)(object)initiativeTimelineRoot,
            active ? new Color(0.72f, 0.48f, 0.12f, 0.98f)
                : player == pvpState.MyIndex ? new Color(0.04f, 0.42f, 0.48f, 0.95f) : new Color(0.5f, 0.1f, 0.12f, 0.95f));
        LayoutElement layout = ((Component)tile).gameObject.AddComponent<LayoutElement>();
        ConfigureTimelineTileLayout(layout, timelineTileSize);
        Text text = CreateText("Turn", ((Component)tile).transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.text = player == pvpState.MyIndex ? "TU" : "AVV";
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
                    yield return new WaitForSecondsRealtime(
                        Mathf.Max(0.2f, configuration.Animation.CardDeployDuration * 0.7f));
                    break;
                }

                case "AttackResolved":
                {
                    yield return PlayPvpDuelRoutine(battleEvent);
                    RenderPvpMatch();
                    break;
                }

                case "SpiritExpired":
                case "AttachmentApplied":
                {
                    BattleCardState fallen = FindPvpCardForPlayerSlot(battleEvent.Player, battleEvent.Slot);
                    if (fallen != null && (Object)(object)fallen.View != (Object)null)
                        yield return fallen.View.PlayDefeatAnimation();
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
            yield return new WaitForSecondsRealtime(initialDelay);

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
                yield return new WaitForSecondsRealtime(holdDuration);

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
                yield return new WaitForSecondsRealtime(betweenCardsDelay);
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
            battleEvent.AttackerRollSelectionMode);
        VigorRollResult defenderRoll = BattlePresentationAnimationPlayer.BuildRoll(
            battleEvent.DefenderDieSides,
            battleEvent.DefenderRollFirst,
            battleEvent.DefenderRollSecond,
            battleEvent.DefenderRollHasSecond,
            battleEvent.DefenderRollSelected,
            battleEvent.DefenderRollSelectionMode);

        ClearPvpActionSelectionForDuel();
        RectTransform duelRoot = (Object)(object)safeAreaRoot != (Object)null ? safeAreaRoot : canvasRect;
        battleAnimationPlayer.PlayDuel(
            duelRoot,
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
            () =>
            {
                ShowPvpAttackSummary(battleEvent, attacker, defender, attackerRoll, defenderRoll);
                PlayPvpAttackResolvedSfx(battleEvent);
            },
            () => SetMessagePanelHiddenForDuel(hidden: true),
            () => SetMessagePanelHiddenForDuel(hidden: false),
            defenderHit: battleEvent.DefenderLostLife || battleEvent.DefenderEliminated,
            attackerTotal: battleEvent.AttackerTotal,
            defenderTotal: battleEvent.DefenderTotal);
        yield return new WaitWhile(() =>
            (Object)(object)battleAnimationPlayer != (Object)null && battleAnimationPlayer.IsBusy);
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

    private void StopPvpRegia()
    {
        if (pvpRegiaRoutine != null)
        {
            StopCoroutine(pvpRegiaRoutine);
            pvpRegiaRoutine = null;
        }
        pvpPendingEvents.Clear();
    }

    private void ClearInitiativeTimeline()
    {
        if ((Object)(object)initiativeTimelineRoot == (Object)null)
            return;

        for (int index = ((Transform)initiativeTimelineRoot).childCount - 1; index >= 0; index--)
            Object.Destroy((Object)(object)((Component)((Transform)initiativeTimelineRoot).GetChild(index)).gameObject);
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
