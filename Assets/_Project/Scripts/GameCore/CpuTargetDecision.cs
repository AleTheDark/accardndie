namespace AccardND.GameCore
{
    public readonly struct CpuTargetDecision
    {
        public CpuTargetDecision(
            int targetIndex,
            int score,
            double defeatProbability,
            MatchupResult matchup)
        {
            TargetIndex = targetIndex;
            Score = score;
            DefeatProbability = defeatProbability;
            Matchup = matchup;
        }

        public int TargetIndex { get; }
        public int Score { get; }
        public double DefeatProbability { get; }
        public MatchupResult Matchup { get; }
    }
}
