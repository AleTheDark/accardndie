namespace AccardND.GameCore.Mana
{
    /// <summary>
    /// Nome ed effetto di ogni suprema, in italiano neutro. Vive in GameCore perche' lo
    /// leggono in due: il client per l'ispezione carta e il server per la voce del Santuario.
    /// Tenerlo qui evita che la descrizione venduta all'altare e quella mostrata in partita
    /// raccontino due cose diverse. La localizzazione resta sopra, in CardRulesGlossary:
    /// queste stringhe sono il fallback e la sorgente per chi non ha il catalogo testi.
    /// </summary>
    public static class SupremeAbilityText
    {
        /// <summary>Nome proprio della suprema, quello che compare sul bottone e sulla carta.</summary>
        public static string Name(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => "Potenziamento",
                HeroClass.Rogue => "Scippo",
                HeroClass.Mage => "Palla di Fuoco",
                HeroClass.Hunter => "Raffica",
                HeroClass.Barbarian => "Cornamusa",
                HeroClass.Paladin => "Riserva",
                HeroClass.Priest => "Purificazione",
                HeroClass.Assassin => "Invisibilita",
                HeroClass.Necromancer => "Evoca Sgherri",
                _ => "Suprema"
            };
        }

        public static string Description(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior =>
                    "Guadagni +2 alla Potenza fino a fine stanza. Se sei l'unica pedina rimasta il bonus sale a +4.",
                HeroClass.Rogue =>
                    "Rubi tutti i potenziamenti del bersaglio. Se non ne ha, gli togli 2 di Potenza fino a fine stanza.",
                HeroClass.Mage =>
                    "Colpisci tutte le pedine avversarie con un dado Vigore abbassato di uno step.",
                HeroClass.Hunter =>
                    "Colpisci tutte le pedine avversarie con un dado Vigore abbassato di uno step.",
                HeroClass.Barbarian =>
                    "Suoni la cornamusa: tutta la squadra riceve un potenziamento fino a fine stanza.",
                HeroClass.Paladin =>
                    "Attingi alla riserva: se il tuo mana e' sotto 6, risale a 6.",
                HeroClass.Priest =>
                    "Togli tutti i malus agli alleati e tutti i potenziamenti agli avversari. Non tocca le aure.",
                HeroClass.Assassin =>
                    "Diventi non bersagliabile. Quando resti l'unica pedina torni bersagliabile, ma difendi con vantaggio.",
                HeroClass.Necromancer =>
                    "In preparazione.",
                _ => string.Empty
            };
        }
    }
}
