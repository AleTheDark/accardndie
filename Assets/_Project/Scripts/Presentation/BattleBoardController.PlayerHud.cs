using System;
using System.Collections;
using System.Linq;
using AccardND.GameCore;
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
		public Text AvatarText;
		public Image AvatarFrame;
		public Image AvatarImage;
		public Text NameText;
		public Text LevelText;
		public Text ExperienceText;
		public Image ExperienceTrack;
		public RectTransform ExperienceFillMask;
		public Image ExperienceFill;
		public Image DiceImage;
		public Text DiceText;
		public Image DeckImage;
		public Text DeckText;
		public Image CooldownImage;
		public Text CooldownText;
		public Image GraveyardImage;
		public Text GraveyardText;
		public Button DeckTooltipButton;
		public Button CooldownTooltipButton;
		public Button GraveyardTooltipButton;
	}

	private CombatantHud playerHud;
	private CombatantHud cpuHud;
	private RectTransform hudTooltipRect;
	private Text hudTooltipText;
	private Coroutine hudTooltipRoutine;

	private void CreatePlayerHudView(Font font)
	{
		playerHud = CreateCombatantHud("Player HUD", font, ResolvePlayerHudDisplayName());
		cpuHud = CreateCombatantHud("CPU HUD", font, "CPU MASTER");
		ConfigureCombatantHudTooltips(playerHud, isPlayer: true);
		ConfigureCombatantHudTooltips(cpuHud, isPlayer: false);
		CreateHudTooltip(font);
	}

	private CombatantHud CreateCombatantHud(string name, Font font, string displayName)
	{
		Image panel = CreateImage(name, (Transform)(object)safeAreaRoot, new Color(0.018f, 0.028f, 0.045f, 0.94f));
		StylePanel(panel);
		SetRect(panel.rectTransform, new Vector2(0.735f, 0.815f), new Vector2(0.985f, 0.985f));
		AddHudPanelGems(panel.rectTransform, name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase));

		Image avatarFrame = CreateImage(name + " Avatar Placeholder", ((Component)panel).transform, new Color(0.045f, 0.105f, 0.13f, 0.98f));
		StylePanel(avatarFrame);
		SetRect(avatarFrame.rectTransform, new Vector2(0.035f, 0.16f), new Vector2(0.17f, 0.86f));

		Image avatarImage = CreateImage(name + " Avatar Icon Placeholder", ((Component)avatarFrame).transform, new Color(0.2f, 0.28f, 0.3f, 0.85f));
		avatarImage.preserveAspect = true;
		Stretch(avatarImage.rectTransform, 8f);

		Text avatarText = CreateText(name + " Avatar Placeholder Text", ((Component)avatarFrame).transform, font, 14, (FontStyle)1, (TextAnchor)4);
		avatarText.text = "ICONA";
		avatarText.color = new Color(0.72f, 0.9f, 0.94f);
		avatarText.resizeTextForBestFit = true;
		avatarText.resizeTextMinSize = 8;
		avatarText.resizeTextMaxSize = 14;
		Stretch(avatarText.rectTransform, 3f);

		Image progressTrack = CreateImage(name + " Progress Track", ((Component)panel).transform, new Color(0.012f, 0.03f, 0.05f, 0.95f));
		StylePanel(progressTrack);
		SetRect(progressTrack.rectTransform, new Vector2(0.36f, 0.47f), new Vector2(0.61f, 0.62f));

		CombatantHud hud = new CombatantHud
		{
			Rect = panel.rectTransform,
			AvatarFrame = avatarFrame,
			AvatarImage = avatarImage,
			AvatarText = avatarText,
			NameText = CreateText(name + " Name", ((Component)panel).transform, font, 21, (FontStyle)1, (TextAnchor)3),
			LevelText = CreateText(name + " Level", ((Component)panel).transform, font, 16, (FontStyle)1, (TextAnchor)3),
			ExperienceText = CreateText(name + " Progress Value", ((Component)panel).transform, font, 14, (FontStyle)1, (TextAnchor)5),
			ExperienceTrack = progressTrack,
			DiceImage = CreateImage(name + " Dice Icon", ((Component)panel).transform, Color.white),
			DiceText = CreateText(name + " Dice Value", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)4),
			DeckImage = CreateImage(name + " Deck Icon", ((Component)panel).transform, Color.white),
			DeckText = CreateText(name + " Deck Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3),
			CooldownImage = CreateImage(name + " Cooldown Icon", ((Component)panel).transform, Color.white),
			CooldownText = CreateText(name + " Cooldown Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3),
			GraveyardImage = CreateImage(name + " Graveyard Icon", ((Component)panel).transform, Color.white),
			GraveyardText = CreateText(name + " Graveyard Count", ((Component)panel).transform, font, 15, (FontStyle)1, (TextAnchor)3)
		};

		hud.NameText.text = displayName;
		hud.NameText.color = AccardND.Battlefield.MmoUiTheme.Gold;
		hud.NameText.resizeTextForBestFit = true;
		hud.NameText.resizeTextMinSize = 13;
		hud.NameText.resizeTextMaxSize = 21;
		SetRect(hud.NameText.rectTransform, new Vector2(0.16f, 0.67f), new Vector2(0.61f, 0.9f));

		hud.LevelText.color = new Color(0.9f, 0.97f, 1f);
		SetRect(hud.LevelText.rectTransform, new Vector2(0.16f, 0.43f), new Vector2(0.34f, 0.64f));

		Image progressFillMask = CreateImage(name + " Progress Fill Mask", ((Component)progressTrack).transform, Color.white);
		StylePanel(progressFillMask);
		progressFillMask.raycastTarget = false;
		Stretch(progressFillMask.rectTransform, 2.5f);
		Mask mask = ((Component)progressFillMask).gameObject.AddComponent<Mask>();
		mask.showMaskGraphic = false;
		hud.ExperienceFillMask = progressFillMask.rectTransform;

		hud.ExperienceFill = CreateImage(name + " Progress Fill", ((Component)progressFillMask).transform, AccardND.Battlefield.MmoUiTheme.Arcane);
		SetRect(hud.ExperienceFill.rectTransform, Vector2.zero, new Vector2(0f, 1f));

		hud.ExperienceText.color = new Color(0.76f, 0.92f, 0.98f);
		hud.ExperienceText.resizeTextForBestFit = true;
		hud.ExperienceText.resizeTextMinSize = 9;
		hud.ExperienceText.resizeTextMaxSize = 14;
		SetRect(hud.ExperienceText.rectTransform, new Vector2(0.36f, 0.22f), new Vector2(0.61f, 0.43f));

		ConfigureHudStat(hud.DiceImage, hud.DiceText, new Vector2(0.66f, 0.52f), new Vector2(0.81f, 0.88f));
		ConfigureHudStat(hud.DeckImage, hud.DeckText, new Vector2(0.83f, 0.52f), new Vector2(0.98f, 0.88f));
		ConfigureHudStat(hud.CooldownImage, hud.CooldownText, new Vector2(0.66f, 0.12f), new Vector2(0.81f, 0.48f));
		ConfigureHudStat(hud.GraveyardImage, hud.GraveyardText, new Vector2(0.83f, 0.12f), new Vector2(0.98f, 0.48f));

		hud.DiceImage.preserveAspect = true;
		hud.DeckImage.sprite = LoadSpriteResource("UI/deck_icon");
		hud.DeckImage.preserveAspect = true;
		hud.CooldownImage.sprite = LoadSpriteResource("UI/cooldown_icon");
		hud.CooldownImage.preserveAspect = true;
		hud.GraveyardImage.sprite = LoadSpriteResource("UI/graveyard_icon");
		hud.GraveyardImage.preserveAspect = true;

		if (name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase))
		{
			ConfigureCpuHudPresentation(hud);
		}

		SetHudImageVisible(hud.AvatarFrame, false);
		SetHudImageVisible(hud.AvatarImage, false);
		SetHudRectVisible(hud.AvatarText?.rectTransform, false);

		return hud;
	}

	private static void ConfigurePlayerHudStandardPresentation(CombatantHud hud)
	{
		if (hud == null)
		{
			return;
		}
		SetRect(hud.DiceImage.rectTransform, new Vector2(0.675f, 0.52f), new Vector2(0.753f, 0.88f));
		SetRect(hud.DiceText.rectTransform, new Vector2(0.759f, 0.6f), new Vector2(0.825f, 0.8f));
		SetRect(hud.LevelText.rectTransform, new Vector2(0.16f, 0.43f), new Vector2(0.36f, 0.64f));
		SetRect(hud.ExperienceText.rectTransform, new Vector2(0.36f, 0.43f), new Vector2(0.61f, 0.64f));
		SetRect(hud.ExperienceTrack.rectTransform, new Vector2(0.16f, 0.22f), new Vector2(0.61f, 0.37f));
		hud.LevelText.alignment = TextAnchor.MiddleLeft;
		hud.ExperienceText.alignment = TextAnchor.MiddleRight;
		hud.DiceText.alignment = TextAnchor.MiddleCenter;
		SetHudStatVisible(hud.DeckImage, hud.DeckText, true);
		SetHudStatVisible(hud.CooldownImage, hud.CooldownText, true);
		SetHudStatVisible(hud.GraveyardImage, hud.GraveyardText, true);
		SetHudButtonVisible(hud.DeckTooltipButton, true);
		SetHudButtonVisible(hud.CooldownTooltipButton, true);
		SetHudButtonVisible(hud.GraveyardTooltipButton, true);
	}

	private static void ConfigurePlayerCampaignHudPresentation(CombatantHud hud)
	{
		if (hud == null)
		{
			return;
		}
		SetRect(hud.DiceImage.rectTransform, new Vector2(0.635f, 0.18f), new Vector2(0.865f, 0.9f));
		SetRect(hud.DiceText.rectTransform, new Vector2(0.87f, 0.36f), new Vector2(0.985f, 0.76f));
		SetRect(hud.LevelText.rectTransform, new Vector2(0.16f, 0.43f), new Vector2(0.36f, 0.64f));
		SetRect(hud.ExperienceText.rectTransform, new Vector2(0.36f, 0.43f), new Vector2(0.61f, 0.64f));
		SetRect(hud.ExperienceTrack.rectTransform, new Vector2(0.16f, 0.22f), new Vector2(0.61f, 0.37f));
		hud.LevelText.alignment = TextAnchor.MiddleLeft;
		hud.ExperienceText.alignment = TextAnchor.MiddleRight;
		hud.DiceText.alignment = TextAnchor.MiddleCenter;
		SetHudStatVisible(hud.DeckImage, hud.DeckText, false);
		SetHudStatVisible(hud.CooldownImage, hud.CooldownText, false);
		SetHudStatVisible(hud.GraveyardImage, hud.GraveyardText, false);
		SetHudButtonVisible(hud.DeckTooltipButton, false);
		SetHudButtonVisible(hud.CooldownTooltipButton, false);
		SetHudButtonVisible(hud.GraveyardTooltipButton, false);
	}

	private static void ConfigureCpuHudPresentation(CombatantHud hud)
	{
		if (hud == null)
		{
			return;
		}
		if ((Object)(object)hud.AvatarText != (Object)null)
		{
			hud.AvatarText.text = "CPU";
			hud.AvatarText.resizeTextMinSize = 12;
			hud.AvatarText.resizeTextMaxSize = 18;
		}
		hud.NameText.alignment = TextAnchor.MiddleLeft;
		hud.LevelText.alignment = TextAnchor.MiddleLeft;
		hud.ExperienceText.alignment = TextAnchor.MiddleLeft;
		hud.DiceText.alignment = TextAnchor.MiddleCenter;
		hud.NameText.color = AccardND.Battlefield.MmoUiTheme.Gold;
		hud.LevelText.color = new Color(0.93f, 0.97f, 1f);
		hud.ExperienceText.color = new Color(0.72f, 0.9f, 0.98f);
		SetRect(hud.NameText.rectTransform, new Vector2(0.16f, 0.62f), new Vector2(0.58f, 0.9f));
		SetRect(hud.LevelText.rectTransform, new Vector2(0.16f, 0.36f), new Vector2(0.62f, 0.58f));
		SetRect(hud.ExperienceText.rectTransform, new Vector2(0.16f, 0.1f), new Vector2(0.72f, 0.32f));
		SetRect(hud.DiceImage.rectTransform, new Vector2(0.62f, 0.18f), new Vector2(0.85f, 0.9f));
		SetRect(hud.DiceText.rectTransform, new Vector2(0.855f, 0.36f), new Vector2(0.97f, 0.76f));
	}

	private void ConfigureCombatantHudTooltips(CombatantHud hud, bool isPlayer)
	{
		if (hud == null || (Object)(object)hud.Rect == (Object)null)
		{
			return;
		}
		CreateHudTooltipButton(hud.Rect, isPlayer ?new Vector2(0.64f, 0.5f) : new Vector2(0.6f, 0.16f), isPlayer ?new Vector2(0.82f, 0.92f) : new Vector2(0.88f, 0.94f), () => ShowHudTooltip(isPlayer ?$"Il tuo dado Vigore: {hud.DiceText.text}." : $"Dado Vigore del Master in questa stanza: {hud.DiceText.text}.", hud.Rect));
		if (!isPlayer)
		{
			CreateHudTooltipButton(hud.Rect, new Vector2(0.205f, 0.08f), new Vector2(0.72f, 0.34f), () => ShowHudTooltip(CurrentCpuProgressTooltip(), hud.Rect));
			return;
		}
		hud.DeckTooltipButton = CreateHudTooltipButton(hud.Rect, new Vector2(0.82f, 0.5f), new Vector2(1f, 0.92f), () => ShowHudTooltip($"Carte disponibili nel tuo mazzo: {hud.DeckText.text}.", hud.Rect));
		hud.CooldownTooltipButton = CreateHudTooltipButton(hud.Rect, new Vector2(0.64f, 0.08f), new Vector2(0.82f, 0.5f), () => ShowHudTooltip($"Carte in cooldown: {hud.CooldownText.text}.", hud.Rect));
		hud.GraveyardTooltipButton = CreateHudTooltipButton(hud.Rect, new Vector2(0.82f, 0.08f), new Vector2(1f, 0.5f), () => ShowHudTooltip($"Carte nel tuo cimitero: {hud.GraveyardText.text}.", hud.Rect));
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
		Image image = CreateImage("HUD Tooltip", (Transform)(object)safeAreaRoot, new Color(0.018f, 0.028f, 0.045f, 0.98f));
		StylePanel(image);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(image.rectTransform, "Tooltip Gem", new Vector2(0.5f, 1f), new Vector2(34f, 34f), new Color(1f, 1f, 1f, 0.95f));
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
		yield return WaitForCardInspectionPause(2.15f);
		if ((Object)(object)hudTooltipRect != (Object)null)
		{
			((Component)hudTooltipRect).gameObject.SetActive(false);
		}
		hudTooltipRoutine = null;
	}

	private static void ConfigureHudStat(Image image, Text text, Vector2 minimum, Vector2 maximum)
	{
		float width = maximum.x - minimum.x;
		SetRect(image.rectTransform, new Vector2(minimum.x, minimum.y), new Vector2(minimum.x + width * 0.52f, maximum.y));
		text.color = new Color(0.9f, 0.96f, 1f);
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 11;
		text.resizeTextMaxSize = 16;
		SetRect(text.rectTransform, new Vector2(minimum.x + width * 0.56f, minimum.y + 0.08f), new Vector2(maximum.x, maximum.y - 0.08f));
	}

	private void RefreshPlayerHud()
	{
		if (playerHud == null || runProgress == null)
		{
			return;
		}

		RefreshCombatantHud(
			playerHud,
			isPlayer: true,
			ResolvePlayerHudDisplayName(),
			$"LV {runProgress.PlayerLevel}",
			$"{runProgress.CurrentExperience}/{runProgress.ExperiencePerLevel} EXP",
			runProgress.ExperiencePerLevel <= 0 ?0f : (float)runProgress.CurrentExperience / runProgress.ExperiencePerLevel,
			EffectivePlayerHudVigorDieSides(),
			campaignDeck?.AvailableCount ?? playerReserve.Count,
			campaignDeck?.CooldownCount ?? 0,
			campaignDeck?.GraveyardCount ?? 0);
		ConfigurePlayerCampaignHudPresentation(playerHud);
	}

	private static void AddHudPanelGems(RectTransform rect, bool cpu)
	{
		Color tint = cpu ? new Color(1f, 0.7f, 0.72f, 0.88f) : new Color(0.75f, 0.95f, 1f, 0.92f);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(rect, "Top Crystal", new Vector2(0.5f, 1f), new Vector2(34f, 34f), tint);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(rect, "Left Crystal", new Vector2(0f, 0.5f), new Vector2(22f, 22f), tint);
		AccardND.Battlefield.MmoUiTheme.AddPanelGem(rect, "Right Crystal", new Vector2(1f, 0.5f), new Vector2(22f, 22f), tint);
	}

	private static string ResolvePlayerHudDisplayName()
	{
		string nickname = PlayerPrefs.GetString(PlayerHudNamePrefsKey, string.Empty);
		nickname = SanitizePlayerHudDisplayName(nickname);
		return string.IsNullOrWhiteSpace(nickname) ? "Guest" : nickname;
	}

	private int EffectivePlayerHudVigorDieSides()
	{
		if (runProgress == null)
		{
			return 0;
		}
		return nextRoomEmpowered ? RaiseVigorDie(runProgress.PlayerVigorDieSides) : runProgress.PlayerVigorDieSides;
	}

	private static string SanitizePlayerHudDisplayName(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return string.Empty;
		string value = raw.Trim();
		const string editorSuffix = "-editor";
		if (Application.isEditor && value.EndsWith(editorSuffix, StringComparison.Ordinal))
			value = value.Substring(0, value.Length - editorSuffix.Length);
		return value;
	}

	private void RefreshCpuHud()
	{
		if (cpuHud == null || runProgress == null)
		{
			return;
		}

		string encounterLabel = CurrentEncounterHudLabel();
		string campaignScenarioLabel = CurrentCampaignScenarioHudLabel();
		string scenarioLabel = CurrentScenarioHudLabel();
		string progressLabel = !string.IsNullOrWhiteSpace(campaignScenarioLabel)
			?$"GROTTA: {campaignScenarioLabel}"
			:(string.IsNullOrWhiteSpace(scenarioLabel) || SameHudLabel(scenarioLabel, encounterLabel)
				?"AREA: GROTTA"
				:$"AREA: {scenarioLabel}");

		RefreshCombatantHud(
			cpuHud,
			isPlayer: false,
			$"STANZA {runProgress.RoomsCleared + 1}",
			$"MINACCIA: {encounterLabel}",
			progressLabel,
			0f,
			EffectiveCpuHudVigorDieSides(),
			0,
			0,
			0);
		SetCpuCounterStatsVisible(false);
		SetCpuProgressBarVisible(false);
		ConfigureCpuHudPresentation(cpuHud);
	}

	private string CurrentCpuProgressTooltip()
	{
		if (activeComposableGolem != null && !activeComposableGolem.IsDefeated)
		{
			ComposableGolemFormStats activeForm = activeComposableGolem.ActiveForm;
			return $"Golem {GolemFormName(activeForm.Form)}: dado Vigore attuale D{activeForm.VigorDieSides}. Cambia forma e dado durante il combattimento.";
		}

		if (!string.IsNullOrWhiteSpace(campaignScenarioId))
		{
			string label = CurrentCampaignScenarioHudLabel();
			ScenarioDefinition scenario = null;
			if ((Object)(object)scenarioCatalog != (Object)null)
			{
				scenario = scenarioCatalog.FindById(campaignScenarioId);
			}
			string bossId = !string.IsNullOrWhiteSpace(campaignScenarioBossId) ?campaignScenarioBossId : scenario?.BossId;
			CardDefinition boss = FindCardDefinition(bossId);
			string bossName = (Object)(object)boss != (Object)null ?boss.DisplayName : bossId;
			if (!string.IsNullOrWhiteSpace(bossName))
			{
				return $"Grotta {label}: nei combattimenti Boss della campagna il Master evoca {bossName}. Resta attiva fino a fine campagna.";
			}
			return $"Grotta {label}: effetto scenario attivo fino a fine campagna.";
		}

		string scenarioLabel = CurrentScenarioHudLabel();
		if (!string.IsNullOrWhiteSpace(scenarioLabel))
		{
			return $"Area {scenarioLabel}: questa stanza usa sfondo e regole dello scenario corrente.";
		}
		return "Area Grotta: stanza base senza effetto scenario speciale.";
	}

	private string CurrentEncounterHudLabel()
	{
		return currentRoomType switch
		{
			RoomType.Boss => "BOSS",
			RoomType.Merchant => "MERCANTE",
			RoomType.Loot => "TESORO",
			RoomType.UnexpectedOpportunity => "IMPREVISTO",
			_ => $"MOSTRO {currentMonsterTier}"
		};
	}

	private string CurrentCampaignScenarioHudLabel()
	{
		if (string.IsNullOrWhiteSpace(campaignScenarioId))
		{
			return string.Empty;
		}
		return FormatHudLabel(ActiveCampaignScenarioLabel());
	}

	private string CurrentScenarioHudLabel()
	{
		if (!string.IsNullOrWhiteSpace(currentScenarioDisplayOverride))
		{
			return FormatHudLabel(currentScenarioDisplayOverride);
		}
		if ((Object)(object)currentScenario != (Object)null && !string.IsNullOrWhiteSpace(currentScenario.DisplayName))
		{
			return FormatHudLabel(currentScenario.DisplayName);
		}
		return string.Empty;
	}

	private static string FormatHudLabel(string label)
	{
		if (string.IsNullOrWhiteSpace(label))
		{
			return string.Empty;
		}
		string value = label.Trim().Replace("_", " ").Replace("-", " ").ToUpperInvariant();
		value = System.Text.RegularExpressions.Regex.Replace(value, "([A-ZÀ-Ü]+)(\\d+)", "$1 $2");
		return System.Text.RegularExpressions.Regex.Replace(value, "\\s+", " ");
	}

	private static bool SameHudLabel(string left, string right)
	{
		return NormalizeHudLabel(left) == NormalizeHudLabel(right);
	}

	private static string NormalizeHudLabel(string label)
	{
		return string.IsNullOrWhiteSpace(label)
			?string.Empty
			:new string(FormatHudLabel(label).Where(char.IsLetterOrDigit).ToArray());
	}

	private static void RefreshCombatantHud(
		CombatantHud hud,
		bool isPlayer,
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
		hud.DiceImage.sprite = LoadHudDiceSprite(diceSides, isPlayer);
		hud.DiceImage.enabled = (Object)(object)hud.DiceImage.sprite != (Object)null;
		hud.DiceText.text = $"D{diceSides}";
		hud.DeckText.text = deckCount.ToString();
		hud.CooldownText.text = cooldownCount.ToString();
		hud.GraveyardText.text = graveyardCount.ToString();
	}

	private void SetCpuCounterStatsVisible(bool visible)
	{
		SetHudStatVisible(cpuHud?.DeckImage, cpuHud?.DeckText, visible);
		SetHudStatVisible(cpuHud?.CooldownImage, cpuHud?.CooldownText, visible);
		SetHudStatVisible(cpuHud?.GraveyardImage, cpuHud?.GraveyardText, visible);
	}

	private void SetCpuProgressBarVisible(bool visible)
	{
		SetHudImageVisible(cpuHud?.ExperienceTrack, visible);
		SetHudRectVisible(cpuHud?.ExperienceFillMask, visible);
		SetHudImageVisible(cpuHud?.ExperienceFill, visible);
	}

	private static void SetHudStatVisible(Image image, Text text, bool visible)
	{
		if ((Object)(object)image != (Object)null)
		{
			((Component)image).gameObject.SetActive(visible);
		}
		if ((Object)(object)text != (Object)null)
		{
			((Component)text).gameObject.SetActive(visible);
		}
	}

	private static void SetHudImageVisible(Image image, bool visible)
	{
		if ((Object)(object)image != (Object)null)
		{
			((Component)image).gameObject.SetActive(visible);
		}
	}

	private static void SetHudButtonVisible(Button button, bool visible)
	{
		if ((Object)(object)button != (Object)null)
		{
			((Component)button).gameObject.SetActive(visible);
		}
	}

	private static void SetHudRectVisible(RectTransform rect, bool visible)
	{
		if ((Object)(object)rect != (Object)null)
		{
			((Component)rect).gameObject.SetActive(visible);
		}
	}

	private void RefreshRoomHud(string phaseLabel, string scenarioLabel)
	{
		if ((Object)(object)topInfoText == (Object)null || runProgress == null)
		{
			return;
		}

		string encounterLabel = CurrentEncounterHudLabel();
		string roundLabel = roundNumber > 0 && !draftActive && !deploymentDraftActive ?$"ROUND {roundNumber}" : phaseLabel;
		string scenario = string.IsNullOrWhiteSpace(scenarioLabel) || SameHudLabel(scenarioLabel, encounterLabel) ?string.Empty : $"  |  {FormatHudLabel(scenarioLabel)}";
		topInfoText.text = $"STANZA {runProgress.RoomsCleared + 1}  |  {encounterLabel}  |  CPU D{EffectiveCpuHudVigorDieSides()}  |  {roundLabel}{scenario}";
	}

	private int EffectiveCpuHudVigorDieSides()
	{
		if (activeComposableGolem != null && !activeComposableGolem.IsDefeated)
		{
			return activeComposableGolem.ActiveForm.VigorDieSides;
		}
		return runProgress != null ?runProgress.MasterVigorDieSides : configuration.Gameplay.VigorDieSides;
	}

	// Icona del dado Vigore da Resources/UI: variante blu per il player,
	// rossa per il Master/avversario (D4..D20; il D3 logico usa il D6).
	private static Sprite LoadHudDiceSprite(int sides, bool isPlayer)
	{
		int visualSides = sides == 3 ? 6 : sides;
		return Resources.Load<Sprite>($"UI/D{visualSides}_{(isPlayer ? "Player" : "Cpu")}");
	}
}
}
