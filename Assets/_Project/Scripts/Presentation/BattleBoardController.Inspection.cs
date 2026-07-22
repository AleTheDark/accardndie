using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
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
		if (!((Object)(object)definition == (Object)null) && !((Object)(object)cardInspectionPanel == (Object)null) && !((Object)(object)cardInspectionSlot == (Object)null))
		{
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
			if (state != null)
			{
				inspectedCardView.SetStrengthValue(DisplayStrength(state));
			}
			UpdateCardInspectionSummary(definition, state);
			HideCardInspectionDraftConfirm();
			PauseGameForCardInspection();
			cardInspectionPanel.SetActive(true);
			PlayCardInspectionOpenSfx();
			if ((Object)(object)cardInspectionCloseButton != (Object)null)
			{
				((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
			}
		}
	}

	private void UpdateCardInspectionSummary(CardDefinition definition, BattleCardState state)
	{
		if ((Object)(object)cardInspectionSummaryText == (Object)null)
		{
			return;
		}
		int num = ((state != null) ?DisplayStrength(state) : definition.Strength);
		string strengthText = ((state != null && num != definition.Strength) ?$"Potenza: {definition.Strength} -> {num}" : $"Potenza: {definition.Strength}");
		string familyText = "Famiglia: Nessuna";
		string classText = "Classe: Nessuna";
		string advantageText = "Vantaggio contro Nessuno";
		string disadvantageText = "Svantaggio contro Nessuno";
		string familyAuraLabel = "AURA FAMIGLIA NESSUNA";
		string familyAuraDescription = "Nessuna aura di famiglia.";
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
			familyText = "Famiglia: " + CardRulesGlossary.ClassFamilyName(classFamily);
			classText = "Classe: " + CardRulesGlossary.HeroClassName(definition.HeroClass);
			advantageText = "Vantaggio contro " + CardRulesGlossary.ClassFamilyName(StrongAgainst(classFamily));
			ClassFamily weakAgainst = IsBragusInspectionCard(definition) ? ClassFamily.Cunning : WeakAgainst(classFamily);
			disadvantageText = "Svantaggio contro " + CardRulesGlossary.ClassFamilyName(weakAgainst);
			familyAuraLabel = AuraInspectionLabel(familyAura);
			familyAuraDescription = FamilyAuraSummary(classFamily);
			classAuraLabel = AuraInspectionLabel(classAura);
			classAuraDescription = ClassAuraSummary(definition.HeroClass);
		}
		List<InspectionStatusDetail> ruleDetails = new List<InspectionStatusDetail>();
		List<InspectionStatusDetail> statusDetails = BuildInspectionStatusDetails(state);
		bool fieldInspection = state != null;
		bool showPrintedRules = !string.IsNullOrWhiteSpace(definition.RulesText)
			&& (!fieldInspection || isBossOrMiniboss);
		string rulesText = showPrintedRules
			?"\n\nRegole:\n" + definition.RulesText
			:string.Empty;
		cardInspectionSummaryText.resizeTextMinSize = isBossOrMiniboss ? 15 : 20;
		cardInspectionSummaryText.resizeTextMaxSize = isBossOrMiniboss ? 30 : 34;
		if (!fieldInspection && !isBossOrMiniboss)
		{
			ruleDetails.Add(new InspectionStatusDetail(familyAuraLabel, familyAuraDescription, AuraColor(familyAura)));
			ruleDetails.Add(new InspectionStatusDetail(classAuraLabel, classAuraDescription, AuraColor(classAura)));
			ruleDetails.Add(new InspectionStatusDetail(CardAbilityInspectionLabel(definition), CardAbilityInspectionDescription(definition), new Color(1f, 0.78f, 0.24f)));
		}
		else if (fieldInspection && ShouldShowFieldAbilitySummary(definition, state))
		{
			ruleDetails.Add(new InspectionStatusDetail(CardAbilityInspectionLabel(definition), CardAbilityInspectionDescription(definition), new Color(1f, 0.78f, 0.24f)));
		}
		if (ShouldShowEquipSummary(definition, state))
		{
			ruleDetails.Add(new InspectionStatusDetail(
				"EQUIPAGGIA",
				$"Questa carta puo essere sacrificata per potenziare un alleato di +{AttachmentBonusForInspection(definition, state)} permanentemente. Se l'alleato equipaggiato muore, muore anche questa carta.",
				new Color(1f, 0.62f, 0.2f)));
		}
		cardInspectionSummaryText.text = strengthText + "\n" + familyText + "\n" + classText
			+ "\n\n" + advantageText + "\n" + disadvantageText
			+ rulesText;
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
	}

	private static bool IsBossOrMinibossInspectionCard(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& definition.Category == CardCategory.Boss;
	}

	private static bool IsBragusInspectionCard(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, BragusBossCardId, StringComparison.OrdinalIgnoreCase);
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

	private static string CardAbilityInspectionLabel(CardDefinition definition)
	{
		if ((Object)(object)definition == (Object)null || !definition.HasHeroClass)
		{
			return "ABILITA'";
		}
		string prefix = IsPassiveClassAbility(definition.HeroClass) ? "ABILITA' PASSIVA " : "ABILITA' ";
		return prefix + CardRulesGlossary.HeroClassName(definition.HeroClass).ToUpperInvariant();
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
		return description;
	}

	private static bool IsPassiveClassAbility(HeroClass heroClass)
	{
		return heroClass == HeroClass.Barbarian
			|| heroClass == HeroClass.Rogue;
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

	private bool ShouldShowFieldAbilitySummary(CardDefinition definition, BattleCardState state)
	{
		if ((Object)(object)definition == (Object)null || state == null || IsBossOrMinibossInspectionCard(definition))
		{
			return false;
		}
		return definition.HasHeroClass && (state.Eliminated || IsPassiveClassAbility(definition.HeroClass) || IsClassAbilityActionAvailable(state));
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
			list.Add(new InspectionStatusDetail("ABILITA PRONTA", "Il prossimo attacco del Guerriero tira due dadi Vigore e li somma.", new Color(1f, 0.72f, 0.25f)));
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
			list.Add(new InspectionStatusDetail($"DADO -{state.PendingVigorStepPenalty}", "Il dado Vigore scende di taglia nel prossimo confronto.", new Color(0.55f, 0.8f, 1f)));
		}
		if (state.PermanentCombatBonus > 0)
		{
			list.Add(new InspectionStatusDetail($"POTENZA AGGIUNTIVA DA EQUIPAGGIAMENTO +{state.PermanentCombatBonus}", "Bonus permanente ottenuto equipaggiando una carta sacrificata.", new Color(0.7f, 1f, 0.45f)));
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
			list.Add(new InspectionStatusDetail($"BERSAGLIO MARCATO +{num}", "Chi attacca questa carta riceve il bonus indicato. Piu marchi sullo stesso bersaglio non si sommano.", new Color(1f, 0.65f, 0.2f)));
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
			PendingAttackBonusKind.Fury => "Furia del Barbaro: bonus temporaneo in attacco e difesa.", 
			PendingAttackBonusKind.Blessing => "Benedizione del Sacerdote: bonus temporaneo al prossimo attacco della carta.", 
			_ => "Bonus temporaneo al prossimo attacco della carta.", 
		};
	}

	private static string AuraInspectionDescription(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => "Quando muore una pedina qualsiasi, ogni carta con aura Forzuta attiva acquisisce +1 permanente.",
			BattleAuraType.Cunning => "Le tue carte Astuta attaccano sempre con vantaggio i nemici che hanno bonus o malus.",
			BattleAuraType.Magic => "Le tue carte Magica si difendono con un dado piu forte, esempio: Se ho un D6 mi difendo con un D8.",
			BattleAuraType.Formation => "Annulla lo svantaggio in attacco.", 
			BattleAuraType.Warrior => "I Guerrieri con abilita pronta attaccano con +1.", 
			BattleAuraType.Barbarian => "Furia del Barbaro vale +1 extra.", 
			BattleAuraType.Paladin => "Quando un Paladino sopravvive ad una difesa, contrattacca con +1.",
			BattleAuraType.Rogue => "I Ladri ritirano una volta per dado se esce 1 o 2, in attacco e in difesa.",
			BattleAuraType.Assassin => "Quando un Assassino inibisce un nemico, quel nemico subisce anche -1 permanente.",
			BattleAuraType.Hunter => "I Bersagli marcati dal Cacciatore valgono +4. Il bonus non si somma.", 
			BattleAuraType.Mage => "Il Mago riduce di una taglia il dado Vigore nemico.", 
			BattleAuraType.Necromancer => "La prima volta che un tuo alleato viene ucciso, resta in campo per un ultimo turno.",
			BattleAuraType.Priest => "Le Benedizioni del Sacerdote danno un bonus maggiore.", 
			_ => "Aura attiva sulla carta.", 
		};
	}

	private static string AuraInspectionLabel(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => "AURA FAMIGLIA FORTUZA",
			BattleAuraType.Cunning => "AURA FAMIGLIA ASTUTA",
			BattleAuraType.Magic => "AURA FAMIGLIA MAGICA",
			BattleAuraType.Formation => "AURA FORMAZIONE",
			BattleAuraType.Warrior => "AURA CLASSE GUERRIERO",
			BattleAuraType.Barbarian => "AURA CLASSE BARBARO",
			BattleAuraType.Paladin => "AURA CLASSE PALADINO",
			BattleAuraType.Rogue => "AURA CLASSE LADRO",
			BattleAuraType.Assassin => "AURA CLASSE ASSASSINO",
			BattleAuraType.Hunter => "AURA CLASSE CACCIATORE",
			BattleAuraType.Mage => "AURA CLASSE MAGO",
			BattleAuraType.Necromancer => "AURA CLASSE NECROMANTE",
			BattleAuraType.Priest => "AURA CLASSE SACERDOTE",
			_ => "AURA",
		};
	}

	private void CreateInspectionStatusRow(InspectionStatusDetail status)
	{
		if (!((Object)(object)cardInspectionStatusRoot == (Object)null))
		{
			GameObject val = new GameObject("Inspection Status " + status.Label, new Type[3]
			{
				typeof(RectTransform),
				typeof(HorizontalLayoutGroup),
				typeof(LayoutElement)
			});
			val.transform.SetParent((Transform)(object)cardInspectionStatusRoot, false);
			LayoutElement component = val.GetComponent<LayoutElement>();
			component.minHeight = 82f;
			component.preferredHeight = 96f;
			HorizontalLayoutGroup component2 = val.GetComponent<HorizontalLayoutGroup>();
			component2.spacing = 10f;
			component2.childAlignment = (TextAnchor)0;
			component2.childControlWidth = true;
			component2.childControlHeight = true;
			component2.childForceExpandWidth = true;
			component2.childForceExpandHeight = false;
			Image image = CreateImage("Icon", val.transform, Color.white);
			image.sprite = PrototypeCardView.GetStatusIconSprite(status.Label);
			image.color = (((Object)(object)image.sprite != (Object)null) ?Color.white : status.Color);
			image.preserveAspect = true;
			image.raycastTarget = false;
			LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
			bool abilityRow = status.Label.StartsWith("ABILITA", StringComparison.OrdinalIgnoreCase);
			bool attachmentRow = status.Label.StartsWith("EQUIPAGGIA", StringComparison.OrdinalIgnoreCase);
			float iconSize = (abilityRow || attachmentRow) ?54f :44f;
			layoutElement.minWidth = iconSize;
			layoutElement.preferredWidth = iconSize;
			layoutElement.minHeight = iconSize;
			layoutElement.preferredHeight = iconSize;
			Text text = CreateText("Description", val.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 30, (FontStyle)1, (TextAnchor)0);
			text.color = new Color(0.16f, 0.085f, 0.025f);
			text.horizontalOverflow = (HorizontalWrapMode)0;
			text.verticalOverflow = (VerticalWrapMode)0;
			text.resizeTextForBestFit = true;
			text.resizeTextMinSize = 20;
			text.resizeTextMaxSize = 30;
			text.text = status.Label + ":\n" + status.Description;
			LayoutElement layoutElement2 = ((Component)text).gameObject.AddComponent<LayoutElement>();
			layoutElement2.minWidth = 0f;
			layoutElement2.preferredWidth = 620f;
			layoutElement2.flexibleWidth = 1f;
			cardInspectionStatusRows.Add(val);
		}
	}

	private void CreateInspectionStatusHeader()
	{
		if ((Object)(object)cardInspectionStatusRoot == (Object)null)
		{
			return;
		}
		Text text = CreateText("Inspection Status Header", (Transform)(object)cardInspectionStatusRoot, AccardND.Battlefield.MmoUiTheme.BodyFont, 30, (FontStyle)1, (TextAnchor)0);
		text.color = new Color(0.16f, 0.085f, 0.025f);
		text.horizontalOverflow = (HorizontalWrapMode)0;
		text.verticalOverflow = (VerticalWrapMode)1;
		text.text = "Status attivi:";
		LayoutElement layoutElement = ((Component)text).gameObject.AddComponent<LayoutElement>();
		layoutElement.minHeight = 34f;
		layoutElement.preferredHeight = 40f;
		cardInspectionStatusRows.Add(((Component)text).gameObject);
	}

	private void ClearInspectionStatusRows()
	{
		for (int num = cardInspectionStatusRows.Count - 1; num >= 0; num--)
		{
			if ((Object)(object)cardInspectionStatusRows[num] != (Object)null)
			{
				Object.Destroy((Object)(object)cardInspectionStatusRows[num]);
			}
		}
		cardInspectionStatusRows.Clear();
	}

	private void CloseCardInspection()
	{
		CloseCardInspection(playSfx: true);
	}

	private void CloseCardInspection(bool playSfx)
	{
		bool wasOpen = (Object)(object)cardInspectionPanel != (Object)null && cardInspectionPanel.activeSelf;
		inspectedInitialDraftOfferIndex = -1;
		inspectedCampaignConsumableActive = false;
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
			if (!cardInspectionPausedGame)
			{
				elapsed += Time.unscaledDeltaTime;
			}
			yield return null;
		}
	}
}
}
