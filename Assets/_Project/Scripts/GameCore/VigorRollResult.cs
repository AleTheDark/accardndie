namespace AccardND.GameCore
{
    public readonly struct VigorRollResult
    {
        public VigorRollResult(
            int dieSides,
            int firstRoll,
            int secondRoll,
            bool hasSecondRoll,
            int selectedRoll,
            MatchupResult matchup,
            VigorSelectionMode selectionMode)
        {
            DieSides = dieSides;
            FirstRoll = firstRoll;
            SecondRoll = secondRoll;
            HasSecondRoll = hasSecondRoll;
            SelectedRoll = selectedRoll;
            Matchup = matchup;
            SelectionMode = selectionMode;
        }

        public int DieSides { get; }
        public int FirstRoll { get; }
        public int SecondRoll { get; }
        public bool HasSecondRoll { get; }
        public int SelectedRoll { get; }
        public MatchupResult Matchup { get; }
        public VigorSelectionMode SelectionMode { get; }
    }
}
