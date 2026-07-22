using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccardND.GameData
{
    [Serializable]
    public sealed class SinglePlayerProgressSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int honey;
        public bool tutorialCompleted;
        public bool hardcoreUnlocked;
        public List<string> unlockedChapters = new List<string>();
        public List<string> unlockedStages = new List<string>();
        public List<string> unlockedClasses = new List<string>();
        public List<string> unlockedScenarios = new List<string>();
        public List<string> unlockedSecondAbilities = new List<string>();
    }

    public interface ISinglePlayerProgressStore
    {
        void Save(string json);
        bool TryLoad(out string json);
        void Delete();
    }

    public sealed class PlayerPrefsSinglePlayerProgressStore : ISinglePlayerProgressStore
    {
        public const string Key = "AccardND.SinglePlayerProgress";

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

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    public interface ISinglePlayerProgressRepository
    {
        SinglePlayerProgressSave Progress { get; }
        int Honey { get; }
        bool TutorialCompleted { get; }
        bool HardcoreUnlocked { get; }
        void AddHoney(int amount);
        bool TrySpendHoney(int amount);
        void SetTutorialCompleted(bool completed = true);
        void SetHardcoreUnlocked(bool unlocked = true);
        bool IsUnlocked(SinglePlayerUnlockType type, string id);
        void Unlock(SinglePlayerUnlockType type, string id);
        /// <summary>
        /// Sostituisce l'intero stato con un'istantanea autoritativa (tipicamente ricevuta dal
        /// server). Serve alla cache locale per rispecchiare la progressione validata dal server.
        /// </summary>
        void ApplyAuthoritative(SinglePlayerProgressSave snapshot);
        void Clear();
    }

    /// <summary>
    /// Repository locale non autoritativo. Serve per sviluppo/cache offline; in produzione la
    /// progressione permanente deve essere validata e salvata dal server.
    /// </summary>
    public sealed class LocalSinglePlayerProgressRepository : ISinglePlayerProgressRepository
    {
        private readonly ISinglePlayerProgressStore store;
        private SinglePlayerProgressSave progress;

        public LocalSinglePlayerProgressRepository() : this(new PlayerPrefsSinglePlayerProgressStore())
        {
        }

        public LocalSinglePlayerProgressRepository(ISinglePlayerProgressStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public SinglePlayerProgressSave Progress => progress ??= LoadOrCreate();

        public int Honey => Progress.honey;
        public bool TutorialCompleted => Progress.tutorialCompleted;
        public bool HardcoreUnlocked => Progress.hardcoreUnlocked;

        public void AddHoney(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Progress.honey += amount;
            Save();
        }

        public bool TrySpendHoney(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Progress.honey < amount)
                return false;
            Progress.honey -= amount;
            Save();
            return true;
        }

        public void SetTutorialCompleted(bool completed = true)
        {
            Progress.tutorialCompleted = completed;
            Save();
        }

        public void SetHardcoreUnlocked(bool unlocked = true)
        {
            Progress.hardcoreUnlocked = unlocked;
            Save();
        }

        public bool IsUnlocked(SinglePlayerUnlockType type, string id)
        {
            return GetUnlockList(type).Contains(NormalizeId(id));
        }

        public void Unlock(SinglePlayerUnlockType type, string id)
        {
            string normalizedId = NormalizeId(id);
            if (string.IsNullOrEmpty(normalizedId))
                throw new ArgumentException("Unlock id cannot be empty.", nameof(id));

            List<string> list = GetUnlockList(type);
            if (!list.Contains(normalizedId))
            {
                list.Add(normalizedId);
                Save();
            }
        }

        public void ApplyAuthoritative(SinglePlayerProgressSave snapshot)
        {
            progress = Sanitize(Clone(snapshot ?? new SinglePlayerProgressSave()));
            Save();
        }

        public void Clear()
        {
            progress = new SinglePlayerProgressSave();
            store.Delete();
        }

        private SinglePlayerProgressSave LoadOrCreate()
        {
            if (!store.TryLoad(out string json) || string.IsNullOrEmpty(json))
                return new SinglePlayerProgressSave();

            SinglePlayerProgressSave loaded = null;
            try
            {
                loaded = JsonUtility.FromJson<SinglePlayerProgressSave>(json);
            }
            catch (Exception)
            {
                loaded = null;
            }

            return loaded == null || loaded.version != SinglePlayerProgressSave.CurrentVersion
                ? new SinglePlayerProgressSave()
                : Sanitize(loaded);
        }

        private void Save()
        {
            store.Save(JsonUtility.ToJson(Progress));
        }

        private List<string> GetUnlockList(SinglePlayerUnlockType type) => type switch
        {
            SinglePlayerUnlockType.Chapter => Progress.unlockedChapters,
            SinglePlayerUnlockType.Stage => Progress.unlockedStages,
            SinglePlayerUnlockType.Class => Progress.unlockedClasses,
            SinglePlayerUnlockType.Scenario => Progress.unlockedScenarios,
            SinglePlayerUnlockType.SecondAbility => Progress.unlockedSecondAbilities,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private static SinglePlayerProgressSave Sanitize(SinglePlayerProgressSave save)
        {
            save.honey = Math.Max(0, save.honey);
            save.unlockedChapters ??= new List<string>();
            save.unlockedStages ??= new List<string>();
            save.unlockedClasses ??= new List<string>();
            save.unlockedScenarios ??= new List<string>();
            save.unlockedSecondAbilities ??= new List<string>();
            return save;
        }

        private static string NormalizeId(string id) => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        private static SinglePlayerProgressSave Clone(SinglePlayerProgressSave source) => new SinglePlayerProgressSave
        {
            version = SinglePlayerProgressSave.CurrentVersion,
            honey = source.honey,
            tutorialCompleted = source.tutorialCompleted,
            hardcoreUnlocked = source.hardcoreUnlocked,
            unlockedChapters = new List<string>(source.unlockedChapters ?? new List<string>()),
            unlockedStages = new List<string>(source.unlockedStages ?? new List<string>()),
            unlockedClasses = new List<string>(source.unlockedClasses ?? new List<string>()),
            unlockedScenarios = new List<string>(source.unlockedScenarios ?? new List<string>()),
            unlockedSecondAbilities = new List<string>(source.unlockedSecondAbilities ?? new List<string>())
        };
    }

    public sealed class SinglePlayerProgressService : ISinglePlayerProgressRepository
    {
        private readonly ISinglePlayerProgressRepository repository;

        public SinglePlayerProgressService() : this(new LocalSinglePlayerProgressRepository())
        {
        }

        public SinglePlayerProgressService(ISinglePlayerProgressStore store)
            : this(new LocalSinglePlayerProgressRepository(store))
        {
        }

        public SinglePlayerProgressService(ISinglePlayerProgressRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public SinglePlayerProgressSave Progress => repository.Progress;
        public int Honey => repository.Honey;
        public bool TutorialCompleted => repository.TutorialCompleted;
        public bool HardcoreUnlocked => repository.HardcoreUnlocked;
        public void AddHoney(int amount) => repository.AddHoney(amount);
        public bool TrySpendHoney(int amount) => repository.TrySpendHoney(amount);
        public void SetTutorialCompleted(bool completed = true) => repository.SetTutorialCompleted(completed);
        public void SetHardcoreUnlocked(bool unlocked = true) => repository.SetHardcoreUnlocked(unlocked);
        public bool IsUnlocked(SinglePlayerUnlockType type, string id) => repository.IsUnlocked(type, id);
        public void Unlock(SinglePlayerUnlockType type, string id) => repository.Unlock(type, id);
        public void ApplyAuthoritative(SinglePlayerProgressSave snapshot) => repository.ApplyAuthoritative(snapshot);
        public void Clear() => repository.Clear();
    }

    public enum SinglePlayerUnlockType
    {
        Chapter,
        Stage,
        Class,
        Scenario,
        SecondAbility
    }
}
