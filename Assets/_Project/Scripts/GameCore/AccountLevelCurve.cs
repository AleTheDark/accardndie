using System;

namespace AccardND.GameCore
{
    /// <summary>
    /// La curva di esperienza dell'account. Sorgente unica per client e server: sta in
    /// GameCore proprio perche' il progetto del server compila questi stessi sorgenti, e
    /// una curva scritta due volte diverge al primo ritocco di bilanciamento.
    ///
    /// Prima ogni chiamante che concedeva esperienza riscriveva a mano il proprio
    /// <c>while (current >= 100)</c>: erano tre copie della stessa soglia fissa, e il giorno
    /// in cui la soglia ha smesso di essere fissa sarebbero diventate tre curve diverse.
    ///
    /// Il costo di un livello cresce di 25 a livello: i primi dieci livelli restano rapidi,
    /// perche' chi comincia deve sentire la spinta subito, e da li' in avanti la coda si
    /// allunga senza mai fermarsi.
    /// </summary>
    public static class AccountLevelCurve
    {
        /// <summary>Costo del primo livello, e passo base della curva.</summary>
        public const int BaseCost = 100;

        /// <summary>Quanto cresce il costo a ogni livello.</summary>
        public const int CostGrowth = 25;

        /// <summary>Punti propoli consegnati da ogni livello account.</summary>
        public const int TalentPointsPerLevel = 1;

        /// <summary>Punti in piu' per ogni livello tondo attraversato.</summary>
        public const int TalentPointsMilestoneBonus = 2;

        /// <summary>Ogni quanti livelli scatta il bonus dei livelli tondi.</summary>
        public const int TalentPointsMilestoneEvery = 5;

        /// <summary>Punti propoli consegnati alla prima conclusione di un capitolo.</summary>
        public const int TalentPointsPerFirstChapterClear = 3;

        /// <summary>Esperienza che separa <paramref name="level"/> dal livello successivo.</summary>
        public static int ExperienceToNext(int level)
        {
            int safeLevel = Math.Max(1, level);
            return BaseCost + CostGrowth * (safeLevel - 1);
        }

        /// <summary>
        /// Applica <paramref name="amount"/> di esperienza a un account e restituisce lo
        /// stato nuovo. E' una funzione pura, quindi si verifica senza database.
        ///
        /// Il ciclo consuma una soglia per volta perche' la soglia cambia a ogni livello:
        /// una singola divisione varrebbe solo con la curva piatta di prima.
        /// </summary>
        public static AccountLevelProgress Apply(int level, int current, int total, int amount)
        {
            int safeLevel = Math.Max(1, level);
            int safeCurrent = Math.Max(0, current);
            int safeTotal = Math.Max(0, total);
            int granted = Math.Max(0, amount);

            safeCurrent += granted;
            safeTotal += granted;

            int levelsGained = 0;
            int toNext = ExperienceToNext(safeLevel);
            while (safeCurrent >= toNext)
            {
                safeCurrent -= toNext;
                safeLevel++;
                levelsGained++;
                toNext = ExperienceToNext(safeLevel);
            }

            return new AccountLevelProgress(safeLevel, safeCurrent, safeTotal, toNext, levelsGained);
        }

        /// <summary>
        /// Punti propoli consegnati salendo da <paramref name="fromLevel"/> (escluso) a
        /// <paramref name="toLevel"/> (incluso): uno per livello, piu' il bonus di ogni
        /// livello tondo attraversato. In forma chiusa, cosi' non dipende da quanti livelli
        /// si riscuotono in una volta sola.
        ///
        /// Sta qui accanto alla curva perche' l'accredito e' del server ma il popup di
        /// level-up deve poter annunciare la cifra esatta: con la regola scritta due volte
        /// il client prometterebbe un numero e il server ne accrediterebbe un altro.
        /// </summary>
        public static int TalentPointsForLevels(int fromLevel, int toLevel)
        {
            int start = Math.Max(0, fromLevel);
            int end = Math.Max(start, toLevel);
            int levels = end - start;
            int milestones = end / TalentPointsMilestoneEvery - start / TalentPointsMilestoneEvery;
            return levels * TalentPointsPerLevel + milestones * TalentPointsMilestoneBonus;
        }
    }

    /// <summary>Stato di un account dopo un accredito di esperienza.</summary>
    public readonly struct AccountLevelProgress
    {
        public AccountLevelProgress(
            int level, int experience, int totalExperience, int experienceToNextLevel, int levelsGained)
        {
            Level = level;
            Experience = experience;
            TotalExperience = totalExperience;
            ExperienceToNextLevel = experienceToNextLevel;
            LevelsGained = levelsGained;
        }

        public int Level { get; }
        public int Experience { get; }
        public int TotalExperience { get; }

        /// <summary>
        /// Quanta esperienza serve <em>in tutto</em> per il livello successivo, non quanta ne
        /// manca: e' il denominatore della barra, ed e' quello che la colonna omonima sul
        /// database ha sempre voluto dire.
        /// </summary>
        public int ExperienceToNextLevel { get; }

        public int LevelsGained { get; }
    }
}
