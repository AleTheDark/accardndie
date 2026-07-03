using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    public interface IBattlePresentationActions
    {
        void Deploy(int handIndex);
        void Attack(int enemySlot);
        void UseAbility(bool targetIsEnemy, int targetSlot);
        void Attach(int allySlot);
        void Pass();
        void SubmitDecisive(int[] loadoutIndices);
        void LeaveToLobby();
    }

    /// <summary>
    /// Schermata battaglia condivisa: renderizza uno stato di presentazione neutro.
    /// Le modalita' concrete traducono il loro stato di gioco in questo modello.
    /// </summary>
    public sealed class BattlePresentationScreen
    {
        private enum TargetMode
        {
            Attack,
            Ability,
            Attachment
        }

        private readonly RectTransform root;
        private BattlePresentationState state;
        private readonly IBattlePresentationActions actions;
        private List<BattlePresentationLoadoutCard> myLoadout;
        private readonly CardDatabase database;
        private readonly GameConfiguration configuration;
        private readonly HashSet<int> decisiveSelection = new();
        private readonly Dictionary<string, float> revealAnimationUntil = new();
        private readonly Dictionary<string, Queue<DeploymentPose>> pendingDeploymentPoses = new();
        private TargetMode mode = TargetMode.Attack;
        private BattlefieldViewRenderer battlefieldRenderer;
        private BattleCardInspectionOverlay inspectionOverlay;
        private DiceSpriteCatalog diceCatalog;
        private BattlePresentationAnimationPlayer animationPlayer;
        private string deploymentIntroSignature;
        private float deploymentIntroUntil;
        private bool deploymentIntroRefreshPending;

        private readonly struct DeploymentPose
        {
            public DeploymentPose(Vector3 worldPosition, Quaternion worldRotation)
            {
                WorldPosition = worldPosition;
                WorldRotation = worldRotation;
            }

            public Vector3 WorldPosition { get; }
            public Quaternion WorldRotation { get; }
        }

        public BattlePresentationScreen(
            Transform parent,
            BattlePresentationState state,
            IBattlePresentationActions actions,
            List<BattlePresentationLoadoutCard> myLoadout,
            CardDatabase database,
            GameConfiguration configuration)
        {
            this.state = state ?? new BattlePresentationState();
            this.actions = actions;
            this.myLoadout = myLoadout ?? new List<BattlePresentationLoadoutCard>();
            this.database = database;
            this.configuration = configuration != null
                ? configuration
                : ScriptableObject.CreateInstance<GameConfiguration>();
            root = BattleUiFactory.CreatePanel(parent, "Match", Color.clear);
            root.GetComponent<Image>().raycastTarget = false;
            BattleUiFactory.Stretch(root);
            diceCatalog = Resources.Load<DiceSpriteCatalog>("DiceSpriteCatalog");
            animationPlayer = root.gameObject.AddComponent<BattlePresentationAnimationPlayer>();
            inspectionOverlay = new BattleCardInspectionOverlay(root, this.configuration);
            Rebuild();
        }

        public void SetState(BattlePresentationState state, List<BattlePresentationLoadoutCard> myLoadout)
        {
            this.state = state ?? new BattlePresentationState();
            this.myLoadout = myLoadout ?? new List<BattlePresentationLoadoutCard>();
            Rebuild();
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy()
        {
            battlefieldRenderer?.Destroy();
            battlefieldRenderer = null;
            inspectionOverlay?.Destroy();
            inspectionOverlay = null;
            Object.Destroy(root.gameObject);
        }

        public void Rebuild()
        {
            battlefieldRenderer?.Destroy();
            battlefieldRenderer = null;
            BattleUiFactory.Clear(root);
            BuildBackground();
            BuildHeader();
            BuildBattlefield();
            BuildPhasePanel();
            inspectionOverlay = new BattleCardInspectionOverlay(root, configuration);
        }

        public void Tick()
        {
            if (!deploymentIntroRefreshPending || Time.unscaledTime < deploymentIntroUntil)
                return;

            deploymentIntroRefreshPending = false;
            Rebuild();
        }

        public void PlayEventAnimations(IReadOnlyList<BattlePresentationEvent> events)
        {
            if (events == null || battlefieldRenderer == null)
                return;

            foreach (BattlePresentationEvent matchEvent in events)
            {
                if (matchEvent == null)
                    continue;

                switch (matchEvent.Type)
                {
                    case "CardInitiative":
                        PlayCardInitiativeAnimation(matchEvent);
                        break;
                    case "CardDeployed":
                        PlayCardDeploymentAnimation(matchEvent);
                        break;
                    case "AttackResolved":
                        PlayAttackResolvedAnimation(matchEvent);
                        break;
                    case "CardRevived":
                        PlayCardRevealAnimation(matchEvent.Player, matchEvent.Slot);
                        break;
                }
            }
        }

        private void BuildBackground()
        {
            var backgroundObject = new GameObject("Battle Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(root, false);
            RectTransform rect = (RectTransform)backgroundObject.transform;
            BattleUiFactory.Stretch(rect);
            Image image = backgroundObject.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("Backgrounds/Background_terrain");
            image.color = new Color(0.08f, 0.11f, 0.12f, 1f);
            image.preserveAspect = false;
            image.raycastTarget = false;
            backgroundObject.transform.SetAsFirstSibling();

            RectTransform glow = BattleUiFactory.CreatePanel(root, "Table Glow", new Color(0.025f, 0.06f, 0.07f, 0.32f));
            BattleUiFactory.SetAnchors(glow, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f));
            glow.GetComponent<Image>().raycastTarget = false;
        }

        private void BuildHeader()
        {
            string auras = string.Empty;
            if (state.LocalPlayerIndex >= 0
                && (!string.IsNullOrWhiteSpace(state.Auras[state.LocalPlayerIndex])
                    || !string.IsNullOrWhiteSpace(state.Auras[1 - state.LocalPlayerIndex])))
                auras = $"  |  AURA TU: {state.Auras[state.LocalPlayerIndex]}  VS {state.Auras[1 - state.LocalPlayerIndex]}";
            int myWins = state.LocalPlayerIndex >= 0 ? state.Wins[state.LocalPlayerIndex] : 0;
            int theirWins = state.LocalPlayerIndex >= 0 ? state.Wins[1 - state.LocalPlayerIndex] : 0;
            string vigorLabel = BuildVigorLabel();
            Text header = BattleUiFactory.CreateText(
                root,
                "Header",
                $"ROUND {state.MatchRound}  |  {vigorLabel}  |  TU {myWins} - {theirWins} {state.OpponentName.ToUpperInvariant()}{auras}",
                22);
            BattleUiFactory.SetAnchors((RectTransform)header.transform, new Vector2(0.035f, 0.952f), new Vector2(0.965f, 0.992f));
        }

        private string BuildVigorLabel()
        {
            if (state.LocalVigorDieSides > 0
                && state.OpponentVigorDieSides > 0
                && state.LocalVigorDieSides != state.OpponentVigorDieSides)
            {
                return $"TU D{state.LocalVigorDieSides}  |  AVV D{state.OpponentVigorDieSides}";
            }

            int dieSides = state.VigorDieSides > 0
                ? state.VigorDieSides
                : state.LocalVigorDieSides > 0
                    ? state.LocalVigorDieSides
                    : state.OpponentVigorDieSides;
            return $"VIGORE D{dieSides}";
        }

        private void BuildBattlefield()
        {
            if (state.LocalPlayerIndex < 0)
                return;

            RectTransform battlefieldRoot = BattleUiFactory.CreatePanel(root, "Battlefield", Color.clear);
            BattleUiFactory.Stretch(battlefieldRoot);
            battlefieldRoot.GetComponent<Image>().raycastTarget = false;

            battlefieldRenderer = new BattlefieldViewRenderer(battlefieldRoot, configuration);
            battlefieldRenderer.CardClicked += OnBattlefieldCardClicked;
            battlefieldRenderer.CardInspected += OnBattlefieldCardInspected;
            Canvas.ForceUpdateCanvases();
            battlefieldRenderer.Render(BattlePresentationViewStateMapper.ToBattlefieldViewState(
                state,
                database,
                IsTileClickable,
                IsTargetModeSelection,
                ShouldPlayEnterAnimation,
                HasPendingDeploymentPose));
            ConfigureActiveCardActions();
        }

        private void PlayCardInitiativeAnimation(BattlePresentationEvent matchEvent)
        {
            if (diceCatalog == null || !TryGetCardView(matchEvent.Player, matchEvent.Slot, out PrototypeCardView view))
                return;

            view.PlayDiceRoll(
                diceCatalog,
                configuration.Gameplay.InitiativeDieSides,
                matchEvent.Initiative,
                matchEvent.Player == state.LocalPlayerIndex ? "INIZIATIVA TU" : "INIZIATIVA AVVERSARIO",
                configuration.Animation.DiceRollDuration,
                configuration.Animation.DiceResultHold);
        }

        private void PlayAttackResolvedAnimation(BattlePresentationEvent matchEvent)
        {
            if (!TryGetCardView(matchEvent.Player, matchEvent.Slot, out PrototypeCardView attacker)
                || !TryGetCardView(matchEvent.TargetPlayer, matchEvent.TargetSlot, out PrototypeCardView defender))
            {
                return;
            }

            VigorRollResult attackerRoll = BattlePresentationAnimationPlayer.BuildRoll(
                matchEvent.AttackerDieSides,
                matchEvent.AttackerRollFirst,
                matchEvent.AttackerRollSecond,
                matchEvent.AttackerRollHasSecond,
                matchEvent.AttackerRollSelected);
            VigorRollResult defenderRoll = BattlePresentationAnimationPlayer.BuildRoll(
                matchEvent.DefenderDieSides,
                matchEvent.DefenderRollFirst,
                matchEvent.DefenderRollSecond,
                matchEvent.DefenderRollHasSecond,
                matchEvent.DefenderRollSelected);

            animationPlayer?.PlayDuel(
                root,
                configuration,
                diceCatalog,
                attacker,
                defender,
                attackerRoll,
                defenderRoll,
                matchEvent.AttackerDieSides,
                matchEvent.DefenderDieSides,
                matchEvent.Player == state.LocalPlayerIndex ? "ATTACCO" : "ATTACCO AVV",
                matchEvent.TargetPlayer == state.LocalPlayerIndex ? "TUA DIFESA" : "DIFESA",
                matchEvent.DefenderEliminated);
        }

        private void PlayCardDeploymentAnimation(BattlePresentationEvent matchEvent)
        {
            if (!TryGetCardView(matchEvent.Player, matchEvent.Slot, out PrototypeCardView view))
                return;

            if (TryPopDeploymentPose(matchEvent.CardId, out DeploymentPose pose))
            {
                view.PlayDeploymentAnimation(
                    pose.WorldPosition,
                    pose.WorldRotation,
                    configuration.Animation.CardDeployDuration);
            }
            else
            {
                view.PlayRevealAnimation(configuration.Animation.CpuCardRevealDuration);
            }
        }

        private void PlayCardRevealAnimation(int player, int slot)
        {
            if (TryGetCardView(player, slot, out PrototypeCardView view))
                view.PlayRevealAnimation(configuration.Animation.CpuCardRevealDuration);
        }

        private bool TryGetCardView(int player, int slot, out PrototypeCardView view)
        {
            view = null;
            if (state.LocalPlayerIndex < 0 || battlefieldRenderer == null)
                return false;

            BattlefieldSide side = player == state.LocalPlayerIndex ? BattlefieldSide.Bottom : BattlefieldSide.Top;
            return battlefieldRenderer.TryGetCardView(new BattlefieldCardKey(side, slot), out view);
        }

        private bool ShouldPlayEnterAnimation(string revealKey)
        {
            float now = Time.unscaledTime;
            float duration = Mathf.Max(0.05f, configuration.Animation.CpuCardRevealDuration);
            if (!revealAnimationUntil.TryGetValue(revealKey, out float until))
            {
                revealAnimationUntil[revealKey] = now + duration + 0.12f;
                return true;
            }

            return now <= until;
        }

        private void BuildPhasePanel()
        {
            RefreshDeploymentIntroGate();
            bool deploymentIntroVisible = state.Phase == BattlePresentationPhase.Deployment
                && state.DeploymentOrder.Count > 0
                && Time.unscaledTime < deploymentIntroUntil;
            switch (state.Phase)
            {
                case BattlePresentationPhase.Deployment when deploymentIntroVisible:
                    BuildNarrationPanel("Tiri iniziativa schieramento: dal piu basso al piu alto.", string.Empty);
                    BuildDeploymentTimeline();
                    break;
                case BattlePresentationPhase.Deployment when state.IsLocalDeployTurn:
                {
                    BuildNarrationPanel("Scegli una carta dalla tua mano da schierare.", string.Empty);
                    BuildDeploymentTimeline();
                    RectTransform bar = BuildHandDock("Deployment Hand");
                    BuildHandStrip(bar, "SCHIERA UNA CARTA");
                    break;
                }
                case BattlePresentationPhase.Deployment:
                    BuildNarrationPanel("L'avversario sta schierando...", string.Empty);
                    BuildDeploymentTimeline();
                    break;
                case BattlePresentationPhase.DecisiveSelection:
                {
                    RectTransform bar = BuildBottomPanel("Decisive Picker");
                    BuildDecisivePicker(bar);
                    break;
                }
                case BattlePresentationPhase.Battle when state.IsLocalBattleTurn:
                    BuildNarrationPanel(CurrentBattlePrompt(), "TURNO TU", showPassButton: true);
                    break;
                case BattlePresentationPhase.Battle:
                    BuildNarrationPanel(LastLogOr("Turno dell'avversario..."), "TURNO AVVERSARIO");
                    break;
                case BattlePresentationPhase.Finished:
                {
                    RectTransform bar = BuildBottomPanel("Finished Actions");
                    BuildFinishedActions(bar);
                    break;
                }
                default:
                    BuildNarrationPanel(LastLogOr("In attesa del server..."), string.Empty);
                    break;
            }
        }

        private void RefreshDeploymentIntroGate()
        {
            if (state.Phase != BattlePresentationPhase.Deployment || state.DeploymentOrder.Count == 0)
                return;

            string signature = $"{state.MatchRound}:{state.DeploymentOrder.Count}:";
            foreach (BattlePresentationDeploymentToken token in state.DeploymentOrder)
                signature += $"{token.Order}-{token.Player}-{token.Initiative};";

            if (signature == deploymentIntroSignature)
                return;

            deploymentIntroSignature = signature;
            deploymentIntroUntil = Time.unscaledTime + Mathf.Max(1.25f, configuration.Animation.DiceRollDuration + 0.75f);
            deploymentIntroRefreshPending = true;
        }

        private void BuildDeploymentTimeline()
        {
            if (state.DeploymentOrder.Count == 0)
                return;

            RectTransform timeline = BattleUiFactory.CreatePanel(root, "Deployment Timeline", new Color(0.015f, 0.025f, 0.04f, 0.42f));
            BattleUiFactory.SetAnchors(timeline, new Vector2(0.16f, 0.76f), new Vector2(0.84f, 0.807f));
            HorizontalLayoutGroup layout = timeline.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 3, 3);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            int activeOrder = ActiveDeploymentOrder();
            foreach (BattlePresentationDeploymentToken token in state.DeploymentOrder)
            {
                bool active = token.Order == activeOrder;
                bool mine = token.Player == state.LocalPlayerIndex;
                Image tile = new GameObject("Deployment Token", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                tile.transform.SetParent(timeline, false);
                tile.color = active
                    ? new Color(0.72f, 0.48f, 0.12f, 0.98f)
                    : mine ? new Color(0.04f, 0.42f, 0.48f, 0.95f) : new Color(0.5f, 0.1f, 0.12f, 0.95f);
                LayoutElement element = tile.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 52f;
                element.preferredWidth = 64f;
                element.flexibleWidth = 0f;

                Text text = BattleUiFactory.CreateText(
                    tile.rectTransform,
                    "Token",
                    $"{(mine ? "TU" : "AVV")}\n{token.Initiative}",
                    15,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                BattleUiFactory.Stretch((RectTransform)text.transform, 2f, 2f);
            }
        }

        private int ActiveDeploymentOrder()
        {
            int deployedCount = state.Boards[0].Count + state.Boards[1].Count;
            return deployedCount >= 0 && deployedCount < state.DeploymentOrder.Count
                ? state.DeploymentOrder[deployedCount].Order
                : -1;
        }

        private RectTransform BuildBottomPanel(string name)
        {
            RectTransform bar = BattleUiFactory.CreatePanel(root, name, new Color(0.025f, 0.028f, 0.04f, 0.82f));
            BattleUiFactory.SetAnchors(bar, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.29f));
            return bar;
        }

        private RectTransform BuildHandDock(string name)
        {
            RectTransform dock = BattleUiFactory.CreatePanel(root, name, Color.clear);
            dock.GetComponent<Image>().raycastTarget = false;
            BattleUiFactory.SetAnchors(dock, new Vector2(0.04f, 0.045f), new Vector2(0.96f, 0.245f));
            return dock;
        }

        private void BuildNarrationPanel(string message, string banner, bool showPassButton = false)
        {
            RectTransform panel = BattleUiFactory.CreatePanel(root, "Message Panel", new Color(0.015f, 0.025f, 0.04f, 0.34f));
            BattleUiFactory.SetAnchors(panel, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.535f));

            bool showBanner = !string.IsNullOrWhiteSpace(banner);
            if (showBanner)
            {
                RectTransform bannerRect = BattleUiFactory.CreatePanel(panel, "Current Turn Banner", configuration.Visual.PlayerTurnColor);
                BattleUiFactory.SetAnchors(bannerRect, new Vector2(0.1825f, 0.69f), new Vector2(0.8175f, 0.98f));
                Text bannerText = BattleUiFactory.CreateText(bannerRect, "Current Turn", banner, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                BattleUiFactory.Stretch((RectTransform)bannerText.transform, 4f, 4f);
            }

            Text text = BattleUiFactory.CreateText(panel, "Battle Log", message, 21, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.color = new Color(0.88f, 0.92f, 0.96f);
            BattleUiFactory.SetAnchors(
                (RectTransform)text.transform,
                new Vector2(0.035f, showBanner ? 0.06f : 0.12f),
                new Vector2(showPassButton ? 0.65f : 0.965f, showBanner ? 0.66f : 0.88f));

            if (!showPassButton)
                return;

            Button pass = BattleUiFactory.CreateButton(
                panel,
                "Pass",
                "PASSA",
                new Color(0.11f, 0.14f, 0.19f, 0.92f),
                actions.Pass,
                18);
            BattleUiFactory.SetAnchors((RectTransform)pass.transform, new Vector2(0.69f, 0.16f), new Vector2(0.97f, 0.66f));
        }

        private void BuildFinishedActions(RectTransform bar)
        {
            bool won = state.Winner == state.LocalPlayerIndex;
            Text result = BattleUiFactory.CreateText(
                bar, "Result", won ? "VITTORIA!" : "SCONFITTA", 42);
            result.color = won ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.4f, 0.35f);
            BattleUiFactory.SetAnchors((RectTransform)result.transform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.95f));
            Button back = BattleUiFactory.CreateButton(
                bar, "Back", "TORNA ALLA LOBBY", new Color(0.05f, 0.45f, 0.5f, 0.98f), actions.LeaveToLobby);
            BattleUiFactory.SetAnchors((RectTransform)back.transform, new Vector2(0.32f, 0.08f), new Vector2(0.68f, 0.4f));
        }

        private void BuildHandStrip(RectTransform bar, string caption)
        {
            Text title = BattleUiFactory.CreateText(bar, "Caption", caption, 20);
            BattleUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.02f, 0.82f), new Vector2(0.98f, 1f));
            int count = state.Hand.Count;
            for (int position = 0; position < count; position++)
            {
                int captured = position;
                float slotWidth = 0.84f / Mathf.Max(count, 1);
                float xMin = 0.08f + position * slotWidth;
                CardDefinition definition = FindCardDefinition(state.Hand[position].DefinitionId);
                if (definition == null)
                {
                    Button button = BattleUiFactory.CreateButton(
                        bar, $"Hand{position}", ShortCardName(state.Hand[position].DefinitionId),
                        new Color(0.1f, 0.35f, 0.42f, 0.98f), () => actions.Deploy(captured), 16);
                    BattleUiFactory.SetAnchors(
                        (RectTransform)button.transform,
                        new Vector2(xMin, 0.02f),
                        new Vector2(xMin + slotWidth - 0.012f, 0.80f));
                    continue;
                }

                RectTransform holder = new GameObject($"Hand{position}", typeof(RectTransform)).GetComponent<RectTransform>();
                holder.SetParent(bar, false);
                BattleUiFactory.SetAnchors(holder, new Vector2(xMin, 0.02f), new Vector2(xMin + slotWidth - 0.012f, 0.80f));
                PrototypeCardView view = PrototypeCardView.Create(holder, definition, configuration);
                BattleUiFactory.Stretch(view.RectTransform);
                AspectRatioFitter fitter = view.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 848f / 1264f;
                view.SetInteractable(true);
                view.ClearActionOverlay();
                view.Button.onClick.RemoveAllListeners();
                view.Button.onClick.AddListener(() =>
                {
                    CaptureDeploymentPose(definition.Id, view);
                    actions.Deploy(captured);
                });
            }
        }

        private void CaptureDeploymentPose(string cardId, PrototypeCardView view)
        {
            if (string.IsNullOrWhiteSpace(cardId) || view == null)
                return;

            if (!pendingDeploymentPoses.TryGetValue(cardId, out Queue<DeploymentPose> poses))
            {
                poses = new Queue<DeploymentPose>();
                pendingDeploymentPoses[cardId] = poses;
            }
            poses.Enqueue(new DeploymentPose(view.RectTransform.position, view.RectTransform.rotation));
        }

        private bool TryPopDeploymentPose(string cardId, out DeploymentPose pose)
        {
            pose = default;
            if (string.IsNullOrWhiteSpace(cardId)
                || !pendingDeploymentPoses.TryGetValue(cardId, out Queue<DeploymentPose> poses)
                || poses.Count == 0)
            {
                return false;
            }

            pose = poses.Dequeue();
            if (poses.Count == 0)
            pendingDeploymentPoses.Remove(cardId);
            return true;
        }

        private bool HasPendingDeploymentPose(string cardId)
        {
            return !string.IsNullOrWhiteSpace(cardId)
                && pendingDeploymentPoses.TryGetValue(cardId, out Queue<DeploymentPose> poses)
                && poses.Count > 0;
        }

        private void BuildDecisivePicker(RectTransform bar)
        {
            Text title = BattleUiFactory.CreateText(
                bar, "Caption",
                $"ROUND DECISIVO: scegli {state.DecisiveRequiredCount} carte ({decisiveSelection.Count} selezionate)",
                20);
            BattleUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.02f, 0.75f), new Vector2(0.98f, 0.98f));

            for (int index = 0; index < myLoadout.Count; index++)
            {
                int captured = index;
                bool selected = decisiveSelection.Contains(index);
                float xMin = 0.02f + index * (0.86f / myLoadout.Count);
                Button button = BattleUiFactory.CreateButton(
                    bar, $"Pick{index}", ShortCardName(myLoadout[index].DefinitionId),
                    selected ? new Color(0.75f, 0.55f, 0.1f, 0.98f) : new Color(0.1f, 0.35f, 0.42f, 0.98f),
                    () => ToggleDecisive(captured), 14);
                BattleUiFactory.SetAnchors(
                    (RectTransform)button.transform,
                    new Vector2(xMin, 0.08f),
                    new Vector2(xMin + 0.86f / myLoadout.Count - 0.006f, 0.7f));
            }

            Button confirm = BattleUiFactory.CreateButton(
                bar, "Confirm", "OK", new Color(0.1f, 0.55f, 0.25f, 0.98f), ConfirmDecisive, 24);
            confirm.interactable = decisiveSelection.Count == state.DecisiveRequiredCount;
            BattleUiFactory.SetAnchors((RectTransform)confirm.transform, new Vector2(0.9f, 0.08f), new Vector2(0.98f, 0.7f));
        }

        private void ConfigureActiveCardActions()
        {
            if (!state.IsLocalBattleTurn)
                return;

            BattlePresentationCard active = BattlePresentationViewStateMapper.FindBySlot(
                state.Boards[state.LocalPlayerIndex], state.ActiveSlot);
            if (active == null || active.Eliminated)
                return;

            var key = new BattlefieldCardKey(BattlefieldSide.Bottom, active.Slot);
            if (battlefieldRenderer == null || !battlefieldRenderer.TryGetCardView(key, out PrototypeCardView view))
                return;

            Sprite attack = LoadUiSprite("attack_button");
            Sprite ability = LoadUiSprite("ability_button");
            Sprite attachment = LoadUiSprite("attachment_button");
            Sprite cancel = LoadUiSprite("cancel_button");

            if (mode == TargetMode.Ability || mode == TargetMode.Attachment)
            {
                view.ShowCancelAction(cancel, new UnityAction(CancelTargetMode));
                return;
            }

            bool canUseAbility = BattlePresentationViewStateMapper.HasActivatableAbility(active.HeroClass);
            bool canAttach = active.Strength >= 2 && active.Strength < 5;
            if (canUseAbility && canAttach)
            {
                view.ShowTripleActions(
                    attack, new UnityAction(SetAttackMode),
                    ability, new UnityAction(ToggleAbilityMode),
                    attachment, new UnityAction(ToggleAttachMode));
            }
            else if (canUseAbility)
            {
                view.ShowDualActions(
                    attack, new UnityAction(SetAttackMode),
                    ability, new UnityAction(ToggleAbilityMode));
            }
            else if (canAttach)
            {
                view.ShowDualActions(
                    attack, new UnityAction(SetAttackMode),
                    attachment, new UnityAction(ToggleAttachMode));
            }
            else
            {
                view.ShowClassAction(attack, new UnityAction(SetAttackMode));
            }
        }

        private string CurrentBattlePrompt()
        {
            BattlePresentationCard active = BattlePresentationViewStateMapper.FindBySlot(
                state.Boards[state.LocalPlayerIndex], state.ActiveSlot);
            return mode switch
            {
                TargetMode.Ability => "ABILITA: tocca il bersaglio "
                    + (BattlePresentationViewStateMapper.AbilityTargetsEnemy(active) ? "nemico." : "alleato."),
                TargetMode.Attachment => "ATTACH: tocca una carta alleata da potenziare.",
                _ => $"Turno di {active?.CardName}: scegli un'azione sulla carta o ispeziona il campo."
            };
        }

        private string LastLogOr(string fallback)
        {
            IReadOnlyList<string> log = state.Log;
            return log.Count > 0 ? log[log.Count - 1] : fallback;
        }

        private static Sprite LoadUiSprite(string name) =>
            Resources.Load<Sprite>("UI/" + name);

        private bool IsTileClickable(int player)
        {
            if (!state.IsLocalBattleTurn)
                return false;
            bool enemyTile = player != state.LocalPlayerIndex;
            return mode switch
            {
                TargetMode.Attack => enemyTile,
                TargetMode.Attachment => !enemyTile,
                TargetMode.Ability => BattlePresentationViewStateMapper.AbilityTargetsEnemy(
                    BattlePresentationViewStateMapper.FindBySlot(
                        state.Boards[state.LocalPlayerIndex], state.ActiveSlot)) == enemyTile,
                _ => false
            };
        }

        private bool IsTargetModeSelection(int player)
        {
            if (!state.IsLocalBattleTurn)
                return false;
            bool enemyTile = player != state.LocalPlayerIndex;
            return mode switch
            {
                TargetMode.Attack => enemyTile,
                TargetMode.Attachment => !enemyTile,
                TargetMode.Ability => BattlePresentationViewStateMapper.AbilityTargetsEnemy(
                    BattlePresentationViewStateMapper.FindBySlot(
                        state.Boards[state.LocalPlayerIndex], state.ActiveSlot)) == enemyTile,
                _ => false
            };
        }

        private void OnBattlefieldCardClicked(BattlefieldCardKey key)
        {
            int player = BattlePresentationViewStateMapper.PlayerForSide(state, key.Side);
            OnTileClicked(player, key.Slot);
        }

        private void OnBattlefieldCardInspected(BattlefieldCardKey key)
        {
            int player = BattlePresentationViewStateMapper.PlayerForSide(state, key.Side);
            BattlePresentationCard card = BattlePresentationViewStateMapper.FindBySlot(state.Boards[player], key.Slot);
            if (card == null)
                return;

            CardDefinition definition = FindCardDefinition(card.CardId);
            if (definition == null)
                return;

            string extra = $"Vite {Mathf.Max(0, card.Lives)}/2"
                + (card.Initiative > 0 ? $"\nIniziativa {card.Initiative}" : string.Empty);
            inspectionOverlay?.Show(definition, BattlePresentationViewStateMapper.CardStatuses(card), extra);
        }

        private void OnTileClicked(int player, int slot)
        {
            switch (mode)
            {
                case TargetMode.Attack:
                    actions.Attack(slot);
                    break;
                case TargetMode.Ability:
                    actions.UseAbility(player != state.LocalPlayerIndex, slot);
                    mode = TargetMode.Attack;
                    break;
                case TargetMode.Attachment:
                    actions.Attach(slot);
                    mode = TargetMode.Attack;
                    break;
            }
            Rebuild();
        }

        private void ToggleAbilityMode()
        {
            BattlePresentationCard active = BattlePresentationViewStateMapper.FindBySlot(
                state.Boards[state.LocalPlayerIndex], state.ActiveSlot);
            if (active != null && active.HeroClass == HeroClass.Warrior)
            {
                actions.UseAbility(false, active.Slot);
                mode = TargetMode.Attack;
            }
            else
            {
                mode = mode == TargetMode.Ability ? TargetMode.Attack : TargetMode.Ability;
            }
            Rebuild();
        }

        private void SetAttackMode()
        {
            mode = TargetMode.Attack;
            Rebuild();
        }

        private void CancelTargetMode()
        {
            mode = TargetMode.Attack;
            Rebuild();
        }

        private void ToggleAttachMode()
        {
            mode = mode == TargetMode.Attachment ? TargetMode.Attack : TargetMode.Attachment;
            Rebuild();
        }

        private void ToggleDecisive(int loadoutIndex)
        {
            if (!decisiveSelection.Remove(loadoutIndex)
                && decisiveSelection.Count < state.DecisiveRequiredCount)
                decisiveSelection.Add(loadoutIndex);
            Rebuild();
        }

        private void ConfirmDecisive()
        {
            if (decisiveSelection.Count != state.DecisiveRequiredCount)
                return;
            var indices = new List<int>(decisiveSelection);
            indices.Sort();
            decisiveSelection.Clear();
            actions.SubmitDecisive(indices.ToArray());
        }

        private CardDefinition FindCardDefinition(string definitionId)
        {
            if (database == null || string.IsNullOrWhiteSpace(definitionId))
                return null;
            CardDefinition definition = database.FindById(definitionId);
            if (definition != null)
                return definition;
            return database.FindById(definitionId.Replace('_', '-'));
        }

        private static string ShortCardName(string definitionId) =>
            string.IsNullOrEmpty(definitionId) ? "?" : definitionId.Replace('-', '\n');

    }
}
