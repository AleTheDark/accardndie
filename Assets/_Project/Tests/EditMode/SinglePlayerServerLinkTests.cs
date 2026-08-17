using System.Reflection;
using System.Threading.Tasks;
using AccardND.Network;
using NUnit.Framework;
using UnityEngine;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// L'aggancio alla progressione autoritativa. Qui si inchioda una cosa sola, ma e'
    /// quella che rendeva la taverna inaffidabile: un tentativo andato a vuoto non deve
    /// restare in cache. Quando restava, ogni richiesta successiva rispondeva "server
    /// assente" all'istante - per sempre - e l'unico modo di uscirne era passare
    /// dall'arena, l'unica schermata che rilascia il link e rifa' il login da sola.
    /// </summary>
    public sealed class SinglePlayerServerLinkTests
    {
        private GameObject host;
        private SinglePlayerServerLink link;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("SinglePlayerServerLinkTests");
            link = host.AddComponent<SinglePlayerServerLink>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        /// <summary>
        /// Senza sessione account il tentativo si conclude subito e senza repository:
        /// e' il caso che prima avvelenava la cache, perche' il task finiva prima ancora
        /// di essere memorizzato.
        /// </summary>
        [Test]
        public void EnsureRepository_WithoutSession_CompletesWithoutRepository()
        {
            Task<ServerSinglePlayerProgressRepository> attempt = link.EnsureRepositoryAsync();

            Assert.That(attempt.IsCompleted, Is.True);
            Assert.That(attempt.Result, Is.Null);
            Assert.That(link.IsReady, Is.False);
        }

        /// <summary>
        /// Il cuore della regressione. Si guarda il campo privato perche' e' l'unico modo
        /// di vederla: un metodo async che si conclude subito con risultato null restituisce
        /// sempre la stessa istanza di Task cachata dal runtime, quindi confrontare i due
        /// task non distingue "rieseguito" da "riproposto dalla cache". Quello che conta e'
        /// che dopo un tentativo concluso non resti niente da riproporre.
        /// </summary>
        [Test]
        public void EnsureRepository_AfterAConcludedAttempt_KeepsNothingToReplay()
        {
            Task<ServerSinglePlayerProgressRepository> first = link.EnsureRepositoryAsync();
            Assert.That(first.IsCompleted, Is.True, "senza sessione il tentativo si conclude da solo");

            Assert.That(CachedAttempt(), Is.Null, "un tentativo concluso non va tenuto in cache");
            Assert.That(link.IsConnecting, Is.False);
        }

        private object CachedAttempt() => typeof(SinglePlayerServerLink)
            .GetField("ensureTask", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(link);
    }
}
