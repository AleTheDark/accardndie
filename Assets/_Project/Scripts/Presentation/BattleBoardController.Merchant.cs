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
	private void OpenMerchantPanel()
	{
		if (currentRoomType == RoomType.Merchant && !((Object)(object)merchantPanel == (Object)null))
		{
			selectedMerchantSaleCard = null;
			RefreshMerchantPanel();
			merchantPanel.SetActive(true);
		}
	}

	private void CloseMerchantPanel()
	{
		if ((Object)(object)merchantPanel != (Object)null)
		{
			merchantPanel.SetActive(false);
		}
	}

	private void RefreshMerchantPanel()
	{
		if (!((Object)(object)merchantPanel == (Object)null))
		{
			bool godMerchant = IsGodMerchantRoom();
			EnsureMerchantStrengthInRange(godMerchant);
			int num = MerchantCostFor(MerchantBuyMode.Random, godMerchant);
			int num2 = MerchantCostFor(MerchantBuyMode.Class, godMerchant);
			int num3 = MerchantCostFor(MerchantBuyMode.Strength, godMerchant);
			if ((Object)(object)merchantStatusText != (Object)null)
			{
				merchantStatusText.text = $"EXP DISPONIBILE {runProgress.AvailableExperience}  |  RANDOM {num}  |  CLASSE {num2}  |  VALORE {num3}  |  MAZZO {CurrentMerchantDeckCount()}/{12}  |  CIM {campaignDeck?.GraveyardCount ?? 0}";
			}
			if ((Object)(object)merchantClassImage != (Object)null)
			{
				merchantClassImage.sprite = LoadSpriteResource(DeckBuilderClassResourcePath(merchantSelectedClass));
			}
			if ((Object)(object)merchantClassText != (Object)null)
			{
				merchantClassText.text = HeroClassDisplayName(merchantSelectedClass).ToUpperInvariant();
			}
			if ((Object)(object)merchantStrengthImage != (Object)null)
			{
				merchantStrengthImage.sprite = LoadSpriteResource(DeckBuilderStrengthResourcePath(merchantSelectedStrength));
			}
			bool interactable = !IsMerchantDeckFull();
			if ((Object)(object)merchantRandomBuyButton != (Object)null)
			{
				merchantRandomBuyButton.interactable = interactable;
			}
			if ((Object)(object)merchantClassBuyButton != (Object)null)
			{
				merchantClassBuyButton.interactable = interactable;
			}
			if ((Object)(object)merchantStrengthBuyButton != (Object)null)
			{
				merchantStrengthBuyButton.interactable = interactable;
			}
			RefreshMerchantSellText();
			RefreshMerchantActionButtons();
			RefreshMerchantOwnedCards();
			RefreshInitiativeDisplay();
		}
	}

	private void RefreshMerchantSellText()
	{
		if ((Object)(object)merchantSellText == (Object)null)
		{
			return;
		}
		if (selectedMerchantSaleCard == null)
		{
			merchantSellText.text = "Seleziona una carta del mazzo per venderla o una carta del cimitero da recuperare.";
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		string displayName = CardDisplayNames.MarketName(definition);
		if (selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard)
		{
			int num = RecoveryCostFor(definition);
			merchantSellText.text = $"{displayName}\nRECUPERO: {num} EXP. Poi puoi venderla per {SellValueFor(definition)} EXP.";
		}
		else
		{
			merchantSellText.text = $"{displayName}\nVALORE VENDITA: {SellValueFor(definition)} EXP";
		}
	}

	private void RefreshMerchantActionButtons()
	{
		bool flag = selectedMerchantSaleCard != null;
		bool flag2 = flag && selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard;
		if ((Object)(object)merchantSellButton != (Object)null)
		{
			merchantSellButton.interactable = flag && !flag2;
		}
		if ((Object)(object)merchantRecoverButton != (Object)null)
		{
			merchantRecoverButton.interactable = flag2;
		}
	}

	private void RefreshMerchantOwnedCards()
	{
		DestroyPrototypeViews(merchantOwnedCardViews);
		PopulateMerchantCardSection(merchantDeckCardsRoot, merchantDeckEmptyText, GetMerchantDeckCards());
		PopulateMerchantCardSection(merchantGraveyardCardsRoot, merchantGraveyardEmptyText, GetMerchantGraveyardCards());
	}

	private void PopulateMerchantCardSection(RectTransform root, Text emptyText, List<CampaignCardInstance> cards)
	{
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		if ((Object)(object)emptyText != (Object)null)
		{
			((Component)emptyText).gameObject.SetActive(cards.Count == 0);
		}
		foreach (CampaignCardInstance card in cards)
		{
			CardDefinition definition = card.Definition;
			PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)root, definition, configuration);
			prototypeCardView.SetInteractable(interactable: true);
			prototypeCardView.SetSelected(card == selectedMerchantSaleCard);
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				SelectMerchantSaleCard(card);
			});
			LayoutElement component = ((Component)prototypeCardView).GetComponent<LayoutElement>();
			if ((Object)(object)component != (Object)null)
			{
				component.minWidth = ImplementationArchiveCardSize;
				component.preferredWidth = ImplementationArchiveCardSize;
				component.minHeight = ImplementationArchiveCardSize;
				component.preferredHeight = ImplementationArchiveCardSize;
				component.flexibleWidth = 0f;
				component.flexibleHeight = 0f;
			}
			merchantOwnedCardViews.Add(prototypeCardView);
		}
	}

	private List<CampaignCardInstance> GetMerchantDeckCards()
	{
		if (campaignDeck == null)
		{
			return new List<CampaignCardInstance>();
		}
		return (from card in campaignDeck.Cards
			where card.Zone != CampaignCardZone.Hand && card.Zone != CampaignCardZone.Battlefield && card.Zone != CampaignCardZone.Graveyard
			orderby MerchantZoneSort(card.Zone), card.Definition.Strength, card.Definition.DisplayName
			select card).ToList();
	}

	private List<CampaignCardInstance> GetMerchantGraveyardCards()
	{
		if (campaignDeck == null)
		{
			return new List<CampaignCardInstance>();
		}
		return (from card in campaignDeck.Cards
			where card.Zone == CampaignCardZone.Graveyard
			orderby card.Definition.Strength, card.Definition.DisplayName
			select card).ToList();
	}

	private static int MerchantZoneSort(CampaignCardZone zone)
	{
		return zone switch
		{
			CampaignCardZone.Deck => 0, 
			CampaignCardZone.Cooldown => 1, 
			CampaignCardZone.Graveyard => 2, 
			_ => 3, 
		};
	}

	private void SelectMerchantSaleCard(CampaignCardInstance card)
	{
		selectedMerchantSaleCard = card;
		RefreshMerchantSellText();
		RefreshMerchantActionButtons();
		RefreshMerchantOwnedCards();
	}

	private void SellSelectedMerchantCard()
	{
		if (selectedMerchantSaleCard == null)
		{
			SetMessage("MERCANTE: scegli prima una carta da vendere.");
			return;
		}
		if (campaignDeck == null || campaignDeck.Cards.Count <= configuration.DeckBuilding.FormationSize)
		{
			SetMessage("MERCANTE: tieni almeno una formazione completa nel mazzo.");
			return;
		}
		if (selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard)
		{
			SetMessage("MERCANTE: una carta nel cimitero non puo' essere venduta. Recuperala prima nel mazzo.");
			RefreshMerchantPanel();
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		int num = SellValueFor(definition);
		if (!campaignDeck.RemoveCard(selectedMerchantSaleCard))
		{
			SetMessage("MERCANTE: questa carta non puo' essere venduta adesso.");
			return;
		}
		RemoveCardDefinitionFromList(playerReserve, definition);
		RemoveCardDefinitionFromList(initialPlayerReserve, definition);
		int num2 = runProgress.AddExperience(num);
		selectedMerchantSaleCard = null;
		PlayBuyCardSfx();
		string displayName = CardDisplayNames.MarketName(definition);
		AppendLog($"VENDITA - {displayName}, +{num} EXP.");
		string text = ((num2 > 0) ?$" LEVEL UP: livello {runProgress.PlayerLevel}, D{runProgress.PlayerVigorDieSides}!" : string.Empty);
		SetMessage($"VENDUTA: {displayName}. Ottieni {num} EXP." + text);
		RefreshMerchantPanel();
	}

	private void RecoverSelectedMerchantCard()
	{
		if (selectedMerchantSaleCard == null)
		{
			SetMessage("MERCANTE: scegli prima una carta dal cimitero.");
			return;
		}
		if (selectedMerchantSaleCard.Zone != CampaignCardZone.Graveyard)
		{
			SetMessage("MERCANTE: questa carta e' gia' fuori dal cimitero.");
			RefreshMerchantPanel();
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		int num = RecoveryCostFor(definition);
		string displayName = CardDisplayNames.MarketName(definition);
		if (!runProgress.TrySpendExperience(num))
		{
			SetMessage($"MERCANTE: servono {num} EXP per recuperare {displayName}, disponibili {runProgress.AvailableExperience}.");
			RefreshMerchantPanel();
		}
		else if (!campaignDeck.RecoverFromGraveyard(selectedMerchantSaleCard))
		{
			SetMessage("MERCANTE: questa carta non puo' essere recuperata adesso.");
			RefreshMerchantPanel();
		}
		else
		{
			AppendLog($"RECUPERO MERCANTE - {displayName} torna nel mazzo, -{num} EXP.");
			SetMessage($"RECUPERATA: {displayName} torna nel mazzo per {num} EXP. Ora puoi venderla o tenerla.");
			RefreshMerchantPanel();
		}
	}

	private void BuyMerchantCard(MerchantBuyMode mode)
	{
		bool flag = IsGodMerchantRoom();
		int num = MerchantCostFor(mode, flag);
		if ((Object)(object)merchantPanel == (Object)null || !merchantPanel.activeSelf)
		{
			return;
		}
		if (IsMerchantDeckFull())
		{
			SetMessage($"MERCANTE: limite mazzo raggiunto ({12} carte). Vendi una carta prima di comprarne altre.");
			RefreshMerchantPanel();
			return;
		}
		List<CardDefinition> merchantRewardPool = GetMerchantRewardPool(flag, mode);
		if (merchantRewardPool.Count == 0)
		{
			SetMessage("MERCANTE: nessuna carta disponibile per questa offerta.");
			RefreshMerchantPanel();
			return;
		}
		if (!runProgress.TrySpendExperience(num))
		{
			SetMessage($"MERCANTE: servono {num} EXP, disponibili {runProgress.AvailableExperience}.");
			RefreshMerchantPanel();
			return;
		}
		CardDefinition cardDefinition = DrawMerchantCard(merchantRewardPool, flag, mode);
		if (!TryAddCardToPlayerCollection(cardDefinition))
		{
			runProgress.AddExperience(num);
			SetMessage("MERCANTE: questa carta e' gia' nel mazzo.");
			RefreshMerchantPanel();
			return;
		}
		string displayName = CardDisplayNames.MarketName(cardDefinition);
		AppendLog($"ACQUISTO - {displayName}, -{num} EXP.");
		PlayBuyCardSfx();
		SetMessage(string.Format("{0}: {1} entra nel mazzo per {2} EXP. ", flag ?"MERCATO GOD" : "ACQUISTO COMPLETATO", displayName, num) + $"EXP disponibile: {runProgress.AvailableExperience}.");
		RefreshMerchantPanel();
	}

	private int CurrentMerchantDeckCount()
	{
		return campaignDeck?.Cards.Count ?? 0;
	}

	private bool IsMerchantDeckFull()
	{
		return CurrentMerchantDeckCount() >= 12;
	}

	private int MerchantCostFor(MerchantBuyMode mode, bool godMerchant)
	{
		int num = (godMerchant ?configuration.Progression.GodMerchantHeroCardCost : configuration.Progression.MerchantHeroCardCost);
		return mode switch
		{
			MerchantBuyMode.Random => num, 
			MerchantBuyMode.Class => num + (godMerchant ?8 : 5), 
			MerchantBuyMode.Strength => num + Math.Max(2, merchantSelectedStrength), 
			_ => num, 
		};
	}

	private static int SellValueFor(CardDefinition definition)
	{
		if (!((Object)(object)definition != (Object)null))
		{
			return 0;
		}
		return Math.Max(3, definition.Strength * 2);
	}

	private static int RecoveryCostFor(CardDefinition definition)
	{
		if (!((Object)(object)definition != (Object)null))
		{
			return 0;
		}
		return Math.Max(3, definition.Strength);
	}

	private static void RemoveCardDefinitionFromList(List<CardDefinition> cards, CardDefinition definition)
	{
		if (cards != null && !((Object)(object)definition == (Object)null))
		{
			int num = cards.FindIndex((CardDefinition card) => (Object)(object)card != (Object)null && card.Id == definition.Id);
			if (num >= 0)
			{
				cards.RemoveAt(num);
			}
		}
	}

	private void CycleMerchantClass()
	{
		Array values = Enum.GetValues(typeof(HeroClass));
		int num = Array.IndexOf(values, merchantSelectedClass);
		merchantSelectedClass = (HeroClass)values.GetValue((num + 1) % values.Length);
		PlayArrowChangeSfx();
		RefreshMerchantPanel();
	}

	private void CycleMerchantStrength()
	{
		bool godMerchant = IsGodMerchantRoom();
		merchantSelectedStrength++;
		if (merchantSelectedStrength > MerchantMaximumStrength(godMerchant))
		{
			merchantSelectedStrength = MerchantMinimumStrength(godMerchant);
		}
		PlayArrowChangeSfx();
		RefreshMerchantPanel();
	}

	private void EnsureMerchantStrengthInRange(bool godMerchant)
	{
		merchantSelectedStrength = Mathf.Clamp(merchantSelectedStrength, MerchantMinimumStrength(godMerchant), MerchantMaximumStrength(godMerchant));
	}

	private int MerchantMinimumStrength(bool godMerchant)
	{
		if (!godMerchant)
		{
			return 2;
		}
		return configuration.Progression.GodMerchantMinimumStrength;
	}

	private int MerchantMaximumStrength(bool godMerchant)
	{
		if (!godMerchant)
		{
			return Math.Max(2, configuration.Progression.GodMerchantMinimumStrength - 1);
		}
		return 10;
	}

	private bool IsGodMerchantRoom()
	{
		if (!string.Equals(pendingScenarioId, "god_merchant", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(currentScenario?.Id, "god_merchant", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private List<CardDefinition> GetMerchantRewardPool(bool godMerchant, MerchantBuyMode mode = MerchantBuyMode.Random)
	{
		List<CardDefinition> campaignRewardPool = GetCampaignRewardPool();
		int minimumStrength = configuration.Progression.GodMerchantMinimumStrength;
		List<CardDefinition> pool = campaignRewardPool.Where((CardDefinition card) => (!godMerchant) ?(card.Strength < minimumStrength) : (card.Strength >= minimumStrength)).ToList();
		return FilterMerchantPool(pool, mode);
	}

	private List<CardDefinition> FilterMerchantPool(List<CardDefinition> pool, MerchantBuyMode mode)
	{
		return mode switch
		{
			MerchantBuyMode.Class => pool.Where((CardDefinition card) => card.HasHeroClass && card.HeroClass == merchantSelectedClass).ToList(), 
			MerchantBuyMode.Strength => pool.Where((CardDefinition card) => card.Strength == merchantSelectedStrength).ToList(), 
			_ => pool, 
		};
	}

	private CardDefinition DrawMerchantCard(List<CardDefinition> rewardPool, bool godMerchant, MerchantBuyMode mode)
	{
		if (godMerchant && mode == MerchantBuyMode.Random)
		{
			return DrawGodMerchantCard(rewardPool);
		}
		if (mode != MerchantBuyMode.Strength)
		{
			return formationDraftService.DrawCandidates(rewardPool, 1)[0];
		}
		return rewardPool[random.NextInclusive(0, rewardPool.Count - 1)];
	}

	private CardDefinition DrawGodMerchantCard(List<CardDefinition> rewardPool)
	{
		int topStrength = rewardPool.Max((CardDefinition card) => card.Strength);
		List<CardDefinition> list = rewardPool.Where((CardDefinition card) => card.Strength == topStrength).ToList();
		return list[random.NextInclusive(0, list.Count - 1)];
	}

	private string GrantGodMerchantWelcomeGift()
	{
		if (campaignDeck == null)
		{
			return string.Empty;
		}
		if (IsMerchantDeckFull())
		{
			return " Il Mercante Divino vorrebbe donarti una carta, ma il mazzo e' pieno.";
		}
		List<CardDefinition> merchantRewardPool = GetMerchantRewardPool(godMerchant: true);
		if (merchantRewardPool.Count == 0)
		{
			return " Il Mercante Divino non trova carte nuove da donarti.";
		}
		CardDefinition cardDefinition = DrawGodMerchantCard(merchantRewardPool);
		if (!TryAddCardToPlayerCollection(cardDefinition))
		{
			return " Il Mercante Divino non trova carte nuove da donarti.";
		}
		string displayName = CardDisplayNames.MarketName(cardDefinition);
		AppendLog("DONO MERCANTE DIVINO - " + displayName + " entra nel mazzo gratis.");
		return " Il Mercante Divino ti dona " + displayName + ".";
	}
}
}
