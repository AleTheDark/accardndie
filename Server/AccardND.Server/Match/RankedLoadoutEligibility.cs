using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;

namespace AccardND.Server.Match;

/// <summary>
/// Regole ranked basate esclusivamente sugli sblocchi autoritativi dell'account.
///
/// Sono due, e si applicano in due momenti diversi perche' riguardano due cose diverse:
/// <list type="bullet">
/// <item><b>Le classi</b> si controllano quando entri in coda, con
/// <see cref="GetFailures"/>: il loadout e' gia' tutto li', quindi tanto vale dirlo subito
/// invece di far cominciare una partita che poi si rompe.</item>
/// <item><b>Le supreme</b> si controllano quando le usi, dentro il motore: una supreme non
/// e' nel loadout, e' un'azione. Schierare un Guerriero senza possederne la supreme e'
/// legittimo — semplicemente, in classificata, non la lancia.
/// <see cref="UnlockedSupremesOf"/> e' quello che il motore riceve.</item>
/// </list>
/// In amichevole non si applica nessuna delle due: li' si prova tutto, ed e' il posto dove
/// capire se una classe ti piace prima di spendere il miele al Santuario.
/// </summary>
public static class RankedLoadoutEligibility
{
    /// <summary>
    /// Le classi di cui l'account puo' usare la supreme in classificata.
    ///
    /// Gli id salvati sono quelli del catalogo del Santuario, nella forma
    /// <c>ability-&lt;classe&gt;-2</c>: qui si torna alla classe. Un id che non si sa
    /// leggere viene ignorato invece di far saltare la partita — il caso vero e' un
    /// catalogo che cambia nomi, e in quel caso e' meglio una supreme in meno di un match
    /// che non parte.
    /// </summary>
    public static IReadOnlyCollection<HeroClass> UnlockedSupremesOf(SinglePlayerProgressData progress)
    {
        var unlocked = new HashSet<HeroClass>();
        foreach (string id in progress?.unlockedSecondAbilities ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            // "ability-warrior-2" -> "warrior"
            string[] parts = id.Split('-');
            if (parts.Length >= 3 && Enum.TryParse(parts[1], ignoreCase: true, out HeroClass heroClass))
                unlocked.Add(heroClass);
        }
        return unlocked;
    }

    public static IReadOnlyList<string> GetFailures(PvpLoadout loadout, SinglePlayerProgressData progress)
    {
        var ownedClasses = new HashSet<string>(
            progress?.unlockedClasses ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (HeroClass heroClass in loadout.Cards.Select(card => card.HeroClass).Distinct())
        {
            string classId = heroClass.ToString().ToLowerInvariant();
            if (!ownedClasses.Contains(classId))
                failures.Add($"{DisplayName(heroClass)}: classe non sbloccata");
        }

        return failures;
    }

    private static string DisplayName(HeroClass heroClass) => heroClass switch
    {
        HeroClass.Assassin => "Assassino",
        HeroClass.Warrior => "Guerriero",
        HeroClass.Mage => "Mago",
        HeroClass.Paladin => "Paladino",
        HeroClass.Rogue => "Ladro",
        HeroClass.Hunter => "Cacciatore",
        HeroClass.Barbarian => "Barbaro",
        HeroClass.Necromancer => "Negromante",
        HeroClass.Priest => "Sacerdote",
        _ => heroClass.ToString()
    };
}
