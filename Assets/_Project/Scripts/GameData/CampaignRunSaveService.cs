using System;
using UnityEngine;

namespace AccardND.GameData
{
    /// <summary>Persistenza grezza (stringa JSON) del save di campagna.</summary>
    public interface ICampaignRunStore
    {
        void Save(string json);
        bool TryLoad(out string json);
        bool Exists();
        void Delete();
    }

    /// <summary>
    /// Store basato su PlayerPrefs: affidabile anche su WebGL/PWA perché PlayerPrefs.Save()
    /// forza il flush su IndexedDB, a differenza delle scritture su file che vengono sincronizzate
    /// solo periodicamente.
    ///
    /// Il salvataggio è di chi lo ha giocato: la chiave porta il playerId. Con una chiave
    /// sola per dispositivo, due account sullo stesso telefono si passavano la campagna a
    /// vicenda - al secondo veniva proposta la run del primo, e riprendendola scriveva i
    /// progressi sul proprio account.
    ///
    /// Senza account (partita offline, login non ancora fatto) si continua a usare la
    /// chiave storica: quel salvataggio non ha ancora un proprietario, e appena il
    /// giocatore entra viene adottato dal suo (vedi <see cref="AdoptSaveWithoutOwner"/>).
    /// </summary>
    public sealed class PlayerPrefsCampaignRunStore : ICampaignRunStore
    {
        /// <summary>La chiave di quando il salvataggio era uno per dispositivo.</summary>
        public const string Key = "AccardND.CampaignRun";

        private const string OwnedKeyPrefix = "AccardND.CampaignRun.";

        private readonly Func<string> ownerId;

        /// <summary>
        /// <paramref name="ownerId"/> viene letto a ogni accesso, non una volta sola: il
        /// giocatore può fare logout e rientrare con un altro account senza che il gioco
        /// riparta, e da quel momento il salvataggio giusto è un altro.
        /// </summary>
        public PlayerPrefsCampaignRunStore(Func<string> ownerId = null)
        {
            this.ownerId = ownerId;
        }

        private string Owner
        {
            get
            {
                string owner = ownerId?.Invoke();
                return string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
            }
        }

        private string CurrentKey
        {
            get
            {
                string owner = Owner;
                return owner == null ? Key : OwnedKeyPrefix + owner;
            }
        }

        public void Save(string json)
        {
            AdoptSaveWithoutOwner();
            PlayerPrefs.SetString(CurrentKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool TryLoad(out string json)
        {
            AdoptSaveWithoutOwner();
            json = PlayerPrefs.GetString(CurrentKey, string.Empty);
            return !string.IsNullOrEmpty(json);
        }

        public bool Exists()
        {
            AdoptSaveWithoutOwner();
            string key = CurrentKey;
            return PlayerPrefs.HasKey(key) && !string.IsNullOrEmpty(PlayerPrefs.GetString(key, string.Empty));
        }

        public void Delete()
        {
            // Anche qui si passa dall'adozione: una run finita deve portarsi via pure il
            // salvataggio orfano, o resterebbe lì ad aspettare di essere proposto a chi
            // apre la campagna dopo.
            AdoptSaveWithoutOwner();
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Passa all'account che sta giocando adesso il salvataggio rimasto senza
        /// proprietario: quello scritto prima di questa versione, o durante una partita
        /// cominciata senza essere entrati. Si fa una volta sola - la chiave vecchia
        /// sparisce comunque - altrimenti resterebbe lì a farsi adottare a turno da ogni
        /// account che apre la campagna.
        /// </summary>
        private void AdoptSaveWithoutOwner()
        {
            if (Owner == null || !PlayerPrefs.HasKey(Key))
                return;

            string orphan = PlayerPrefs.GetString(Key, string.Empty);
            PlayerPrefs.DeleteKey(Key);
            string key = CurrentKey;
            // Se questo account ha già una sua campagna in corso, quella vince: è più
            // recente e comunque è sua.
            if (!string.IsNullOrEmpty(orphan) && string.IsNullOrEmpty(PlayerPrefs.GetString(key, string.Empty)))
                PlayerPrefs.SetString(key, orphan);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Salva/carica lo stato di una run di campagna. Serializza <see cref="CampaignRunSave"/> in
    /// JSON e lo affida a un <see cref="ICampaignRunStore"/> (di default PlayerPrefs). Un JSON
    /// corrotto o di versione incompatibile viene trattato come "nessun salvataggio".
    /// </summary>
    public sealed class CampaignRunSaveService
    {
        private readonly ICampaignRunStore store;

        public CampaignRunSaveService() : this(new PlayerPrefsCampaignRunStore())
        {
        }

        public CampaignRunSaveService(ICampaignRunStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool HasSave => store.Exists();

        public void Save(CampaignRunSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            store.Save(JsonUtility.ToJson(save));
        }

        public bool TryLoad(out CampaignRunSave save)
        {
            save = null;
            if (!store.TryLoad(out string json) || string.IsNullOrEmpty(json))
                return false;

            try
            {
                save = JsonUtility.FromJson<CampaignRunSave>(json);
            }
            catch (Exception)
            {
                save = null;
            }

            // Le versioni vecchie si leggono, non si buttano: la v1 non conosce la
            // battaglia in corso e riprende dalla scelta della via, che e' quello che
            // faceva comunque. Rifiutarla avrebbe fatto sparire la campagna di chi
            // aggiorna il gioco a meta' run.
            if (save == null
                || save.version < CampaignRunSave.MinimumSupportedVersion
                || save.version > CampaignRunSave.CurrentVersion)
            {
                save = null;
                return false;
            }
			// I save v1 precedenti al mana non contengono il campo: JsonUtility lo
			// lascerebbe a zero, mentre una run esistente deve ripartire dalla riserva base.
			if (!json.Contains("\"playerMana\""))
				save.playerMana = CampaignRunSave.DefaultPlayerMana;
            return true;
        }

        public void Clear() => store.Delete();
    }
}
