using UnityEngine;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Decide quali VFX procedurali della UI hanno il permesso di animarsi.
    /// Scintille e bagliori riscrivono colori e transform a ogni frame, e ogni
    /// scrittura sporca il canvas che li contiene: e' un prezzo che ha senso
    /// pagare per quello che il giocatore sta guardando, non per una fila di
    /// bottoni scorsa fuori dallo schermo o per l'hub sepolto sotto un modale.
    /// </summary>
    public static class UiVfxBudget
    {
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// Quando e' valorizzata, solo i VFX che stanno sotto questa gerarchia
        /// possono animarsi: e' il pannello in primo piano, e tutto il resto e'
        /// coperto. Nulla equivale a "nessun modale aperto".
        /// </summary>
        public static Transform ForegroundRoot { get; private set; }

        public static void SetForegroundRoot(Transform root) => ForegroundRoot = root;

        /// <summary>
        /// Vero se vale la pena animare il VFX ancorato a <paramref name="rect"/>.
        /// <paramref name="canvas"/> serve solo a risolvere la camera dei canvas
        /// non overlay: puo' essere nullo.
        /// </summary>
        public static bool ShouldAnimate(RectTransform rect, Canvas canvas)
        {
            if (rect == null)
                return false;

            if (ForegroundRoot != null && !rect.IsChildOf(ForegroundRoot))
                return false;

            return IsOnScreen(rect, canvas);
        }

        private static bool IsOnScreen(RectTransform rect, Canvas canvas)
        {
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            rect.GetWorldCorners(corners);

            float minimumX = float.MaxValue;
            float maximumX = float.MinValue;
            float minimumY = float.MaxValue;
            float maximumY = float.MinValue;
            for (int index = 0; index < corners.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                minimumX = Mathf.Min(minimumX, point.x);
                maximumX = Mathf.Max(maximumX, point.x);
                minimumY = Mathf.Min(minimumY, point.y);
                maximumY = Mathf.Max(maximumY, point.y);
            }

            return maximumX > 0f
                && minimumX < Screen.width
                && maximumY > 0f
                && minimumY < Screen.height;
        }
    }
}
