using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
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
	private string FormatResult(string actor, BattleCardState attacker, BattleCardState defender, CombatResult result, CombatModifiers modifiers)
	{
		string text = (result.DefenderIsDefeated ?"eliminata" : "resiste");
		return actor + ": " + attacker.Card.Name + " [" + FormatCombatTotal(attacker.Card.Strength, modifiers.AttackerFlatBonus, result.AttackerRoll, result.AttackerTotal) + "] contro " + defender.Card.Name + " [" + FormatCombatTotal(defender.Card.Strength, modifiers.DefenderFlatBonus, result.DefenderRoll, result.DefenderTotal) + "] - " + text + ".";
	}

	private string FormatCombatTotal(int strength, int flatBonus, VigorRollResult roll, int total)
	{
		string text = ((flatBonus > 0) ?$" + {flatBonus}" : string.Empty);
		return $"{strength}{text} + {FormatVigorRoll(roll)} = {total}";
	}

	private static string FormatImpossibleAttack(BattleCardState attacker, BattleCardState defender, int attackerDieSides, int defenderDieSides, CombatModifiers modifiers)
	{
		int num = (modifiers.SumAttackerVigor ?(attackerDieSides * 2) : attackerDieSides);
		int num2 = attacker.Card.Strength + num + modifiers.AttackerFlatBonus;
		int num3 = defender.Card.Strength + 1 + modifiers.DefenderFlatBonus;
		string text = (modifiers.SumAttackerVigor ?$"2D{attackerDieSides}" : $"D{attackerDieSides}");
		string text2 = ((modifiers.AttackerFlatBonus > 0) ?$" + {modifiers.AttackerFlatBonus}" : string.Empty);
		string text3 = ((modifiers.DefenderFlatBonus > 0) ?$" + {modifiers.DefenderFlatBonus}" : string.Empty);
		return $"0%: {attacker.Card.Name} arriva al massimo a {num2} " + $"({attacker.Card.Strength}{text2} + {text}), mentre {defender.Card.Name} parte da " + $"{num3} ({defender.Card.Strength}{text3} + D{defenderDieSides}:1).";
	}

	private string FormatResultDetailed(string actor, BattleCardState attacker, BattleCardState defender, CombatResult result, CombatModifiers modifiers)
	{
		string text = (result.DefenderIsDefeated ?"eliminata" : "resiste");
		return actor + ": " + attacker.Card.Name + " [" + FormatCombatTotalDetailed(attacker, modifiers.AttackerFlatBonus, result.AttackerRoll, result.AttackerTotal, true, defender) + "] contro " + defender.Card.Name + " [" + FormatCombatTotalDetailed(defender, modifiers.DefenderFlatBonus, result.DefenderRoll, result.DefenderTotal, false, attacker) + "] - " + text + ".";
	}

	private string FormatCombatTotalDetailed(BattleCardState card, int flatBonus, VigorRollResult roll, int total, bool attacking, BattleCardState opponent)
	{
		string text = FormatFlatBonus(flatBonus, FormatFlatBonusDetails(card, flatBonus, attacking, opponent));
		return $"{card.Card.Strength}{text} + {FormatVigorRoll(roll)} = {total}";
	}

	private string FormatImpossibleAttackDetailed(BattleCardState attacker, BattleCardState defender, int attackerDieSides, int defenderDieSides, CombatModifiers modifiers)
	{
		int maximumVigor = modifiers.SumAttackerVigor ?attackerDieSides * 2 : attackerDieSides;
		int attackerMaximum = attacker.Card.Strength + maximumVigor + modifiers.AttackerFlatBonus;
		int defenderMinimum = defender.Card.Strength + 1 + modifiers.DefenderFlatBonus;
		string attackerDie = modifiers.SumAttackerVigor ?$"2D{attackerDieSides}" : $"D{attackerDieSides}";
		string attackerBonus = FormatFlatBonus(modifiers.AttackerFlatBonus, FormatFlatBonusDetails(attacker, modifiers.AttackerFlatBonus, true, defender));
		string defenderBonus = FormatFlatBonus(modifiers.DefenderFlatBonus, FormatFlatBonusDetails(defender, modifiers.DefenderFlatBonus, false, attacker));
		return $"0%: {attacker.Card.Name} arriva al massimo a {attackerMaximum} " + $"({attacker.Card.Strength}{attackerBonus} + {attackerDie}), mentre {defender.Card.Name} parte da " + $"{defenderMinimum} ({defender.Card.Strength}{defenderBonus} + D{defenderDieSides}:1).";
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
			if (card.BelongsToPlayer && playerAura == BattleAuraType.Warrior && card.Card.HeroClass == HeroClass.Warrior && card.AbilityArmed)
			{
				parts.Add("Aura Warrior +1");
				described++;
			}
			int hunterBonus = HunterMarkAttackBonus(card, opponent);
			if (hunterBonus > 0)
			{
				parts.Add($"Preda Hunter +{hunterBonus}");
				described += hunterBonus;
			}
		}
		else if (card.PendingAttackBonusKind == PendingAttackBonusKind.Fury && card.PendingAttackBonus > 0)
		{
			parts.Add($"Furia +{card.PendingAttackBonus}");
			described += card.PendingAttackBonus;
		}
		if (card.PermanentCombatBonus != 0)
		{
			parts.Add(FormatSignedBonus("Permanente", card.PermanentCombatBonus));
			described += card.PermanentCombatBonus;
		}
		if (described != flatBonus)
		{
			parts.Add(FormatSignedBonus("Altro", flatBonus - described));
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
			PendingAttackBonusKind.Fury => "Furia",
			PendingAttackBonusKind.Blessing => "Benedizione",
			_ => "Bonus",
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
			VigorSelectionMode.Highest => "max", 
			VigorSelectionMode.Lowest => "min", 
			VigorSelectionMode.Sum => "somma", 
			_ => "risultato", 
		};
		return $"{text}[{roll.FirstRoll},{roll.SecondRoll}] {text2}:{roll.SelectedRoll}";
	}

	private void SetMessage(string message)
	{
		if ((Object)(object)messageText != (Object)null)
		{
			messageText.text = message;
			UpdateMessageTextLayout();
		}
		AppendLog(message);
	}

	private void UpdateMessageTextLayout()
	{
		if ((Object)(object)messageText == (Object)null)
		{
			return;
		}
		bool flag = IsActionButtonVisible(restartButton) || IsActionButtonVisible(confirmFormationButton) || IsActionButtonVisible(cancelActionButton) || IsActionButtonVisible(abilityButton) || IsActionButtonVisible(attachmentButton) || IsActionButtonVisible(merchantBuyButton);
		bool flag2 = IsMerchantActionHudVisible();
		bool flag3 = IsSingleActionNonCombatHudVisible();
		if (deploymentDraftActive)
		{
			messageText.alignment = (TextAnchor)4;
			SetRect(messageText.rectTransform, flag ?new Vector2(0.06f, 0.06f) : new Vector2(0.06f, 0.06f), flag ?new Vector2(0.62f, 0.66f) : new Vector2(0.94f, 0.66f));
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
		return !((Object)(object)button == (Object)null) && ((Component)button).gameObject.activeInHierarchy;
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

	private void SetTurnBanner(bool playerTurn, string label)
	{
		if ((Object)(object)turnBannerImage != (Object)null)
		{
			Color val = (playerTurn ?configuration.Visual.PlayerTurnColor : configuration.Visual.CpuTurnColor);
			val.a = Mathf.Min(val.a, 0.78f);
			turnBannerImage.color = val;
		}
		if ((Object)(object)turnBannerText != (Object)null)
		{
			turnBannerText.text = label;
			UpdateMessageTextLayout();
		}
	}

	private void ConfigureActionButtonLayout(bool merchantVisible)
	{
		if (!((Object)(object)restartButton == (Object)null) && !((Object)(object)merchantBuyButton == (Object)null))
		{
			bool singleNonCombat = !merchantVisible && IsSingleActionNonCombatHudVisible();
			SetRect((RectTransform)((Component)restartButton).transform, merchantVisible ?new Vector2(0.53f, 0.06f) : (singleNonCombat ?new Vector2(0.325f, 0.06f) : new Vector2(0.69f, 0.16f)), merchantVisible ?new Vector2(0.88f, 0.27f) : (singleNonCombat ?new Vector2(0.675f, 0.27f) : new Vector2(0.97f, 0.84f)));
			SetRect((RectTransform)((Component)merchantBuyButton).transform, merchantVisible ?new Vector2(0.12f, 0.06f) : new Vector2(0.69f, 0.54f), merchantVisible ?new Vector2(0.47f, 0.27f) : new Vector2(0.97f, 0.92f));
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
			string text2 = $"{text}[STANZA {num} / ROUND {roundNumber}] {message}";
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
			int num = Mathf.Max(1, configuration.Logging.VisibleEntries);
			int count = Mathf.Max(0, gameLogEntries.Count - num);
			logText.text = string.Join("\n", gameLogEntries.Skip(count));
		}
	}

	private void ToggleLogPanel()
	{
		if (!((Object)(object)logPanel == (Object)null))
		{
			bool flag = !logPanel.activeSelf;
			logPanel.SetActive(flag);
			if (flag && (Object)(object)optionsPanel != (Object)null)
			{
				optionsPanel.SetActive(false);
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
		optionsPanel.SetActive(show);
		if (show)
		{
			if ((Object)(object)logPanel != (Object)null)
			{
				logPanel.SetActive(false);
			}
			RefreshSfxOptionsUi();
		}
	}

	private void OpenLogFromOptions()
	{
		if ((Object)(object)optionsPanel != (Object)null)
		{
			optionsPanel.SetActive(false);
		}
		if ((Object)(object)logPanel != (Object)null)
		{
			logPanel.SetActive(true);
			RefreshLogText();
		}
	}
}
}
