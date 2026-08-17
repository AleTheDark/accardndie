using AccardND.GameCore;
using AccardND.NetProtocol;

namespace AccardND.LoadTest;

/// <summary>
/// Il loadout dei bot: le nove carte Guerriero, una per valore da 2 a 10.
///
/// Sono tutte definizioni distinte (il validatore vieta i doppioni), costano
/// 2+3+...+10 = 54 dei 60 punti di budget con il dado base da 3 (gratis) e nessun dado
/// in bisaccia, e usano una sola classe: il Guerriero, l'unica che il tutorial regala.
/// Cosi' lo stesso loadout passa sia nelle stanze private sia nella coda ranked, dove
/// il server pretende che le classi siano sbloccate davvero.
/// </summary>
public static class Loadouts
{
    private static readonly (string Id, int Value)[] WarriorLadder =
    {
        ("2-goblin-warrior", 2),
        ("3-skeleton-warrior", 3),
        ("4-animal-warrior", 4),
        ("5-darkelf-warrior", 5),
        ("6-chimera-warrior", 6),
        ("7-whitealien-warrior", 7),
        ("8-spirit-warrior", 8),
        ("9-faceless-warrior", 9),
        ("10-champion-warrior", 10)
    };

    public static PvpLoadoutDto Warrior()
    {
        var cards = new LoadoutCardDto[WarriorLadder.Length];
        for (int index = 0; index < WarriorLadder.Length; index++)
        {
            (string id, int value) = WarriorLadder[index];
            cards[index] = new LoadoutCardDto
            {
                definitionId = id,
                value = value,
                heroClass = (int)HeroClass.Warrior
            };
        }

        return new PvpLoadoutDto
        {
            cards = cards,
            baseDieSides = 3,
            bagDiceSides = Array.Empty<int>()
        };
    }
}
