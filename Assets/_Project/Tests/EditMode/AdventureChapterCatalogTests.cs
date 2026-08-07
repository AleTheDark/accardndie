using System.Collections.Generic;
using System.Linq;
using AccardND.GameData;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// Il catalogo dei capitoli lato client. Non decide niente - l'autorita' e' il server -
    /// ma se e' incoerente la schermata Avventura carica lo scenario sbagliato o non carica
    /// niente, e sono errori che si vedono solo giocando.
    /// </summary>
    public sealed class AdventureChapterCatalogTests
    {
        [Test]
        public void Chapters_are_numbered_from_one_without_gaps()
        {
            IReadOnlyList<AdventureChapter> chapters = AdventureChapterCatalog.All;

            for (int index = 0; index < chapters.Count; index++)
            {
                Assert.That(chapters[index].Number, Is.EqualTo(index + 1),
                    "I numeri di capitolo devono seguire l'ordine della tabella.");
                Assert.That(chapters[index].Id, Is.EqualTo($"chapter-{index + 1}"),
                    "L'id finisce nel database: deve corrispondere al numero.");
            }
        }

        /// <summary>
        /// Un capitolo giocabile senza scenario o senza boss manderebbe LoadScenario a vuoto
        /// e farebbe partire la run sullo sfondo di ripiego.
        /// </summary>
        [Test]
        public void Playable_chapters_have_a_scenario_and_a_boss()
        {
            foreach (AdventureChapter chapter in AdventureChapterCatalog.All.Where(c => c.Playable))
            {
                Assert.That(chapter.ScenarioId, Is.Not.Null.And.Not.Empty, chapter.Id);
                Assert.That(chapter.BossId, Is.Not.Null.And.Not.Empty, chapter.Id);
                Assert.That(chapter.ScenarioLabel, Is.Not.Null.And.Not.Empty, chapter.Id);
            }
        }

        [Test]
        public void A_boss_closes_at_most_one_chapter()
        {
            string[] bosses = AdventureChapterCatalog.All
                .Select(chapter => chapter.BossId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToArray();

            Assert.That(bosses, Is.Unique, "Due capitoli con lo stesso boss renderebbero ambiguo il completamento.");
        }

        [Test]
        public void Each_advanced_class_is_the_reward_of_a_single_chapter()
        {
            string[] rewards = AdventureChapterCatalog.All
                .Select(chapter => chapter.RewardClassId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToArray();

            Assert.That(rewards, Is.Unique);
        }

        [Test]
        public void The_tutorial_hands_over_the_first_chapter()
        {
            AdventureChapter first = AdventureChapterCatalog.Find(AdventureChapterCatalog.TutorialChapterId);

            Assert.That(first, Is.Not.Null);
            Assert.That(first.Number, Is.EqualTo(1));
            // Arriva dal tutorial: se avesse un prezzo comparirebbe in vendita al Santuario.
            Assert.That(first.HoneyCost, Is.Zero);
        }

        [Test]
        public void Chapters_that_can_be_bought_get_more_expensive_going_forward()
        {
            AdventureChapter[] priced = AdventureChapterCatalog.All
                .Where(chapter => chapter.HoneyCost > 0)
                .ToArray();

            for (int index = 1; index < priced.Length; index++)
            {
                Assert.That(priced[index].HoneyCost, Is.GreaterThan(priced[index - 1].HoneyCost),
                    $"{priced[index].Id} non costa piu' di {priced[index - 1].Id}.");
            }
        }

        [Test]
        public void A_boss_resolves_to_its_own_chapter()
        {
            Assert.That(AdventureChapterCatalog.FindByBoss("trentor")?.Id, Is.EqualTo("chapter-1"));
            Assert.That(AdventureChapterCatalog.FindByBoss("boss-bragus")?.Id, Is.EqualTo("chapter-2"));
            Assert.That(AdventureChapterCatalog.FindByBoss("boss-medusa")?.Id, Is.EqualTo("chapter-6"));
            Assert.That(AdventureChapterCatalog.FindByBoss("boss-palatir")?.Id, Is.EqualTo("chapter-7"));
            Assert.That(AdventureChapterCatalog.FindByBoss("carta-inventata"), Is.Null);
        }

        [Test]
        public void An_unknown_boss_still_gets_a_readable_name()
        {
            Assert.That(AdventureChapterCatalog.BossDisplayName("trentor"), Is.EqualTo("Trentor"));
            Assert.That(AdventureChapterCatalog.BossDisplayName(null), Is.EqualTo("Boss"));
            Assert.That(AdventureChapterCatalog.BossDisplayName("miniboss-composable-golem"), Is.EqualTo("Boss"));
        }
    }
}
