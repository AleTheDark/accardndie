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
    private static bool activeGestureUsesTouch;
    /// <summary>
    /// Dito che ha aperto la gesture: seguire `primaryTouch` voleva dire seguire
    /// il primo dito appoggiato sullo schermo, che in landscape puo' essere un
    /// pollice fermo sul bordo mentre la mano la si sfoglia con l'altra mano.
    /// </summary>
    private static int activeTouchId = -1;

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

        BeginGesture(eventData, view.Button);
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

        FinalizeGesture();
    }

    /// <summary>
    /// Chiude la gesture scegliendo la carta sotto al dito. La chiamano sia il
    /// rilascio dell'EventSystem sia <see cref="Update"/>: l'ordine fra i due non e'
    /// garantito e chi arriva secondo trova lo stato gia' pulito, quindi esce subito.
    /// </summary>
    private static void FinalizeGesture()
    {
        DeploymentHandSwipeSelector selected = active;
        bool canFinalizeSelection = CanFinalize(selected);
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

        bool pointerFound = TryGetPointerPosition(out Vector2 pointerPosition);
        try
        {
            if (pointerFound)
            {
                SelectCardAt(pointerPosition);
                TryCommitDeployment(pointerPosition);
            }
        }
        catch (Exception exception)
        {
            // La gesture va chiusa comunque: ripetere l'eccezione ogni frame
            // lascerebbe la mano bloccata per il resto della partita.
            Debug.LogWarning($"SWIPE MANO - gesture interrotta da un errore: {exception}");
            ForceReset();
            return;
        }

        // Il device che ha aperto la gesture non e' piu' premuto: la gesture e' finita.
        // Chiudendola qui il tap resta valido anche quando l'EventSystem consegna
        // OnPointerUp dopo questo Update, invece di perdere la selezione.
        if (!pointerFound)
            FinalizeGesture();
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
        bool canCommit = selected.commit != null && CanFinalize(selected);
        suppressedButton = pressedButton;
        suppressClickUntilFrame = Time.frameCount + 1;
        ResetGesture();
        if (canCommit)
        {
            selected.select.Invoke();
            selected.commit.Invoke();
        }
    }

    /// <summary>
    /// Interroga il controller senza mai lasciare la gesture aperta: un predicato
    /// che esplode non deve impedire la chiusura dello stato statico.
    /// </summary>
    private static bool CanFinalize(DeploymentHandSwipeSelector selector)
    {
        if (selector == null || selector.select == null || selector.canSelect == null)
            return false;

        try
        {
            return selector.canSelect();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SWIPE MANO - predicato di selezione fallito: {exception.Message}");
            return false;
        }
    }

    private static bool TryGetPointerPosition(out Vector2 position)
    {
        if (activeGestureUsesTouch)
        {
            UnityEngine.InputSystem.Controls.TouchControl touch =
                PrototypeCardView.FindTouch(activeTouchId);
            if (touch != null && touch.press.isPressed)
            {
                position = touch.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        // Gesture da mouse: leggiamo il mouse, non `Pointer.current`. Con un tocco
        // fantasma bloccato Pointer.current puo' essere il touchscreen, e seguiremmo
        // la sua posizione ferma invece del cursore.
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (!mouse.leftButton.isPressed)
            {
                position = default;
                return false;
            }

            position = mouse.position.ReadValue();
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
            && CanBeSelected();
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
            InvokeSafely(active.clearPreview, "chiusura anteprima");
        }
        active = this;
        if (view != null)
            view.SetDraftSelected(true);
        InvokeSafely(preview, "apertura anteprima");
    }

    /// <summary>
    /// I callback vivono nel controller di battaglia: se uno esplode la gesture
    /// deve comunque restare chiudibile, altrimenti la mano si blocca.
    /// </summary>
    private static void InvokeSafely(Action callback, string description)
    {
        if (callback == null)
            return;

        try
        {
            callback.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SWIPE MANO - {description} fallita: {exception}");
        }
    }

    private void BeginGesture(PointerEventData eventData, Button sourceButton)
    {
        activeGroup = group;
        activePointerId = eventData.pointerId;
        activeSelectionOrigin = eventData.position;
        pressedButton = sourceButton;
        // Il device va fissato adesso. Leggerlo frame per frame significherebbe
        // seguire il touchscreen ogni volta che riporta un press, anche quando il
        // press e' un fantasma bloccato e la gesture vera arriva dal mouse: la mano
        // seguirebbe una posizione ferma e potrebbe schierare da sola.
        activeTouchId = PrototypeCardView.TouchIdOf(eventData);
        activeGestureUsesTouch = PrototypeCardView.IsPointerEventFromTouchscreen(eventData);
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
            if (snapshot.Selector == null
                || !snapshot.Selector.CanBeSelected()
                || !ContainsPoint(snapshot, screenPosition))
            {
                continue;
            }
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
        if (view == null || view.Button == null || !view.Button.interactable || canSelect == null)
            return false;

        try
        {
            return canSelect();
        }
        catch (Exception exception)
        {
            // Girato in Update: senza questa rete un predicato rotto ripeterebbe
            // l'eccezione ogni frame e la gesture non si chiuderebbe mai.
            Debug.LogWarning($"SWIPE MANO - predicato di selezione fallito: {exception.Message}");
            return false;
        }
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
        // Prima si chiude la gesture, poi si ripulisce la grafica: se la pulizia
        // fallisce (view distrutta, callback del controller rotto) lo stato statico
        // deve comunque tornare libero, altrimenti la mano resta muta per sempre.
        DeploymentHandSwipeSelector previous = active;
        active = null;
        activeGroup = null;
        activePointerId = int.MinValue;
        activeSelectionOrigin = Vector2.zero;
        pressedButton = null;
        activeGestureUsesTouch = false;
        activeTouchId = -1;
        hitSnapshots.Clear();

        if (previous == null || previous.view == null)
            return;

        try
        {
            previous.view.SetDraftSelected(false);
            previous.clearPreview?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SWIPE MANO - preview non rilasciata durante il reset: {exception.Message}");
        }
    }

    /// <summary>
    /// Vero finche' resta uno stato di selezione da rilasciare, anche quando il
    /// selettore che lo teneva e' gia' stato distrutto.
    /// </summary>
    internal static bool HasActiveGesture =>
        !ReferenceEquals(active, null) || !ReferenceEquals(activeGroup, null);

    /// <summary>
    /// Rilascio incondizionato dello stato statico della mano. Sempre sicura da
    /// chiamare: senza gesture in corso non fa nulla.
    /// </summary>
    internal static void ForceReset()
    {
        selectors.RemoveWhere(selector => selector == null);
        ResetGesture();
        suppressedButton = null;
        suppressClickUntilFrame = -1;
    }

    internal static bool ShouldSuppressClick(Button button)
    {
        return button != null
            && button == suppressedButton
            && Time.frameCount <= suppressClickUntilFrame;
    }
}
}
