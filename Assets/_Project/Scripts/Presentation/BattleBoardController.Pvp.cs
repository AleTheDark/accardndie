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
        Attack,
        Ability,
        Attachment
    }

    private bool pvpPresentationActive;
    private PvpClientMatchState pvpState;
    private IReadOnlyList<LoadoutCardDto> pvpLoadout;
    private IBattlePresentationActions pvpActions;
    private readonly List<PrototypeCardView> pvpHandViews = new();
    private readonly Dictionary<BattleCardState, int> pvpCardSlots = new();
    private readonly Dictionary<BattleCardState, int> pvpCardLives = new();
    private PvpPresentationTargetMode pvpTargetMode = PvpPresentationTargetMode.Attack;

    public void ShowPvpMatch(
        AccardND.NetProtocol.PvpClientMatchState state,
        System.Collections.Generic.IReadOnlyList<AccardND.NetProtocol.LoadoutCardDto> myLoadout,
        AccardND.Battlefield.IBattlePresentationActions actions)
    {
        pvpPresentationActive = true;
        pvpState = state;
        pvpLoadout = myLoadout;
        pvpActions = actions;
        pvpTargetMode = PvpPresentationTargetMode.Attack;
        pvpCardSlots.Clear();
        pvpCardLives.Clear();

        if ((Object)(object)modeSelectionPanel != (Object)null)
            modeSelectionPanel.SetActive(false);
        if ((Object)(object)roomChoicePanel != (Object)null)
            roomChoicePanel.SetActive(false);
        if ((Object)(object)deckBuilderPanel != (Object)null)
            deckBuilderPanel.SetActive(false);

        SetCombatChromeVisible(true);
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
        RenderPvpMatch();
        PlayPvpPresentationEvents(events);
    }

    public void HidePvpMatch()
    {
        if (!pvpPresentationActive)
            return;

        pvpPresentationActive = false;
        pvpState = null;
        pvpLoadout = null;
        pvpActions = null;
        pvpTargetMode = PvpPresentationTargetMode.Attack;
        pvpCardSlots.Clear();
        pvpCardLives.Clear();

        DestroyCardViews(playerCards);
        DestroyCardViews(cpuCards);
        DestroyPrototypeViews(pvpHandViews);
        ClearCardRowChildren(playerRow);
        ClearCardRowChildren(cpuRow);
        ClearCardRowChildren(playerHandRow);
        pvpCardSlots.Clear();
        pvpCardLives.Clear();
        ClearInitiativeTimeline();
        SetMessage("Multiplayer chiuso.");
        SetCombatChromeVisible(false);
    }

    private void RenderPvpMatch()
    {
        if (!pvpPresentationActive || pvpState == null || cardDatabase == null)
            return;

        DestroyCardViews(playerCards);
        DestroyCardViews(cpuCards);
        DestroyPrototypeViews(pvpHandViews);
        ClearCardRowChildren(playerRow);
        ClearCardRowChildren(cpuRow);
        ClearCardRowChildren(playerHandRow);

        currentRoomType = RoomType.Monster;
        draftActive = false;
        deploymentDraftActive = false;
        inputLocked = !IsPvpLocalActionTurn();
        gameFinished = pvpState.Phase == PvpClientPhase.Finished;
        roundNumber = pvpState.MatchRound;
        selectedPlayerIndex = pvpState.ActiveSlot;
        playerAura = ToBattleAura(pvpState.Auras[pvpState.MyIndex >= 0 ? pvpState.MyIndex : 0]);
        cpuAura = ToBattleAura(pvpState.Auras[OpponentIndex()]);

        BuildPvpBoard(pvpState.MyIndex, playerCards, playerRow, belongsToPlayer: true);
        BuildPvpBoard(OpponentIndex(), cpuCards, cpuRow, belongsToPlayer: false);
        LinkPvpMarkedTargets();
        RefreshPvpCardVisuals(playerCards);
        RefreshPvpCardVisuals(cpuCards);
        BuildPvpHand();
        RefreshPvpHud();
        RefreshPvpTimeline();
        RefreshPvpMessage();
        ApplyResponsiveLayout();
        ConfigurePvpActionOverlays();
    }

    private void BuildPvpBoard(int player, ICollection<BattleCardState> destination, RectTransform row, bool belongsToPlayer)
    {
        if (pvpState == null || player < 0 || player >= pvpState.Boards.Length)
            return;

        foreach (PvpClientCard source in pvpState.Boards[player].OrderBy(card => card.Slot))
        {
            CardDefinition definition = FindPvpDefinition(source.CardId);
            if ((Object)(object)definition == (Object)null)
                continue;

            PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)row, definition, configuration);
            var card = new BattleCardState(definition, view, belongsToPlayer)
            {
                Initiative = source.Initiative,
                Eliminated = source.Eliminated || source.Lives <= 0,
                IsSpirit = source.IsSpirit,
                InhibitedTurns = source.Inhibited ? 1 : 0,
                AbilityArmed = source.Protecting,
                PermanentCombatBonus = source.PermanentBonus + source.Strength - definition.Strength,
                PendingAttackBonus = source.PendingBonus,
                PendingVigorStepPenalty = source.DiePenaltySteps
            };

            int capturedSlot = source.Slot;
            pvpCardSlots[card] = capturedSlot;
            pvpCardLives[card] = Mathf.Max(0, source.Lives);
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
        if (pvpState == null || pvpState.Phase != PvpClientPhase.Deployment || !pvpState.IsMyDeployTurn)
            return;

        for (int position = 0; position < pvpState.Hand.Count; position++)
        {
            int captured = position;
            CardDefinition definition = FindPvpDefinition(pvpState.Hand[position].DefinitionId);
            if ((Object)(object)definition == (Object)null)
                continue;

            PrototypeCardView view = PrototypeCardView.Create((Transform)(object)playerHandRow, definition, configuration);
            view.SetInteractable(true);
            view.Button.onClick.AddListener(new UnityAction(() => pvpActions?.Deploy(captured)));
            pvpHandViews.Add(view);
        }
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
            card.View.SetTurnAura(IsPvpActiveCard(card), card.BelongsToPlayer);
            int lives = pvpCardLives.TryGetValue(card, out int value) ? value : card.Eliminated ? 0 : 2;
            card.View.SetHealthBar(
                lives,
                2,
                card.BelongsToPlayer ? new Color(0.08f, 0.68f, 0.72f) : new Color(0.78f, 0.18f, 0.16f));
            card.View.SetInteractable(true);
        }
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

        if (pvpTargetMode != PvpPresentationTargetMode.Attack)
        {
            active.View.ShowCancelAction(cancel, new UnityAction(() =>
            {
                pvpTargetMode = PvpPresentationTargetMode.Attack;
                RenderPvpMatch();
            }));
            return;
        }

        bool canUseAbility = BattlePresentationViewStateMapper.HasActivatableAbility(active.Card.HeroClass);
        bool canAttach = active.Card.Strength >= 2 && active.Card.Strength < 5;
        if (canUseAbility && canAttach)
        {
            active.View.ShowTripleActions(
                attack, new UnityAction(() => pvpTargetMode = PvpPresentationTargetMode.Attack),
                ability, new UnityAction(ActivatePvpAbility),
                attachment, new UnityAction(ActivatePvpAttachment));
        }
        else if (canUseAbility)
        {
            active.View.ShowDualActions(
                attack, new UnityAction(() => pvpTargetMode = PvpPresentationTargetMode.Attack),
                ability, new UnityAction(ActivatePvpAbility));
        }
        else if (canAttach)
        {
            active.View.ShowDualActions(
                attack, new UnityAction(() => pvpTargetMode = PvpPresentationTargetMode.Attack),
                attachment, new UnityAction(ActivatePvpAttachment));
        }
        else
        {
            active.View.ShowClassAction(attack, new UnityAction(() => pvpTargetMode = PvpPresentationTargetMode.Attack));
        }
    }

    private void ActivatePvpAbility()
    {
        BattleCardState active = FindPvpLocalCard(pvpState.ActiveSlot);
        if (active != null && active.Card.HeroClass == HeroClass.Warrior)
        {
            pvpActions?.UseAbility(false, pvpState.ActiveSlot);
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
            pvpTargetMode = PvpPresentationTargetMode.Attack;
            return;
        }

        if (pvpState != null && pvpState.IsMyBattleTurn && pvpTargetMode == PvpPresentationTargetMode.Ability)
        {
            pvpActions?.UseAbility(false, slot);
            pvpTargetMode = PvpPresentationTargetMode.Attack;
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
                pvpTargetMode = PvpPresentationTargetMode.Attack;
                return;
            }

            if (pvpTargetMode == PvpPresentationTargetMode.Attack)
            {
                pvpActions?.Attack(slot);
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
            float timelineTileSize = GetTimelineTileSize(pvpState.DeploymentOrder.Count);
            foreach (PvpClientDeploymentToken token in pvpState.DeploymentOrder)
                AddPvpTimelineTile(token.Player, token.Initiative, token.Player == pvpState.DeployPlayer, timelineTileSize);
            ResizeTimelineTiles(pvpState.DeploymentOrder.Count);
            return;
        }

        List<BattleCardState> timelineCards = cpuCards
            .Where(card => card != null && !card.Eliminated)
            .OrderByDescending(card => card.Initiative)
            .Concat(playerCards.Where(card => card != null && !card.Eliminated).OrderByDescending(card => card.Initiative))
            .ToList();
        float battleTileSize = GetTimelineTileSize(timelineCards.Count);
        foreach (BattleCardState card in timelineCards)
        {
            AddPvpTimelineTile(card.BelongsToPlayer ?pvpState.MyIndex : OpponentIndex(), card.Initiative, IsPvpActiveCard(card), battleTileSize);
        }
        ResizeTimelineTiles(timelineCards.Count);
    }

    private void AddPvpTimelineTile(int player, int initiative, bool active, float timelineTileSize)
    {
        Image tile = CreateImage("PVP Timeline", (Transform)(object)initiativeTimelineRoot,
            active ? new Color(0.72f, 0.48f, 0.12f, 0.98f)
                : player == pvpState.MyIndex ? new Color(0.08f, 0.25f, 0.32f, 0.94f) : new Color(0.32f, 0.1f, 0.12f, 0.94f));
        LayoutElement layout = ((Component)tile).gameObject.AddComponent<LayoutElement>();
        ConfigureTimelineTileLayout(layout, timelineTileSize);
        Text text = CreateText("Turn", ((Component)tile).transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.text = $"{(player == pvpState.MyIndex ? "TU" : "AVV")}\n{initiative}";
        SetRect(text.rectTransform, Vector2.zero, Vector2.one);
    }

    private void RefreshPvpMessage()
    {
        string message = pvpState.Phase switch
        {
            PvpClientPhase.Deployment when pvpState.IsMyDeployTurn => "Scegli una carta dalla tua mano da schierare.",
            PvpClientPhase.Deployment => "L'avversario sta schierando...",
            PvpClientPhase.Battle when pvpState.IsMyBattleTurn => "Il tuo turno: scegli un'azione sulla pedina attiva.",
            PvpClientPhase.Battle => "Turno dell'avversario...",
            PvpClientPhase.Finished => pvpState.Winner == pvpState.MyIndex ? "VITTORIA!" : "SCONFITTA",
            _ => pvpState.Log.Count > 0 ? pvpState.Log[pvpState.Log.Count - 1] : "In attesa..."
        };
        if ((Object)(object)messageText != (Object)null)
            messageText.text = message;
    }

    private void PlayPvpPresentationEvents(IReadOnlyList<BattlePresentationEvent> events)
    {
        if (events == null)
            return;

        foreach (BattlePresentationEvent battleEvent in events)
        {
            if (battleEvent == null)
                continue;

            if (battleEvent.Type == "CardDeployed")
                PlayPawnEnteringBattlefieldSfx(FindPvpDefinition(battleEvent.CardId));
        }
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
