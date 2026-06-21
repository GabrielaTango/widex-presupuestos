using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Infrastructure;

namespace WidexPresupuestos.Sync.Services;

/// <summary>
/// Base de los servicios de sync ERP→app. Concentra la maquinaria común a todas
/// las entidades: orquestación de la corrida (sync_log + sync_state), decisión
/// full-load vs delta por Change Tracking, y helpers de upsert/baja por lotes
/// en MySQL. Cada entidad sólo implementa su lectura de Tango (full y delta).
/// </summary>
public abstract class EntitySyncService
{
    protected readonly ITangoConnectionFactory Tango;
    protected readonly IDbConnectionFactory    Mysql;
    protected readonly SyncOptions             Opts;
    private readonly SyncStateRepository _state;
    private readonly SyncLogRepository   _log;
    private readonly ILogger             _logger;

    protected EntitySyncService(
        ITangoConnectionFactory tango,
        IDbConnectionFactory    mysql,
        SyncStateRepository     state,
        SyncLogRepository       log,
        IOptions<SyncOptions>   opts,
        ILogger                 logger)
    {
        Tango   = tango;
        Mysql   = mysql;
        _state  = state;
        _log    = log;
        Opts    = opts.Value;
        _logger = logger;
    }

    /// <summary>Tabla origen en el ERP (p. ej. "GVA14"). Define la clave de sync_state.</summary>
    protected abstract string TablaOrigen { get; }
    /// <summary>Nombre de la entidad para logs y sync_log (p. ej. "clientes").</summary>
    protected abstract string Entidad { get; }

    /// <summary>Nombre público de la entidad (para el Worker).</summary>
    public string Nombre => Entidad;

    /// <summary>Carga completa desde Tango. Devuelve filas procesadas.</summary>
    protected abstract Task<int> FullLoadAsync(CancellationToken ct);
    /// <summary>Carga incremental (delta) desde una versión de CT. Devuelve filas procesadas.</summary>
    protected abstract Task<int> DeltaLoadAsync(long lastVersion, CancellationToken ct);

    /// <summary>Ejecuta una corrida (full o delta), registrando en sync_log/sync_state.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        var logId  = await _log.InsertInicioAsync(Entidad, inicio);
        var (lastVersion, _) = await _state.GetAsync(TablaOrigen);

        int total = 0;
        try
        {
            long? currentVersion = await GetCurrentVersionAsync(ct);

            bool doFull;
            if (currentVersion is null)
            {
                _logger.LogInformation("[{E}] Change Tracking no activo en Tango. Full load.", Entidad);
                doFull = true;
            }
            else if (lastVersion is null)
            {
                _logger.LogInformation("[{E}] Sin versión previa. Full load.", Entidad);
                doFull = true;
            }
            else
            {
                long minValid = await GetMinValidVersionAsync(ct);
                doFull = lastVersion < minValid;
                if (doFull)
                    _logger.LogWarning("[{E}] Versión {V} expiró (min válida {M}). Full load.", Entidad, lastVersion, minValid);
            }

            total = doFull
                ? await FullLoadAsync(ct)
                : await DeltaLoadAsync(lastVersion!.Value, ct);

            DateTime? fullAt = doFull ? DateTime.UtcNow : null;
            await _state.SaveAsync(TablaOrigen, currentVersion, fullAt);

            await _log.UpdateFinAsync(logId, DateTime.UtcNow, total, SyncEstado.Ok, null);
            _logger.LogInformation("[{E}] Corrida OK. {N} registros en {S:F1}s.",
                Entidad, total, (DateTime.UtcNow - inicio).TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{E}] Error en sync.", Entidad);
            await _log.UpdateFinAsync(logId, DateTime.UtcNow, total, SyncEstado.Error, ex.Message);
            throw;
        }

        return total;
    }

    // ── Change Tracking ────────────────────────────────────────────────────

    private async Task<long?> GetCurrentVersionAsync(CancellationToken ct)
    {
        using var conn = Tango.CreateConnection();
        conn.Open();
        // NULL si CT no está habilitado en la base.
        return await conn.ExecuteScalarAsync<long?>(
            new CommandDefinition("SELECT CHANGE_TRACKING_CURRENT_VERSION()", cancellationToken: ct));
    }

    private async Task<long> GetMinValidVersionAsync(CancellationToken ct)
    {
        using var conn = Tango.CreateConnection();
        conn.Open();
        var v = await conn.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(@t))",
            new { t = TablaOrigen }, cancellationToken: ct));
        return v ?? 0;
    }

    // ── Helpers de escritura en MySQL (compartidos por todas las entidades) ──

    /// <summary>
    /// Abre un reader forward-only sobre Tango. El SQL debe empezar con
    /// `SET QUOTED_IDENTIFIER ON;` si usa métodos XML. No usa SequentialAccess:
    /// las columnas son chicas (el XML se reduce a varchars en el server) y así
    /// el mapeo puede leer columnas en cualquier orden sin restricciones.
    /// </summary>
    protected async Task<(SqlConnection conn, SqlDataReader reader)> OpenTangoReaderAsync(
        string sql, int commandTimeout, CancellationToken ct, params (string name, object value)[] parameters)
    {
        var conn = (SqlConnection)Tango.CreateConnection();
        await conn.OpenAsync(ct);
        var cmd = new SqlCommand(sql, conn) { CommandTimeout = commandTimeout };
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        var reader = await cmd.ExecuteReaderAsync(ct);
        return (conn, reader);
    }

    /// <summary>
    /// Upsert por lotes en MySQL: INSERT multi-fila + ON DUPLICATE KEY UPDATE de
    /// todas las columnas salvo la clave de negocio. `rows` lleva los valores en
    /// el orden [keyCol, ...otherCols].
    /// </summary>
    protected async Task<int> UpsertBatchAsync(
        string table, string keyCol, IReadOnlyList<string> otherCols,
        IReadOnlyList<object?[]> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return 0;

        var cols = new List<string> { keyCol };
        cols.AddRange(otherCols);

        var sb = new StringBuilder();
        var prms = new DynamicParameters();
        sb.Append("INSERT INTO ").Append(table).Append(" (")
          .Append(string.Join(", ", cols)).Append(") VALUES ");

        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(');
            for (int j = 0; j < cols.Count; j++)
            {
                var p = $"p{i}_{j}";
                if (j > 0) sb.Append(',');
                sb.Append('@').Append(p);
                prms.Add(p, rows[i][j]);
            }
            sb.Append(')');
        }

        sb.Append(" ON DUPLICATE KEY UPDATE ")
          .Append(string.Join(", ", otherCols.Select(c => $"{c}=VALUES({c})")));

        using var conn = (MySqlConnection)Mysql.CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sb.ToString(), prms, cancellationToken: ct));
        return rows.Count;
    }

    /// <summary>Marca filas como inactivas (activo=0) por clave de negocio.</summary>
    protected async Task<int> MarkInactiveAsync(
        string table, string keyCol, IReadOnlyList<string> keys, CancellationToken ct)
    {
        if (keys.Count == 0) return 0;

        var prms = new DynamicParameters();
        var inClause = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0) inClause.Append(',');
            inClause.Append("@k").Append(i);
            prms.Add($"k{i}", keys[i]);
        }

        var sql = $"UPDATE {table} SET activo = 0, sincronizado_en = UTC_TIMESTAMP() " +
                  $"WHERE {keyCol} IN ({inClause})";

        using var conn = (MySqlConnection)Mysql.CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, prms, cancellationToken: ct));
        return keys.Count;
    }
}
