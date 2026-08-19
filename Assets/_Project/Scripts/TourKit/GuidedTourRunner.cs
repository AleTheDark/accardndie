using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccardND.TourKit
{
    /// <summary>
    /// La macchina a stati di un tour guidato: tiene la lista delle tappe, sa a quale si
    /// trova e decide quando si passa alla successiva. Non disegna niente — la presentazione
    /// passa da <see cref="IGuidedTourView"/>.
    ///
    /// Non dipende da alcun tipo di gioco: l'assembly AccardND.TourKit non ha riferimenti.
    /// </summary>
    public sealed class GuidedTourRunner
    {
        private readonly List<GuidedTourStep> steps = new List<GuidedTourStep>();
        private readonly IGuidedTourView view;
        private int stepIndex = -1;
        private Action completed;

        public GuidedTourRunner(IGuidedTourView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public bool IsActive => stepIndex >= 0;

        public int StepCount => steps.Count;

        /// <summary>La tappa corrente, oppure null se non c'e' un tour in corso.</summary>
        public GuidedTourStep CurrentStep =>
            IsActive && stepIndex < steps.Count ? steps[stepIndex] : null;

        public void Start(IEnumerable<GuidedTourStep> tourSteps, Action onCompleted)
        {
            steps.Clear();
            if (tourSteps != null)
            {
                steps.AddRange(tourSteps);
            }
            if (steps.Count == 0)
            {
                onCompleted?.Invoke();
                return;
            }

            completed = onCompleted;
            stepIndex = 0;
            view.EnsureCreated();
            ShowCurrentStep();
        }

        /// <summary>
        /// Il pulsante CONTINUA e' spesso condiviso con altro: restituisce true se il tocco
        /// e' stato consumato dal tour, cosi' il chiamante sa che non deve fare altro.
        /// </summary>
        public bool TryAdvanceFromContinue()
        {
            if (!IsActive)
            {
                return false;
            }
            if (steps[stepIndex].Advance != GuidedTourAdvance.Continue)
            {
                // Tappa che aspetta un tocco o un evento: il pulsante non deve scavalcarla.
                return true;
            }
            Advance();
            return true;
        }

        /// <summary>
        /// Il giocatore ha toccato il bersaglio illuminato. Restituisce true se il tocco e'
        /// stato consumato dal tour.
        /// </summary>
        public bool NotifyTargetTapped()
        {
            if (!IsActive || steps[stepIndex].Advance != GuidedTourAdvance.TapTarget)
            {
                return false;
            }
            Advance();
            return true;
        }

        public bool IsWaitingForTarget(RectTransform target)
        {
            if (!IsActive
                || target == null
                || steps[stepIndex].Advance != GuidedTourAdvance.TapTarget)
            {
                return false;
            }
            return steps[stepIndex].Target?.Invoke() == target;
        }

        /// <summary>
        /// Un evento di gioco e' arrivato (per esempio "class-purchased:mage"). Se e' quello
        /// che la tappa aspettava, il tour prosegue. E' cosi' che un acquisto guidato non ha
        /// bisogno di logica dedicata: e' una tappa che aspetta un evento.
        /// </summary>
        public void NotifyEvent(string eventId)
        {
            if (!IsActive)
            {
                return;
            }
            GuidedTourStep step = steps[stepIndex];
            if (step.Advance != GuidedTourAdvance.GameEvent
                || !string.Equals(step.AwaitedEvent, eventId, StringComparison.Ordinal))
            {
                return;
            }
            Advance();
        }

        /// <summary>
        /// Interrompe il tour senza eseguire la callback di completamento: e' quello che
        /// serve quando si esce dalla schermata. Al rientro il tour riparte da capo, che e'
        /// meglio di riprenderlo a meta' con una schermata diversa sotto.
        /// </summary>
        public void Abort()
        {
            if (!IsActive)
            {
                return;
            }
            completed = null;
            Finish();
        }

        private void ShowCurrentStep()
        {
            if (!IsActive || stepIndex >= steps.Count)
            {
                Finish();
                return;
            }

            GuidedTourStep step = steps[stepIndex];
            step.OnEnter?.Invoke();

            // Il bersaglio si risolve adesso, non quando il tour e' stato scritto: la
            // schermata puo' essersi appena aperta, e i suoi pulsanti nascono con lei.
            RectTransform target = step.Target?.Invoke();

            view.ShowStep(step, target, stepIndex + 1, steps.Count);
        }

        private void Advance()
        {
            stepIndex++;
            if (stepIndex >= steps.Count)
            {
                Finish();
                return;
            }
            ShowCurrentStep();
        }

        private void Finish()
        {
            stepIndex = -1;
            steps.Clear();
            view.Hide();

            Action onCompleted = completed;
            completed = null;
            onCompleted?.Invoke();
        }
    }
}
