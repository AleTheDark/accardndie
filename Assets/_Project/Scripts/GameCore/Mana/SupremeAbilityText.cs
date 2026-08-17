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
                    "Costo 6 mana: Guadagni +2 alla Potenza fino a fine stanza. Se sei l'unica pedina rimasta il bonus sale a +4.",
                HeroClass.Rogue =>
                    "Costo 3 mana: Rubi un potenziamento e 2 mana al bersaglio. Se non ha potenziamenti, rubi invece 1 Potenza fino a fine stanza.",
                HeroClass.Mage =>
                    "Costo 4 mana: Colpisci tutte le pedine avversarie con un dado Vigore abbassato di uno step.",
                HeroClass.Hunter =>
                    "Costo 4 mana: Colpisci tutte le pedine avversarie con un dado Vigore abbassato di uno step.",
                HeroClass.Barbarian =>
                    "Costo 4 mana: Suoni la cornamusa: tutta la squadra accumula Furia. La conserva durante le sconfitte e la scarica alla prima vittoria.",
                HeroClass.Paladin =>
                    "Costo 2 mana: Attingi alla riserva: se il tuo mana e' sotto 6, risale a 6.",
                HeroClass.Priest =>
                    "Costo 4 mana: Togli tutti i malus agli alleati e tutti i potenziamenti agli avversari. Non tocca le aure.",
                HeroClass.Assassin =>
                    "Costo 5 mana: Diventi non bersagliabile. Quando resti l'unica pedina torni bersagliabile, ma difendi con vantaggio.",
                HeroClass.Necromancer =>
                    "Costo 8 mana: Evoca 2 sgherri di Potenza 2 che intercettano gli attacchi diretti e ad area. " +
                    "Non hanno un turno e non possono attaccare. Quando uno muore, tutte le tue pedine " +
                    "ottengono +1 Potenza; la Purificazione li dissolve senza attivare il bonus.",
                _ => string.Empty
            };
        }
    }
}
