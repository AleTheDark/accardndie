using System;
using System.Collections.Generic;
using System.Linq;
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
	// Forza permanente incisa dal Sigillo Oscuro. Confluisce nel bonus permanente
	// della carta ma resta scorporata nei pannelli di stato, che le danno un token
	// e una descrizione a parte.
	private const int RubySealPowerBonus = 2;

	// Oggetti della bisaccia portati in questa run e non ancora usati.
	private readonly List<string> runBagItemIds = new List<string>();

	// Oggetti della bisaccia consumati: a fine run il server li scala dalla scorta.
	private readonly List<string> consumedBagItemIds = new List<string>();

	private static string CampaignConsumableResourceName(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => "detector_item",
			CampaignConsumableType.SecondChance => "second_chance_item",
			CampaignConsumableType.Empower => "empower_item",
			CampaignConsumableType.SigilloRubino => "ruby_seal_item",
			CampaignConsumableType.DoubleExp => "double_exp_item",
			CampaignConsumableType.ManaGain5 => "mana_gain_5_item",
			CampaignConsumableType.ManaGain10 => "mana_gain_10_item",
			CampaignConsumableType.Jolly => "jolly_item",
			_ => "info_button",
		};
	}

	private static string CampaignConsumableName(CampaignConsumableType itemType)
	{
		if (itemType == CampaignConsumableType.ManaGain5) return "Mana +5";
		if (itemType == CampaignConsumableType.ManaGain10) return "Mana +10";
		if (itemType == CampaignConsumableType.Jolly) return "Jolly";
		return GameText.Get(GameTextKeys.Consumables.Name(CampaignConsumableLocalizationId(itemType)));
	}

	private static string CampaignConsumableDescription(CampaignConsumableType itemType)
	{
		if (itemType == CampaignConsumableType.ManaGain5) return "Recupera 5 mana in qualsiasi momento, senza superare il limite massimo di 10.";
		if (itemType == CampaignConsumableType.ManaGain10) return "Recupera 10 mana in qualsiasi momento, senza superare il limite massimo di 10.";
		if (itemType == CampaignConsumableType.Jolly) return "Ti teletrasporta subito alla scelta della stanza, anche mentre sei gia dentro una stanza.";
		return GameText.Get(GameTextKeys.Consumables.Description(CampaignConsumableLocalizationId(itemType)));
	}

	private static string CampaignConsumableLocalizationId(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => "detector",
			CampaignConsumableType.SecondChance => "second_chance",
			CampaignConsumableType.Empower => "empower",
			CampaignConsumableType.SigilloRubino => "ruby_seal",
			CampaignConsumableType.DoubleExp => "double_experience",
			CampaignConsumableType.ManaGain5 => "mana_gain_5",
			CampaignConsumableType.ManaGain10 => "mana_gain_10",
			CampaignConsumableType.Jolly => "jolly",
			_ => "generic",
		};
	}

	private void HandleCampaignConsumableClicked(CampaignConsumableType itemType)
	{
		ShowCampaignConsumableInspection(itemType);
	}

	private void ConfirmInspectedCampaignConsumable()
	{
		if (!inspectedCampaignConsumableActive)
		{
			return;
		}
		CampaignConsumableType itemType = inspectedCampaignConsumableType;
		CloseCardInspection(playSfx: false);
		if (campaignDeck == null || pvpPresentationActive)
		{
			return;
		}
		if (TryUseCampaignConsumable(itemType) && (Object)(object)implementationArchivePanel != (Object)null && implementationArchivePanel.activeSelf)
		{
			RefreshImplementationArchive();
		}
	}

	private bool TryUseCampaignConsumable(CampaignConsumableType itemType)
	{
		if (IsConsumableBlockedInBattle(itemType) && IsCampaignBattleActive())
		{
			string itemName = CampaignConsumableName(itemType);
			SetMessage(GameText.Format(GameTextKeys.Consumables.CannotUseInBattle, itemName));
			AppendLog(GameText.Format(GameTextKeys.Consumables.BlockedByBattleLog, itemName));
			return false;
		}
		if (itemType == CampaignConsumableType.Empower && IsEmpowerBlockedInCurrentRoom())
		{
			SetMessage(GameText.Get(GameTextKeys.Consumables.EmpowerBossBlocked));
			AppendLog(GameText.Get(GameTextKeys.Consumables.EmpowerBossBlockedLog));
			return false;
		}
		if (itemType == CampaignConsumableType.SigilloRubino)
		{
			return BeginRubySealTargetSelection();
		}
		if (campaignConsumables == null || !campaignConsumables.TryConsume(itemType))
		{
			return false;
		}
		RecordCampaignItemUsed(itemType);
		switch (itemType)
		{
		case CampaignConsumableType.Detector:
			PlayDetectorItemUseSfx();
			if ((Object)(object)roomChoicePanel != (Object)null && roomChoicePanel.activeSelf)
			{
				RevealCurrentCampaignDoorsWithDetector();
				SetMessage(GameText.Get(GameTextKeys.Consumables.DetectorRevealed));
			}
			else
			{
				nextDoorChoiceRevealed = true;
				SetMessage(GameText.Get(GameTextKeys.Consumables.DetectorNextChoice));
			}
			AppendLog(GameText.Get(GameTextKeys.Consumables.DetectorActivatedLog));
			return true;
		case CampaignConsumableType.SecondChance:
			int revived = RecoverAllGraveyardCards();
			SetMessage(GameText.Format(GameTextKeys.Consumables.SecondChanceUsed, revived));
			AppendLog(GameText.Format(GameTextKeys.Consumables.SecondChanceUsedLog, revived));
			return true;
		case CampaignConsumableType.Empower:
			PlayEmpowerItemUseSfx();
			nextRoomEmpowered = true;
			RefreshPlayerHud();
			SetMessage(GameText.Get(GameTextKeys.Consumables.EmpowerUsed));
			AppendLog(GameText.Get(GameTextKeys.Consumables.EmpowerUsedLog));
			return true;
		case CampaignConsumableType.DoubleExp:
			nextRoomDoubleExperience = true;
			SetMessage(GameText.Get(GameTextKeys.Consumables.DoubleExperienceReady));
			AppendLog(GameText.Get(GameTextKeys.Consumables.DoubleExperienceReadyLog));
			return true;
		case CampaignConsumableType.ManaGain5:
			int gainedFive = campaignPlayerMana.Gain(5);
			battleSfx?.PlayManaGain();
			RefreshPlayerHud();
			SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Consumables.ManaRecovered, "Mana recuperato: +{0}. Riserva {1}/{2}.", "Mana restored: +{0}. Reserve {1}/{2}.", "Mana wiederhergestellt: +{0}. Reserve {1}/{2}.", "Maná recuperado: +{0}. Reserva {1}/{2}.", "Mana récupéré : +{0}. Réserve {1}/{2}.", gainedFive, CampaignPlayerManaCurrent, CampaignManaMaximum));
			AppendLog(GameText.Format(GameTextKeys.Consumables.ManaRecoveredLog, 5, gainedFive, CampaignPlayerManaCurrent, CampaignManaMaximum));
			return true;
		case CampaignConsumableType.ManaGain10:
			int gainedTen = campaignPlayerMana.Gain(10);
			battleSfx?.PlayManaGain();
			RefreshPlayerHud();
			SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Consumables.ManaRecovered, "Mana recuperato: +{0}. Riserva {1}/{2}.", "Mana restored: +{0}. Reserve {1}/{2}.", "Mana wiederhergestellt: +{0}. Reserve {1}/{2}.", "Maná recuperado: +{0}. Reserva {1}/{2}.", "Mana récupéré : +{0}. Réserve {1}/{2}.", gainedTen, CampaignPlayerManaCurrent, CampaignManaMaximum));
			AppendLog(GameText.Format(GameTextKeys.Consumables.ManaRecoveredLog, 10, gainedTen, CampaignPlayerManaCurrent, CampaignManaMaximum));
			return true;
		case CampaignConsumableType.Jolly:
			// Prima di abbandonare la stanza consolidiamo le eliminazioni: le carte morte
			// devono restare al cimitero. Le sopravvissute tornano subito disponibili,
			// perche' il Jolly non equivale al completamento di un combattimento.
			if (campaignDeck != null)
			{
				CampaignCardInstance[] defeatedCards = playerCards
					.Where(card => card != null && card.Eliminated && card.CampaignCard != null)
					.Select(card => card.CampaignCard)
					.Distinct()
					.ToArray();
				campaignDeck.CompleteCombat(defeatedCards, skipCooldown: true);
				ApplySecondWindTalent(defeatedCards);
			}
			// Il Jolly puo' interrompere anche una stanza attiva. BeginRoomChoice esegue
			// poi la stessa pulizia visiva usata al termine di una stanza.
			BeginRoomChoice();
			RevealCurrentCampaignDoorsWithDetector();
			SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Consumables.JollyUsed, "Jolly usato: scegli la stanza in cui teletrasportarti.", "Joker used: choose the room to teleport to.", "Joker benutzt: Wähle den Raum, in den du dich teleportieren möchtest.", "Comodín usado: elige la sala a la que teletransportarte.", "Joker utilisé : choisissez la salle vers laquelle vous téléporter."));
			AppendLog(GameText.GetLocalizedFallback(GameTextKeys.Consumables.JollyUsedLog, "JOLLY - teletrasporto alla scelta della stanza", "JOKER - teleporting to room choice", "JOKER - Teleport zur Raumauswahl", "COMODÍN - teletransporte a la elección de sala", "JOKER - téléportation vers le choix de salle"));
			return true;
		default:
			return false;
		}
	}

	private bool IsEmpowerBlockedInCurrentRoom()
	{
		return campaignDeck != null && currentRoomType == RoomType.Boss;
	}

	private bool BeginRubySealTargetSelection()
	{
		if (!HasRubySealTarget())
		{
			SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealNoTarget));
			AppendLog(GameText.Get(GameTextKeys.Consumables.RubySealNoTargetLog));
			return false;
		}
		rubySealTargetSelectionActive = true;
		ShowRubySealTargetPanel();
		SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealSelectTarget));
		AppendLog(GameText.Get(GameTextKeys.Consumables.RubySealSelectTargetLog));
		return true;
	}

	private void HandleImplementationCardClicked(CampaignCardInstance card)
	{
		if (!rubySealTargetSelectionActive)
		{
			ShowCardInspection(card?.Definition);
			return;
		}
		TryApplyRubySealTo(card);
	}

	private bool TryApplyRubySealTo(CampaignCardInstance target)
	{
		if (!IsRubySealTarget(target))
		{
			SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealInvalidTarget));
			return false;
		}
		if (campaignConsumables == null || !campaignConsumables.TryConsume(CampaignConsumableType.SigilloRubino))
		{
			rubySealTargetSelectionActive = false;
			return false;
		}
		if (!campaignDeck.TryApplyRubySeal(target, RubySealPowerBonus))
		{
			campaignConsumables.Add(CampaignConsumableType.SigilloRubino);
			SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealAlreadyApplied));
			return false;
		}
		RecordCampaignItemUsed(CampaignConsumableType.SigilloRubino);
		rubySealTargetSelectionActive = false;
		PlayEmpowerItemUseSfx();
		string cardName = CardDisplayNames.MarketName(target.Definition);
		SetMessage(GameText.Format(GameTextKeys.Consumables.RubySealApplied, cardName));
		AppendLog(GameText.Format(GameTextKeys.Consumables.RubySealAppliedLog, cardName));
		if ((Object)(object)implementationArchivePanel != (Object)null && implementationArchivePanel.activeSelf)
		{
			RefreshImplementationArchive();
		}
		return true;
	}

	private bool HasRubySealTarget()
	{
		return playerCards.Any(IsRubySealBattleTarget);
	}

	private static bool IsRubySealBattleTarget(BattleCardState card)
	{
		return card != null
			&& !card.Eliminated
			&& !card.HasEquipment
			&& IsRubySealTarget(card.CampaignCard);
	}

	private void ShowRubySealTargetPanel()
	{
		CloseRubySealTargetPanel(cancelSelection: false);
		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		Image backdrop = CreateImage("Ruby Seal Target Backdrop", (Transform)(object)safeAreaRoot, Color.clear);
		backdrop.raycastTarget = true;
		Stretch(backdrop.rectTransform, 0f);
		rubySealTargetPanel = ((Component)backdrop).gameObject;
		Canvas overlayCanvas = rubySealTargetPanel.AddComponent<Canvas>();
		overlayCanvas.overrideSorting = true;
		overlayCanvas.sortingOrder = 5000;
		rubySealTargetPanel.AddComponent<GraphicRaycaster>();

		Image window = CreateImage("Ruby Seal Target Window", ((Component)backdrop).transform, Color.white);
		window.sprite = LoadSpriteResource("UI/Sanctuary/santuary_items");
		window.type = Image.Type.Simple;
		RectTransform windowRect = window.rectTransform;
		windowRect.anchorMin = new Vector2(0.08f, 0.1f);
		windowRect.anchorMax = new Vector2(0.92f, 0.9f);
		windowRect.offsetMin = Vector2.zero;
		windowRect.offsetMax = Vector2.zero;
		Text title = CreateText("Ruby Seal Target Title", ((Component)window).transform, font, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
		title.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Consumables.RubySealTargetTitle,
			"SIGILLO RUBINO\nScegli una pedina schierata da sigillare, prenderà +2 potenza permanente ma non potrà più ricevere equipaggiamenti dagli alleati");
		title.horizontalOverflow = HorizontalWrapMode.Wrap;
		title.verticalOverflow = VerticalWrapMode.Truncate;
		title.resizeTextForBestFit = true;
		title.resizeTextMinSize = 17;
		title.resizeTextMaxSize = 24;
		RectTransform titleRect = title.rectTransform;
		titleRect.anchorMin = new Vector2(0.08f, 0.72f);
		titleRect.anchorMax = new Vector2(0.92f, 0.91f);
		titleRect.offsetMin = Vector2.zero;
		titleRect.offsetMax = Vector2.zero;

		rubySealTargetCardsRoot = CreateCardRow("Ruby Seal Deployed Pawns", ((Component)window).transform, new Vector2(0.5f, 0.49f));
		rubySealTargetCardsRoot.anchorMin = new Vector2(0.09f, 0.34f);
		rubySealTargetCardsRoot.anchorMax = new Vector2(0.91f, 0.64f);
		rubySealTargetCardsRoot.offsetMin = Vector2.zero;
		rubySealTargetCardsRoot.offsetMax = Vector2.zero;
		HorizontalLayoutGroup cardsLayout = ((Component)rubySealTargetCardsRoot).GetComponent<HorizontalLayoutGroup>();
		cardsLayout.spacing = 12f;
		foreach (BattleCardState battleCard in playerCards.Where(IsRubySealBattleTarget))
		{
			PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)rubySealTargetCardsRoot, battleCard.Definition, configuration);
			view.SetInteractable(true);
			view.SetStrengthValue(DisplayStrength(battleCard));
			((UnityEvent)view.Button.onClick).AddListener((UnityAction)delegate
			{
				ApplyRubySealToBattleCard(battleCard);
			});
			rubySealTargetCardViews.Add(view);
		}

		Button cancel = CreateButton(
			"Cancel Ruby Seal Selection",
			((Component)window).transform,
			font,
			GameText.GetOrFallbackSilent(GameTextKeys.Common.Cancel, "ANNULLA"));
		RectTransform cancelRect = ((Component)cancel).GetComponent<RectTransform>();
		cancelRect.anchorMin = new Vector2(0.3f, 0.035f);
		cancelRect.anchorMax = new Vector2(0.7f, 0.17f);
		cancelRect.offsetMin = Vector2.zero;
		cancelRect.offsetMax = Vector2.zero;
		Text cancelLabel = ((Component)cancel).GetComponentInChildren<Text>();
		if ((Object)(object)cancelLabel != (Object)null)
			cancelLabel.fontSize = 26;
		((UnityEvent)cancel.onClick).AddListener((UnityAction)delegate { CloseRubySealTargetPanel(cancelSelection: true); });
		rubySealTargetPanel.transform.SetAsLastSibling();
	}

	private void ApplyRubySealToBattleCard(BattleCardState battleCard)
	{
		if (!IsRubySealBattleTarget(battleCard) || !TryApplyRubySealTo(battleCard.CampaignCard))
			return;
		battleCard.PermanentCombatBonus += RubySealPowerBonus;
		if ((Object)(object)battleCard.View != (Object)null)
			RefreshPersistentStatus(battleCard);
		CloseRubySealTargetPanel(cancelSelection: false);
		CloseImplementationArchive();
	}

	private void CloseRubySealTargetPanel(bool cancelSelection)
	{
		if (cancelSelection)
			rubySealTargetSelectionActive = false;
		foreach (PrototypeCardView view in rubySealTargetCardViews)
			if ((Object)(object)view != (Object)null) Object.Destroy(((Component)view).gameObject);
		rubySealTargetCardViews.Clear();
		if ((Object)(object)rubySealTargetPanel != (Object)null)
			Object.Destroy(rubySealTargetPanel);
		rubySealTargetPanel = null;
		rubySealTargetCardsRoot = null;
	}

	private static bool IsRubySealTarget(CampaignCardInstance card)
	{
		return card != null
			&& !card.HasRubySeal
			&& card.Definition != null
			&& card.Definition.CanEnterCombat
			&& card.Zone != AccardND.GameData.CampaignCardZone.Graveyard;
	}

	private static bool IsConsumableBlockedInBattle(CampaignConsumableType itemType)
	{
		return itemType == CampaignConsumableType.SecondChance;
	}

	private bool IsCampaignBattleActive()
	{
		if (IsRoomChoiceActive())
		{
			return false;
		}
		return campaignDeck != null
			&& (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss)
			&& (draftActive || deploymentDraftActive || roundNumber > 0 || playerCards.Count > 0 || cpuCards.Count > 0);
	}

	private bool IsRoomChoiceActive()
	{
		return (Object)(object)roomChoicePanel != (Object)null && roomChoicePanel.activeSelf;
	}

	private int RecoverAllGraveyardCards()
	{
		if (campaignDeck == null)
		{
			return 0;
		}
		int recovered = 0;
		foreach (var card in campaignDeck.Cards)
		{
			if (card.Zone == AccardND.GameData.CampaignCardZone.Graveyard && campaignDeck.RecoverFromGraveyard(card))
			{
				recovered++;
			}
		}
		return recovered;
	}

	private (string description, int bonusExperience) GrantRandomConsumable(string source)
	{
		return GrantRandomConsumable(source, out _);
	}

	private (string description, int bonusExperience) GrantRandomConsumable(
		string source,
		out CampaignConsumableType grantedItem)
	{
		CampaignConsumableType[] pool =
		{
			CampaignConsumableType.Detector,
			CampaignConsumableType.SecondChance,
			CampaignConsumableType.Empower,
			CampaignConsumableType.SigilloRubino,
			CampaignConsumableType.DoubleExp
			, CampaignConsumableType.ManaGain5
			, CampaignConsumableType.ManaGain10
			, CampaignConsumableType.Jolly
		};
		CampaignConsumableType itemType = pool[random.NextInclusive(0, pool.Length - 1)];
		grantedItem = itemType;
		campaignConsumables.Add(itemType);
		string itemName = CampaignConsumableName(itemType);
		AppendLog(GameText.Format(GameTextKeys.Consumables.GrantedLog, source, itemName));
		return (description: GameText.Format(GameTextKeys.Consumables.GrantedDescription, source, itemName), bonusExperience: 0);
	}

	/// <summary>
	/// Riempie la borsa di run con la bisaccia scelta al Santuario: un pezzo per oggetto
	/// selezionato. La scorta permanente non viene toccata qui, cala solo a fine run e solo
	/// per quello che e' stato davvero usato.
	/// </summary>
	private void LoadCampaignConsumablesFromBag()
	{
		campaignConsumables.Clear();
		runBagItemIds.Clear();
		consumedBagItemIds.Clear();
		List<string> bag = singlePlayerProgressService.Progress?.bagItems;
		if (bag == null || bag.Count == 0)
		{
			AppendLog(GameText.Get(GameTextKeys.Consumables.BagEmptyLog));
			WarmCampaignRunAds();
			return;
		}

		foreach (string itemId in bag)
		{
			if (!TryParseSanctuaryItemId(itemId, out CampaignConsumableType itemType))
			{
				continue;
			}
			campaignConsumables.Add(itemType);
			runBagItemIds.Add(itemId);
		}
		AppendLog(GameText.Format(GameTextKeys.Consumables.BagLoadedLog, runBagItemIds.Count));
		// La borsa e' composta: da qui in poi si sa se l'interstitial degli oggetti puo'
		// servire, ed e' il primo momento in cui questa run puo' chiedere annunci.
		WarmCampaignRunAds();
	}

	/// <summary>Id del catalogo Santuario -> tipo consumabile di run.</summary>
	private static bool TryParseSanctuaryItemId(string itemId, out CampaignConsumableType itemType)
	{
		switch (itemId)
		{
		case "detector":
			itemType = CampaignConsumableType.Detector;
			return true;
		case "second-chance":
			itemType = CampaignConsumableType.SecondChance;
			return true;
		case "empower":
			itemType = CampaignConsumableType.Empower;
			return true;
		case "sigillo-rubino":
		case "ruby-seal":
			itemType = CampaignConsumableType.SigilloRubino;
			return true;
		case "double-exp":
			itemType = CampaignConsumableType.DoubleExp;
			return true;
		case "mana-5":
		case "mana_gain_5":
			itemType = CampaignConsumableType.ManaGain5;
			return true;
		case "mana-10":
		case "mana_gain_10":
			itemType = CampaignConsumableType.ManaGain10;
			return true;
		case "jolly":
			itemType = CampaignConsumableType.Jolly;
			return true;
		default:
			itemType = CampaignConsumableType.Detector;
			return false;
		}
	}

	private static string SanctuaryItemIdOf(CampaignConsumableType itemType) => itemType switch
	{
		CampaignConsumableType.Detector => "detector",
		CampaignConsumableType.SecondChance => "second-chance",
		CampaignConsumableType.Empower => "empower",
		CampaignConsumableType.SigilloRubino => "sigillo-rubino",
		CampaignConsumableType.DoubleExp => "double-exp",
		CampaignConsumableType.ManaGain5 => "mana-5",
		CampaignConsumableType.ManaGain10 => "mana-10",
		CampaignConsumableType.Jolly => "jolly",
		_ => null
	};

	/// <summary>
	/// Segna un oggetto come usato. Il contatore delle quest non guarda la provenienza: vale
	/// tanto un oggetto della bisaccia quanto uno trovato in una stanza o comprato al mercante
	/// e usato subito. La bisaccia si conta a parte perche' solo quella scala la scorta.
	///
	/// E' anche il punto in cui parte l'interstitial dell'uso oggetti, ed e' l'unico che va
	/// bene: qui l'oggetto e' gia' stato tolto dalla borsa, quindi la pubblicita' non puo'
	/// partire per un uso poi rifiutato (sigillo senza bersaglio, potenziamento in stanza
	/// boss). Le regole di frequenza fanno il resto: tre oggetti di fila restano una
	/// pubblicita' sola.
	/// </summary>
	private void RecordCampaignItemUsed(CampaignConsumableType itemType)
	{
		if (ShouldTrackQuestProgress)
			runProgress?.RecordItemUsed();
		string itemId = SanctuaryItemIdOf(itemType);
		if (string.IsNullOrEmpty(itemId) || !runBagItemIds.Remove(itemId))
		{
			return;
		}
		consumedBagItemIds.Add(itemId);
		AccardND.Ads.AdService.ShowInterstitial(AccardND.Ads.AdPlacement.BagItemUsed);
	}

	/// <summary>
	/// Gli oggetti ancora in mano a fine run che il server deve versare nella scorta: quelli
	/// trovati nelle stanze o comprati al mercante e mai usati. Gli oggetti della bisaccia
	/// rimasti (<see cref="runBagItemIds"/>) restano fuori: non sono mai usciti dalla scorta,
	/// aggiungerli li duplicherebbe a ogni run.
	/// </summary>
	private string[] CollectUnusedRunItemIds()
	{
		if (campaignConsumables == null)
		{
			return Array.Empty<string>();
		}
		var kept = new List<string>();
		foreach (CampaignConsumableType itemType in Enum.GetValues(typeof(CampaignConsumableType)))
		{
			string itemId = SanctuaryItemIdOf(itemType);
			if (string.IsNullOrEmpty(itemId))
			{
				continue;
			}
			int stillInBag = 0;
			foreach (string bagItemId in runBagItemIds)
			{
				if (bagItemId == itemId)
					stillInBag++;
			}
			int foundInRun = campaignConsumables.GetQuantity(itemType) - stillInBag;
			for (int index = 0; index < foundInRun; index++)
			{
				kept.Add(itemId);
			}
		}
		return kept.ToArray();
	}

	/// <summary>
	/// Chiude sul server una campagna abbandonata che ha lasciato qualcosa in sospeso sugli
	/// oggetti: consumi della bisaccia da scalare, usi da contare per le quest o oggetti
	/// trovati in run da versare nella scorta. Tornare al menu cancella il salvataggio, quindi
	/// e' l'ultimo momento utile. Il riepilogo persistente e' lo stesso usato a fine run,
	/// percio' tutto sopravvive anche se la connessione cade mentre si torna al menu.
	/// </summary>
	private async System.Threading.Tasks.Task PersistConsumedBagItemsOnCampaignExitAsync()
	{
		if (runProgress == null)
		{
			return;
		}

		string[] keptItemIds = CollectUnusedRunItemIds();
		if (consumedBagItemIds.Count == 0 && runProgress.ItemsUsed == 0 && keptItemIds.Length == 0)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(campaignRunRewardId))
		{
			campaignRunRewardId = System.Guid.NewGuid().ToString("N");
		}

		var summary = new AccardND.Network.DeathRewardSummary(
			campaignRunRewardId,
			"campaign",
			string.IsNullOrWhiteSpace(activeAdventureChapterId) ? "free-run" : activeAdventureChapterId,
			string.IsNullOrWhiteSpace(campaignScenarioId) ? "default" : campaignScenarioId,
			runProgress.RoomsCleared,
			runProgress.EnemiesDefeated,
			0,
			Mathf.Max(0, runProgress.AvailableExperience),
			runProgress.MinibossesDefeated,
			defeatedBossIdsInRun.ToArray(),
			consumedBagItemIds.ToArray(),
			runProgress.DiceRolled,
			runProgress.AbilitiesUsed,
			runProgress.TotalExperience,
			runProgress.SupremesUsed,
			runProgress.QuickChallengesCompleted,
			runProgress.MerchantPurchases,
			runProgress.GoldEarned,
			runProgress.LevelsGained,
			runProgress.ItemsUsed,
			keptItemIds);

		if (serverProgress != null || await EnsureServerProgressAsync())
		{
			try
			{
				await serverProgress.ClaimDeathRewardAsync(summary);
				MirrorServerProgress();
				return;
			}
			catch (System.Exception exception)
			{
				// La richiesta online e' persistente ed e' gia' nell'outbox: verra' ritentata.
				AppendLog($"ACCOUNT - consumo oggetti in attesa di sincronizzazione: {exception.Message}");
				return;
			}
		}

		AccardND.Network.ServerSinglePlayerProgressClient.QueueDeathRewardForReplay(summary);
	}

	private int ConsumeNextRoomExperienceMultiplier()
	{
		if (!nextRoomDoubleExperience)
		{
			return 1;
		}
		nextRoomDoubleExperience = false;
		AppendLog(GameText.Get(GameTextKeys.Consumables.DoubleExperienceConsumedLog));
		return 2;
	}

	private void ShowCampaignConsumableInspection(CampaignConsumableType itemType)
	{
		if ((Object)(object)cardInspectionPanel == (Object)null || (Object)(object)cardInspectionSlot == (Object)null)
		{
			return;
		}
		if ((Object)(object)inspectedCardView != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)inspectedCardView).gameObject);
			inspectedCardView = null;
		}
		ClearInspectionStatusRows();
		inspectedCampaignConsumableActive = true;
		inspectedCampaignConsumableType = itemType;
		Image icon = CreateImage("Consumable Inspection Icon", (Transform)(object)cardInspectionSlot, Color.white);
		icon.sprite = LoadSpriteResource("UI/" + CampaignConsumableResourceName(itemType));
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		Stretch(icon.rectTransform);
		cardInspectionStatusRows.Add(((Component)icon).gameObject);
		cardInspectionSummaryText.text = GameText.Format(
			GameTextKeys.Consumables.InspectionSummary,
			CampaignConsumableName(itemType),
			CampaignConsumableDescription(itemType),
			campaignConsumables?.GetQuantity(itemType) ?? 0);
		if ((Object)(object)cardInspectionDraftConfirmButton != (Object)null)
		{
			bool canUse = campaignDeck != null
				&& !pvpPresentationActive
				&& (campaignConsumables?.GetQuantity(itemType) ?? 0) > 0
				&& !(IsConsumableBlockedInBattle(itemType) && IsCampaignBattleActive());
			((Component)cardInspectionDraftConfirmButton).gameObject.SetActive(canUse);
			cardInspectionDraftConfirmButton.interactable = canUse;
			if ((Object)(object)cardInspectionDraftConfirmButtonText != (Object)null)
			{
				cardInspectionDraftConfirmButtonText.text = GameText.Get(GameTextKeys.Common.Use);
			}
			if ((Object)(object)cardInspectionDraftConfirmButtonRect != (Object)null)
			{
				SetCardInspectionConfirmButtonRect(Screen.width > Screen.height);
			}
			if (canUse)
			{
				((Component)cardInspectionDraftConfirmButton).transform.SetAsLastSibling();
			}
		}
		cardInspectionPanel.SetActive(true);
		PlayCardInspectionOpenSfx();
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
			((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
		}
	}
}
}
