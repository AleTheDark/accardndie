namespace AccardND.Server;

/// <summary>
/// Parametri delle stagioni. A fine stagione: snapshot della ladder nella Hall of
/// Fame, soft reset dell'MMR verso il valore di partenza e nuova stagione.
/// </summary>
public sealed class SeasonConfig
{
    /// <summary>Durata di una stagione in giorni (~trimestrale).</summary>
    public int DurationDays { get; set; } = 90;

    /// <summary>
    /// Quota di distanza dal valore di partenza mantenuta al soft reset:
    /// newMmr = start + (old - start) * factor. 0 = azzera tutti allo start, 1 = nessun reset.
    /// </summary>
    public double SoftResetFactor { get; set; } = 0.5;
}
