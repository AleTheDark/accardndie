using AccardND.GameCore;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private enum TutorialRoguePracticeStep
	{
		Intro,
		AttackMage,
		AttackRogue,
		AttackWarrior,
		Done
	}

	private const string TutorialRoguePlayerId = "6-chimera-rogue";
	private const string TutorialRogueMageId = "7-whitealien-mage";
	private const string TutorialRogueNeutralId = "7-whitealien-rogue";
	private const string TutorialRogueWarriorId = "7-whitealien-warrior";

	private bool tutorialRoguePracticeActive;
	private bool tutorialRogueActionUnlocked;
	private TutorialRoguePracticeStep tutorialRoguePracticeStep;

	private void StartTutorialRoguePractice()
	{
		tutorialRoguePracticeActive = true;
		tutorialRogueActionUnlocked = false;
		tutorialRoguePracticeStep = TutorialRoguePracticeStep.Intro;
		adventureScriptedTutorialActive = true;
		SetCombatChromeVisible(visible: true);
		deploymentInitiativesReady = true;
		StartBattle();
		ShowTutorialRoguePracticeStep(
			TutorialClassText("rogue_practice_intro_title"),
			TutorialClassText("rogue_practice_intro_body"));
	}

	private bool AdvanceTutorialRoguePractice(AdventureTutorialAction action)
	{
		if (!tutorialRoguePracticeActive)
			return false;

		if (action == AdventureTutorialAction.AttackPressed)
		{
			// Nella pratica del Ladro il colore dell'aura del bersaglio e' la lezione:
			// lo spotlight dorato la coprirebbe e renderebbe indistinguibili vantaggio,
			// parita' e svantaggio. Dopo ATTACCA lasciamo visibili soltanto le aure reali.
			MoveAdventureTutorialSpotlight(null);
			return true;
		}

		if (action == AdventureTutorialAction.PlayerTurnStarted)
		{
			if (tutorialRoguePracticeStep == TutorialRoguePracticeStep.AttackMage)
			{
				tutorialRoguePracticeStep = TutorialRoguePracticeStep.AttackRogue;
				ShowTutorialRoguePracticeStep(
					TutorialClassText("rogue_practice_same_faction_title"),
					TutorialClassText("rogue_practice_same_faction_body"));
			}
			else if (tutorialRoguePracticeStep == TutorialRoguePracticeStep.AttackRogue)
			{
				tutorialRoguePracticeStep = TutorialRoguePracticeStep.AttackWarrior;
				ShowTutorialRoguePracticeStep(
					TutorialClassText("rogue_practice_disadvantage_title"),
					TutorialClassText("rogue_practice_disadvantage_body"));
			}
			return true;
		}

		if (action == AdventureTutorialAction.BattleFinished)
		{
			tutorialRoguePracticeStep = TutorialRoguePracticeStep.Done;
			ShowTutorialRoguePracticeStep(
				TutorialClassText("rogue_practice_complete_title"),
				TutorialClassText("rogue_practice_complete_body"));
			return true;
		}

		if (action == AdventureTutorialAction.NextPressed)
		{
			if (tutorialRoguePracticeStep == TutorialRoguePracticeStep.Intro)
				tutorialRoguePracticeStep = TutorialRoguePracticeStep.AttackMage;
			else if (tutorialRoguePracticeStep == TutorialRoguePracticeStep.Done)
			{
				EndTutorialRoguePractice();
				return true;
			}

			tutorialRogueActionUnlocked = true;
			adventureScriptedTutorialPanel.SetActive(false);
			RefreshCardActionOverlays();
			MoveAdventureTutorialSpotlight(ActivePlayerAttackActionRect());
			return true;
		}

		return true;
	}

	private void ShowTutorialRoguePracticeStep(string title, string body)
	{
		tutorialRogueActionUnlocked = false;
		EnsureAdventureScriptedTutorialView();
		adventureScriptedTutorialPanel.SetActive(true);
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		SetMessagePanelVisibleDuringAdventureTutorial(visible: false);
		if ((Object)(object)adventureScriptedTutorialTitleText != (Object)null)
			adventureScriptedTutorialTitleText.text = title;
		adventureScriptedTutorialStepText.text = LocalizedAdventureTutorialStepCounter(
			(int)tutorialRoguePracticeStep + 1, (int)TutorialRoguePracticeStep.Done + 1);
		PlaceAdventureTutorialPanel(null);
		ResizeAdventureTutorialPanelForBody(body);
		StartAdventureTutorialBodyText(body);
		SetAdventureTutorialNextButtonEnabled(enabled: true);
		MoveAdventureTutorialSpotlight(null);
		RefreshCardActionOverlays();
	}

	private string ExpectedTutorialRogueTargetId() => tutorialRoguePracticeStep switch
	{
		TutorialRoguePracticeStep.AttackMage => TutorialRogueMageId,
		TutorialRoguePracticeStep.AttackRogue => TutorialRogueNeutralId,
		TutorialRoguePracticeStep.AttackWarrior => TutorialRogueWarriorId,
		_ => null
	};

	private bool TutorialRoguePracticeAllowsAttack() => tutorialRogueActionUnlocked
		&& tutorialRoguePracticeStep is TutorialRoguePracticeStep.AttackMage
			or TutorialRoguePracticeStep.AttackRogue
			or TutorialRoguePracticeStep.AttackWarrior;

	private bool TutorialRoguePracticeAllowsEnemyTarget(BattleCardState target) =>
		tutorialRogueActionUnlocked && target != null
		&& IsTutorialCard(target.Card.Id, ExpectedTutorialRogueTargetId());

	private bool TryScriptTutorialRoguePracticeResult(
		BattleCardState attacker,
		BattleCardState defender,
		CombatResult resolved,
		out CombatResult scripted)
	{
		scripted = resolved;
		if (!tutorialRoguePracticeActive || attacker == null || defender == null)
			return false;

		if (!attacker.BelongsToPlayer)
		{
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 1, 0, 4);
			return true;
		}

		if (!IsTutorialCard(attacker.Card.Id, TutorialRoguePlayerId))
			return false;

		VigorRollResult baseRoll = resolved.AttackerRoll;
		bool twoDice = baseRoll.HasSecondRoll;
		int second = twoDice ? 3 : 0;
		VigorRollResult rerolled = new VigorRollResult(
			baseRoll.DieSides,
			4,
			second,
			twoDice,
			baseRoll.SelectionMode == VigorSelectionMode.Lowest ? 3 : 4,
			baseRoll.Matchup,
			baseRoll.SelectionMode,
			firstRollBeforeReroll: 1,
			secondRollBeforeReroll: twoDice ? 2 : 0);
		VigorRollResult defenderRoll = ScriptRoll(resolved.DefenderRoll, 1);
		int attackerPower = resolved.AttackerTotal - resolved.AttackerRoll.SelectedRoll;
		int defenderPower = resolved.DefenderTotal - resolved.DefenderRoll.SelectedRoll;
		scripted = new CombatResult(
			rerolled,
			defenderRoll,
			attackerPower + rerolled.SelectedRoll,
			defenderPower + defenderRoll.SelectedRoll);
		return true;
	}

	private void EndTutorialRoguePractice()
	{
		tutorialRoguePracticeActive = false;
		tutorialRogueActionUnlocked = false;
		adventureScriptedTutorialActive = false;
		adventureScriptedTutorialPanel.SetActive(false);
		MoveAdventureTutorialSpotlight(null);
		CompleteActiveTutorialModule();
	}
}
}
