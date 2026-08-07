using System;
using System.Collections.Generic;
using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AccardND.Presentation
{
internal sealed class DeploymentHandSwipeSelector : MonoBehaviour,
    IPointerDownHandler,
    IPointerEnterHandler,
    IPointerUpHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private const float DeployScreenHeightRatio = 0.5f;
    private const float DeployMinimumUpwardPixels = 60f;
    private const float DeployVerticalDominance = 1.15f;

    private static DeploymentHandSwipeSelector active;
    private static readonly HashSet<DeploymentHandSwipeSelector> selectors = new();
    private static readonly List<CardHitSnapshot> hitSnapshots = new();
    private static object activeGroup;
    private static int activePointerId = int.MinValue;
    private static Vector2 activeSelectionOrigin;
    private static Button pressedButton;
    private static Button suppressedButton;
    private static int suppressClickUntilFrame = -1;

    private object group;
    private PrototypeCardView view;
    private Func<bool> canSelect;
    private Action select;
    private Action commit;
    private Action preview;
    private Action clearPreview;

    private readonly struct CardHitSnapshot
    {
        internal readonly DeploymentHandSwipeSelector Selector;
        internal readonly Vector2 BottomLeft;
        internal readonly Vector2 TopLeft;
        internal readonly Vector2 TopRight;
        internal readonly Vector2 BottomRight;
        internal readonly int VisualOrder;

        internal CardHitSnapshot(
            DeploymentHandSwipeSelector selector,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            int visualOrder)
        {
            Selector = selector;
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            VisualOrder = visualOrder;
        }
    }

    internal void Configure(
        object selectionGroup,
        PrototypeCardView cardView,
        Func<bool> canSelectCard,
        Action selectCard,
        Action commitSelection,
        Action previewCard = null,
        Action clearCardPreview = null)
    {
        group = selectionGroup;
        view = cardView;
        canSelect = canSelectCard;
        select = selectCard;
        commit = commitSelection;
        preview = previewCard;
        clearPreview = clearCardPreview;
        selectors.Add(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanSelect(eventData))
            return;

        BeginGesture(eventData.pointerId, view.Button, eventData.position);
        PreviewThisCard();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // The gesture must only start from a real pointer-down on a card.
        // Starting here can create a ghost gesture in the release frame when an
        // action overlay disappears and exposes the neighbouring card beneath it.
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activeGroup != group || activePointerId != eventData.pointerId)
            return;

        DeploymentHandSwipeSelector selected = active;
        bool canFinalizeSelection = selected != null
            && selected.select != null
            && selected.canSelect != null
            && selected.canSelect();
        suppressedButton = pressedButton;
        suppressClickUntilFrame = Time.frameCount + 1;
        ResetGesture();
        if (canFinalizeSelection)
            selected.select.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // La selezione viene aperta da OnPointerDown. Implementare questo handler
        // garantisce che Unity continui a inviare OnDrag mentre il dito lascia la mano.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (activeGroup != group || activePointerId != eventData.pointerId || active == null)
            return;

        TryCommitDeployment(eventData.position);
    }

    private void Update()
    {
        if (active != this)
            return;

        if (TryGetPointerPosition(out Vector2 pointerPosition))
        {
            SelectCardAt(pointerPosition);
            TryCommitDeployment(pointerPosition);
        }

        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
            ResetGesture();
    }

    private static void TryCommitDeployment(Vector2 pointerPosition)
    {
        if (active == null)
            return;

        Vector2 delta = pointerPosition - activeSelectionOrigin;
        float deployLine = Screen.height * DeployScreenHeightRatio;
        if (pointerPosition.y < deployLine
            || delta.y < DeployMinimumUpwardPixels
            || delta.y < Mathf.Abs(delta.x) * DeployVerticalDominance)
            return;

        DeploymentHandSwipeSelector selected = active;
        bool canCommit = selected.commit != null
            && selected.select != null
            && selected.canSelect != null
            && selected.canSelect();
        suppressedButton = pressedButton;
        suppressClickUntilFrame = Time.frameCount + 1;
        ResetGesture();
        if (canCommit)
        {
            selected.select.Invoke();
            selected.commit.Invoke();
        }
    }

    private static bool TryGetPointerPosition(out Vector2 position)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            position = Pointer.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // OnPointerUp chiude la gesture senza confermare se la soglia non è stata superata.
    }

    private bool CanSelect(PointerEventData eventData)
    {
        return eventData != null
            && eventData.button == PointerEventData.InputButton.Left
            && !StartedOnChildActionButton(eventData)
            && view != null
            && view.Button != null
            && view.Button.interactable
            && canSelect != null
            && canSelect();
    }

    private bool StartedOnChildActionButton(PointerEventData eventData)
    {
        GameObject hitObject = eventData.pointerCurrentRaycast.gameObject;
        if (hitObject == null)
            hitObject = eventData.pointerPress;
        if (hitObject == null)
            return false;

        Button hitButton = hitObject.GetComponentInParent<Button>();
        return hitButton != null && hitButton != view?.Button;
    }

    private void PreviewThisCard()
    {
        if (active == this)
            return;

        if (active != null && active.view != null)
        {
            active.view.SetDraftSelected(false);
            active.clearPreview?.Invoke();
        }
        active = this;
        view?.SetDraftSelected(true);
        preview?.Invoke();
    }

    private void BeginGesture(int pointerId, Button sourceButton, Vector2 pointerPosition)
    {
        activeGroup = group;
        activePointerId = pointerId;
        activeSelectionOrigin = pointerPosition;
        pressedButton = sourceButton;
        CaptureHitSnapshots();
    }

    private static void CaptureHitSnapshots()
    {
        hitSnapshots.Clear();
        foreach (DeploymentHandSwipeSelector candidate in selectors)
        {
            if (candidate == null || candidate.group != activeGroup || !candidate.CanBeSelected())
                continue;

            RectTransform rect = candidate.view.RectTransform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = candidate.view.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            hitSnapshots.Add(new CardHitSnapshot(
                candidate,
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[1]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[2]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[3]),
                rect.GetSiblingIndex()));
        }
    }

    private static void SelectCardAt(Vector2 screenPosition)
    {
        CardHitSnapshot? best = null;
        foreach (CardHitSnapshot snapshot in hitSnapshots)
        {
            if (!snapshot.Selector.CanBeSelected() || !ContainsPoint(snapshot, screenPosition))
                continue;
            if (!best.HasValue || snapshot.VisualOrder > best.Value.VisualOrder)
                best = snapshot;
        }

        if (!best.HasValue || best.Value.Selector == active)
            return;

        best.Value.Selector.PreviewThisCard();
    }

    private static bool ContainsPoint(CardHitSnapshot card, Vector2 point)
    {
        bool? sign = null;
        Vector2[] corners = { card.BottomLeft, card.TopLeft, card.TopRight, card.BottomRight };
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 edge = corners[(i + 1) % corners.Length] - corners[i];
            Vector2 relative = point - corners[i];
            float cross = edge.x * relative.y - edge.y * relative.x;
            if (Mathf.Abs(cross) < 0.01f)
                continue;
            bool currentSign = cross > 0f;
            if (sign.HasValue && sign.Value != currentSign)
                return false;
            sign = currentSign;
        }
        return true;
    }

    private bool CanBeSelected()
    {
        return view != null
            && view.Button != null
            && view.Button.interactable
            && canSelect != null
            && canSelect();
    }

    private void OnDisable()
    {
        selectors.Remove(this);
        if (active == this || activeGroup == group)
            ResetGesture();
    }

    private void OnEnable()
    {
        if (group != null)
            selectors.Add(this);
    }

    private static void ResetGesture()
    {
        if (active != null && active.view != null)
        {
            active.view.SetDraftSelected(false);
            active.clearPreview?.Invoke();
        }
        active = null;
        activeGroup = null;
        activePointerId = int.MinValue;
        activeSelectionOrigin = Vector2.zero;
        pressedButton = null;
        hitSnapshots.Clear();
    }

    internal static bool ShouldSuppressClick(Button button)
    {
        return button != null
            && button == suppressedButton
            && Time.frameCount <= suppressClickUntilFrame;
    }
}
}
