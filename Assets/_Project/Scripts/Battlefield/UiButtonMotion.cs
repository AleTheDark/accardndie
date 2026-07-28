using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Micro-feedback condiviso per mouse, touch, tastiera e controller.
    /// Anima solo scala e posizione dell'etichetta, senza alterare il layout.
    /// </summary>
    public sealed class UiButtonMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private const float HoverScale = 1.025f;
        private const float PressedScale = 0.975f;
        private const float Speed = 15f;
        private const float SubmitPulseDuration = 0.12f;

        private bool pointerInside;
        private bool pointerPressed;
        private bool selected;
        private float submitPulse;
        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;
        private Selectable selectable;
        private RectTransform label;
        private Vector2 labelBasePosition;
        private bool labelPositionCaptured;

        private void OnEnable()
        {
            selectable = GetComponent<Selectable>();
            if (!baseScaleCaptured)
            {
                baseScale = transform.localScale;
                baseScaleCaptured = true;
            }
        }

        private void OnDisable()
        {
            pointerInside = false;
            pointerPressed = false;
            selected = false;
            submitPulse = 0f;
            if (baseScaleCaptured)
                transform.localScale = baseScale;
            if (labelPositionCaptured && label != null)
                label.anchoredPosition = labelBasePosition;
        }

        private void Update()
        {
            CaptureLabelIfNeeded();

            if (submitPulse > 0f)
                submitPulse = Mathf.Max(0f, submitPulse - Time.unscaledDeltaTime);

            bool interactable = selectable == null || selectable.IsInteractable();
            bool pressed = interactable && (pointerPressed || submitPulse > 0f);
            bool focused = interactable && (pointerInside || selected);
            float target = pressed ? PressedScale : focused ? HoverScale : 1f;
            Vector3 goal = baseScale * target;
            float blend = 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, goal, blend);

            if (labelPositionCaptured && label != null)
            {
                Vector2 labelGoal = labelBasePosition + (pressed ? new Vector2(0f, -2f) : Vector2.zero);
                label.anchoredPosition = Vector2.Lerp(label.anchoredPosition, labelGoal, blend);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pointerPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => pointerPressed = true;

        public void OnPointerUp(PointerEventData eventData) => pointerPressed = false;

        public void OnSelect(BaseEventData eventData) => selected = true;

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            pointerPressed = false;
        }

        public void OnSubmit(BaseEventData eventData) => submitPulse = SubmitPulseDuration;

        private void CaptureLabelIfNeeded()
        {
            if (labelPositionCaptured)
                return;

            Transform labelTransform = transform.Find("Label");
            if (labelTransform is not RectTransform rect)
                return;

            label = rect;
            labelBasePosition = rect.anchoredPosition;
            labelPositionCaptured = true;
        }
    }
}
