using System;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// L'elenco dei moduli occupa la stessa schermata dei capitoli invece di avere un
	/// pannello suo: stessa griglia, stesso ritorno all'hub, stesso aspetto. Una seconda
	/// schermata quasi identica sarebbe solo un altro layout da tenere allineato.
	/// </summary>
	private bool tutorialModuleIndexOpen;

	private void OpenTutorialModuleIndex()
	{
		tutorialModuleIndexOpen = true;
		if ((Object)(object)adventureChapterBackButton != (Object)null)
			adventureChapterBackButton.gameObject.SetActive(true);
		RefreshAdventureChapterHeading();
		RefreshAdventureChapterList();
	}

	private void CloseTutorialModuleIndex()
	{
		if (!tutorialModuleIndexOpen)
		{
			return;
		}
		tutorialModuleIndexOpen = false;
		if ((Object)(object)adventureChapterBackButton != (Object)null)
			adventureChapterBackButton.gameObject.SetActive(false);
		RefreshAdventureChapterHeading();
		RefreshAdventureChapterList();
	}

	private void RefreshAdventureChapterHeading()
	{
		if ((Object)(object)adventureChapterHeadingText == (Object)null)
		{
			return;
		}
		if (tutorialModuleIndexOpen)
		{
			string heading = GameText.GetLocalizedFallback(
				GameTextKeys.Adventure.TutorialIndexTitle,
				"TUTORIAL", "TUTORIALS", "TUTORIALS", "TUTORIALES", "TUTORIELS");
			AccardND.Battlefield.EditableRuntimeText.BindLocalized(
				adventureChapterHeadingText, GameTextKeys.Adventure.TutorialIndexTitle, heading);
			return;
		}
		SetLocalizedText(adventureChapterHeadingText, GameTextKeys.Adventure.Title, "AVVENTURA");
	}

	/// <summary>
	/// Le righe dei moduli. I moduli conclusi mostrano lo stato di completamento; gli altri
	/// non aggiungono indicazioni testuali sotto il sottotitolo.
	/// Il modulo corrente e' l'unico giocabile - il percorso e' una fila, non un menu - ma
	/// quelli gia' fatti restano riaprbili, perche' una lezione si puo' voler rivedere.
	/// </summary>
	private void BuildTutorialModuleRows()
	{
		var completed = singlePlayerProgressService?.Progress?.completedTutorialModules;
		string nextModuleId = TutorialModuleCatalog.NextModule(completed);

		foreach (string moduleId in TutorialModuleCatalog.All)
		{
			string id = moduleId;
			bool done = TutorialModuleCatalog.IsCompleted(completed, id);
			bool current = string.Equals(id, nextModuleId, StringComparison.Ordinal);
			bool prerequisiteSatisfied = TutorialModulePrerequisiteSatisfied(id);
			bool playableCurrent = current && prerequisiteSatisfied;
			(string title, string subtitle) = TutorialModuleDisplayText(id);

			string status = done
				? GameText.GetLocalizedFallback(
					GameTextKeys.Campaign.ChapterCompleted,
					"completato", "completed", "abgeschlossen", "completado", "terminé")
				: current && !prerequisiteSatisfied
					? GameText.GetLocalizedFallback(
						GameTextKeys.Adventure.TutorialModuleVisitShopStatus,
						"VISITA PRIMA IL NEGOZIO", "VISIT THE SHOP FIRST", "BESUCHE ZUERST DEN SHOP",
						"VISITA PRIMERO LA TIENDA", "VISITEZ D'ABORD LA BOUTIQUE")
					: string.Empty;

			// "locked" mette il velo leggero, perche' e' pensato per il lucchetto dei capitoli
			// che e' gia' scuro di suo. La copertina di un modulo e' chiara: qui serve il velo
			// pieno, quello di "aperto ma non giocabile", altrimenti i moduli ancora da fare
			// sembrano disponibili quanto quello corrente.
			CreateAdventureRow(
				id,
				title,
				subtitle,
				status,
				available: done || playableCurrent,
				locked: false,
				TutorialModuleCoverSprite(id),
				() => OnTutorialModuleRowPressed(id, done, playableCurrent),
				highlighted: playableCurrent);
		}
	}

	private bool TutorialModulePrerequisiteSatisfied(string moduleId)
	{
		// Dopo il Ladro si apre il Negozio e il suo giro guidato fa parte del percorso:
		// OGGETTI non puo' essere avviato dalla lista rimasta aperta aggirando quel gate.
		if (string.Equals(moduleId, TutorialModuleCatalog.ItemsAndBag, StringComparison.Ordinal))
			return CurrentTutorialFlow().ShopTourSeen;
		return true;
	}

	private (string Title, string Subtitle) TutorialModuleDisplayText(string moduleId)
	{
		(string fallbackTitle, string fallbackSubtitle) = TutorialModuleCatalog.DisplayText(moduleId);
		return moduleId switch
		{
			TutorialModuleCatalog.Warrior => (
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleTitle(moduleId), fallbackTitle,
					"THE WARRIOR", "DER KRIEGER", "EL GUERRERO", "LE GUERRIER"),
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleSubtitle(moduleId), fallbackSubtitle,
					"ABILITY, TECHNIQUE AND AURA", "FÄHIGKEIT, TECHNIK UND AURA",
					"HABILIDAD, TÉCNICA Y AURA", "CAPACITÉ, TECHNIQUE ET AURA")),
			TutorialModuleCatalog.Mage => (
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleTitle(moduleId), fallbackTitle,
					"THE MAGICIAN", "DER MAGIER", "EL MAGO", "LE MAGICIEN"),
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleSubtitle(moduleId), fallbackSubtitle,
					"ABILITY, TECHNIQUE AND AURA", "FÄHIGKEIT, TECHNIK UND AURA",
					"HABILIDAD, TÉCNICA Y AURA", "CAPACITÉ, TECHNIQUE ET AURA")),
			TutorialModuleCatalog.Rogue => (
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleTitle(moduleId), fallbackTitle,
					"THE THIEF", "DER DIEB", "EL LADRÓN", "LE VOLEUR"),
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleSubtitle(moduleId), fallbackSubtitle,
					"PASSIVES AND FACTION TRIANGLE", "PASSIVE FÄHIGKEITEN UND FRAKTIONSDREIECK",
					"PASIVAS Y TRIÁNGULO DE FACCIONES", "PASSIFS ET TRIANGLE DES FACTIONS")),
			TutorialModuleCatalog.ItemsAndBag => (
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleTitle(moduleId), fallbackTitle,
					"ITEMS", "GEGENSTÄNDE", "OBJETOS", "OBJETS"),
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleSubtitle(moduleId), fallbackSubtitle,
					"CONSUMABLES AND BAG", "VERBRAUCHSGÜTER UND TASCHE",
					"CONSUMIBLES Y BOLSA", "CONSOMMABLES ET SAC")),
			TutorialModuleCatalog.ChapterRun => (
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleTitle(moduleId), fallbackTitle,
					"A CHAPTER", "EIN KAPITEL", "UN CAPÍTULO", "UN CHAPITRE"),
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleSubtitle(moduleId), fallbackSubtitle,
					"ROOMS, MINIBOSSES AND BOSSES", "RÄUME, MINIBOSSE UND BOSSE",
					"SALAS, MINIJEFES Y JEFES", "SALLES, MINI-BOSS ET BOSS")),
			TutorialModuleCatalog.Basics => (
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleTitle(moduleId), fallbackTitle,
					"FIRST STEPS", "ERSTE SCHRITTE", "PRIMEROS PASOS", "PREMIERS PAS"),
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleSubtitle(moduleId), fallbackSubtitle,
					"THE FIELD TRIAL", "DIE FELDPRÜFUNG", "LA PRUEBA DE CAMPO", "L'ÉPREUVE SUR LE TERRAIN")),
			_ => (fallbackTitle, fallbackSubtitle)
		};
	}

	/// <summary>
	/// La copertina del modulo. Se l'illustrazione dedicata non c'e' ancora si ripiega su
	/// quella del tutorial, che e' sempre presente: una riga senza immagine sfonderebbe la
	/// griglia molto piu' di una copertina ripetuta.
	/// </summary>
	private Sprite TutorialModuleCoverSprite(string moduleId)
	{
		Sprite dedicated = LoadSpriteResource("UI/Tutorial/" + moduleId);
		return (Object)(object)dedicated != (Object)null
			? dedicated
			: LoadSpriteResource("UI/tutorial_chapter");
	}

	private void OnTutorialModuleRowPressed(string moduleId, bool done, bool current)
	{
		if (!done && !TutorialModulePrerequisiteSatisfied(moduleId))
		{
			SetMessage(GameText.GetLocalizedFallback(
				GameTextKeys.Adventure.TutorialModuleVisitShopFirst,
				"Prima vai al Negozio e completa la sua visita guidata.",
				"First visit the Shop and complete its guided tour.",
				"Besuche zuerst den Shop und schließe seine Führung ab.",
				"Primero visita la Tienda y completa su recorrido guiado.",
				"Visitez d'abord la Boutique et terminez sa visite guidée."));
			return;
		}
		if (!done && !current)
		{
			string blockingModuleId = TutorialModuleCatalog.NextModule(
				singlePlayerProgressService?.Progress?.completedTutorialModules);
			SetMessage(blockingModuleId == null
				? GameText.GetLocalizedFallback(
					GameTextKeys.Adventure.TutorialModuleOpensLater,
					"Questo modulo si apre più avanti.", "This module unlocks later.",
					"Dieses Modul wird später freigeschaltet.", "Este módulo se desbloquea más adelante.",
					"Ce module se débloquera plus tard.")
				: GameText.GetLocalizedFallback(
					GameTextKeys.Adventure.TutorialModuleCompleteFirst,
					"Prima completa: {0}.", "Complete this first: {0}.", "Schließe zuerst {0} ab.",
					"Completa primero: {0}.", "Terminez d'abord : {0}.",
					TutorialModuleDisplayText(blockingModuleId).Title));
			return;
		}
		ShowTutorialModuleConfirmPopup(moduleId, alreadyDone: done);
	}

	/// <summary>
	/// Il popup di conferma e' quello condiviso con capitoli e vecchio tutorial: titolo e
	/// corpo vengono ribindati alle chiavi del modulo corrente, cosi' restano centralizzati
	/// e seguono la lingua selezionata anche dopo che la UI e' stata costruita.
	/// </summary>
	private void ShowTutorialModuleConfirmPopup(string moduleId, bool alreadyDone)
	{
		if ((Object)(object)adventureTutorialConfirmPopup == (Object)null)
		{
			StartTutorialModule(moduleId);
			return;
		}

		(string title, string subtitle) = TutorialModuleDisplayText(moduleId);
		if ((Object)(object)adventureTutorialConfirmTitleText != (Object)null)
		{
			AccardND.Battlefield.EditableRuntimeText.BindLocalized(
				adventureTutorialConfirmTitleText,
				GameTextKeys.Adventure.TutorialModuleTitle(moduleId),
				title);
		}
		if ((Object)(object)adventureTutorialConfirmBodyText != (Object)null)
		{
			string body = alreadyDone
				? GameText.GetLocalizedFallback(
					GameTextKeys.Adventure.TutorialModuleReplayBody,
					"{0}. L'hai gia' completato: puoi rigiocarlo, ma la ricompensa e' gia' stata consegnata.",
					"{0}. You already completed it: you can play it again, but the reward has already been granted.",
					"{0}. Du hast es bereits abgeschlossen: Du kannst es erneut spielen, aber die Belohnung wurde schon vergeben.",
					"{0}. Ya lo has completado: puedes volver a jugarlo, pero la recompensa ya fue entregada.",
					"{0}. Vous l'avez déjà terminé : vous pouvez le rejouer, mais la récompense a déjà été accordée.",
					subtitle)
				: TutorialModuleIntroText(moduleId, subtitle);
			AccardND.Battlefield.EditableRuntimeText.BindLocalized(
				adventureTutorialConfirmBodyText,
				alreadyDone
					? GameTextKeys.Adventure.TutorialModuleReplayBody
					: GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
				body);
		}
		SetAdventureRewardImagesVisible(false, false);
		adventureConfirmAction = () => StartTutorialModule(moduleId);
		adventureTutorialConfirmPopup.SetActive(true);
		adventureTutorialConfirmPopup.transform.SetAsLastSibling();
	}

	private string TutorialModuleIntroText(string moduleId, string subtitle)
	{
		return moduleId switch
		{
			TutorialModuleCatalog.Basics =>
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
					"La prova pratica: una run guidata dall'inizio alla fine, con me che ti indico cosa toccare. Alla fine ricevi il primo capitolo e la Seconda Chance.",
					"The field trial: a guided run from start to finish, with instructions on what to tap. At the end, you receive the first chapter and a Second Chance.",
					"Die Feldprüfung: ein geführter Lauf von Anfang bis Ende. Am Schluss erhältst du das erste Kapitel und eine Zweite Chance.",
					"La prueba de campo: una partida guiada de principio a fin. Al terminar recibirás el primer capítulo y una Segunda Oportunidad.",
					"L'épreuve sur le terrain : une partie guidée du début à la fin. Vous recevrez ensuite le premier chapitre et une Seconde Chance."),
			TutorialModuleCatalog.Warrior =>
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
					"Si comincia da qui: il mana, l'abilita' del Guerriero e la sua tecnica. Alla fine il Guerriero e' tuo, si apre il Santuario e ricevi i vasetti per la classe successiva.",
					"Start here: learn about mana, the Warrior's ability, and his technique. At the end, the Warrior is yours, the Sanctuary opens, and you receive honey jars for the next class.",
					"Beginne hier: Lerne Mana, die Fähigkeit und die Technik des Kriegers kennen. Danach gehört der Krieger dir, das Heiligtum öffnet sich und du erhältst Honiggläser für die nächste Klasse.",
					"Empieza aquí: aprende sobre el maná, la habilidad del Guerrero y su técnica. Al final, el Guerrero será tuyo, se abrirá el Santuario y recibirás tarros de miel para la siguiente clase.",
					"Commencez ici : découvrez le mana, la capacité du Guerrier et sa technique. À la fin, le Guerrier sera à vous, le Sanctuaire s'ouvrira et vous recevrez des pots de miel pour la classe suivante."),
			TutorialModuleCatalog.Mage =>
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
					"L'abilita' del Mago, la Palla di fuoco e l'aura dei Magici. Alla fine ricevi i vasetti per la classe successiva.",
					"Learn the Magician's ability, Fireball, and the Magic faction aura. At the end, you receive honey jars for the next class.",
					"Lerne die Fähigkeit des Magiers, den Feuerball und die Aura der magischen Fraktion kennen. Danach erhältst du Honiggläser für die nächste Klasse.",
					"Aprende la habilidad del Mago, Bola de Fuego y el aura de la facción Mágica. Al final recibirás tarros de miel para la siguiente clase.",
					"Découvrez la capacité du Magicien, Boule de feu et l'aura de la faction Magique. À la fin, vous recevrez des pots de miel pour la classe suivante."),
			TutorialModuleCatalog.Rogue =>
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
					"Le abilita' passive, Ruba potenziamenti e il triangolo completo delle fazioni. Alla fine si apre il Negozio.",
					"Learn about passive abilities, Steal Buffs, and the complete faction triangle. At the end, the Shop opens.",
					"Lerne passive Fähigkeiten, Verstärkungen stehlen und das vollständige Fraktionsdreieck kennen. Danach öffnet sich der Shop.",
					"Aprende sobre habilidades pasivas, Robar mejoras y el triángulo completo de facciones. Al final se abrirá la Tienda.",
					"Découvrez les capacités passives, Vol de bonus et le triangle complet des factions. À la fin, la Boutique s'ouvrira."),
			TutorialModuleCatalog.ItemsAndBag =>
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
					"Come si usano i consumabili e cosa distingue la scorta dalla bisaccia.",
					"Learn how to use consumables and the difference between your inventory and your bag.",
					"Lerne, wie Verbrauchsgegenstände verwendet werden und was Vorrat und Tasche unterscheidet.",
					"Aprende a usar consumibles y la diferencia entre el inventario y la bolsa.",
					"Apprenez à utiliser les consommables et à distinguer la réserve du sac."),
			TutorialModuleCatalog.ChapterRun =>
				GameText.GetLocalizedFallback(GameTextKeys.Adventure.TutorialModuleIntro(moduleId),
					"Com'e' fatto un capitolo: le porte, il miniboss, il boss e le stanze che non sono battaglie.",
					"Learn how a chapter works: doors, the miniboss, the boss, and rooms that are not battles.",
					"Lerne den Aufbau eines Kapitels kennen: Türen, Miniboss, Boss und Räume ohne Kämpfe.",
					"Aprende cómo funciona un capítulo: puertas, minijefe, jefe y salas que no son combates.",
					"Découvrez le fonctionnement d'un chapitre : portes, mini-boss, boss et salles sans combat."),
			_ => subtitle
		};
	}
}
}
