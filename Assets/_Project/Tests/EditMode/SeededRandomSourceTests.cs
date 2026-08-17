using System;
using AccardND.GameCore;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class SeededRandomSourceTests
    {
        /// <summary>
        /// Il seme della slot della Prova Lampo chiede l'intero range positivo: prima il
        /// maximum + 1 andava in overflow e ogni Sfida Veloce completata bloccava la stanza.
        /// </summary>
        [Test]
        public void NextInclusive_WithMaxIntUpperBound_StaysInRange()
        {
            var random = new SeededRandomSource(1729);

            for (int attempt = 0; attempt < 200; attempt++)
            {
                int value = random.NextInclusive(1, int.MaxValue);
                Assert.That(value, Is.InRange(1, int.MaxValue));
            }
        }

        [Test]
        public void NextInclusive_IncludesBothEnds()
        {
            var random = new SeededRandomSource(7);

            Assert.That(random.NextInclusive(4, 4), Is.EqualTo(4));

            bool sawMinimum = false;
            bool sawMaximum = false;
            for (int attempt = 0; attempt < 200 && !(sawMinimum && sawMaximum); attempt++)
            {
                int value = random.NextInclusive(0, 1);
                sawMinimum |= value == 0;
                sawMaximum |= value == 1;
            }

            Assert.That(sawMinimum && sawMaximum, Is.True);
        }

        /// <summary>
        /// Il punto dello snapshot di battaglia: ricaricare lo stesso salvataggio deve
        /// dare gli stessi dadi. Se ne desse di nuovi, chiudere l'app davanti a un turno
        /// andato male sarebbe il modo piu' comodo di ritirarli.
        /// </summary>
        [Test]
        public void Restore_FromTheSameSnapshot_AlwaysDealsTheSameDice()
        {
            var original = new SeededRandomSource(20260817);
            for (int roll = 0; roll < 37; roll++)
                original.NextInclusive(1, 6);
            int draws = original.Draws;

            int[] FirstRollsAfterResume()
            {
                SeededRandomSource resumed = SeededRandomSource.Restore(original.Seed, draws);
                return new[]
                {
                    resumed.NextInclusive(1, 6),
                    resumed.NextInclusive(1, 20),
                    resumed.NextInclusive(1, 100)
                };
            }

            Assert.That(draws, Is.EqualTo(37));
            Assert.That(FirstRollsAfterResume(), Is.EqualTo(FirstRollsAfterResume()));
        }

        [Test]
        public void Restore_KeepsCountingFromWhereItLeftOff()
        {
            var original = new SeededRandomSource(11);
            original.NextInclusive(1, 6);
            original.NextInclusive(1, 6);

            SeededRandomSource resumed = SeededRandomSource.Restore(original.Seed, original.Draws);
            resumed.NextInclusive(1, 6);

            // Il conto va avanti dal salvataggio: ripartire da zero avrebbe riavvolto il
            // flusso a ogni ripresa, e con esso la garanzia sui dadi.
            Assert.That(resumed.Draws, Is.EqualTo(3));
        }

        [Test]
        public void NextInclusive_WithInvertedRange_Throws()
        {
            var random = new SeededRandomSource(3);

            Assert.Throws<ArgumentException>(() => random.NextInclusive(5, 4));
        }
    }
}
