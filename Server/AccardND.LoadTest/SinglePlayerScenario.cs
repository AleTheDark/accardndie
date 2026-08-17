using AccardND.NetProtocol;

namespace AccardND.LoadTest;

/// <summary>
/// Il traffico di chi gioca in singolo, che al lancio sara' la stragrande maggioranza:
/// apertura dell'app (una raffica di letture), poi giri di run di campagna intervallati
/// da consultazioni di taverna, santuario, talenti e classifiche.
///
/// Le run scrivono davvero: <c>run.started</c>, i rapporti di uccisioni e la reward di
/// morte aprono transazioni su SQLite, ed e' li' che un solo scrittore diventa il collo
/// di bottiglia. Se qualcosa deve cedere sotto carico, cede prima di qui.
/// </summary>
public sealed class SinglePlayerScenario
{
    private static readonly string[] Chapters =
    {
        "chapter-1", "chapter-1", "chapter-1", "chapter-2", "chapter-2", "chapter-3"
    };

    private static readonly string[] Monsters =
    {
        "trentor", "bragus", "jurinashor", "seraphel", "medusa", "palatir"
    };

    private readonly Options options;
    private readonly Random random;

    public SinglePlayerScenario(Options options, Random random)
    {
        this.options = options;
        this.random = random;
    }

    /// <summary>Raffica di apertura: quello che il client chiede appena entrato.</summary>
    public static async Task OpenAppAsync(BotConnection bot, CancellationToken cancellation)
    {
        await bot.RequestAsync("rules.get", MessageTypes.RulesGet, null, cancellation);
        await bot.RequestAsync(
            "singleplayer.progress.get", MessageTypes.SinglePlayerProgressGet, null, cancellation);
        await bot.RequestAsync("profile.get", MessageTypes.ProfileGet, null, cancellation);
        await bot.RequestAsync("sanctuary.get", MessageTypes.SanctuaryGet, null, cancellation);
        await bot.RequestAsync("tavern.get", MessageTypes.TavernGet, null, cancellation);
        await bot.RequestAsync("talents.get", MessageTypes.TalentsGet, null, cancellation);
        await bot.RequestAsync("achievements.get", MessageTypes.AchievementsGet, null, cancellation);
        await bot.RequestAsync("iap.get", MessageTypes.IapGet, null, cancellation);
        await bot.RequestAsync("ranked.get", MessageTypes.RankedGet, null, cancellation);
    }

    /// <summary>
    /// Sblocca il Guerriero riscuotendo il primo modulo del tutorial. Serve solo ai bot
    /// che devono entrare in coda ranked: nelle stanze private il server non guarda gli
    /// sblocchi. E' idempotente, quindi si puo' richiamare a ogni riconnessione.
    /// </summary>
    public static Task UnlockWarriorAsync(BotConnection bot, CancellationToken cancellation) =>
        bot.RequestAsync(
            "singleplayer.reward.tutorial_module",
            MessageTypes.SinglePlayerClaimTutorialModule,
            new SinglePlayerTutorialModuleRequest
            {
                moduleId = "m1-warrior",
                moduleRunId = Guid.NewGuid().ToString("N")
            },
            cancellation);

    /// <summary>Un'azione del giocatore, scelta a sorte con i pesi del comportamento tipico.</summary>
    public async Task StepAsync(BotConnection bot, CancellationToken cancellation)
    {
        int roll = random.Next(100);
        if (roll < 35)
        {
            await PlayRunAsync(bot, cancellation);
            return;
        }

        if (roll < 60)
        {
            await bot.RequestAsync(
                "singleplayer.progress.get", MessageTypes.SinglePlayerProgressGet, null, cancellation);
            await bot.RequestAsync("tavern.get", MessageTypes.TavernGet, null, cancellation);
            return;
        }

        if (roll < 75)
        {
            await bot.RequestAsync("sanctuary.get", MessageTypes.SanctuaryGet, null, cancellation);
            await bot.RequestAsync("talents.get", MessageTypes.TalentsGet, null, cancellation);
            return;
        }

        if (roll < 88)
        {
            await bot.RequestAsync("profile.get", MessageTypes.ProfileGet, null, cancellation);
            await bot.RequestAsync("stats.get", MessageTypes.StatsGet, null, cancellation);
            return;
        }

        // Le classifiche sono le letture piu' pesanti: scandiscono e ordinano tabelle intere.
        await bot.RequestAsync("halloffame.get", MessageTypes.HallOfFameGet,
            new HallOfFameGetRequest { seasonId = 0 }, cancellation);
        await bot.RequestAsync(
            "adventure.leaderboard.get", MessageTypes.AdventureLeaderboardGet, null, cancellation);
    }

    /// <summary>Una run di campagna dall'inizio alla morte, come la registra il client.</summary>
    private async Task PlayRunAsync(BotConnection bot, CancellationToken cancellation)
    {
        string runId = Guid.NewGuid().ToString("N");
        string chapterId = Chapters[random.Next(Chapters.Length)];
        int rooms = random.Next(2, 12);

        await bot.RequestAsync("singleplayer.run.started", MessageTypes.SinglePlayerRunStarted,
            new SinglePlayerRunStartRequest
            {
                runId = runId,
                mode = "campaign",
                chapterId = chapterId,
                stageId = $"{chapterId}-stage-1"
            }, cancellation);

        // Durante la run il client riporta le uccisioni a ondate, non una per volta.
        int reports = 1 + rooms / 4;
        for (int index = 0; index < reports && !cancellation.IsCancellationRequested; index++)
        {
            await Task.Delay(ThinkDelay(2, 6), cancellation);
            await bot.RequestAsync("campaign.report_kills", MessageTypes.CampaignReportKills,
                new CampaignKillsRequest
                {
                    monsters = new[] { Monsters[random.Next(Monsters.Length)] },
                    bosses = Array.Empty<string>()
                }, cancellation);
        }

        await Task.Delay(ThinkDelay(2, 6), cancellation);

        int enemies = rooms * random.Next(2, 5);
        await bot.RequestAsync("singleplayer.reward.death", MessageTypes.SinglePlayerClaimDeathReward,
            new SinglePlayerDeathRewardRequest
            {
                runId = runId,
                mode = "campaign",
                chapterId = chapterId,
                stageId = $"{chapterId}-stage-1",
                roomsCleared = rooms,
                enemiesDefeated = enemies,
                bossesDefeated = rooms > 8 ? 1 : 0,
                minibossesDefeated = rooms / 5,
                matchExperience = enemies * 3,
                experienceEarned = enemies * 3,
                diceRolled = enemies * 4,
                abilitiesUsed = enemies,
                itemsUsed = random.Next(0, 3),
                supremesUsed = random.Next(0, 2),
                quickChallengesCompleted = random.Next(0, 2),
                merchantPurchases = random.Next(0, 3),
                goldEarned = enemies * 5,
                levelsGained = 0,
                defeatedBossIds = Array.Empty<string>(),
                consumedItemIds = Array.Empty<string>(),
                keptItemIds = Array.Empty<string>()
            }, cancellation);

        // Dopo la reward il client rilegge la progressione per aggiornare la schermata.
        await bot.RequestAsync(
            "singleplayer.progress.get", MessageTypes.SinglePlayerProgressGet, null, cancellation);
    }

    public TimeSpan ThinkDelay() => ThinkDelay(options.ThinkMinSeconds, options.ThinkMaxSeconds);

    private TimeSpan ThinkDelay(double minimum, double maximum) =>
        TimeSpan.FromSeconds(minimum + random.NextDouble() * (maximum - minimum));
}
