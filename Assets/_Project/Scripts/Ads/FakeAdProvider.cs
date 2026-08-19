using System;
using System.Collections;
using System.Threading.Tasks;
using AccardND.Battlefield;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccardND.Ads
{
    /// <summary>
    /// La pubblicita' finta: un pannello nero con un conto alla rovescia. Non guadagna niente
    /// e non e' un ripiego in attesa dell'SDK vero, e' lo strumento con cui si prova quello
    /// che conta davvero, cioe' dove la pubblicita' si infila nel gioco. Con questa si vede
    /// subito se l'interstitial dopo l'uso di un oggetto spezza il turno, senza aspettare
    /// un account AdMob e una build firmata.
    ///
    /// Dice sempre di essere finta a schermo: una pubblicita' di prova che sembra vera e'
    /// il tipo di cosa che finisce in produzione senza che nessuno se ne accorga.
    /// </summary>
    public sealed class FakeAdProvider : IAdProvider
    {
        private const float InterstitialSeconds = 2f;
        private const float RewardedSeconds = 2f;

        public string ProviderId => "fake";

        public Task<bool> InitializeAsync() => Task.FromResult(true);

        public bool IsReady(AdPlacement placement) => true;

        // La pubblicita' finta e' sempre pronta e non chiede niente a nessuno: preparare e
        // lasciar perdere non hanno niente da fare. Restano vuote apposta, cosi' provando in
        // editor si vede dove il gioco chiama Warm senza che una rete vera lo mascheri.
        public void Warm(AdPlacement placement)
        {
        }

        public void Cool(AdPlacement placement)
        {
        }

        public Task<AdResult> ShowAsync(AdPlacement placement, AdRewardContext context)
        {
            var completion = new TaskCompletionSource<AdResult>();
            AdRunner.Instance.Run(ShowRoutine(placement, completion));
            return completion.Task;
        }

        private static IEnumerator ShowRoutine(AdPlacement placement, TaskCompletionSource<AdResult> completion)
        {
            AdFormat format = AdPlacements.FormatOf(placement);
            float duration = format == AdFormat.Rewarded ? RewardedSeconds : InterstitialSeconds;

            WarnIfNoEventSystem();
            GameObject overlay = BuildOverlay(placement, format, out Text countdown, out Button close, out Button skip);

            var outcome = AdOutcome.Watched;
            bool skipped = false;
            if (skip != null)
                skip.onClick.AddListener(() => skipped = true);

            // Tempo reale: mentre l'annuncio e' a schermo la scala del tempo e' a zero, e un
            // conto alla rovescia legato a Time.time non scenderebbe mai.
            float remaining = duration;
            while (remaining > 0f && !skipped)
            {
                countdown.text = format == AdFormat.Rewarded
                    ? GameText.Format(GameTextKeys.Ads.FakeCountdownReward, Mathf.CeilToInt(remaining))
                    : GameText.Format(GameTextKeys.Ads.FakeCountdownClose, Mathf.CeilToInt(remaining));
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (skipped)
            {
                outcome = AdOutcome.Skipped;
            }
            else
            {
                countdown.text = format == AdFormat.Rewarded
                    ? GameText.Get(GameTextKeys.Ads.FakeRewardUnlocked)
                    : string.Empty;
                bool closed = false;
                close.onClick.AddListener(() => closed = true);
                close.interactable = true;
                close.GetComponentInChildren<Text>().text = GameText.Get(GameTextKeys.Common.Close);
                while (!closed)
                    yield return null;
            }

            // Il pannello sparisce prima di sbloccare l'attesa: chi riprende puo' aprire subito
            // un'altra schermata, e trovarsi la pubblicita' ancora addosso sarebbe un rebus.
            UnityEngine.Object.Destroy(overlay);
            completion.TrySetResult(new AdResult(
                outcome,
                outcome == AdOutcome.Watched ? "fake-" + Guid.NewGuid().ToString("N") : null));
        }

        private static GameObject BuildOverlay(
            AdPlacement placement, AdFormat format, out Text countdown, out Button close, out Button skip)
        {
            var root = new GameObject("Fake Ad", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Sopra ogni pannello del gioco: le schermate di riepilogo arrivano a 950, e una
            // pubblicita' che spunta sotto il popup che l'ha chiesta non si chiude piu'.
            canvas.sortingOrder = 32000;
            UnityEngine.Object.DontDestroyOnLoad(root);

            Font font = MmoUiTheme.LoreFont;

            Image veil = CreatePanel(root.transform, "Backdrop", new Color(0.005f, 0.008f, 0.015f, 0.92f));
            Stretch(veil.rectTransform, Vector2.zero, Vector2.one);

            Image background = CreatePanel(root.transform, "MMO UI Panel", MmoUiTheme.Panel);
            background.sprite = MmoUiTheme.GetPanelSprite();
            background.type = Image.Type.Sliced;
            Stretch(background.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            MmoUiTheme.AddPanelGem(background.rectTransform, "Panel Crystal", new Vector2(0.5f, 1f),
                new Vector2(38f, 38f), new Color(0.82f, 0.96f, 1f, 0.86f));

            Text banner = CreateText(background.transform, "Banner", font, 26, TextAnchor.MiddleCenter);
            banner.text = GameText.Get(GameTextKeys.Ads.FakeBanner);
            banner.color = MmoUiTheme.Bad;
            Stretch(banner.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.95f));

            Text title = CreateText(background.transform, "Title", font, 44, TextAnchor.MiddleCenter);
            title.text = format == AdFormat.Rewarded
                ? GameText.Get(GameTextKeys.Ads.FakeRewardedTitle)
                : GameText.Get(GameTextKeys.Ads.FakeInterstitialTitle);
            title.color = MmoUiTheme.Gold;
            Stretch(title.rectTransform, new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.74f));

            Text subtitle = CreateText(background.transform, "Subtitle", font, 26, TextAnchor.MiddleCenter);
            subtitle.text = AdPlacements.DescribeFor(placement) + "\n" + AdPlacements.KeyOf(placement);
            subtitle.color = MmoUiTheme.TextMuted;
            Stretch(subtitle.rectTransform, new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.6f));

            countdown = CreateText(background.transform, "Countdown", font, 32, TextAnchor.MiddleCenter);
            countdown.color = Color.white;
            Stretch(countdown.rectTransform, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.44f));

            close = CreateButton(background.transform, "Close", font, "ATTENDI");
            close.interactable = false;
            Stretch((RectTransform)close.transform, new Vector2(0.32f, 0.14f), new Vector2(0.68f, 0.26f));

            // Il tasto per chiudere a meta' esiste solo sui rewarded, ed e' li' apposta: e' il
            // caso che il gameplay sbaglia piu' spesso, perche' e' l'unico in cui il giocatore
            // ha detto di no e la ricompensa non va data.
            skip = null;
            if (format == AdFormat.Rewarded)
            {
                skip = CreateButton(background.transform, "Skip", font, "CHIUDI SENZA PREMIO");
                Stretch((RectTransform)skip.transform, new Vector2(0.62f, 0.9f), new Vector2(0.98f, 0.98f));
            }

            return root;
        }

        /// <summary>
        /// Senza EventSystem i bottoni non ricevono i clic e il pannello non si chiude piu'.
        /// In partita ce n'e' sempre uno; qui si segnala e basta, perche' costruirne uno
        /// significherebbe scegliere il modulo di input al posto della scena (il progetto usa
        /// il nuovo Input System) e si rischia di rompere i comandi di tutto il gioco.
        /// </summary>
        private static void WarnIfNoEventSystem()
        {
            if (EventSystem.current == null)
                Debug.LogWarning("[Ads] Nessun EventSystem in scena: il pannello di prova non sara' cliccabile.");
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Image));
            host.transform.SetParent(parent, false);
            Image image = host.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Text));
            host.transform.SetParent(parent, false);
            Text text = host.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Font font, string label)
        {
            Image background = CreatePanel(parent, name, new Color(0.16f, 0.2f, 0.28f, 1f));
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            background.sprite = MmoUiTheme.GetButtonSprite(MmoUiTheme.ButtonVariant.Gold);
            background.type = Image.Type.Sliced;
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);

            Text text = CreateText(background.transform, "Label", font, 24, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = Color.white;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
