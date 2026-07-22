using UnityEngine;
using UnityEngine.EventSystems;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Micro-feedback da MMO per i bottoni: leggera crescita in hover e
    /// compressione alla pressione. Solo scala locale, non tocca il layout.
    /// </summary>
    public sealed class UiButtonMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.04f;
        private const float PressedScale = 0.96f;
        private const float Speed = 16f;

        private bool hovered;
        private bool pressed;
        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;

        private void OnEnable()
        {
            if (!baseScaleCaptured)
            {
                baseScale = transform.localScale;
                baseScaleCaptured = true;
            }
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            if (baseScaleCaptured)
                transform.localScale = baseScale;
        }

        private void Update()
        {
            float target = pressed ? PressedScale : hovered ? HoverScale : 1f;
            Vector3 goal = baseScale * target;
            transform.localScale = Vector3.Lerp(transform.localScale, goal, Time.unscaledDeltaTime * Speed);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;

        public void OnPointerUp(PointerEventData eventData) => pressed = false;
    }
}
