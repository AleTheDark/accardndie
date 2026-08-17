using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private GameObject levelUpRewardPopup;
    private Text levelUpRewardBodyText;
    private Text levelUpRewardPropolisText;
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

        Text title = CreateText("Level Up Reward Title", dialog.transform, font, 40,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
        Font levelUpTitleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
        if (levelUpTitleFont != null)
            title.font = levelUpTitleFont;
        title.fontSize = 40;
        title.fontStyle = FontStyle.Bold;
        title.resizeTextForBestFit = false;
		title.text = GameText.GetOrFallbackSilent(GameTextKeys.Campaign.LevelUpTitle, "NUOVO LIVELLO!");
        title.color = new Color(0.95f, 0.79f, 0.34f);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.94f));

        levelUpRewardBodyText = CreateText("Level Up Reward Body", dialog.transform, font, 26,
            FontStyle.Normal, TextAnchor.MiddleCenter);
        levelUpRewardBodyText.color = new Color(0.88f, 0.94f, 0.97f);
        levelUpRewardBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetRect(levelUpRewardBodyText.rectTransform, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.74f));

        CreateLevelUpRewardPropolisRow(dialog.transform, font);

        levelUpRewardOkButton = CreateButton("Claim Level Up Reward", dialog.transform, font, "OK");
        levelUpRewardOkButton.onClick.AddListener(new UnityAction(ClaimLevelUpReward));
        AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(levelUpRewardOkButton);
        SetRect((RectTransform)levelUpRewardOkButton.transform,
            new Vector2(0.24f, 0.04f), new Vector2(0.76f, 0.27f));
        levelUpRewardPopup.SetActive(false);
    }

    /// <summary>
    /// La ricompensa in figura: il propoli con il suo moltiplicatore. Il testo dice quanti
    /// punti arrivano, ma e' l'icona quella che il giocatore riconosce quando poi la
    /// ritrova nell'albero dei talenti.
    /// </summary>
    private void CreateLevelUpRewardPropolisRow(Transform parent, Font font)
    {
        Image row = CreateImage("Level Up Reward Propolis Row", parent, Color.clear);
        SetRect(row.rectTransform, new Vector2(0.16f, 0.28f), new Vector2(0.84f, 0.48f));

        Image propolis = CreateImage("Level Up Reward Propolis Icon", row.transform, Color.white);
        propolis.sprite = LoadSpriteResource("UI/ProfileTalents/propolis_currency");
        propolis.preserveAspect = true;
        SetRect(propolis.rectTransform, new Vector2(0.2f, 0f), new Vector2(0.48f, 1f));

        levelUpRewardPropolisText = CreateText("Level Up Reward Propolis Count", row.transform, font, 30,
            FontStyle.Bold, TextAnchor.MiddleLeft);
        levelUpRewardPropolisText.color = new Color(0.95f, 0.79f, 0.34f);
        SetRect(levelUpRewardPropolisText.rectTransform, new Vector2(0.52f, 0.12f), new Vector2(0.82f, 0.88f));
    }

    private void TryShowLevelUpRewardPopup()
    {
        if (levelUpRewardClaimInProgress || levelUpRewardPopup == null || !IsAccountHubVisible())
            return;

        SinglePlayerProgressSave progress = singlePlayerProgressService?.Progress;
        int levels = Mathf.Max(0, progress?.pendingLevelRewards ?? 0);
        if (levels <= 0)
            return;

        // I punti li accredita il server, ma la regola e' una sola e sta in GameCore: i
        // livelli non riscossi sono per forza gli ultimi raggiunti, quindi la stessa forma
        // chiusa che usa la riscossione dice gia' qui quanti punti arrivano, bonus dei
        // livelli tondi compreso. Prima si annunciava il minimo garantito, e chi passava un
        // livello tondo si vedeva promettere 1 e accreditare 3.
        int level = Mathf.Max(1, progress.accountLevel);
        int points = AccountLevelCurve.TalentPointsForLevels(level - levels, level);
        levelUpRewardBodyText.text = levels == 1
            ? $"Hai raggiunto il livello {level}!\n\nEcco il tuo propoli per l'albero dei talenti."
            : $"Hai guadagnato {levels} livelli!\n\nEcco il tuo propoli per l'albero dei talenti.";
        levelUpRewardPropolisText.text = $"x {points}";
        levelUpRewardOkButton.interactable = true;
        bool wasAlreadyVisible = levelUpRewardPopup.activeSelf;
        levelUpRewardPopup.SetActive(true);
        levelUpRewardPopup.transform.SetAsLastSibling();
        if (!wasAlreadyVisible)
            PlayLevelUpSfx();
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
            TryStartFirstLevelTalentTour();
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
