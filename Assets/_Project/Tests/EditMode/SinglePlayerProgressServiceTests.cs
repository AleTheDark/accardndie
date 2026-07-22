using AccardND.GameData;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class SinglePlayerProgressServiceTests
    {
        [Test]
        public void LocalRepository_AddAndSpendHoney_PersistsBalance()
        {
            var store = new InMemoryStore();
            var service = new LocalSinglePlayerProgressRepository(store);

            service.AddHoney(25);

            Assert.That(service.Honey, Is.EqualTo(25));
            Assert.That(service.TrySpendHoney(9), Is.True);
            Assert.That(service.Honey, Is.EqualTo(16));
            Assert.That(service.TrySpendHoney(30), Is.False);
            Assert.That(service.Honey, Is.EqualTo(16));

            var reloaded = new LocalSinglePlayerProgressRepository(store);
            Assert.That(reloaded.Honey, Is.EqualTo(16));
        }

        [Test]
        public void LocalRepository_Unlock_PersistsWithoutDuplicates()
        {
            var store = new InMemoryStore();
            var service = new LocalSinglePlayerProgressRepository(store);

            service.Unlock(SinglePlayerUnlockType.Chapter, "chapter-1");
            service.Unlock(SinglePlayerUnlockType.Chapter, "chapter-1");
            service.SetTutorialCompleted();
            service.SetHardcoreUnlocked();

            var reloaded = new LocalSinglePlayerProgressRepository(store);

            Assert.That(reloaded.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-1"), Is.True);
            Assert.That(reloaded.TutorialCompleted, Is.True);
            Assert.That(reloaded.HardcoreUnlocked, Is.True);
            Assert.That(reloaded.Progress.unlockedChapters, Has.Count.EqualTo(1));
        }

        [Test]
        public void Service_DelegatesToInjectedRepository()
        {
            var repository = new LocalSinglePlayerProgressRepository(new InMemoryStore());
            var service = new SinglePlayerProgressService(repository);

            service.AddHoney(40);
            service.Unlock(SinglePlayerUnlockType.Scenario, "mirror");

            Assert.That(repository.Honey, Is.EqualTo(40));
            Assert.That(repository.IsUnlocked(SinglePlayerUnlockType.Scenario, "mirror"), Is.True);
        }

        [Test]
        public void ApplyAuthoritative_ReplacesStateAndPersists()
        {
            var store = new InMemoryStore();
            var service = new LocalSinglePlayerProgressRepository(store);
            service.AddHoney(5);
            service.Unlock(SinglePlayerUnlockType.Class, "old-class");

            var snapshot = new SinglePlayerProgressSave
            {
                honey = 120,
                tutorialCompleted = true,
                hardcoreUnlocked = true,
                unlockedChapters = { "chapter-1", "chapter-2" }
            };
            service.ApplyAuthoritative(snapshot);

            Assert.That(service.Honey, Is.EqualTo(120));
            Assert.That(service.TutorialCompleted, Is.True);
            Assert.That(service.HardcoreUnlocked, Is.True);
            Assert.That(service.IsUnlocked(SinglePlayerUnlockType.Chapter, "chapter-2"), Is.True);
            // Lo stato precedente viene sostituito, non fuso.
            Assert.That(service.IsUnlocked(SinglePlayerUnlockType.Class, "old-class"), Is.False);

            var reloaded = new LocalSinglePlayerProgressRepository(store);
            Assert.That(reloaded.Honey, Is.EqualTo(120));
            Assert.That(reloaded.Progress.unlockedChapters, Has.Count.EqualTo(2));
        }

        [Test]
        public void ApplyAuthoritative_ClonesLists_NoAliasingWithSource()
        {
            var service = new LocalSinglePlayerProgressRepository(new InMemoryStore());
            var snapshot = new SinglePlayerProgressSave { honey = 10 };
            snapshot.unlockedChapters.Add("chapter-1");

            service.ApplyAuthoritative(snapshot);
            // Mutare la sorgente dopo l'apply non deve toccare lo stato memorizzato.
            snapshot.unlockedChapters.Add("chapter-2");

            Assert.That(service.Progress.unlockedChapters, Has.Count.EqualTo(1));
        }

        [Test]
        public void ApplyAuthoritative_NullSnapshot_ResetsToEmpty()
        {
            var service = new LocalSinglePlayerProgressRepository(new InMemoryStore());
            service.AddHoney(50);

            service.ApplyAuthoritative(null);

            Assert.That(service.Honey, Is.EqualTo(0));
            Assert.That(service.TutorialCompleted, Is.False);
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
