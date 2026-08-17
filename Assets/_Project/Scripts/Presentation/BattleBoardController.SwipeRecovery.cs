using UnityEngine;
using UnityEngine.InputSystem;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// Quanti frame consecutivi tolleriamo con una gesture aperta e nessun dito
	/// sullo schermo. Un rilascio vero si chiude nello stesso frame, quindi questa
	/// finestra non puo' interrompere uno swipe legittimo: intercetta solo gli
	/// stati rimasti appesi (device perso, overlay distrutto a meta' gesture,
	/// callback andato in eccezione).
	/// </summary>
	private const int StuckGestureIdleFrames = 15;

	/// <summary>
	/// Rete di sicurezza indipendente dal puntatore. Se il device riporta una
	/// pressione fantasma - il caso in cui il rilascio non arriva mai - il conteggio
	/// dei frame liberi non scatta e senza questo tetto la gesture resterebbe aperta
	/// per sempre. Il tempo si azzera a ogni movimento del puntatore, quindi vale
	/// solo per una gesture completamente immobile: chi sta ancora scegliendo dove
	/// trascinare la carta non viene mai interrotto.
	/// </summary>
	private const float FrozenGestureMaximumSeconds = 10f;

	/// <summary>
	/// Spostamento oltre il quale consideriamo il puntatore ancora vivo.
	/// </summary>
	private const float FrozenGesturePixelTolerance = 3f;

	private int stuckGestureIdleFrames;
	private float stuckGestureFrozenSince = -1f;
	private Vector2 stuckGesturePointerAnchor;

	/// <summary>
	/// Lo swipe delle azioni e quello della mano vivono su stato statico condiviso.
	/// Finche' quello stato resta occupato ogni click su Attacca/Abilita'/Suprema
	/// viene soppresso e il turno diventa ingiocabile: qui lo sorvegliamo a ogni
	/// frame e lo liberiamo appena e' certo che non appartiene piu' a nessuno.
	/// </summary>
	private void UpdateStuckGestureWatchdog()
	{
		if (!HasPendingSwipeGesture())
		{
			stuckGestureIdleFrames = 0;
			stuckGestureFrozenSince = -1f;
			return;
		}

		Vector2 pointer = CurrentPointerPosition();
		if (stuckGestureFrozenSince < 0f
			|| (pointer - stuckGesturePointerAnchor).sqrMagnitude
				> FrozenGesturePixelTolerance * FrozenGesturePixelTolerance)
		{
			stuckGesturePointerAnchor = pointer;
			stuckGestureFrozenSince = Time.unscaledTime;
		}
		else if (Time.unscaledTime - stuckGestureFrozenSince >= FrozenGestureMaximumSeconds)
		{
			ReleaseStuckSwipeGestures($"puntatore immobile da {FrozenGestureMaximumSeconds:F0}s");
			return;
		}

		if (IsAnyPointerPressed())
		{
			stuckGestureIdleFrames = 0;
			return;
		}

		stuckGestureIdleFrames++;
		if (stuckGestureIdleFrames < StuckGestureIdleFrames)
			return;

		ReleaseStuckSwipeGestures("nessun puntatore premuto");
	}

	/// <summary>
	/// Pulizia silenziosa agganciata ai passaggi che il giocatore attraversa
	/// naturalmente quando qualcosa si e' incantato: aprire e chiudere l'ispezione
	/// di una carta. E' volutamente incondizionata, perche' l'ispezione e' modale e
	/// nessuna gesture legittima puo' sopravviverle - nemmeno quella di un device
	/// che riporta una pressione fantasma, che e' proprio il caso da sbloccare.
	/// </summary>
	private void ReleaseStuckSwipeGestures(string reason)
	{
		if (!HasPendingSwipeGesture())
			return;

		stuckGestureIdleFrames = 0;
		stuckGestureFrozenSince = -1f;
		Debug.LogWarning(
			$"SWIPE - stato bloccato rilasciato ({reason}). "
			+ PrototypeCardView.DescribeSwipeGestureState());
		PrototypeCardView.ReleaseStuckSwipeGesture();
		DeploymentHandSwipeSelector.ForceReset();
	}

	private static bool HasPendingSwipeGesture()
	{
		return PrototypeCardView.HasActiveSwipeGesture
			|| DeploymentHandSwipeSelector.HasActiveGesture;
	}

	private static bool IsAnyPointerPressed()
	{
		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
			return true;

		return Pointer.current != null && Pointer.current.press.isPressed;
	}

	private static Vector2 CurrentPointerPosition()
	{
		// Il mouse ha la precedenza quando il tasto e' giu': se convive con un tocco
		// fantasma bloccato, seguire quest'ultimo farebbe sembrare immobile anche una
		// gesture del tutto viva.
		if (Mouse.current != null && Mouse.current.leftButton.isPressed)
			return Mouse.current.position.ReadValue();

		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
			return Touchscreen.current.primaryTouch.position.ReadValue();

		return Pointer.current != null
			? Pointer.current.position.ReadValue()
			: Vector2.zero;
	}
}
}
