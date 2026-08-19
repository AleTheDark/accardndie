using AccardND.TourKit;
using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// La lezione di una classe. E' uno stampo solo, parametrico sulla classe: abilita',
	/// tecnica e aura si leggono dal motore (<see cref="AbilityManaCosts"/>,
	/// <see cref="HeroClassFamily"/>, <see cref="SupremeAbilityText"/>) invece di essere
	/// riscritte modulo per modulo. Aggiungere la lezione di una classe avanzata e' una riga
	/// di catalogo piu' i testi, non codice nuovo.
	/// </summary>
	private HeroClass? tutorialLessonHeroClass;

	/// <summary>
	/// La classe insegnata dal modulo, se e' un modulo di classe.
	/// </summary>
	private static HeroClass? TutorialModuleHeroClass(string moduleId) => moduleId switch
	{
		TutorialModuleCatalog.Warrior => HeroClass.Warrior,
		TutorialModuleCatalog.Mage => HeroClass.Mage,
		TutorialModuleCatalog.Rogue => HeroClass.Rogue,
		_ => null
	};

	private void StartTutorialClassLesson(HeroClass heroClass)
	{
		if (heroClass == HeroClass.Mage)
		{
			StartTutorialMageDuel();
			return;
		}

		tutorialLessonHeroClass = heroClass;

		// La conferma viene aperta dall'indice dei tutorial, che e' un overlay a schermo
		// intero. Le lezioni testuali in precedenza avviavano subito il GuidedTour senza
		// chiudere quell'overlay: il pannello nasceva correttamente, ma restava dietro
		// all'indice e al giocatore sembrava che ANDIAMO non facesse nulla.
		EnsureAdventureScriptedTutorialView();
		ReturnToStart(showModeSelection: false);
		tutorialModuleIndexOpen = false;
		SetAccountHubHudActive(false);
		SetBattlefieldSurfaceVisible(visible: true);
		SetCombatChromeVisible(visible: false);
		SetAdventureTutorialTimelineVisible(visible: false);
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(false);
		}
		if (heroClass == HeroClass.Rogue && !BuildTutorialRogueLessonRoom())
		{
			SetMessage(TutorialClassText("rogue_unavailable"));
			tutorialLessonHeroClass = null;
			activeTutorialModuleId = null;
			ReturnToTutorialIndex();
			return;
		}

		AppendLog($"[TUTORIAL MODULE] avvio lezione {heroClass}; modulo={activeTutorialModuleId ?? "nessuno"}.");
		StartGuidedTour(BuildTutorialClassLessonSteps(heroClass), () =>
		{
			tutorialLessonHeroClass = null;
			if (heroClass == HeroClass.Rogue)
			{
				StartTutorialRoguePractice();
				return;
			}
			CompleteActiveTutorialModule();
		});
	}

	/// <summary>
	/// Anche la lezione del Ladro si svolge su una stanza vera. Non avvia il turno: le
	/// pedine sono esempi visivi mentre il tour spiega passiva, tecnica e fazione.
	/// </summary>
	private bool BuildTutorialRogueLessonRoom()
	{
		CardDefinition player = FindTutorialCard("6-chimera-rogue");
		CardDefinition weak = FindTutorialCard("7-whitealien-mage");
		CardDefinition middle = FindTutorialCard("7-whitealien-rogue");
		CardDefinition strong = FindTutorialCard("7-whitealien-warrior");
		if ((Object)(object)player == (Object)null
			|| (Object)(object)weak == (Object)null
			|| (Object)(object)middle == (Object)null
			|| (Object)(object)strong == (Object)null)
		{
			return false;
		}

		campaignDeck = new CampaignDeckState(new List<CardDefinition>());
		currentRoomType = RoomType.Monster;
		pendingRoomDifficulty = RoomDifficulty.Easy;
		campaignScenarioId = "default";
		pendingScenarioId = "default";
		currentScenarioDisplayOverride = TutorialClassText("rogue_scenario");
		ResetScenarioRuleState();
		LoadScenario(RoomType.Any, RoomDifficulty.Any, null, "default");
		RestoreCampaignMana(10);
		if ((Object)(object)campaignZoneRect != (Object)null)
			((Component)campaignZoneRect).gameObject.SetActive(false);

		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);

		BattleCardState playerState = AddCard(playerCards, playerRow, player, belongsToPlayer: true, 0);
		if (playerState != null)
			playerState.Initiative = 20;
		initialPlayerFormation.Clear();
		initialPlayerFormation.Add(player);
		initialCpuFormation.Clear();
		survivingCpuFormation.Clear();

		CardDefinition[] enemies = { weak, middle, strong };
		for (int index = 0; index < enemies.Length; index++)
		{
			BattleCardState state = AddCard(cpuCards, cpuRow, enemies[index], belongsToPlayer: false, index);
			if (state != null)
				state.Initiative = 3 - index;
			initialCpuFormation.Add(enemies[index]);
		}

		ApplyResponsiveLayout();
		RestoreBattlefieldCardVisibility();
		return true;
	}

	/// <summary>
	/// Le quattro tappe, sempre nello stesso ordine: mana (solo la prima volta), abilita',
	/// tecnica, aura. La tappa dell'abilita' cambia forma quando la classe non ne ha una da
	/// attivare - Ladro e Barbaro - ed e' proprio quello il momento in cui si insegna che le
	/// passive esistono.
	/// </summary>
	private List<GuidedTourStep> BuildTutorialClassLessonSteps(HeroClass heroClass)
	{
		var steps = new List<GuidedTourStep>();
		string className = CardRulesGlossary.HeroClassName(heroClass);
		ClassFamily family = HeroClassFamily.Of(heroClass);

		if (heroClass == HeroClass.Warrior)
		{
			// Il mana entra qui: e' la prima classe con un'abilita' da pagare, e spiegarlo
			// prima - quando non c'e' niente da comprare col mana - non attaccherebbe.
			steps.Add(new GuidedTourStep
			{
				Title = TutorialClassText("mana_title"),
				Body = TutorialClassText("mana_body")
			});
		}

		steps.Add(ManaActionPolicy.HasActivatablePrimary(heroClass)
			? TutorialActiveAbilityStep(heroClass, className)
			: TutorialPassiveAbilityStep(heroClass, className));

		steps.Add(new GuidedTourStep
		{
			Title = TutorialClassText("technique_title"),
			Body = TutorialSupremeLessonText(heroClass, className)
		});

		steps.Add(new GuidedTourStep
		{
			Title = TutorialClassText("aura_title"),
			Body = TutorialAuraLessonText(heroClass, className, family)
		});

		if (heroClass == HeroClass.Rogue)
		{
			steps.Add(new GuidedTourStep
			{	
				Title = TutorialClassText("faction_triangle_title"),
				Body = TutorialClassText("faction_triangle_body")
			});
			steps.Add(new GuidedTourStep
			{
				Title = TutorialClassText("target_colors_title"),
				Body = TutorialClassText("target_colors_body")
			});
		}

		return steps;
	}

	private GuidedTourStep TutorialActiveAbilityStep(HeroClass heroClass, string className)
	{
		int cost = AbilityManaCosts.Primary(heroClass);
		string effect = heroClass switch
		{
			HeroClass.Warrior => TutorialClassText("warrior_primary_effect"),
			HeroClass.Mage => TutorialClassText("mage_primary_effect"),
			_ => TutorialClassText("generic_primary_effect")
		};
		string weight = cost >= 5
			? TutorialClassText("expensive_ability_note")
			: string.Empty;

		return new GuidedTourStep
		{
			Title = TutorialClassText("ability_title"),
			Body = TutorialClassText("active_ability_body",
				className, effect, cost, weight)
		};
	}

	/// <summary>
	/// La tappa che vale il modulo del Ladro: non c'e' niente da premere, e va detto.
	/// </summary>
	private GuidedTourStep TutorialPassiveAbilityStep(HeroClass heroClass, string className)
	{
		string effect = heroClass switch
		{
			HeroClass.Rogue => TutorialClassText("rogue_passive_effect"),
			HeroClass.Barbarian => TutorialClassText("barbarian_passive_effect"),
			_ => TutorialClassText("generic_passive_effect")
		};

		return new GuidedTourStep
		{
			Title = TutorialClassText("passive_title"),
			Body = TutorialClassText("passive_body",
				className, effect)
		};
	}

	private string TutorialSupremeLessonText(HeroClass heroClass, string className)
	{
		int cost = AbilityManaCosts.Supreme(heroClass);
		string name = CardRulesGlossary.SupremeName(heroClass);
		string description = CardRulesGlossary.SupremeDescription(heroClass);
		// L'avviso viene prima, non dopo: provare un potere e scoprire solo alla fine di non
		// possederlo e' l'unico punto del percorso capace di lasciare un'impressione brutta.
		return TutorialClassText("supreme_body",
			className, name, cost, description);
	}

	private string TutorialAuraLessonText(HeroClass heroClass, string className, ClassFamily family)
	{
		string familyName = CardRulesGlossary.ClassFamilyName(family);
		ClassFamily beaten = TutorialFamilyBeatenBy(family);
		ClassFamily beatenBy = TutorialFamilyThatBeats(family);

		string core = TutorialClassText("aura_body",
			className, familyName, CardRulesGlossary.ClassFamilyName(beaten), CardRulesGlossary.ClassFamilyName(beatenBy));

		return core;
	}

	private static ClassFamily TutorialFamilyBeatenBy(ClassFamily family) => family switch
	{
		ClassFamily.Might => ClassFamily.Cunning,
		ClassFamily.Cunning => ClassFamily.Magic,
		_ => ClassFamily.Might
	};

	private static ClassFamily TutorialFamilyThatBeats(ClassFamily family) => family switch
	{
		ClassFamily.Might => ClassFamily.Magic,
		ClassFamily.Cunning => ClassFamily.Might,
		_ => ClassFamily.Cunning
	};

	/// <summary>
	/// Catalogo centralizzato di tutto il testo della lezione di classe. Le chiavi sono
	/// stabili e pronte per le String Table; i fallback inglesi evitano testi italiani
	/// anche prima della prossima sincronizzazione degli asset di localizzazione.
	/// </summary>
	private static string TutorialClassText(string id, params object[] arguments)
	{
		if (!TutorialClassTextCatalog.TryGet(id, out TutorialClassTextCatalog.Entry entry))
			return TutorialClassTextCatalog.KeyFor(id);

		return GameText.GetLocalizedFallback(entry.Key, entry.Italian, entry.English, arguments);
	}

	// ---- Lezione pratica del Mago ------------------------------------------------

	private enum TutorialMageDuelStep
	{
		Intro,
		BaseAttack,
		Ability,
		AttackAfterAbility,
		Supreme,
		Done
	}

	private const string TutorialMagePlayerId = "6-chimera-mage";
	private const string TutorialMageBaseTargetId = "2-goblin-mage";
	private const string TutorialMageAbilityTargetId = "4-animal-mage";
	private static readonly string[] TutorialMageSupremeWaveIds =
	{
		"2-goblin-mage", "3-skeleton-mage", "4-animal-mage"
	};

	private bool tutorialMageDuelActive;
	private TutorialMageDuelStep tutorialMageDuelStep;

	private void StartTutorialMageDuel()
	{
		tutorialMageDuelActive = true;
		tutorialMageDuelStep = TutorialMageDuelStep.Intro;
		tutorialWarriorDuelActionUnlocked = false;
		adventureScriptedTutorialActive = true;
		adventureScriptedTutorialStepAcknowledged = false;
		adventureScriptedTutorialPendingTarget = null;

		EnsureAdventureScriptedTutorialView();
		ReturnToStart(showModeSelection: false);
		tutorialModuleIndexOpen = false;
		SetAccountHubHudActive(false);
		SetBattlefieldSurfaceVisible(visible: true);
		SetCombatChromeVisible(visible: true);
		SetAdventureTutorialTimelineVisible(visible: false);
		if ((Object)(object)adventureChapterPanel != (Object)null)
			adventureChapterPanel.SetActive(false);

		if (!BuildTutorialMageDuelRoom())
		{
			SetMessage(TutorialClassText("mage_unavailable"));
			EndTutorialMageDuel(complete: false);
			return;
		}

		StartTutorialWarriorPawnEntrance();
		ShowTutorialMageStep(
			TutorialClassText("mage_intro_title"),
			TutorialClassText("mage_intro_body"),
			continueEnabled: true);
		AppendLog("[TUTORIAL MODULE] stanza pratica del Mago avviata.");
	}

	private bool BuildTutorialMageDuelRoom()
	{
		CardDefinition player = FindTutorialCard(TutorialMagePlayerId);
		CardDefinition first = FindTutorialCard(TutorialMageBaseTargetId);
		CardDefinition second = FindTutorialCard(TutorialMageAbilityTargetId);
		if ((Object)(object)player == (Object)null
			|| (Object)(object)first == (Object)null
			|| (Object)(object)second == (Object)null)
			return false;

		campaignDeck = new CampaignDeckState(new List<CardDefinition>());
		currentRoomType = RoomType.Monster;
		pendingRoomDifficulty = RoomDifficulty.Easy;
		campaignScenarioId = "fog";
		pendingScenarioId = "fog";
		currentScenarioDisplayOverride = TutorialClassText("mage_scenario");
		ResetScenarioRuleState();
		LoadScenario(RoomType.Any, RoomDifficulty.Any, null, "fog");
		RestoreCampaignMana(10);
		if ((Object)(object)campaignZoneRect != (Object)null)
			((Component)campaignZoneRect).gameObject.SetActive(false);

		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);
		BattleCardState playerState = AddCard(playerCards, playerRow, player, belongsToPlayer: true, 0);
		if (playerState != null)
			playerState.Initiative = 20;

		initialPlayerFormation.Clear();
		initialPlayerFormation.Add(player);
		initialCpuFormation.Clear();
		survivingCpuFormation.Clear();
		CardDefinition[] enemies = { first, second };
		for (int index = 0; index < enemies.Length; index++)
		{
			BattleCardState state = AddCard(cpuCards, cpuRow, enemies[index], belongsToPlayer: false, index);
			if (state != null)
				state.Initiative = 2 - index;
			initialCpuFormation.Add(enemies[index]);
		}

		ApplyResponsiveLayout();
		RestoreBattlefieldCardVisibility();
		deploymentInitiativesReady = true;
		StartBattle();
		return true;
	}

	private bool AdvanceTutorialMageDuel(AdventureTutorialAction action)
	{
		if (!tutorialMageDuelActive)
			return false;

		switch (tutorialMageDuelStep)
		{
		case TutorialMageDuelStep.Intro:
			if (action == AdventureTutorialAction.NextPressed)
			{
				tutorialMageDuelStep = TutorialMageDuelStep.BaseAttack;
				ShowTutorialMageStep(
					TutorialClassText("mage_base_attack_title"),
					TutorialClassText("mage_base_attack_body"),
					continueEnabled: true);
			}
			return true;

		case TutorialMageDuelStep.BaseAttack:
			if (action == AdventureTutorialAction.NextPressed)
			{
				UnlockTutorialMageAction(ActivePlayerAttackActionRect());
			}
			else if (action == AdventureTutorialAction.AttackPressed)
			{
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialMageBaseTargetId));
			}
			else if (action == AdventureTutorialAction.PlayerTurnStarted)
			{
				tutorialMageDuelStep = TutorialMageDuelStep.Ability;
				ShowTutorialMageStep(
					TutorialClassText("mage_ability_title"),
					TutorialClassText("mage_ability_body",
						AbilityManaCosts.Primary(HeroClass.Mage)),
					continueEnabled: true);
			}
			return true;

		case TutorialMageDuelStep.Ability:
			if (action == AdventureTutorialAction.NextPressed)
			{
				UnlockTutorialMageAction(ActivePlayerAbilityActionRect());
			}
			else if (action == AdventureTutorialAction.AbilityPressed)
			{
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialMageAbilityTargetId));
			}
			else if (action == AdventureTutorialAction.EnemyTargeted)
			{
				tutorialMageDuelStep = TutorialMageDuelStep.AttackAfterAbility;
				ShowTutorialMageStep(
					TutorialClassText("mage_exploit_penalty_title"),
					TutorialClassText("mage_exploit_penalty_body"),
					continueEnabled: true);
			}
			return true;

		case TutorialMageDuelStep.AttackAfterAbility:
			if (action == AdventureTutorialAction.NextPressed)
			{
				UnlockTutorialMageAction(ActivePlayerAttackActionRect());
			}
			else if (action == AdventureTutorialAction.AttackPressed)
			{
				MoveAdventureTutorialSpotlight(TutorialWarriorEnemyRect(TutorialMageAbilityTargetId));
			}
			else if (action == AdventureTutorialAction.BattleFinished)
			{
				SpawnTutorialMageSupremeWave();
				tutorialMageDuelStep = TutorialMageDuelStep.Supreme;
				ShowTutorialMageStep(
					TutorialClassText("mage_supreme_title"),
					TutorialClassText("mage_supreme_body",
						AbilityManaCosts.Supreme(HeroClass.Mage)),
					continueEnabled: true);
			}
			return true;

		case TutorialMageDuelStep.Supreme:
			if (action == AdventureTutorialAction.NextPressed)
			{
				UnlockTutorialMageAction(ActivePlayerSupremeActionRect());
			}
			else if (action == AdventureTutorialAction.SupremeUsed)
			{
				adventureScriptedTutorialPanel.SetActive(false);
				MoveAdventureTutorialSpotlight(null);
			}
			else if (action == AdventureTutorialAction.BattleFinished)
			{
				tutorialMageDuelStep = TutorialMageDuelStep.Done;
				ShowTutorialMageStep(
					TutorialClassText("mage_complete_title"),
					TutorialClassText("mage_complete_body"),
					continueEnabled: true);
			}
			return true;

		case TutorialMageDuelStep.Done:
			if (action == AdventureTutorialAction.NextPressed)
				EndTutorialMageDuel(complete: true);
			return true;
		}

		return true;
	}

	private void UnlockTutorialMageAction(RectTransform target)
	{
		tutorialWarriorDuelActionUnlocked = true;
		adventureScriptedTutorialPanel.SetActive(false);
		SetAdventureTutorialNextButtonEnabled(enabled: false);
		RefreshCardActionOverlays();
		MoveAdventureTutorialSpotlight(target);
	}

	private void ShowTutorialMageStep(string title, string body, bool continueEnabled)
	{
		tutorialWarriorDuelActionUnlocked = false;
		EnsureAdventureScriptedTutorialView();
		adventureScriptedTutorialPanel.SetActive(true);
		adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		SetMessagePanelVisibleDuringAdventureTutorial(visible: false);
		if ((Object)(object)adventureScriptedTutorialTitleText != (Object)null)
			adventureScriptedTutorialTitleText.text = title;
		adventureScriptedTutorialStepText.text = LocalizedAdventureTutorialStepCounter(
			(int)tutorialMageDuelStep + 1, (int)TutorialMageDuelStep.Done + 1);
		PlaceAdventureTutorialPanel(null);
		ResizeAdventureTutorialPanelForBody(body);
		StartAdventureTutorialBodyText(body);
		SetAdventureTutorialNextButtonEnabled(continueEnabled);
		MoveAdventureTutorialSpotlight(null);
		RefreshCardActionOverlays();
	}

	private bool TutorialMageDuelAllowsAttack() => tutorialWarriorDuelActionUnlocked
		&& tutorialMageDuelStep is TutorialMageDuelStep.BaseAttack or TutorialMageDuelStep.AttackAfterAbility;

	private bool TutorialMageDuelAllowsAbility() => tutorialWarriorDuelActionUnlocked
		&& tutorialMageDuelStep == TutorialMageDuelStep.Ability;

	private bool TutorialMageDuelAllowsSupreme() => tutorialWarriorDuelActionUnlocked
		&& tutorialMageDuelStep == TutorialMageDuelStep.Supreme;

	private bool TutorialMageDuelAllowsEnemyTarget(BattleCardState target)
	{
		if (!tutorialWarriorDuelActionUnlocked || target == null)
			return false;
		string expectedId = tutorialMageDuelStep switch
		{
			TutorialMageDuelStep.BaseAttack => TutorialMageBaseTargetId,
			TutorialMageDuelStep.Ability => TutorialMageAbilityTargetId,
			TutorialMageDuelStep.AttackAfterAbility => TutorialMageAbilityTargetId,
			_ => null
		};
		return !string.IsNullOrEmpty(expectedId) && IsTutorialCard(target.Card.Id, expectedId);
	}

	private bool TryScriptTutorialMageDuelResult(
		BattleCardState attacker,
		BattleCardState defender,
		CombatResult resolved,
		out CombatResult scripted)
	{
		scripted = resolved;
		if (!tutorialMageDuelActive || attacker == null || defender == null)
			return false;
		if (!attacker.BelongsToPlayer)
		{
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 1, 0, resolved.DefenderRoll.DieSides);
			return true;
		}
		if (IsTutorialCard(defender.Card.Id, TutorialMageBaseTargetId))
		{
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 3, 0, 1);
			return true;
		}
		if (IsTutorialCard(defender.Card.Id, TutorialMageAbilityTargetId))
		{
			scripted = BuildTutorialDuelResult(attacker, defender, resolved, 2, 0, 1);
			return true;
		}
		return false;
	}

	private void SpawnTutorialMageSupremeWave()
	{
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(cpuRow);
		initialCpuFormation.Clear();
		survivingCpuFormation.Clear();
		for (int index = 0; index < TutorialMageSupremeWaveIds.Length; index++)
		{
			CardDefinition enemy = FindTutorialCard(TutorialMageSupremeWaveIds[index]);
			if ((Object)(object)enemy == (Object)null)
				continue;
			BattleCardState state = AddCard(cpuCards, cpuRow, enemy, belongsToPlayer: false, index);
			if (state != null)
				state.Initiative = 3 - index;
			initialCpuFormation.Add(enemy);
		}
		RestoreCampaignMana(10);
		ApplyResponsiveLayout();
		RestoreBattlefieldCardVisibility();
		deploymentInitiativesReady = true;
		StartBattle();
		StartTutorialWarriorPawnEntrance();
	}

	private void EndTutorialMageDuel(bool complete)
	{
		tutorialMageDuelActive = false;
		tutorialWarriorDuelActionUnlocked = false;
		adventureScriptedTutorialActive = false;
		SetAdventureTutorialTimelineVisible(visible: false);
		SetMessagePanelVisibleDuringAdventureTutorial(visible: !complete);
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
			adventureScriptedTutorialPanel.SetActive(false);
		MoveAdventureTutorialSpotlight(null);
		if (complete)
		{
			CompleteActiveTutorialModule();
			return;
		}
		activeTutorialModuleId = null;
		ReturnToTutorialIndex();
	}
}
}
