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
        public int accountLevel = 1;
        public int accountExperience;
        public int accountTotalExperience;
        public int accountExperienceToNextLevel = 100;
        public bool tutorialCompleted;
        public bool hardcoreUnlocked;
        public List<string> unlockedChapters = new List<string>();
        public List<string> unlockedStages = new List<string>();
        public List<string> unlockedClasses = new List<string>();
        public List<string> unlockedScenarios = new List<string>();
        public List<string> unlockedSecondAbilities = new List<string>();

        /// <summary>
        /// Capitoli portati a termine (boss finale sconfitto), distinti da quelli soltanto
        /// sbloccati: un capitolo si compra, ma si completa solo giocandolo.
        /// </summary>
        public List<string> clearedChapters = new List<string>();

        /// <summary>Slot aggiuntivi della bisaccia gia acquistati.</summary>
        public List<string> unlockedSlots = new List<string>();

        /// <summary>Oggetti sbloccati al Santuario, quindi vendibili dal negozio.</summary>
        public List<string> unlockedItems = new List<string>();

        /// <summary>Consumabili scelti per la prossima run (id del catalogo Santuario).</summary>
        public List<string> bagItems = new List<string>();

        /// <summary>
        /// Contatori cumulativi di campagna rispecchiati dal server (nemici, boss, run).
        /// Sola lettura per il client: li aggiorna solo il server.
        /// </summary>
        public List<SinglePlayerCounterSave> counters = new List<SinglePlayerCounterSave>();
    }

    /// <summary>Un contatore cumulativo nella cache locale.</summary>
    [Serializable]
    public sealed class SinglePlayerCounterSave
    {
        public string key;
        public int value;
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
        int AddAccountExperience(int amount);
        bool TrySpendHoney(int amount);
        void SetTutorialCompleted(bool completed = true);
        void SetHardcoreUnlocked(bool unlocked = true);
        bool IsUnlocked(SinglePlayerUnlockType type, string id);
        void Unlock(SinglePlayerUnlockType type, string id);
        /// <summary>
        /// Valore di un contatore cumulativo (0 se mai incrementato). I contatori arrivano
        /// dal server: il client li legge e basta.
        /// </summary>
        int GetCounter(string key);
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
        public int AccountLevel => Progress.accountLevel;
        public int AccountExperience => Progress.accountExperience;
        public int AccountTotalExperience => Progress.accountTotalExperience;
        public int AccountExperienceToNextLevel => Progress.accountExperienceToNextLevel;
        public bool TutorialCompleted => Progress.tutorialCompleted;
        public bool HardcoreUnlocked => Progress.hardcoreUnlocked;

        public void AddHoney(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Progress.honey += amount;
            Save();
        }

        public int AddAccountExperience(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0)
                return 0;

            int levelsGained = 0;
            Progress.accountTotalExperience += amount;
            Progress.accountExperience += amount;
            Progress.accountExperienceToNextLevel = Progress.accountExperienceToNextLevel <= 0
                ? 100
                : Progress.accountExperienceToNextLevel;
            while (Progress.accountExperience >= Progress.accountExperienceToNextLevel)
            {
                Progress.accountExperience -= Progress.accountExperienceToNextLevel;
                Progress.accountLevel++;
                levelsGained++;
                Progress.honey += 5;
            }
            Save();
            return levelsGained;
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

        public int GetCounter(string key)
        {
            string normalizedKey = NormalizeId(key);
            if (string.IsNullOrEmpty(normalizedKey))
                return 0;

            foreach (SinglePlayerCounterSave counter in Progress.counters)
            {
                if (counter != null && string.Equals(counter.key, normalizedKey, StringComparison.Ordinal))
                    return Math.Max(0, counter.value);
            }
            return 0;
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
            SinglePlayerUnlockType.ChapterCleared => Progress.clearedChapters,
            SinglePlayerUnlockType.Slot => Progress.unlockedSlots,
            SinglePlayerUnlockType.Item => Progress.unlockedItems,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private static SinglePlayerProgressSave Sanitize(SinglePlayerProgressSave save)
        {
            save.honey = Math.Max(0, save.honey);
            save.accountLevel = Math.Max(1, save.accountLevel);
            save.accountExperience = Math.Max(0, save.accountExperience);
            save.accountTotalExperience = Math.Max(0, save.accountTotalExperience);
            save.accountExperienceToNextLevel = save.accountExperienceToNextLevel <= 0
                ? 100
                : save.accountExperienceToNextLevel;
            save.unlockedChapters ??= new List<string>();
            save.unlockedStages ??= new List<string>();
            save.unlockedClasses ??= new List<string>();
            save.unlockedScenarios ??= new List<string>();
            save.unlockedSecondAbilities ??= new List<string>();
            save.clearedChapters ??= new List<string>();
            save.unlockedSlots ??= new List<string>();
            save.unlockedItems ??= new List<string>();
            save.bagItems ??= new List<string>();
            save.counters ??= new List<SinglePlayerCounterSave>();
            return save;
        }

        private static string NormalizeId(string id) => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        private static SinglePlayerProgressSave Clone(SinglePlayerProgressSave source) => new SinglePlayerProgressSave
        {
            version = SinglePlayerProgressSave.CurrentVersion,
            honey = source.honey,
            accountLevel = source.accountLevel,
            accountExperience = source.accountExperience,
            accountTotalExperience = source.accountTotalExperience,
            accountExperienceToNextLevel = source.accountExperienceToNextLevel,
            tutorialCompleted = source.tutorialCompleted,
            hardcoreUnlocked = source.hardcoreUnlocked,
            unlockedChapters = new List<string>(source.unlockedChapters ?? new List<string>()),
            unlockedStages = new List<string>(source.unlockedStages ?? new List<string>()),
            unlockedClasses = new List<string>(source.unlockedClasses ?? new List<string>()),
            unlockedScenarios = new List<string>(source.unlockedScenarios ?? new List<string>()),
            unlockedSecondAbilities = new List<string>(source.unlockedSecondAbilities ?? new List<string>()),
            clearedChapters = new List<string>(source.clearedChapters ?? new List<string>()),
            unlockedSlots = new List<string>(source.unlockedSlots ?? new List<string>()),
            unlockedItems = new List<string>(source.unlockedItems ?? new List<string>()),
            bagItems = new List<string>(source.bagItems ?? new List<string>()),
            counters = CloneCounters(source.counters)
        };

        private static List<SinglePlayerCounterSave> CloneCounters(List<SinglePlayerCounterSave> source)
        {
            var cloned = new List<SinglePlayerCounterSave>();
            if (source == null)
                return cloned;

            foreach (SinglePlayerCounterSave counter in source)
            {
                if (counter != null && !string.IsNullOrWhiteSpace(counter.key))
                    cloned.Add(new SinglePlayerCounterSave { key = counter.key, value = Math.Max(0, counter.value) });
            }
            return cloned;
        }
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
        public int AccountLevel => repository.Progress.accountLevel;
        public int AccountExperience => repository.Progress.accountExperience;
        public int AccountTotalExperience => repository.Progress.accountTotalExperience;
        public int AccountExperienceToNextLevel => repository.Progress.accountExperienceToNextLevel;
        public bool TutorialCompleted => repository.TutorialCompleted;
        public bool HardcoreUnlocked => repository.HardcoreUnlocked;
        public void AddHoney(int amount) => repository.AddHoney(amount);
        public int AddAccountExperience(int amount) => repository.AddAccountExperience(amount);
        public bool TrySpendHoney(int amount) => repository.TrySpendHoney(amount);
        public void SetTutorialCompleted(bool completed = true) => repository.SetTutorialCompleted(completed);
        public void SetHardcoreUnlocked(bool unlocked = true) => repository.SetHardcoreUnlocked(unlocked);
        public bool IsUnlocked(SinglePlayerUnlockType type, string id) => repository.IsUnlocked(type, id);
        public void Unlock(SinglePlayerUnlockType type, string id) => repository.Unlock(type, id);
        public int GetCounter(string key) => repository.GetCounter(key);
        public void ApplyAuthoritative(SinglePlayerProgressSave snapshot) => repository.ApplyAuthoritative(snapshot);
        public void Clear() => repository.Clear();
    }

    public enum SinglePlayerUnlockType
    {
        Chapter,
        Stage,
        Class,
        Scenario,
        SecondAbility,

        /// <summary>
        /// Capitolo completato. Non e acquistabile: lo concede il server quando il boss
        /// finale del capitolo viene sconfitto.
        /// </summary>
        ChapterCleared,

        /// <summary>Slot aggiuntivo della bisaccia.</summary>
        Slot,

        /// <summary>
        /// Oggetto sbloccato: da' il diritto di comprarlo al negozio, non una copia.
        /// </summary>
        Item
    }
}
