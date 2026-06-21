using Microsoft.Extensions.Options;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Services;

namespace WidexPresupuestos.Sync;

/// <summary>
/// Worker principal del Sync. Loop periódico por PeriodicTimer.
/// Recorre todas las entidades registradas; un error en una NO frena a las otras
/// ni tumba el worker (se loguea y se espera el siguiente tick).
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly IReadOnlyList<EntitySyncService> _entidades;
    private readonly SyncOptions     _opts;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IEnumerable<EntitySyncService> entidades,
        IOptions<SyncOptions>          opts,
        ILogger<Worker>                logger)
    {
        _entidades = entidades.ToList();
        _opts      = opts.Value;
        _logger    = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync worker iniciado. {N} entidades, intervalo {I}s.",
            _entidades.Count, _opts.IntervalSeconds);

        await RunCycleAsync(stoppingToken);   // primera corrida inmediata

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opts.IntervalSeconds));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando ciclo de sync.");
        foreach (var entidad in _entidades)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await entidad.RunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;   // shutdown limpio
            }
            catch (Exception ex)
            {
                // Ya quedó registrado en sync_log por el servicio; logueamos y seguimos
                // con la próxima entidad para no perder un ciclo entero por una sola.
                _logger.LogError(ex, "[{E}] Falló en este ciclo. Continúa con las demás.", entidad.Nombre);
            }
        }
    }
}
