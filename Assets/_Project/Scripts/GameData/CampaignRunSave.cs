using System;
using System.Collections.Generic;
using AccardND.GameCore;

namespace AccardND.GameData
{
    /// <summary>Una carta del mazzo di campagna in uno snapshot salvato.</summary>
    [Serializable]
    public sealed class CampaignCardSave
    {
        public string definitionId;
        public int zone;
        public int instanceId;
    }

    /// <summary>Un consumabile posseduto in uno snapshot salvato.</summary>
    [Serializable]
    public sealed class CampaignConsumableSave
    {
        public string type;
        public int count;
    }

    /// <summary>
    /// Stato serializzabile di una run di campagna (save/resume). Contiene solo dati:
    /// niente riferimenti a UnityEngine.Object, così è (de)serializzabile con JsonUtility.
    /// Il punto di salvataggio previsto è la schermata "scelta della via", dove lo stato
    /// del combattimento è smontato e questi dati sono coerenti.
    /// </summary>
    [Serializable]
    public sealed class CampaignRunSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // Progressione (contatori di RunProgressState)
        public int playerLevel = 1;
        public int currentExperience;
        public int totalExperience;
        public int availableExperience;
        public int roomsCleared;

        // Mazzo di campagna
        public List<CampaignCardSave> deck = new List<CampaignCardSave>();
        public int nextInstanceId = 1;

        // Stato scenario / regole di stanza (popolato dal controller in fase di wiring)
        public string campaignScenarioId;
        public string campaignScenarioBossId;
        public bool merchantRoomsBlockedUntilMonster;
        public bool rewardRoomsBlockedUntilMonster;
        public int nextMonsterTierBonus;
        public bool nextDoorChoiceRevealed;

        // Consumabili posseduti (mappati dal controller in fase di wiring)
        public List<CampaignConsumableSave> consumables = new List<CampaignConsumableSave>();
    }

    /// <summary>
    /// Mappa lo stato di dominio (RunProgressState, CampaignDeckState) da/verso
    /// <see cref="CampaignRunSave"/>. Gli scalari di scenario/regole li imposta direttamente
    /// il controller sul DTO.
    /// </summary>
    public static class CampaignRunMapper
    {
        public static void WriteProgress(CampaignRunSave save, RunProgressState progress)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            save.playerLevel = progress.PlayerLevel;
            save.currentExperience = progress.CurrentExperience;
            save.totalExperience = progress.TotalExperience;
            save.availableExperience = progress.AvailableExperience;
            save.roomsCleared = progress.RoomsCleared;
        }

        public static void ReadProgress(CampaignRunSave save, RunProgressState progress)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            progress.RestoreProgress(save.playerLevel, save.currentExperience,
                save.totalExperience, save.availableExperience, save.roomsCleared);
        }

        public static void WriteDeck(CampaignRunSave save, CampaignDeckState deck)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (deck == null) throw new ArgumentNullException(nameof(deck));

            save.deck = new List<CampaignCardSave>(deck.Cards.Count);
            foreach (CampaignCardInstance card in deck.Cards)
            {
                save.deck.Add(new CampaignCardSave
                {
                    definitionId = card.Definition.Id,
                    zone = (int)card.Zone,
                    instanceId = card.InstanceId
                });
            }
            save.nextInstanceId = deck.NextInstanceId;
        }

        /// <summary>
        /// Ricostruisce il mazzo dallo snapshot. <paramref name="resolve"/> mappa un id carta a
        /// una CardDefinition (es. CardDatabase.FindById); le carte non più nel database vengono
        /// saltate, così un aggiornamento del gioco non rompe un salvataggio vecchio.
        /// </summary>
        public static void ReadDeck(CampaignRunSave save, CampaignDeckState deck, Func<string, CardDefinition> resolve)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));

            var entries = new List<CampaignCardRestoreEntry>(save.deck?.Count ?? 0);
            if (save.deck != null)
            {
                foreach (CampaignCardSave card in save.deck)
                {
                    CardDefinition definition = resolve(card.definitionId);
                    if (definition == null)
                        continue;
                    entries.Add(new CampaignCardRestoreEntry(definition, (CampaignCardZone)card.zone, card.instanceId));
                }
            }
            deck.RestoreFrom(entries, save.nextInstanceId);
        }
    }
}
