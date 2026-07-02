using System;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private RectTransform playerHudRect;
	private Image playerHudAvatarImage;
	private Text playerHudNameText;
	private Text playerHudLevelText;
	private Text playerHudExperienceText;
	private Image playerHudExperienceFill;
	private Image playerHudDiceImage;
	private Text playerHudDiceText;
	private Image playerHudCooldownImage;
	private Text playerHudCooldownText;
	private Image playerHudGraveyardImage;
	private Text playerHudGraveyardText;
	private Text playerHudDeckText;

	private void CreatePlayerHudView(Font font)
	{
		Image panel = CreateImage("Player HUD", (Transform)(object)safeAreaRoot, new Color(0.008f, 0.014f, 0.022f, 0.9f));
		StylePanel(panel);
		playerHudRect = panel.rectTransform;
		SetRect(playerHudRect, new Vector2(0.025f, 0.035f), new Vector2(0.275f, 0.21f));

		Image avatarFrame = CreateImage("Player Avatar Placeholder", ((Component)panel).transform, new Color(0.08f, 0.12f, 0.15f, 0.98f));
		StylePanel(avatarFrame);
		SetRect(avatarFrame.rectTransform, new Vector2(0.045f, 0.31f), new Vector2(0.25f, 0.86f));

		playerHudAvatarImage = CreateImage("Player Avatar Icon Placeholder", ((Component)avatarFrame).transform, new Color(0.2f, 0.28f, 0.3f, 0.85f));
		playerHudAvatarImage.preserveAspect = true;
		Stretch(playerHudAvatarImage.rectTransform, 8f);

		Text avatarText = CreateText("Player Avatar Placeholder Text", ((Component)avatarFrame).transform, font, 14, (FontStyle)1, (TextAnchor)4);
		avatarText.text = "ICONA";
		avatarText.color = new Color(0.65f, 0.75f, 0.76f);
		Stretch(avatarText.rectTransform, 3f);

		playerHudNameText = CreateText("Player Name", ((Component)panel).transform, font, 21, (FontStyle)1, (TextAnchor)3);
		playerHudNameText.text = "PLAYER";
		playerHudNameText.color = new Color(0.96f, 0.82f, 0.42f);
		SetRect(playerHudNameText.rectTransform, new Vector2(0.29f, 0.68f), new Vector2(0.94f, 0.9f));

		playerHudLevelText = CreateText("Player Level", ((Component)panel).transform, font, 16, (FontStyle)1, (TextAnchor)3);
		playerHudLevelText.color = new Color(0.87f, 0.92f, 0.94f);
		SetRect(playerHudLevelText.rectTransform, new Vector2(0.29f, 0.49f), new Vector2(0.47f, 0.66f));

		Image experienceTrack = CreateImage("Player EXP Track", ((Component)panel).transform, new Color(0.04f, 0.06f, 0.07f, 0.95f));
		StylePanel(experienceTrack);
		SetRect(experienceTrack.rectTransform, new Vector2(0.49f, 0.52f), new Vector2(0.94f, 0.63f));

		playerHudExperienceFill = CreateImage("Player EXP Fill", ((Component)experienceTrack).transform, new Color(0.72f, 0.48f, 0.12f, 0.95f));
		SetRect(playerHudExperienceFill.rectTransform, Vector2.zero, new Vector2(0f, 1f));

		playerHudExperienceText = CreateText("Player EXP Value", ((Component)panel).transform, font, 14, (FontStyle)1, (TextAnchor)5);
		playerHudExperienceText.color = new Color(0.86f, 0.92f, 0.95f);
		SetRect(playerHudExperienceText.rectTransform, new Vector2(0.49f, 0.34f), new Vector2(0.94f, 0.49f));

		playerHudDiceImage = CreateImage("Player Dice Icon", ((Component)panel).transform, Color.white);
		playerHudDiceImage.preserveAspect = true;
		SetRect(playerHudDiceImage.rectTransform, new Vector2(0.05f, 0.055f), new Vector2(0.15f, 0.26f));

		playerHudDiceText = CreateText("Player Dice Value", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3);
		playerHudDiceText.color = new Color(0.87f, 0.92f, 0.94f);
		SetRect(playerHudDiceText.rectTransform, new Vector2(0.155f, 0.075f), new Vector2(0.29f, 0.245f));

		playerHudCooldownImage = CreateImage("Cooldown Icon", ((Component)panel).transform, Color.white);
		playerHudCooldownImage.sprite = LoadSpriteResource("UI/cooldown_icon");
		playerHudCooldownImage.preserveAspect = true;
		SetRect(playerHudCooldownImage.rectTransform, new Vector2(0.34f, 0.04f), new Vector2(0.45f, 0.27f));

		playerHudCooldownText = CreateText("Cooldown Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3);
		playerHudCooldownText.color = new Color(0.87f, 0.92f, 0.94f);
		SetRect(playerHudCooldownText.rectTransform, new Vector2(0.455f, 0.075f), new Vector2(0.56f, 0.245f));

		playerHudGraveyardImage = CreateImage("Graveyard Icon", ((Component)panel).transform, Color.white);
		playerHudGraveyardImage.sprite = LoadSpriteResource("UI/graveyard_icon");
		playerHudGraveyardImage.preserveAspect = true;
		SetRect(playerHudGraveyardImage.rectTransform, new Vector2(0.61f, 0.04f), new Vector2(0.72f, 0.27f));

		playerHudGraveyardText = CreateText("Graveyard Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3);
		playerHudGraveyardText.color = new Color(0.87f, 0.92f, 0.94f);
		SetRect(playerHudGraveyardText.rectTransform, new Vector2(0.725f, 0.075f), new Vector2(0.83f, 0.245f));

		playerHudDeckText = CreateText("Deck Count", ((Component)panel).transform, font, 14, (FontStyle)1, (TextAnchor)5);
		playerHudDeckText.color = new Color(0.74f, 0.84f, 0.86f);
		SetRect(playerHudDeckText.rectTransform, new Vector2(0.82f, 0.075f), new Vector2(0.95f, 0.245f));
	}

	private void RefreshPlayerHud()
	{
		if ((Object)(object)playerHudRect == (Object)null || runProgress == null)
		{
			return;
		}

		playerHudNameText.text = "PLAYER";
		playerHudLevelText.text = $"LV {runProgress.PlayerLevel}";
		playerHudExperienceText.text = $"{runProgress.CurrentExperience}/{runProgress.ExperiencePerLevel} EXP";
		float experienceProgress = runProgress.ExperiencePerLevel <= 0 ?0f : Mathf.Clamp01((float)runProgress.CurrentExperience / runProgress.ExperiencePerLevel);
		SetRect(playerHudExperienceFill.rectTransform, Vector2.zero, new Vector2(experienceProgress, 1f));
		playerHudDiceImage.sprite = LoadDivineDiceSprite(runProgress.PlayerVigorDieSides);
		playerHudDiceText.text = $"D{runProgress.PlayerVigorDieSides}";
		playerHudCooldownText.text = (campaignDeck?.CooldownCount ?? 0).ToString();
		playerHudGraveyardText.text = (campaignDeck?.GraveyardCount ?? 0).ToString();
		playerHudDeckText.text = $"MAZZO {campaignDeck?.AvailableCount ?? playerReserve.Count}";
	}

	private void RefreshRoomHud(string phaseLabel, string scenarioLabel)
	{
		if ((Object)(object)topInfoText == (Object)null || runProgress == null)
		{
			return;
		}

		string encounterLabel = currentRoomType == RoomType.Boss ?$"BOSS {currentMonsterTier}" : $"MOSTRO {currentMonsterTier}";
		string roundLabel = roundNumber > 0 && !draftActive && !deploymentDraftActive ?$"ROUND {roundNumber}" : phaseLabel;
		string scenario = string.IsNullOrWhiteSpace(scenarioLabel) ?string.Empty : $"  |  {scenarioLabel}";
		topInfoText.text = $"STANZA {runProgress.RoomsCleared + 1}  |  {encounterLabel}  |  CPU D{runProgress.MasterVigorDieSides}  |  {roundLabel}{scenario}";
	}

	private static Sprite LoadDivineDiceSprite(int sides)
	{
		string resourcePath = $"DiceUI/DiceImages/D{sides}_Divine";
		Sprite sprite = LoadSpriteResource(resourcePath);
		if ((Object)(object)sprite != (Object)null)
		{
			return sprite;
		}
#if UNITY_EDITOR
		return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DiceUI/DiceImages/D{sides}_Divine.png");
#else
		return null;
#endif
	}
}
}
