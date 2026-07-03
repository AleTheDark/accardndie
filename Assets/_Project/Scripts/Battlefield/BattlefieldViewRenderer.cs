using System;
using System.Collections;
using System.Collections.Generic;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Battlefield
{
    public sealed class BattlefieldViewRenderer
    {
        private readonly RectTransform root;
        private readonly GameConfiguration configuration;
        private readonly Font font;
        private readonly Dictionary<BattlefieldCardKey, PrototypeCardView> views = new();
        private readonly List<GameObject> emptySlots = new();
        private BattlefieldViewState currentState;

        public BattlefieldViewRenderer(RectTransform root, GameConfiguration configuration)
        {
            this.root = root;
            this.configuration = configuration;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public event Action<BattlefieldCardKey> CardClicked;
        public event Action<BattlefieldCardKey> CardInspected;

        public bool TryGetCardView(BattlefieldCardKey key, out PrototypeCardView view) =>
            views.TryGetValue(key, out view);

        public void Render(BattlefieldViewState state)
        {
            currentState = state ?? new BattlefieldViewState();
            Clear();
            BuildRow(currentState.TopCards, BattlefieldSide.Top, true);
            BuildRow(currentState.BottomCards, BattlefieldSide.Bottom, false);
        }

        public void Destroy()
        {
            Clear();
            CardClicked = null;
            CardInspected = null;
        }

        private void BuildRow(IReadOnlyList<BattlefieldCardViewState> cards, BattlefieldSide side, bool top)
        {
            int formationSize = Mathf.Max(1, currentState.FormationSize);
            Vector2 rootSize = root.rect.size;
            if (rootSize.x <= 1f || rootSize.y <= 1f)
                rootSize = new Vector2(Screen.width, Screen.height);

            bool compact = rootSize.y > rootSize.x * 1.12f;
            float rowWidth = rootSize.x * (compact ? 0.88f : 0.72f);
            float gap = Mathf.Clamp(rootSize.x * 0.026f, 18f, 42f);
            float maxCardFromWidth = (rowWidth - gap * (formationSize - 1)) / formationSize;
            float maxCardFromHeight = rootSize.y * (compact ? 0.135f : 0.24f);
            float cardSize = Mathf.Max(72f, Mathf.Min(maxCardFromWidth, maxCardFromHeight));
            float step = cardSize + gap;
            float start = -step * (formationSize - 1) * 0.5f;
            float y = rootSize.y * (top
                ? compact ? 0.68f : 0.68f
                : compact ? 0.24f : 0.24f);

            for (int slot = 0; slot < formationSize; slot++)
            {
                BattlefieldCardViewState card = FindCard(cards, side, slot);
                RectTransform holder = CreateHolder(
                    $"{side} Slot {slot}",
                    new Vector2(start + slot * step, y),
                    new Vector2(cardSize, cardSize));

                if (card?.Definition == null)
                {
                    BuildEmptySlot(holder);
                    continue;
                }

                PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview(holder, card.Definition, configuration);
                views[card.Key] = view;
                ConfigureCardRect(view.RectTransform);
                view.SetStrengthValue(card.Strength);
                view.SetSelected(card.Selected);
                view.SetTurnAura(card.ActiveTurn, card.PlayerOwned);
                view.SetHealthBar(card.Lives, Mathf.Max(1, card.MaximumLives), card.PlayerOwned
                    ? new Color(0.08f, 0.68f, 0.72f)
                    : new Color(0.78f, 0.18f, 0.16f));
                view.SetStatuses(card.Statuses == null ? Array.Empty<PrototypeCardView.StatusToken>() : ToArray(card.Statuses));
                view.SetAlpha(card.Eliminated ? 0.48f : 1f);
                bool canClickAction = card.Clickable && !card.Eliminated;
                bool canInspect = card.Inspectable;
                view.SetInteractable(canClickAction || canInspect);
                if (card.PlayEnterAnimation && !card.Eliminated)
                    ScheduleRevealAnimation(view, configuration.Animation.CpuCardRevealDuration);
                if (canClickAction)
                {
                    BattlefieldCardKey captured = card.Key;
                    view.ShowCardClickAction(new UnityAction(() => CardClicked?.Invoke(captured)));
                }
                else if (canInspect)
                {
                    BattlefieldCardKey captured = card.Key;
                    view.Button.onClick.AddListener(new UnityAction(() => CardInspected?.Invoke(captured)));
                }
            }
        }

        private RectTransform CreateHolder(string name, Vector2 anchoredPosition, Vector2 size)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(root, false);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private void BuildEmptySlot(RectTransform holder)
        {
            var panel = new GameObject("Empty Slot", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(holder, false);
            var rect = (RectTransform)panel.transform;
            Stretch(rect);
            Image image = panel.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            emptySlots.Add(panel);
        }

        private static BattlefieldCardViewState FindCard(
            IReadOnlyList<BattlefieldCardViewState> cards,
            BattlefieldSide side,
            int slot)
        {
            if (cards == null)
                return null;
            foreach (BattlefieldCardViewState card in cards)
            {
                if (card != null && card.Key.Side == side && card.Key.Slot == slot)
                    return card;
            }
            return null;
        }

        private static void ConfigureCardRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ScheduleRevealAnimation(PrototypeCardView view, float duration)
        {
            if (view == null)
                return;

            Canvas.ForceUpdateCanvases();
            DelayedRevealAnimation runner = view.gameObject.GetComponent<DelayedRevealAnimation>();
            if (runner == null)
                runner = view.gameObject.AddComponent<DelayedRevealAnimation>();
            runner.Play(view, duration);
        }

        private sealed class DelayedRevealAnimation : MonoBehaviour
        {
            private Coroutine routine;

            public void Play(PrototypeCardView view, float duration)
            {
                if (routine != null)
                    StopCoroutine(routine);
                routine = StartCoroutine(PlayRoutine(view, duration));
            }

            private IEnumerator PlayRoutine(PrototypeCardView view, float duration)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                if (view != null)
                    view.PlayRevealAnimation(duration);
                routine = null;
            }
        }

        private Text CreateText(
            Transform parent,
            string name,
            string content,
            int size,
            FontStyle style,
            TextAnchor anchor)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Text));
            holder.transform.SetParent(parent, false);
            Text text = holder.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void Clear()
        {
            views.Clear();
            emptySlots.Clear();
            for (int index = root.childCount - 1; index >= 0; index--)
                Object.Destroy(root.GetChild(index).gameObject);
        }

        private static PrototypeCardView.StatusToken[] ToArray(IReadOnlyList<PrototypeCardView.StatusToken> statuses)
        {
            var result = new PrototypeCardView.StatusToken[statuses.Count];
            for (int index = 0; index < statuses.Count; index++)
                result[index] = statuses[index];
            return result;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
