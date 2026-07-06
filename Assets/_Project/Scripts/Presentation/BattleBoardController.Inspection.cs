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
			Stretch(inspectedCardView.RectTransform);
			AspectRatioFitter aspectRatioFitter = ((Component)inspectedCardView).gameObject.AddComponent<AspectRatioFitter>();
			aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			aspectRatioFitter.aspectRatio = 0.6708861f;
			if (state != null)
			{
				inspectedCardView.SetStrengthValue(DisplayStrength(state));
			}
			UpdateCardInspectionSummary(definition, state);
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
		string text = ((state != null && num != definition.Strength) ?$"Forza: {definition.Strength} -> {num}" : $"Forza: {definition.Strength}");
		string text2 = (definition.HasHeroClass ?CardRulesGlossary.HeroClassName(definition.HeroClass) : "Nessuna");
		string text3 = "Famiglia: Nessuna";
		string auraText = "Aura di famiglia: Nessuna\nAura di classe: Nessuna";
		if (definition.HasHeroClass)
		{
			ClassFamily classFamily = HeroClassFamily.Of(definition.HeroClass);
			text3 = "Famiglia: " + CardRulesGlossary.ClassFamilyName(classFamily) + "\nVantaggio contro: " + CardRulesGlossary.ClassFamilyName(StrongAgainst(classFamily)) + "\nSvantaggio contro: " + CardRulesGlossary.ClassFamilyName(WeakAgainst(classFamily));
			auraText = "Aura di famiglia: " + CardRulesGlossary.ClassFamilyName(classFamily) + " - " + FamilyAuraSummary(classFamily) + "\nAura di classe: " + CardRulesGlossary.HeroClassName(definition.HeroClass) + " - " + ClassAuraSummary(definition.HeroClass);
		}
		List<InspectionStatusDetail> statusDetails = BuildInspectionStatusDetails(state);
		string equipText = ShouldShowEquipSummary(definition, state)
			?$"\n\nEQUIPAGGIA: questa carta puo essere sacrificata per potenziare un alleato di +{AttachmentBonusForInspection(definition, state)} permanentemente."
			:string.Empty;
		string rulesText = definition.HasHeroClass && !string.IsNullOrWhiteSpace(definition.RulesText)
			?"\n\nRegole:\n" + definition.RulesText
			:string.Empty;
		cardInspectionSummaryText.text = definition.DisplayName + "\n" + text + "\nClasse: " + text2 + "\n" + text3 + "\n" + auraText + "\n\n" + CardAbilitySummary(definition) + equipText + rulesText + (statusDetails.Count > 0 ? "\n\nStatus attivi:" : string.Empty);
		foreach (InspectionStatusDetail item in statusDetails)
		{
			CreateInspectionStatusRow(item);
		}
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
			return CanUseAttachment(state);
		}
		return definition != null && definition.CanEnterCombat && definition.Strength >= 2 && definition.Strength < 5;
	}

	private static int AttachmentBonusForInspection(CardDefinition definition, BattleCardState state)
	{
		if (state != null)
		{
			return AttachmentBonus(state);
		}
		return definition != null ?5 - definition.Strength : 0;
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
			list.Add(new InspectionStatusDetail($"FORZA +{state.PermanentCombatBonus}", "Bonus permanente alla Forza in questa battaglia.", new Color(0.7f, 1f, 0.45f)));
		}
		if (state.PermanentCombatBonus < 0)
		{
			list.Add(new InspectionStatusDetail(state.PermanentCombatBonus.ToString(), "Malus permanente alla forza in questa battaglia.", new Color(1f, 0.42f, 0.42f)));
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
			BattleAuraType.Might => "Se un eroe Forza attacca e non elimina il bersaglio, ottiene +1 permanente.", 
			BattleAuraType.Cunning => "Gli eroi Astuzia hanno vantaggio contro nemici marcati o inibiti.", 
			BattleAuraType.Magic => "Gli eroi Magia difendono con il dado Vigore aumentato di una taglia.", 
			BattleAuraType.Formation => "Annulla lo svantaggio in attacco.", 
			BattleAuraType.Warrior => "I Guerrieri con abilita pronta attaccano con +1.", 
			BattleAuraType.Barbarian => "Furia del Barbaro vale +1 extra.", 
			BattleAuraType.Paladin => "La protezione del Paladino diventa piu efficace.", 
			BattleAuraType.Rogue => "I Ladri possono ritirare anche i 2 in attacco.", 
			BattleAuraType.Assassin => "Gli Assassini controllano meglio i bersagli inibiti.", 
			BattleAuraType.Hunter => "I Bersagli marcati dal Cacciatore valgono +4. Il bonus non si somma.", 
			BattleAuraType.Mage => "Il Mago riduce di una taglia il dado Vigore nemico.", 
			BattleAuraType.Necromancer => "I caduti restano Spiriti fino al loro turno, prima di essere eliminati.", 
			BattleAuraType.Priest => "Le Benedizioni del Sacerdote danno un bonus maggiore.", 
			_ => "Aura attiva sulla carta.", 
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
			component.minHeight = 74f;
			component.preferredHeight = 88f;
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
			layoutElement.minWidth = 50f;
			layoutElement.preferredWidth = 50f;
			layoutElement.minHeight = 50f;
			layoutElement.preferredHeight = 50f;
			Text text = CreateText("Description", val.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 18, (FontStyle)1, (TextAnchor)0);
			text.color = new Color(0.16f, 0.085f, 0.025f);
			text.horizontalOverflow = (HorizontalWrapMode)0;
			text.verticalOverflow = (VerticalWrapMode)0;
			text.resizeTextForBestFit = true;
			text.resizeTextMinSize = 13;
			text.resizeTextMaxSize = 18;
			text.text = status.Label + ": " + status.Description;
			LayoutElement layoutElement2 = ((Component)text).gameObject.AddComponent<LayoutElement>();
			layoutElement2.minWidth = 0f;
			layoutElement2.preferredWidth = 620f;
			layoutElement2.flexibleWidth = 1f;
			cardInspectionStatusRows.Add(val);
		}
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
		bool wasOpen = (Object)(object)cardInspectionPanel != (Object)null && cardInspectionPanel.activeSelf;
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
		if (wasOpen)
		{
			PlayCardInspectionCloseSfx();
		}
		if (pvpPresentationActive)
		{
			RenderPvpMatch();
		}
		else if (playerCards.Count > 0 || cpuCards.Count > 0)
		{
			UpdateInteractions();
		}
	}
}
}
