using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
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
		PlayerDeploymentTurnStarted,
		DeploymentCardSelected,
		DeploymentConfirmed,
		DeploymentCompleted,
		CardInspected,
		PlayerTurnStarted,
		AbilityPressed,
		SupremeUsed,
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
			"Giocherai una stanza guidata. Ti illuminero' su cosa guardare o premere, per farti capire a grandi line come funziona il combattimento.",
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
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Easy;
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
			"4-animal-warrior",
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
		// Le lezioni di classe usano lo stesso pannello e gli stessi eventi della stanza
		// guidata, ma hanno un copione loro: quando una e' in corso e' lei a rispondere.
		if (AdvanceTutorialWarriorDuel(action))
		{
			return;
		}
		if (action != AdventureTutorialAction.NextPressed
			&& action != AdventureTutorialAction.InitiativeRolled
			&& action != AdventureTutorialAction.PlayerDeploymentTurnStarted
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
				adventureScriptedTutorialPanel.SetActive(false);
				MoveAdventureTutorialSpotlight(null);
				BuildScriptedTutorialRun();
				break;
			}
			// Il dialogo deve riapparire solo quando il deployment e' davvero
			// arrivato al primo token del giocatore: a quel punto il tiro e le
			// entrate delle eventuali carte CPU precedenti sono gia' terminati.
			if (action == AdventureTutorialAction.PlayerDeploymentTurnStarted)
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
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialTimelineVisible(visible: false);
				// Questo passaggio entra direttamente nell'attacco guidato: non deve
				// riaprire la vecchia spiegazione/inspection del campo.
				adventureScriptedTutorialInspectionOpened = true;
				if (currentDeploymentIndex >= deploymentOrder.Count
					&& selectedPlayerDeploymentIndices.Count >= configuration.Gameplay.FormationSize)
				{
					// Le carte mostrate durante lo schieramento sono ancora preview: prima di
					// iniziare un turno vanno trasformate negli stati di combattimento reali.
					// Chiamare BeginCurrentTurn qui faceva eseguire CheckEndGame con cpuCards
					// ancora vuoto, assegnando subito la ricompensa e bloccando il tutorial.
					CompleteDeploymentAndStartBattle();
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
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialTimelineVisible(visible: false);
				MoveAdventureTutorialSpotlight(null);
				((MonoBehaviour)this).StartCoroutine(ShowAdventureTutorialAttackSpotlightAfterBattlefieldMove());
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
				SetAdventureTutorialNextButtonEnabled(enabled: false);
				adventureScriptedTutorialPanel.SetActive(false);
				MoveAdventureTutorialSpotlight(TutorialCpuStrengthFourTargetRect());
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
					HideAdventureTutorialCombatDice();
					ShowAdventureScriptedTutorialStep(
						"Completa il tutorial",
						"Sconfiggi tutti i nemici per completare il tutorial.",
						null);
					SetAdventureTutorialNextButtonEnabled(enabled: true);
					MoveAdventureTutorialSpotlight(null);
					break;
				}
				AcknowledgeAdventureTutorialStep();
				adventureScriptedTutorialPanel.SetActive(false);
				SetAdventureTutorialTimelineVisible(visible: false);
				MoveAdventureTutorialSpotlight(null);
				break;
			}
			if (action == AdventureTutorialAction.BattleFinished)
			{
				adventureScriptedTutorialStep = 7;
				ShowAdventureScriptedTutorialStep(
					"Ricompensa",
					"Hai completato il tutorial. Ora hai accesso al primo capitolo dell'Avventura. Avanza nell'avventura, sblocca le classi, completa le missioni giornaliere e scala la classifica!",
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

	private void HideAdventureTutorialCombatDice()
	{
		foreach (BattleCardState card in playerCards)
			card?.View?.HideActiveDiceRoll();
		foreach (BattleCardState card in cpuCards)
			card?.View?.HideActiveDiceRoll();
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

		if (TryScriptTutorialWarriorDuelResult(attacker, defender, resolved, out CombatResult lessonResult))
		{
			return lessonResult;
		}

		string attackerId = attacker.Card.Id;
		string defenderId = defender.Card.Id;
		VigorRollResult attackerRoll;
		VigorRollResult defenderRoll;

		// Primo attacco guidato: qualunque pedina del giocatore elimina sempre
		// il bersaglio da 4 mostrato dallo spotlight.
		if (attacker.BelongsToPlayer && defender.Card.Strength == 4)
		{
			attackerRoll = ScriptRoll(resolved.AttackerRoll, resolved.AttackerRoll.DieSides,
				resolved.AttackerRoll.DieSides);
			defenderRoll = ScriptRoll(resolved.DefenderRoll, 1, 1);
		}
		// 1. Mago 8 del giocatore contro Guerriero 8: 4 e 3 con vantaggio, contro 3.
		else if (IsTutorialCard(attackerId, "8-spirit-mage") && IsTutorialCard(defenderId, "8-spirit-warrior"))
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
				return $"L'attaccante e di fazione {actorFamily}, e forte contro la fazione {opponentFamily}: si applica vantaggio tirando due dadi ({roll.FirstRoll} e {roll.SecondRoll}) e si conserva il risultato piu alto ({roll.SelectedRoll}).";
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

	private RectTransform TutorialCpuStrengthFourTargetRect()
	{
		foreach (BattleCardState card in cpuCards)
		{
			if (card != null && !card.Eliminated && card.Card.Strength == 4
				&& (Object)(object)card.View != (Object)null
				&& ((Component)card.View).gameObject.activeInHierarchy)
				return card.View.RectTransform;
		}
		return null;
	}

	private bool IsAdventureTutorialStrengthFourTarget(BattleCardState card)
	{
		return card != null && card.Card.Strength == 4;
	}

	private void ShowAdventureTutorialAfterStrengthFourDefeat(BattleCardState defender)
	{
		if (!adventureScriptedTutorialActive || adventureScriptedTutorialStep != 5
			|| !IsAdventureTutorialStrengthFourTarget(defender))
			return;

		adventureScriptedTutorialStep = 6;
		ShowAdventureScriptedTutorialStep(
			"Primo nemico sconfitto",
			"Hai vinto il tiro di dado e sconfitto la pedina nemica da 4. Premi CONTINUA per proseguire.",
			null);
		SetAdventureTutorialNextButtonEnabled(enabled: true);
		SetAdventureTutorialTimelineVisible(visible: false);
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
			"Tocca una pedina schierata per aprire la scheda. Da li puoi leggere potenza, fazione, classe, abilita e vantaggi.",
			FirstPlayerBattleCardRect());
		SetAdventureTutorialNextButtonEnabled(enabled: false);
		MoveAdventureTutorialSpotlight(FirstPlayerBattleCardRect());
	}

	private IEnumerator ShowAdventureTutorialAttackSpotlightAfterBattlefieldMove()
	{
		// Il turno comincia mentre la fila del giocatore sta ancora scendendo dalla
		// posa di schieramento. Lo spotlight memorizza subito le coordinate del target,
		// quindi va creato soltanto quando pedina e pulsante ATTACCA sono fermi.
		while (playerBattlefieldRowTransitionCoroutine != null)
		{
			yield return null;
		}

		// Lascia a layout e Canvas un frame per aggiornare il rettangolo dell'azione
		// dopo l'ultima scrittura della transizione.
		yield return null;
		Canvas.ForceUpdateCanvases();
		if (!adventureScriptedTutorialActive || adventureScriptedTutorialStep != 5)
		{
			yield break;
		}

		RefreshCardActionOverlays();
		Canvas.ForceUpdateCanvases();
		MoveAdventureTutorialSpotlight(ActivePlayerAttackActionRect());
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
		return card.View.AttackActionSpotlightRect ?? card.View.RectTransform;
	}

	private RectTransform ActivePlayerSupremeActionRect()
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
		return card.View.SupremeActionSpotlightRect ?? card.View.RectTransform;
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
		return card.View.AbilityActionSpotlightRect ?? card.View.RectTransform;
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
		// Negozio e Santuario usano Canvas fullscreen con sorting order 900. Il tutorial
		// vive nella safe area e, senza un proprio Canvas, viene disegnato dietro di loro
		// anche se SetAsLastSibling e' corretto nel suo ramo della gerarchia.
		Canvas tutorialCanvas = adventureScriptedTutorialPanel.AddComponent<Canvas>();
		tutorialCanvas.overrideSorting = true;
		tutorialCanvas.sortingOrder = 1100;
		adventureScriptedTutorialPanel.AddComponent<GraphicRaycaster>();
		CanvasGroup panelCanvasGroup = adventureScriptedTutorialPanel.AddComponent<CanvasGroup>();
		// Il pannello contiene CONTINUA: se il gruppo padre non accetta input, il bottone
		// resta acceso graficamente ma Unity scarta il click prima di raggiungerlo. Lo
		// sfondo del pannello non intercetta nulla (panel.raycastTarget e' false), quindi
		// rendere interattivo il gruppo abilita soltanto i controlli figli.
		panelCanvasGroup.blocksRaycasts = true;
		panelCanvasGroup.interactable = true;

		adventureScriptedTutorialTitleText = CreateText("Adventure Scripted Tutorial Title", ((Component)panel).transform, AccardND.Battlefield.MmoUiTheme.LoreFont, 40, FontStyle.Normal, TextAnchor.MiddleLeft);
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(adventureScriptedTutorialTitleText);
		adventureScriptedTutorialTitleText.fontSize = 40;
		adventureScriptedTutorialTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		adventureScriptedTutorialTitleText.raycastTarget = false;
		SetRect(adventureScriptedTutorialTitleText.rectTransform, new Vector2(0.04f, 0.68f), new Vector2(0.68f, 0.92f));

		adventureScriptedTutorialStepText = CreateText("Adventure Scripted Tutorial Counter", ((Component)panel).transform, font, 18, (FontStyle)1, (TextAnchor)5);
		adventureScriptedTutorialStepText.color = new Color(0.64f, 0.78f, 0.86f);
		adventureScriptedTutorialStepText.raycastTarget = false;
		SetRect(adventureScriptedTutorialStepText.rectTransform, new Vector2(0.7f, 0.68f), new Vector2(0.96f, 0.92f));

		adventureScriptedTutorialBodyText = CreateText("Adventure Scripted Tutorial Body", ((Component)panel).transform, font, 30, (FontStyle)1, TextAnchor.UpperLeft);
		adventureScriptedTutorialBodyText.color = new Color(0.88f, 0.92f, 0.96f);
		adventureScriptedTutorialBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		adventureScriptedTutorialBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		adventureScriptedTutorialBodyText.resizeTextForBestFit = false;
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
			// Il pannello e' condiviso: quando c'e' un tour fuori dalla battaglia e' lui a
			// rispondere al CONTINUA, altrimenti risponde il tutorial di combattimento.
			if (TryAdvanceGuidedTourFromContinue())
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
		Canvas spotlightCanvas = ((Component)adventureScriptedTutorialSpotlight).gameObject.AddComponent<Canvas>();
		spotlightCanvas.overrideSorting = true;
		spotlightCanvas.sortingOrder = 1090;
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
			Canvas dimmerCanvas = ((Component)dimmer).gameObject.AddComponent<Canvas>();
			dimmerCanvas.overrideSorting = true;
			dimmerCanvas.sortingOrder = 1080;
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
		body = SetLocalizedAdventureTutorialCopy(title, body);
		adventureScriptedTutorialStepText.text = LocalizedAdventureTutorialStepCounter(
			Mathf.Min(adventureScriptedTutorialStep + 1, 8), 8);
		// Dall'arrivo dei D20 fino alla fine dello schieramento la timeline deve
		// restare visibile: questi passi la descrivono e il giocatore deve poter
		// seguire concretamente l'ordine delle iniziative.
		bool showDeploymentTimeline = adventureScriptedTutorialStep >= 2
			&& adventureScriptedTutorialStep <= 4;
		SetAdventureTutorialTimelineVisible(showDeploymentTimeline);
		SetAdventureTutorialNextButtonEnabled(adventureScriptedTutorialStep != 3);
		PlaceAdventureTutorialPanel(null);
		ResizeAdventureTutorialPanelForBody(body);
		StartAdventureTutorialBodyText(body);
		MoveAdventureTutorialSpotlight(null);
	}

	/// <summary>
	/// Unico ingresso per titolo e corpo del pannello tutorial condiviso.
	/// Tutti i tutorial, inclusi quelli di combattimento, passano dalle String Table qui.
	/// </summary>
	private string SetLocalizedAdventureTutorialCopy(string title, string body)
	{
		string localizedTitle = LocalizeAdventureTutorialFallback(title);
		string localizedBody = LocalizeAdventureTutorialFallback(body);
		if ((Object)(object)adventureScriptedTutorialTitleText != (Object)null)
			adventureScriptedTutorialTitleText.text = localizedTitle;
		return localizedBody;
	}

	private static string LocalizeAdventureTutorialFallback(string italian)
	{
		string localized = GameText.GetAutoLocalizedFallback(italian);
		if (!string.Equals(localized, italian, StringComparison.Ordinal)
			|| !GameText.CurrentLocaleCode.StartsWith("en", StringComparison.OrdinalIgnoreCase))
		{
			return localized;
		}

		return italian switch
		{
			"RICOMPENSA OTTENUTA" => "REWARD RECEIVED",
			"SANTUARIO SBLOCCATO" => "SANCTUARY UNLOCKED",
			"COMPRA IL LADRO" => "BUY THE THIEF",
			"IL SANTUARIO" => "THE SANCTUARY",
			"GLI ALTARI" => "THE ALTARS",
			"IL MIELE" => "HONEY",
			"TORNA IN CAMPAGNA" => "RETURN TO CAMPAIGN",
			"APRI L'AVVENTURA" => "OPEN ADVENTURE",
			"APRI I TUTORIAL" => "OPEN TUTORIALS",
			"IL SECONDO TUTORIAL" => "THE SECOND TUTORIAL",
			"IL TERZO TUTORIAL" => "THE THIRD TUTORIAL",
			"LA TUA PROSSIMA CLASSE" => "YOUR NEXT CLASS",
			"COMPRA LA CLASSE" => "BUY THE CLASS",
			"USA IL REGALO" => "USE YOUR GIFT",
			"TUTORIAL OGGETTI" => "ITEM TUTORIAL",
			"IL NEGOZIO" => "THE SHOP",
			"L'OFFERTA DEL GIORNO" => "TODAY'S OFFER",
			"UN REGALO PER IL TUTORIAL" => "A GIFT FOR THE TUTORIAL",
			"Il Santuario e' ora accessibile. Entra e usa il miele ricevuto per comprare il Mago."
				=> "The Sanctuary is now available. Enter and use the honey you received to buy the Magician.",
			"Entra nel SANTUARIO e usa i 40 vasetti ricevuti per comprare il Ladro."
				=> "Enter the SANCTUARY and use the 40 honey jars you received to buy the Thief.",
			"Questo e' il Santuario. Qui si trasforma il miele in cose permanenti: classi nuove, tecniche e reliquie. Quello che compri qui resta tuo per sempre, anche quando una run finisce male."
				=> "This is the Sanctuary. Here, honey becomes permanent upgrades: new classes, techniques, and relics. Everything you buy here remains yours, even when a run ends badly.",
			"Le CLASSI sono le pedine che potrai schierare. Le TECNICHE sono la seconda abilita' di una classe che possiedi gia'. Le RELIQUIE ampliano la bisaccia e il banco del Mercato."
				=> "CLASSES are the pawns you can deploy. TECHNIQUES are the second ability of a class you already own. RELICS expand your bag and the Market bench.",
			"Tutto qui dentro si paga in vasetti di miele. Non si trovano giocando: si guadagnano in taverna, con le quest del giorno. Durante il tutorial te li do io, giusti per quello che serve."
				=> "Everything here is paid for with honey jars. You do not find them during runs: you earn them in the Tavern by completing daily quests. During the tutorial, you receive exactly what you need.",
			"Il Mago e' stato sbloccato. Entra in CAMPAGNA per raggiungere il suo tutorial."
				=> "The Magician has been unlocked. Enter CAMPAIGN to reach its tutorial.",
			"Il Ladro e' stato sbloccato. Entra in CAMPAGNA per raggiungere il terzo tutorial."
				=> "The Thief has been unlocked. Enter CAMPAIGN to reach the third tutorial.",
			"Seleziona AVVENTURA: i tutorial delle classi si trovano qui."
				=> "Select ADVENTURE: class tutorials are found here.",
			"Seleziona AVVENTURA per tornare ai tutorial delle classi."
				=> "Select ADVENTURE to return to the class tutorials.",
			"Seleziona AVVENTURA." => "Select ADVENTURE.",
			"Apri la sezione TUTORIAL per vedere il prossimo modulo disponibile."
				=> "Open the TUTORIAL section to see the next available module.",
			"Apri la sezione TUTORIAL." => "Open the TUTORIAL section.",
			"Apri la sezione TUTORIAL per vedere la prossima lezione."
				=> "Open the TUTORIAL section to see the next lesson.",
			"Il Mago e' ora disponibile. Apri il suo tutorial per continuare il percorso."
				=> "The Magician is now available. Open its tutorial to continue your journey.",
			"Il Ladro e' ora disponibile. Apri il suo tutorial per continuare il percorso."
				=> "The Thief is now available. Open its tutorial to continue your journey.",
			"L'Empower e' nella tua scorta. Entra in CAMPAGNA per raggiungere il tutorial sugli oggetti e imparare a usarlo."
				=> "Empower is in your inventory. Enter CAMPAIGN to reach the item tutorial and learn how to use it.",
			"Apri OGGETTI: qui userai l'Empower ricevuto dal Negozio."
				=> "Open ITEMS: here you will use the Empower received from the Shop.",
			"Nel Negozio puoi comprare gli oggetti consumabili da usare durante l'Avventura. Alcuni potenziano un tiro o una stanza, altri ti salvano nei momenti piu' difficili."
				=> "In the Shop you can buy consumable items to use during Adventure. Some improve a roll or a room, while others save you in the hardest moments.",
			"Ogni giorno alcuni oggetti costano meno, e le copie in offerta sono limitate. Vale la pena passare a controllare."
				=> "Every day, some items cost less and the discounted stock is limited. It is worth checking regularly.",
			"Hai ricevuto un Empower. Aumenta di uno step il tuo dado Vigore in attacco: nel prossimo tutorial lo metteremo nella bisaccia e lo useremo sul campo."
				=> "You received an Empower. It increases your attack Vigor die by one step: in the next tutorial, you will put it in your bag and use it in the field.",
			_ => TranslateDynamicAdventureTutorialFallback(italian)
		};
	}

	private static string TranslateDynamicAdventureTutorialFallback(string italian)
	{
		if (italian.StartsWith("Hai sbloccato l'accesso al Santuario e hai ricevuto ", StringComparison.Ordinal))
		{
			string amount = italian.Substring("Hai sbloccato l'accesso al Santuario e hai ricevuto ".Length)
				.Split(' ')[0];
			return $"You unlocked access to the Sanctuary and received {amount} honey jars. To access the next tutorial, buy the Magician in the Sanctuary.";
		}
		if (italian.StartsWith("Hai completato il tutorial del Mago e hai ricevuto ", StringComparison.Ordinal))
		{
			string amount = italian.Substring("Hai completato il tutorial del Mago e hai ricevuto ".Length)
				.Split(' ')[0];
			return $"You completed the Magician tutorial and received {amount} honey jars. Use them to buy the Thief in the Sanctuary and unlock the third tutorial.";
		}
		if (italian.StartsWith("Con i vasetti che hai appena ricevuto puoi prendere ", StringComparison.Ordinal))
		{
			string className = italian.Contains("Ladro", StringComparison.Ordinal) ? "the Thief" : "the Magician";
			return $"With the honey jars you just received, you can unlock {className}. Open the CLASSES altar.";
		}
		if (italian.StartsWith("Scegli il ", StringComparison.Ordinal))
		{
			string className = italian.Contains("Ladro", StringComparison.Ordinal) ? "the Thief" : "the Magician";
			return $"Choose {className} and confirm. You have exactly enough honey jars: that is the purpose of the gift.";
		}
		return italian;
	}

	private static string LocalizedAdventureTutorialStepCounter(int current, int total) =>
		GameText.Format("tutorial.guided.step_counter", current, total);

	private void ResizeAdventureTutorialPanelForBody(string body)
	{
		if ((Object)(object)adventureScriptedTutorialPanel == (Object)null
			|| (Object)(object)adventureScriptedTutorialBodyText == (Object)null)
		{
			return;
		}

		adventureScriptedTutorialBodyText.text = body ?? string.Empty;
		Canvas.ForceUpdateCanvases();

		RectTransform panelRect = (RectTransform)adventureScriptedTutorialPanel.transform;
		float missingBodyHeight = Mathf.Max(0f,
			adventureScriptedTutorialBodyText.preferredHeight - adventureScriptedTutorialBodyText.rectTransform.rect.height);
		float maximumPanelHeight = safeAreaRoot.rect.height * 0.9f;
		float extraHeight = Mathf.Min(missingBodyHeight + (missingBodyHeight > 0f ? 16f : 0f),
			Mathf.Max(0f, maximumPanelHeight - panelRect.rect.height));
		if (extraHeight <= 0f)
		{
			return;
		}

		Vector2 offsetMin = panelRect.offsetMin;
		Vector2 offsetMax = panelRect.offsetMax;
		offsetMin.y -= extraHeight * 0.5f;
		offsetMax.y += extraHeight * 0.5f;
		panelRect.offsetMin = offsetMin;
		panelRect.offsetMax = offsetMax;
		Canvas.ForceUpdateCanvases();
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
		panelRect.anchorMin = new Vector2(0.08f, 0.39f);
		panelRect.anchorMax = new Vector2(0.82f, 0.61f);
		panelRect.offsetMin = new Vector2(0f, -81.2749f);
		panelRect.offsetMax = new Vector2(0f, 81.2752f);
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
			adventureScriptedTutorialSpotlight.rectTransform.SetParent(
				(Transform)(object)safeAreaRoot, false);
			adventureScriptedTutorialSpotlight.rectTransform.rotation = Quaternion.identity;
			SetAdventureTutorialDimmers(null);
			return;
		}
		spotlightObject.SetActive(true);
		RectTransform spotlightRect = adventureScriptedTutorialSpotlight.rectTransform;
		// Lo spotlight segue direttamente il bersaglio nella sua gerarchia. In questo modo
		// non esiste alcuna conversione fra il Canvas del Santuario e quello del tutorial:
		// posizione locale zero significa sempre il centro esatto del Mago.
		spotlightRect.SetParent(target, false);
		spotlightRect.anchorMin = new Vector2(0.5f, 0.5f);
		spotlightRect.anchorMax = new Vector2(0.5f, 0.5f);
		spotlightRect.pivot = new Vector2(0.5f, 0.5f);
		spotlightRect.anchoredPosition = Vector2.zero;
		spotlightRect.localRotation = Quaternion.identity;
		spotlightRect.localScale = Vector3.one;
		spotlightRect.sizeDelta = target.rect.size * 1.18f;
		spotlightRect.SetAsLastSibling();
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
		if (!complete)
		{
			return;
		}

		// La lezione fa parte di un modulo del percorso: e' il modulo a decidere quale
		// ricompensa riscuotere e dove riportare il giocatore.
		if (activeTutorialModuleId != null)
		{
			CompleteActiveTutorialModule();
			return;
		}

		ConfirmStartTutorialAdventureStage();
		ReturnToStart(showModeSelection: false);
		SetAccountHubHudActive(true);
		ShowAdventureChapterSelection();
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
