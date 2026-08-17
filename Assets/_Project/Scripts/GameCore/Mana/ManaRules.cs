namespace AccardND.GameCore.Mana
{
    /// <summary>
    /// Parametri dell'economia del mana (rune). Vedi Docs/mana-design.md.
    /// Condivisa fra campagna e PvP: il server compila questo assembly, quindi
    /// e' la stessa tabella su client e server.
    /// </summary>
    public sealed class ManaRules
    {
        public ManaRules(
            int maximum = 10,
            int runStart = 3,
            int roundFloor = 2,
            int gainOnActivation = 1,
            int gainOnSkip = 3,
            int gainOnParry = 1,
            int gainOnKill = 0,
            int gainOnLoss = 0,
            int supremeRepeatSurcharge = 1,
            int paladinReserveThreshold = 6,
            int attackCost = 1)
        {
            AttackCost = attackCost;
            Maximum = maximum;
            RunStart = runStart;
            RoundFloor = roundFloor;
            GainOnActivation = gainOnActivation;
            GainOnSkip = gainOnSkip;
            GainOnParry = gainOnParry;
            GainOnKill = gainOnKill;
            GainOnLoss = gainOnLoss;
            SupremeRepeatSurcharge = supremeRepeatSurcharge;
            PaladinReserveThreshold = paladinReserveThreshold;
        }

        /// <summary>Tetto della riserva. Il mana non lo supera mai.</summary>
        public int Maximum { get; }

        /// <summary>Riserva a inizio run di campagna / inizio match.</summary>
        public int RunStart { get; }

        /// <summary>A inizio stanza/round, se sei sotto questa soglia risali a essa.</summary>
        public int RoundFloor { get; }

        /// <summary>Recupero a fine attivazione della pedina.</summary>
        public int GainOnActivation { get; }

        /// <summary>Recupero se la pedina salta l'attivazione. Sostituisce <see cref="GainOnActivation"/>.</summary>
        public int GainOnSkip { get; }

        /// <summary>Recupero quando una pedina para un colpo.</summary>
        public int GainOnParry { get; }

        /// <summary>
        /// Recupero per chi elimina una pedina avversaria. A 0: uccidere non produce
        /// mana. Il meccanismo resta, quindi riattivarlo e' cambiare questo numero.
        /// </summary>
        public int GainOnKill { get; }

        /// <summary>
        /// Recupero per il proprietario della pedina eliminata. Era l'anti-valanga del
        /// progetto originale; a 0 anche questo, per tenere l'economia sulle tre voci
        /// che contano: parata, fine attivazione, costo d'attacco.
        /// </summary>
        public int GainOnLoss { get; }

        /// <summary>
        /// Sovrapprezzo cumulativo per ogni suprema successiva della stessa classe nel round.
        /// E' l'unico sovrapprezzo del gioco: le abilita' base hanno un costo fisso e nessuna
        /// spesa ne alza il prezzo, ne' per la stessa pedina ne' per il resto della squadra.
        /// </summary>
        public int SupremeRepeatSurcharge { get; }

        /// <summary>Soglia a cui la Riserva del Paladino porta il mana del giocatore.</summary>
        public int PaladinReserveThreshold { get; }

        /// <summary>
        /// Costo dell'attacco base. Col recupero di fine attivazione il saldo e' zero,
        /// quindi il costo morde solo a riserva vuota: chi e' a secco non puo' attaccare
        /// e deve saltare per rifiatare.
        /// </summary>
        public int AttackCost { get; }

        public static ManaRules CreateDefault() => new();

        /// <summary>
        /// Le stesse regole con un tetto diverso. Serve al talento "Riserva", che alza il
        /// massimo della riserva del giocatore in campagna.
        ///
        /// Il tetto si cambia qui e non dentro <see cref="ManaPool"/> perche' e' la regola a
        /// dire quanto mana ci sta: <c>Gain</c>, <c>RaiseTo</c> e <c>Restore</c> tagliano
        /// tutti su <see cref="Maximum"/>, e la barra a schermo legge lo stesso numero. Una
        /// riserva con un tetto "vero" e uno "mostrato" sarebbe due tetti.
        /// </summary>
        public ManaRules WithMaximum(int maximum) => new(
            maximum,
            RunStart,
            RoundFloor,
            GainOnActivation,
            GainOnSkip,
            GainOnParry,
            GainOnKill,
            GainOnLoss,
            SupremeRepeatSurcharge,
            PaladinReserveThreshold,
            AttackCost);
    }
}
