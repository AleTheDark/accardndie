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
            VigorSelectionMode selectionMode,
            int firstRollBeforeReroll = 0,
            int secondRollBeforeReroll = 0)
        {
            DieSides = dieSides;
            FirstRoll = firstRoll;
            SecondRoll = secondRoll;
            HasSecondRoll = hasSecondRoll;
            SelectedRoll = selectedRoll;
            Matchup = matchup;
            SelectionMode = selectionMode;
            FirstRollBeforeReroll = firstRollBeforeReroll;
            SecondRollBeforeReroll = secondRollBeforeReroll;
        }

        public int DieSides { get; }
        public int FirstRoll { get; }
        public int SecondRoll { get; }
        public bool HasSecondRoll { get; }
        public int SelectedRoll { get; }
        public MatchupResult Matchup { get; }
        public VigorSelectionMode SelectionMode { get; }
        public int FirstRollBeforeReroll { get; }
        public int SecondRollBeforeReroll { get; }
    }
}
