using System;
using System.Collections.Generic;
using AccardND.GameData;
using AccardND.Localization;
using AccardND.TourKit;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
/// <summary>
/// La macchina a stati del tour vive in <see cref="GuidedTourRunner"/> (assembly
/// AccardND.TourKit, senza riferimenti di gioco). Qui resta solo la pelle: il pannello
/// del tutorial di battaglia, lo spotlight e il blocco dell'input.
/// </summary>
public sealed partial class BattleBoardController : IGuidedTourView
{
	private GuidedTourRunner guidedTourRunner;

	private Image guidedTourInputBlocker;

	private GuidedTourRunner GuidedTour =>
		guidedTourRunner ??= new GuidedTourRunner(this);

	private bool IsGuidedTourActive => GuidedTour.IsActive;

	/// <summary>
	/// Avvia un tour. Riusa il pannello del tutorial di battaglia - spotlight, dimmer, testo
	/// a macchina, pulsante CONTINUA - che era gia' generico: l'unica cosa che lo legava al
	/// combattimento era il posto da cui veniva chiamato.
	/// </summary>
	private void StartGuidedTour(IEnumerable<GuidedTourStep> steps, Action onCompleted)
	{
		GuidedTour.Start(steps, onCompleted);
	}

	/// <summary>
	/// Il pulsante CONTINUA del pannello e' condiviso col tutorial di battaglia: quando c'e'
	/// un tour in corso e' il tour a rispondere.
	/// </summary>
	private bool TryAdvanceGuidedTourFromContinue()
	{
		return GuidedTour.TryAdvanceFromContinue();
	}

	/// <summary>
	/// Il giocatore ha toccato il bersaglio illuminato. Restituisce true se il tocco e' stato
	/// consumato dal tour, cosi' la schermata sa che non deve fare altro.
	/// </summary>
	private bool NotifyGuidedTourTargetTapped()
	{
		return GuidedTour.NotifyTargetTapped();
	}

	private bool IsGuidedTourWaitingForTarget(RectTransform target)
	{
		return GuidedTour.IsWaitingForTarget(target);
	}

	/// <summary>
	/// Un evento di gioco e' arrivato (per esempio "class-purchased:mage"). Se e' quello che
	/// la tappa aspettava, il tour prosegue.
	/// </summary>
	private void NotifyGuidedTourEvent(string eventId)
	{
		GuidedTour.NotifyEvent(eventId);
	}

	/// <summary>
	/// Interrompe il tour senza segnarlo come visto: e' quello che succede premendo Home.
	/// Al rientro riparte da capo, che e' meglio di riprenderlo a meta' con una schermata
	/// diversa sotto.
	/// </summary>
	private void AbortGuidedTour()
	{
		GuidedTour.Abort();
	}

	void IGuidedTourView.EnsureCreated()
	{
		EnsureAdventureScriptedTutorialView();
	}

	void IGuidedTourView.ShowStep(GuidedTourStep step, RectTransform target, int stepNumber, int stepCount)
	{
		Image panelImage = adventureScriptedTutorialPanel != null
			? adventureScriptedTutorialPanel.GetComponent<Image>()
			: null;
		if ((Object)(object)panelImage != (Object)null)
		{
			Color color = panelImage.color;
			color.a = Mathf.Clamp01(step.PanelOpacity);
			panelImage.color = color;
		}
		if ((Object)(object)adventureScriptedTutorialSpotlight != (Object)null)
		{
			adventureScriptedTutorialSpotlight.sprite = step.ClassicRectSpotlight
				? AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite()
				: GetHelpAuraSprite();
			adventureScriptedTutorialSpotlight.type = step.ClassicRectSpotlight
				? Image.Type.Sliced
				: Image.Type.Simple;
			adventureScriptedTutorialSpotlight.fillCenter = !step.ClassicRectSpotlight;
			adventureScriptedTutorialSpotlight.color = step.ClassicRectSpotlight
				? new Color(1f, 0.78f, 0.18f, 0.95f)
				: Color.white;
		}

		adventureScriptedTutorialPanel.SetActive(step.ShowPanel);
		SetGuidedTourInputBlocked(step.ShowPanel
			&& step.Advance == GuidedTourAdvance.Continue);
		if (step.ShowPanel)
			adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		string localizedBody = SetLocalizedAdventureTutorialCopy(step.Title, step.Body);
		adventureScriptedTutorialStepText.text = LocalizedAdventureTutorialStepCounter(
			stepNumber, stepCount);
		if (step.BottomPanel)
			SetRect((RectTransform)adventureScriptedTutorialPanel.transform,
				new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.28f));
		else if (step.CenterPanel)
			SetAdventureTutorialPanelToMessageDialogRect(
				(RectTransform)adventureScriptedTutorialPanel.transform);
		else
			PlaceAdventureTutorialPanel(target);
		ResizeAdventureTutorialPanelForBody(localizedBody);
		if (step.ShowPanel)
			StartAdventureTutorialBodyText(localizedBody);
		SetAdventureTutorialNextButtonEnabled(step.ShowPanel
			&& step.Advance == GuidedTourAdvance.Continue);
		bool alreadyHighlighted = HasActiveTutorialGateHalo(target);
		MoveAdventureTutorialSpotlight(step.ShowSpotlight && !alreadyHighlighted ? target : null);
	}

	void IGuidedTourView.Hide()
	{
		SetGuidedTourInputBlocked(false);
		if (adventureScriptedTutorialTextRoutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(adventureScriptedTutorialTextRoutine);
			adventureScriptedTutorialTextRoutine = null;
		}
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			adventureScriptedTutorialPanel.SetActive(false);
		}
		MoveAdventureTutorialSpotlight(null);
		if ((Object)(object)adventureScriptedTutorialPanel != (Object)null)
		{
			Image panelImage = adventureScriptedTutorialPanel.GetComponent<Image>();
			if ((Object)(object)panelImage != (Object)null)
			{
				Color color = panelImage.color;
				color.a = 0.985f;
				panelImage.color = color;
			}
		}
	}

	/// <summary>
	/// Evita di sovrapporre lo spotlight del tour all'highlight permanente dei gate.
	/// Vale automaticamente per Hub e righe Avventura, anche quando vengono ricostruiti.
	/// </summary>
	private bool HasActiveTutorialGateHalo(RectTransform target)
	{
		if ((Object)(object)target == (Object)null)
			return false;

		foreach (Image halo in tutorialGateHalos.Values)
		{
			if ((Object)(object)halo == (Object)null || !((Component)halo).gameObject.activeInHierarchy)
				continue;

			Transform haloTransform = ((Component)halo).transform;
			if (haloTransform.IsChildOf(target))
				return true;
		}
		return false;
	}

	/// <summary>
	/// Rende modali le tappe con CONTINUA: il pannello resta interattivo grazie al suo
	/// sorting order superiore, mentre ogni controllo della schermata sottostante viene
	/// intercettato. Le tappe TapTarget e GameEvent devono invece lasciare libero il
	/// bersaglio richiesto dal tour.
	/// </summary>
	private void SetGuidedTourInputBlocked(bool blocked)
	{
		if (blocked && (Object)(object)guidedTourInputBlocker == (Object)null)
		{
			guidedTourInputBlocker = CreateImage(
				"Guided Tour Input Blocker",
				(Transform)(object)safeAreaRoot,
				new Color(0f, 0f, 0f, 0.001f));
			Stretch(guidedTourInputBlocker.rectTransform);
			guidedTourInputBlocker.raycastTarget = true;

			Canvas blockerCanvas = ((Component)guidedTourInputBlocker).gameObject.AddComponent<Canvas>();
			blockerCanvas.overrideSorting = true;
			blockerCanvas.sortingOrder = 1095;
			((Component)guidedTourInputBlocker).gameObject.AddComponent<GraphicRaycaster>();
		}

		if ((Object)(object)guidedTourInputBlocker == (Object)null)
			return;

		((Component)guidedTourInputBlocker).gameObject.SetActive(blocked);
		if (blocked)
			guidedTourInputBlocker.rectTransform.SetAsLastSibling();
	}
}
}
