using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Tiene il frame rate basso quando lo schermo e' fermo e lo alza solo
    /// finche' qualcosa si muove davvero: una battaglia in corso, un dado che
    /// rotola, un dito sul vetro. Un gioco a turni passa gran parte del tempo
    /// a farsi leggere, e disegnare sessanta volte al secondo un'immagine
    /// immobile era la voce piu' pesante sul consumo di batteria.
    /// </summary>
    public static class FrameRateGovernor
    {
        /// <summary>Schermate ferme: hub, negozio, taverna, santuario, menu.</summary>
        public const int IdleFrameRate = 30;

        /// <summary>Battaglia, animazioni, interazione diretta.</summary>
        public const int ActiveFrameRate = 60;

        /// <summary>
        /// Coda dopo l'ultimo tocco. Copre l'inerzia degli scroll e i tween
        /// dei bottoni, che partono quando il dito ha gia' lasciato lo schermo.
        /// </summary>
        private const float InputBoostSeconds = 0.85f;

        private static readonly HashSet<Object> holds = new HashSet<Object>();
        private static readonly System.Predicate<Object> destroyed = hold => hold == null;

        private static bool highFrameRateScreen;
        private static float boostedUntil;
        private static int appliedFrameRate = -1;

        /// <summary>
        /// Falso in editor e su desktop: li' il frame rate resta quello di
        /// sistema e nessuna delle chiamate qui sotto ha effetto.
        /// </summary>
        public static bool Enabled { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            Enabled = true;
            QualitySettings.vSyncCount = 0;
            holds.Clear();
            highFrameRateScreen = false;
            appliedFrameRate = -1;

            // Il caricamento iniziale e' tutto tranne che fermo: si parte alti
            // e si scende quando il giocatore si e' fermato a guardare l'hub.
            Apply(ActiveFrameRate);
            Boost(4f);

            var driverObject = new GameObject("Frame Rate Governor");
            Object.DontDestroyOnLoad(driverObject);
            driverObject.AddComponent<FrameRateGovernorDriver>();
#endif
        }

        /// <summary>
        /// Blocca il frame rate alto finche' <paramref name="token"/> non lo
        /// rilascia. Ripetere la chiamata con lo stesso token non fa nulla, e
        /// un token distrutto senza rilascio viene raccolto al tick successivo:
        /// nessuno dei due casi puo' lasciare il gioco incastrato a sessanta.
        /// </summary>
        public static void Acquire(Object token)
        {
            if (token != null && holds.Add(token))
                Refresh();
        }

        public static void Release(Object token)
        {
            if (token != null && holds.Remove(token))
                Refresh();
        }

        /// <summary>
        /// Vero per le schermate che devono restare a sessanta a prescindere
        /// (la battaglia, dove le animazioni partono senza preavviso).
        /// </summary>
        public static void SetHighFrameRateScreen(bool value)
        {
            if (highFrameRateScreen == value)
                return;

            highFrameRateScreen = value;
            Refresh();
        }

        /// <summary>Alza il frame rate per i prossimi <paramref name="seconds"/>.</summary>
        public static void Boost(float seconds = InputBoostSeconds)
        {
            boostedUntil = Mathf.Max(boostedUntil, Time.unscaledTime + seconds);
            Refresh();
        }

        internal static void Tick()
        {
            if (!Enabled)
                return;

            PollInput();
            Refresh();
        }

        private static void PollInput()
        {
            // Pointer copre sia il touch sia il mouse: basta sapere che c'e'
            // un dito appoggiato, non dove.
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                Boost();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed)
                Boost();
        }

        private static void Refresh()
        {
            if (!Enabled)
                return;

            if (holds.Count > 0)
                holds.RemoveWhere(destroyed);

            bool high = highFrameRateScreen
                || holds.Count > 0
                || Time.unscaledTime < boostedUntil;
            Apply(high ? ActiveFrameRate : IdleFrameRate);
        }

        private static void Apply(int frameRate)
        {
            if (appliedFrameRate == frameRate)
                return;

            appliedFrameRate = frameRate;
            Application.targetFrameRate = frameRate;
        }
    }

    /// <summary>
    /// Unico Update del governor: sonda l'input e rivaluta il bersaglio.
    /// Vive in DontDestroyOnLoad, fuori da qualsiasi scena.
    /// </summary>
    internal sealed class FrameRateGovernorDriver : MonoBehaviour
    {
        private void Update() => FrameRateGovernor.Tick();
    }
}
