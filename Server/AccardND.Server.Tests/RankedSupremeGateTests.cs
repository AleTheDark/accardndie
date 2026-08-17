using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Match;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// In classificata si usano solo le supreme sbloccate; in amichevole si usa tutto.
///
/// Il test vive qui e non fra gli EditMode di Unity, dove stanno gli altri test del motore,
/// perche' e' meta' di una regola ranked - l'altra meta' e' <see cref="RankedLoadoutEligibilityTests"/>,
/// che copre le classi al momento della coda - e perche' qui gira a ogni `dotnet test` invece
/// che solo aprendo l'editor.
/// </summary>
public sealed class RankedSupremeGateTests
{
    [Fact]
    public void UnaSupremeNonSbloccataVieneRifiutataInClassificata()
    {
        PvpMatchEngine engine = BattleReady(allowedForPlayer0: Array.Empty<HeroClass>());
        BankMana(engine, player: 0, required: 8);

        var rifiuto = Assert.Throws<PvpActionException>(
            () => engine.UseSupreme(0, targetPlayer: 1, targetSlot: 0));

        Assert.Equal(PvpActionErrorCodes.SupremeNotAvailable, rifiuto.ErrorCode);
        Assert.Contains("sbloccato", rifiuto.Message);
    }

    [Fact]
    public void LaStessaSupremePassaSeLaClasseEStataSbloccata()
    {
        PvpMatchEngine engine = BattleReady(allowedForPlayer0: new[] { HeroClass.Mage });
        BankMana(engine, player: 0, required: 8);

        // Non lancia: e' l'unica asserzione che serve, il resto degli effetti della supreme
        // e' gia' coperto dai test del motore.
        engine.UseSupreme(0, targetPlayer: 1, targetSlot: 0);
    }

    [Fact]
    public void InAmichevoleSiUsaTuttoAncheSenzaSblocchi()
    {
        // allowedSupremes null = nessun limite: e' esattamente quello che MatchSession passa
        // quando la stanza non e' classificata.
        PvpMatchEngine engine = BattleReady(allowedForPlayer0: null);
        BankMana(engine, player: 0, required: 8);

        engine.UseSupreme(0, targetPlayer: 1, targetSlot: 0);
    }

    [Fact]
    public void GliIdDelSantuarioDiventanoClassi()
    {
        var progress = new SinglePlayerProgressData
        {
            unlockedSecondAbilities = new[] { "ability-mage-2", "ability-priest-2", "spazzatura" }
        };

        IReadOnlyCollection<HeroClass> unlocked = RankedLoadoutEligibility.UnlockedSupremesOf(progress);

        Assert.Contains(HeroClass.Mage, unlocked);
        Assert.Contains(HeroClass.Priest, unlocked);
        // L'id illeggibile viene saltato invece di far saltare la partita.
        Assert.Equal(2, unlocked.Count);
    }

    [Fact]
    public void SenzaSblocchiLInsiemeEVuotoMaMaiNullo()
    {
        Assert.Empty(RankedLoadoutEligibility.UnlockedSupremesOf(null));
        Assert.Empty(RankedLoadoutEligibility.UnlockedSupremesOf(new SinglePlayerProgressData()));
    }

    // --- impalcatura -------------------------------------------------------------------
    // Ricalca quella di Assets/_Project/Tests/EditMode/PvpSupremeTests.cs: dadi pilotati,
    // due schieramenti a specchio, e si arriva alla fase di battaglia schierando in ordine.

    private sealed class QueuedRandom : IRandomSource
    {
        private readonly Queue<int> values;

        public QueuedRandom(IEnumerable<int> values) => this.values = new Queue<int>(values);

        public int NextInclusive(int minimum, int maximum) =>
            values.Count > 0 ? values.Dequeue() : minimum;
    }

    private static List<CombatCard> Loadout(string prefix, HeroClass heroClass)
    {
        var cards = new List<CombatCard>();
        for (int index = 0; index < 9; index++)
            cards.Add(new CombatCard($"{prefix}-{index}", $"{prefix}-{index}", heroClass, 5));
        return cards;
    }

    private static IEnumerable<int> IdentityShuffles()
    {
        for (int player = 0; player < 2; player++)
            for (int index = 8; index >= 1; index--)
                yield return index;
    }

    /// <summary>Iniziative pilotate perche' schieri e apra sempre il giocatore 0.</summary>
    private static IEnumerable<int> DeploymentAndInitiatives()
    {
        foreach (int initiative in new[] { 20, 19, 18 })
        {
            yield return initiative;
            yield return 1;
        }
        foreach (int initiative in new[] { 6, 5, 4 })
        {
            yield return initiative;
            yield return 1;
        }
    }

    private static PvpMatchEngine BattleReady(IReadOnlyCollection<HeroClass> allowedForPlayer0)
    {
        var random = new QueuedRandom(
            IdentityShuffles()
                .Concat(DeploymentAndInitiatives())
                .Concat(Enumerable.Repeat(3, 600)));

        // Il permesso del giocatore 1 resta null (nessun limite): il test guarda solo il
        // giocatore 0, e lasciare aperto l'avversario lo tiene fuori dall'equazione.
        IReadOnlyList<IReadOnlyCollection<HeroClass>> allowed =
            allowedForPlayer0 == null ? null : new[] { allowedForPlayer0, null };

        var engine = new PvpMatchEngine(
            Loadout("p0", HeroClass.Mage),
            Loadout("p1", HeroClass.Mage),
            PvpMatchRules.CreateDefault(),
            random,
            manaRules: null,
            allowedSupremes: allowed);

        engine.Start();
        while (engine.Phase == PvpMatchPhase.Deployment)
            engine.Deploy(engine.ActivePlayer, 0);
        return engine;
    }

    /// <summary>Salta turni finche' il giocatore ha il mana richiesto ed e' il suo turno.</summary>
    private static void BankMana(PvpMatchEngine engine, int player, int required)
    {
        for (int guard = 0; guard < 60; guard++)
        {
            if (engine.Phase != PvpMatchPhase.Battle)
                return;
            if (engine.ManaOf(player) >= required && engine.ActivePlayer == player)
                return;
            engine.Pass(engine.ActivePlayer);
        }
        Assert.Fail($"Non sono riuscito a portare il giocatore {player} a {required} mana.");
    }
}
