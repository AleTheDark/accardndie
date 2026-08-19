using AccardND.TourKit;
using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using AccardND.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// Il modulo che si sta giocando adesso, o null. Serve a sapere quale ricompensa
	/// riscuotere quando la lezione finisce: la lezione stessa non lo sa.
	/// </summary>
	private string activeTutorialModuleId;

	private void StartTutorialModule(string moduleId)
	{
		if (!TutorialModuleCatalog.Exists(moduleId))
		{
			AppendLog(TutorialModuleText("log_unknown",
				"TUTORIAL - modulo sconosciuto '{0}'.", "TUTORIAL - unknown module '{0}'.", moduleId));
			return;
		}

		activeTutorialModuleId = moduleId;
		AppendLog(TutorialModuleText("log_start",
			"TUTORIAL - avvio modulo {0}.", "TUTORIAL - starting module {0}.", moduleId));
		if (string.Equals(moduleId, TutorialModuleCatalog.Warrior, StringComparison.Ordinal)
			|| string.Equals(moduleId, TutorialModuleCatalog.Mage, StringComparison.Ordinal)
			|| string.Equals(moduleId, TutorialModuleCatalog.Rogue, StringComparison.Ordinal))
		{
			PlayTutorialMusic();
		}
		StartTutorialModuleLesson(moduleId);
	}

	/// <summary>
	/// Chiude il modulo in corso: riscuote la ricompensa dal server, rispecchia lo stato e
	/// riapre l'hub sui cancelli aggiornati. Se il server non risponde il modulo **non** si
	/// chiude: la lezione l'hai fatta, ma il percorso non avanza su una ricompensa che
	/// nessuno ha registrato, e il claim resta in coda per la riconnessione.
	/// </summary>
	private async void CompleteActiveTutorialModule()
	{
		string moduleId = activeTutorialModuleId;
		activeTutorialModuleId = null;
		if (moduleId == null)
		{
			return;
		}

		if (!ServerProgressReady)
		{
			AppendLog(TutorialModuleText("log_server_unavailable",
				"TUTORIAL - modulo {0} non registrato: server non disponibile.",
				"TUTORIAL - module {0} was not recorded: server unavailable.", moduleId));
			SetMessage(GameText.Get(GameTextKeys.Adventure.TutorialConnectionRequired));
			ReturnToTutorialIndex();
			return;
		}

		try
		{
			SinglePlayerRewardOutcome outcome = await serverProgress.ClaimTutorialModuleRewardAsync(
				moduleId, Guid.NewGuid().ToString("N"));
			MirrorServerProgress();
			AppendLog(TutorialModuleText("log_complete",
				"TUTORIAL - modulo {0} completato: {1} miele.",
				"TUTORIAL - module {0} complete: {1} honey.", moduleId, outcome.GrantedHoney));
			AnnounceTutorialModuleReward(moduleId, outcome.GrantedHoney);
			if (string.Equals(moduleId, TutorialModuleCatalog.Basics, StringComparison.Ordinal))
			{
				// "First Steps" termina dentro la stanza guidata: alla conclusione riporta
				// direttamente all'Hub invece di riaprire l'indice dei moduli.
				ShowHubFromSinglePlayer();
				return;
			}
			if (string.Equals(moduleId, TutorialModuleCatalog.Warrior, StringComparison.Ordinal))
			{
				StartWarriorSanctuaryUnlockTour(outcome.GrantedHoney);
				return;
			}
			if (string.Equals(moduleId, TutorialModuleCatalog.Mage, StringComparison.Ordinal))
			{
				StartMageSanctuaryRewardTour(outcome.GrantedHoney);
				return;
			}
			if (string.Equals(moduleId, TutorialModuleCatalog.Rogue, StringComparison.Ordinal))
			{
				// Finita la terza lezione il passo successivo e' il Negozio, che viene
				// evidenziato direttamente nell'Hub. Non riaprire l'indice dei tutorial.
				ShowHubFromSinglePlayer();
				return;
			}
		}
		catch (Exception exception)
		{
			AppendLog(TutorialModuleText("log_reward_rejected",
				"TUTORIAL - ricompensa del modulo {0} rifiutata: {1}",
				"TUTORIAL - reward for module {0} was rejected: {1}", moduleId, exception.Message));
			SetMessage(exception.Message);
		}

		ReturnToTutorialIndex();
	}

	/// <summary>
	/// Cosa dire appena finito un modulo. Il dono va raccontato per quello che e' - vasetti
	/// per una classe precisa - altrimenti sembra un guadagno, e il miele in questo gioco si
	/// guadagna solo in taverna.
	/// </summary>
	private void AnnounceTutorialModuleReward(string moduleId, int grantedHoney)
	{
		if (grantedHoney > 0)
		{
			string classId = TutorialGate.PendingPurchaseClassId(CurrentTutorialFlow());
			string className = classId == "rogue"
				? TutorialModuleText("class_thief", "il Ladro", "the Thief")
				: TutorialModuleText("class_magician", "il Mago", "the Magician");
			SetMessage(TutorialModuleText("reward_honey",
				"Ecco {0} vasetti di miele: bastano esattamente per {1}. Passa dal Santuario.",
				"Here are {0} honey jars: exactly enough for {1}. Visit the Sanctuary.",
				grantedHoney, className));
			return;
		}

		if (string.Equals(moduleId, TutorialModuleCatalog.ChapterRun, StringComparison.Ordinal))
		{
			SetMessage(TutorialModuleText("tutorial_complete_reward",
				"Tutorial completato: hai il primo capitolo e una Seconda Chance nella scorta. Da adesso il miele si guadagna in taverna, con le quest del giorno.",
				"Tutorial complete: you now have the first chapter and a Second Chance in your stash. From now on, earn honey in the Tavern by completing daily quests."));
			return;
		}

		SetMessage(TutorialModuleText("module_complete", "Modulo completato.", "Module complete."));
	}

	/// <summary>
	/// Dopo un modulo si torna dove si e' partiti: l'elenco dei moduli, con la riga
	/// successiva gia' accesa. Tornare all'hub costringerebbe a rifare tre tocchi per
	/// riprendere il percorso.
	/// </summary>
	private void ReturnToTutorialIndex()
	{
		ReturnToStart(showModeSelection: false);
		SetAccountHubHudActive(true);
		tutorialModuleIndexOpen = true;
		ShowAdventureChapterSelectionKeepingTutorialIndex();
	}

	private void ShowAdventureChapterSelectionKeepingTutorialIndex()
	{
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)adventureChapterPanel != (Object)null)
		{
			adventureChapterPanel.SetActive(true);
			adventureChapterPanel.transform.SetAsLastSibling();
		}
		RefreshAdventureChapterLayout();
		RefreshAdventureChapterHeading();
		RefreshAdventureChapterList();
	}

	/// <summary>
	/// Quale lezione parte per quale modulo. Oggi solo il primo ha la sua run scriptata: gli
	/// altri arrivano spacchettando il tutorial monolitico, e finche' non ci sono la riga
	/// resta visibile ma lo dice chiaramente invece di far partire la lezione sbagliata.
	/// </summary>
	private void StartTutorialModuleLesson(string moduleId)
	{
		// Il Guerriero ha la sua stanza guidata: si impara premendo, non leggendo.
		if (string.Equals(moduleId, TutorialModuleCatalog.Warrior, System.StringComparison.Ordinal))
		{
			StartTutorialWarriorDuel();
			return;
		}

		HeroClass? lessonClass = TutorialModuleHeroClass(moduleId);
		if (lessonClass.HasValue)
		{
			StartTutorialClassLesson(lessonClass.Value);
			return;
		}

		switch (moduleId)
		{
		case TutorialModuleCatalog.Basics:
			// La prova pratica, ultima del percorso: la run guidata dall'inizio alla fine.
			StartAdventureScriptedTutorial();
			return;
		case TutorialModuleCatalog.ChapterRun:
			StartTutorialChapterLesson();
			return;
		case TutorialModuleCatalog.ItemsAndBag:
			StartTutorialItemsLesson();
			return;
		default:
			activeTutorialModuleId = null;
			SetMessage(TutorialModuleText("module_unavailable",
				"Questo modulo non e' ancora giocabile.", "This module is not playable yet."));
			AppendLog(TutorialModuleText("log_not_implemented",
				"TUTORIAL - lezione del modulo {0} non ancora implementata.",
				"TUTORIAL - lesson for module {0} is not implemented yet.", moduleId));
			return;
		}
	}

	/// <summary>
	/// Com'e' fatto un capitolo. I numeri delle stanze non si scrivono a mano: si leggono
	/// dalla configurazione, cosi' il testo non mente se un domani si ribilancia.
	/// </summary>
	private void StartTutorialChapterLesson()
	{
		ProgressionConfiguration progression = configuration.Progression;
		int minibossRoom = Mathf.Max(1, progression.MinibossEveryRooms);
		int bossRoom = Mathf.Max(minibossRoom + 1, progression.FinalBossRoom);

		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = TutorialModuleText("chapter_doors_title", "LE PORTE", "THE DOORS"),
				Body = TutorialModuleText("chapter_doors_body",
					"Un capitolo e' una fila di stanze. Alla fine di ognuna scegli fra tre porte, e la scelta e' definitiva: non si torna indietro a prendere quella che hai lasciato.",
					"A chapter is a sequence of rooms. At the end of each room, choose one of three doors. The choice is final: you cannot return for the path you left behind.")
			},
			new GuidedTourStep
			{
				Title = TutorialModuleText("chapter_bosses_title", "MINIBOSS E BOSS", "MINIBOSS AND BOSS"),
				Body = TutorialModuleText("chapter_bosses_body",
					"Alla stanza {0} ti aspetta un miniboss. Alla stanza {1} c'e' il boss del capitolo: batterlo chiude il capitolo e apre quello dopo.",
					"A miniboss awaits in room {0}. The chapter boss awaits in room {1}: defeat it to complete the chapter and unlock the next one.",
					minibossRoom, bossRoom)
			},
			new GuidedTourStep
			{
				Title = TutorialModuleText("chapter_rooms_title", "NON SOLO MOSTRI", "MORE THAN MONSTERS"),
				Body = TutorialModuleText("chapter_rooms_body",
					"Fra una battaglia e l'altra trovi il Mercato, dove si compra e si potenzia; le stanze del tesoro, che regalano oggetti; e le Prove Lampo, piccole sfide che pagano esperienza.",
					"Between battles, you may find the Market for purchases and upgrades, treasure rooms that grant items, and Flash Trials: short challenges that reward experience.")
			},
			new GuidedTourStep
			{
				Title = TutorialModuleText("chapter_run_end_title", "UNA RUN FINISCE", "WHEN A RUN ENDS"),
				Body = TutorialModuleText("chapter_run_end_body",
					"Se la formazione cade, la run finisce e si ricomincia il capitolo da capo. Quello che resta e' permanente: esperienza dell'account, classi, oggetti comprati.",
					"If your formation falls, the run ends and the chapter starts over. Account experience, classes, and purchased items remain permanently unlocked.")
			}
		};

		StartGuidedTour(steps, CompleteActiveTutorialModule);
	}

	/// <summary>
	/// Oggetti e bisaccia. E' una lezione a schermo, non una run: la differenza fra scorta e
	/// bisaccia si spiega guardando le due cose, e la stanza in cui usare davvero un oggetto
	/// arriva con la prova finale del percorso.
	/// </summary>
	private void StartTutorialItemsLesson()
	{
		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = TutorialModuleText("items_stash_title", "LA SCORTA", "THE STASH"),
				Body = TutorialModuleText("items_stash_body",
					"Gli oggetti che compri finiscono nella scorta: sono tuoi e restano li' anche quando una run finisce.",
					"Items you buy are stored in your stash. They belong to you and remain there even when a run ends.")
			},
			new GuidedTourStep
			{
				Title = TutorialModuleText("items_bag_title", "LA BISACCIA", "THE BAG"),
				Body = TutorialModuleText("items_bag_body",
					"La bisaccia e' un'altra cosa: e' la scelta di quali oggetti della scorta portarti dietro nella prossima run. Gli slot nella bisaccia sono pochi, e si ampliano al Santuario.",
					"Your bag is different: it contains the stash items you choose to carry into the next run. Bag slots are limited and can be expanded in the Sanctuary.")
			},
			new GuidedTourStep
			{
				Title = TutorialModuleText("items_ownership_title", "POSSEDERE NON E' PORTARE", "OWNING IS NOT CARRYING"),
				Body = TutorialModuleText("items_ownership_body",
					"Un oggetto lascia la scorta solo quando lo usi davvero. Portartelo dietro senza usarlo non costa niente: alla fine della run torna al suo posto.",
					"An item leaves your stash only when you actually use it. Carrying it without using it costs nothing: it returns to the stash when the run ends.")
			},
			new GuidedTourStep
			{
				Title = TutorialModuleText("items_usage_title", "QUANDO SI USANO", "WHEN TO USE ITEMS"),
				Body = TutorialModuleText("items_usage_body",
					"Dentro una run apri la bisaccia e scegli. Alcuni oggetti agiscono subito, altri - come la Seconda Chance - non si possono usare mentre sei in battaglia.",
					"During a run, open your bag and choose an item. Some take effect immediately; others, such as Second Chance, cannot be used during battle.")
			}
		};

		StartGuidedTour(steps, CompleteActiveTutorialModule);
	}

	/// <summary>
	/// Catalogo unico dei testi appartenenti ai moduli generici del tutorial. Le chiavi
	/// tutorial.modules.* sono pronte per le String Table e hanno fallback inglesi completi.
	/// </summary>
	private static string TutorialModuleText(
		string id, string italian, string english, params object[] arguments) =>
		GameText.GetLocalizedFallback(
			"tutorial.modules." + id, italian, english, arguments);
}
}
