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
			string heading = GameText.Get(GameTextKeys.Adventure.TutorialIndexTitle);
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
				? GameText.Get(GameTextKeys.Campaign.ChapterCompleted)
				: current && !prerequisiteSatisfied
					? GameText.Get(GameTextKeys.Adventure.TutorialModuleVisitShopStatus)
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
			SetMessage(GameText.Get(GameTextKeys.Adventure.TutorialModuleVisitShopFirst));
			return;
		}
		if (!done && !current)
		{
			string blockingModuleId = TutorialModuleCatalog.NextModule(
				singlePlayerProgressService?.Progress?.completedTutorialModules);
			SetMessage(blockingModuleId == null
				? GameText.Get(GameTextKeys.Adventure.TutorialModuleOpensLater)
				: GameText.Format(GameTextKeys.Adventure.TutorialModuleCompleteFirst, TutorialModuleDisplayText(blockingModuleId).Title));
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
				? GameText.Format(GameTextKeys.Adventure.TutorialModuleReplayBody, subtitle)
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
				GameText.Get(GameTextKeys.Adventure.TutorialModuleIntro(moduleId)),
			TutorialModuleCatalog.Warrior =>
				GameText.Get(GameTextKeys.Adventure.TutorialModuleIntro(moduleId)),
			TutorialModuleCatalog.Mage =>
				GameText.Get(GameTextKeys.Adventure.TutorialModuleIntro(moduleId)),
			TutorialModuleCatalog.Rogue =>
				GameText.Get(GameTextKeys.Adventure.TutorialModuleIntro(moduleId)),
			TutorialModuleCatalog.ItemsAndBag =>
				GameText.Get(GameTextKeys.Adventure.TutorialModuleIntro(moduleId)),
			TutorialModuleCatalog.ChapterRun =>
				GameText.Get(GameTextKeys.Adventure.TutorialModuleIntro(moduleId)),
			_ => subtitle
		};
	}
}
}
