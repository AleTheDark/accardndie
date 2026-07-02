namespace AccardND.GameCore
{
    public enum MatchupResult
    {
        Disadvantage = -1,
        Neutral = 0,
        Advantage = 1
    }

    public static class ClassMatchup
    {
        // Matrice a famiglie: Forza > Astuzia > Magia > Forza.
        public static MatchupResult Compare(HeroClass first, HeroClass second)
        {
            ClassFamily firstFamily = HeroClassFamily.Of(first);
            ClassFamily secondFamily = HeroClassFamily.Of(second);
            if (firstFamily == secondFamily)
                return MatchupResult.Neutral;

            if (Beats(firstFamily, secondFamily))
                return MatchupResult.Advantage;

            if (Beats(secondFamily, firstFamily))
                return MatchupResult.Disadvantage;

            return MatchupResult.Neutral;
        }

        private static bool Beats(ClassFamily first, ClassFamily second)
        {
            return (first == ClassFamily.Might && second == ClassFamily.Cunning)
                || (first == ClassFamily.Cunning && second == ClassFamily.Magic)
                || (first == ClassFamily.Magic && second == ClassFamily.Might);
        }
    }
}
