using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    internal interface IPvpMatchActions
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
    /// Schermata match: ridisegna interamente il contenuto a ogni cambiamento di
    /// stato (turn-based, la semplicità vale più del costo di rebuild).
    /// </summary>
    internal sealed class PvpMatchScreen
    {
        private enum TargetMode
        {
            Attack,
            Ability,
            Attachment
        }

        private readonly RectTransform root;
        private readonly PvpClientMatchState state;
        private readonly IPvpMatchActions actions;
        private readonly List<LoadoutCardDto> myLoadout;
        private readonly CardDatabase database;
        private readonly HashSet<int> decisiveSelection = new();
        private TargetMode mode = TargetMode.Attack;

        public PvpMatchScreen(
            Transform parent,
            PvpClientMatchState state,
            IPvpMatchActions actions,
            List<LoadoutCardDto> myLoadout,
            CardDatabase database)
        {
            this.state = state;
            this.actions = actions;
            this.myLoadout = myLoadout;
            this.database = database;
            root = PvpUiFactory.CreatePanel(parent, "Match", new Color(0.05f, 0.08f, 0.11f, 0.98f));
            PvpUiFactory.Stretch(root);
            Rebuild();
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy() => Object.Destroy(root.gameObject);

        public void Rebuild()
        {
            PvpUiFactory.Clear(root);
            BuildHeader();
            BuildBoard(1 - state.MyIndex, top: true);
            BuildBoard(state.MyIndex, top: false);
            BuildLog();
            BuildActionBar();
        }

        private void BuildHeader()
        {
            string auras = string.Empty;
            if (state.MyIndex >= 0
                && (state.Auras[state.MyIndex] != GameCore.Pvp.PvpAuraType.None
                    || state.Auras[1 - state.MyIndex] != GameCore.Pvp.PvpAuraType.None))
                auras = $"  |  AURA TU: {state.Auras[state.MyIndex]}  VS {state.Auras[1 - state.MyIndex]}";
            int myWins = state.MyIndex >= 0 ? state.Wins[state.MyIndex] : 0;
            int theirWins = state.MyIndex >= 0 ? state.Wins[1 - state.MyIndex] : 0;
            Text header = PvpUiFactory.CreateText(
                root, "Header",
                $"ROUND {state.MatchRound}  |  VIGORE D{state.VigorDieSides}  |  TU {myWins} - {theirWins} {state.OpponentName.ToUpperInvariant()}{auras}",
                22);
            PvpUiFactory.SetAnchors((RectTransform)header.transform, new Vector2(0.02f, 0.94f), new Vector2(0.98f, 0.995f));
        }

        private void BuildBoard(int player, bool top)
        {
            if (player < 0)
                return;
            float yMin = top ? 0.72f : 0.3f;
            float yMax = top ? 0.93f : 0.51f;
            List<PvpClientCard> board = state.Boards[player];
            for (int slot = 0; slot < 3; slot++)
            {
                PvpClientCard card = slot < board.Count ? FindBySlot(board, slot) : null;
                float xMin = 0.05f + slot * 0.32f;
                RectTransform tile = PvpUiFactory.CreatePanel(
                    root, $"Tile{player}-{slot}", TileColor(player, card));
                PvpUiFactory.SetAnchors(tile, new Vector2(xMin, yMin), new Vector2(xMin + 0.29f, yMax));

                if (card == null)
                {
                    PvpUiFactory.CreateText(tile, "Empty", "-", 26);
                    continue;
                }

                Sprite artwork = database != null ? database.FindById(card.CardId)?.Artwork : null;
                if (artwork != null)
                {
                    var artHolder = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artHolder.transform.SetParent(tile, false);
                    var art = artHolder.GetComponent<Image>();
                    art.sprite = artwork;
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    if (card.Eliminated)
                        art.color = new Color(0.35f, 0.3f, 0.3f, 0.75f);
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.84f));
                }

                Text name = PvpUiFactory.CreateText(tile, "Name", CardTitle(card), 18, TextAnchor.UpperCenter);
                name.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)name.transform, new Vector2(0.02f, 0.84f), new Vector2(0.98f, 0.99f));

                Text stats = PvpUiFactory.CreateText(tile, "Stats", CardStats(card), 18, TextAnchor.MiddleCenter, FontStyle.Normal);
                stats.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)stats.transform, new Vector2(0.02f, 0.16f), new Vector2(0.98f, 0.34f));

                Text status = PvpUiFactory.CreateText(tile, "Status", CardStatus(card), 14, TextAnchor.LowerCenter, FontStyle.Normal);
                status.color = new Color(1f, 0.8f, 0.4f);
                status.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)status.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.16f));

                if (!card.Eliminated && IsTileClickable(player))
                {
                    int capturedSlot = slot;
                    Button button = tile.gameObject.AddComponent<Button>();
                    button.onClick.AddListener(() => OnTileClicked(player, capturedSlot));
                }
            }
        }

        private static PvpClientCard FindBySlot(List<PvpClientCard> board, int slot)
        {
            foreach (PvpClientCard card in board)
            {
                if (card.Slot == slot)
                    return card;
            }
            return null;
        }

        private Color TileColor(int player, PvpClientCard card)
        {
            bool mine = player == state.MyIndex;
            if (card == null)
                return new Color(0.12f, 0.14f, 0.18f, 0.9f);
            if (card.Eliminated)
                return new Color(0.16f, 0.09f, 0.09f, 0.85f);
            bool isActiveTurn = state.Phase == PvpClientPhase.Battle
                && state.ActivePlayer == player && state.ActiveSlot == card.Slot;
            if (isActiveTurn)
                return new Color(0.72f, 0.5f, 0.1f, 0.95f);
            return mine
                ? new Color(0.07f, 0.28f, 0.34f, 0.95f)
                : new Color(0.34f, 0.1f, 0.12f, 0.95f);
        }

        private static string CardTitle(PvpClientCard card) =>
            $"{card.CardName}\n{card.HeroClass}";

        private static string CardStats(PvpClientCard card)
        {
            int shownStrength = card.Strength + card.PermanentBonus + card.PendingBonus;
            string hearts = card.Eliminated ? "MORTA" : new string('♥', Mathf.Max(card.Lives, 0));
            string initiative = card.Initiative > 0 ? $"\nINIZIATIVA {card.Initiative}" : string.Empty;
            return $"FORZA {shownStrength}   {hearts}{initiative}";
        }

        private static string CardStatus(PvpClientCard card)
        {
            var flags = new List<string>();
            if (card.IsSpirit)
                flags.Add("SPIRITO");
            if (card.Inhibited)
                flags.Add("INIBITO");
            if (card.Marked)
                flags.Add("MARCATO");
            if (card.Protecting)
                flags.Add("PROTEGGE");
            if (card.DiePenaltySteps > 0)
                flags.Add($"DADO -{card.DiePenaltySteps}");
            if (card.PendingBonus > 0)
                flags.Add($"+{card.PendingBonus} PROSSIMO");
            return string.Join("  ", flags);
        }

        private void BuildLog()
        {
            RectTransform panel = PvpUiFactory.CreatePanel(root, "Log", new Color(0f, 0f, 0f, 0.45f));
            PvpUiFactory.SetAnchors(panel, new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.71f));
            var lines = new List<string>();
            IReadOnlyList<string> log = state.Log;
            for (int index = Mathf.Max(0, log.Count - 5); index < log.Count; index++)
                lines.Add(log[index]);
            Text text = PvpUiFactory.CreateText(
                panel, "Lines", string.Join("\n", lines), 17, TextAnchor.LowerLeft, FontStyle.Normal);
            PvpUiFactory.Stretch((RectTransform)text.transform, 10f, 4f);
        }

        private void BuildActionBar()
        {
            RectTransform bar = PvpUiFactory.CreatePanel(root, "Actions", new Color(0.04f, 0.06f, 0.09f, 0.9f));
            PvpUiFactory.SetAnchors(bar, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.28f));

            switch (state.Phase)
            {
                case PvpClientPhase.Deployment when state.IsMyDeployTurn:
                    BuildHandStrip(bar, "SCHIERA UNA CARTA (tocca per schierare)");
                    break;
                case PvpClientPhase.Deployment:
                    PvpUiFactory.CreateText(bar, "Wait", "L'avversario sta schierando...", 24);
                    break;
                case PvpClientPhase.DecisiveSelection:
                    BuildDecisivePicker(bar);
                    break;
                case PvpClientPhase.Battle when state.IsMyBattleTurn:
                    BuildBattleActions(bar);
                    break;
                case PvpClientPhase.Battle:
                    PvpUiFactory.CreateText(bar, "Wait", "Turno dell'avversario...", 24);
                    break;
                case PvpClientPhase.Finished:
                {
                    bool won = state.Winner == state.MyIndex;
                    Text result = PvpUiFactory.CreateText(
                        bar, "Result", won ? "VITTORIA!" : "SCONFITTA", 42);
                    result.color = won ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.4f, 0.35f);
                    PvpUiFactory.SetAnchors((RectTransform)result.transform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.95f));
                    Button back = PvpUiFactory.CreateButton(
                        bar, "Back", "TORNA ALLA LOBBY", new Color(0.05f, 0.45f, 0.5f, 0.98f), actions.LeaveToLobby);
                    PvpUiFactory.SetAnchors((RectTransform)back.transform, new Vector2(0.32f, 0.08f), new Vector2(0.68f, 0.4f));
                    break;
                }
                default:
                    PvpUiFactory.CreateText(bar, "Wait", "In attesa del server...", 24);
                    break;
            }
        }

        private void BuildHandStrip(RectTransform bar, string caption)
        {
            Text title = PvpUiFactory.CreateText(bar, "Caption", caption, 20);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.02f, 0.75f), new Vector2(0.98f, 0.98f));
            int count = state.Hand.Count;
            for (int position = 0; position < count; position++)
            {
                int captured = position;
                float xMin = 0.02f + position * (0.96f / Mathf.Max(count, 1));
                Button button = PvpUiFactory.CreateButton(
                    bar, $"Hand{position}", ShortCardName(state.Hand[position].DefinitionId),
                    new Color(0.1f, 0.35f, 0.42f, 0.98f), () => actions.Deploy(captured), 16);
                PvpUiFactory.SetAnchors(
                    (RectTransform)button.transform,
                    new Vector2(xMin, 0.08f),
                    new Vector2(xMin + 0.96f / Mathf.Max(count, 1) - 0.01f, 0.7f));
            }
        }

        private void BuildDecisivePicker(RectTransform bar)
        {
            Text title = PvpUiFactory.CreateText(
                bar, "Caption",
                $"ROUND DECISIVO: scegli {state.DecisiveRequiredCount} carte ({decisiveSelection.Count} selezionate)",
                20);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.02f, 0.75f), new Vector2(0.98f, 0.98f));

            for (int index = 0; index < myLoadout.Count; index++)
            {
                int captured = index;
                bool selected = decisiveSelection.Contains(index);
                float xMin = 0.02f + index * (0.86f / myLoadout.Count);
                Button button = PvpUiFactory.CreateButton(
                    bar, $"Pick{index}", ShortCardName(myLoadout[index].definitionId),
                    selected ? new Color(0.75f, 0.55f, 0.1f, 0.98f) : new Color(0.1f, 0.35f, 0.42f, 0.98f),
                    () => ToggleDecisive(captured), 14);
                PvpUiFactory.SetAnchors(
                    (RectTransform)button.transform,
                    new Vector2(xMin, 0.08f),
                    new Vector2(xMin + 0.86f / myLoadout.Count - 0.006f, 0.7f));
            }

            Button confirm = PvpUiFactory.CreateButton(
                bar, "Confirm", "OK", new Color(0.1f, 0.55f, 0.25f, 0.98f), ConfirmDecisive, 24);
            confirm.interactable = decisiveSelection.Count == state.DecisiveRequiredCount;
            PvpUiFactory.SetAnchors((RectTransform)confirm.transform, new Vector2(0.9f, 0.08f), new Vector2(0.98f, 0.7f));
        }

        private void BuildBattleActions(RectTransform bar)
        {
            PvpClientCard active = FindBySlot(state.Boards[state.MyIndex], state.ActiveSlot);
            string caption = mode switch
            {
                TargetMode.Ability => "ABILITÀ: tocca il bersaglio " + (AbilityTargetsEnemy(active) ? "NEMICO" : "ALLEATO"),
                TargetMode.Attachment => "ATTACH: tocca la carta alleata da potenziare",
                _ => $"TURNO DI {active?.CardName}: tocca un nemico per attaccare"
            };
            Text title = PvpUiFactory.CreateText(bar, "Caption", caption, 20);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.02f, 0.72f), new Vector2(0.98f, 0.98f));

            Button pass = PvpUiFactory.CreateButton(
                bar, "Pass", "PASSA", new Color(0.35f, 0.35f, 0.4f, 0.98f), () => actions.Pass());
            PvpUiFactory.SetAnchors((RectTransform)pass.transform, new Vector2(0.03f, 0.12f), new Vector2(0.24f, 0.62f));

            bool canUseAbility = active != null && HasActivatableAbility(active.HeroClass);
            Button ability = PvpUiFactory.CreateButton(
                bar, "Ability", mode == TargetMode.Ability ? "ANNULLA ABILITÀ" : "ABILITÀ",
                new Color(0.28f, 0.12f, 0.45f, 0.98f), ToggleAbilityMode);
            ability.interactable = canUseAbility;
            PvpUiFactory.SetAnchors((RectTransform)ability.transform, new Vector2(0.27f, 0.12f), new Vector2(0.55f, 0.62f));

            bool canAttach = active != null && active.Strength >= 2 && active.Strength < 5;
            Button attach = PvpUiFactory.CreateButton(
                bar, "Attach", mode == TargetMode.Attachment ? "ANNULLA ATTACH" : "ATTACH",
                new Color(0.45f, 0.28f, 0.08f, 0.98f), ToggleAttachMode);
            attach.interactable = canAttach;
            PvpUiFactory.SetAnchors((RectTransform)attach.transform, new Vector2(0.58f, 0.12f), new Vector2(0.86f, 0.62f));
        }

        private bool IsTileClickable(int player)
        {
            if (!state.IsMyBattleTurn)
                return false;
            bool enemyTile = player != state.MyIndex;
            return mode switch
            {
                TargetMode.Attack => enemyTile,
                TargetMode.Attachment => !enemyTile,
                TargetMode.Ability => AbilityTargetsEnemy(
                    FindBySlot(state.Boards[state.MyIndex], state.ActiveSlot)) == enemyTile,
                _ => false
            };
        }

        private void OnTileClicked(int player, int slot)
        {
            switch (mode)
            {
                case TargetMode.Attack:
                    actions.Attack(slot);
                    break;
                case TargetMode.Ability:
                    actions.UseAbility(player != state.MyIndex, slot);
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
            PvpClientCard active = FindBySlot(state.Boards[state.MyIndex], state.ActiveSlot);
            if (active != null && active.HeroClass == HeroClass.Warrior)
            {
                // Il Warrior non ha bersaglio: si arma e poi si attacca normalmente.
                actions.UseAbility(false, active.Slot);
                mode = TargetMode.Attack;
            }
            else
            {
                mode = mode == TargetMode.Ability ? TargetMode.Attack : TargetMode.Ability;
            }
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

        private static bool AbilityTargetsEnemy(PvpClientCard card) =>
            card != null && card.HeroClass is HeroClass.Assassin or HeroClass.Mage or HeroClass.Hunter;

        private static bool HasActivatableAbility(HeroClass heroClass) =>
            heroClass is not (HeroClass.Rogue or HeroClass.Barbarian);

        private static string ShortCardName(string definitionId) =>
            string.IsNullOrEmpty(definitionId) ? "?" : definitionId.Replace('-', '\n');
    }
}
