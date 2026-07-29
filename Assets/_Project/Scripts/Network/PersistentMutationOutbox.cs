using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Dove finiscono le richieste che non possono andare perse. Ogni mutazione
    /// critica (una reward, una riscossione, un acquisto) viene scritta qui *prima*
    /// di partire e cancellata solo quando il server ha risposto qualcosa di
    /// definitivo - anche un rifiuto.
    ///
    /// Serve al caso peggiore: la rete cade a fine run e il giocatore chiude il
    /// gioco prima che torni. Senza questa coda la ricompensa sparisce; con questa
    /// riparte al prossimo avvio con lo stesso requestId, e il dedup del server
    /// (che vive su disco, vedi RequestDedupStore) impedisce che venga applicata
    /// due volte se invece era arrivata.
    ///
    /// La memoria è deliberatamente corta: oltre <see cref="Lifetime"/> una
    /// mutazione non vale più la pena di essere rigiocata, e il ricordo lato server
    /// che la rende sicura sarebbe comunque scaduto.
    /// </summary>
    public sealed class PersistentMutationOutbox
    {
        /// <summary>
        /// Oltre questo tempo una voce viene lasciata cadere. Sta appena sotto la
        /// vita del dedup lato server (8 giorni): rigiocare qualcosa che il server
        /// non ricorda più significherebbe rischiare di applicarlo due volte.
        /// </summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

        /// <summary>
        /// Tetto di sicurezza: la coda è fatta per una manciata di mutazioni rimaste
        /// indietro, non per accumulare. Se si riempie cade la più vecchia.
        /// </summary>
        public const int MaxEntries = 32;

        private const string DefaultPrefsKey = "AccardND.ServerProgress.PersistentOutbox.v1";

        /// <summary>Una mutazione in attesa di conferma, con tutto il necessario per rispedirla.</summary>
        [Serializable]
        public sealed class Entry
        {
            public string playerId;
            public string requestId;
            public string messageType;
            public string expectedType;
            public string payloadJson;

            /// <summary>Quando è stata scritta (UTC, tick): serve solo a farla scadere.</summary>
            public long createdAtUtcTicks;
        }

        [Serializable]
        private sealed class EntryList
        {
            public List<Entry> entries = new();
        }

        /// <summary>
        /// Dove finisce il JSON della coda. Esiste per poter provare la coda senza
        /// PlayerPrefs (e senza sporcare le preferenze della macchina di chi lancia i test).
        /// </summary>
        public interface IStorage
        {
            string Read();
            void Write(string json);
            void Clear();
        }

        private sealed class PlayerPrefsStorage : IStorage
        {
            private readonly string key;

            public PlayerPrefsStorage(string key) => this.key = key;

            public string Read() => PlayerPrefs.GetString(key, string.Empty);

            public void Write(string json)
            {
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }

            public void Clear()
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private readonly IStorage storage;

        public PersistentMutationOutbox(IStorage storage = null)
        {
            this.storage = storage ?? new PlayerPrefsStorage(DefaultPrefsKey);
        }

        /// <summary>
        /// Scrive la mutazione su disco e restituisce il requestId con cui va spedita.
        /// Va chiamata prima dell'invio: è tutto il senso della coda.
        /// </summary>
        public Entry Add(string playerId, string messageType, string expectedType, string payloadJson)
        {
            var entry = new Entry
            {
                playerId = playerId ?? string.Empty,
                requestId = Guid.NewGuid().ToString("N"),
                messageType = messageType,
                expectedType = expectedType,
                payloadJson = payloadJson ?? string.Empty,
                createdAtUtcTicks = DateTime.UtcNow.Ticks
            };

            EntryList saved = Load();
            saved.entries.Add(entry);
            DropExpired(saved);
            while (saved.entries.Count > MaxEntries)
                saved.entries.RemoveAt(0);
            Save(saved);
            return entry;
        }

        /// <summary>Il server ha risposto (anche con un rifiuto): la mutazione è chiusa.</summary>
        public void Remove(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return;
            EntryList saved = Load();
            if (saved.entries.RemoveAll(entry => entry?.requestId == requestId) == 0)
                return;
            Save(saved);
        }

        /// <summary>
        /// Le mutazioni ancora in sospeso per questo giocatore, nell'ordine in cui
        /// sono nate (un moltiplicatore pubblicitario non ha senso prima della sua
        /// reward). Le scadute e quelle di altri account non tornano indietro; le
        /// scadute vengono anche tolte dal disco.
        /// </summary>
        public IReadOnlyList<Entry> PendingFor(string playerId)
        {
            EntryList saved = Load();
            if (DropExpired(saved))
                Save(saved);

            string owner = playerId ?? string.Empty;
            var pending = new List<Entry>();
            foreach (Entry entry in saved.entries)
            {
                if (entry != null && entry.playerId == owner)
                    pending.Add(entry);
            }
            return pending;
        }

        private static bool DropExpired(EntryList saved)
        {
            long oldest = DateTime.UtcNow.Subtract(Lifetime).Ticks;
            return saved.entries.RemoveAll(
                entry => entry == null || entry.createdAtUtcTicks < oldest) > 0;
        }

        private EntryList Load()
        {
            string json = storage.Read();
            if (string.IsNullOrEmpty(json))
                return new EntryList();
            try
            {
                return JsonUtility.FromJson<EntryList>(json) ?? new EntryList();
            }
            catch (Exception)
            {
                // Coda illeggibile (formato vecchio, scrittura interrotta): si riparte
                // vuoti. Perdere qui è meno grave che non partire.
                return new EntryList();
            }
        }

        private void Save(EntryList saved)
        {
            if (saved.entries.Count == 0)
                storage.Clear();
            else
                storage.Write(JsonUtility.ToJson(saved));
        }
    }
}
