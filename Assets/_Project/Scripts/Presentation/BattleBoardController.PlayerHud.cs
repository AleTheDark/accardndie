using System;
using System.Collections;
using System.Linq;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private sealed class CombatantHud
	{
		public RectTransform Rect;
		public Text NameText;
		public Text LevelText;
		public Text ExperienceText;
		public Image ExperienceFill;
		public Image DiceImage;
		public Text DiceText;
		public Image DeckImage;
		public Text DeckText;
		public Image CooldownImage;
		public Text CooldownText;
		public Image GraveyardImage;
		public Text GraveyardText;
	}

	private CombatantHud playerHud;
	private CombatantHud cpuHud;
	private RectTransform hudTooltipRect;
	private Text hudTooltipText;
	private Coroutine hudTooltipRoutine;

	private void CreatePlayerHudView(Font font)
	{
		playerHud = CreateCombatantHud("Player HUD", font, "PLAYER");
		cpuHud = CreateCombatantHud("CPU HUD", font, "CPU MASTER");
		ConfigureCombatantHudTooltips(playerHud, isPlayer: true);
		ConfigureCombatantHudTooltips(cpuHud, isPlayer: false);
		CreateHudTooltip(font);
	}

	private CombatantHud CreateCombatantHud(string name, Font font, string displayName)
	{
		Image panel = CreateImage(name, (Transform)(object)safeAreaRoot, new Color(0.008f, 0.014f, 0.022f, 0.9f));
		StylePanel(panel);
		SetRect(panel.rectTransform, new Vector2(0.76f, 0.78f), new Vector2(0.985f, 0.93f));

		Image avatarFrame = CreateImage(name + " Avatar Placeholder", ((Component)panel).transform, new Color(0.08f, 0.12f, 0.15f, 0.98f));
		StylePanel(avatarFrame);
		SetRect(avatarFrame.rectTransform, new Vector2(0.045f, 0.31f), new Vector2(0.25f, 0.86f));

		Image avatarImage = CreateImage(name + " Avatar Icon Placeholder", ((Component)avatarFrame).transform, new Color(0.2f, 0.28f, 0.3f, 0.85f));
		avatarImage.preserveAspect = true;
		Stretch(avatarImage.rectTransform, 8f);

		Text avatarText = CreateText(name + " Avatar Placeholder Text", ((Component)avatarFrame).transform, font, 14, (FontStyle)1, (TextAnchor)4);
		avatarText.text = "ICONA";
		avatarText.color = new Color(0.65f, 0.75f, 0.76f);
		Stretch(avatarText.rectTransform, 3f);

		CombatantHud hud = new CombatantHud
		{
			Rect = panel.rectTransform,
			NameText = CreateText(name + " Name", ((Component)panel).transform, font, 21, (FontStyle)1, (TextAnchor)3),
			LevelText = CreateText(name + " Level", ((Component)panel).transform, font, 16, (FontStyle)1, (TextAnchor)3),
			ExperienceText = CreateText(name + " Progress Value", ((Component)panel).transform, font, 14, (FontStyle)1, (TextAnchor)5),
			DiceImage = CreateImage(name + " Dice Icon", ((Component)panel).transform, Color.white),
			DiceText = CreateText(name + " Dice Value", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3),
			DeckImage = CreateImage(name + " Deck Icon", ((Component)panel).transform, Color.white),
			DeckText = CreateText(name + " Deck Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3),
			CooldownImage = CreateImage(name + " Cooldown Icon", ((Component)panel).transform, Color.white),
			CooldownText = CreateText(name + " Cooldown Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3),
			GraveyardImage = CreateImage(name + " Graveyard Icon", ((Component)panel).transform, Color.white),
			GraveyardText = CreateText(name + " Graveyard Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3)
		};

		hud.NameText.text = displayName;
		hud.NameText.color = new Color(0.96f, 0.82f, 0.42f);
		SetRect(hud.NameText.rectTransform, new Vector2(0.29f, 0.68f), new Vector2(0.94f, 0.9f));

		hud.LevelText.color = new Color(0.87f, 0.92f, 0.94f);
		SetRect(hud.LevelText.rectTransform, new Vector2(0.29f, 0.49f), new Vector2(0.47f, 0.66f));

		Image progressTrack = CreateImage(name + " Progress Track", ((Component)panel).transform, new Color(0.04f, 0.06f, 0.07f, 0.95f));
		StylePanel(progressTrack);
		SetRect(progressTrack.rectTransform, new Vector2(0.49f, 0.52f), new Vector2(0.94f, 0.63f));

		hud.ExperienceFill = CreateImage(name + " Progress Fill", ((Component)progressTrack).transform, new Color(0.72f, 0.48f, 0.12f, 0.95f));
		SetRect(hud.ExperienceFill.rectTransform, Vector2.zero, new Vector2(0f, 1f));

		hud.ExperienceText.color = new Color(0.86f, 0.92f, 0.95f);
		SetRect(hud.ExperienceText.rectTransform, new Vector2(0.49f, 0.34f), new Vector2(0.94f, 0.49f));

		ConfigureHudStat(hud.DiceImage, hud.DiceText, 0.05f, 0.25f);
		ConfigureHudStat(hud.DeckImage, hud.DeckText, 0.29f, 0.49f);
		ConfigureHudStat(hud.CooldownImage, hud.CooldownText, 0.54f, 0.72f);
		ConfigureHudStat(hud.GraveyardImage, hud.GraveyardText, 0.76f, 0.94f);

		hud.DiceImage.preserveAspect = true;
		hud.DeckImage.sprite = LoadSpriteResource("UI/deck_icon");
		hud.DeckImage.preserveAspect = true;
		hud.CooldownImage.sprite = LoadSpriteResource("UI/cooldown_icon");
		hud.CooldownImage.preserveAspect = true;
		hud.GraveyardImage.sprite = LoadSpriteResource("UI/graveyard_icon");
		hud.GraveyardImage.preserveAspect = true;

		return hud;
	}

	private void ConfigureCombatantHudTooltips(CombatantHud hud, bool isPlayer)
	{
		if (hud == null || (Object)(object)hud.Rect == (Object)null)
		{
			return;
		}
		CreateHudTooltipButton(hud.Rect, new Vector2(0.035f, 0.0f), new Vector2(0.265f, 0.33f), () => ShowHudTooltip(isPlayer ?$"Il tuo dado Vigore: {hud.DiceText.text}." : $"Il dado Vigore del tuo avversario: {hud.DiceText.text}.", hud.Rect));
		CreateHudTooltipButton(hud.Rect, new Vector2(0.275f, 0.0f), new Vector2(0.515f, 0.33f), () => ShowHudTooltip(isPlayer ?$"Carte disponibili nel tuo mazzo: {hud.DeckText.text}." : $"Carte dell'avversario in formazione: {hud.DeckText.text}.", hud.Rect));
		CreateHudTooltipButton(hud.Rect, new Vector2(0.525f, 0.0f), new Vector2(0.745f, 0.33f), () => ShowHudTooltip(isPlayer ?$"Carte in cooldown: {hud.CooldownText.text}." : $"Carte avversarie in cooldown: {hud.CooldownText.text}.", hud.Rect));
		CreateHudTooltipButton(hud.Rect, new Vector2(0.75f, 0.0f), new Vector2(0.965f, 0.33f), () => ShowHudTooltip(isPlayer ?$"Carte nel tuo cimitero: {hud.GraveyardText.text}." : $"Carte avversarie eliminate: {hud.GraveyardText.text}.", hud.Rect));
	}

	private Button CreateHudTooltipButton(RectTransform parent, Vector2 minimum, Vector2 maximum, Action action)
	{
		Button button = CreateTransparentButton("HUD Tooltip Hotspot", (Transform)(object)parent);
		SetRect((RectTransform)((Component)button).transform, minimum, maximum);
		((UnityEvent)button.onClick).AddListener(new UnityAction(() => action?.Invoke()));
		return button;
	}

	private void CreateHudTooltip(Font font)
	{
		Image image = CreateImage("HUD Tooltip", (Transform)(object)safeAreaRoot, new Color(0.008f, 0.014f, 0.022f, 0.97f));
		StylePanel(image);
		image.raycastTarget = false;
		hudTooltipRect = image.rectTransform;
		hudTooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
		hudTooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
		hudTooltipRect.pivot = new Vector2(0.5f, 0.5f);
		hudTooltipRect.sizeDelta = new Vector2(360f, 72f);
		hudTooltipText = CreateText("HUD Tooltip Text", ((Component)image).transform, font, 18, (FontStyle)1, (TextAnchor)4);
		hudTooltipText.color = new Color(0.9f, 0.94f, 0.96f);
		hudTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
		hudTooltipText.verticalOverflow = VerticalWrapMode.Truncate;
		Stretch(hudTooltipText.rectTransform, 10f);
		((Component)image).gameObject.SetActive(false);
	}

	private void ShowHudTooltip(string message, RectTransform sourceRect)
	{
		if ((Object)(object)hudTooltipRect == (Object)null || (Object)(object)hudTooltipText == (Object)null)
		{
			SetMessage(message);
			return;
		}
		hudTooltipText.text = message;
		hudTooltipRect.sizeDelta = new Vector2(380f, 76f);
		Vector2 sourceCenter = (Object)(object)sourceRect != (Object)null ?RectCenterInSafeArea(sourceRect) : Vector2.zero;
		Rect safeRect = safeAreaRoot.rect;
		float yOffset = sourceCenter.y > 0f ?-78f :78f;
		float halfWidth = hudTooltipRect.sizeDelta.x * 0.5f;
		float halfHeight = hudTooltipRect.sizeDelta.y * 0.5f;
		hudTooltipRect.anchoredPosition = new Vector2(
			Mathf.Clamp(sourceCenter.x, safeRect.xMin + halfWidth + 8f, safeRect.xMax - halfWidth - 8f),
			Mathf.Clamp(sourceCenter.y + yOffset, safeRect.yMin + halfHeight + 8f, safeRect.yMax - halfHeight - 8f));
		((Component)hudTooltipRect).gameObject.SetActive(true);
		((Transform)hudTooltipRect).SetAsLastSibling();
		if (hudTooltipRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(hudTooltipRoutine);
		}
		hudTooltipRoutine = ((MonoBehaviour)this).StartCoroutine(HideHudTooltipAfterDelay());
	}

	private IEnumerator HideHudTooltipAfterDelay()
	{
		yield return (object)new WaitForSecondsRealtime(2.15f);
		if ((Object)(object)hudTooltipRect != (Object)null)
		{
			((Component)hudTooltipRect).gameObject.SetActive(false);
		}
		hudTooltipRoutine = null;
	}

	private static void ConfigureHudStat(Image image, Text text, float minimumX, float maximumX)
	{
		SetRect(image.rectTransform, new Vector2(minimumX, 0.015f), new Vector2(minimumX + 0.13f, 0.31f));
		text.color = new Color(0.87f, 0.92f, 0.94f);
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 11;
		text.resizeTextMaxSize = 16;
		SetRect(text.rectTransform, new Vector2(minimumX + 0.135f, 0.065f), new Vector2(maximumX, 0.255f));
	}

	private void RefreshPlayerHud()
	{
		if (playerHud == null || runProgress == null)
		{
			return;
		}

		RefreshCombatantHud(
			playerHud,
			"PLAYER",
			$"LV {runProgress.PlayerLevel}",
			$"{runProgress.CurrentExperience}/{runProgress.ExperiencePerLevel} EXP",
			runProgress.ExperiencePerLevel <= 0 ?0f : (float)runProgress.CurrentExperience / runProgress.ExperiencePerLevel,
			runProgress.PlayerVigorDieSides,
			campaignDeck?.AvailableCount ?? playerReserve.Count,
			campaignDeck?.CooldownCount ?? 0,
			campaignDeck?.GraveyardCount ?? 0);
	}

	private void RefreshCpuHud()
	{
		if (cpuHud == null || runProgress == null)
		{
			return;
		}

		int defeatedCount = cpuCards.Count((BattleCardState card) => card != null && card.Eliminated);
		int activeCount = cpuCards.Count((BattleCardState card) => card != null && !card.Eliminated);
		int totalCount = cpuCards.Count > 0 ?cpuCards.Count : initialCpuFormation.Count;
		float progress = totalCount <= 0 ?0f : (float)activeCount / totalCount;

		RefreshCombatantHud(
			cpuHud,
			"CPU MASTER",
			currentRoomType == RoomType.Boss ?$"BOSS {currentMonsterTier}" : $"MOSTRO {currentMonsterTier}",
			totalCount > 0 ?$"{activeCount}/{totalCount} ATTIVI" : "AVVERSARIO",
			progress,
			runProgress.MasterVigorDieSides,
			totalCount,
			0,
			defeatedCount);
	}

	private static void RefreshCombatantHud(
		CombatantHud hud,
		string name,
		string level,
		string progressLabel,
		float progress,
		int diceSides,
		int deckCount,
		int cooldownCount,
		int graveyardCount)
	{
		hud.NameText.text = name;
		hud.LevelText.text = level;
		hud.ExperienceText.text = progressLabel;
		SetRect(hud.ExperienceFill.rectTransform, Vector2.zero, new Vector2(Mathf.Clamp01(progress), 1f));
		hud.DiceImage.sprite = LoadDivineDiceSprite(diceSides);
		hud.DiceText.text = $"D{diceSides}";
		hud.DeckText.text = deckCount.ToString();
		hud.CooldownText.text = cooldownCount.ToString();
		hud.GraveyardText.text = graveyardCount.ToString();
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
