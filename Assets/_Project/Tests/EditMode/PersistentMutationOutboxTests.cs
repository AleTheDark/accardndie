using System;
using AccardND.NetProtocol;
using AccardND.Network;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// La coda che tiene in vita le mutazioni critiche attraverso una caduta di rete
    /// e la chiusura del gioco. Se sbaglia in un verso perde una ricompensa, se
    /// sbaglia nell'altro la fa pagare due volte: vale la pena inchiodarla.
    /// </summary>
    public sealed class PersistentMutationOutboxTests
    {
        private sealed class MemoryStorage : PersistentMutationOutbox.IStorage
        {
            public string Json = string.Empty;
            public string Read() => Json;
            public void Write(string json) => Json = json;
            public void Clear() => Json = string.Empty;
        }

        [Test]
        public void Add_PersistsTheMutationForItsOwner()
        {
            var storage = new MemoryStorage();
            var outbox = new PersistentMutationOutbox(storage);

            PersistentMutationOutbox.Entry entry = outbox.Add(
                "player-a", MessageTypes.TavernClaimQuest, MessageTypes.TavernData, "{\"questId\":\"q1\"}");

            var reopened = new PersistentMutationOutbox(storage);
            var pending = reopened.PendingFor("player-a");

            Assert.That(pending.Count, Is.EqualTo(1));
            Assert.That(pending[0].requestId, Is.EqualTo(entry.requestId));
            Assert.That(pending[0].messageType, Is.EqualTo(MessageTypes.TavernClaimQuest));
            Assert.That(pending[0].expectedType, Is.EqualTo(MessageTypes.TavernData));
            Assert.That(pending[0].payloadJson, Is.EqualTo("{\"questId\":\"q1\"}"));
        }

        [Test]
        public void Remove_ClosesTheMutationSoItIsNotReplayed()
        {
            var storage = new MemoryStorage();
            var outbox = new PersistentMutationOutbox(storage);

            PersistentMutationOutbox.Entry entry = outbox.Add(
                "player-a", MessageTypes.SanctuaryBuyItem, MessageTypes.SanctuaryData, "{}");
            outbox.Remove(entry.requestId);

            Assert.That(outbox.PendingFor("player-a"), Is.Empty);
            Assert.That(storage.Json, Is.Empty, "la coda vuota non deve lasciare residui su disco");
        }

        [Test]
        public void PendingFor_IgnoresMutationsOfAnotherAccount()
        {
            var outbox = new PersistentMutationOutbox(new MemoryStorage());
            outbox.Add("player-a", MessageTypes.TavernClaimBonus, MessageTypes.TavernData, string.Empty);

            Assert.That(outbox.PendingFor("player-b"), Is.Empty);
            Assert.That(outbox.PendingFor("player-a").Count, Is.EqualTo(1));
        }

        [Test]
        public void PendingFor_KeepsInsertionOrder()
        {
            var outbox = new PersistentMutationOutbox(new MemoryStorage());
            // Il moltiplicatore pubblicitario non ha senso prima della reward che moltiplica.
            outbox.Add("player-a", MessageTypes.SinglePlayerClaimDeathReward, MessageTypes.SinglePlayerRewardResult, "{}");
            outbox.Add("player-a", MessageTypes.SinglePlayerClaimAdMultiplier, MessageTypes.SinglePlayerRewardResult, "{}");

            var pending = outbox.PendingFor("player-a");

            Assert.That(pending[0].messageType, Is.EqualTo(MessageTypes.SinglePlayerClaimDeathReward));
            Assert.That(pending[1].messageType, Is.EqualTo(MessageTypes.SinglePlayerClaimAdMultiplier));
        }

        [Test]
        public void QueueDeathRewardForReplay_PersistsRewardAndAllQuestStats()
        {
            var storage = new MemoryStorage();
            var outbox = new PersistentMutationOutbox(storage);
            var summary = new DeathRewardSummary(
                "run-offline", "campaign", "chapter-2", "stage-4",
                7, 19, 1, 43, 2,
                new[] { "golem", "boss" }, new[] { "potion", "bomb" },
                31, 6, 125,
                // itemsUsed non e' la lunghezza di consumedItemIds: conta anche gli oggetti
                // trovati in run o comprati al mercante e usati sul posto.
                itemsUsed: 5, keptItemIds: new[] { "detector" });

            bool queued = ServerSinglePlayerProgressClient.QueueDeathRewardForReplay(
                summary, "player-a", outbox);

            Assert.That(queued, Is.True);
            var pending = outbox.PendingFor("player-a");
            Assert.That(pending.Count, Is.EqualTo(1));
            var payload = UnityEngine.JsonUtility.FromJson<SinglePlayerDeathRewardRequest>(pending[0].payloadJson);
            Assert.That(payload.runId, Is.EqualTo("run-offline"));
            Assert.That(payload.matchExperience, Is.EqualTo(43));
            Assert.That(payload.roomsCleared, Is.EqualTo(7));
            Assert.That(payload.enemiesDefeated, Is.EqualTo(19));
            Assert.That(payload.minibossesDefeated, Is.EqualTo(2));
            Assert.That(payload.bossesDefeated, Is.EqualTo(1));
            Assert.That(payload.diceRolled, Is.EqualTo(31));
            Assert.That(payload.abilitiesUsed, Is.EqualTo(6));
            Assert.That(payload.experienceEarned, Is.EqualTo(125));
            Assert.That(payload.itemsUsed, Is.EqualTo(5));
            Assert.That(payload.defeatedBossIds, Is.EqualTo(new[] { "golem", "boss" }));
            Assert.That(payload.consumedItemIds, Is.EqualTo(new[] { "potion", "bomb" }));
            Assert.That(payload.keptItemIds, Is.EqualTo(new[] { "detector" }));
        }

        [Test]
        public void QueueChapterClearForReplay_PersistsBossForItsOwner()
        {
            var storage = new MemoryStorage();
            var outbox = new PersistentMutationOutbox(storage);

            bool queued = ServerSinglePlayerProgressClient.QueueChapterClearForReplay(
                "boss-bragus", "player-a", outbox);

            Assert.That(queued, Is.True);
            var pending = outbox.PendingFor("player-a");
            Assert.That(pending.Count, Is.EqualTo(1));
            Assert.That(pending[0].messageType, Is.EqualTo(MessageTypes.SinglePlayerClearChapter));
            Assert.That(pending[0].expectedType, Is.EqualTo(MessageTypes.SinglePlayerProgressData));
            var payload = UnityEngine.JsonUtility.FromJson<SinglePlayerClearChapterRequest>(pending[0].payloadJson);
            Assert.That(payload.bossId, Is.EqualTo("boss-bragus"));
        }

        [Test]
        public void PendingFor_DropsMutationsOlderThanTheServerMemory()
        {
            // Oltre la vita del dedup lato server rigiocare non è più sicuro: meglio
            // perdere la mutazione che rischiare di applicarla due volte.
            long tooOld = DateTime.UtcNow.Subtract(PersistentMutationOutbox.Lifetime + TimeSpan.FromDays(1)).Ticks;
            var storage = new MemoryStorage
            {
                Json = "{\"entries\":[{\"playerId\":\"player-a\",\"requestId\":\"old\","
                    + "\"messageType\":\"" + MessageTypes.TavernClaimQuest + "\","
                    + "\"expectedType\":\"" + MessageTypes.TavernData + "\","
                    + "\"payloadJson\":\"{}\",\"createdAtUtcTicks\":" + tooOld + "}]}"
            };
            var outbox = new PersistentMutationOutbox(storage);

            Assert.That(outbox.PendingFor("player-a"), Is.Empty);
            Assert.That(storage.Json, Is.Empty, "la voce scaduta va anche tolta dal disco");
        }

        [Test]
        public void Add_BeyondTheCap_DropsTheOldest()
        {
            var outbox = new PersistentMutationOutbox(new MemoryStorage());
            for (int index = 0; index < PersistentMutationOutbox.MaxEntries + 3; index++)
            {
                outbox.Add(
                    "player-a",
                    MessageTypes.SinglePlayerPurchaseUnlock,
                    MessageTypes.SinglePlayerProgressData,
                    "{\"id\":\"" + index + "\"}");
            }

            var pending = outbox.PendingFor("player-a");

            Assert.That(pending.Count, Is.EqualTo(PersistentMutationOutbox.MaxEntries));
            Assert.That(pending[0].payloadJson, Is.EqualTo("{\"id\":\"3\"}"));
        }

        [Test]
        public void CorruptedQueue_StartsEmptyInsteadOfThrowing()
        {
            var storage = new MemoryStorage { Json = "non e' json" };
            var outbox = new PersistentMutationOutbox(storage);

            Assert.That(outbox.PendingFor("player-a"), Is.Empty);
            Assert.DoesNotThrow(() => outbox.Add(
                "player-a", MessageTypes.TavernClaimBonus, MessageTypes.TavernData, string.Empty));
        }
    }
}
