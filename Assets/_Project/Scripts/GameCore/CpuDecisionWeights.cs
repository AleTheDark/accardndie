namespace AccardND.GameCore
{
    public readonly struct CpuDecisionWeights
    {
        public CpuDecisionWeights(
            int killProbabilityWeight,
            int classAdvantageWeight,
            int threatWeight,
            int randomTieBreaker)
        {
            KillProbabilityWeight = killProbabilityWeight;
            ClassAdvantageWeight = classAdvantageWeight;
            ThreatWeight = threatWeight;
            RandomTieBreaker = randomTieBreaker;
        }

        /// <summary>Punti assegnati a una probabilita' di uccisione del 100%.</summary>
        public int KillProbabilityWeight { get; }

        /// <summary>Preferenza stilistica per il vantaggio di classe, sopra al puro calcolo.</summary>
        public int ClassAdvantageWeight { get; }

        /// <summary>
        /// Punti per ogni punto di Potenza effettiva del bersaglio: positivo significa che la
        /// CPU va a togliere di mezzo le pedine piu' grosse.
        /// </summary>
        public int ThreatWeight { get; }

        /// <summary>
        /// Distrazione della CPU fuori da Diabolica, in percentuale di
        /// <see cref="KillProbabilityWeight"/>: 5 vale cinque punti di probabilita'.
        /// </summary>
        public int RandomTieBreaker { get; }
    }
}
