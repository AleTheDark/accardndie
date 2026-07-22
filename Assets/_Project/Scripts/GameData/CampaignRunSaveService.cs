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
    /// </summary>
    public sealed class PlayerPrefsCampaignRunStore : ICampaignRunStore
    {
        public const string Key = "AccardND.CampaignRun";

        public void Save(string json)
        {
            PlayerPrefs.SetString(Key, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool TryLoad(out string json)
        {
            json = PlayerPrefs.GetString(Key, string.Empty);
            return !string.IsNullOrEmpty(json);
        }

        public bool Exists() =>
            PlayerPrefs.HasKey(Key) && !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, string.Empty));

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
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

            if (save == null || save.version != CampaignRunSave.CurrentVersion)
            {
                save = null;
                return false;
            }
            return true;
        }

        public void Clear() => store.Delete();
    }
}
