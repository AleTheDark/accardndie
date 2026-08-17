using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    public sealed class RankedRecapDebugScene : MonoBehaviour
    {
        private const int Step = 15;
        private readonly string[] tiers = { "NABBO", "APPRENDISTA", "GOLD", "PLATINO", "ESPERTO", "DIVINO", "ONNIPOTENTE" };
        private int tierIndex = 2;
        private int division = 3;
        private int lp = 65;
        private bool victory = true;
        private PvpMatchResultOverlay recap;
        private Transform canvasRoot;
        private Text selectedRankText;
        private RectTransform debugPanel;

        private void Start()
        {
            EnsureEventSystem();
            EnsureAudioListener();
            Canvas canvas = new GameObject("Ranked Recap Debug Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            canvasRoot = canvas.transform;
            Show(0);
            BuildDebugControls();
        }

        private void Show(int delta)
        {
            int oldTier = tierIndex, oldDivision = division, oldLp = lp;
            ApplyDelta(delta);
            if (selectedRankText != null)
                selectedRankText.text = $"SELECTED: {tiers[tierIndex]} {Roman(division)} — {lp} LP";
            recap?.Destroy();
            recap = new PvpMatchResultOverlay(canvasRoot, new MatchResultData
            {
                youWon = victory, ranked = true, endedReason = "normal", scoreYou = victory ? 3 : 1, scoreOpponent = victory ? 1 : 3,
                tier = tiers[tierIndex], division = Roman(division), leaguePoints = lp, lpDelta = delta,
                promoted = delta > 0 && (oldTier != tierIndex || oldDivision != division),
                demoted = delta < 0 && (oldTier != tierIndex || oldDivision != division), accountExperienceEarned = victory ? 15 : 5
            }, () => { });
            // Il recap viene ricreato a ogni prova e finirebbe sopra ai controlli:
            // il pannello debug deve restare sempre accessibile in sovraimpressione.
            if (debugPanel != null)
                debugPanel.SetAsLastSibling();
        }

        private void ApplyDelta(int delta)
        {
            lp += delta;
            while (lp >= 100 && tierIndex < tiers.Length - 1) { lp -= 100; if (--division < 1) { division = 4; tierIndex++; } }
            while (lp < 0 && (tierIndex > 0 || division < 4)) { lp += 100; if (++division > 4) { division = 1; tierIndex--; } }
            lp = Mathf.Clamp(lp, 0, 100);
        }

        private void BuildDebugControls()
        {
            RectTransform panel = PvpUiFactory.CreateSoftPanel(canvasRoot, "Debug LP Controls", new Color(0, 0, 0, .88f));
            debugPanel = panel;
            PvpUiFactory.SetAnchors(panel, new Vector2(.01f, .02f), new Vector2(.25f, .39f));
            Text legend = PvpUiFactory.CreateLabel(panel, "Legend", "DEBUG LP & RANK\n100 LP = next division\nI follows IV · next tier after I", 16, TextAnchor.UpperCenter);
            PvpUiFactory.SetAnchors((RectTransform)legend.transform, new Vector2(.04f, .68f), new Vector2(.96f, .96f));
            selectedRankText = PvpUiFactory.CreateTitleText(panel, "Selected Rank", $"SELECTED: {tiers[tierIndex]} {Roman(division)} — {lp} LP", 15);
            PvpUiFactory.SetAnchors((RectTransform)selectedRankText.transform, new Vector2(.04f, .56f), new Vector2(.96f, .69f));
            Button previousRank = PvpUiFactory.CreateButton(panel, "Previous Rank", "< RANK", Color.gray, () => ChangeRank(-1), 15);
            Button nextRank = PvpUiFactory.CreateButton(panel, "Next Rank", "RANK >", Color.yellow, () => ChangeRank(1), 15);
            Button minus = PvpUiFactory.CreateButton(panel, "Subtract LP", "−15 LP", Color.red, () => Show(-Step), 17);
            Button plus = PvpUiFactory.CreateButton(panel, "Add LP", "+15 LP", Color.green, () => Show(Step), 17);
            Button outcome = PvpUiFactory.CreateButton(panel, "Toggle Result", "WIN / LOSE", Color.yellow, () => { victory = !victory; Show(0); }, 15);
            PvpUiFactory.SetAnchors((RectTransform)previousRank.transform, new Vector2(.04f, .37f), new Vector2(.48f, .54f));
            PvpUiFactory.SetAnchors((RectTransform)nextRank.transform, new Vector2(.52f, .37f), new Vector2(.96f, .54f));
            PvpUiFactory.SetAnchors((RectTransform)minus.transform, new Vector2(.04f, .08f), new Vector2(.34f, .32f));
            PvpUiFactory.SetAnchors((RectTransform)plus.transform, new Vector2(.36f, .08f), new Vector2(.66f, .32f));
            PvpUiFactory.SetAnchors((RectTransform)outcome.transform, new Vector2(.68f, .08f), new Vector2(.96f, .32f));
            panel.SetAsLastSibling();
        }

        private void ChangeRank(int direction)
        {
            tierIndex = Mathf.Clamp(tierIndex + direction, 0, tiers.Length - 1);
            Show(0);
        }

        private static string Roman(int value) => value == 1 ? "I" : value == 2 ? "II" : value == 3 ? "III" : "IV";
        private static void EnsureEventSystem() { if (FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); }
        private static void EnsureAudioListener()
        {
            if (FindFirstObjectByType<AudioListener>() != null) return;
            Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (camera != null) camera.gameObject.AddComponent<AudioListener>();
        }
        private void OnDestroy() => recap?.Destroy();
    }
}
