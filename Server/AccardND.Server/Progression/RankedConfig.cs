namespace AccardND.Server;

/// <summary>
/// Parametri del ranking. I tier sono derivati dall'MMR nascosto: ogni tier è
/// diviso in <see cref="DivisionsPerTier"/> divisioni larghe <see cref="DivisionWidth"/>
/// punti MMR; i punti lega (0-100) sono la posizione dentro la divisione corrente.
/// Rinominare i tier o spostare le soglie è solo una modifica di config.
/// </summary>
public sealed class RankedConfig
{
    /// <summary>MMR di partenza (nascosto al giocatore).</summary>
    public int StartMmr { get; set; } = 1000;

    /// <summary>Partite di piazzamento prima di mostrare il tier.</summary>
    public int PlacementMatches { get; set; } = 5;

    /// <summary>K-factor Elo durante il piazzamento (oscillazioni ampie).</summary>
    public int PlacementK { get; set; } = 40;

    /// <summary>K-factor Elo a regime.</summary>
    public int StandardK { get; set; } = 24;

    /// <summary>Ampiezza in MMR di una divisione.</summary>
    public int DivisionWidth { get; set; } = 100;

    /// <summary>MMR alla base assoluta (Nabbo, divisione più bassa).</summary>
    public int TierFloor { get; set; } = 800;

    public int DivisionsPerTier { get; set; } = 4;

    /// <summary>Tier dal più basso al più alto.</summary>
    public string[] Tiers { get; set; } =
        { "Nabbo", "Apprendista", "Esperto", "Divino", "Onnipotente" };
}
