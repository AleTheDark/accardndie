using AccardND.Presentation.ReviewPrompt;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>
    /// Banco di prova del popup di recensione, senza far girare una run di campagna.
    ///
    /// Copre le tre cose che possono rompersi: l'aspetto del popup, il comportamento
    /// delle stelle (5 apre lo store, meno no) e la regola che decide se mostrarlo.
    /// Quest'ultima si vede nella tabella in basso, che valuta
    /// <see cref="ReviewPromptPolicy.ShouldPrompt"/> sui casi limite senza doverli
    /// riprodurre a mano.
    /// </summary>
    public sealed class ReviewPromptDebugScene : MonoBehaviour
    {
        private Text stateLabel;
        private Text matrixLabel;
        private Canvas canvas;

        private void Start()
        {
            EnsureEventSystem();

            canvas = DebugUi.CreateCanvas("Review Prompt Debug", transform);
            DebugUi.Background(canvas.transform);

            // BattleBoardController si auto-istanzia in qualunque scena, quindi anche qui
            // sotto compare l'hub del gioco. DebugUi disegna il banco a 5000 per stargli
            // sopra; il popup, che in gioco vive a 960, qui deve superare anche il banco.
            // In produzione SortingOrder resta il valore di default.
            ReviewPromptController.SortingOrder = 5100;

            Font font = DebugUi.Font;

            Text title = DebugUi.Text("Title", canvas.transform, font, 42, FontStyle.Bold);
            title.text = "POPUP RECENSIONE — BANCO DI PROVA";
            title.color = AccardND.Battlefield.MmoUiTheme.Gold;
            DebugUi.SetRect(title.rectTransform, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f));

            // In editor la piattaforma non e' Android e il popup non comparirebbe mai:
            // questa e' la stessa scorciatoia che usera' chi prova sul telefono, ma al
            // contrario.
            ReviewPromptController.SimulateAndroidForDebug = true;

            BuildButton("StarGate", "MOSTRA — MODO STELLE\n(5 stelle aprono lo store)",
                new Vector2(0.05f, 0.72f), new Vector2(0.48f, 0.86f),
                () => Show(ReviewPromptMode.StarGate));

            BuildButton("DirectAsk", "MOSTRA — MODO CONFORME\n(chiede e basta, niente filtro)",
                new Vector2(0.52f, 0.72f), new Vector2(0.95f, 0.86f),
                () => Show(ReviewPromptMode.DirectAsk));

            BuildButton("Reset", "AZZERA STATO SALVATO",
                new Vector2(0.05f, 0.62f), new Vector2(0.48f, 0.7f),
                () =>
                {
                    ReviewPromptState.Reset();
                    RefreshState();
                });

            BuildButton("Trigger", "PROVA IL VERO INNESCO\n(come a fine capitolo 1)",
                new Vector2(0.52f, 0.62f), new Vector2(0.95f, 0.7f),
                TryRealTrigger);

            stateLabel = DebugUi.Text("State", canvas.transform, font, 24, FontStyle.Normal);
            stateLabel.alignment = TextAnchor.UpperLeft;
            stateLabel.color = Color.white;
            DebugUi.SetRect(stateLabel.rectTransform, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.6f));

            matrixLabel = DebugUi.Text("Matrix", canvas.transform, font, 21, FontStyle.Normal);
            matrixLabel.alignment = TextAnchor.UpperLeft;
            matrixLabel.color = AccardND.Battlefield.MmoUiTheme.TextMuted;
            DebugUi.SetRect(matrixLabel.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.42f));

            RefreshState();
            RefreshMatrix();
        }

        private void Show(ReviewPromptMode mode)
        {
            if (ReviewPromptController.IsShowing)
                return;

            ReviewPromptController.Mode = mode;
            // Show() salta i controlli di proposito: qui si guarda il popup, non la regola.
            ReviewPromptController.Show(canvas.transform, RefreshState);
            RefreshState();
        }

        /// <summary>
        /// Passa da <see cref="ReviewPromptController.TryShow"/> con gli stessi argomenti
        /// che gli arrivano a fine campagna. Se lo stato salvato dice "gia' chiesto", qui
        /// non compare niente: e' il comportamento giusto, e il motivo finisce nel log.
        /// </summary>
        private void TryRealTrigger()
        {
            bool shown = ReviewPromptController.TryShow(
                canvas.transform,
                ReviewPromptPolicy.TriggerChapterId,
                runCompleted: true,
                onClosed: RefreshState);

            if (!shown)
                Debug.Log("[Recensione] innesco rifiutato: vedi la tabella delle regole in basso.");

            RefreshState();
        }

        private void RefreshState()
        {
            if (stateLabel == null)
                return;

            stateLabel.text =
                "<b>STATO SALVATO (PlayerPrefs)</b>\n"
                + $"gia' chiesto: {Yes(ReviewPromptState.AlreadyPrompted)}    "
                + $"gia' recensito: {Yes(ReviewPromptState.AlreadyRated)}    "
                + $"ultimo voto: {ReviewPromptState.LastStars}/5\n"
                + $"modo attuale: <b>{ReviewPromptController.Mode}</b>    "
                + $"android simulato: {Yes(ReviewPromptController.SimulateAndroidForDebug)}\n"
                + $"url store: {StoreReviewLauncher.WebUrl}";
        }

        private void RefreshMatrix()
        {
            if (matrixLabel == null)
                return;

            matrixLabel.text =
                "<b>REGOLA DI INNESCO</b>  (capitolo, vinta, android, gia' chiesto, gia' recensito) -> mostra?\n"
                + Row("chapter-1", true, true, false, false)
                + Row("chapter-1", false, true, false, false)
                + Row("chapter-2", true, true, false, false)
                + Row("free-run", true, true, false, false)
                + Row("chapter-1", true, false, false, false)
                + Row("chapter-1", true, true, true, false)
                + Row("chapter-1", true, true, false, true);
        }

        private static string Row(
            string chapter,
            bool completed,
            bool android,
            bool prompted,
            bool rated)
        {
            var request = new ReviewPromptPolicy.Request(chapter, completed, android, prompted, rated);
            bool show = ReviewPromptPolicy.ShouldPrompt(request);
            string verdict = show
                ? "<color=#6BE86B>MOSTRA</color>"
                : "<color=#E86B6B>no</color>";

            return $"{chapter,-10} vinta:{Short(completed)} android:{Short(android)} "
                + $"chiesto:{Short(prompted)} recensito:{Short(rated)}  ->  {verdict}\n";
        }

        private static string Short(bool value) => value ? "si" : "no";

        private static string Yes(bool value) =>
            value ? "<color=#6BE86B>si</color>" : "<color=#E86B6B>no</color>";

        private void BuildButton(
            string name,
            string label,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action)
        {
            Button button = DebugUi.Button(name, canvas.transform, DebugUi.Font, label);
            button.onClick.AddListener(action);
            DebugUi.SetRect((RectTransform)button.transform, min, max);
        }

        /// <summary>
        /// Il progetto usa l'Input System nuovo: un EventSystem con il modulo legacy non
        /// consegnerebbe nessun click ai bottoni del popup.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
