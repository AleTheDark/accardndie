using UnityEngine;

namespace AccardND.Ads
{
    /// <summary>
    /// Ferma il gioco mentre l'annuncio e' a schermo. Su Android l'activity della rete
    /// pubblicitaria manda comunque l'app in background, ma su WebGL no: senza questa pausa
    /// la musica continuerebbe sotto il video e le animazioni andrebbero avanti dietro un
    /// pannello opaco.
    ///
    /// Il ripristino rimette i valori di prima, non dei valori "normali" decisi qui: il gioco
    /// puo' essere gia' in pausa quando parte la pubblicita' (fine run, schermata di riepilogo)
    /// e riprenderlo d'ufficio sarebbe un bug difficile da vedere.
    /// </summary>
    internal static class AdPause
    {
        private static float previousTimeScale = 1f;
        private static bool previousAudioPause;
        private static bool paused;

        public static void Begin()
        {
            if (paused)
                return;

            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            paused = true;
        }

        public static void End()
        {
            if (!paused)
                return;

            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            paused = false;
        }
    }
}
