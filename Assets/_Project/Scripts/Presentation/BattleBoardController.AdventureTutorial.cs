using System;
using System.Collections;
using System.Collections.Generic;
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
	private enum AdventureTutorialAction
	{
		Started,
		NextPressed,
		DraftReady,
		InitiativeRolled,
		DeploymentCardSelected,
		DeploymentConfirmed,
		DeploymentCompleted,
		CardInspected,
		PlayerTurnStarted,
		AbilityPressed,
		AttackPressed,
		EnemyTargeted,
		BattleFinished
	}

	private void StartAdventureScriptedTutorial()
	{
		adventureScriptedTutorialActive = true;
		adventureScriptedTutorialStep = 0;
		adventureScriptedTutorialStepAcknowledged = false;
		adventureScriptedTutorialPendingTarget = null;
		adventureScriptedTutorialAllowedDraftIndex = -1;
		adventureScriptedTutorialInspectionOpened = false;
		adventureScriptedTutorialAwaitingAdvantageContinue = false;
		adventureScriptedTutorialObjectiveShown = false;
		EnsureAdventureScriptedTutorialView();
		ReturnToStart(showModeSelection: false);
		SetAccountHubHudActive(false);
		SetBattlefieldSurfaceVisible(visible: true);
		SetCombatChromeVisible(visible: true);
		SetAdventureTutorialTimelineVisible(visible: false);
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}
		ShowAdventureScriptedTutorialStep(
			"Tutorial guidato",
			"Giocherai una stanza guidata. Ti illuminero cosa guardare o premere, per farti capire a grandi linee come funziona il combattimento.",
			null);
	}

	private void BuildScriptedTutorialRun()
	{
		List<CardDefinition> playerDeck = BuildTutorialPlayerDeck();
		if (playerDeck.Count == 0)
		{
			SetMessage("Tutorial non disponibile: nessuna carta valida nel database.");
			EndAdventureScriptedTutorial(complete: false);
			return;
		}

		campaignDeck = new CampaignDeckState(new List<CardDefinition>());
		currentRoomType = RoomType.Monster;
		currentMonsterTier = 1;
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Normal;
		currentScenarioDisplayOverride = "Tutorial - Primo Scontro";
		ResetScenarioRuleState();
		RestoreCampaignMana(10);
		((Component)campaignZoneRect).gameObject.SetActive(false);
		AppendLog("TUTORIAL AVVENTURA - run scriptata avviata.");
		PrepareNextCampaignCombatDraft();
	}

	private List<CardDefinition> BuildTutorialPlayerDeck()
	{
		return ResolveTutorialCards(new[]
		{
			"10-champion-warrior",
			"9-faceless-mage",
			"8-spirit-mage",
			"8-spirit-warrior",
			"7-whitealien-rogue",
			"6-chimera-warrior"
		});
	}

	private IReadOnlyList<CardDefinition> TutorialCards()
	{
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		return cardDatabase?.Cards ?? Array.Empty<CardDefinition>();
	}

	private List<CardDefinition> BuildTutorialCpuDeploymentHand()
	{
		return ResolveTutorialCards(new[]
		{
			"8-spirit-warrior",
			"7-whitealien-mage",
			"6-chimera-rogue"
		});
	}

	private List<CardDefinition> ResolveTutorialCards(IReadOnlyList<string> ids)
	{
		IReadOnlyList<CardDefinition> cards = TutorialCards();
		List<CardDefinition> result = new List<CardDefinition>();
		foreach (string id in ids)
		{
			CardDefinition card = cards.FirstOrDefault(candidate =>
				(Object)(object)candidate != (Object)null
				&& string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
			if ((Object)(object)card != (Object)null)
			{
				result.Add(card);
			}
			else
			{
				AppendLog($"TUTORIAL AVVENTURA - carta scriptata '{id}' non trovata.");
			}
		}
		return result;
	}

	private void NotifyAdventureTutorial(AdventureTutorialAction action)
	{
		if (!adventureScriptedTutorialActive)
		{
			return;
		}
		if (action != AdventureTutorialAction.NextPressed
			&& action != AdventureTutorialAction.InitiativeRolled
			&& action != AdventureTutorialAction.DeploymentCardSelected
			&& action != AdventureTutorialAction.DeploymentConfirmed
			&& action != AdventureTutorialAction.DeploymentCompleted
			&& action != AdventureTutorialAction.CardInspected
			&& action != AdventureTutorialAction.PlayerTurnStarted
			&& action != AdventureTutorialAction.AbilityPressed
			&& action != AdventureTutorialAction.AttackPressed
			&& action != AdventureTutorialAction.EnemyTargeted
			&& !adventureScriptedTutorialStepAcknowledged)
		{
			return;
		}
		AdvanceAdventureTutorialAfter(action);
	}

	private void AdvanceAdventureTutorialAfter(AdventureTutorialAction action)
	{
		if (action == AdventureTutorialAction.PlayerTurnStarted
			&& adventureScriptedTutorialStep >= 4
			&& IsAdventureTutorialWarriorAbilityTurn())
		{
			adventureScriptedTutorialStep = 5;
			RefreshCardActionOverlays();
			ShowAdventureTutorialWarriorAbilityStep();
			return;
		}
		switch (adventureScriptedTutorialStep)
		{
		case 0:
			if (action == AdventureTutorialAction.NextPressed)
			{
				adventureScriptedTutorialStep = 1;
				ShowAdventureScriptedTutorialStep(
					"Iniziative",
					"Le carte entrano in mano e il gioco tira le iniziative, questo tiro determina chi schiera prima e chi attacca prima.",
					null);
			}
			break;
		case 1:
			if (action == AdventureTutorialAction.NextPressed)
			{
				AcknowledgeAdventureTutorialStep();
				BuildScriptedTutorialRun();
				break;
			}
			if (action == AdventureTutorialAction.InitiativeRolled)
			{
				adventureScriptedTutorialStep = 2;
				ShowAdventureScriptedTutorialStep(
					"Il tuo turno di schieramento",
					"Guarda la timeline delle iniziative qui a destra: quando tocca a te, scegli una carta dalla mano. Ogni carta ha un valore che indica la tua potenza. Seleziona quelle piu forti!",
					FirstVisibleDraftCardRect());
			}
			break;
		case 2:
			if (action == AdventureTutorialAction.NextPressed)
			{
				AcknowledgeAdventureTutorialStep();
				ProcessNextDeploymentToken();
				break;
			}
			if (action == AdventureTutorialAction.DeploymentCardSelected)
			{
				adventureScriptedTutorialStep = 3;
				ShowAdventureScriptedTutorialStep(
					"Conferma lo schieramento",
					"Questa carta e pronta a entrare. Premi CONFERMA per metterla sul campo.",
					(Object)(object)confirmActionButton != (Object)null ? (RectTransform)((Component)confirmActionButton).transform : null);
				MoveAdventureTutorialSpotlight(null);
			}
			break;
		case 3:
			if (action == AdventureTutorialAction.NextPressed)
			{
				AcknowledgeAdventureTutorialStep();
				break;
			}
			if (action == AdventureTutorialAction.DeploymentCardSelected)
			{
				ShowAdventureScriptedTutorialStep(
					"Conferma lo schieramento",
					"Questa carta e pronta a entrare. Premi CONFERMA per metterla sul campo.",
					(Object)(object)confirmActionButton != (Object)null ? (RectTransform)((Component)confirmActionButton).transform : null);
				MoveAdventureTutorialSpotlight(null);
				break;
			}
			if (action == AdventureTutorialAction.DeploymentCompleted)
			{
				adventureScriptedTutorialStep = 4;
				ShowAdventureScriptedTutorialStep(
					"Il campo",
					"Sopra ci sono i mostri del Master, sotto la tua formazione. Premi CONTINUA per sistemare le pedine sul campo.",
					null);
				SetAdventureTutorialNextButtonEnabled(enabled: true);
				break;
			}
			if (action == AdventureTutorialAction.DeploymentConfirmed)
			{
				if (selectedPlayerDeploymentIndices.Count < configuration.Gameplay.FormationSize)
				{
					ShowAdventureScriptedTutorialStep(
						"Prossima iniziativa",
						"Lo schieramento continua in ordine di iniziativa. Quando tocchera ancora a te, scegli un'altra carta dalla mano.",
						null);
					SetAdventureTutorialNextButtonEnabled(enabled: false);
				}
				else
				{
					ShowAdventureScriptedTutorialStep(
						"Schieramento",
						"L'ultima carta sta entrando in campo. Aspetta che lo schieramento sia completo.",
						null);
					SetAdventureTutorialNextButtonEnabled(enabled: false);
				}
			}
			break;
		case 4:
			if (action == AdventureTutorialAction.NextPressed)
			{
				AcknowledgeAdventureTutorialStep();
				if (currentDeploymentIndex >= deploymentOrder.Count
					&& selectedPlayerDeploymentIndices.Count >= configuration.Gameplay.FormationSize)
				{
					if (adventureScriptedTutorialInspectionOpened)
					{
						BeginCurrentTurn();
					}
					else
					{
						CompleteDeploymentAndStartBattle();
					}
				}
				break;
			}
			if (action == AdventureTutorialAction.DeploymentCompleted)
			{
				ShowAdventureScriptedTutorialStep(
					"Il campo",
					"Sopra ci sono i mostri del Master, sotto la tua formazione. Premi CONTINUA per sistemare le pedine sul campo.",
					null);
				SetAdventureTutorialNextButtonEnabled(enabled: true);
			}
			if (action == AdventureTutorialAction.CardInspected)
			{
				adventureScriptedTutorialInspectionOpened = true;
				SetAdventureTutorialNextButtonEnabled(enabled: true);
				MoveAdventureTutorialSpotlight(null);
			}
			if (action == AdventureTutorialAction.DeploymentCardSelected
				&& selectedPlayerDeploymentIndices.Count < configuration.Gameplay.FormationSize)
			{
				adventureScriptedTutorialStep = 3;
				ShowAdventureScriptedTutorialStep(
					"Conferma lo schieramento",
					"Questa carta e pronta a entrare. Premi CONFERMA per metterla sul campo.",
					(Object)(object)confirmActionButton != (Object)null ? (RectTransform)((Component)confirmActionButton).transform : null);
			}
			if (action == AdventureTutorialAction.PlayerTurnStarted)
			{
				adventureScriptedTutorialStep = 5;
				RefreshCardActionOverlays();
				if (IsAdventureTutorialWarriorAbilityTurn())
				{
					ShowAdventureTutorialWarriorAbilityStep();
					break;
				}
				RectTransform attackTarget = ActivePlayerAttackActionRect();
				ShowAdventureScriptedTutorialStep(
					"Attacca un mostro",
					"Quando e il tuo turno premi su attacca e scegli un bersaglio nemico, partiranno i dadi Vigore e vedrai come si risolve il confronto.",
					attackTarget);
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				MoveAdventureTutorialSpotlight(attackTarget);
			}
			break;
		case 5:
			if (action == AdventureTutorialAction.PlayerTurnStarted && IsAdventureTutorialWarriorAbilityTurn())
			{
				RefreshCardActionOverlays();
				ShowAdventureTutorialWarriorAbilityStep();
				break;
			}
			if (action == AdventureTutorialAction.NextPressed)
			{
				if (adventureScriptedTutorialAwaitingAdvantageContinue)
				{
					adventureScriptedTutorialAwaitingAdvantageContinue = false;
					ShowAdventureTutorialAttackTargetStep();
					break;
				}
				AcknowledgeAdventureTutorialStep();
				break;
			}
			if (action == AdventureTutorialAction.AttackPressed)
			{
				if (IsAdventureTutorialMageAdvantageAttackTurn())
				{
					adventureScriptedTutorialAwaitingAdvantageContinue = true;
					ShowAdventureScriptedTutorialStep(
						"Vantaggio e svantaggio",
						"Prima del primo attacco guarda le aure sulle pedine nemiche: verde vuol dire vantaggio, gialla vuol dire normale, rossa vuol dire svantaggio. Il vantaggio fa tirare due dadi Vigore e tenere il risultato migliore.",
						null);
					SetAdventureTutorialNextButtonEnabled(enabled: true);
					MoveAdventureTutorialSpotlight(null);
					break;
				}
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				MoveAdventureTutorialSpotlight(FirstAliveCpuCardRect());
				break;
			}
			if (action == AdventureTutorialAction.AbilityPressed)
			{
				ShowAdventureTutorialAttackTargetStep();
				break;
			}
			if (action == AdventureTutorialAction.EnemyTargeted)
			{
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				MoveAdventureTutorialSpotlight(null);
				break;
			}
			break;
		case 6:
			if (action == AdventureTutorialAction.NextPressed)
			{
				if (!adventureScriptedTutorialObjectiveShown)
				{
					adventureScriptedTutorialObjectiveShown = true;
					ShowAdventureScriptedTutorialStep(
						"Completa il tutorial",
						"Sconfiggi tutti i nemici per completare il tutorial.",
						null);
					SetAdventureTutorialNextButtonEnabled(enabled: true);
					MoveAdventureTutorialSpotlight(null);
					break;
				}
				AcknowledgeAdventureTutorialStep();
				break;
			}
			if (action == AdventureTutorialAction.BattleFinished)
			{
				adventureScriptedTutorialStep = 7;
				ShowAdventureScriptedTutorialStep(
					"Ricompensa",
					"Hai completato il tutorial. Ti consegno le classi base e il primo capitolo dell'Avventura. I vasetti di miele, invece, si guadagnano in taverna con le quest del giorno.",
					null);
			}
			break;
		case 7:
			if (action == AdventureTutorialAction.NextPressed)
			{
				EndAdventureScriptedTutorial(complete: true);
			}
			break;
		}
	}

	private void ShowAdventureTutorialWarriorAbilityStep()
	{
		RectTransform abilityTarget = ActivePlayerAbilityActionRect();
		ShowAdventureScriptedTutorialStep(
			"Abilita Guerriero",
			"Questo e il Guerriero. Le carte possono avere abilita speciali: premi ABILITA per preparare il suo colpo pesante. Nel prossimo attacco tirera il dado Vigore e un dado di uno step inferiore, sommandoli alla sua potenza.",
			abilityTarget);
		SetAdventureTutorialNextButtonEnabled(enabled: false);
		MoveAdventureTutorialSpotlight(abilityTarget);
	}

	private void ShowAdventureTutorialAttackTargetStep()
	{
		RectTransform target = TutorialCpuWarriorTargetRect();
		string body = IsAdventureTutorialWarriorAbilityAttackPrepared()
			? "Ora premi la pedina Warrior avversaria illuminata. Il Guerriero usera l'abilita appena preparata nel tiro Vigore."
			: "Ora premi la pedina Warrior avversaria illuminata per vedere il vantaggio del Mago in azione.";
		ShowAdventureScriptedTutorialStep(
			"Scegli il bersaglio",
			body,
			target);
		SetAdventureTutorialNextButtonEnabled(enabled: false);
		MoveAdventureTutorialSpotlight(target);
	}

	private bool IsAdventureTutorialWaitingForAdvantageContinue()
	{
		return adventureScriptedTutorialActive && adventureScriptedTutorialAwaitingAdvantageContinue;
	}

	private bool IsAdventureTutorialMageAdvantageAttackTurn()
	{
		if (!adventureScriptedTutorialActive || selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			return false;
		}
		BattleCardState card = playerCards[selectedPlayerIndex];
		return card != null
			&& !card.Eliminated
			&& card.Card.HeroClass == HeroClass.Mage
			&& (card.Card.Strength == 8 || string.Equals(card.Card.Id, "8-spirit-mage", StringComparison.OrdinalIgnoreCase));
	}

	private bool IsAdventureTutorialWarriorAbilityAttackPrepared()
	{
		if (!adventureScriptedTutorialActive || selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			return false;
		}
		BattleCardState card = playerCards[selectedPlayerIndex];
		return card != null
			&& !card.Eliminated
			&& card.Card.HeroClass == HeroClass.Warrior
			&& card.AbilityArmed;
	}

	private bool ShowAdventureTutorialCombatRollResult(BattleCardState attacker, BattleCardState defender, CombatResult result)
	{
		if (!adventureScriptedTutorialActive || adventureScriptedTutorialStep != 5)
		{
			return false;
		}
		adventureScriptedTutorialStep = 6;
		ShowAdventureScriptedTutorialStep(
			"Dadi Vigore",
			FormatAdventureTutorialCombatRollText(attacker, defender, result),
			null);
		SetAdventureTutorialNextButtonEnabled(enabled: true);
		MoveAdventureTutorialSpotlight(null);
		return true;
	}

	/// <summary>
	/// Rende deterministici i confronti della battaglia guidata. Il resolver normale
	/// continua a stabilire numero di dadi, vantaggio e reroll; qui sostituiamo solo
	/// i valori mostrati e usati dal risultato finale.
	/// </summary>
	private CombatResult ScriptAdventureTutorialCombatResult(
		BattleCardState attacker,
		BattleCardState defender,
		CombatResult resolved)
	{
		if (!adventureScriptedTutorialActive || attacker == null || defender == null)
		{
			return resolved;
		}

		string attackerId = attacker.Card.Id;
		string defenderId = defender.Card.Id;
		VigorRollResult attackerRoll;
		VigorRollResult defenderRoll;

		// 1. Mago 8 del giocatore contro Guerriero 8: 4 e 3 con vantaggio, contro 3.
		if (IsTutorialCard(attackerId, "8-spirit-mage") && IsTutorialCard(defenderId, "8-spirit-warrior"))
		{
			attackerRoll = ScriptRoll(resolved.AttackerRoll, 4, 3);
			defenderRoll = ScriptRoll(resolved.DefenderRoll, 3);
		}
		// 2. Ladro 7 del giocatore perde contro la pedina scelta rimasta.
		else if (IsTutorialCard(attackerId, "7-whitealien-rogue")
			&& (IsTutorialCard(defenderId, "6-chimera-rogue") || IsTutorialCard(defenderId, "7-whitealien-mage")))
		{
			attackerRoll = ScriptRoll(resolved.AttackerRoll, 2, 2);
			defenderRoll = ScriptRoll(resolved.DefenderRoll,
				IsTutorialCard(defenderId, "6-chimera-rogue") ? 4 : 3);
		}
		// 3. Il Ladro 6 CPU elimina il Ladro 7: 4 contro 1.
		else if (IsTutorialCard(attackerId, "6-chimera-rogue") && IsTutorialCard(defenderId, "7-whitealien-rogue"))
		{
			attackerRoll = ScriptRoll(resolved.AttackerRoll, 4);
			defenderRoll = ScriptRoll(resolved.DefenderRoll, 1);
		}
		// Se resta il Mago 7, il suo contrattacco non elimina il Guerriero 10.
		else if (IsTutorialCard(attackerId, "7-whitealien-mage") && IsTutorialCard(defenderId, "10-champion-warrior"))
		{
			attackerRoll = ScriptRoll(resolved.AttackerRoll, 1, 1);
			defenderRoll = ScriptRoll(resolved.DefenderRoll, 4);
		}
		// Il successivo attacco del Guerriero chiude lo scontro con l'eventuale Mago.
		else if (IsTutorialCard(attackerId, "10-champion-warrior") && IsTutorialCard(defenderId, "7-whitealien-mage"))
		{
			attackerRoll = ScriptRoll(resolved.AttackerRoll, 4, 3);
			defenderRoll = ScriptRoll(resolved.DefenderRoll, 1);
		}
		else
		{
			return resolved;
		}

		return new CombatResult(
			attackerRoll,
			defenderRoll,
			attacker.Card.Strength + attackerRoll.SelectedRoll,
			defender.Card.Strength + defenderRoll.SelectedRoll);
	}

	private static bool IsTutorialCard(string actualId, string expectedId)
	{
		return string.Equals(actualId, expectedId, StringComparison.OrdinalIgnoreCase);
	}

	private static VigorRollResult ScriptRoll(VigorRollResult template, int first, int second = 0)
	{
		first = Mathf.Clamp(first, 1, template.DieSides);
		bool hasSecond = template.HasSecondRoll;
		second = hasSecond ? Mathf.Clamp(second > 0 ? second : first, 1, template.DieSides) : 0;
		int selected = template.SelectionMode switch
		{
			VigorSelectionMode.Sum => first + second,
			VigorSelectionMode.Highest => Mathf.Max(first, second),
			VigorSelectionMode.Lowest => Mathf.Min(first, second),
			_ => first
		};
		return new VigorRollResult(
			template.DieSides,
			first,
			second,
			hasSecond,
			selected,
			template.Matchup,
			template.SelectionMode);
	}

	private string FormatAdventureTutorialCombatRollText(BattleCardState attacker, BattleCardState defender, CombatResult result)
	{
		List<string> lines = new List<string>();
		string attackerRule = FormatAdventureTutorialRollRule(attacker, defender, result.AttackerRoll, attackerIsAttacking: true);
		if (!string.IsNullOrEmpty(attackerRule))
		{
			lines.Add(attackerRule);
		}
		string defenderRule = FormatAdventureTutorialRollRule(defender, attacker, result.DefenderRoll, attackerIsAttacking: false);
		if (!string.IsNullOrEmpty(defenderRule))
		{
			lines.Add(defenderRule);
		}
		lines.Add($"Attaccante: potenza {attacker.Card.Strength} + {result.AttackerRoll.SelectedRoll} = {result.AttackerTotal}.");
		lines.Add($"Difensore: potenza {defender.Card.Strength} + {result.DefenderRoll.SelectedRoll} = {result.DefenderTotal}.");
		lines.Add(result.DefenderIsDefeated
			? $"Vince l'attaccante: il totale e piu alto, quindi il difensore perde il confronto."
			: $"Resiste il difensore: in difesa basta pareggiare o superare l'attacco.");
		return string.Join("\n", lines);
	}

	private string FormatAdventureTutorialRollRule(BattleCardState actor, BattleCardState opponent, VigorRollResult roll, bool attackerIsAttacking)
	{
		if (actor == null || opponent == null || !roll.HasSecondRoll)
		{
			return string.Empty;
		}
		if (roll.SelectionMode == VigorSelectionMode.Sum)
		{
			return $"{(attackerIsAttacking ? "L'attaccante" : "Il difensore")} ha usato l'abilita Guerriero: tira {roll.FirstRoll} e {roll.SecondRoll} e li somma ({roll.SelectedRoll}).";
		}
		string actorFamily = CardRulesGlossary.ClassFamilyName(HeroClassFamily.Of(actor.Card.HeroClass));
		string opponentFamily = CardRulesGlossary.ClassFamilyName(HeroClassFamily.Of(opponent.Card.HeroClass));
		if (roll.SelectionMode == VigorSelectionMode.Highest)
		{
			if (attackerIsAttacking)
			{
				return $"L'attaccante e di famiglia {actorFamily}, e forte contro la famiglia {opponentFamily}: si applica vantaggio tirando due dadi ({roll.FirstRoll} e {roll.SecondRoll}) e si conserva il risultato piu alto ({roll.SelectedRoll}).";
			}
			return $"Il difensore ha vantaggio in difesa: tira due dadi ({roll.FirstRoll} e {roll.SecondRoll}) e conserva il risultato piu alto ({roll.SelectedRoll}).";
		}
		if (roll.SelectionMode == VigorSelectionMode.Lowest)
		{
			return $"{(attackerIsAttacking ? "L'attaccante" : "Il difensore")} ha svantaggio perche {opponentFamily} batte {actorFamily}: tira {roll.FirstRoll} e {roll.SecondRoll}, tiene il piu basso ({roll.SelectedRoll}).";
		}
		return string.Empty;
	}

	private IEnumerator WaitForAdventureTutorialStepAcknowledged(int step)
	{
		while (adventureScriptedTutorialActive
			&& adventureScriptedTutorialStep == step
			&& !adventureScriptedTutorialStepAcknowledged)
		{
			yield return null;
		}
	}

	private void AcknowledgeAdventureTutorialStep()
	{
		adventureScriptedTutorialStepAcknowledged = true;
		SetAdventureTutorialNextButtonEnabled(enabled: false);
		MoveAdventureTutorialSpotlight(adventureScriptedTutorialStep == 2 ? null : adventureScriptedTutorialPendingTarget);
	}

	private RectTransform FirstVisibleDraftCardRect()
	{
		foreach (PrototypeCardView view in draftViews)
		{
			if ((Object)(object)view != (Object)null && ((Component)view).gameObject.activeInHierarchy)
			{
				return view.RectTransform;
			}
		}
		return null;
	}

	private RectTransform FirstSelectableDraftCardRect()
	{
		int index = FirstSelectableDraftCardIndex();
		return index >= 0 ? draftViews[index].RectTransform : FirstVisibleDraftCardRect();
	}

	private int FirstSelectableDraftCardIndex()
	{
		int scriptedIndex = ScriptedTutorialDeploymentCardIndex();
		if (scriptedIndex >= 0)
		{
			return scriptedIndex;
		}
		for (int i = 0; i < draftViews.Count; i++)
		{
			PrototypeCardView view = draftViews[i];
			if ((Object)(object)view != (Object)null
				&& ((Component)view).gameObject.activeInHierarchy
				&& !selectedDraftCards.Contains(i))
			{
				return i;
			}
		}
		return -1;
	}

	private int ScriptedTutorialDeploymentCardIndex()
	{
		string[] orderedIds =
		{
			"10-champion-warrior",
			"7-whitealien-rogue",
			"8-spirit-mage"
		};
		int stepIndex = selectedPlayerDeploymentIndices.Count;
		if (stepIndex < 0 || stepIndex >= orderedIds.Length)
		{
			return -1;
		}
		string targetId = orderedIds[stepIndex];
		for (int i = 0; i < draftCandidates.Count && i < draftViews.Count; i++)
		{
			CardDefinition card = draftCandidates[i];
			if ((Object)(object)card != (Object)null
				&& !selectedDraftCards.Contains(i)
				&& string.Equals(card.Id, targetId, StringComparison.OrdinalIgnoreCase)
				&& (Object)(object)draftViews[i] != (Object)null
				&& ((Component)draftViews[i]).gameObject.activeInHierarchy)
			{
				return i;
			}
		}
		return -1;
	}

	private void ShowAdventureTutorialDeploymentChoiceSpotlight()
	{
		if (!adventureScriptedTutorialActive
			|| adventureScriptedTutorialStep < 2
			|| selectedPlayerDeploymentIndices.Count >= configuration.Gameplay.FormationSize)
		{
			return;
		}
		adventureScriptedTutorialAllowedDraftIndex = FirstSelectableDraftCardIndex();
		SetAdventureTutorialDraftInteractivityForChoice();
		MoveAdventureTutorialSpotlight(adventureScriptedTutorialAllowedDraftIndex >= 0 ? draftViews[adventureScriptedTutorialAllowedDraftIndex].RectTransform : null);
	}

	private bool IsAdventureTutorialDraftCardAllowed(int index)
	{
		return !adventureScriptedTutorialActive
			|| !deploymentDraftActive
			|| adventureScriptedTutorialAllowedDraftIndex < 0
			|| index == adventureScriptedTutorialAllowedDraftIndex;
	}

	private void SetAdventureTutorialDraftInteractivityForChoice()
	{
		if (!adventureScriptedTutorialActive || !deploymentDraftActive)
		{
			return;
		}
		for (int i = 0; i < draftViews.Count; i++)
		{
			PrototypeCardView view = draftViews[i];
			if ((Object)(object)view == (Object)null)
			{
				continue;
			}
			view.SetDraftSelected(i == pendingDeploymentIndex);
			view.SetInteractable(i == adventureScriptedTutorialAllowedDraftIndex
				&& !selectedDraftCards.Contains(i)
				&& pendingDeploymentIndex < 0);
		}
	}

	private void SetAdventureTutorialDraftInteractivityForConfirmation()
	{
		if (!adventureScriptedTutorialActive || !deploymentDraftActive)
		{
			return;
		}
		for (int i = 0; i < draftViews.Count; i++)
		{
			PrototypeCardView view = draftViews[i];
			if ((Object)(object)view == (Object)null)
			{
				continue;
			}
			view.SetDraftSelected(i == pendingDeploymentIndex);
			view.SetInteractable(false);
		}
	}

	private RectTransform FirstAliveCpuCardRect()
	{
		foreach (BattleCardState card in cpuCards)
		{
			if (card != null && !card.Eliminated && (Object)(object)card.View != (Object)null)
			{
				return card.View.RectTransform;
			}
		}
		return null;
	}

	private RectTransform TutorialCpuWarriorTargetRect()
	{
		foreach (BattleCardState card in cpuCards)
		{
			if (card != null
				&& !card.Eliminated
				&& card.Card.HeroClass == HeroClass.Warrior
				&& (Object)(object)card.View != (Object)null
				&& ((Component)card.View).gameObject.activeInHierarchy)
			{
				return card.View.RectTransform;
			}
		}
		return FirstAliveCpuCardRect();
	}

	private RectTransform FirstPlayerDeploymentPreviewRect()
	{
		foreach (PrototypeCardView view in playerDeploymentPreviewViews)
		{
			if ((Object)(object)view != (Object)null && ((Component)view).gameObject.activeInHierarchy)
			{
				return view.RectTransform;
			}
		}
		return (Object)(object)playerRow != (Object)null ? playerRow : null;
	}

	private RectTransform FirstPlayerBattleCardRect()
	{
		foreach (BattleCardState card in playerCards)
		{
			if (card != null && !card.Eliminated && (Object)(object)card.View != (Object)null)
			{
				return card.View.RectTransform;
			}
		}
		return (Object)(object)playerRow != (Object)null ? playerRow : null;
	}

	private IEnumerator ShowAdventureTutorialInspectionStepAfterBattlefieldMove()
	{
		while (playerBattlefieldRowTransitionCoroutine != null)
		{
			yield return null;
		}
		adventureScriptedTutorialInspectionOpened = false;
		ShowAdventureScriptedTutorialStep(
			"Leggi una pedina",
			"Tocca una pedina schierata per aprire la scheda. Da li puoi leggere potenza, famiglia, classe, abilita e vantaggi.",
			FirstPlayerBattleCardRect());
		SetAdventureTutorialNextButtonEnabled(enabled: false);
		MoveAdventureTutorialSpotlight(FirstPlayerBattleCardRect());
	}

	private RectTransform ActivePlayerAttackActionRect()
	{
		if (selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			return null;
		}
		BattleCardState card = playerCards[selectedPlayerIndex];
		if (card == null || (Object)(object)card.View == (Object)null)
		{
			return null;
		}
		return card.View.AttackActionRect ?? card.View.RectTransform;
	}

	private RectTransform ActivePlayerAbilityActionRect()
	{
		if (selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			return null;
		}
		BattleCardState card = playerCards[selectedPlayerIndex];
		if (card == null || (Object)(object)card.View == (Object)null)
		{
			return null;
		}
		return card.View.AbilityActionRect ?? card.View.RectTransform;
	}

	private bool IsAdventureTutorialWarriorAbilityTurn()
	{
		if (!adventureScriptedTutorialActive || selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			return false;
		}
		BattleCardState card = playerCards[selectedPlayerIndex];
		return card != null
			&& !card.Eliminated
			&& !card.AbilityUsed
			&& !card.AbilityArmed
			&& card.Card.HeroClass == HeroClass.Warrior
			&& (card.Card.Strength == 10 || string.Equals(card.Card.Id, "10-champion-warrior", StringComparison.OrdinalIgnoreCase));
	}

	private void EnsureAdventureScriptedTutorialView()
	{
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			return;
		}

		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		CreateAdventureTutorialDimmers();
		Image panel = CreateImage("Adventure Scripted Tutorial Panel", (Transform)(object)safeAreaRoot, new Color(0.01f, 0.018f, 0.028f, 0.94f));
		panel.raycastTarget = false;
		StylePanel(panel);
		SetAdventureTutorialPanelToMessageDialogRect(panel.rectTransform);
		adventureScriptedTutorialPanel = ((Component)panel).gameObject;
		CanvasGroup panelCanvasGroup = adventureScriptedTutorialPanel.AddComponent<CanvasGroup>();
		panelCanvasGroup.blocksRaycasts = false;
		panelCanvasGroup.interactable = false;

		adventureScriptedTutorialTitleText = CreateText("Adventure Scripted Tutorial Title", ((Component)panel).transform, AccardND.Battlefield.MmoUiTheme.LoreFont, 26, FontStyle.Normal, (TextAnchor)3);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(adventureScriptedTutorialTitleText);
		adventureScriptedTutorialTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		adventureScriptedTutorialTitleText.raycastTarget = false;
		SetRect(adventureScriptedTutorialTitleText.rectTransform, new Vector2(0.04f, 0.68f), new Vector2(0.68f, 0.92f));

		adventureScriptedTutorialStepText = CreateText("Adventure Scripted Tutorial Counter", ((Component)panel).transform, font, 18, (FontStyle)1, (TextAnchor)5);
		adventureScriptedTutorialStepText.color = new Color(0.64f, 0.78f, 0.86f);
		adventureScriptedTutorialStepText.raycastTarget = false;
		SetRect(adventureScriptedTutorialStepText.rectTransform, new Vector2(0.7f, 0.68f), new Vector2(0.96f, 0.92f));

		adventureScriptedTutorialBodyText = CreateText("Adventure Scripted Tutorial Body", ((Component)panel).transform, font, 26, (FontStyle)1, TextAnchor.UpperLeft);
		adventureScriptedTutorialBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		adventureScriptedTutorialBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		adventureScriptedTutorialBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		adventureScriptedTutorialBodyText.resizeTextForBestFit = true;
		adventureScriptedTutorialBodyText.resizeTextMinSize = 18;
		adventureScriptedTutorialBodyText.resizeTextMaxSize = 26;
		adventureScriptedTutorialBodyText.raycastTarget = false;
		SetRect(adventureScriptedTutorialBodyText.rectTransform, new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.66f));

		adventureScriptedTutorialNextButton = CreateButton("Adventure Scripted Tutorial Next", ((Component)panel).transform, font, "CONTINUA");
		AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(adventureScriptedTutorialNextButton);
		CanvasGroup nextButtonCanvasGroup = ((Component)adventureScriptedTutorialNextButton).gameObject.AddComponent<CanvasGroup>();
		nextButtonCanvasGroup.blocksRaycasts = true;
		nextButtonCanvasGroup.interactable = true;
		nextButtonCanvasGroup.ignoreParentGroups = true;
		((UnityEvent)adventureScriptedTutorialNextButton.onClick).AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			if (CompleteAdventureTutorialBodyText())
			{
				return;
			}
			NotifyAdventureTutorial(AdventureTutorialAction.NextPressed);
		});
		SetRect((RectTransform)((Component)adventureScriptedTutorialNextButton).transform, new Vector2(0.66f, 0.05f), new Vector2(0.96f, 0.19f));

		adventureScriptedTutorialSpotlight = CreateImage("Adventure Scripted Tutorial Spotlight", (Transform)(object)safeAreaRoot, Color.white);
		adventureScriptedTutorialSpotlight.sprite = GetHelpAuraSprite();
		adventureScriptedTutorialSpotlight.preserveAspect = false;
		adventureScriptedTutorialSpotlight.raycastTarget = false;
		CanvasGroup spotlightCanvasGroup = ((Component)adventureScriptedTutorialSpotlight).gameObject.AddComponent<CanvasGroup>();
		spotlightCanvasGroup.blocksRaycasts = false;
		spotlightCanvasGroup.interactable = false;
		((Component)adventureScriptedTutorialSpotlight).gameObject.SetActive(false);
		adventureScriptedTutorialPanel.SetActive(false);
	}

	private void CreateAdventureTutorialDimmers()
	{
		if (adventureScriptedTutorialDimmers.Count > 0)
		{
			return;
		}
		for (int index = 0; index < 4; index++)
		{
			Image dimmer = CreateImage("Adventure Tutorial Dimmer " + index, (Transform)(object)safeAreaRoot, new Color(0f, 0f, 0f, 0.62f));
			dimmer.raycastTarget = false;
			CanvasGroup dimmerCanvasGroup = ((Component)dimmer).gameObject.AddComponent<CanvasGroup>();
			dimmerCanvasGroup.blocksRaycasts = false;
			dimmerCanvasGroup.interactable = false;
			((Component)dimmer).gameObject.SetActive(false);
			adventureScriptedTutorialDimmers.Add(dimmer);
		}
	}

	private void ShowAdventureScriptedTutorialStep(string title, string body, RectTransform target)
	{
		EnsureAdventureScriptedTutorialView();
		adventureScriptedTutorialPanel.SetActive(true);
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		SetMessagePanelVisibleDuringAdventureTutorial(visible: false);
		adventureScriptedTutorialStepAcknowledged = false;
		adventureScriptedTutorialPendingTarget = target;
		adventureScriptedTutorialTitleText.text = title;
		StartAdventureTutorialBodyText(body);
		adventureScriptedTutorialStepText.text = $"PASSO {Mathf.Min(adventureScriptedTutorialStep + 1, 8)}/8";
		SetAdventureTutorialTimelineVisible(adventureScriptedTutorialStep >= 2);
		SetAdventureTutorialNextButtonEnabled(adventureScriptedTutorialStep != 3);
		PlaceAdventureTutorialPanel(null);
		MoveAdventureTutorialSpotlight(null);
	}

	private void SetAdventureTutorialNextButtonEnabled(bool enabled)
	{
		if ((Object)(object)adventureScriptedTutorialNextButton == (Object)null)
		{
			return;
		}
		GameObject buttonObject = ((Component)adventureScriptedTutorialNextButton).gameObject;
		buttonObject.SetActive(true);
		adventureScriptedTutorialNextButton.interactable = enabled;
		CanvasGroup canvasGroup = buttonObject.GetComponent<CanvasGroup>();
		if ((Object)(object)canvasGroup != (Object)null)
		{
			canvasGroup.alpha = enabled ? 1f : 0.38f;
		}
	}

	private void StartAdventureTutorialBodyText(string body)
	{
		adventureScriptedTutorialBodyFullText = body ?? string.Empty;
		if (adventureScriptedTutorialTextRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(adventureScriptedTutorialTextRoutine);
			adventureScriptedTutorialTextRoutine = null;
		}
		if ((Object)(object)adventureScriptedTutorialBodyText == (Object)null)
		{
			return;
		}
		adventureScriptedTutorialBodyText.text = string.Empty;
		adventureScriptedTutorialTextRoutine = ((MonoBehaviour)this).StartCoroutine(PlayAdventureTutorialBodyText(adventureScriptedTutorialBodyFullText));
	}

	private IEnumerator PlayAdventureTutorialBodyText(string body)
	{
		for (int index = 0; index < body.Length; index++)
		{
			if ((Object)(object)adventureScriptedTutorialBodyText == (Object)null)
			{
				adventureScriptedTutorialTextRoutine = null;
				yield break;
			}
			adventureScriptedTutorialBodyText.text = body.Substring(0, index + 1);
			yield return new WaitForSecondsRealtime(GetAdventureTutorialTextDelay(body[index]));
		}
		adventureScriptedTutorialTextRoutine = null;
	}

	private float GetAdventureTutorialTextDelay(char character)
	{
		if (character == '.' || character == '!' || character == '?')
		{
			return 0.16f;
		}
		if (character == ',' || character == ':' || character == ';')
		{
			return 0.09f;
		}
		if (char.IsWhiteSpace(character))
		{
			return 0.012f;
		}
		return 0.028f;
	}

	private bool CompleteAdventureTutorialBodyText()
	{
		if (adventureScriptedTutorialTextRoutine == null)
		{
			return false;
		}
		((MonoBehaviour)this).StopCoroutine(adventureScriptedTutorialTextRoutine);
		adventureScriptedTutorialTextRoutine = null;
		if ((Object)(object)adventureScriptedTutorialBodyText != (Object)null)
		{
			adventureScriptedTutorialBodyText.text = adventureScriptedTutorialBodyFullText;
		}
		return true;
	}

	private void SetAdventureTutorialTimelineVisible(bool visible)
	{
		if ((Object)(object)timelineBackgroundRect != (Object)null)
		{
			((Component)timelineBackgroundRect).gameObject.SetActive(visible);
		}
		if ((Object)(object)initiativeTimelineRoot != (Object)null)
		{
			((Component)initiativeTimelineRoot).gameObject.SetActive(visible);
		}
	}

	private void PlaceAdventureTutorialPanel(RectTransform target)
	{
		if ((Object)(object)adventureScriptedTutorialPanel == (Object)null)
		{
			return;
		}
		RectTransform panelRect = (RectTransform)adventureScriptedTutorialPanel.transform;
		if ((Object)(object)target == (Object)null)
		{
			SetAdventureTutorialPanelToMessageDialogRect(panelRect);
			return;
		}
		Vector3[] corners = new Vector3[4];
		target.GetWorldCorners(corners);
		Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
		Vector3 localCenter = ((Transform)safeAreaRoot).InverseTransformPoint(worldCenter);
		float normalizedY = Mathf.InverseLerp(safeAreaRoot.rect.yMin, safeAreaRoot.rect.yMax, localCenter.y);
		if (normalizedY < 0.42f)
		{
			SetRect(panelRect, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.74f));
		}
		else
		{
			SetRect(panelRect, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.28f));
		}
	}

	private void SetAdventureTutorialPanelToMessageDialogRect(RectTransform panelRect)
	{
		if ((Object)(object)panelRect == (Object)null)
		{
			return;
		}
		SetRect(panelRect, new Vector2(0.08f, 0.39f), new Vector2(0.82f, 0.61f));
	}

	private void MoveAdventureTutorialSpotlight(RectTransform target)
	{
		if ((Object)(object)adventureScriptedTutorialSpotlight == (Object)null)
		{
			return;
		}
		GameObject spotlightObject = ((Component)adventureScriptedTutorialSpotlight).gameObject;
		if ((Object)(object)target == (Object)null || !((Component)target).gameObject.activeInHierarchy)
		{
			spotlightObject.SetActive(false);
			adventureScriptedTutorialSpotlight.rectTransform.rotation = Quaternion.identity;
			SetAdventureTutorialDimmers(null);
			return;
		}
		spotlightObject.SetActive(true);
		RectTransform spotlightRect = adventureScriptedTutorialSpotlight.rectTransform;
		spotlightRect.SetAsLastSibling();
		spotlightRect.rotation = ShouldRotateAdventureTutorialSpotlightWithTarget(target) ? target.rotation : Quaternion.identity;
		Vector3[] corners = new Vector3[4];
		target.GetWorldCorners(corners);
		Vector3 center = (corners[0] + corners[2]) * 0.5f;
		spotlightRect.position = center;
		float width = Vector3.Distance(corners[0], corners[3]) * 1.18f;
		float height = Vector3.Distance(corners[0], corners[1]) * 1.18f;
		spotlightRect.sizeDelta = new Vector2(width, height);
		SetAdventureTutorialDimmers(null);
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
	}

	private bool ShouldRotateAdventureTutorialSpotlightWithTarget(RectTransform target)
	{
		if ((Object)(object)target == (Object)null)
		{
			return false;
		}
		foreach (PrototypeCardView view in draftViews)
		{
			if ((Object)(object)view != (Object)null && (Object)(object)view.RectTransform == (Object)(object)target)
			{
				return true;
			}
		}
		return false;
	}

	private void SetAdventureTutorialDimmers(Rect? worldHole)
	{
		CreateAdventureTutorialDimmers();
		if (!worldHole.HasValue || (Object)(object)safeAreaRoot == (Object)null)
		{
			foreach (Image dimmer in adventureScriptedTutorialDimmers)
			{
				if ((Object)(object)dimmer != (Object)null)
				{
					((Component)dimmer).gameObject.SetActive(false);
				}
			}
			return;
		}
		Rect hole = worldHole.Value;
		Vector3[] safeCorners = new Vector3[4];
		safeAreaRoot.GetWorldCorners(safeCorners);
		Rect safe = new Rect(
			safeCorners[0].x,
			safeCorners[0].y,
			safeCorners[2].x - safeCorners[0].x,
			safeCorners[2].y - safeCorners[0].y);
		hole.xMin = Mathf.Clamp(hole.xMin, safe.xMin, safe.xMax);
		hole.xMax = Mathf.Clamp(hole.xMax, safe.xMin, safe.xMax);
		hole.yMin = Mathf.Clamp(hole.yMin, safe.yMin, safe.yMax);
		hole.yMax = Mathf.Clamp(hole.yMax, safe.yMin, safe.yMax);
		SetWorldDimmer(adventureScriptedTutorialDimmers[0], new Rect(safe.xMin, hole.yMax, safe.width, safe.yMax - hole.yMax));
		SetWorldDimmer(adventureScriptedTutorialDimmers[1], new Rect(safe.xMin, safe.yMin, safe.width, hole.yMin - safe.yMin));
		SetWorldDimmer(adventureScriptedTutorialDimmers[2], new Rect(safe.xMin, hole.yMin, hole.xMin - safe.xMin, hole.height));
		SetWorldDimmer(adventureScriptedTutorialDimmers[3], new Rect(hole.xMax, hole.yMin, safe.xMax - hole.xMax, hole.height));
	}

	private void SetWorldDimmer(Image dimmer, Rect worldRect)
	{
		if ((Object)(object)dimmer == (Object)null)
		{
			return;
		}
		GameObject dimmerObject = ((Component)dimmer).gameObject;
		if (worldRect.width <= 1f || worldRect.height <= 1f)
		{
			dimmerObject.SetActive(false);
			return;
		}
		dimmerObject.SetActive(true);
		RectTransform rect = dimmer.rectTransform;
		rect.position = new Vector3(worldRect.center.x, worldRect.center.y, 0f);
		rect.sizeDelta = new Vector2(worldRect.width, worldRect.height);
		rect.SetAsLastSibling();
	}

	private void EndAdventureScriptedTutorial(bool complete)
	{
		adventureScriptedTutorialActive = false;
		adventureScriptedTutorialAllowedDraftIndex = -1;
		adventureScriptedTutorialInspectionOpened = false;
		adventureScriptedTutorialAwaitingAdvantageContinue = false;
		adventureScriptedTutorialObjectiveShown = false;
		if (adventureScriptedTutorialTextRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(adventureScriptedTutorialTextRoutine);
			adventureScriptedTutorialTextRoutine = null;
		}
		SetAdventureTutorialTimelineVisible(visible: true);
		SetMessagePanelVisibleDuringAdventureTutorial(visible: true);
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			adventureScriptedTutorialPanel.SetActive(false);
		}
		if ((Object)(object)adventureScriptedTutorialSpotlight != (Object)null)
		{
			((Component)adventureScriptedTutorialSpotlight).gameObject.SetActive(false);
		}
		SetAdventureTutorialDimmers(null);
		if (complete)
		{
			ConfirmStartTutorialAdventureStage();
			ReturnToStart(showModeSelection: false);
			ShowAdventureChapterSelection();
		}
	}

	private void SetMessagePanelVisibleDuringAdventureTutorial(bool visible)
	{
		if ((Object)(object)messagePanelRect != (Object)null)
		{
			((Component)messagePanelRect).gameObject.SetActive(visible && !adventureScriptedTutorialActive);
		}
	}
}
}
