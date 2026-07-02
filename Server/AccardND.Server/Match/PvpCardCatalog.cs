using System.Text.Json;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;

namespace AccardND.Server.Match;

/// <summary>
/// Catalogo autoritativo delle carte schierabili (esportato dall'editor Unity).
/// Impedisce ai client di dichiarare valori o classi falsi nel loadout.
/// </summary>
public sealed class PvpCardCatalog
{
    private sealed record CatalogFile(List<CatalogEntry> Cards);
    private sealed record CatalogEntry(string Id, int Value, int HeroClass);

    private readonly Dictionary<string, CatalogEntry> entriesById = new();

    public bool IsEnforced => entriesById.Count > 0;
    public int Count => entriesById.Count;

    public static PvpCardCatalog Load(string path, ILogger logger)
    {
        var catalog = new PvpCardCatalog();
        if (!File.Exists(path))
        {
            logger.LogWarning(
                "Catalogo carte non trovato in {Path}: la validazione carte è DISATTIVATA.", path);
            return catalog;
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CatalogFile file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), options);
        if (file?.Cards != null)
        {
            foreach (CatalogEntry entry in file.Cards)
                catalog.entriesById[entry.Id] = entry;
        }
        logger.LogInformation("Catalogo carte PvP: {Count} carte schierabili.", catalog.Count);
        return catalog;
    }

    /// <summary>Verifica che ogni carta dichiarata esista e abbia valore e classe corretti.</summary>
    public bool TryValidate(PvpLoadout loadout, out string error)
    {
        if (!IsEnforced)
        {
            error = null;
            return true;
        }

        foreach (PvpLoadoutCard card in loadout.Cards)
        {
            if (!entriesById.TryGetValue(card.DefinitionId, out CatalogEntry entry))
            {
                error = $"La carta '{card.DefinitionId}' non esiste nel catalogo.";
                return false;
            }
            if (card.Value != entry.Value)
            {
                error = $"La carta '{card.DefinitionId}' vale {entry.Value}, non {card.Value}.";
                return false;
            }
            if ((int)card.HeroClass != entry.HeroClass)
            {
                error = $"La carta '{card.DefinitionId}' è {(HeroClass)entry.HeroClass}, non {card.HeroClass}.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
