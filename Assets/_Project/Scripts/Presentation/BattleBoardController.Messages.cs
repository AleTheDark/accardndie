using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private bool campaignEndedBannerVisible;

	private string FormatResultDetailed(string actor, BattleCardState attacker, BattleCardState defender, CombatResult result, CombatModifiers modifiers)
	{
		string outcome = GameText.Get(result.DefenderIsDefeated
			? GameTextKeys.Combat.OutcomeEliminated
			: GameTextKeys.Combat.OutcomeResists);
		return GameText.Format(
			GameTextKeys.Combat.ResultDetailed,
			actor,
			attacker.Card.Name,
			FormatCombatTotalDetailed(attacker, modifiers.AttackerFlatBonus, result.AttackerRoll, result.AttackerTotal, true, defender),
			defender.Card.Name,
			FormatCombatTotalDetailed(defender, modifiers.DefenderFlatBonus, result.DefenderRoll, result.DefenderTotal, false, attacker),
			outcome);
	}

	private static string FormatResultSummary(BattleCardState attacker, BattleCardState defender, CombatResult result)
	{
		return result.DefenderIsDefeated
			? GameText.Format(GameTextKeys.Combat.ResultEliminates, attacker.Card.Name, defender.Card.Name)
			: GameText.Format(GameTextKeys.Combat.ResultResists, defender.Card.Name, attacker.Card.Name);
	}

	private string FormatCombatTotalDetailed(BattleCardState card, int flatBonus, VigorRollResult roll, int total, bool attacking, BattleCardState opponent)
	{
		string text = FormatFlatBonus(flatBonus, FormatFlatBonusDetails(card, flatBonus, attacking, opponent));
		return $"{card.Card.Strength}{text} + {FormatVigorRoll(roll)} = {total}";
	}

	private string FormatImpossibleAttackDetailed(BattleCardState attacker, BattleCardState defender, int attackerDieSides, int defenderDieSides, CombatModifiers modifiers)
	{
		int lowerDieSides = AccardND.GameCore.Pvp.PvpVigorScale.Lower(attackerDieSides);
		int maximumVigor = modifiers.SumAttackerVigor ?attackerDieSides + lowerDieSides : attackerDieSides;
		int attackerMaximum = attacker.Card.Strength + maximumVigor + modifiers.AttackerFlatBonus;
		int defenderMinimum = defender.Card.Strength + 1 + modifiers.DefenderFlatBonus;
		string attackerDie = modifiers.SumAttackerVigor ?$"D{attackerDieSides}+D{lowerDieSides}" : $"D{attackerDieSides}";
		string attackerBonus = FormatFlatBonus(modifiers.AttackerFlatBonus, FormatFlatBonusDetails(attacker, modifiers.AttackerFlatBonus, true, defender));
		string defenderBonus = FormatFlatBonus(modifiers.DefenderFlatBonus, FormatFlatBonusDetails(defender, modifiers.DefenderFlatBonus, false, attacker));
		return GameText.Format(
			GameTextKeys.Combat.ImpossibleAttackDetailed,
			attacker.Card.Name,
			attackerMaximum,
			attacker.Card.Strength,
			attackerBonus,
			attackerDie,
			defender.Card.Name,
			defenderMinimum,
			defender.Card.Strength,
			defenderBonus,
			defenderDieSides);
	}

	private string FormatFlatBonusDetails(BattleCardState card, int flatBonus, bool attacking, BattleCardState opponent)
	{
		if (card == null || flatBonus == 0)
		{
			return string.Empty;
		}
		List<string> parts = new List<string>();
		int described = 0;
		if (attacking)
		{
			if (card.PendingAttackBonus > 0)
			{
				parts.Add($"{PendingAttackBonusSource(card)} +{card.PendingAttackBonus}");
				described += card.PendingAttackBonus;
			}
			if (AuraFor(card) == BattleAuraType.Warrior
				&& card.Card.HeroClass == HeroClass.Warrior
				&& opponent != null
				&& card.Card.Strength < opponent.Card.Strength)
			{
				parts.Add(GameText.Get(GameTextKeys.Combat.BonusAuraWarrior));
				described += 2;
			}
			int hunterBonus = HunterMarkAttackBonus(card, opponent);
			if (hunterBonus > 0)
			{
				parts.Add(GameText.Format(GameTextKeys.Combat.BonusHunterPrey, hunterBonus));
				described += hunterBonus;
			}
		}
		else if (card.PendingAttackBonusKind == PendingAttackBonusKind.Fury && card.PendingAttackBonus > 0)
		{
			parts.Add(GameText.Format(GameTextKeys.Combat.BonusFury, card.PendingAttackBonus));
			described += card.PendingAttackBonus;
		}
		if (!attacking
			&& AuraFor(card) == BattleAuraType.Warrior
			&& card.Card.HeroClass == HeroClass.Warrior
			&& opponent != null
			&& card.Card.Strength < opponent.Card.Strength)
		{
			parts.Add(GameText.Get(GameTextKeys.Combat.BonusAuraWarrior));
			described += 2;
		}
		if (card.PermanentCombatBonus != 0)
		{
			parts.Add(FormatSignedBonus(GameText.Get(GameTextKeys.Combat.BonusEquipmentMalus), card.PermanentCombatBonus));
			described += card.PermanentCombatBonus;
		}
		if (card.MightAuraCombatBonus != 0)
		{
			parts.Add(FormatSignedBonus(GameText.Get(GameTextKeys.Combat.BonusMightAura), card.MightAuraCombatBonus));
			described += card.MightAuraCombatBonus;
		}
		if (described != flatBonus)
		{
			parts.Add(FormatSignedBonus(GameText.Get(GameTextKeys.Combat.BonusOther), flatBonus - described));
		}
		return parts.Count > 0 ?string.Join(", ", parts) : string.Empty;
	}

	private static string FormatFlatBonus(int flatBonus, string details)
	{
		if (flatBonus == 0)
		{
			return string.Empty;
		}
		string value = flatBonus > 0 ?$" + {flatBonus}" : $" - {Math.Abs(flatBonus)}";
		return string.IsNullOrEmpty(details) ?value : $"{value} ({details})";
	}

	private static string FormatSignedBonus(string label, int value)
	{
		return value >= 0 ?$"{label} +{value}" : $"{label} {value}";
	}

	private static string PendingAttackBonusSource(BattleCardState card)
	{
		return card.PendingAttackBonusKind switch
		{
			PendingAttackBonusKind.Fury => GameText.Get(GameTextKeys.Combat.BonusSourceFury),
			PendingAttackBonusKind.Blessing => GameText.Get(GameTextKeys.Combat.BonusSourceBlessing),
			_ => GameText.Get(GameTextKeys.Combat.BonusSourceGeneric),
		};
	}

	private string FormatVigorRoll(VigorRollResult roll)
	{
		string text = $"D{roll.DieSides}";
		if (!roll.HasSecondRoll)
		{
			return $"{text}:{roll.FirstRoll}";
		}
		string text2 = roll.SelectionMode switch
		{
			VigorSelectionMode.Highest => GameText.Get(GameTextKeys.PvpLog.RollHighest),
			VigorSelectionMode.Lowest => GameText.Get(GameTextKeys.PvpLog.RollLowest),
			VigorSelectionMode.Sum => GameText.Get(GameTextKeys.PvpLog.RollSum),
			_ => GameText.Get(GameTextKeys.PvpLog.RollResult),
		};
		return $"{text}[{roll.FirstRoll},{roll.SecondRoll}] {text2}:{roll.SelectedRoll}";
	}

	private void SetMessage(string message)
	{
		SetBattlefieldMessage(message);
		AppendLog(message);
	}

	private void SetBattlefieldMessage(string message)
	{
		if ((Object)(object)messageText != (Object)null)
		{
			messageText.text = message;
			UpdateMessageTextLayout();
			HideNormalMessagePanelDuringAdventureTutorial();
		}
	}

	private void HideNormalMessagePanelDuringAdventureTutorial()
	{
		if (!adventureScriptedTutorialActive || (Object)(object)messagePanelRect == (Object)null)
		{
			return;
		}
		((Component)messagePanelRect).gameObject.SetActive(false);
	}

	private void UpdateMessageTextLayout()
	{
		RefreshMessagePanelVisibility();
		if ((Object)(object)messageText == (Object)null)
		{
			return;
		}
		if (IsCampaignEndedBannerVisible())
		{
			messageText.alignment = (TextAnchor)4;
			SetRect(messageText.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.58f));
			if ((Object)(object)turnBannerImage != (Object)null)
			{
				SetRect(turnBannerImage.rectTransform, new Vector2(0.18f, 0.66f), new Vector2(0.82f, 0.96f));
			}
			if ((Object)(object)messagePanelRect != (Object)null)
			{
				SetCenteredCampaignEndedMessagePanel();
			}
			return;
		}
		bool flag = IsActionButtonVisible(restartButton) || IsActionButtonVisible(confirmActionButton) || IsActionButtonVisible(cancelActionButton) || IsActionButtonVisible(abilityButton) || IsActionButtonVisible(attachmentButton) || IsActionButtonVisible(merchantBuyButton);
		bool flag2 = IsMerchantActionHudVisible();
		bool flag3 = IsSingleActionNonCombatHudVisible();
		if (deploymentDraftActive)
		{
			messageText.alignment = (TextAnchor)4;
			SetRect(messageText.rectTransform, flag ?new Vector2(0.06f, 0.06f) : new Vector2(0.04f, 0.06f), flag ?new Vector2(0.62f, 0.66f) : new Vector2(0.88f, 0.66f));
			if ((Object)(object)turnBannerImage != (Object)null)
			{
				SetRect(turnBannerImage.rectTransform, new Vector2(0.08f, 0.69f), new Vector2(0.92f, 0.98f));
			}
			return;
		}
		messageText.alignment = (TextAnchor)4;
		SetRect(messageText.rectTransform, (flag2 || flag3) ?new Vector2(0.08f, 0.31f) : (flag ?new Vector2(0.035f, 0.06f) : new Vector2(0.06f, 0.06f)), (flag2 || flag3) ?new Vector2(0.92f, 0.66f) : (flag ?new Vector2(0.65f, 0.66f) : new Vector2(0.94f, 0.66f)));
		if ((Object)(object)turnBannerImage != (Object)null)
		{
			SetRect(turnBannerImage.rectTransform, (flag2 || flag3) ?new Vector2(0.12f, 0.72f) : new Vector2(0.1825f, 0.69f), (flag2 || flag3) ?new Vector2(0.88f, 0.98f) : new Vector2(0.8175f, 0.98f));
		}
	}

	private static bool IsActionButtonVisible(Button button)
	{
		return !((Object)(object)button == (Object)null) && ((Component)button).gameObject.activeSelf;
	}

	private void RefreshMessagePanelVisibility()
	{
		if (adventureScriptedTutorialActive || (Object)(object)messagePanelRect == (Object)null)
		{
			return;
		}

		bool hasVisibleAction = IsActionButtonVisible(restartButton)
			|| IsActionButtonVisible(confirmActionButton)
			|| IsActionButtonVisible(cancelActionButton)
			|| IsActionButtonVisible(abilityButton)
			|| IsActionButtonVisible(attachmentButton)
			|| IsActionButtonVisible(merchantBuyButton);
		GameObject panel = ((Component)messagePanelRect).gameObject;
		bool shouldBeVisible = hasVisibleAction && !messagePanelHiddenForDuel;
		if (panel.activeSelf != shouldBeVisible)
		{
			panel.SetActive(shouldBeVisible);
		}
	}

	private bool IsMerchantActionHudVisible()
	{
		return currentRoomType == RoomType.Merchant && IsActionButtonVisible(restartButton) && IsActionButtonVisible(merchantBuyButton);
	}

	private bool IsSingleActionNonCombatHudVisible()
	{
		return (currentRoomType == RoomType.Loot || currentRoomType == RoomType.UnexpectedOpportunity)
			&& IsActionButtonVisible(restartButton)
			&& !IsActionButtonVisible(merchantBuyButton);
	}

	private void SetTurnBanner(bool playerTurn, string label, bool defeat = false, bool campaignEnded = false)
	{
		campaignEndedBannerVisible = campaignEnded;
		SetTurnCoinState(playerTurn, !defeat && !campaignEnded);
		if ((Object)(object)turnBannerImage != (Object)null)
		{
			Color val = (playerTurn ?configuration.Visual.PlayerTurnColor : configuration.Visual.CpuTurnColor);
			if (!playerTurn && defeat)
			{
				val = Color.Lerp(val, Color.black, 0.35f);
			}
			val.a = Mathf.Min(val.a, 0.78f);
			turnBannerImage.color = val;
		}
		if ((Object)(object)turnBannerText != (Object)null)
		{
			turnBannerText.text = label;
			UpdateMessageTextLayout();
		}
	}

	private bool IsCampaignEndedBannerVisible()
	{
		return gameFinished && campaignEndedBannerVisible;
	}

	private void SetCenteredCampaignEndedMessagePanel()
	{
		float aspect = Mathf.Max(1f, Screen.safeArea.width) / Mathf.Max(1f, Screen.safeArea.height);
		bool compact = IsCompactLayout(aspect, configuration.ResponsiveLayout);
		SetRect(messagePanelRect, compact ?new Vector2(0.08f, 0.42f) : new Vector2(0.25f, 0.405f), compact ?new Vector2(0.92f, 0.58f) : new Vector2(0.75f, 0.565f));
	}

	private void ConfigureActionButtonLayout(bool merchantVisible)
	{
		if (!((Object)(object)restartButton == (Object)null) && !((Object)(object)merchantBuyButton == (Object)null))
		{
			bool singleNonCombat = !merchantVisible && IsSingleActionNonCombatHudVisible();
			SetRect((RectTransform)((Component)restartButton).transform, merchantVisible ?new Vector2(0.51f, 0.025f) : (singleNonCombat ?new Vector2(0.325f, 0.06f) : new Vector2(0.69f, 0.14f)), merchantVisible ?new Vector2(0.965f, 0.405f) : (singleNonCombat ?new Vector2(0.675f, 0.27f) : new Vector2(0.97f, 0.58f)));
			SetRect((RectTransform)((Component)merchantBuyButton).transform, merchantVisible ?new Vector2(0.035f, 0.025f) : new Vector2(0.69f, 0.54f), merchantVisible ?new Vector2(0.49f, 0.405f) : new Vector2(0.97f, 0.92f));
			if ((Object)(object)merchantOpenButtonPulseVfx != (Object)null)
			{
				((Component)merchantOpenButtonPulseVfx).gameObject.SetActive(merchantVisible);
			}
			if ((Object)(object)merchantContinueButtonPulseVfx != (Object)null)
			{
				((Component)merchantContinueButtonPulseVfx).gameObject.SetActive(merchantVisible);
			}
		}
		UpdateMessageTextLayout();
	}

	private void AppendLog(string message)
	{
		if (!string.IsNullOrWhiteSpace(message))
		{
			LoggingConfiguration logging = configuration.Logging;
			string text = (logging.IncludeTimestamp ?$"[{DateTime.Now:HH:mm:ss}] " : string.Empty);
			int num = ((runProgress != null) ?(runProgress.RoomsCleared + 1) : 0);
			string text2 = text + GameText.Format(GameTextKeys.GameLog.EntryContext, num, roundNumber, message);
			gameLogEntries.Add(text2);
			int num2 = Mathf.Max(10, logging.MaximumEntries);
			if (gameLogEntries.Count > num2)
			{
				gameLogEntries.RemoveRange(0, gameLogEntries.Count - num2);
			}
			RefreshLogText();
			if (logging.EchoToUnityConsole)
			{
				Debug.Log((object)("[Accard N' Die] " + text2));
			}
		}
	}

	private void RefreshLogText()
	{
		if (!((Object)(object)logText == (Object)null))
		{
			ConfigureLogTextRect();
			int num = Mathf.Max(configuration.Logging.VisibleEntries, EstimateVisibleLogEntries());
			int count = Mathf.Max(0, gameLogEntries.Count - num);
			logText.text = string.Join("\n", gameLogEntries.Skip(count));
		}
	}

	private void ConfigureLogTextRect()
	{
		if ((Object)(object)logText == (Object)null)
		{
			return;
		}

		logText.horizontalOverflow = HorizontalWrapMode.Wrap;
		logText.verticalOverflow = VerticalWrapMode.Truncate;
		SetRect(logText.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.895f));
	}

	private int EstimateVisibleLogEntries()
	{
		if ((Object)(object)logText == (Object)null)
		{
			return Mathf.Max(1, configuration.Logging.VisibleEntries);
		}

		float height = logText.rectTransform.rect.height;
		if (height <= 1f && (Object)(object)logPanel != (Object)null)
		{
			RectTransform panelRect = (RectTransform)logPanel.transform;
			height = panelRect.rect.height * 0.86f;
		}

		float lineHeight = Mathf.Max(1f, logText.fontSize * 1.12f);
		return Mathf.Max(1, Mathf.FloorToInt(height / lineHeight));
	}

	private void ToggleLogPanel()
	{
		if (!((Object)(object)logPanel == (Object)null))
		{
			bool flag = !logPanel.activeSelf;
			logPanel.SetActive(flag);
			if (flag && (Object)(object)optionsPanel != (Object)null)
			{
				CloseOptionsPanel();
			}
			if (flag)
			{
				RefreshLogText();
			}
		}
	}

	private void ToggleOptionsPanel()
	{
		if ((Object)(object)optionsPanel == (Object)null)
		{
			return;
		}
		bool show = !optionsPanel.activeSelf;
		SetOptionsPanelVisible(show);
		if (show)
		{
			if ((Object)(object)logPanel != (Object)null)
			{
				logPanel.SetActive(false);
			}
			RefreshSfxOptionsUi();
			RefreshMusicOptionsUi();
			RefreshLanguageOptionsUi();
			RefreshPrivacyOptionsButton();
		}
	}

	/// <summary>
	/// Il bottone delle opzioni privacy compare solo dove serve. Non e' una scelta estetica:
	/// dove il consenso e' stato raccolto Google pretende che il giocatore possa tornare
	/// sulle proprie scelte, e dove non e' mai stato chiesto (fuori dall'Europa, o senza
	/// pubblicita' del tutto) un bottone che apre un modulo vuoto e' solo confusione.
	/// </summary>
	private void RefreshPrivacyOptionsButton()
	{
		if ((Object)(object)optionsPrivacyButton == (Object)null)
			return;
		((Component)optionsPrivacyButton).gameObject.SetActive(AccardND.Ads.AdConsent.IsPrivacyOptionsRequired);
	}

	private async void ShowPrivacyOptions()
	{
		CloseOptionsPanel();
		await AccardND.Ads.AdConsent.ShowPrivacyOptionsAsync();
	}

	private void CloseOptionsPanel()
	{
		SetOptionsPanelVisible(false);
	}

	/// <summary>
	/// In arena non si "torna al menu": si molla la partita, e mollare una partita
	/// PvP è una resa. Stesso bottone, significato diverso.
	/// </summary>
	private void RefreshOptionsMainMenuButton()
	{
		if ((Object)(object)optionsMainMenuButton == (Object)null)
		{
			return;
		}
		bool surrender = IsPvpMatchInProgress;
		optionsMainMenuButton.interactable = surrender || HasActiveCampaignSession();
		if ((Object)(object)optionsMainMenuButtonText != (Object)null)
		{
			optionsMainMenuButtonText.text = surrender
				? GameText.GetLocalizedFallback(GameTextKeys.Options.Surrender, "ARRENDITI", "SURRENDER")
				: GameText.GetLocalizedFallback(GameTextKeys.Options.MainMenu, "MENU", "MENU");
		}
	}

	private void SetOptionsPanelVisible(bool visible)
	{
		if (visible)
		{
			RefreshOptionsMainMenuButton();
		}
		else
		{
			CloseLanguageDropdown();
		}
		if ((Object)(object)optionsBackdropPanel != (Object)null)
		{
			optionsBackdropPanel.SetActive(visible);
			if (visible)
			{
				optionsBackdropPanel.transform.SetAsLastSibling();
			}
		}
		if ((Object)(object)optionsPanel != (Object)null)
		{
			optionsPanel.SetActive(visible);
			if (visible)
			{
				optionsPanel.transform.SetAsLastSibling();
			}
		}
	}

	private bool HasActiveCampaignSession()
	{
		return campaignDeck != null
			|| initialDeckBuilder != null
			|| ((Object)(object)deckBuilderPanel != (Object)null && deckBuilderPanel.activeInHierarchy);
	}

	private void OpenLogFromOptions()
	{
		if ((Object)(object)optionsPanel != (Object)null)
		{
			CloseOptionsPanel();
		}
		if ((Object)(object)logPanel != (Object)null)
		{
			logPanel.SetActive(true);
			RefreshLogText();
		}
	}

	private void ReturnToMainMenuFromOptions()
	{
		ShowReturnToMenuConfirmation();
	}

	private void CreateReturnToMenuConfirmation(Transform parent, Font font)
	{
		Image overlay = CreateImage("Return To Menu Confirmation", parent, new Color(0f, 0f, 0f, 0.72f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		returnToMenuConfirmPanel = ((Component)overlay).gameObject;
		Canvas canvas = returnToMenuConfirmPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		// Sopra il canvas del PvP (950) e sopra le opzioni (981): è una domanda a cui
		// bisogna rispondere, e in arena la risposta costa la partita.
		canvas.sortingOrder = 990;
		returnToMenuConfirmPanel.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Return To Menu Dialog", ((Component)overlay).transform, new Color(0.01f, 0.018f, 0.028f, 0.98f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		SetRect(dialog.rectTransform, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.66f));

		Text title = CreateText("Return To Menu Title", ((Component)dialog).transform, AccardND.Battlefield.MmoUiTheme.LoreFont, 40, (FontStyle)0, (TextAnchor)4);
		title.text = GameText.Get(GameTextKeys.Options.ReturnToMenuTitle);
		title.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(title.rectTransform, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.9f));
		returnToMenuTitleText = title;

		Text body = CreateText("Return To Menu Body", ((Component)dialog).transform, font, 30, (FontStyle)0, (TextAnchor)4);
		body.text = GameText.Get(GameTextKeys.Options.ReturnToMenuBody);
		returnToMenuBodyText = body;
		body.color = new Color(0.86f, 0.92f, 0.94f);
		body.horizontalOverflow = HorizontalWrapMode.Wrap;
		body.verticalOverflow = VerticalWrapMode.Truncate;
		body.resizeTextForBestFit = false;
		SetRect(body.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.66f));

		Button cancelButton = CreateButton("Cancel Return To Menu", ((Component)dialog).transform, font, GameText.Get(GameTextKeys.Common.Cancel));
		((Component)cancelButton).GetComponentInChildren<Text>().fontSize = 25;
		((UnityEvent)cancelButton.onClick).AddListener(new UnityAction(HideReturnToMenuConfirmation));
		SetRect((RectTransform)((Component)cancelButton).transform, new Vector2(0.08f, 0.09f), new Vector2(0.46f, 0.28f));

		Button confirmButton = CreateButton("Confirm Return To Menu", ((Component)dialog).transform, font, GameText.Get(GameTextKeys.Common.Exit));
		((Component)confirmButton).GetComponentInChildren<Text>().fontSize = 25;
		((UnityEvent)confirmButton.onClick).AddListener(new UnityAction(ConfirmReturnToMainMenu));
		SetRect((RectTransform)((Component)confirmButton).transform, new Vector2(0.54f, 0.09f), new Vector2(0.92f, 0.28f));
		returnToMenuConfirmButtonText = ((Component)confirmButton).GetComponentInChildren<Text>();

		returnToMenuConfirmPanel.SetActive(false);
	}

	/// <summary>
	/// Stesso pannello, due domande diverse: in campagna si chiede se tornare al menu,
	/// in arena se arrendersi. La resa è irreversibile e regala la partita
	/// all'avversario: chiederlo a chiare lettere è il minimo.
	/// </summary>
	private void ApplyReturnToMenuConfirmationCopy(bool surrender)
	{
		returnToMenuConfirmIsSurrender = surrender;
		if ((Object)(object)returnToMenuTitleText != (Object)null)
		{
			returnToMenuTitleText.text = surrender
				? GameText.GetLocalizedFallback(GameTextKeys.Options.SurrenderTitle, "ARRENDERSI?", "SURRENDER?")
				: GameText.Get(GameTextKeys.Options.ReturnToMenuTitle);
		}
		if ((Object)(object)returnToMenuBodyText != (Object)null)
		{
			returnToMenuBodyText.text = surrender
				? GameText.GetLocalizedFallback(
					GameTextKeys.Options.SurrenderBody,
					"Ti arrendi: la partita è persa e la vittoria va al tuo avversario.",
					"You surrender: the match is lost and your opponent wins.")
				: GameText.Get(GameTextKeys.Options.ReturnToMenuBody);
		}
		if ((Object)(object)returnToMenuConfirmButtonText != (Object)null)
		{
			returnToMenuConfirmButtonText.text = surrender
				? GameText.GetLocalizedFallback(GameTextKeys.Options.Surrender, "ARRENDITI", "SURRENDER")
				: GameText.Get(GameTextKeys.Common.Exit);
		}
	}

	private void ShowReturnToMenuConfirmation()
	{
		bool surrender = IsPvpMatchInProgress;
		if ((Object)(object)returnToMenuConfirmPanel == (Object)null)
		{
			returnToMenuConfirmIsSurrender = surrender;
			ConfirmReturnToMainMenu();
			return;
		}
		ApplyReturnToMenuConfirmationCopy(surrender);
		if ((Object)(object)optionsPanel != (Object)null)
		{
			CloseOptionsPanel();
		}
		if ((Object)(object)logPanel != (Object)null)
		{
			logPanel.SetActive(false);
		}
		returnToMenuConfirmPanel.SetActive(true);
		returnToMenuConfirmPanel.transform.SetAsLastSibling();
	}

	private void HideReturnToMenuConfirmation()
	{
		if ((Object)(object)returnToMenuConfirmPanel != (Object)null)
		{
			returnToMenuConfirmPanel.SetActive(false);
		}
	}

	private void ConfirmReturnToMainMenu()
	{
		if (returnToMenuConfirmIsSurrender)
		{
			returnToMenuConfirmIsSurrender = false;
			CloseOptionsPanel();
			HideReturnToMenuConfirmation();
			SurrenderPvpMatch();
			return;
		}

		if ((Object)(object)optionsPanel != (Object)null)
		{
			CloseOptionsPanel();
		}
		if ((Object)(object)logPanel != (Object)null)
		{
			logPanel.SetActive(false);
		}
		if ((Object)(object)cardInspectionPanel != (Object)null)
		{
			cardInspectionPanel.SetActive(false);
		}
		if ((Object)(object)merchantPanel != (Object)null)
		{
			merchantPanel.SetActive(false);
		}
		if ((Object)(object)implementationArchivePanel != (Object)null)
		{
			SetImplementationArchiveVisible(false);
		}
		HideReturnToMenuConfirmation();
		if ((Object)(object)hintPanel != (Object)null)
		{
			hintPanel.SetActive(false);
		}
		if ((Object)(object)auraCodexPanel != (Object)null)
		{
			auraCodexPanel.SetActive(false);
		}
		pendingHints.Clear();
		hintActive = false;

		ReturnToStart();
	}
}
}
