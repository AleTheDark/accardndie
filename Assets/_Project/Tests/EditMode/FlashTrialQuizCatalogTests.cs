using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.Presentation;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// Il catalogo del quiz e' scritto a mano voce per voce: questi test tengono insieme
    /// le due cose che una svista non fa notare in partita, cioe' una domanda malformata
    /// e un catalogo che si vince a memoria.
    /// </summary>
    public sealed class FlashTrialQuizCatalogTests
    {
        [Test]
        public void Catalog_HasEnoughQuestionsToAvoidRepeats()
        {
            // Con una sessione da 3 domande servono abbastanza voci perche' due prove
            // di fila non mostrino le stesse: sotto questa soglia il quiz si impara.
            Assert.That(FlashTrialQuizCatalog.Questions.Length,
                Is.GreaterThanOrEqualTo(FlashTrialQuizSession.TotalQuestions * 6));
        }

        [Test]
        public void Catalog_EveryQuestionHasThreeAnswersAndAValidSolution()
        {
            foreach (FlashTrialQuizQuestion question in FlashTrialQuizCatalog.Questions)
            {
                Assert.That(question.AnswerCount, Is.EqualTo(3), question.LocalizationId);
                Assert.That(question.CorrectAnswerIndex, Is.InRange(0, 2), question.LocalizationId);
            }
        }

        [Test]
        public void Catalog_LocalizationIdsAreUnique()
        {
            List<string> duplicates = FlashTrialQuizCatalog.Questions
                .GroupBy(question => question.LocalizationId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.That(duplicates, Is.Empty);
        }

        [Test]
        public void Catalog_SolutionsAreSpreadAcrossTheThreeSlots()
        {
            // Le risposte non vengono mescolate a schermo. Se la soluzione stesse quasi
            // sempre nello stesso pulsante, il quiz si vincerebbe senza leggere.
            FlashTrialQuizQuestion[] questions = FlashTrialQuizCatalog.Questions;
            for (int slot = 0; slot < 3; slot++)
            {
                int count = questions.Count(question => question.CorrectAnswerIndex == slot);
                Assert.That(count, Is.GreaterThanOrEqualTo(questions.Length / 5),
                    $"Troppe poche soluzioni sulla risposta {slot}.");
            }
        }
    }
}
