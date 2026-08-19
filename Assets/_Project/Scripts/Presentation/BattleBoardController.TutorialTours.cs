using AccardND.TourKit;
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
	/// <summary>Id degli eventi che una tappa puo' aspettare.</summary>
	private const string TutorialEventClassPurchasedPrefix = "class-purchased:";

	/// <summary>
	/// Dopo il primo premio livello accompagna il giocatore dalla Home al favo. Il flag usa
	/// la stessa memoria per-account degli altri tour, quindi non ricompare ai livelli
	/// successivi e viene azzerato insieme all'onboarding dagli strumenti di debug.
	/// </summary>
	private void TryStartFirstLevelTalentTour()
	{
		if (IsGuidedTourActive || HasSeenTutorialTour(TutorialSurface.HubProfile))
			return;

		if ((Object)(object)modeSelectionProfileButton == (Object)null)
			return;

		// Il primo livello puo' arrivare prima della fine dell'onboarding principale. In quel
		// caso il premio appena riscosso ha priorita' sul gate e rende accessibile il Profilo.
		modeSelectionProfileButton.interactable = true;

		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "SPENDI IL PROPOLI",
				Body = "Il nuovo livello ti ha dato punti propoli. Apri il PROFILO per scoprire dove investirli.",
				Target = () => (Object)(object)modeSelectionProfileButton == (Object)null
					? null
					: (RectTransform)((Component)modeSelectionProfileButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true
			},
			new GuidedTourStep
			{
				Title = "I TUOI TALENTI",
				Body = "Apri la scheda TALENTI: qui trovi tutti i potenziamenti permanenti del tuo account.",
				Target = () => profileTabs.Count > (int)ProfilePage.Talents
					? (RectTransform)profileTabs[(int)ProfilePage.Talents].transform
					: null,
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true
			},
			new GuidedTourStep
			{
				Title = "IL FAVO DEI TALENTI",
				Body = "Questo e' il favo. Scegli un ramo e usa il propoli per sbloccare o potenziare le sue celle. I talenti ottenuti resteranno attivi nelle prossime avventure.",
				Target = TalentHiveTourRect,
				Advance = GuidedTourAdvance.Continue,
				BottomPanel = true
			}
		};

		StartGuidedTour(steps, () =>
		{
			MarkTutorialTourSeen(TutorialSurface.HubProfile);
			RefreshHubTutorialGates();
		});
	}

	private RectTransform TalentHiveTourRect()
	{
		if ((Object)(object)profileContentRoot == (Object)null)
			return null;

		foreach (RectTransform rect in profileContentRoot.GetComponentsInChildren<RectTransform>(true))
		{
			if (rect.name == "Talent Hive Backing")
				return rect;
		}
		return profileContentRoot;
	}

	private void StartWarriorSanctuaryUnlockTour(int grantedHoney)
	{
		int honey = Mathf.Max(0, grantedHoney);
		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "RICOMPENSA OTTENUTA",
				Body = $"Hai sbloccato l'accesso al Santuario e hai ricevuto {honey} vasetti di miele. Per accedere al prossimo tutorial dovrai comprare il Mago dal Santuario.",
				Advance = GuidedTourAdvance.Continue,
				CenterPanel = true,
				OnEnter = () => SetAdventureTutorialTimelineVisible(visible: false)
			},
			new GuidedTourStep
			{
				Title = "SANTUARIO SBLOCCATO",
				Body = "Il Santuario e' ora accessibile. Entra e usa il miele ricevuto per comprare il Mago.",
				Target = () => (Object)(object)modeSelectionSanctuaryButton == (Object)null
					? null
					: (RectTransform)((Component)modeSelectionSanctuaryButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				OnEnter = () =>
				{
					// Continua sulla ricompensa porta direttamente all'Hub: il giocatore
					// non deve premere HOME prima di poter raggiungere il Santuario.
					ShowHubFromSinglePlayer(preserveGuidedTour: true);
					SetAdventureTutorialTimelineVisible(visible: false);
					StartCoroutine(PlaySanctuaryUnlockAnimation());
				}
			}
		};

		StartGuidedTour(steps, () =>
		{
			TryStartPendingTutorialTour(TutorialSurface.HubSanctuary);
		});
	}

	private void StartMageSanctuaryRewardTour(int grantedHoney)
	{
		int honey = Mathf.Max(0, grantedHoney);
		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "RICOMPENSA OTTENUTA",
				Body = $"Hai completato il tutorial del Mago e hai ricevuto {honey} vasetti di miele. Servono per comprare il Ladro al Santuario e sbloccare il terzo tutorial.",
				Advance = GuidedTourAdvance.Continue,
				CenterPanel = true,
				OnEnter = () => SetAdventureTutorialTimelineVisible(visible: false)
			},
			new GuidedTourStep
			{
				Title = "COMPRA IL LADRO",
				Body = "Entra nel SANTUARIO e usa i 40 vasetti ricevuti per comprare il Ladro.",
				Target = () => (Object)(object)modeSelectionSanctuaryButton == (Object)null
					? null
					: (RectTransform)((Component)modeSelectionSanctuaryButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				OnEnter = () =>
				{
					// Continua sulla ricompensa chiude direttamente la stanza: non serve un
					// secondo dialogo che chieda al giocatore di premere HOME.
					ShowHubFromSinglePlayer(preserveGuidedTour: true);
					SetAdventureTutorialTimelineVisible(visible: false);
				}
			}
		};

		StartGuidedTour(steps, () =>
		{
			TryStartPendingTutorialTour(TutorialSurface.HubSanctuary);
		});
	}

	private IEnumerator PlaySanctuaryUnlockAnimation()
	{
		RefreshHubTutorialGates();
		if ((Object)(object)modeSelectionSanctuaryButton == (Object)null)
			yield break;

		RectTransform target = (RectTransform)((Component)modeSelectionSanctuaryButton).transform;
		Vector3 originalScale = target.localScale;
		const float duration = 0.72f;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			float pulse = Mathf.Sin(progress * Mathf.PI) * 0.16f;
			target.localScale = originalScale * (1f + pulse);
			yield return null;
		}
		target.localScale = originalScale;
	}

	/// <summary>
	/// Il tour dovuto adesso, se ce n'e' uno. Si chiama entrando in una schermata: e' li'
	/// che il tour ha senso, non nell'hub davanti a una porta ancora chiusa.
	/// </summary>
	private void TryStartPendingTutorialTour(TutorialSurface openedSurface)
	{
		if (IsGuidedTourActive)
		{
			if (openedSurface == TutorialSurface.HubShop)
				Debug.Log("[TUTORIAL SHOP] tour interno non avviato: esiste ancora un GuidedTour attivo.");
			return;
		}

		TutorialFlowState flow = CurrentTutorialFlow();
		if (openedSurface == TutorialSurface.HubShop)
		{
			Debug.Log($"[TUTORIAL SHOP] controllo tour interno; moduli={flow.CompletedModules}; "
				+ $"shopTourVisto={flow.ShopTourSeen}; "
				+ $"tourPendente={TutorialGate.PendingTourSurface(flow)?.ToString() ?? "nessuno"}.");
		}
		if (TutorialGate.PendingTourSurface(flow) == openedSurface)
		{
			if (openedSurface == TutorialSurface.HubSanctuary)
			{
				StartSanctuaryTour();
				return;
			}
			if (openedSurface == TutorialSurface.HubShop)
			{
				StartShopTour();
				return;
			}
		}

		// Nessun tour informativo, ma potrebbe esserci un acquisto guidato in sospeso.
		if (openedSurface == TutorialSurface.HubSanctuary)
		{
			string classId = TutorialGate.PendingPurchaseClassId(flow);
			if (classId != null)
			{
				StartGuidedPurchaseTour(classId);
			}
		}
	}

	// ---- Santuario ---------------------------------------------------------------

	private void StartSanctuaryTour()
	{
		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "IL SANTUARIO",
				Body = "Questo e' il Santuario. Qui si trasforma il miele in cose permanenti: classi nuove, tecniche e reliquie. Quello che compri qui resta tuo per sempre, anche quando una run finisce male.",
				Target = () => SanctuaryAltarRect(0)
			},
			new GuidedTourStep
			{
				Title = "GLI ALTARI",
				Body = "Le CLASSI sono le pedine che potrai schierare. Le TECNICHE sono la seconda abilita' di una classe che possiedi gia'. Le RELIQUIE ampliano la bisaccia e il banco del Mercato.",
				Target = () => SanctuaryAltarRect(1)
			},
			new GuidedTourStep
			{
				Title = "IL MIELE",
				Body = "Tutto qui dentro si paga in vasetti di miele. Non si trovano giocando: si guadagnano in taverna, con le quest del giorno. Durante il tutorial te li do io, giusti per quello che serve.",
				Target = () => (RectTransform)((Component)accountHoneyPanelImage).transform
			}
		};

		StartGuidedTour(steps, () =>
		{
			MarkTutorialTourSeen(TutorialSurface.HubSanctuary);
			RefreshSanctuaryAltarButtons();
			// Finito il giro informativo, prosegui subito con l'acquisto richiesto dal
			// percorso (Mago o Ladro) senza costringere il giocatore a uscire e rientrare.
			TryStartPendingTutorialTour(TutorialSurface.HubSanctuary);
		});
	}

	private RectTransform SanctuaryAltarRect(int index)
	{
		if (index < 0 || index >= sanctuaryAltarButtons.Count)
		{
			return null;
		}
		Button button = sanctuaryAltarButtons[index];
		return (Object)(object)button == (Object)null
			? null
			: (RectTransform)((Component)button).transform;
	}

	private RectTransform SanctuaryEntryRect(string entryId)
	{
		if (string.IsNullOrWhiteSpace(entryId))
			return null;

		string expectedName = "Sanctuary " + entryId;
		foreach (GameObject card in sanctuaryCards)
		{
			if ((Object)(object)card != (Object)null
				&& string.Equals(card.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
			{
				// La Cover coincide con tutta la carta ed e' un bersaglio stabile dopo il
				// rebuild della griglia: evidenziarla rende inequivocabile quale classe comprare.
				Transform cover = card.transform.Find("Cover");
				return (Object)(object)cover != (Object)null
					? (RectTransform)cover
					: (RectTransform)card.transform;
			}
		}
		return null;
	}

	private RectTransform AdventureRowRect(string rowId)
	{
		if (string.IsNullOrWhiteSpace(rowId))
			return null;

		string expectedName = "Adventure " + rowId;
		foreach (GameObject row in adventureChapterRows)
		{
			if ((Object)(object)row != (Object)null
				&& string.Equals(row.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
			{
				return (RectTransform)row.transform;
			}
		}
		return null;
	}

	private void StartMageTutorialNavigationTour()
	{
		if ((Object)(object)sanctuaryPanel != (Object)null)
			sanctuaryPanel.SetActive(false);
		if ((Object)(object)modeSelectionPanel == (Object)null || !modeSelectionPanel.activeInHierarchy)
			ShowHubFromSinglePlayer();
		// ShowHubFromSinglePlayer ricalcola i gate e puo' aver gia' ripristinato questo
		// stesso tour dal flow server: in quel caso non sovrascriverlo con una copia.
		if (IsGuidedTourActive)
			return;

		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "TORNA IN CAMPAGNA",
				Body = "Il Mago e' stato sbloccato. Entra in CAMPAGNA per raggiungere il suo tutorial.",
				Target = () => (Object)(object)modeSelectionCampaignButton == (Object)null
					? null
					: (RectTransform)((Component)modeSelectionCampaignButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true,
				ShowSpotlight = false
			},
			new GuidedTourStep
			{
				Title = "APRI L'AVVENTURA",
				Body = "Seleziona AVVENTURA: i tutorial delle classi si trovano qui.",
				Target = () => (Object)(object)campaignModeAdventureButton == (Object)null
					? null
					: (RectTransform)((Component)campaignModeAdventureButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true
			},
			new GuidedTourStep
			{
				Title = "APRI I TUTORIAL",
				Body = "Apri la sezione TUTORIAL per vedere il prossimo modulo disponibile.",
				Target = () => AdventureRowRect("tutorial"),
				Advance = GuidedTourAdvance.TapTarget,
				BottomPanel = true
			},
			new GuidedTourStep
			{
				Title = "IL SECONDO TUTORIAL",
				Body = "Il Mago e' ora disponibile. Apri il suo tutorial per continuare il percorso.",
				Target = () => AdventureRowRect(TutorialModuleCatalog.Mage),
				Advance = GuidedTourAdvance.TapTarget,
				BottomPanel = true
			}
		};

		StartGuidedTour(steps, onCompleted: null);
	}

	private void StartRogueTutorialNavigationTour()
	{
		if ((Object)(object)sanctuaryPanel != (Object)null)
			sanctuaryPanel.SetActive(false);
		if ((Object)(object)modeSelectionPanel == (Object)null || !modeSelectionPanel.activeInHierarchy)
			ShowHubFromSinglePlayer();
		if (IsGuidedTourActive)
			return;

		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "TORNA IN CAMPAGNA",
				Body = "Il Ladro e' stato sbloccato. Entra in CAMPAGNA per raggiungere il terzo tutorial.",
				Target = () => (Object)(object)modeSelectionCampaignButton == (Object)null
					? null
					: (RectTransform)((Component)modeSelectionCampaignButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				ShowSpotlight = false
			},
			new GuidedTourStep
			{
				Title = "APRI L'AVVENTURA",
				Body = "Seleziona AVVENTURA per tornare ai tutorial delle classi.",
				Target = () => (Object)(object)campaignModeAdventureButton == (Object)null
					? null
					: (RectTransform)((Component)campaignModeAdventureButton).transform,
				Advance = GuidedTourAdvance.TapTarget
			},
			new GuidedTourStep
			{
				Title = "APRI I TUTORIAL",
				Body = "Apri la sezione TUTORIAL.",
				Target = () => AdventureRowRect("tutorial"),
				Advance = GuidedTourAdvance.TapTarget,
				BottomPanel = true
			},
			new GuidedTourStep
			{
				Title = "IL TERZO TUTORIAL",
				Body = "Il Ladro e' ora disponibile. Apri il suo tutorial per continuare il percorso.",
				Target = () => AdventureRowRect(TutorialModuleCatalog.Rogue),
				Advance = GuidedTourAdvance.TapTarget,
				BottomPanel = true
			}
		};

		StartGuidedTour(steps, onCompleted: null);
	}

	private void StartItemsTutorialNavigationTour()
	{
		if ((Object)(object)shopPanel != (Object)null)
			shopPanel.SetActive(false);
		if ((Object)(object)modeSelectionPanel == (Object)null || !modeSelectionPanel.activeInHierarchy)
			ShowHubFromSinglePlayer();
		if (IsGuidedTourActive)
			return;

		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "USA IL REGALO",
				Body = "L'Empower e' nella tua scorta. Entra in CAMPAGNA per raggiungere il tutorial sugli oggetti e imparare a usarlo.",
				Target = () => (Object)(object)modeSelectionCampaignButton == (Object)null
					? null
					: (RectTransform)((Component)modeSelectionCampaignButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true,
				ShowSpotlight = false,
				ShowPanel = false
			},
			new GuidedTourStep
			{
				Title = "APRI L'AVVENTURA",
				Body = "Seleziona AVVENTURA.",
				Target = () => (Object)(object)campaignModeAdventureButton == (Object)null
					? null
					: (RectTransform)((Component)campaignModeAdventureButton).transform,
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true,
				BottomPanel = true,
				ShowPanel = false
			},
			new GuidedTourStep
			{
				Title = "APRI I TUTORIAL",
				Body = "Apri la sezione TUTORIAL per vedere la prossima lezione.",
				Target = () => AdventureRowRect("tutorial"),
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true,
				ShowPanel = false
			},
			new GuidedTourStep
			{
				Title = "TUTORIAL OGGETTI",
				Body = "Apri OGGETTI: qui userai l'Empower ricevuto dal Negozio.",
				Target = () => AdventureRowRect(TutorialModuleCatalog.ItemsAndBag),
				Advance = GuidedTourAdvance.TapTarget,
				ClassicRectSpotlight = true,
				ShowPanel = false
			}
		};

		StartGuidedTour(steps, onCompleted: null);
	}

	// ---- Acquisto guidato --------------------------------------------------------

	/// <summary>
	/// Il tour che fa comprare una classe. Non ha logica propria: e' una tappa che aspetta
	/// l'evento "classe comprata", e l'evento lo emette l'acquisto vero del Santuario.
	/// </summary>
	private void StartGuidedPurchaseTour(string classId)
	{
		string className = classId == "rogue" ? "il Ladro" : "il Mago";
		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "LA TUA PROSSIMA CLASSE",
				Body = $"Con i vasetti che hai appena ricevuto puoi prendere {className}. Apri l'altare delle CLASSI.",
				Target = () => SanctuaryAltarRect(0),
				Advance = GuidedTourAdvance.TapTarget
			},
			new GuidedTourStep
			{
				Title = "COMPRA LA CLASSE",
				Body = $"Scegli {className} e conferma. I vasetti bastano esattamente: e' il senso del dono.",
				Target = () => SanctuaryEntryRect(classId),
				Advance = GuidedTourAdvance.GameEvent,
				AwaitedEvent = TutorialEventClassPurchasedPrefix + classId,
				ClassicRectSpotlight = true,
				BottomPanel = true,
				OnEnter = () =>
				{
					// Le carte vengono ricostruite quando si apre l'altare. Prima di leggere i
					// world-corner del Mago/Ladro bisogna chiudere il ciclo di layout, altrimenti
					// spotlight e popup usano ancora il rettangolo provvisorio del contenitore.
					Canvas.ForceUpdateCanvases();
					if ((Object)(object)sanctuaryListRoot != (Object)null)
						LayoutRebuilder.ForceRebuildLayoutImmediate(sanctuaryListRoot);
					Canvas.ForceUpdateCanvases();
				}
			}
		};

		StartGuidedTour(steps, () =>
		{
			RefreshSanctuaryAltarButtons();
			if (string.Equals(classId, "mage", System.StringComparison.OrdinalIgnoreCase))
			{
				StartMageTutorialNavigationTour();
				return;
			}
			if (string.Equals(classId, "rogue", System.StringComparison.OrdinalIgnoreCase))
			{
				StartRogueTutorialNavigationTour();
				return;
			}
			SetMessage($"Ottimo. Ora torna in Campagna: il tutorial di questa classe ti aspetta.");
		});
	}

	/// <summary>
	/// Chiamata dall'acquisto del Santuario andato a buon fine. Se il tour stava aspettando
	/// proprio quella classe, prosegue.
	/// </summary>
	private void NotifyTutorialClassPurchased(string classId)
	{
		if (string.IsNullOrWhiteSpace(classId))
		{
			return;
		}
		NotifyGuidedTourEvent(TutorialEventClassPurchasedPrefix + classId.Trim().ToLowerInvariant());
	}

	// ---- Negozio -----------------------------------------------------------------

	private void StartShopTour()
	{
		Debug.Log("[TUTORIAL SHOP] StartShopTour: apertura popup guidato del Negozio.");
		var steps = new List<GuidedTourStep>
		{
			new GuidedTourStep
			{
				Title = "IL NEGOZIO",
				Body = "Nel Negozio puoi comprare gli oggetti consumabili da usare durante l'Avventura. Alcuni potenziano un tiro o una stanza, altri ti salvano nei momenti piu' difficili.",
				Target = () => shopCatalogRoot,
				PanelOpacity = 1f
			},
			new GuidedTourStep
			{
				Title = "L'OFFERTA DEL GIORNO",
				Body = "Ogni giorno alcuni oggetti costano meno, e le copie in offerta sono limitate. Vale la pena passare a controllare.",
				Target = () => shopOffersRoot,
				PanelOpacity = 1f
			},
			new GuidedTourStep
			{
				Title = "UN REGALO PER IL TUTORIAL",
				Body = "Hai ricevuto un Empower. Aumenta di uno step il tuo dado Vigore in attacco: nel prossimo tutorial lo metteremo nella bisaccia e lo useremo sul campo.",
				Target = () => shopCatalogRoot,
				PanelOpacity = 1f
			}
		};

		StartGuidedTour(steps, () =>
		{
			MarkTutorialTourSeen(TutorialSurface.HubShop);
			RefreshAdventureChapterList();
			StartItemsTutorialNavigationTour();
		});
	}
}
}
