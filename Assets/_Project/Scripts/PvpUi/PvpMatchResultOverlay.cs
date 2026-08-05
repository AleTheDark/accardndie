using System;
using System.Collections;
using System.Text;
using AccardND.Localization;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>Riepilogo di fine partita: esito, variazione rank e traguardi sbloccati.</summary>
    internal sealed class PvpMatchResultOverlay
    {
        private readonly RectTransform root;
        private readonly RectTransform confettiRoot;

        private Button tripleButton;

		public PvpMatchResultOverlay(
			Transform parent, MatchResultData result, Action onContinue,
			Action<MatchResultData, Action<bool>> onTripleExperience = null)
        {
            root = PvpUiFactory.CreatePanel(parent, "MatchResult", new Color(0.018f, 0.026f, 0.04f, 0.995f));
            bool landscape = Screen.width >= Screen.height;
            PvpUiFactory.SetAnchors(
                root,
                landscape ? new Vector2(0.25f, 0.16f) : new Vector2(0.18f, 0.23f),
                landscape ? new Vector2(0.75f, 0.84f) : new Vector2(0.82f, 0.77f));
            if (result.youWon)
                confettiRoot = CreateVictoryConfetti(parent);

            string headline = result.youWon
                ? GameText.Get(GameTextKeys.PvpResult.Victory)
                : GameText.Get(GameTextKeys.PvpResult.Defeat);
            Color headColor = result.youWon ? PvpUiFactory.Good : PvpUiFactory.Bad;
            RectTransform titleBand = PvpUiFactory.CreateTitleBand(
                root,
                GameText.Get(GameTextKeys.PvpResult.Title),
                result.youWon
                    ? GameText.Get(GameTextKeys.PvpResult.VictorySubtitle)
                    : GameText.Get(GameTextKeys.PvpResult.DefeatSubtitle));
            PvpUiFactory.SetAnchors(
                titleBand,
                landscape ? new Vector2(0.06f, 0.82f) : new Vector2(0.06f, 0.78f),
                landscape ? new Vector2(0.94f, 0.96f) : new Vector2(0.94f, 0.95f));
            Text head = PvpUiFactory.CreateText(root, "Head", headline, landscape ? 68 : 54);
            head.color = headColor;
            PvpUiFactory.SetAnchors(
                (RectTransform)head.transform,
                landscape ? new Vector2(0.05f, 0.68f) : new Vector2(0.05f, 0.66f),
                landscape ? new Vector2(0.95f, 0.84f) : new Vector2(0.95f, 0.79f));

            Text score = PvpUiFactory.CreateText(
                root, "Score", $"{result.scoreYou} - {result.scoreOpponent}", landscape ? 38 : 34);
            score.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)score.transform,
                landscape ? new Vector2(0.05f, 0.55f) : new Vector2(0.05f, 0.52f),
                landscape ? new Vector2(0.95f, 0.66f) : new Vector2(0.95f, 0.62f));

            RectTransform detailPanel = PvpUiFactory.CreateSoftPanel(root, "Detail Panel", new Color(0.035f, 0.055f, 0.08f, 0.94f));
            PvpUiFactory.SetAnchors(
                detailPanel,
                landscape ? new Vector2(0.07f, 0.20f) : new Vector2(0.07f, 0.18f),
                landscape ? new Vector2(0.93f, 0.52f) : new Vector2(0.93f, 0.49f));

            Text detail = PvpUiFactory.CreateText(
                detailPanel, "Detail", BuildDetail(result), landscape ? 23 : 21, TextAnchor.UpperCenter, FontStyle.Normal);
            detail.color = Color.white;
            PvpUiFactory.Stretch((RectTransform)detail.transform, 16f, 12f);

            Button continueButton = PvpUiFactory.CreateButton(
                root,
                "Continue",
                GameText.Get(GameTextKeys.PvpResult.Continue),
                new Color(0.1f, 0.5f, 0.3f, 0.98f),
                () => onContinue?.Invoke(),
                landscape ? 25 : 22);
            PvpUiFactory.SetAnchors(
                (RectTransform)continueButton.transform,
                landscape ? new Vector2(0.30f, 0.055f) : new Vector2(0.27f, 0.055f),
                landscape ? new Vector2(0.70f, 0.16f) : new Vector2(0.73f, 0.145f));

			// L'ultima condizione tiene fuori il bottone quando non c'e' nessun annuncio
			// pronto: prometterlo e poi non averlo e' il modo piu' veloce per far sembrare
			// rotto un riepilogo di fine partita.
			if (result.accountExperienceCanTriple
				&& result.accountExperienceEarned > 0
				&& !string.IsNullOrWhiteSpace(result.accountExperienceRewardClaimId)
				&& onTripleExperience != null
				&& AccardND.Ads.AdService.IsReady(AccardND.Ads.AdPlacement.PvpExperienceTriple))
			{
				tripleButton = PvpUiFactory.CreateButton(
					root,
					"Triple Account EXP",
					GameText.GetOrFallbackSilent(
						GameTextKeys.PvpResult.TripleExperienceOffer,
						"ADV  ×3 EXP ({0})",
						result.accountExperienceEarned * 3),
					new Color(0.42f, 0.18f, 0.62f, 0.98f),
					() =>
					{
						tripleButton.interactable = false;
						onTripleExperience(result, success =>
						{
							if (tripleButton == null) return;
							tripleButton.GetComponentInChildren<Text>().text = success
								? GameText.GetOrFallbackSilent(
									GameTextKeys.PvpResult.TripleExperienceApplied,
									"EXP TRIPLICATA: +{0}",
									result.accountExperienceEarned * 3)
								: GameText.GetOrFallbackSilent(
									GameTextKeys.PvpResult.TripleExperienceUnavailable,
									"ADV NON DISPONIBILE");
							tripleButton.interactable = !success;
						});
					}, landscape ? 22 : 19);
				PvpUiFactory.SetAnchors((RectTransform)tripleButton.transform,
					new Vector2(0.18f, 0.055f), new Vector2(0.54f, 0.16f));
				PvpUiFactory.SetAnchors((RectTransform)continueButton.transform,
					new Vector2(0.58f, 0.055f), new Vector2(0.88f, 0.16f));
			}
        }

        public void Destroy()
        {
            if (confettiRoot != null)
                UnityEngine.Object.Destroy(confettiRoot.gameObject);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        private static RectTransform CreateVictoryConfetti(Transform parent)
        {
            var holder = new GameObject("Victory Confetti", typeof(RectTransform), typeof(CanvasGroup), typeof(VictoryConfettiRain));
            holder.transform.SetParent(parent, false);
            var rect = (RectTransform)holder.transform;
            PvpUiFactory.Stretch(rect);
            rect.SetAsLastSibling();

            CanvasGroup canvasGroup = holder.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            holder.GetComponent<VictoryConfettiRain>().Play();
            return rect;
        }

        private static string BuildDetail(MatchResultData result)
        {
            var builder = new StringBuilder();
            if (result.endedReason == "timeout")
                builder.AppendLine(GameText.Get(GameTextKeys.PvpResult.Timeout));
            else if (result.endedReason == "disconnect")
                builder.AppendLine(GameText.Get(GameTextKeys.PvpResult.OpponentForfeit));
            else if (result.endedReason == "surrender")
            {
                builder.AppendLine(result.youWon
                    ? GameText.GetLocalizedFallback(
                        GameTextKeys.PvpResult.OpponentSurrendered,
                        "HAI VINTO: IL TUO AVVERSARIO SI È ARRESO",
                        "YOU WON: YOUR OPPONENT SURRENDERED")
                    : GameText.GetLocalizedFallback(
                        GameTextKeys.PvpResult.Surrendered,
                        "Ti sei arreso.",
                        "You surrendered."));
            }

            if (result.ranked)
            {
                if (result.placement)
                {
                    builder.AppendLine(GameText.Format(GameTextKeys.PvpResult.Placement, result.placementRemaining));
                }
                else
                {
                    string sign = result.lpDelta >= 0 ? "+" : string.Empty;
                    builder.AppendLine(GameText.Format(
                        GameTextKeys.PvpResult.League,
                        result.tier,
                        result.division,
                        result.leaguePoints,
                        sign,
                        result.lpDelta));
                    if (result.promoted)
                        builder.AppendLine(GameText.Get(GameTextKeys.PvpResult.Promoted));
                    else if (result.demoted)
                        builder.AppendLine(GameText.Get(GameTextKeys.PvpResult.Demoted));
                }
            }
            if (result.ranked)
            {
                builder.AppendLine($"EXP ACCOUNT: +{result.accountExperienceEarned} ({result.scoreYou} round vinti × 5)");
            }
            else
            {
                builder.AppendLine(GameText.Get(GameTextKeys.PvpResult.Friendly));
            }

            if (result.unlockedAchievements != null && result.unlockedAchievements.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine(GameText.Get(GameTextKeys.PvpResult.Achievements));
                foreach (string achievement in result.unlockedAchievements)
                    builder.AppendLine(GameText.Format(GameTextKeys.PvpResult.AchievementItem, achievement));
            }

            return builder.ToString().TrimEnd();
        }
    }

    internal sealed class VictoryConfettiRain : MonoBehaviour
    {
        private static readonly Color[] Colors =
        {
            new Color(1f, 0.86f, 0.22f, 0.95f),
            new Color(0.25f, 0.95f, 0.55f, 0.95f),
            new Color(0.25f, 0.7f, 1f, 0.95f),
            new Color(1f, 0.28f, 0.36f, 0.95f),
            new Color(0.86f, 0.55f, 1f, 0.95f),
            new Color(1f, 1f, 1f, 0.92f)
        };

        private RectTransform rect;

        public void Play()
        {
            rect = (RectTransform)transform;
            StartCoroutine(Rain());
        }

        private IEnumerator Rain()
        {
            for (int i = 0; i < 96; i++)
            {
                CreatePiece(i * 0.018f);
                if (i % 8 == 0)
                    yield return null;
            }

            while (isActiveAndEnabled)
            {
                CreatePiece(0f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.012f, 0.035f));
            }
        }

        private void CreatePiece(float delay)
        {
            var piece = new GameObject("Confetto", typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(transform, false);

            var pieceRect = (RectTransform)piece.transform;
            pieceRect.anchorMin = new Vector2(0.5f, 1f);
            pieceRect.anchorMax = new Vector2(0.5f, 1f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.sizeDelta = new Vector2(UnityEngine.Random.Range(7f, 14f), UnityEngine.Random.Range(14f, 28f));

            Image image = piece.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Colors[UnityEngine.Random.Range(0, Colors.Length)];

            StartCoroutine(Fall(pieceRect, image, delay));
        }

        private IEnumerator Fall(RectTransform piece, Image image, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (piece == null || image == null)
                yield break;

            Rect bounds = rect.rect;
            float startX = UnityEngine.Random.Range(bounds.xMin - 60f, bounds.xMax + 60f);
            float endX = startX + UnityEngine.Random.Range(-140f, 140f);
            float startY = bounds.yMax + UnityEngine.Random.Range(12f, 140f);
            float endY = bounds.yMin - UnityEngine.Random.Range(80f, 180f);
            float duration = UnityEngine.Random.Range(2.2f, 4.2f);
            float spin = UnityEngine.Random.Range(-720f, 720f);
            float sway = UnityEngine.Random.Range(18f, 68f);
            float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float elapsed = 0f;

            while (elapsed < duration && piece != null && image != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 1.6f);
                float x = Mathf.Lerp(startX, endX, eased) + Mathf.Sin((t * Mathf.PI * 5.5f) + phase) * sway;
                float y = Mathf.Lerp(startY, endY, eased);
                piece.anchoredPosition = new Vector2(x, y);
                piece.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                piece.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.55f, Mathf.PingPong(t * 5f, 1f)), 1f);

                Color color = image.color;
                color.a = Mathf.Clamp01(Mathf.Min(t * 10f, (1f - t) * 8f)) * 0.95f;
                image.color = color;
                yield return null;
            }

            if (piece != null)
                Destroy(piece.gameObject);
        }
    }
}
