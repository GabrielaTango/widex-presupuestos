namespace WidexPresupuestos.Sync;

/// <summary>
/// Esqueleto del Sync Worker. Fase 1: sólo loguea inicio.
/// Los jobs de sincronización ERP↔app se implementarán en fases posteriores.
/// Este es el ÚNICO proceso que tiene acceso a la conexión Tango (MSSQL).
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync worker iniciado. Jobs de sincronización ERP pendientes de implementación.");
        // Pendiente Fase 2: jobs ERP→app (maestros) y app→ERP (pedidos).
        await Task.CompletedTask;
    }
}
