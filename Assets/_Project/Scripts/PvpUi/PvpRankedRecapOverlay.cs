using System;
using System.Collections;
using AccardND.Battlefield;
using AccardND.Localization;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>Recap PvP condiviso da fine partita e scena debug.</summary>
    internal sealed class PvpMatchResultOverlay
    {
        private readonly GameObject host;
        private readonly RectTransform vfxRoot;
        private readonly AudioSource music;

        public PvpMatchResultOverlay(Transform parent, MatchResultData result, Action onContinue,
            Action<MatchResultData, Action<bool>> onTripleExperience = null)
        {
            host = new GameObject("Ranked Match Recap", typeof(RectTransform), typeof(CanvasGroup), typeof(AudioSource), typeof(RankedRecapAnimator));
            host.transform.SetParent(parent, false);
            RectTransform screen = (RectTransform)host.transform;
            PvpUiFactory.Stretch(screen);

            Image veil = AddImage(screen, "Backdrop", new Color(0.003f, 0.006f, 0.011f, 0.94f));
            PvpUiFactory.Stretch((RectTransform)veil.transform);

            bool landscape = Screen.width >= Screen.height;
            RectTransform frame = PvpUiFactory.CreatePanel(screen, "MMO Ranked Recap Panel", Color.white);
            PvpUiFactory.SetAnchors(frame,
                landscape ? new Vector2(.24f, .08f) : new Vector2(.08f, .09f),
                landscape ? new Vector2(.76f, .92f) : new Vector2(.92f, .91f));

            Text title = PvpUiFactory.CreateText(frame, "Result Title",
                result.youWon ? GameText.Get(GameTextKeys.PvpResult.Victory) : GameText.Get(GameTextKeys.PvpResult.Defeat),
                70);
            title.font = MmoUiTheme.LoreFont;
            title.fontStyle = FontStyle.Normal;
            title.color = result.youWon ? new Color(1f, .82f, .34f) : new Color(.78f, .34f, .32f);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(.08f, .81f), new Vector2(.92f, .96f));

            Text score = PvpUiFactory.CreateTitleText(frame, "Final Score", $"{result.scoreYou}  —  {result.scoreOpponent}", 40);
            score.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)score.transform, new Vector2(.14f, .68f), new Vector2(.86f, .80f));

            RectTransform progress = PvpUiFactory.CreateContainer(frame, "League Progress");
            PvpUiFactory.SetAnchors(progress, new Vector2(.08f, .27f), new Vector2(.92f, .64f));
            BuildProgress(progress, result, out Text lpValue, out ArcaneExperienceFillGraphic lpFill, out Text rankChange);

            Text reason = PvpUiFactory.CreateLabel(frame, "Match Note", BuildNote(result), landscape ? 18 : 16, TextAnchor.MiddleCenter);
            PvpUiFactory.SetAnchors((RectTransform)reason.transform, new Vector2(.1f, .22f), new Vector2(.9f, .31f));

            Button continueButton = PvpUiFactory.CreateButton(frame, "Continue Campaign", GameText.Get(GameTextKeys.PvpResult.Continue), Color.green, () => onContinue?.Invoke(), landscape ? 25 : 21);
            RectTransform continueRect = (RectTransform)continueButton.transform;
            PvpUiFactory.SetAnchors(continueRect, new Vector2(.20f, .045f), new Vector2(.80f, .19f));
            Image continueImage = continueButton.GetComponent<Image>();
            Sprite greenCta = Resources.Load<Sprite>("UI/CampaignRestyle/campaign_cta_confirm_green");
            if (greenCta != null) { continueImage.sprite = greenCta; continueImage.type = Image.Type.Sliced; }
            Text continueLabel = continueButton.GetComponentInChildren<Text>();
            if (continueLabel != null) { continueLabel.font = MmoUiTheme.LoreFont; continueLabel.fontSize = 30; continueLabel.resizeTextForBestFit = false; }
            PvpUiVfx.CreateRankedButton(continueRect, new Color(.25f, 1f, .48f, 1f));

            vfxRoot = result.youWon ? LegacyPvpMatchResultOverlay.CreateVictoryConfetti(screen) : LegacyPvpMatchResultOverlay.CreateDefeatPetals(screen);
            // Coriandoli e petali devono attraversare visivamente anche il pannello;
            // il CanvasGroup del VFX non intercetta comunque i raycast della CTA.
            vfxRoot.SetAsLastSibling();

            music = host.GetComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = false;
            music.ignoreListenerPause = true;
            music.volume = .72f;
            music.clip = Resources.Load<AudioClip>(result.youWon ? "SFX/arena_3" : "SFX/game_over");
            if (music.clip != null) music.Play();

            host.GetComponent<RankedRecapAnimator>().Play(result, lpValue, lpFill, rankChange);
        }

        public void Destroy()
        {
            if (music != null) music.Stop();
            // Destroy e' differito a fine frame: in debug il recap successivo viene
            // creato immediatamente, quindi nascondiamo prima il vecchio per evitare
            // testi, stemmi e barre sovrapposti durante il cambio rank.
            if (host != null) host.SetActive(false);
            if (vfxRoot != null) vfxRoot.gameObject.SetActive(false);
            if (vfxRoot != null) UnityEngine.Object.Destroy(vfxRoot.gameObject);
            if (host != null) UnityEngine.Object.Destroy(host);
        }

        internal static RectTransform CreateVictoryConfetti(Transform parent) => LegacyPvpMatchResultOverlay.CreateVictoryConfetti(parent);
        internal static RectTransform CreateDefeatPetals(Transform parent) => LegacyPvpMatchResultOverlay.CreateDefeatPetals(parent);

        private static void BuildProgress(Transform parent, MatchResultData result, out Text lpValue, out ArcaneExperienceFillGraphic lpFill, out Text rankChange)
        {
            Text rank = PvpUiFactory.CreateTitleText(parent, "Current Rank", result.ranked && !result.placement ? $"{result.tier}  {result.division}" : result.placement ? "PLACEMENT" : "FRIENDLY", 46);
            rank.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)rank.transform, new Vector2(.08f, .76f), new Vector2(.92f, .96f));

            // L'aura contiene anche un sigillo procedurale: va creata prima dello
            // stemma, altrimenti copre l'artwork reale con la stella generica.
            PvpUiVfx.CreateRankAura(parent, new Vector2(.29f, .30f), new Vector2(.71f, .75f), RankCoreColor(result.tier));
            Image emblem = AddImage(parent, "Rank Emblem", Color.white);
            emblem.sprite = PvpUiFactory.RankEmblem(result.tier);
            emblem.preserveAspect = true;
            PvpUiFactory.SetAnchors((RectTransform)emblem.transform, new Vector2(.29f, .30f), new Vector2(.71f, .75f));

            RectTransform bar = PvpUiFactory.CreateSoftPanel(parent, "LP Experience Bar", new Color(.008f, .012f, .018f, .96f));
            PvpUiFactory.SetAnchors(bar, new Vector2(.12f, .13f), new Vector2(.88f, .27f));
            GameObject maskObject = new GameObject("LP Fill Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskObject.transform.SetParent(bar, false);
            RectTransform maskRect = (RectTransform)maskObject.transform;
            PvpUiFactory.Stretch(maskRect, 5f, 5f);
            Image maskImage = maskObject.GetComponent<Image>(); maskImage.color = Color.white; maskImage.raycastTarget = false;
            maskObject.GetComponent<Mask>().showMaskGraphic = false;
            GameObject fillObject = new GameObject("Arcane Rank Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(ArcaneExperienceFillGraphic));
            fillObject.transform.SetParent(maskObject.transform, false);
            lpFill = fillObject.GetComponent<ArcaneExperienceFillGraphic>();
            RectTransform fillRect = lpFill.rectTransform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(0f, 1f); fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            SetRankPalette(lpFill, result.tier);

            lpValue = PvpUiFactory.CreateTitleText(bar, "LP Value", result.ranked ? "0 / 100 LP" : "NO LP", 20);
            PvpUiFactory.Stretch((RectTransform)lpValue.transform, 5f, 2f);
            rankChange = PvpUiFactory.CreateText(parent, "Rank Change", "", 21);
            rankChange.color = result.lpDelta >= 0 ? PvpUiFactory.Good : PvpUiFactory.Bad;
            PvpUiFactory.SetAnchors((RectTransform)rankChange.transform, new Vector2(.08f, -.05f), new Vector2(.92f, .13f));
        }

        private static Color RankCoreColor(string tier)
        {
            string key = (tier ?? string.Empty).Trim().ToUpperInvariant();
            if (key.Contains("NABBO") || key.Contains("BRONZE")) return new Color(.48f, .25f, .09f);
            if (key.Contains("SILVER") || key.Contains("ARGENTO") || key.Contains("APPRENDISTA")) return new Color(.72f, .76f, .82f);
            if (key.Contains("PLATIN")) return new Color(.82f, .9f, .96f);
            if (key.Contains("GOLD") || key.Contains("ORO") || key.Contains("ESPERTO")) return new Color(1f, .67f, .08f);
            if (key.Contains("DIAM" ) || key.Contains("DIVINO")) return new Color(.2f, .86f, 1f);
            if (key.Contains("MASTER") || key.Contains("ONNIPOTENTE")) return new Color(.62f, .24f, .96f);
            return new Color(.96f, .98f, 1f);
        }

        private static void SetRankPalette(ArcaneExperienceFillGraphic fill, string tier)
        {
            Color core = RankCoreColor(tier);
            fill.SetPalette(Color.Lerp(core, Color.black, .78f), Color.Lerp(core, Color.black, .22f), Color.Lerp(core, Color.white, .35f));
        }

        private static string BuildNote(MatchResultData r)
        {
            if (!r.ranked) return "No league points are awarded in a friendly match.";
            if (r.placement) return $"Placement matches remaining: {r.placementRemaining}";
            if (r.endedReason == "timeout") return "The match ended by timeout.";
            if (r.endedReason == "disconnect") return "The opponent left the match.";
            return $"Account experience  +{r.accountExperienceEarned}";
        }

        private static Image AddImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }
    }

    internal sealed class RankedRecapAnimator : MonoBehaviour
    {
        public void Play(MatchResultData result, Text value, ArcaneExperienceFillGraphic fill, Text change) => StartCoroutine(Animate(result, value, fill, change));

        private IEnumerator Animate(MatchResultData r, Text value, ArcaneExperienceFillGraphic fill, Text change)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            float intro = 0f;
            while (intro < 1f) { intro += Time.unscaledDeltaTime * 2.8f; group.alpha = Mathf.SmoothStep(0f, 1f, intro); yield return null; }
            if (!r.ranked || r.placement) { fill.rectTransform.anchorMax = Vector2.up; value.text = r.placement ? GameText.Format(GameTextKeys.PvpResult.Placement, r.placementRemaining) : GameText.Get(GameTextKeys.PvpResult.Friendly); yield break; }

            int oldLp = Mathf.Clamp(r.leaguePoints - r.lpDelta, 0, 100);
            float elapsed = 0f;
            while (elapsed < 1.35f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.35f));
                int shown = Mathf.RoundToInt(Mathf.Lerp(oldLp, r.leaguePoints, t));
                fill.rectTransform.anchorMax = new Vector2(shown / 100f, 1f);
                value.text = $"{shown} / 100 LP";
                yield return null;
            }
            string sign = r.lpDelta >= 0 ? "+" : string.Empty;
            change.text = r.promoted ? GameText.Format(GameTextKeys.PvpResult.Promoted, r.tier, r.division, sign, r.lpDelta) : r.demoted ? GameText.Format(GameTextKeys.PvpResult.Demoted, r.tier, r.division, sign, r.lpDelta) : $"{sign}{r.lpDelta} LP";
        }
    }
}
