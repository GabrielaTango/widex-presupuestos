using Microsoft.Extensions.Options;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Services;

namespace WidexPresupuestos.Sync;

/// <summary>
/// Worker principal del Sync. Loop periódico por PeriodicTimer.
/// Un error en una entidad NO tumba el worker; se loguea y se espera el siguiente tick.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ClientesSyncService _clientes;
    private readonly SyncOptions         _opts;
    private readonly ILogger<Worker>     _logger;

    public Worker(
        ClientesSyncService  clientes,
        IOptions<SyncOptions> opts,
        ILogger<Worker>      logger)
    {
        _clientes = clientes;
        _opts     = opts.Value;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync worker iniciado. Intervalo: {I}s.", _opts.IntervalSeconds);

        // Primera corrida inmediata
        await RunCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opts.IntervalSeconds));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando ciclo de sync.");
        try
        {
            await _clientes.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown limpio, no es un error
        }
        catch (Exception ex)
        {
            // El error ya fue registrado en sync_log por el servicio.
            // Logueamos aquí también para que sea visible en el host y seguimos.
            _logger.LogError(ex, "Ciclo de sync terminó con error. Se reintentará en {I}s.", _opts.IntervalSeconds);
        }
    }
}
