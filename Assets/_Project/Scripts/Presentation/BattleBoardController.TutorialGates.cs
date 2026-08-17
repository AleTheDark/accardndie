using System.Collections;
using System.Collections.Generic;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const string TutorialTourSeenPrefsPrefix = "AccardTutorialTourSeen_";

	private static string TutorialTourSeenPrefsKey(TutorialSurface surface)
	{
		string playerId = AccardND.Network.AccountServerSession.PlayerId;
		return string.IsNullOrWhiteSpace(playerId)
			? TutorialTourSeenPrefsPrefix + surface
			: TutorialTourSeenPrefsPrefix + playerId + "_" + surface;
	}

	/// <summary>
	/// Veli e aloni applicati dai cancelli, tenuti per bottone: rigenerarli a ogni refresh
	/// significherebbe creare e distruggere oggetti UI a ogni ritorno all'hub.
	/// </summary>
	private readonly Dictionary<Button, Image> tutorialGateVeils = new Dictionary<Button, Image>();

	private readonly Dictionary<Button, Image> tutorialGateHalos = new Dictionary<Button, Image>();

	private Coroutine tutorialGateHaloPulseCoroutine;
	private string lastTutorialFlowDiagnostic;

	/// <summary>
	/// Lo stato del percorso come lo vede la UI. Quasi tutto arriva dalla progressione
	/// autoritativa; solo "questo tour l'ho gia' visto" e' locale, perche' non e'
	/// progressione e non vale un viaggio fino al server.
	/// </summary>
	private TutorialFlowState CurrentTutorialFlow()
	{
		if (singlePlayerProgressService == null)
		{
			// Prima che la progressione esista non c'e' niente da chiudere: meglio un hub
			// tutto aperto che un hub tutto bloccato per un riferimento non ancora pronto.
			return new TutorialFlowState(TutorialModuleCatalog.Count, true, true, true, true);
		}

		SinglePlayerProgressSave progress = singlePlayerProgressService.Progress;
		return TutorialFlowState.Read(
			progress?.completedTutorialModules,
			singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Class, "mage"),
			singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Class, "rogue"),
			HasSeenTutorialTour(TutorialSurface.HubSanctuary),
			HasSeenTutorialTour(TutorialSurface.HubShop));
	}

	private bool IsTutorialOnboardingActive()
	{
		return !CurrentTutorialFlow().IsComplete;
	}

	private TutorialGateState TutorialGateFor(TutorialSurface surface)
	{
		return TutorialGate.Evaluate(surface, CurrentTutorialFlow());
	}

	private bool IsTutorialSurfaceOpen(TutorialSurface surface)
	{
		return TutorialGateFor(surface) != TutorialGateState.Closed;
	}

	private static bool HasSeenTutorialTour(TutorialSurface surface)
	{
		return PlayerPrefs.GetInt(TutorialTourSeenPrefsKey(surface), 0) != 0;
	}

	private void MarkTutorialTourSeen(TutorialSurface surface)
	{
		PlayerPrefs.SetInt(TutorialTourSeenPrefsKey(surface), 1);
		PlayerPrefs.Save();
	}

	/// <summary>
	/// I tour visti sono per account, non per dispositivo: senza questo, chi entra con un
	/// secondo account si troverebbe l'onboarding senza nessuna delle spiegazioni.
	/// </summary>
	private void ClearTutorialTourMemory()
	{
		foreach (TutorialSurface surface in System.Enum.GetValues(typeof(TutorialSurface)))
		{
			PlayerPrefs.DeleteKey(TutorialTourSeenPrefsKey(surface));
		}
		PlayerPrefs.Save();
	}

	// ---- Applicazione ai bottoni -------------------------------------------------

	private void ApplyTutorialGate(Button button, TutorialSurface surface)
	{
		ApplyTutorialGateState(button, TutorialGateFor(surface));
	}

	private void ApplyTutorialGateState(Button button, TutorialGateState state)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}

		bool closed = state == TutorialGateState.Closed;
		button.interactable = !closed;
		SetTutorialGateVeilVisible(button, closed);
		SetTutorialGateHaloVisible(button, state == TutorialGateState.Highlighted);
		SetHubPortalSparksActive(button, !closed);
	}

	/// <summary>
	/// Le scintille del portale seguono il cancello. Vivono sull'hotspot disegnato sullo
	/// sfondo, non sul pulsante, quindi il velo non le copre: senza spegnerle, una zona
	/// chiusa resta scura ma continua a luccicare, che e' esattamente l'invito opposto.
	/// </summary>
	private void SetHubPortalSparksActive(Button target, bool active)
	{
		if ((Object)(object)target == (Object)null
			|| !modeSelectionHotspotRects.TryGetValue(target, out RectTransform hotspot)
			|| (Object)(object)hotspot == (Object)null)
		{
			return;
		}
		HubPortalVfx sparks = ((Component)hotspot).GetComponentInChildren<HubPortalVfx>(includeInactive: true);
		if ((Object)(object)sparks != (Object)null)
		{
			((Component)sparks).gameObject.SetActive(active);
		}
	}

	private void SetTutorialGateVeilVisible(Button button, bool visible)
	{
		if (!tutorialGateVeils.TryGetValue(button, out Image veil) || (Object)(object)veil == (Object)null)
		{
			if (!visible)
			{
				return;
			}
			veil = CreateImage("Tutorial Gate Veil", ((Component)button).transform, TutorialGateVeilColor);
			veil.raycastTarget = false;
			Stretch(veil.rectTransform);
			tutorialGateVeils[button] = veil;
		}
		if (visible)
		{
			MatchTutorialGateOverlayToButtonArtwork(veil, button);
		}
		((Component)veil).gameObject.SetActive(visible);
		if (visible)
		{
			((Component)veil).transform.SetAsLastSibling();
		}
	}

	private void SetTutorialGateHaloVisible(Button button, bool visible)
	{
		ShowTutorialGateHalo(button, ((Component)button).transform, button.targetGraphic as Image, visible);
	}

	/// <summary>
	/// Accende l'alone su un elemento qualunque, non solo sui pulsanti dell'hub. Le righe
	/// dell'Avventura, per esempio, hanno un bersaglio invisibile grande quanto la cella: li'
	/// l'alone va messo intorno alla copertina, che e' la cosa che il giocatore vede.
	/// </summary>
	private void ShowTutorialGateHalo(Button key, Transform parent, Image artwork, bool visible)
	{
		if ((Object)(object)key == (Object)null)
		{
			return;
		}
		if (!tutorialGateHalos.TryGetValue(key, out Image halo) || (Object)(object)halo == (Object)null)
		{
			if (!visible)
			{
				return;
			}
			// Le righe dell'Avventura si distruggono e si ricreano a ogni refresh: senza
			// questa potatura la mappa si riempirebbe di chiavi morte a ogni visita.
			PruneDestroyedTutorialGateHalos();
			halo = CreateImage("Tutorial Gate Halo", parent, new Color(1f, 0.84f, 0.24f, 0.85f));
			halo.sprite = GetTutorialGateGlowSprite();
			halo.type = Image.Type.Sliced;
			// Il centro non si disegna: la luce e' una cornice intorno all'elemento, non un
			// velo giallo sopra la sua immagine.
			halo.fillCenter = false;
			halo.preserveAspect = false;
			halo.raycastTarget = false;
			tutorialGateHalos[key] = halo;
		}
		if (visible)
		{
			SizeTutorialGateHaloToArtwork(halo, artwork);
		}
		((Component)halo).gameObject.SetActive(visible);
		if (visible)
		{
			((Component)halo).transform.SetAsFirstSibling();
			EnsureTutorialGateHaloPulse();
		}
	}

	private static readonly Color TutorialGateVeilColor = new Color(0f, 0f, 0f, 0.62f);

	/// <summary>
	/// Il velo deve coprire il pulsante **disegnato**, non il suo RectTransform. I pulsanti
	/// dell'hub hanno <c>preserveAspect</c>: l'immagine sta al centro di un riquadro piu'
	/// grande, e un velo steso sul rect copriva anche il vuoto intorno, disegnando rettangoli
	/// scuri sospesi sullo sfondo.
	///
	/// La soluzione che regge anche i pulsanti di forma irregolare: il velo usa lo stesso
	/// sprite del pulsante, con le stesse regole di adattamento, tinto di nero. Cosi' non
	/// approssima l'area disegnata - la ricalca, bordi arrotondati e trasparenze comprese.
	/// </summary>
	private static void MatchTutorialGateOverlayToButtonArtwork(Image overlay, Button button)
	{
		Image artwork = button.targetGraphic as Image;
		if ((Object)(object)artwork == (Object)null || (Object)(object)artwork.sprite == (Object)null)
		{
			// Pulsante a tinta unita: il rect e' gia' l'area disegnata.
			overlay.sprite = null;
			overlay.type = Image.Type.Simple;
			overlay.preserveAspect = false;
			overlay.color = TutorialGateVeilColor;
			return;
		}

		overlay.sprite = artwork.sprite;
		overlay.type = artwork.type;
		overlay.preserveAspect = artwork.preserveAspect;
		overlay.fillCenter = artwork.fillCenter;
		overlay.color = TutorialGateVeilColor;
	}

	/// <summary>
	/// L'alone non puo' usare lo stesso trucco: la sua immagine e' un bagliore tondo, non la
	/// sagoma del pulsante. Qui si calcola a mano il rettangolo davvero disegnato e gli si
	/// lascia un margine, cosi' la luce abbraccia il pulsante invece di galleggiarci intorno.
	/// </summary>
	private static void SizeTutorialGateHaloToArtwork(Image halo, Image artwork)
	{
		RectTransform haloRect = halo.rectTransform;
		if ((Object)(object)artwork == (Object)null)
		{
			// Le copertine dei moduli passano il loro riquadro come parent: l'alone deve
			// coincidere esattamente con quel rect, senza il margine usato sui pulsanti.
			haloRect.anchorMin = Vector2.zero;
			haloRect.anchorMax = Vector2.one;
			haloRect.pivot = new Vector2(0.5f, 0.5f);
			haloRect.anchoredPosition = Vector2.zero;
			haloRect.sizeDelta = Vector2.zero;
			return;
		}

		RectTransform reference = artwork.rectTransform;
		if ((Object)(object)reference == (Object)null)
		{
			return;
		}

		Vector2 drawn = TutorialGateDrawnSize(artwork, reference.rect.size);
		if (drawn.x <= 1f || drawn.y <= 1f)
		{
			// Layout non ancora calcolato: meglio lasciare la misura di prima che schiacciare
			// l'alone a zero e farlo sparire.
			return;
		}

		haloRect.anchorMin = new Vector2(0.5f, 0.5f);
		haloRect.anchorMax = new Vector2(0.5f, 0.5f);
		haloRect.pivot = new Vector2(0.5f, 0.5f);
		haloRect.anchoredPosition = Vector2.zero;
		// Margine fisso e non proporzionale: la cornice di luce e' spessa uguale su un
		// riquadro grande e su uno piccolo, come un bordo, non come un ingrandimento.
		haloRect.sizeDelta = drawn + new Vector2(TutorialGateGlowMargin * 2f, TutorialGateGlowMargin * 2f);
	}

	/// <summary>Quanto sborda la luce oltre il bordo dell'elemento indicato, in pixel.</summary>
	private const float TutorialGateGlowMargin = 26f;

	private const int TutorialGateGlowTextureSize = 128;

	private const int TutorialGateGlowBorder = 34;

	private static Sprite tutorialGateGlowSprite;

	/// <summary>
	/// La cornice luminosa dei cancelli. E' un 9-slice e non un bagliore tondo: gli elementi
	/// da indicare sono riquadri - copertine quadrate, targhe larghe - e un alone circolare
	/// steso su un rettangolo sborda dai lati corti e sta stretto su quelli lunghi. Cosi'
	/// invece la luce segue il bordo, qualunque sia la forma.
	///
	/// I bordi del 9-slice sono fissi: la cornice non si assottiglia ne' si ingrossa quando
	/// il riquadro cambia misura, e gli angoli restano tondi invece di stirarsi.
	/// </summary>
	private static Sprite GetTutorialGateGlowSprite()
	{
		if ((Object)(object)tutorialGateGlowSprite != (Object)null)
		{
			return tutorialGateGlowSprite;
		}

		const int size = TutorialGateGlowTextureSize;
		const float border = TutorialGateGlowBorder;
		var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
		{
			name = "tutorial_gate_glow",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave
		};

		var pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				// Distanza dal rettangolo interno: zero sul bordo dell'elemento, cresce
				// andando verso l'esterno. Negli angoli e' radiale, sui lati e' dritta, ed
				// e' quello che tiene gli spigoli arrotondati una volta stirata.
				float outsideX = Mathf.Max(0f, Mathf.Max(border - x, x - (size - 1 - border)));
				float outsideY = Mathf.Max(0f, Mathf.Max(border - y, y - (size - 1 - border)));
				float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) / border;

				// Massima sul bordo e in dissolvenza verso fuori, con una coda morbida che
				// evita lo stacco netto contro lo sfondo.
				float falloff = Mathf.Exp(-Mathf.Pow(distance / 0.45f, 2f));
				byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(falloff));
				pixels[y * size + x] = new Color32(255, 205, 48, alpha);
			}
		}

		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		tutorialGateGlowSprite = Sprite.Create(
			texture,
			new Rect(0f, 0f, size, size),
			new Vector2(0.5f, 0.5f),
			100f,
			0,
			SpriteMeshType.FullRect,
			new Vector4(border, border, border, border));
		tutorialGateGlowSprite.name = "tutorial_gate_glow";
		tutorialGateGlowSprite.hideFlags = HideFlags.HideAndDontSave;
		return tutorialGateGlowSprite;
	}

	/// <summary>
	/// Quanto occupa davvero l'immagine dentro il rect. Con <c>preserveAspect</c> Unity la
	/// incastra mantenendo le proporzioni e lascia due bande vuote: sono quelle che il velo
	/// non deve toccare.
	/// </summary>
	private static Vector2 TutorialGateDrawnSize(Image artwork, Vector2 rectSize)
	{
		if ((Object)(object)artwork == (Object)null
			|| (Object)(object)artwork.sprite == (Object)null
			|| !artwork.preserveAspect)
		{
			return rectSize;
		}

		Rect spriteRect = artwork.sprite.rect;
		if (spriteRect.height <= 0f || rectSize.x <= 0f || rectSize.y <= 0f)
		{
			return rectSize;
		}

		float spriteAspect = spriteRect.width / spriteRect.height;
		float rectAspect = rectSize.x / rectSize.y;
		return rectAspect > spriteAspect
			? new Vector2(rectSize.y * spriteAspect, rectSize.y)
			: new Vector2(rectSize.x, rectSize.x / spriteAspect);
	}

	private void PruneDestroyedTutorialGateHalos()
	{
		List<Button> dead = null;
		foreach (KeyValuePair<Button, Image> entry in tutorialGateHalos)
		{
			if ((Object)(object)entry.Key == (Object)null || (Object)(object)entry.Value == (Object)null)
			{
				(dead ??= new List<Button>()).Add(entry.Key);
			}
		}
		if (dead == null)
		{
			return;
		}
		foreach (Button key in dead)
		{
			tutorialGateHalos.Remove(key);
		}
	}

	private void EnsureTutorialGateHaloPulse()
	{
		if (tutorialGateHaloPulseCoroutine == null)
		{
			tutorialGateHaloPulseCoroutine = ((MonoBehaviour)this).StartCoroutine(PulseTutorialGateHalos());
		}
	}

	/// <summary>
	/// Un solo battito per tutti gli aloni accesi. Con una coroutine per bottone il ritmo
	/// si sfaserebbe e l'hub sembrerebbe pieno di luci scollegate invece di indicare una
	/// cosa sola.
	///
	/// L'alone respira e ruota piano: la sola opacita' che sale e scende, su uno sfondo gia'
	/// pieno di torce accese, si legge come una luce ferma. Il movimento e' quello che
	/// distingue "premi qui" da "qui c'e' un lampione".
	/// </summary>
	private IEnumerator PulseTutorialGateHalos()
	{
		while (true)
		{
			float wave = Mathf.Sin(Time.unscaledTime * 2.1f);
			float alpha = 0.42f + 0.45f * Mathf.Abs(wave);
			float scale = 1f + 0.055f * wave;

			foreach (KeyValuePair<Button, Image> entry in tutorialGateHalos)
			{
				Image halo = entry.Value;
				if ((Object)(object)halo == (Object)null || !((Component)halo).gameObject.activeInHierarchy)
				{
					continue;
				}
				Color color = halo.color;
				color.a = alpha;
				halo.color = color;
				// Solo respiro, niente rotazione: lo sprite dell'aura ha una luce con una sua
				// direzione, e girarlo lo fa sembrare storto invece che vivo.
				halo.rectTransform.localScale = new Vector3(scale, scale, 1f);
			}
			yield return null;
		}
	}

	// ---- Refresh per schermata ---------------------------------------------------

	private void RefreshHubTutorialGates()
	{
		if (singlePlayerProgressService == null)
		{
			// Stato del percorso ancora sconosciuto: non si tocca niente. Aprire tutto
			// "per sicurezza" vorrebbe dire mostrare l'hub intero per un istante e poi
			// chiuderlo in faccia al giocatore appena arriva la progressione.
			return;
		}
		TutorialFlowState flow = CurrentTutorialFlow();
		LogTutorialFlowState(flow);
		if (flow.IsComplete)
		{
			ClearTutorialGateDecorations();
			return;
		}

		// Le misure dei pulsanti servono subito: senza un aggiornamento del canvas, alla prima
		// apertura dell'hub si leggerebbero quelle di prima del layout.
		Canvas.ForceUpdateCanvases();
		foreach ((Button button, TutorialSurface surface) in HubTutorialSurfaces())
		{
			ApplyTutorialGate(button, surface);
		}
		if (flow.CompletedModules == 1 && flow.OwnsMage && !IsGuidedTourActive
			&& (Object)(object)modeSelectionPanel != (Object)null
			&& modeSelectionPanel.activeInHierarchy)
		{
			StartMageTutorialNavigationTour();
		}
		else if (flow.CompletedModules == 2 && flow.OwnsRogue && !IsGuidedTourActive
			&& (Object)(object)modeSelectionPanel != (Object)null
			&& modeSelectionPanel.activeInHierarchy)
		{
			StartRogueTutorialNavigationTour();
		}
		else if (flow.CompletedModules == 3 && flow.ShopTourSeen && !IsGuidedTourActive
			&& (Object)(object)modeSelectionPanel != (Object)null
			&& modeSelectionPanel.activeInHierarchy)
		{
			StartItemsTutorialNavigationTour();
		}
	}

	private void LogTutorialFlowState(TutorialFlowState flow)
	{
		string nextModule = TutorialModuleCatalog.NextModule(
			singlePlayerProgressService?.Progress?.completedTutorialModules) ?? "nessuno";
		string pendingClass = TutorialGate.PendingPurchaseClassId(flow) ?? "nessuna";
		string pendingTour = TutorialGate.PendingTourSurface(flow)?.ToString() ?? "nessuno";
		string expected = pendingClass != "nessuna"
			? $"acquista classe {pendingClass}"
			: nextModule == TutorialModuleCatalog.Mage
				? "apri il secondo tutorial: IL MAGO"
				: $"apri modulo {nextModule}";
		string diagnostic =
			$"[TUTORIAL FLOW] moduli={flow.CompletedModules}/{TutorialModuleCatalog.Count}; " +
			$"mago={flow.OwnsMage}; ladro={flow.OwnsRogue}; " +
			$"tourSantuarioVisto={flow.SanctuaryTourSeen}; tourPendente={pendingTour}; " +
			$"acquistoPendente={pendingClass}; prossimoModulo={nextModule}; atteso={expected}.";
		if (diagnostic == lastTutorialFlowDiagnostic)
			return;
		lastTutorialFlowDiagnostic = diagnostic;
		Debug.Log(diagnostic);
	}

	/// <summary>
	/// Le destinazioni dell'hub con il loro pulsante. Sta in un posto solo perche' chi apre i
	/// cancelli e chi li toglie devono lavorare esattamente sulla stessa lista: un pulsante
	/// dimenticato dalla seconda resterebbe velato per sempre.
	/// </summary>
	private IEnumerable<(Button Button, TutorialSurface Surface)> HubTutorialSurfaces()
	{
		yield return (modeSelectionCampaignButton, TutorialSurface.HubCampaign);
		yield return (modeSelectionSanctuaryButton, TutorialSurface.HubSanctuary);
		yield return (modeSelectionShopButton, TutorialSurface.HubShop);
		yield return (modeSelectionTavernButton, TutorialSurface.HubTavern);
		yield return (modeSelectionLibraryButton, TutorialSurface.HubLibrary);
		yield return (modeSelectionProfileButton, TutorialSurface.HubProfile);
		yield return (modeSelectionHallOfFameButton, TutorialSurface.HubLeaderboard);
		yield return (modeSelectionMultiplayerButton, TutorialSurface.HubArena);
	}

	private void RefreshCampaignTutorialGates()
	{
		if (!IsTutorialOnboardingActive())
		{
			ClearTutorialGateDecoration(campaignModeAdventureButton);
			ClearTutorialGateDecoration(campaignModeHardcoreButton);
			return;
		}

		ApplyTutorialGate(campaignModeAdventureButton, TutorialSurface.CampaignAdventure);
		ApplyTutorialGate(campaignModeHardcoreButton, TutorialSurface.CampaignHardcore);
	}

	/// <summary>
	/// Quando tutto e' aperto i cancelli devono sparire senza lasciare tracce: un velo
	/// dimenticato su un bottone attivo e' peggio di un bottone bloccato, perche' sembra
	/// un difetto grafico e non una regola.
	/// </summary>
	private void ClearTutorialGateDecorations()
	{
		foreach ((Button button, TutorialSurface _) in HubTutorialSurfaces())
		{
			SetHubPortalSparksActive(button, active: true);
		}
		foreach (KeyValuePair<Button, Image> entry in tutorialGateVeils)
		{
			if ((Object)(object)entry.Value != (Object)null)
			{
				((Component)entry.Value).gameObject.SetActive(false);
			}
			if ((Object)(object)entry.Key != (Object)null)
			{
				entry.Key.interactable = true;
			}
		}
		foreach (KeyValuePair<Button, Image> entry in tutorialGateHalos)
		{
			if ((Object)(object)entry.Value != (Object)null)
			{
				((Component)entry.Value).gameObject.SetActive(false);
			}
		}
	}

	private void ClearTutorialGateDecoration(Button button)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		button.interactable = true;
		if (tutorialGateVeils.TryGetValue(button, out Image veil) && (Object)(object)veil != (Object)null)
		{
			((Component)veil).gameObject.SetActive(false);
		}
		if (tutorialGateHalos.TryGetValue(button, out Image halo) && (Object)(object)halo != (Object)null)
		{
			((Component)halo).gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Gli altari del Santuario. L'ordine dei pulsanti e' quello dell'enum
	/// <c>SanctuaryAltar</c> (Classi, Tecniche, Reliquie): la schermata ci indicizza gia'
	/// sopra per sapere quale tab e' attiva.
	/// </summary>
	private void RefreshSanctuaryTutorialGates()
	{
		bool onboarding = IsTutorialOnboardingActive();
		for (int index = 0; index < sanctuaryAltarButtons.Count; index++)
		{
			Button button = sanctuaryAltarButtons[index];
			if ((Object)(object)button == (Object)null)
			{
				continue;
			}
			if (!onboarding)
			{
				SetTutorialGateVeilVisible(button, visible: false);
				SetTutorialGateHaloVisible(button, visible: false);
				continue;
			}

			TutorialGateState state = TutorialGateFor(index switch
			{
				0 => TutorialSurface.SanctuaryAltarClasses,
				1 => TutorialSurface.SanctuaryAltarTechniques,
				_ => TutorialSurface.SanctuaryAltarRelics
			});
			// Il tab attivo e' gia' non interattivo per costruzione (non ha senso
			// riselezionarlo): qui si toglie solo, non si restituisce.
			if (state == TutorialGateState.Closed)
			{
				button.interactable = false;
			}
			SetTutorialGateVeilVisible(button, state == TutorialGateState.Closed);
			// L'alone serve a portare il giocatore sul tab CLASSI. Una volta aperto,
			// deve lasciare spazio all'highlight della carta da acquistare.
			bool selectedClassesAltar = index == 0
				&& sanctuaryActiveAltar == SanctuaryAltar.Classes;
			SetTutorialGateHaloVisible(button,
				state == TutorialGateState.Highlighted && !selectedClassesAltar);
		}
	}

	/// <summary>
	/// Quale zona dell'hub e' quel pulsante. Serve agli hotspot disegnati sullo sfondo, che
	/// ricevono il tocco anche quando il pulsante e' spento.
	/// </summary>
	private bool TryGetHubTutorialSurface(Button button, out TutorialSurface surface)
	{
		surface = TutorialSurface.HubCampaign;
		if ((Object)(object)button == (Object)null)
		{
			return false;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionCampaignButton)
		{
			surface = TutorialSurface.HubCampaign;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionSanctuaryButton)
		{
			surface = TutorialSurface.HubSanctuary;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionShopButton)
		{
			surface = TutorialSurface.HubShop;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionTavernButton)
		{
			surface = TutorialSurface.HubTavern;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionLibraryButton)
		{
			surface = TutorialSurface.HubLibrary;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionProfileButton)
		{
			surface = TutorialSurface.HubProfile;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionHallOfFameButton)
		{
			surface = TutorialSurface.HubLeaderboard;
			return true;
		}
		if ((Object)(object)button == (Object)(object)modeSelectionMultiplayerButton)
		{
			surface = TutorialSurface.HubArena;
			return true;
		}
		return false;
	}

	// ---- Cosa dire a chi tocca una porta chiusa -----------------------------------

	/// <summary>
	/// Un tap su una zona chiusa non resta muto: dice cosa manca e rimanda al modulo giusto.
	/// Senza, il giocatore ripete il tocco convinto che il gioco non risponda.
	/// </summary>
	private void ExplainTutorialGate(TutorialSurface surface)
	{
		TutorialFlowState flow = CurrentTutorialFlow();
		string pendingClassId = TutorialGate.PendingPurchaseClassId(flow);
		if (pendingClassId != null)
		{
			SetMessage(pendingClassId == "mage"
				? "Prima passa dal Santuario: ti aspetta il Mago."
				: "Prima passa dal Santuario: ti aspetta il Ladro.");
			return;
		}

		TutorialSurface? tour = TutorialGate.PendingTourSurface(flow);
		if (tour == TutorialSurface.HubSanctuary)
		{
			SetMessage("Prima dai un'occhiata al Santuario: ti spiego a cosa serve.");
			return;
		}
		if (tour == TutorialSurface.HubShop)
		{
			SetMessage("Prima dai un'occhiata al Negozio: ti spiego a cosa serve.");
			return;
		}

		string nextModuleId = TutorialModuleCatalog.NextModule(
			singlePlayerProgressService?.Progress?.completedTutorialModules);
		SetMessage(nextModuleId == null
			? "Si apre piu' avanti."
			: $"Si apre completando il tutorial: {TutorialModuleCatalog.Title(nextModuleId)}.");
	}
}
}
