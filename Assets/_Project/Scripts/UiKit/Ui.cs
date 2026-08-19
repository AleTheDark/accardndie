using System;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.UiKit
{
    /// <summary>
    /// Primitive di costruzione e disposizione per UI uGUI create da codice.
    ///
    /// Contiene solo cio' che non sa niente del gioco: creare un rect, ancorarlo, un'immagine,
    /// un bottone trasparente. Tema, localizzazione e widget specifici restano fuori — e'
    /// quella la riga che tiene questo assembly senza riferimenti e quindi riutilizzabile.
    /// </summary>
    public static class Ui
    {
        /// <summary>
        /// Ancora <paramref name="rect"/> fra i due estremi normalizzati, azzerando gli offset.
        /// </summary>
        public static void SetRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Ancora <paramref name="rect"/> a tutto il genitore, con un margine uniforme.</summary>
        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            host.transform.SetParent(parent, false);
            Image image = host.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Zona cliccabile invisibile con un accenno di feedback su hover e pressione.
        /// </summary>
        public static Button CreateTransparentButton(string name, Transform parent)
        {
            Image image = CreateImage(name, parent, Color.clear);
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.disabledColor = Color.clear;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            return button;
        }

        /// <summary>Riga orizzontale di carte, centrata sull'ancora indicata.</summary>
        public static RectTransform CreateCardRow(string name, Transform parent, Vector2 anchor)
        {
            GameObject host = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            host.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)host.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1050f, 285f);

            HorizontalLayoutGroup layout = host.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 34f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rect;
        }

        /// <summary>
        /// Fa coprire al sprite tutto il genitore mantenendo le proporzioni. Se lo sprite
        /// manca si usa <paramref name="fallbackAspectRatio"/>, cosi' lo sfondo non collassa
        /// mentre l'immagine sta ancora caricando.
        /// </summary>
        public static AspectRatioFitter ConfigureFittedBackground(Image image, Sprite sprite, float fallbackAspectRatio)
        {
            image.preserveAspect = true;

            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>()
                ?? image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite != null
                ? sprite.rect.width / sprite.rect.height
                : fallbackAspectRatio;
            return fitter;
        }

        /// <summary>
        /// In verticale lo schermo e' stretto e il testo va ingrandito, altrimenti su telefono
        /// diventa illeggibile.
        /// </summary>
        public static int ResponsiveTextSize(int size)
        {
            return Screen.height > Screen.width ? Mathf.CeilToInt(size * 1.18f) : size;
        }

        public static int ResponsiveTextMinSize(int size)
        {
            return Mathf.Max(Screen.height > Screen.width ? 16 : 12, Mathf.RoundToInt(size * 0.74f));
        }
    }
}
