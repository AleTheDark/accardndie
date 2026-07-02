namespace AccardND.GameCore
{
    public readonly struct CpuDecisionWeights
    {
        public CpuDecisionWeights(
            int killProbabilityWeight,
            int classAdvantageWeight,
            int weakerTargetWeight,
            int randomTieBreaker)
        {
            KillProbabilityWeight = killProbabilityWeight;
            ClassAdvantageWeight = classAdvantageWeight;
            WeakerTargetWeight = weakerTargetWeight;
            RandomTieBreaker = randomTieBreaker;
        }

        public int KillProbabilityWeight { get; }
        public int ClassAdvantageWeight { get; }
        public int WeakerTargetWeight { get; }
        public int RandomTieBreaker { get; }
    }
}
