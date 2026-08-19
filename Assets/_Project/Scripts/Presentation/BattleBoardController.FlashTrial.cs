using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const int FlashTrialConsumableRewardCardThreshold = 12;

	private readonly struct FlashTrialCampaignReward
	{
		public FlashTrialCampaignReward(FlashTrialSlotOutcome outcome, CardDefinition card,
			int bonusExperience, int bonusGold, string description)
		{
			Outcome = outcome;
			Card = card;
			BonusExperience = bonusExperience;
			BonusGold = bonusGold;
			Description = description;
		}

		public FlashTrialSlotOutcome Outcome { get; }
		public CardDefinition Card { get; }
		public int BonusExperience { get; }
		public int BonusGold { get; }
		public string Description { get; }
	}

	/// <summary>
	/// Risolve la slot contro il mazzo vero. GetCampaignRewardPool esclude gia' ogni carta
	/// equivalente a una posseduta, indipendentemente dalla zona in cui si trova.
	/// </summary>
	private FlashTrialCampaignReward RollFlashTrialCampaignReward(
		FlashTrialResult performance, int completedLevels)
	{
		var machine = new FlashTrialSlotMachine(random.NextInclusive(1, int.MaxValue));
		List<CardDefinition> available = GetCampaignRewardPool();
		FlashTrialSlotOutcome outcome;
		CardDefinition selectedCard = null;
		var grantedConsumables = new List<CampaignConsumableType>();
		int ownedCardCount = campaignDeck == null
			? 0
			: campaignDeck.AvailableCount + campaignDeck.CooldownCount + campaignDeck.GraveyardCount;
		bool deckIsFull = ownedCardCount >= FlashTrialConsumableRewardCardThreshold;

		if (!deckIsFull && available.Count > 0)
		{
			List<FlashTrialCardCandidate> candidates = available
				.Select(card => new FlashTrialCardCandidate(card.Id, card.HeroClass, card.Strength))
				.ToList();
			outcome = machine.Roll(performance, completedLevels, candidates);
			selectedCard = available.FirstOrDefault(card => card.Id == outcome.CardId);
			if ((Object)(object)selectedCard != (Object)null && !TryAddCardToPlayerCollection(selectedCard))
			{
				AppendLog($"PROVA LAMPO - carta '{selectedCard.Id}' gia' presente, premio carta annullato.");
				selectedCard = null;
			}
		}
		else if (deckIsFull)
		{
			// Da 12 carte possedute (mazzo + cooldown + cimitero) le
			// prime due bobine pagano consumabili; la seconda soltanto con un Perfetto.
			// La terza bobina continua sempre a pagare EXP oppure oro.
			outcome = machine.Roll(performance, completedLevels);
			GrantRandomConsumable("PROVA LAMPO", out CampaignConsumableType first);
			grantedConsumables.Add(first);
			CampaignConsumableType? second = null;
			if (performance == FlashTrialResult.Perfect)
			{
				GrantRandomConsumable("PROVA LAMPO PERFETTA", out CampaignConsumableType extra);
				grantedConsumables.Add(extra);
				second = extra;
			}
			outcome = outcome.WithConsumables(
				CampaignConsumableName(first), CampaignConsumableResourceName(first),
				second.HasValue ? CampaignConsumableName(second.Value) : null,
				second.HasValue ? CampaignConsumableResourceName(second.Value) : null);
		}
		else
		{
			// Un pool carte vuoto non equivale a un mazzo pieno: finche' il mazzo non
			// raggiunge il limite, la slot non deve mai sostituire la carta con oggetti.
			outcome = machine.Roll(performance, completedLevels);
		}

		int bonusExperience = outcome.Currency == FlashTrialCurrencyReward.Experience ? outcome.Amount : 0;
		int bonusGold = outcome.Currency == FlashTrialCurrencyReward.Gold ? outcome.Amount : 0;
		if (bonusGold > 0)
		{
			runProgress.AddGold(bonusGold);
			if (ShouldTrackQuestProgress)
				runProgress.RecordGoldEarned(bonusGold);
		}

		string cardText = grantedConsumables.Count > 0
			? $" Ottieni {string.Join(" e ", grantedConsumables.Select(CampaignConsumableName))}."
			: (Object)(object)selectedCard != (Object)null
			? $" Ottieni {CardDisplayNames.MarketName(selectedCard)} (potenza {selectedCard.Strength})."
			: " Nessuna nuova carta disponibile.";
		string currencyText = bonusExperience > 0
			? $" +{bonusExperience} EXP."
			: $" +{bonusGold} oro.";
		string description = cardText + currencyText;
		AppendLog("PROVA LAMPO -" + description);
		return new FlashTrialCampaignReward(outcome, selectedCard, bonusExperience, bonusGold, description);
	}
}
}
