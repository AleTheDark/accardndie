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

        /// <summary>Sotto questa soglia la scala e' "arrivata" e si aggancia.</summary>
        private const float ScaleRestThreshold = 0.001f;

        /// <summary>Idem per l'etichetta, che si muove in pixel invece che in scala.</summary>
        private const float LabelRestThreshold = 0.02f;

        /// <summary>
        /// Per quanti frame cercare il figlio "Label" prima di rinunciare. Puo'
        /// essere creato dopo di noi, ma se dopo un secondo non c'e' non arrivera'
        /// piu': continuare a cercarlo sarebbe una Find per frame a vita.
        /// </summary>
        private const int LabelSearchFrames = 60;

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
        private int labelSearchFramesLeft = LabelSearchFrames;
        private bool atRest;

        private void OnEnable()
        {
            selectable = GetComponent<Selectable>();
            if (!baseScaleCaptured)
            {
                baseScale = transform.localScale;
                baseScaleCaptured = true;
            }

            // Un bottone appena riacceso puo' aver ricevuto la sua etichetta nel
            // frattempo, e deve poter tornare alla scala di riposo.
            atRest = false;
            if (!labelPositionCaptured)
                labelSearchFramesLeft = LabelSearchFrames;
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
            bool hasLabel = labelPositionCaptured && label != null;
            Vector2 labelGoal = labelBasePosition + (pressed ? new Vector2(0f, -2f) : Vector2.zero);

            // Il Lerp esponenziale si avvicina alla meta senza mai toccarla:
            // senza questo aggancio finale ogni bottone a riposo riscriverebbe
            // la propria scala a ogni frame, e un canvas con una scala che
            // cambia e' un canvas da ricostruire. Con centinaia di bottoni in
            // scena era lavoro pagato per non muovere niente.
            bool scaleArrived = (transform.localScale - goal).sqrMagnitude
                <= ScaleRestThreshold * ScaleRestThreshold;
            bool labelArrived = !hasLabel || (label.anchoredPosition - labelGoal).sqrMagnitude
                <= LabelRestThreshold * LabelRestThreshold;

            if (scaleArrived && labelArrived)
            {
                if (!atRest)
                {
                    transform.localScale = goal;
                    if (hasLabel)
                        label.anchoredPosition = labelGoal;
                    atRest = true;
                }

                return;
            }

            atRest = false;
            float blend = 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, goal, blend);

            if (hasLabel)
                label.anchoredPosition = Vector2.Lerp(label.anchoredPosition, labelGoal, blend);
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
            if (labelPositionCaptured || labelSearchFramesLeft <= 0)
                return;

            labelSearchFramesLeft--;
            Transform labelTransform = transform.Find("Label");
            if (labelTransform is not RectTransform rect)
                return;

            label = rect;
            labelBasePosition = rect.anchoredPosition;
            labelPositionCaptured = true;
        }
    }
}
