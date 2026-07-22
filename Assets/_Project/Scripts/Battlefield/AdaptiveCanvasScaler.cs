using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Rende un canvas pronto per landscape e portrait: aggiorna risoluzione di
    /// riferimento e match del CanvasScaler quando cambia orientamento o finestra.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        private static readonly Vector2 LandscapeReference = new(1920f, 1080f);
        private static readonly Vector2 PortraitReference = new(1080f, 1920f);

        private CanvasScaler scaler;
        private int lastWidth;
        private int lastHeight;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
                Apply();
        }

        private void Apply()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            float aspect = lastWidth / (float)Mathf.Max(1, lastHeight);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            if (aspect >= 1f)
            {
                scaler.referenceResolution = LandscapeReference;
                // Ultrawide: privilegia l'altezza per non rimpicciolire la UI.
                scaler.matchWidthOrHeight = aspect >= 1.85f ? 1f : 0.5f;
            }
            else
            {
                scaler.referenceResolution = PortraitReference;
                scaler.matchWidthOrHeight = 0f;
            }
        }
    }
}
