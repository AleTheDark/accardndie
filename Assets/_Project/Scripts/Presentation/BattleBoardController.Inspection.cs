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
		string text2 = (definition.HasHeroClass ?HeroClassDisplayName(definition.HeroClass) : "Nessuna");
		string text3 = "Famiglia: Nessuna";
		if (definition.HasHeroClass)
		{
			ClassFamily classFamily = HeroClassFamily.Of(definition.HeroClass);
			text3 = "Famiglia: " + ClassFamilyDisplayName(classFamily) + "\nForte contro: famiglia " + ClassFamilyDisplayName(StrongAgainst(classFamily)) + "\nDebole contro: famiglia " + ClassFamilyDisplayName(WeakAgainst(classFamily));
		}
		cardInspectionSummaryText.text = definition.DisplayName + "\n" + text + "\nClasse: " + text2 + "\n" + text3 + "\n\n" + CardAbilitySummary(definition) + "\n\nStatus attivi:";
		foreach (InspectionStatusDetail item in BuildInspectionStatusDetails(state))
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
		ClassBalanceConfiguration classBalance = configuration.ClassBalance;
		string description = definition.HeroClass switch
		{
			HeroClass.Warrior => "Quando attiva l'abilita, somma due dadi Vigore nel prossimo attacco.", 
			HeroClass.Barbarian => $"Dopo un attacco senza eliminare il bersaglio prepara Furia, +{classBalance.BarbarianRageBonus} in attacco e difesa.", 
			HeroClass.Paladin => "Protegge un alleato deviando su di se, oppure si difende con vantaggio.", 
			HeroClass.Rogue => "Puo ritirare gli 1 nel tiro Vigore quando attacca.", 
			HeroClass.Assassin => "Puo inibire un nemico, limitandone il turno.", 
			HeroClass.Hunter => $"Marca un bersaglio con Preda persistente; chi lo attacca prende +{classBalance.HunterStrongTargetBonus}, o +{classBalance.HunterStrongTargetBonus * 2} con Aura Hunter. Non cumulabile.", 
			HeroClass.Mage => "Indebolisce un nemico abbassando il suo dado Vigore per il prossimo confronto.", 
			HeroClass.Necromancer => "Rialza un alleato eliminato come Spirito per un ultimo turno.", 
			HeroClass.Priest => $"Benedice un alleato dandogli +{classBalance.PriestBlessingBonus} al prossimo attacco.", 
			_ => string.IsNullOrWhiteSpace(definition.RulesText) ?"Nessuna abilita di combattimento." : definition.RulesText, 
		};
		return "Abilit\u00E0 " + HeroClassDisplayName(definition.HeroClass) + ":\n" + description;
	}

	private static string HeroClassDisplayName(HeroClass heroClass)
	{
		return heroClass switch
		{
			HeroClass.Warrior => "Guerriero", 
			HeroClass.Barbarian => "Barbaro", 
			HeroClass.Paladin => "Paladino", 
			HeroClass.Rogue => "Rogue", 
			HeroClass.Assassin => "Assassino", 
			HeroClass.Hunter => "Hunter", 
			HeroClass.Mage => "Mago", 
			HeroClass.Necromancer => "Necromancer", 
			HeroClass.Priest => "Priest", 
			_ => heroClass.ToString(), 
		};
	}

	private static string ClassFamilyDisplayName(ClassFamily family)
	{
		return family switch
		{
			ClassFamily.Might => "Might", 
			ClassFamily.Cunning => "Cunning", 
			ClassFamily.Magic => "Magic", 
			_ => family.ToString(), 
		};
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

	private List<InspectionStatusDetail> BuildInspectionStatusDetails(BattleCardState state)
	{
		List<InspectionStatusDetail> list = new List<InspectionStatusDetail>();
		if (state == null)
		{
			list.Add(new InspectionStatusDetail("NESSUNO", "Apri una carta in battaglia per vedere buff, debuff e aura attivi.", Color.gray));
			return list;
		}
		if (state.Eliminated)
		{
			list.Add(new InspectionStatusDetail("MORTE", "La carta e eliminata: non puo agire e andra al cimitero se appartiene alla tua formazione.", new Color(0.95f, 0.12f, 0.12f)));
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
			list.Add(new InspectionStatusDetail("ABILITA PRONTA", "Il prossimo attacco del Guerriero sommera due dadi Vigore.", new Color(1f, 0.72f, 0.25f)));
		}
		if (state.IsSpirit)
		{
			list.Add(new InspectionStatusDetail("SPIRITO", "Resta in campo per un ultimo turno grazie al Necromancer.", new Color(0.65f, 0.75f, 1f)));
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
			list.Add(new InspectionStatusDetail($"ATTACH +{state.PermanentCombatBonus}", "Forza permanente in battaglia.", new Color(0.7f, 1f, 0.45f)));
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
			list.Add(new InspectionStatusDetail($"MARCATO +{num}", "La carta e' una preda marcata: gli attacchi contro di lei ricevono bonus. Piu marchi non si sommano.", new Color(1f, 0.65f, 0.2f)));
		}
		if (list.Count == 0)
		{
			list.Add(new InspectionStatusDetail("NESSUNO", "Nessun buff, debuff o aura attivo in questo momento.", Color.gray));
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
			BattleAuraType.Might => "Se un eroe Might attacca e non elimina il bersaglio, ottiene +1 permanente.", 
			BattleAuraType.Cunning => "Gli eroi Cunning hanno vantaggio contro nemici marcati o inibiti.", 
			BattleAuraType.Magic => "Gli eroi Magic difendono con il dado Vigore aumentato di 1 step.", 
			BattleAuraType.Formation => "Annulla lo svantaggio in attacco.", 
			BattleAuraType.Warrior => "I Warrior con abilita pronta attaccano con +1.", 
			BattleAuraType.Barbarian => "Furia del Barbaro vale +1 extra.", 
			BattleAuraType.Paladin => "La protezione del Paladino diventa piu efficace.", 
			BattleAuraType.Rogue => "I Rogue possono ritirare anche i 2 in attacco.", 
			BattleAuraType.Assassin => "Gli Assassin controllano meglio i bersagli inibiti.", 
			BattleAuraType.Hunter => "I marchi Hunter valgono +4 persistente e non cumulabile.", 
			BattleAuraType.Mage => "Il Mage abbassa di uno step il dado Vigore nemico.", 
			BattleAuraType.Necromancer => "I caduti restano Spiriti fino al loro turno, prima di essere eliminati.", 
			BattleAuraType.Priest => "Le benedizioni del Priest danno un bonus maggiore.", 
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
		if (playerCards.Count > 0 || cpuCards.Count > 0)
		{
			UpdateInteractions();
		}
	}
}
}
