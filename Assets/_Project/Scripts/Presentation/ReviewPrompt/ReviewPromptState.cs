using UnityEngine;

namespace AccardND.Presentation.ReviewPrompt
{
    /// <summary>
    /// Memoria del popup di recensione. Stessa convenzione degli hint "prima volta"
    /// (<c>AccardHint_*</c> in BattleBoardController.Hints.cs): PlayerPrefs, chiavi con
    /// prefisso, nessuna dipendenza dal server.
    ///
    /// Restare locale e' voluto: se il giocatore reinstalla, ricomincia comunque il
    /// capitolo 1 e una seconda domanda dopo mesi non e' un abuso. Legarlo all'account
    /// vorrebbe dire una chiamata di rete in piu' nel momento piu' delicato della run.
    /// </summary>
    public static class ReviewPromptState
    {
        private const string PromptedKey = "AccardReviewPrompt_Shown";
        private const string RatedKey = "AccardReviewPrompt_Rated";
        private const string LastStarsKey = "AccardReviewPrompt_LastStars";

        /// <summary>Il popup e' gia' comparso almeno una volta.</summary>
        public static bool AlreadyPrompted
        {
            get => PlayerPrefs.GetInt(PromptedKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(PromptedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Il giocatore e' gia' stato mandato allo store.</summary>
        public static bool AlreadyRated
        {
            get => PlayerPrefs.GetInt(RatedKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(RatedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// L'ultimo voto scelto. Non serve al gioco: serve a te, per sapere dal log di
        /// una sessione di prova cosa aveva votato chi non e' arrivato allo store.
        /// </summary>
        public static int LastStars
        {
            get => PlayerPrefs.GetInt(LastStarsKey, 0);
            set
            {
                PlayerPrefs.SetInt(LastStarsKey, Mathf.Clamp(value, 0, ReviewPromptPolicy.MaxStars));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Rimette tutto a zero: usato dalla scena di debug.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(PromptedKey);
            PlayerPrefs.DeleteKey(RatedKey);
            PlayerPrefs.DeleteKey(LastStarsKey);
            PlayerPrefs.Save();
        }
    }
}
