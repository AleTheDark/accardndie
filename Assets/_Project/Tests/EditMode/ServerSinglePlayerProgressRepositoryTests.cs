using System;
using System.Threading.Tasks;
using AccardND.GameData;
using AccardND.Network;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class ServerSinglePlayerProgressRepositoryTests
    {
        [Test]
        public void RefreshAsync_AppliesServerSnapshotToCache()
        {
            var server = new FakeServerClient
            {
                NextSnapshot = new SinglePlayerProgressSave
                {
                    honey = 75,
                    hardcoreUnlocked = true,
                    unlockedChapters = { "chapter-1" }
                }
            };
            var cache = new LocalSinglePlayerProgressRepository(new InMemoryStore());
            var repo = new ServerSinglePlayerProgressRepository(server, cache);

            bool ok = Await(repo.RefreshAsync());

            Assert.That(ok, Is.True);
            Assert.That(repo.IsSynced, Is.True);
            Assert.That(repo.Honey, Is.EqualTo(75));
            Assert.That(repo.HardcoreUnlocked, Is.True);
            Assert.That(repo.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-1"), Is.True);
        }

        [Test]
        public void RefreshAsync_ServerUnreachable_KeepsCacheAndReportsNotSynced()
        {
            var cache = new LocalSinglePlayerProgressRepository(new InMemoryStore());
            var repo = new ServerSinglePlayerProgressRepository(new FakeServerClient(), cache);
            // Prima istantanea valida (es. cache persistita da una sessione precedente).
            repo.ApplyAuthoritative(new SinglePlayerProgressSave { honey = 40 });

            var offline = new FakeServerClient { ThrowOnLoad = new InvalidOperationException("offline") };
            var repoOffline = new ServerSinglePlayerProgressRepository(offline, cache);

            bool ok = Await(repoOffline.RefreshAsync());

            Assert.That(ok, Is.False);
            Assert.That(repoOffline.IsSynced, Is.False);
            // La cache (ultima istantanea nota) resta disponibile per l'uso offline.
            Assert.That(repoOffline.Honey, Is.EqualTo(40));
        }

        [Test]
        public void PurchaseUnlockAsync_ForwardsRequestAndAppliesNewState()
        {
            var server = new FakeServerClient
            {
                NextSnapshot = new SinglePlayerProgressSave { honey = 15, hardcoreUnlocked = true }
            };
            var repo = new ServerSinglePlayerProgressRepository(
                server, new LocalSinglePlayerProgressRepository(new InMemoryStore()));

            Await(repo.PurchaseUnlockAsync(SinglePlayerUnlockType.Chapter, "chapter-1"));

            Assert.That(server.LastPurchase, Is.EqualTo((SinglePlayerUnlockType.Chapter, "chapter-1")));
            Assert.That(repo.Honey, Is.EqualTo(15));
            Assert.That(repo.HardcoreUnlocked, Is.True);
        }

        [Test]
        public void PurchaseUnlockAsync_ServerRejects_PropagatesAndKeepsCache()
        {
            var server = new FakeServerClient
            {
                ThrowOnPurchase = new InvalidOperationException("Vasetti di miele insufficienti.")
            };
            var repo = new ServerSinglePlayerProgressRepository(
                server, new LocalSinglePlayerProgressRepository(new InMemoryStore()));
            repo.ApplyAuthoritative(new SinglePlayerProgressSave { honey = 10 });

            var ex = Assert.Throws<InvalidOperationException>(
                () => Await(repo.PurchaseUnlockAsync(SinglePlayerUnlockType.Chapter, "chapter-1")));

            Assert.That(ex.Message, Does.Contain("insufficienti"));
            Assert.That(repo.Honey, Is.EqualTo(10));
        }

        [Test]
        public void ClaimTutorialRewardAsync_AppliesRewardStateToCache()
        {
            var server = new FakeServerClient
            {
                NextReward = new SinglePlayerRewardOutcome(
                    new SinglePlayerProgressSave { honey = 60, tutorialCompleted = true }, "claim-1", 60)
            };
            var repo = new ServerSinglePlayerProgressRepository(
                server, new LocalSinglePlayerProgressRepository(new InMemoryStore()));

            SinglePlayerRewardOutcome outcome = Await(repo.ClaimTutorialRewardAsync("run-1"));

            Assert.That(server.LastTutorialRunId, Is.EqualTo("run-1"));
            Assert.That(outcome.GrantedHoney, Is.EqualTo(60));
            Assert.That(outcome.RewardClaimId, Is.EqualTo("claim-1"));
            Assert.That(repo.Honey, Is.EqualTo(60));
            Assert.That(repo.TutorialCompleted, Is.True);
        }

        [Test]
        public void ClaimDeathRewardAsync_ForwardsSummaryAndAppliesState()
        {
            var server = new FakeServerClient
            {
                NextReward = new SinglePlayerRewardOutcome(
                    new SinglePlayerProgressSave { honey = 21 }, "claim-death", 21)
            };
            var repo = new ServerSinglePlayerProgressRepository(
                server, new LocalSinglePlayerProgressRepository(new InMemoryStore()));

            var summary = new DeathRewardSummary("run-9", "hardcore", null, null, 3, 4, 1);
            SinglePlayerRewardOutcome outcome = Await(repo.ClaimDeathRewardAsync(summary));

            Assert.That(server.LastDeathSummary?.RunId, Is.EqualTo("run-9"));
            Assert.That(server.LastDeathSummary?.RoomsCleared, Is.EqualTo(3));
            Assert.That(outcome.RewardClaimId, Is.EqualTo("claim-death"));
            Assert.That(repo.Honey, Is.EqualTo(21));
        }

        [Test]
        public void ClaimAdMultiplierAsync_ForwardsClaimAndAppliesState()
        {
            var server = new FakeServerClient
            {
                NextReward = new SinglePlayerRewardOutcome(
                    new SinglePlayerProgressSave { honey = 63 }, "claim-death", 42)
            };
            var repo = new ServerSinglePlayerProgressRepository(
                server, new LocalSinglePlayerProgressRepository(new InMemoryStore()));

            SinglePlayerRewardOutcome outcome = Await(repo.ClaimAdMultiplierAsync("claim-death", "ad-imp-1"));

            Assert.That(server.LastAd, Is.EqualTo(("claim-death", "ad-imp-1")));
            Assert.That(outcome.GrantedHoney, Is.EqualTo(42));
            Assert.That(repo.Honey, Is.EqualTo(63));
        }

        [Test]
        public void LocalMutators_ThrowBecauseServerIsAuthoritative()
        {
            var repo = new ServerSinglePlayerProgressRepository(
                new FakeServerClient(), new LocalSinglePlayerProgressRepository(new InMemoryStore()));

            Assert.Throws<NotSupportedException>(() => repo.AddHoney(10));
            Assert.Throws<NotSupportedException>(() => repo.TrySpendHoney(5));
            Assert.Throws<NotSupportedException>(() => repo.SetTutorialCompleted());
            Assert.Throws<NotSupportedException>(() => repo.SetHardcoreUnlocked());
            Assert.Throws<NotSupportedException>(() => repo.Unlock(SinglePlayerUnlockType.Class, "wizard"));
        }

        private static T Await<T>(Task<T> task) => task.GetAwaiter().GetResult();
        private static void Await(Task task) => task.GetAwaiter().GetResult();

        private sealed class FakeServerClient : IServerSinglePlayerProgressClient
        {
            public SinglePlayerProgressSave NextSnapshot = new SinglePlayerProgressSave();
            public SinglePlayerRewardOutcome NextReward;
            public Exception ThrowOnLoad;
            public Exception ThrowOnPurchase;
            public (SinglePlayerUnlockType Type, string Id)? LastPurchase;
            public string LastTutorialRunId;
            public DeathRewardSummary? LastDeathSummary;
            public (string ClaimId, string AdId)? LastAd;

            public Task<SinglePlayerProgressSave> LoadProgressAsync() =>
                ThrowOnLoad != null
                    ? Task.FromException<SinglePlayerProgressSave>(ThrowOnLoad)
                    : Task.FromResult(NextSnapshot);

            public Task<SinglePlayerProgressSave> PurchaseUnlockAsync(SinglePlayerUnlockType type, string id)
            {
                LastPurchase = (type, id);
                return ThrowOnPurchase != null
                    ? Task.FromException<SinglePlayerProgressSave>(ThrowOnPurchase)
                    : Task.FromResult(NextSnapshot);
            }

            public bool HardcorePurchased;

            public Task<SinglePlayerProgressSave> PurchaseHardcoreAsync()
            {
                HardcorePurchased = true;
                return ThrowOnPurchase != null
                    ? Task.FromException<SinglePlayerProgressSave>(ThrowOnPurchase)
                    : Task.FromResult(NextSnapshot);
            }

            public Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId)
            {
                LastTutorialRunId = tutorialRunId;
                return Task.FromResult(NextReward);
            }

            public Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary)
            {
                LastDeathSummary = summary;
                return Task.FromResult(NextReward);
            }

            public Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId)
            {
                LastAd = (rewardClaimId, adImpressionId);
                return Task.FromResult(NextReward);
            }
        }

        private sealed class InMemoryStore : ISinglePlayerProgressStore
        {
            private string json;

            public void Save(string value) => json = value;
            public bool TryLoad(out string value)
            {
                value = json;
                return !string.IsNullOrEmpty(value);
            }
            public void Delete() => json = null;
        }
    }
}
