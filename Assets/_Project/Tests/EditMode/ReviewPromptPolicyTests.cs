using AccardND.Presentation.ReviewPrompt;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// La regola che decide se chiedere la recensione. E' logica pura proprio per poter
    /// essere fissata qui: il popup vive dentro il flusso di fine campagna, dove
    /// riprodurre a mano ognuno di questi casi costerebbe una run intera.
    /// </summary>
    public sealed class ReviewPromptPolicyTests
    {
        private static ReviewPromptPolicy.Request Request(
            string chapterId = ReviewPromptPolicy.TriggerChapterId,
            bool runCompleted = true,
            bool isAndroid = true,
            bool alreadyPrompted = false,
            bool alreadyRated = false) =>
            new(chapterId, runCompleted, isAndroid, alreadyPrompted, alreadyRated);

        [Test]
        public void Prompts_after_first_completed_chapter_one_run_on_android()
        {
            Assert.IsTrue(ReviewPromptPolicy.ShouldPrompt(Request()));
        }

        [Test]
        public void Never_prompts_off_android()
        {
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(isAndroid: false)));
        }

        [Test]
        public void Never_prompts_after_a_lost_run()
        {
            // Chiedere le stelle a chi ha appena perso e' il modo migliore per riceverne una.
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(runCompleted: false)));
        }

        [Test]
        public void Never_prompts_outside_chapter_one()
        {
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(chapterId: "chapter-2")));
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(chapterId: "free-run")));
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(chapterId: null)));
        }

        [Test]
        public void Chapter_id_is_matched_loosely()
        {
            // L'id arriva da piu' punti del codice: spazi e maiuscole non devono
            // trasformare un innesco valido in un silenzio inspiegabile.
            Assert.IsTrue(ReviewPromptPolicy.ShouldPrompt(Request(chapterId: "  Chapter-1 ")));
        }

        [Test]
        public void Asks_only_once()
        {
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(alreadyPrompted: true)));
            Assert.IsFalse(ReviewPromptPolicy.ShouldPrompt(Request(alreadyRated: true)));
        }

        [Test]
        public void Star_gate_opens_the_store_only_on_full_marks()
        {
            for (int stars = 1; stars < ReviewPromptPolicy.MaxStars; stars++)
            {
                Assert.IsFalse(
                    ReviewPromptPolicy.ShouldOpenStore(ReviewPromptMode.StarGate, stars),
                    $"{stars} stelle non devono aprire lo store");
            }

            Assert.IsTrue(
                ReviewPromptPolicy.ShouldOpenStore(ReviewPromptMode.StarGate, ReviewPromptPolicy.MaxStars));
        }

        [Test]
        public void Direct_ask_never_filters_by_rating()
        {
            // E' cio' che rende conforme la modalita': il voto non decide chi passa.
            for (int stars = 0; stars <= ReviewPromptPolicy.MaxStars; stars++)
            {
                Assert.IsTrue(
                    ReviewPromptPolicy.ShouldOpenStore(ReviewPromptMode.DirectAsk, stars),
                    $"{stars} stelle devono comunque poter aprire lo store");
            }
        }
    }
}
