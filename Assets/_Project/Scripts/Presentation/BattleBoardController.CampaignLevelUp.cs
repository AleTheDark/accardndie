using System.Collections;
using AccardND.GameCore;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private GameObject campaignLevelUpPopup;
    private Text campaignLevelUpBodyText;
    private bool campaignLevelUpPending;

    private void CreateCampaignLevelUpPopup(Transform parent, Font font)
    {
        Image overlay = CreateImage("Campaign Level Up Popup", parent, new Color(0f, 0f, 0f, 0.78f));
        overlay.raycastTarget = true;
        Stretch(overlay.rectTransform);
        campaignLevelUpPopup = overlay.gameObject;
        Canvas canvas = campaignLevelUpPopup.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 970;
        campaignLevelUpPopup.AddComponent<GraphicRaycaster>();

        Image dialog = CreateImage("Campaign Level Up Dialog", overlay.transform,
            new Color(0.01f, 0.018f, 0.028f, 0.99f));
        dialog.raycastTarget = true;
        StylePanel(dialog);
        SetRect(dialog.rectTransform, new Vector2(0.14f, 0.29f), new Vector2(0.86f, 0.71f));

        Text title = CreateText("Campaign Level Up Title", dialog.transform, font, 40,
            FontStyle.Normal, TextAnchor.MiddleCenter);
        Font levelUpTitleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
        if (levelUpTitleFont != null)
            title.font = levelUpTitleFont;
        title.fontSize = 40;
        title.fontStyle = FontStyle.Normal;
        title.resizeTextForBestFit = false;
        title.text = GameText.Get(GameTextKeys.Campaign.LevelUpTitle);
        AccardND.Battlefield.EditableRuntimeText.BindLocalized(
            title,
            GameTextKeys.Campaign.LevelUpTitle,
            "NUOVO LIVELLO!");
        title.color = new Color(0.95f, 0.79f, 0.34f);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.73f), new Vector2(0.94f, 0.93f));

        campaignLevelUpBodyText = CreateText("Campaign Level Up Body", dialog.transform, font, 30,
            FontStyle.Normal, TextAnchor.MiddleCenter);
        campaignLevelUpBodyText.fontSize = 30;
        campaignLevelUpBodyText.resizeTextForBestFit = false;
        campaignLevelUpBodyText.color = new Color(0.88f, 0.94f, 0.97f);
        campaignLevelUpBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetRect(campaignLevelUpBodyText.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.71f));

        Button nextRoomButton = CreateButton(
            "Next Room After Level Up",
            dialog.transform,
            font,
            GameText.Get(GameTextKeys.Campaign.NextRoom));
        AccardND.Battlefield.EditableRuntimeText.BindLocalized(
            nextRoomButton.GetComponentInChildren<Text>(),
            GameTextKeys.Campaign.NextRoom,
            "PROSSIMA STANZA");
        nextRoomButton.onClick.AddListener(new UnityAction(ContinueAfterCampaignLevelUp));
        ApplyMerchantRoomCta(nextRoomButton, nextRoomButton.GetComponentInChildren<Text>(),
            "UI/CampaignRestyle/campaign_cta_blue", preserveAspect: false);
        SetRect((RectTransform)nextRoomButton.transform, new Vector2(0.14f, 0.05f), new Vector2(0.86f, 0.31f));
        AccardND.Battlefield.MmoUiTheme.AddMotion(nextRoomButton);
        AccardND.PvpUi.PvpUiVfx.CreateRankedButton(
            (RectTransform)nextRoomButton.transform,
            new Color(0.2f, 0.68f, 1f, 1f));
        campaignLevelUpPopup.SetActive(false);
    }

    private void PlayCampaignExperienceReward(int previousLevel, int previousExperience, RoomReward reward)
    {
        if (reward.LevelsGained <= 0)
        {
			campaignLevelUpPending = false;
            RefreshCombatHudRefactor();
            return;
        }

		campaignLevelUpPending = true;
        if (combatExperienceFillRoutine != null)
            StopCoroutine(combatExperienceFillRoutine);
        combatExperienceFillRoutine = StartCoroutine(AnimateCampaignExperienceReward(
            previousLevel, previousExperience));
    }

    private IEnumerator AnimateCampaignExperienceReward(int level, int experience)
    {
        int finalLevel = runProgress.PlayerLevel;
        int finalExperience = runProgress.CurrentExperience;

        while (level < finalLevel)
        {
            int threshold = runProgress.GetExperienceThresholdForLevel(level);
            yield return AnimateExperienceSegment(experience, threshold, threshold);
            level++;
            experience = 0;
            SetExperienceVisual(0, runProgress.GetExperienceThresholdForLevel(level));
            yield return new WaitForSecondsRealtime(0.12f);
        }

        int finalThreshold = runProgress.ExperiencePerLevel;
        if (finalThreshold > 0)
            yield return AnimateExperienceSegment(experience, finalExperience, finalThreshold);
        else
        {
            if ((Object)(object)combatExperienceFill != (Object)null)
                combatExperienceFill.rectTransform.anchorMax = Vector2.one;
            if ((Object)(object)combatExperienceText != (Object)null)
                combatExperienceText.text = GameText.Get(GameTextKeys.Campaign.MaxLevel);
        }

        combatExperienceFillRoutine = null;
        ShowCampaignLevelUpPopup();
    }

    private IEnumerator AnimateExperienceSegment(int from, int to, int maximum)
    {
        float duration = Mathf.Lerp(0.28f, 1.05f, Mathf.Clamp01((float)Mathf.Abs(to - from) / Mathf.Max(1, maximum)));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            SetExperienceVisual(Mathf.RoundToInt(Mathf.Lerp(from, to, t)), maximum);
            yield return null;
        }
        SetExperienceVisual(to, maximum);
    }

    private void SetExperienceVisual(int current, int maximum)
    {
        if ((Object)(object)combatExperienceFill != (Object)null)
            combatExperienceFill.rectTransform.anchorMax = new Vector2(
                maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 1f, 1f);
        if ((Object)(object)combatExperienceText != (Object)null)
            combatExperienceText.text = maximum > 0
                ? GameText.Format(GameTextKeys.Campaign.ExperienceProgress, current, maximum)
                : GameText.Get(GameTextKeys.Campaign.MaxLevel);
    }

    private void ShowCampaignLevelUpPopup()
    {
        if (campaignLevelUpPopup == null || runProgress == null)
            return;
        campaignLevelUpBodyText.text = GameText.Format(GameTextKeys.Campaign.LevelUpBody, runProgress.PlayerLevel, runProgress.PlayerVigorDieSides);
        campaignLevelUpPopup.SetActive(true);
        campaignLevelUpPopup.transform.SetAsLastSibling();
        PlayLevelUpSfx();
    }

    private void ContinueAfterCampaignLevelUp()
    {
        campaignLevelUpPending = false;
        campaignLevelUpPopup.SetActive(false);
        HandlePrimaryAction();
    }
}
}
