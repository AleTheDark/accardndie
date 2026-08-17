using System;
using System.Collections;
using System.Collections.Generic;
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
	/// <summary>
	/// Come si passa alla tappa dopo.
	/// </summary>
	private enum GuidedTourAdvance
	{
		/// <summary>Il giocatore preme CONTINUA.</summary>
		Continue,

		/// <summary>Il giocatore tocca il bersaglio illuminato.</summary>
		TapTarget,

		/// <summary>
		/// Aspetta che succeda una cosa nel gioco (una classe comprata, un oggetto usato).
		/// La tappa non ha un pulsante: si sblocca da sola quando l'evento arriva.
		/// </summary>
		GameEvent
	}

	/// <summary>
	/// Una tappa del tour. Il bersaglio e' una funzione e non un RectTransform gia' risolto:
	/// molte schermate costruiscono i loro pulsanti solo quando si aprono, quindi al momento
	/// in cui il tour viene scritto quel rect non esiste ancora.
	/// </summary>
	private sealed class GuidedTourStep
	{
		public string Title;
		public string Body;
		public Func<RectTransform> Target;
		public GuidedTourAdvance Advance = GuidedTourAdvance.Continue;
		public bool CenterPanel;
		public bool BottomPanel;
		public bool ClassicRectSpotlight;
		public bool ShowSpotlight = true;
		public bool ShowPanel = true;
		// I tour spesso vengono mostrati sopra griglie illustrate (Tutorial, Negozio,
		// Santuario): con il pannello troppo trasparente titoli e copertine sottostanti
		// interferiscono con il corpo del testo.
		public float PanelOpacity = 0.985f;

		/// <summary>Per <see cref="GuidedTourAdvance.GameEvent"/>: l'id dell'evento atteso.</summary>
		public string AwaitedEvent;

		/// <summary>Eseguita quando la tappa compare (per aprire un pannello, per esempio).</summary>
		public Action OnEnter;
	}

	private readonly List<GuidedTourStep> guidedTourSteps = new List<GuidedTourStep>();

	private int guidedTourStepIndex = -1;

	private Action guidedTourCompleted;

	private Image guidedTourInputBlocker;

	private bool IsGuidedTourActive => guidedTourStepIndex >= 0;

	/// <summary>
	/// Avvia un tour. Riusa il pannello del tutorial di battaglia - spotlight, dimmer, testo
	/// a macchina, pulsante CONTINUA - che era gia' generico: l'unica cosa che lo legava al
	/// combattimento era il posto da cui veniva chiamato.
	/// </summary>
	private void StartGuidedTour(IEnumerable<GuidedTourStep> steps, Action onCompleted)
	{
		guidedTourSteps.Clear();
		guidedTourSteps.AddRange(steps);
		if (guidedTourSteps.Count == 0)
		{
			onCompleted?.Invoke();
			return;
		}

		guidedTourCompleted = onCompleted;
		guidedTourStepIndex = 0;
		EnsureAdventureScriptedTutorialView();
		ShowCurrentGuidedTourStep();
	}

	private void ShowCurrentGuidedTourStep()
	{
		if (!IsGuidedTourActive || guidedTourStepIndex >= guidedTourSteps.Count)
		{
			FinishGuidedTour();
			return;
		}

		GuidedTourStep step = guidedTourSteps[guidedTourStepIndex];
		step.OnEnter?.Invoke();
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

		// Il bersaglio si risolve adesso, non quando il tour e' stato scritto: la schermata
		// puo' essersi appena aperta, e i suoi pulsanti nascono con lei.
		RectTransform target = step.Target?.Invoke();

		adventureScriptedTutorialPanel.SetActive(step.ShowPanel);
		SetGuidedTourInputBlocked(step.ShowPanel
			&& step.Advance == GuidedTourAdvance.Continue);
		if (step.ShowPanel)
			adventureScriptedTutorialPanel.transform.SetAsLastSibling();
		string localizedBody = SetLocalizedAdventureTutorialCopy(step.Title, step.Body);
		adventureScriptedTutorialStepText.text = LocalizedAdventureTutorialStepCounter(
			guidedTourStepIndex + 1, guidedTourSteps.Count);
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
	/// Il pulsante CONTINUA del pannello e' condiviso col tutorial di battaglia: quando c'e'
	/// un tour in corso e' il tour a rispondere.
	/// </summary>
	private bool TryAdvanceGuidedTourFromContinue()
	{
		if (!IsGuidedTourActive)
		{
			return false;
		}
		if (guidedTourSteps[guidedTourStepIndex].Advance != GuidedTourAdvance.Continue)
		{
			// Tappa che aspetta un tocco o un evento: il pulsante non deve scavalcarla.
			return true;
		}
		AdvanceGuidedTour();
		return true;
	}

	/// <summary>
	/// Il giocatore ha toccato il bersaglio illuminato. Restituisce true se il tocco e' stato
	/// consumato dal tour, cosi' la schermata sa che non deve fare altro.
	/// </summary>
	private bool NotifyGuidedTourTargetTapped()
	{
		if (!IsGuidedTourActive
			|| guidedTourSteps[guidedTourStepIndex].Advance != GuidedTourAdvance.TapTarget)
		{
			return false;
		}
		AdvanceGuidedTour();
		return true;
	}

	private bool IsGuidedTourWaitingForTarget(RectTransform target)
	{
		if (!IsGuidedTourActive
			|| target == null
			|| guidedTourSteps[guidedTourStepIndex].Advance != GuidedTourAdvance.TapTarget)
		{
			return false;
		}

		return guidedTourSteps[guidedTourStepIndex].Target?.Invoke() == target;
	}

	/// <summary>
	/// Un evento di gioco e' arrivato (per esempio "class-purchased:mage"). Se e' quello che
	/// la tappa aspettava, il tour prosegue. E' cosi' che l'acquisto guidato non ha bisogno
	/// di logica dedicata: e' una tappa che aspetta un evento.
	/// </summary>
	private void NotifyGuidedTourEvent(string eventId)
	{
		if (!IsGuidedTourActive)
		{
			return;
		}
		GuidedTourStep step = guidedTourSteps[guidedTourStepIndex];
		if (step.Advance != GuidedTourAdvance.GameEvent
			|| !string.Equals(step.AwaitedEvent, eventId, StringComparison.Ordinal))
		{
			return;
		}
		AdvanceGuidedTour();
	}

	private void AdvanceGuidedTour()
	{
		guidedTourStepIndex++;
		if (guidedTourStepIndex >= guidedTourSteps.Count)
		{
			FinishGuidedTour();
			return;
		}
		ShowCurrentGuidedTourStep();
	}

	private void FinishGuidedTour()
	{
		SetGuidedTourInputBlocked(false);
		guidedTourStepIndex = -1;
		guidedTourSteps.Clear();
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

		Action completed = guidedTourCompleted;
		guidedTourCompleted = null;
		completed?.Invoke();
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

	/// <summary>
	/// Interrompe il tour senza segnarlo come visto: e' quello che succede premendo Home.
	/// Al rientro riparte da capo, che e' meglio di riprenderlo a meta' con una schermata
	/// diversa sotto.
	/// </summary>
	private void AbortGuidedTour()
	{
		if (!IsGuidedTourActive)
		{
			return;
		}
		guidedTourCompleted = null;
		FinishGuidedTour();
	}
}
}
