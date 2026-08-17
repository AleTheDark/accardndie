using System;

namespace AccardND.GameCore
{
    /// <summary>
    /// Sorgente casuale riproducibile: dallo stesso seme esce sempre la stessa sequenza.
    ///
    /// Tiene il conto delle estrazioni fatte perché una partita salvata a metà deve poter
    /// ripartire dallo stesso punto del flusso. Senza, una battaglia ripresa ripartirebbe
    /// da dadi nuovi: chiudere l'app davanti a un turno andato male e riaprirla sarebbe
    /// diventato il modo piu' comodo di ritirare i dadi.
    /// </summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random random;

        public SeededRandomSource(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        /// <summary>Il seme di partenza: va salvato insieme a <see cref="Draws"/>.</summary>
        public int Seed { get; }

        /// <summary>Quante estrazioni sono state chieste finora.</summary>
        public int Draws { get; private set; }

        /// <summary>
        /// Ricrea una sorgente ferma dove si era fermata quella salvata, ripercorrendo le
        /// estrazioni gia' fatte. Quello che conta non e' tanto ricucire il flusso esatto -
        /// nessun giocatore puo' accorgersene - quanto che il ripristino sia una funzione
        /// del solo salvataggio: ricaricare dieci volte lo stesso punto deve dare dieci
        /// volte gli stessi dadi, o il salvataggio diventa un pulsante "ritira".
        /// </summary>
        public static SeededRandomSource Restore(int seed, int draws)
        {
            var restored = new SeededRandomSource(seed);
            for (int drawn = 0; drawn < draws; drawn++)
                restored.random.Next();
            restored.Draws = Math.Max(0, draws);
            return restored;
        }

        public int NextInclusive(int minimum, int maximum)
        {
            if (minimum > maximum)
                throw new ArgumentException("Minimum cannot be greater than maximum.");

            Draws++;

            // Con maximum uguale a int.MaxValue la somma va in overflow e diventa int.MinValue:
            // Random.Next alzerebbe ArgumentOutOfRangeException proprio quando il chiamante
            // chiede l'intero range (per esempio per generare un seme).
            if (maximum == int.MaxValue)
            {
                long range = (long)maximum - minimum + 1L;
                return (int)(minimum + (long)(random.NextDouble() * range));
            }

            return random.Next(minimum, maximum + 1);
        }
    }
}
