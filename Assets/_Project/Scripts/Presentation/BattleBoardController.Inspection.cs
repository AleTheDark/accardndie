using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private CardDefinition inspectedDefinition;
	private BattleCardState inspectedState;

	public void ShowPvpLoadoutCardInspection(
		CardDefinition definition,
		UnityAction onAdd,
		bool canAdd,
		string buttonText)
	{
		inspectedPvpLoadoutAddAction = onAdd;
		inspectedPvpLoadoutActive = true;
		ShowCardInspection(definition);
		Canvas inspectionCanvas = cardInspectionPanel != null
			? cardInspectionPanel.GetComponent<Canvas>()
			: null;
		if ((Object)(object)inspectionCanvas != (Object)null)
			inspectionCanvas.sortingOrder = 970;
		// Nella Loadout la scelta/rimozione avviene direttamente nella lista delle carte.
		// L'ispezione resta quindi puramente informativa e non mostra il pulsante del draft.
		HideCardInspectionDraftConfirm();
	}

	private void ShowCardInspection(CardDefinition definition)
	{
		ShowCardInspection(definition, null);
	}

	private void ShowCardInspection(BattleCardState state)
	{
		if (state != null)
		{
			ShowCardInspection(state.Definition, state);
		}
	}

	private void ShowCardInspection(CardDefinition definition, BattleCardState state)
	{
		// Aprire l'ispezione e' il gesto che il giocatore fa naturalmente quando
		// qualcosa non risponde: ne approfittiamo per liberare in silenzio lo stato
		// dello swipe rimasto appeso, cosi' il turno torna giocabile da solo.
		ReleaseStuckSwipeGestures("apertura inspection");

		// Inspection and target selection must never compete for the same card click.
		// Keep this guard here too: some UI callbacks call this method directly and
		// may survive an interaction refresh.
		if (attackTargetingActive
			|| abilityTargetMode != AbilityTargetMode.None
			|| pendingAbilityUser != null
			|| activeAttachmentSource != null)
		{
			LogInspectionState("SHOW_INSPECTION_GUARD_BLOCKED", state);
			return;
		}
		LogInspectionState("SHOW_INSPECTION_ACCEPTED", state);

		if (!((Object)(object)definition == (Object)null) && !((Object)(object)cardInspectionPanel == (Object)null) && !((Object)(object)cardInspectionSlot == (Object)null))
		{
			inspectedDefinition = definition;
			inspectedState = state;
			RefreshCardInspectionLayout();
			bool seraphelInspection = IsSeraphelBossDefinition(definition)
				|| string.Equals(definition.Id, SeraphelPhaseTwoCardId, StringComparison.OrdinalIgnoreCase);
			bool jurinashorInspection = IsJurinashorBossDefinition(definition);
			if (seraphelInspection)
			{
				bool landscape = Screen.width > Screen.height;
				SetRect(cardInspectionSlot,
					landscape ? new Vector2(0.075f, 0.07f) : new Vector2(0.17f, 0.55f),
					landscape ? new Vector2(0.54f, 0.96f) : new Vector2(0.83f, 0.96f));
				if ((Object)(object)cardInspectionContentViewport != (Object)null && !landscape)
				{
					SetRect(cardInspectionContentViewport, new Vector2(0.12f, 0.07f), new Vector2(0.84f, 0.535f));
					cardInspectionContentViewport.offsetMax = new Vector2(CardInspectionViewportRightExpansion, cardInspectionContentViewport.offsetMax.y);
				}
			}
			if ((Object)(object)inspectedCardView != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)inspectedCardView).gameObject);
			}
			ClearInspectionStatusRows();
			inspectedCardView = PrototypeCardView.Create((Transform)(object)cardInspectionSlot, definition, configuration);
			inspectedCardView.SetInteractable(interactable: false);
			inspectedCardView.ClearActionOverlay();
			Stretch(inspectedCardView.RectTransform);
			AspectRatioFitter aspectRatioFitter = ((Component)inspectedCardView).gameObject.AddComponent<AspectRatioFitter>();
			aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			aspectRatioFitter.aspectRatio = 0.6708861f;
			if (seraphelInspection)
				inspectedCardView.ConfigureSeraphelInspectionArtwork(definition);
			if (jurinashorInspection)
				inspectedCardView.ConfigureJurinashorInspectionArtwork(
					definition,
					state != null && IsJurinashorImmuneToDebuffs(state));
			if (state != null)
			{
				inspectedCardView.SetStrengthValue(DisplayStrength(state));
			}
			UpdateCardInspectionSummary(definition, state);
			// UpdateCardInspectionSummary applica il layout generico del contenuto.
			// Seraphel va riallineata dopo quel passaggio, così il testo comincia
			// realmente sotto il bordo inferiore della carta ingrandita.
			if (seraphelInspection)
			{
				bool landscape = Screen.width > Screen.height;
				SetRect(cardInspectionSlot,
					landscape ? new Vector2(0.075f, 0.07f) : new Vector2(0.17f, 0.55f),
					landscape ? new Vector2(0.54f, 0.96f) : new Vector2(0.83f, 0.96f));
				if ((Object)(object)cardInspectionContentViewport != (Object)null)
				{
					SetRect(cardInspectionContentViewport,
						landscape ? new Vector2(0.56f, 0.07f) : new Vector2(0.12f, 0.07f),
						landscape ? new Vector2(0.915f, 0.89f) : new Vector2(0.84f, 0.525f));
					cardInspectionContentViewport.offsetMax = new Vector2(CardInspectionViewportRightExpansion, cardInspectionContentViewport.offsetMax.y);
				}
			}
			HideCardInspectionDraftConfirm();
			PauseGameForCardInspection();
			cardInspectionPanel.SetActive(true);
			RefreshCardInspectionPreferredHeights();
			PlayCardInspectionOpenSfx();
			NotifyAdventureTutorial(AdventureTutorialAction.CardInspected);
			if ((Object)(object)cardInspectionCloseButton != (Object)null)
			{
				((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
			}
		}
	}

	private void RefreshOpenCardInspectionLocale()
	{
		if ((Object)(object)cardInspectionPanel == (Object)null || !cardInspectionPanel.activeSelf || (Object)(object)inspectedDefinition == (Object)null)
			return;

		UpdateCardInspectionSummary(inspectedDefinition, inspectedState);
		RefreshCardInspectionPreferredHeights();
	}

	private void UpdateCardInspectionSummary(CardDefinition definition, BattleCardState state)
	{
		if ((Object)(object)cardInspectionSummaryText == (Object)null)
		{
			return;
		}
		int num = ((state != null) ?DisplayStrength(state) : definition.Strength);
		string strengthText = state != null && num != definition.Strength
			? InspectionText(GameTextKeys.Inspection.StrengthChanged, "Potenza: {0} -> {1}", "Power: {0} -> {1}", "Stärke: {0} -> {1}", "Poder: {0} -> {1}", "Puissance : {0} -> {1}", definition.Strength, num)
			: InspectionText(GameTextKeys.Inspection.Strength, "Potenza: {0}", "Power: {0}", "Stärke: {0}", "Poder: {0}", "Puissance : {0}", definition.Strength);
		string familyText = InspectionText(GameTextKeys.Inspection.NoFamily, "Fazione: Nessuna", "Faction: None", "Familie: Keine", "Familia: Ninguna", "Famille : Aucune");
		string classText = InspectionText(GameTextKeys.Inspection.NoClass, "Classe: Nessuna", "Class: None", "Klasse: Keine", "Clase: Ninguna", "Classe : Aucune");
		string advantageText = InspectionText(GameTextKeys.Inspection.Advantage, "Vantaggio contro Nessuno", "Advantage against None", "Vorteil gegen Keine", "Ventaja contra Ninguna", "Avantage contre Aucune");
		string disadvantageText = GameText.GetLocalizedFallback(GameTextKeys.Combat.DisadvantageAgainst, "Svantaggio contro {0}", "Disadvantage against {0}", "Nachteil gegen {0}", "Desventaja contra {0}", "Désavantage contre {0}", "Nessuno");
		string familyAuraLabel = InspectionText(GameTextKeys.Inspection.FamilyAura, "AURA FAZIONE NESSUNA", "NO FACTION AURA", "KEINE FAMILIENAURA", "SIN AURA DE FAMILIA", "AUCUNE AURA DE FAMILLE");
		string familyAuraDescription = "Nessuna aura di fazione.";
		string classAuraLabel = "AURA CLASSE NESSUNA";
		string classAuraDescription = "Nessuna aura di classe.";
		BattleAuraType familyAura = BattleAuraType.None;
		BattleAuraType classAura = BattleAuraType.None;
		bool isBossOrMiniboss = IsBossOrMinibossInspectionCard(definition);
		if (definition.HasHeroClass)
		{
			ClassFamily classFamily = HeroClassFamily.Of(definition.HeroClass);
			familyAura = FamilyAuraFor(classFamily);
			classAura = ClassAuraFor(definition.HeroClass);
			familyText = InspectionText(GameTextKeys.Inspection.Family, "Fazione: {0}", "Faction: {0}", "Familie: {0}", "Familia: {0}", "Famille : {0}", CardRulesGlossary.ClassFamilyName(classFamily));
			classText = InspectionText(GameTextKeys.Inspection.Class, "Classe: {0}", "Class: {0}", "Klasse: {0}", "Clase: {0}", "Classe : {0}", CardRulesGlossary.HeroClassName(definition.HeroClass));
			advantageText = InspectionText(GameTextKeys.Inspection.Advantage, "Vantaggio contro {0}", "Advantage against {0}", "Vorteil gegen {0}", "Ventaja contra {0}", "Avantage contre {0}", CardRulesGlossary.ClassFamilyName(StrongAgainst(classFamily)));
			disadvantageText = GameText.GetLocalizedFallback(GameTextKeys.Combat.DisadvantageAgainst, "Svantaggio contro {0}", "Disadvantage against {0}", "Nachteil gegen {0}", "Desventaja contra {0}", "Désavantage contre {0}", CardRulesGlossary.ClassFamilyName(WeakAgainst(classFamily)));
			familyAuraLabel = AuraInspectionLabel(familyAura);
			familyAuraDescription = FamilyAuraSummary(classFamily);
			classAuraLabel = AuraInspectionLabel(classAura);
			classAuraDescription = ClassAuraSummary(definition.HeroClass);
		}
		List<InspectionStatusDetail> ruleDetails = new List<InspectionStatusDetail>();
		List<InspectionStatusDetail> statusDetails = BuildInspectionStatusDetails(state);
		bool fieldInspection = state != null;
		ApplyCardInspectionContentLayout(fieldInspection && isBossOrMiniboss && statusDetails.Count > 0);
		ConfigureCardInspectionText(cardInspectionSummaryText);
		if (!isBossOrMiniboss && !fieldInspection && definition.HasHeroClass)
		{
			ruleDetails.Add(new InspectionStatusDetail(familyAuraLabel, familyAuraDescription, AuraColor(familyAura), AuraIconStatus(familyAura)));
			ruleDetails.Add(new InspectionStatusDetail(classAuraLabel, classAuraDescription, AuraColor(classAura), AuraIconStatus(classAura)));
		}
		if (ShouldShowAbilitySummary(definition))
		{
			ruleDetails.Add(new InspectionStatusDetail(CardAbilityInspectionLabel(definition), CardAbilityInspectionDescription(definition), new Color(1f, 0.78f, 0.24f)));
			if (ShouldShowSupremeSummary(definition, state))
			{
				ruleDetails.Add(new InspectionStatusDetail(
					CardSupremeInspectionLabel(definition, state),
					CardSupremeInspectionDescription(definition),
					SupremeInspectionColor));
			}
		}
		if (ShouldShowEquipSummary(definition, state))
		{
			ruleDetails.Add(new InspectionStatusDetail(
				"EQUIPAGGIA",
				$"Questa carta puo essere sacrificata per potenziare un alleato di +{AttachmentBonusForInspection(definition, state)} permanentemente. Se l'alleato equipaggiato muore, muore anche questa carta.",
				new Color(1f, 0.62f, 0.2f)));
		}
		List<string> summaryLines = new List<string>
		{
			strengthText,
			familyText,
			classText,
			advantageText,
			disadvantageText
		};
		string inspectionRules = JurinashorInspectionRules(definition, state);
		if (isBossOrMiniboss && !string.IsNullOrWhiteSpace(inspectionRules))
		{
			summaryLines.Add("Regole:");
			summaryLines.Add(CompactInspectionText(inspectionRules));
		}
		cardInspectionSummaryText.text = string.Join("\n", summaryLines);
		foreach (InspectionStatusDetail item in ruleDetails)
		{
			CreateInspectionStatusRow(item);
		}
		if (statusDetails.Count > 0)
		{
			CreateInspectionStatusHeader();
		}
		foreach (InspectionStatusDetail item in statusDetails)
		{
			CreateInspectionStatusRow(item);
		}
		RefreshCardInspectionPreferredHeights();
	}

	private string JurinashorInspectionRules(CardDefinition definition, BattleCardState state)
	{
		if (!IsJurinashorBossDefinition(definition))
			return definition?.RulesText;

		bool phaseTwo = state != null && activeJurinashorBoss != null && activeJurinashorBoss.IsPhaseTwo;
		return phaseTwo
			? "FASE II — RINASCITA NECROMANTICA\n30 HP. All'ingresso rimuove tutti i malus ed è immune ai nuovi malus. Può controllare fino a 5 Spade Maledette. Quando para o termina il turno recupera il doppio del mana. Ogni 3 mana spende 3 mana ed evoca una spada. Ogni spada fornisce +2 Potenza in attacco e difesa, può essere bersagliata singolarmente o da effetti ad area e non attacca autonomamente. Quando Jurinashor elimina una pedina evoca una spada, senza spendere mana, fino al limite."
			: "FASE I — ARSENALE MALEDETTO\n30 HP. Ogni 3 mana spende 3 mana ed evoca una Spada Maledetta, fino a 3 spade. Ogni spada fornisce +2 Potenza in attacco e difesa, può essere bersagliata singolarmente o da effetti ad area e non attacca autonomamente. Quando Jurinashor elimina una pedina evoca una spada, senza spendere mana, fino al limite. Gli attacchi di Jurinashor non consumano mana.";
	}

	private static bool IsBossOrMinibossInspectionCard(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& definition.Category == CardCategory.Boss;
	}

	private string CardAbilitySummary(CardDefinition definition)
	{
		if ((Object)(object)definition == (Object)null || !definition.HasHeroClass)
		{
			if (!string.IsNullOrWhiteSpace(definition?.RulesText))
			{
				return "Abilit\u00E0:\n" + definition.RulesText;
			}
			return "Abilit\u00E0:\nNessuna abilita di combattimento.";
		}
		string description = CardRulesGlossary.AbilityDescription(definition.HeroClass, configuration.ClassBalance);
		return CardRulesGlossary.AbilityTitle(definition.HeroClass) + ":\n" + description;
	}

	private string CardAbilityInspectionLabel(CardDefinition definition)
	{
		if ((Object)(object)definition == (Object)null || !definition.HasHeroClass)
		{
			return "ABILITA'";
		}
		string label = IsPassiveClassAbility(definition.HeroClass)
			? InspectionText(GameTextKeys.Inspection.PassiveAbility, "ABILITÀ PASSIVA {0}", "PASSIVE {0} ABILITY", "PASSIVE FÄHIGKEIT: {0}", "HABILIDAD PASIVA: {0}", "COMPÉTENCE PASSIVE : {0}", CardRulesGlossary.HeroClassName(definition.HeroClass).ToUpperInvariant())
			: InspectionText(GameTextKeys.Inspection.Ability, "ABILITÀ {0}", "{0} ABILITY", "FÄHIGKEIT: {0}", "HABILIDAD: {0}", "COMPÉTENCE : {0}", CardRulesGlossary.HeroClassName(definition.HeroClass).ToUpperInvariant());
		return label + ManaCostSuffix(PrimaryCostForInspection(definition.HeroClass));
	}

	/// <summary>
	/// Costo che verrebbe addebitato adesso, non il valore a listino: per la suprema
	/// include la ripetizione di classe del round. Fuori partita (deck builder,
	/// Santuario) non c'e' una riserva viva, quindi si mostra la base.
	/// </summary>
	private int PrimaryCostForInspection(HeroClass heroClass)
	{
		return CampaignManaEnabled
			? campaignPlayerMana.CostOfPrimary(heroClass)
			: AbilityManaCosts.Primary(heroClass);
	}

	private int SupremeCostForInspection(HeroClass heroClass, BattleCardState state = null)
	{
		if (pvpPresentationActive && pvpState != null && state != null
			&& pvpCardSlots.TryGetValue(state, out int slot))
		{
			bool isLocalCard = playerCards.Contains(state);
			int owner = isLocalCard ? pvpState.MyIndex : OpponentIndex();
			return pvpState.SupremeCostFor(owner, heroClass);
		}
		return CampaignManaEnabled
			? campaignPlayerMana.CostOfSupreme(heroClass)
			: AbilityManaCosts.Supreme(heroClass);
	}

	private int SupremeUsesForInspection(HeroClass heroClass, BattleCardState state = null)
	{
		if (pvpPresentationActive && pvpState != null && state != null
			&& pvpCardSlots.ContainsKey(state))
		{
			bool isLocalCard = playerCards.Contains(state);
			int owner = isLocalCard ? pvpState.MyIndex : OpponentIndex();
			return pvpState.SupremeUsesFor(owner, heroClass);
		}
		return CampaignManaEnabled && state != null && state.BelongsToPlayer
			? campaignPlayerMana.SupremeUses(heroClass)
			: 0;
	}

	/// <summary>Suffisso da attaccare all'intestazione. Le passive costano 0 e non lo mostrano.</summary>
	private static string ManaCostSuffix(int cost)
	{
		return cost <= 0 ? string.Empty : GameText.GetLocalizedFallback(GameTextKeys.Inspection.ManaSuffix, " ({0} MANA)", " ({0} MANA)", " ({0} MANA)", " ({0} MANÁ)", " ({0} MANA)", cost);
	}

	private string CardAbilityInspectionDescription(CardDefinition definition)
	{
		if ((Object)(object)definition == (Object)null || !definition.HasHeroClass)
		{
			return !string.IsNullOrWhiteSpace(definition?.RulesText)
				? definition.RulesText
				: "Nessuna abilita di combattimento.";
		}
		if (definition.HeroClass == HeroClass.Assassin)
		{
			return "Fai saltare il turno di gioco ad una carta avversaria";
		}
		string description = CardRulesGlossary.AbilityDescription(definition.HeroClass, configuration.ClassBalance);
		const string passivePrefix = "Abilita passiva: ";
		if (IsPassiveClassAbility(definition.HeroClass) && description.StartsWith(passivePrefix, StringComparison.OrdinalIgnoreCase))
		{
			description = description.Substring(passivePrefix.Length);
		}
		// Il costo sta nell'intestazione, non in coda: si legge prima di decidere
		// se l'abilita' ti interessa, non dopo aver finito la descrizione.
		return description;
	}

	private static bool IsPassiveClassAbility(HeroClass heroClass)
	{
		return heroClass == HeroClass.Barbarian
			|| heroClass == HeroClass.Rogue;
	}

	// --- Abilita' suprema ---

	private static readonly Color SupremeInspectionColor = new Color(0.44f, 0.86f, 0.36f);

	/// <summary>Id dell'unlock al Santuario, es. "ability-mage-2".</summary>
	private static string SupremeUnlockId(HeroClass heroClass) =>
		$"ability-{heroClass.ToString().ToLowerInvariant()}-2";

	/// <summary>
	/// La suprema compare solo se la tecnica e' stata appresa: mostrarla bloccata
	/// occuperebbe spazio nell'ispezione senza dare niente al giocatore.
	/// </summary>
	private bool ShouldShowSupremeSummary(CardDefinition definition, BattleCardState state)
	{
		if ((Object)(object)definition == (Object)null || !definition.HasHeroClass)
		{
			return false;
		}
		if (!CardRulesGlossary.HasSupreme(definition.HeroClass))
		{
			return false;
		}
		// In una stanza in cui la CPU puo usare le supreme (attualmente la
		// Diabolica), l'ispezione deve spiegare anche la tecnica del nemico.
		// Lo sblocco al Santuario riguarda soltanto le carte del giocatore e non
		// deve nascondere un'azione che l'avversario puo effettivamente eseguire.
		if (state != null
			&& !state.BelongsToPlayer
			&& !pvpPresentationActive
			&& RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesSupremes)
		{
			return true;
		}
		return IsSupremeUnlocked(definition.HeroClass);
	}

	private bool IsSupremeUnlocked(HeroClass heroClass)
	{
		// Dentro una lezione di classe la tecnica si prova senza possederla: e' una sandbox,
		// e il permesso vale solo li'. Fuori dalla lezione la regola resta quella del
		// Santuario, che e' dove la tecnica si compra davvero.
		if (IsTutorialSandboxSupremeAllowed(heroClass))
		{
			return true;
		}
		return singlePlayerProgressService != null
			&& singlePlayerProgressService.IsUnlocked(
				AccardND.GameData.SinglePlayerUnlockType.SecondAbility,
				SupremeUnlockId(heroClass));
	}

	private string CardSupremeInspectionLabel(CardDefinition definition, BattleCardState state)
	{
		return InspectionText(GameTextKeys.Inspection.Supreme, "SUPREMA: {0}", "SUPREME: {0}", "ULTIMATIVE: {0}", "SUPREMA: {0}", "SUPRÊME : {0}", CardRulesGlossary.SupremeName(definition.HeroClass).ToUpperInvariant())
			+ ManaCostSuffix(SupremeCostForInspection(definition.HeroClass, state));
	}

	private static string CardSupremeInspectionDescription(CardDefinition definition)
	{
		return CardRulesGlossary.SupremeDescription(definition.HeroClass);
	}

	private static string HeroClassDisplayName(HeroClass heroClass)
	{
		return CardRulesGlossary.HeroClassName(heroClass);
	}

	private static string ClassFamilyDisplayName(ClassFamily family)
	{
		return CardRulesGlossary.ClassFamilyName(family);
	}

	private static ClassFamily StrongAgainst(ClassFamily family)
	{
		return family switch
		{
			ClassFamily.Might => ClassFamily.Cunning, 
			ClassFamily.Cunning => ClassFamily.Magic, 
			ClassFamily.Magic => ClassFamily.Might, 
			_ => ClassFamily.Might, 
		};
	}

	private static ClassFamily WeakAgainst(ClassFamily family)
	{
		return family switch
		{
			ClassFamily.Might => ClassFamily.Magic, 
			ClassFamily.Cunning => ClassFamily.Might, 
			ClassFamily.Magic => ClassFamily.Cunning, 
			_ => ClassFamily.Might, 
		};
	}

	private static BattleAuraType FamilyAuraFor(ClassFamily family)
	{
		return family switch
		{
			ClassFamily.Might => BattleAuraType.Might,
			ClassFamily.Cunning => BattleAuraType.Cunning,
			ClassFamily.Magic => BattleAuraType.Magic,
			_ => BattleAuraType.None,
		};
	}

	private static BattleAuraType ClassAuraFor(HeroClass heroClass)
	{
		return heroClass switch
		{
			HeroClass.Warrior => BattleAuraType.Warrior,
			HeroClass.Barbarian => BattleAuraType.Barbarian,
			HeroClass.Paladin => BattleAuraType.Paladin,
			HeroClass.Rogue => BattleAuraType.Rogue,
			HeroClass.Assassin => BattleAuraType.Assassin,
			HeroClass.Hunter => BattleAuraType.Hunter,
			HeroClass.Mage => BattleAuraType.Mage,
			HeroClass.Necromancer => BattleAuraType.Necromancer,
			HeroClass.Priest => BattleAuraType.Priest,
			_ => BattleAuraType.None,
		};
	}

	private static string FamilyAuraSummary(ClassFamily family)
	{
		return AuraEffectText(FamilyAuraFor(family));
	}

	private static string ClassAuraSummary(HeroClass heroClass)
	{
		return AuraEffectText(ClassAuraFor(heroClass));
	}

	private bool ShouldShowEquipSummary(CardDefinition definition, BattleCardState state)
	{
		if (IsBossOrMinibossInspectionCard(definition))
		{
			return false;
		}
		if (state != null)
		{
			return CanUseAttachment(state) || IsEquipCapableInspectionCard(definition, state);
		}
		return IsEquipCapableInspectionCard(definition, state);
	}

	private static int AttachmentBonusForInspection(CardDefinition definition, BattleCardState state)
	{
		if (state != null)
		{
			return state.Card != null ?5 - state.Card.Strength : 0;
		}
		return definition != null ?5 - definition.Strength : 0;
	}

	private static bool ShouldShowAbilitySummary(CardDefinition definition)
	{
		if ((Object)(object)definition == (Object)null || IsBossOrMinibossInspectionCard(definition))
		{
			return false;
		}
		return definition.HasHeroClass || !string.IsNullOrWhiteSpace(definition.RulesText);
	}

	private static bool IsEquipCapableInspectionCard(CardDefinition definition, BattleCardState state)
	{
		if (state != null && state.Card != null)
		{
			return state.Card.Strength >= 2 && state.Card.Strength < 5;
		}
		return definition != null && definition.CanEnterCombat && definition.Strength >= 2 && definition.Strength < 5;
	}

	private List<InspectionStatusDetail> BuildInspectionStatusDetails(BattleCardState state)
	{
		List<InspectionStatusDetail> list = new List<InspectionStatusDetail>();
		if (state == null)
		{
			return list;
		}
		if (state.Eliminated)
		{
			list.Add(new InspectionStatusDetail("ELIMINATA", "La carta e eliminata: non puo agire e andra al cimitero se appartiene alla tua formazione.", new Color(0.95f, 0.12f, 0.12f)));
			return list;
		}
		int supremeUses = SupremeUsesForInspection(state.Card.HeroClass, state);
		if (supremeUses > 0)
		{
			int supremeCost = SupremeCostForInspection(state.Card.HeroClass, state);
			list.Add(new InspectionStatusDetail(
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.SupremeCostMalusStatus, "MALUS SUPREMA {0} MANA", "SUPREME PENALTY {0} MANA", "HÖCHSTE-FÄHIGKEIT-MALUS {0} MANA", "PENALIZACIÓN SUPREMA {0} MANÁ", "MALUS SUPRÊME {0} MANA", supremeCost),
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.SupremeCostMalusDescription, "Suprema usata {0} volte in questa partita: il prossimo uso costa {1} mana.", "Supreme used {0} times in this match: the next use costs {1} Mana.", "Höchste Fähigkeit wurde in diesem Spiel {0}-mal benutzt: Die nächste Nutzung kostet {1} Mana.", "Suprema usada {0} veces en esta partida: el próximo uso cuesta {1} de Maná.", "Capacité suprême utilisée {0} fois dans cette partie : la prochaine utilisation coûte {1} Mana.", supremeUses, supremeCost),
				new Color(1f, 0.38f, 0.32f), "debuff_supreme_cost"));
		}
		if (state.SeraphelSeals > 0)
		{
			int sealDamage = state.SeraphelSeals * SeraphelBoss.DamagePerSeal;
			list.Add(new InspectionStatusDetail(
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.SeraphelSealsStatus, "SIGILLI {0} - MALUS", "SEALS {0} - PENALTY", "SIEGEL {0} - MALUS", "SELLOS {0} - PENALIZACIÓN", "SCEAUX {0} - MALUS", state.SeraphelSeals),
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.SeraphelSealsDescription, "Malus persistente di Seraphel: il boss ottiene +{0} contro questa carta. Al terzo Sigillo la carta viene eliminata automaticamente.", "Seraphel's persistent penalty: the boss gains +{0} against this card. At the third Seal, the card is eliminated automatically.", "Seraphels dauerhafter Malus: Der Boss erhält +{0} gegen diese Karte. Beim dritten Siegel wird die Karte automatisch eliminiert.", "Penalización persistente de Seraphel: el jefe obtiene +{0} contra esta carta. Al tercer Sello, la carta se elimina automáticamente.", "Malus persistant de Seraphel : le boss gagne +{0} contre cette carte. Au troisième Sceau, la carte est éliminée automatiquement.", sealDamage),
				new Color(1f, 0.22f, 0.18f)));
		}
		BattleAuraType battleAuraType = AuraForCard(state);
		if (battleAuraType != BattleAuraType.None)
		{
			list.Add(new InspectionStatusDetail("AURA " + AuraShortLabel(battleAuraType), AuraInspectionDescription(battleAuraType), AuraColor(battleAuraType)));
		}
		if (state.AbilityArmed && state.Card.HeroClass == HeroClass.Paladin)
		{
			list.Add(new InspectionStatusDetail("PROTEZIONE PRONTA", "Protegge il bersaglio scelto: devia o si difende con vantaggio.", new Color(0.35f, 0.75f, 1f)));
		}
		if (state.AbilityArmed && state.Card.HeroClass == HeroClass.Warrior)
		{
			list.Add(new InspectionStatusDetail("ABILITA PRONTA", "Il prossimo attacco del Guerriero tira il dado Vigore e un dado di uno step inferiore, poi li somma.", new Color(1f, 0.72f, 0.25f)));
		}
		if (state.IsSpirit)
		{
			list.Add(new InspectionStatusDetail("SPIRITO", "Resta in campo per un ultimo turno grazie al Necromante.", new Color(0.65f, 0.75f, 1f)));
		}
		if (IsWaitingAfterRevive(state))
		{
			list.Add(new InspectionStatusDetail("RIALZATA", "Il Necromante l'ha rialzata: agira dal prossimo round con la sua iniziativa originale.", new Color(0.45f, 1f, 0.82f)));
		}
		if (state.InhibitedTurns > 0)
		{
			list.Add(new InspectionStatusDetail("INIBITO", $"Per {state.InhibitedTurns} turno/i questa carta e ostacolata dall'Assassino.", new Color(0.6f, 0.5f, 1f)));
		}
		if (state.PendingVigorStepPenalty > 0)
		{
			list.Add(new InspectionStatusDetail($"DADO -{state.PendingVigorStepPenalty}", "Il dado Vigore scende di tante taglie quanti sono i marchi, fino a D2, nel prossimo confronto.", new Color(0.55f, 0.8f, 1f)));
		}
		if (state.CampaignCard?.HasRubySeal == true)
		{
			list.Add(new InspectionStatusDetail(
				"SIGILLO OSCURO +2",
				"Bonus permanente del Sigillo Oscuro. Questa pedina non puo ricevere altri sigilli ne equipaggiamenti da altre pedine.",
				new Color(0.72f, 0.35f, 0.9f)));
		}
		int equipmentBonus = EquipmentBonusOf(state);
		if (equipmentBonus > 0)
		{
			list.Add(new InspectionStatusDetail($"POTENZA AGGIUNTIVA DA EQUIPAGGIAMENTO +{equipmentBonus}", "Bonus permanente ottenuto equipaggiando una carta sacrificata.", new Color(0.7f, 1f, 0.45f)));
		}
		if (state.MightAuraCombatBonus > 0)
		{
			list.Add(new InspectionStatusDetail($"AURA FORZUTA DA MORTE +{state.MightAuraCombatBonus}", "Bonus permanente ottenuto perche una pedina e morta mentre questa carta e sotto aura Forzuta.", new Color(1f, 0.16f, 0.12f)));
		}
		if (state.PermanentCombatBonus < 0)
		{
			list.Add(new InspectionStatusDetail($"FORZA {state.PermanentCombatBonus}", "Malus permanente alla forza in questa battaglia.", new Color(1f, 0.42f, 0.42f)));
		}
		if (state.PendingAttackBonus > 0)
		{
			list.Add(new InspectionStatusDetail(PendingAttackBonusLabel(state), PendingAttackBonusDescription(state), new Color(1f, 0.75f, 0.25f)));
		}
		int num = HunterMarkBonusForTarget(state);
		if (num > 0)
		{
			list.Add(new InspectionStatusDetail(
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.HunterMarkStatus, "BERSAGLIO MARCATO +{0}", "MARKED TARGET +{0}", "MARKIERTES ZIEL +{0}", "OBJETIVO MARCADO +{0}", "CIBLE MARQUÉE +{0}", num),
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.HunterMarkDescription, "Chi attacca questa carta riceve il bonus indicato. Più marchi sullo stesso bersaglio non si sommano.", "Anyone attacking this card receives the indicated bonus. Multiple marks on the same target do not stack.", "Wer diese Karte angreift, erhält den angegebenen Bonus. Mehrere Markierungen auf demselben Ziel sind nicht kumulativ.", "Quien ataque esta carta recibe el bono indicado. Varias marcas sobre el mismo objetivo no se acumulan.", "Toute personne attaquant cette carte reçoit le bonus indiqué. Plusieurs marques sur la même cible ne se cumulent pas."),
				new Color(1f, 0.65f, 0.2f),
				"BERSAGLIO MARCATO"));
		}
		return list;
	}

	private static string PendingAttackBonusLabel(BattleCardState card)
	{
		return card.PendingAttackBonusKind switch
		{
			PendingAttackBonusKind.Fury => $"FURIA +{card.PendingAttackBonus}", 
			PendingAttackBonusKind.Blessing => $"BENEDIZIONE +{card.PendingAttackBonus}", 
			_ => $"BONUS +{card.PendingAttackBonus}", 
		};
	}

	private static string PendingAttackBonusDescription(BattleCardState card)
	{
		return card.PendingAttackBonusKind switch
		{
			PendingAttackBonusKind.Fury => "Accumula Furia durante le sconfitte, la scarica alla prima vittoria.", 
			PendingAttackBonusKind.Blessing => "Benedizione del Sacerdote: bonus temporaneo al prossimo attacco della carta.", 
			_ => "Bonus temporaneo al prossimo attacco della carta.", 
		};
	}

	private string AuraInspectionDescription(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => CardRulesGlossary.FamilyAuraDescription(ClassFamily.Might),
			BattleAuraType.Cunning => CardRulesGlossary.FamilyAuraDescription(ClassFamily.Cunning),
			BattleAuraType.Magic => CardRulesGlossary.FamilyAuraDescription(ClassFamily.Magic),
			BattleAuraType.Formation => CardRulesGlossary.FormationAuraDescription(),
			BattleAuraType.Warrior => CardRulesGlossary.ClassAuraDescription(HeroClass.Warrior, configuration?.ClassBalance),
			BattleAuraType.Barbarian => CardRulesGlossary.ClassAuraDescription(HeroClass.Barbarian, configuration?.ClassBalance),
			BattleAuraType.Paladin => CardRulesGlossary.ClassAuraDescription(HeroClass.Paladin, configuration?.ClassBalance),
			BattleAuraType.Rogue => CardRulesGlossary.ClassAuraDescription(HeroClass.Rogue, configuration?.ClassBalance),
			BattleAuraType.Assassin => CardRulesGlossary.ClassAuraDescription(HeroClass.Assassin, configuration?.ClassBalance),
			BattleAuraType.Hunter => CardRulesGlossary.ClassAuraDescription(HeroClass.Hunter, configuration?.ClassBalance),
			BattleAuraType.Mage => CardRulesGlossary.ClassAuraDescription(HeroClass.Mage, configuration?.ClassBalance),
			BattleAuraType.Necromancer => CardRulesGlossary.ClassAuraDescription(HeroClass.Necromancer, configuration?.ClassBalance),
			BattleAuraType.Priest => CardRulesGlossary.ClassAuraDescription(HeroClass.Priest, configuration?.ClassBalance),
			_ => "Aura attiva sulla carta.", 
		};
	}

	private string AuraInspectionLabel(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => InspectionText(GameTextKeys.Inspection.FamilyAura, "AURA FAZIONE {0}", "{0} FACTION AURA", "FAMILIENAURA: {0}", "AURA DE FAMILIA: {0}", "AURA DE FAMILLE : {0}", CardRulesGlossary.ClassFamilyName(ClassFamily.Might).ToUpperInvariant()),
			BattleAuraType.Cunning => InspectionText(GameTextKeys.Inspection.FamilyAura, "AURA FAZIONE {0}", "{0} FACTION AURA", "FAMILIENAURA: {0}", "AURA DE FAMILIA: {0}", "AURA DE FAMILLE : {0}", CardRulesGlossary.ClassFamilyName(ClassFamily.Cunning).ToUpperInvariant()),
			BattleAuraType.Magic => InspectionText(GameTextKeys.Inspection.FamilyAura, "AURA FAZIONE {0}", "{0} FACTION AURA", "FAMILIENAURA: {0}", "AURA DE FAMILIA: {0}", "AURA DE FAMILLE : {0}", CardRulesGlossary.ClassFamilyName(ClassFamily.Magic).ToUpperInvariant()),
			BattleAuraType.Formation => InspectionText(GameTextKeys.Inspection.FamilyAura, "AURA FORMAZIONE", "FORMATION AURA", "FORMATIONSAURA", "AURA DE FORMACIÓN", "AURA DE FORMATION"),
			BattleAuraType.Warrior or BattleAuraType.Barbarian or BattleAuraType.Paladin or BattleAuraType.Rogue or BattleAuraType.Assassin or BattleAuraType.Hunter or BattleAuraType.Mage or BattleAuraType.Necromancer or BattleAuraType.Priest => InspectionText(GameTextKeys.Inspection.ClassAura, "AURA CLASSE {0}", "{0} CLASS AURA", "KLASSENAURA: {0}", "AURA DE CLASE: {0}", "AURA DE CLASSE : {0}", AuraClassName(aura).ToUpperInvariant()),
			_ => "AURA",
		};
	}

	private static string AuraIconStatus(BattleAuraType aura)
	{
		return aura == BattleAuraType.None ? null : "AURA " + aura.ToString().ToUpperInvariant();
	}

	private static string InspectionText(string key, string italian, string english, string german, string spanish, string french, params object[] arguments)
	{
		return GameText.GetLocalizedFallback(key, italian, english, german, spanish, french, arguments);
	}

	private static string AuraClassName(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Warrior => CardRulesGlossary.HeroClassName(HeroClass.Warrior),
			BattleAuraType.Barbarian => CardRulesGlossary.HeroClassName(HeroClass.Barbarian),
			BattleAuraType.Paladin => CardRulesGlossary.HeroClassName(HeroClass.Paladin),
			BattleAuraType.Rogue => CardRulesGlossary.HeroClassName(HeroClass.Rogue),
			BattleAuraType.Assassin => CardRulesGlossary.HeroClassName(HeroClass.Assassin),
			BattleAuraType.Hunter => CardRulesGlossary.HeroClassName(HeroClass.Hunter),
			BattleAuraType.Mage => CardRulesGlossary.HeroClassName(HeroClass.Mage),
			BattleAuraType.Necromancer => CardRulesGlossary.HeroClassName(HeroClass.Necromancer),
			BattleAuraType.Priest => CardRulesGlossary.HeroClassName(HeroClass.Priest),
			_ => string.Empty
		};
	}

	private void CreateInspectionStatusRow(InspectionStatusDetail status)
	{
		string label = CompactInspectionText(status.Label);
		string description = CompactInspectionText(status.Description);
		if (!((Object)(object)cardInspectionStatusRoot == (Object)null)
			&& !string.IsNullOrWhiteSpace(label)
			&& !string.IsNullOrWhiteSpace(description))
		{
			GameObject val = new GameObject("Inspection Status " + label, new Type[2]
			{
				typeof(RectTransform),
				typeof(HorizontalLayoutGroup)
			});
			val.transform.SetParent((Transform)(object)cardInspectionStatusRoot, false);
			HorizontalLayoutGroup component2 = val.GetComponent<HorizontalLayoutGroup>();
			component2.spacing = 10f;
			component2.childAlignment = (TextAnchor)0;
			component2.childControlWidth = true;
			component2.childControlHeight = true;
			component2.childForceExpandWidth = true;
			component2.childForceExpandHeight = false;
			Image image = CreateImage("Icon", val.transform, Color.white);
			bool supremeMalusRow = label.StartsWith("MALUS SUPREMA", StringComparison.OrdinalIgnoreCase);
			image.sprite = supremeMalusRow
				? GetSupremeButtonSprite()
				: PrototypeCardView.GetStatusIconSprite(status.IconStatus ?? label);
			image.color = (((Object)(object)image.sprite != (Object)null) ?Color.white : status.Color);
			image.preserveAspect = true;
			image.raycastTarget = false;
			LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
			bool abilityRow = label.StartsWith("ABILITA", StringComparison.OrdinalIgnoreCase);
			bool supremeRow = label.StartsWith("SUPREMA", StringComparison.OrdinalIgnoreCase);
			bool attachmentRow = label.StartsWith("EQUIPAGGIA", StringComparison.OrdinalIgnoreCase);
			float iconSize = (abilityRow || supremeRow || supremeMalusRow || attachmentRow) ?54f :44f;
			layoutElement.minWidth = iconSize;
			layoutElement.preferredWidth = iconSize;
			layoutElement.minHeight = iconSize;
			layoutElement.preferredHeight = iconSize;
			Text text = CreateText("Description", val.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, CardInspectionFontSize, (FontStyle)1, (TextAnchor)0);
			text.color = new Color(0.16f, 0.085f, 0.025f);
			text.horizontalOverflow = (HorizontalWrapMode)0;
			text.verticalOverflow = (VerticalWrapMode)1;
			ConfigureCardInspectionText(text);
			text.text = label + ":\n" + description;
			LayoutElement layoutElement2 = ((Component)text).gameObject.AddComponent<LayoutElement>();
			layoutElement2.minWidth = 0f;
			layoutElement2.preferredWidth = 620f;
			layoutElement2.flexibleWidth = 1f;
			cardInspectionStatusRows.Add(val);
		}
	}

	private static string CompactInspectionText(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string[] lines = value.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
		return string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
	}

	private void CreateInspectionStatusHeader()
	{
		if ((Object)(object)cardInspectionStatusRoot == (Object)null)
		{
			return;
		}
		Text text = CreateText("Inspection Status Header", (Transform)(object)cardInspectionStatusRoot, AccardND.Battlefield.MmoUiTheme.BodyFont, CardInspectionFontSize, (FontStyle)1, (TextAnchor)0);
		text.color = new Color(0.16f, 0.085f, 0.025f);
		text.horizontalOverflow = (HorizontalWrapMode)0;
		text.verticalOverflow = (VerticalWrapMode)1;
		ConfigureCardInspectionText(text);
		text.text = GameText.GetOrFallbackSilent(GameTextKeys.Inspection.ActiveStatuses, "Status attivi:");
		cardInspectionStatusRows.Add(((Component)text).gameObject);
	}

	private void RefreshCardInspectionPreferredHeights()
	{
		if ((Object)(object)cardInspectionContentRoot == (Object)null)
		{
			return;
		}

		// Prima assegniamo le larghezze effettive, poi misuriamo ogni testo.
		// In questo modo nessun contenitore eredita l'altezza libera del viewport.
		LayoutElement statusRootLayout = ((Component)cardInspectionStatusRoot).GetComponent<LayoutElement>();
		if ((Object)(object)statusRootLayout == (Object)null)
		{
			statusRootLayout = ((Component)cardInspectionStatusRoot).gameObject.AddComponent<LayoutElement>();
		}
		bool hasVisibleStatusRows = cardInspectionStatusRows.Any(
			row => (Object)(object)row != (Object)null
				&& row.activeSelf
				&& row.transform.parent == (Transform)(object)cardInspectionStatusRoot);
		statusRootLayout.ignoreLayout = !hasVisibleStatusRows;
		if (hasVisibleStatusRows)
		{
			// Altezza provvisoria minima: il layout padre assegna subito la larghezza
			// corretta, necessaria per misurare il wrapping dei testi.
			statusRootLayout.minHeight = 1f;
			statusRootLayout.preferredHeight = 1f;
			statusRootLayout.flexibleHeight = 0f;
		}
		LayoutRebuilder.ForceRebuildLayoutImmediate(cardInspectionContentRoot);
		LayoutRebuilder.ForceRebuildLayoutImmediate(cardInspectionStatusRoot);
		Canvas.ForceUpdateCanvases();
		SetCardInspectionElementHeight(
			((Component)cardInspectionSummaryText).gameObject,
			cardInspectionSummaryText.preferredHeight);

		float statusHeight = 0f;
		int visibleStatusRows = 0;
		foreach (GameObject row in cardInspectionStatusRows)
		{
			if ((Object)(object)row == (Object)null || !row.activeSelf)
			{
				continue;
			}

			Text directText = row.GetComponent<Text>();
			if ((Object)(object)directText != (Object)null)
			{
				statusHeight += SetCardInspectionElementHeight(row, directText.preferredHeight);
				visibleStatusRows++;
				continue;
			}

			Text description = row.transform.Find("Description")?.GetComponent<Text>();
			LayoutElement iconLayout = row.transform.Find("Icon")?.GetComponent<LayoutElement>();
			float textHeight = (Object)(object)description != (Object)null ?description.preferredHeight : 0f;
			float iconHeight = (Object)(object)iconLayout != (Object)null ?iconLayout.preferredHeight : 0f;
			statusHeight += SetCardInspectionElementHeight(row, Mathf.Max(textHeight, iconHeight));
			visibleStatusRows++;
		}

		if (visibleStatusRows == 0)
		{
			statusRootLayout.ignoreLayout = true;
		}
		else
		{
			VerticalLayoutGroup statusLayout = ((Component)cardInspectionStatusRoot).GetComponent<VerticalLayoutGroup>();
			if ((Object)(object)statusLayout != (Object)null && visibleStatusRows > 1)
			{
				statusHeight += statusLayout.spacing * (visibleStatusRows - 1);
			}
			statusRootLayout.ignoreLayout = false;
			SetCardInspectionElementHeight(((Component)cardInspectionStatusRoot).gameObject, statusHeight);
		}

		LayoutRebuilder.ForceRebuildLayoutImmediate(cardInspectionStatusRoot);
		LayoutRebuilder.ForceRebuildLayoutImmediate(cardInspectionContentRoot);
		Canvas.ForceUpdateCanvases();
		if ((Object)(object)cardInspectionContentScroll != (Object)null)
		{
			cardInspectionContentScroll.verticalNormalizedPosition = 1f;
		}
	}

	private static float SetCardInspectionElementHeight(GameObject target, float preferredHeight)
	{
		LayoutElement layout = target.GetComponent<LayoutElement>();
		if ((Object)(object)layout == (Object)null)
		{
			layout = target.AddComponent<LayoutElement>();
		}

		float height = Mathf.Max(1f, Mathf.Ceil(preferredHeight));
		layout.minHeight = height;
		layout.preferredHeight = height;
		layout.flexibleHeight = 0f;
		return height;
	}

	private void ClearInspectionStatusRows()
	{
		for (int num = cardInspectionStatusRows.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)cardInspectionStatusRows[num] != (Object)null)
			{
				cardInspectionStatusRows[num].SetActive(false);
				Object.Destroy((Object)(object)cardInspectionStatusRows[num]);
			}
		}
		cardInspectionStatusRows.Clear();
		if ((Object)(object)cardInspectionStatusRoot != (Object)null)
		{
			LayoutElement statusRootLayout = ((Component)cardInspectionStatusRoot).GetComponent<LayoutElement>();
			if ((Object)(object)statusRootLayout != (Object)null)
			{
				statusRootLayout.ignoreLayout = true;
			}
		}
	}

	private void CloseCardInspection()
	{
		CloseCardInspection(playSfx: true);
	}

	private void CloseCardInspection(bool playSfx)
	{
		// Stessa pulizia silenziosa dell'apertura: chiudere l'ispezione riporta il
		// giocatore sulla plancia e deve trovarla sempre reattiva.
		ReleaseStuckSwipeGestures("chiusura inspection");

		bool wasOpen = (Object)(object)cardInspectionPanel != (Object)null && cardInspectionPanel.activeSelf;
		bool wasPvpLoadoutInspection = inspectedPvpLoadoutActive;
		inspectedInitialDraftOfferIndex = -1;
		inspectedCampaignConsumableActive = false;
		inspectedPvpLoadoutAddAction = null;
		inspectedPvpLoadoutActive = false;
		HideCardInspectionDraftConfirm();
		if ((Object)(object)inspectedCardView != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)inspectedCardView).gameObject);
			inspectedCardView = null;
		}
		ClearInspectionStatusRows();
		if ((Object)(object)cardInspectionPanel != (Object)null)
		{
			cardInspectionPanel.SetActive(false);
			if (wasPvpLoadoutInspection)
			{
				Canvas inspectionCanvas = cardInspectionPanel.GetComponent<Canvas>();
				if ((Object)(object)inspectionCanvas != (Object)null)
					inspectionCanvas.sortingOrder = 700;
			}
		}
		if (wasOpen && playSfx)
		{
			PlayCardInspectionOpenSfx();
		}
		ResumeGameAfterCardInspection();
		if (pvpPresentationActive)
		{
			RenderPvpMatch();
		}
		else if (playerCards.Count > 0 || cpuCards.Count > 0)
		{
			UpdateInteractions();
		}
	}

	private void HideCardInspectionDraftConfirm()
	{
		if ((Object)(object)cardInspectionDraftConfirmButton != (Object)null)
		{
			((Component)cardInspectionDraftConfirmButton).gameObject.SetActive(false);
		}
	}

	private void PauseGameForCardInspection()
	{
		if (cardInspectionPausedGame)
		{
			return;
		}

		cardInspectionPreviousTimeScale = Time.timeScale;
		Time.timeScale = 0f;
		cardInspectionPausedGame = true;
	}

	private void ResumeGameAfterCardInspection()
	{
		if (!cardInspectionPausedGame)
		{
			return;
		}

		Time.timeScale = cardInspectionPreviousTimeScale;
		cardInspectionPausedGame = false;
	}

	private IEnumerator WaitForCardInspectionPause(float seconds)
	{
		float elapsed = 0f;
		while (elapsed < seconds)
		{
			if (!cardInspectionPausedGame && !implementationArchivePausedGame)
			{
				elapsed += Time.unscaledDeltaTime;
			}
			yield return null;
		}
	}
}
}
