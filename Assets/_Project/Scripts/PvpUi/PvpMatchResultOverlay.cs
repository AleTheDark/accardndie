using System;
using System.Text;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>Riepilogo di fine partita: esito, variazione rank e traguardi sbloccati.</summary>
    internal sealed class PvpMatchResultOverlay
    {
        private readonly RectTransform root;

        public PvpMatchResultOverlay(Transform parent, MatchResultData result, Action onContinue)
        {
            root = PvpUiFactory.CreatePanel(parent, "MatchResult", new Color(0.018f, 0.026f, 0.04f, 0.995f));
            PvpUiFactory.SetAnchors(root, new Vector2(0.25f, 0.16f), new Vector2(0.75f, 0.84f));

            string headline = result.youWon ? "VITTORIA" : "SCONFITTA";
            Color headColor = result.youWon ? PvpUiFactory.Good : PvpUiFactory.Bad;
            RectTransform titleBand = PvpUiFactory.CreateTitleBand(root, "RISULTATO ARENA", result.youWon ? "La tua leggenda avanza" : "Riorganizza il loadout e torna in arena");
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.06f, 0.81f), new Vector2(0.94f, 0.96f));
            Text head = PvpUiFactory.CreateText(root, "Head", headline, 52);
            head.color = headColor;
            PvpUiFactory.SetAnchors((RectTransform)head.transform, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f));

            Text score = PvpUiFactory.CreateText(
                root, "Score", $"{result.scoreYou} - {result.scoreOpponent}", 34);
            score.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)score.transform, new Vector2(0.05f, 0.69f), new Vector2(0.95f, 0.8f));

            RectTransform detailPanel = PvpUiFactory.CreateSoftPanel(root, "Detail Panel", new Color(0.035f, 0.055f, 0.08f, 0.94f));
            PvpUiFactory.SetAnchors(detailPanel, new Vector2(0.07f, 0.19f), new Vector2(0.93f, 0.67f));

            Text detail = PvpUiFactory.CreateText(
                detailPanel, "Detail", BuildDetail(result), 22, TextAnchor.UpperCenter, FontStyle.Normal);
            detail.color = Color.white;
            PvpUiFactory.Stretch((RectTransform)detail.transform, 16f, 12f);

            Button continueButton = PvpUiFactory.CreateButton(
                root, "Continue", "CONTINUA", new Color(0.1f, 0.5f, 0.3f, 0.98f), () => onContinue?.Invoke(), 24);
            PvpUiFactory.SetAnchors((RectTransform)continueButton.transform, new Vector2(0.3f, 0.055f), new Vector2(0.7f, 0.16f));
        }

        public void Destroy() => UnityEngine.Object.Destroy(root.gameObject);

        private static string BuildDetail(MatchResultData result)
        {
            var builder = new StringBuilder();
            if (result.endedReason == "timeout")
                builder.AppendLine("Vittoria/sconfitta per tempo scaduto.");
            else if (result.endedReason == "disconnect")
                builder.AppendLine("L'avversario ha abbandonato.");

            if (result.ranked)
            {
                if (result.placement)
                {
                    builder.AppendLine($"Piazzamento: {result.placementRemaining} partite rimaste.");
                }
                else
                {
                    string sign = result.lpDelta >= 0 ? "+" : string.Empty;
                    builder.AppendLine($"{result.tier} {result.division} — {result.leaguePoints} LP  ({sign}{result.lpDelta} LP)");
                    if (result.promoted)
                        builder.AppendLine("PROMOSSO!");
                    else if (result.demoted)
                        builder.AppendLine("Retrocesso.");
                }
            }
            else
            {
                builder.AppendLine("Partita amichevole (nessuna variazione di rank).");
            }

            if (result.unlockedAchievements != null && result.unlockedAchievements.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("TRAGUARDI SBLOCCATI:");
                foreach (string achievement in result.unlockedAchievements)
                    builder.AppendLine($"* {achievement}");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
