namespace WidexPresupuestos.Sync.Config;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>Intervalo entre corridas del sync (segundos). Default: 300.</summary>
    public int IntervalSeconds { get; init; } = 300;

    /// <summary>Filas por lote en upserts MySQL. Default: 1000.</summary>
    public int BatchSize { get; init; } = 1000;
}
