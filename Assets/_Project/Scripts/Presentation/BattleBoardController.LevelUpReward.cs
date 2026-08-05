using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private const int HoneyPerAccountLevel = 5;
    private GameObject levelUpRewardPopup;
    private Text levelUpRewardBodyText;
    private Button levelUpRewardOkButton;
    private bool levelUpRewardClaimInProgress;

    private void CreateLevelUpRewardPopup(Transform parent, Font font)
    {
        Image overlay = CreateImage("Level Up Reward Popup", parent, new Color(0f, 0f, 0f, 0.78f));
        overlay.raycastTarget = true;
        Stretch(overlay.rectTransform);
        levelUpRewardPopup = overlay.gameObject;
        Canvas canvas = levelUpRewardPopup.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 980;
        levelUpRewardPopup.AddComponent<GraphicRaycaster>();

        Image dialog = CreateImage("Level Up Reward Dialog", overlay.transform,
            new Color(0.01f, 0.018f, 0.028f, 0.99f));
        dialog.raycastTarget = true;
        StylePanel(dialog);
        SetRect(dialog.rectTransform, new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.7f));

        Text title = CreateText("Level Up Reward Title", dialog.transform, font, 32,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
        title.text = "NUOVO LIVELLO!";
        title.color = new Color(0.95f, 0.79f, 0.34f);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.92f));

        levelUpRewardBodyText = CreateText("Level Up Reward Body", dialog.transform, font, 26,
            FontStyle.Normal, TextAnchor.MiddleCenter);
        levelUpRewardBodyText.color = new Color(0.88f, 0.94f, 0.97f);
        levelUpRewardBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetRect(levelUpRewardBodyText.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.7f));

        levelUpRewardOkButton = CreateButton("Claim Level Up Reward", dialog.transform, font, "OK");
        levelUpRewardOkButton.onClick.AddListener(new UnityAction(ClaimLevelUpReward));
        SetRect((RectTransform)levelUpRewardOkButton.transform,
            new Vector2(0.32f, 0.09f), new Vector2(0.68f, 0.28f));
        levelUpRewardPopup.SetActive(false);
    }

    private void TryShowLevelUpRewardPopup()
    {
        if (levelUpRewardClaimInProgress || levelUpRewardPopup == null || !IsAccountHubVisible())
            return;

        SinglePlayerProgressSave progress = singlePlayerProgressService?.Progress;
        int levels = Mathf.Max(0, progress?.pendingLevelRewards ?? 0);
        if (levels <= 0)
            return;

        int honey = levels * HoneyPerAccountLevel;
        levelUpRewardBodyText.text = levels == 1
            ? $"Hai raggiunto il livello {Mathf.Max(1, progress.accountLevel)}!\n\nRicompensa: <b>+{honey} vasetti di miele</b>"
            : $"Hai guadagnato {levels} livelli!\n\nRicompensa: <b>+{honey} vasetti di miele</b>";
        levelUpRewardOkButton.interactable = true;
        levelUpRewardPopup.SetActive(true);
        levelUpRewardPopup.transform.SetAsLastSibling();
    }

    private async void ClaimLevelUpReward()
    {
        if (levelUpRewardClaimInProgress || serverProgress == null)
            return;

        levelUpRewardClaimInProgress = true;
        levelUpRewardOkButton.interactable = false;
        try
        {
            await serverProgress.ClaimLevelRewardsAsync();
            MirrorServerProgress();
            RefreshSinglePlayerProgressView();
            RefreshAccountBannerView();
            levelUpRewardPopup.SetActive(false);
        }
        catch (System.Exception exception)
        {
            SetMessage($"Ricompensa level-up non riscossa: {exception.Message}");
            levelUpRewardOkButton.interactable = true;
        }
        finally
        {
            levelUpRewardClaimInProgress = false;
        }
    }
}
}
