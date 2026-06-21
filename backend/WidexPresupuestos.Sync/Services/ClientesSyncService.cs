using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Infrastructure;

namespace WidexPresupuestos.Sync.Services;

/// <summary>
/// Sincroniza GVA14 (clientes y obras sociales) desde Tango hacia MySQL.
/// Estrategia: full load cuando CT no está disponible o la versión expiró;
/// delta incremental cuando hay una versión válida.
/// </summary>
public sealed class ClientesSyncService
{
    private const string TablaOrigen = "GVA14";
    private const string Entidad     = "clientes";

    private readonly ITangoConnectionFactory _tango;
    private readonly IDbConnectionFactory    _mysql;
    private readonly SyncStateRepository     _state;
    private readonly SyncLogRepository       _log;
    private readonly SyncOptions             _opts;
    private readonly ILogger<ClientesSyncService> _logger;

    public ClientesSyncService(
        ITangoConnectionFactory      tango,
        IDbConnectionFactory         mysql,
        SyncStateRepository          state,
        SyncLogRepository            log,
        IOptions<SyncOptions>        opts,
        ILogger<ClientesSyncService> logger)
    {
        _tango  = tango;
        _mysql  = mysql;
        _state  = state;
        _log    = log;
        _opts   = opts.Value;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta una corrida completa (full o delta) y retorna el nro de registros procesados.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var inicio  = DateTime.UtcNow;
        var logId   = await _log.InsertInicioAsync(Entidad, inicio);
        var (lastVersion, _) = await _state.GetAsync(TablaOrigen);

        int total = 0;
        try
        {
            long? currentVersion = await GetCurrentVersionAsync(ct);

            bool doFull;
            if (currentVersion is null)
            {
                // CT no está habilitado: full load, no guardar versión
                _logger.LogInformation("[{E}] Change Tracking no activo en Tango. Ejecutando full load.", Entidad);
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

            if (doFull)
                total = await FullLoadAsync(ct);
            else
                total = await DeltaLoadAsync(lastVersion!.Value, ct);

            // Guardar nueva versión (puede ser null si CT no está activo)
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

    // -----------------------------------------------------------------------
    // Change Tracking helpers
    // -----------------------------------------------------------------------

    private async Task<long?> GetCurrentVersionAsync(CancellationToken ct)
    {
        using var conn = _tango.CreateConnection();
        conn.Open();
        // Puede devolver NULL si CT no está habilitado en la base
        return await conn.ExecuteScalarAsync<long?>(
            new CommandDefinition("SELECT CHANGE_TRACKING_CURRENT_VERSION()", cancellationToken: ct));
    }

    private async Task<long> GetMinValidVersionAsync(CancellationToken ct)
    {
        using var conn = _tango.CreateConnection();
        conn.Open();
        var v = await conn.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                "SELECT CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID('GVA14'))",
                cancellationToken: ct));
        return v ?? 0;
    }

    // -----------------------------------------------------------------------
    // Full load
    // -----------------------------------------------------------------------

    private async Task<int> FullLoadAsync(CancellationToken ct)
    {
        _logger.LogInformation("[{E}] Iniciando full load (lotes de {B}).", Entidad, _opts.BatchSize);

        // SET QUOTED_IDENTIFIER ON es requerido por los métodos XML de SQL Server
        const string sql = @"
SET QUOTED_IDENTIFIER ON;
SELECT
  LTRIM(RTRIM(COD_CLIENT)) AS CodClient,
  RAZON_SOCI               AS RazonSoci,
  CUIT                     AS Cuit,
  CASE WHEN GRUPO_EMPR = 'OB.SOC' THEN 1 ELSE 0 END AS EsObraSocial,
  CAST(CAST(CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1096_NRO_CARNETAFILIADOO)[1]','varchar(40)') AS NroCarnet,
  CAST(CAST(CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1096_OBRA_SOCIAL)[1]','varchar(6)')          AS ObraSocialCod,
  NULLIF(NRO_LISTA, 0)     AS NroLista
FROM GVA14";

        using var tangoConn = (Microsoft.Data.SqlClient.SqlConnection)_tango.CreateConnection();
        await tangoConn.OpenAsync(ct);

        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, tangoConn)
        {
            CommandTimeout = 300  // 5 min para el full de 136k filas
        };

        using var reader = await cmd.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess, ct);

        var batch  = new List<ClienteRow>(_opts.BatchSize);
        int total  = 0;

        while (await reader.ReadAsync(ct))
        {
            batch.Add(ReadRow(reader));

            if (batch.Count >= _opts.BatchSize)
            {
                total += await UpsertBatchAsync(batch, activo: true, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            total += await UpsertBatchAsync(batch, activo: true, ct);

        _logger.LogInformation("[{E}] Full load completado: {N} filas.", Entidad, total);
        return total;
    }

    // -----------------------------------------------------------------------
    // Delta load
    // -----------------------------------------------------------------------

    private async Task<int> DeltaLoadAsync(long lastVersion, CancellationToken ct)
    {
        _logger.LogInformation("[{E}] Delta desde versión {V}.", Entidad, lastVersion);

        const string sql = @"
SET QUOTED_IDENTIFIER ON;
SELECT
  ct.SYS_CHANGE_OPERATION  AS Operation,
  LTRIM(RTRIM(ct.COD_CLIENT)) AS CodClient,
  g.RAZON_SOCI             AS RazonSoci,
  g.CUIT                   AS Cuit,
  CASE WHEN g.GRUPO_EMPR = 'OB.SOC' THEN 1 ELSE 0 END AS EsObraSocial,
  CAST(CAST(g.CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1096_NRO_CARNETAFILIADOO)[1]','varchar(40)') AS NroCarnet,
  CAST(CAST(g.CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1096_OBRA_SOCIAL)[1]','varchar(6)')          AS ObraSocialCod,
  NULLIF(g.NRO_LISTA, 0)   AS NroLista
FROM CHANGETABLE(CHANGES GVA14, @last) ct
LEFT JOIN GVA14 g ON g.COD_CLIENT = ct.COD_CLIENT";

        using var tangoConn = (Microsoft.Data.SqlClient.SqlConnection)_tango.CreateConnection();
        await tangoConn.OpenAsync(ct);

        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, tangoConn)
        {
            CommandTimeout = 120
        };
        cmd.Parameters.AddWithValue("@last", lastVersion);

        using var reader = await cmd.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess, ct);

        var upserts  = new List<ClienteRow>(_opts.BatchSize);
        var deletes  = new List<string>();   // cod_client a marcar inactivos
        int total    = 0;

        while (await reader.ReadAsync(ct))
        {
            var op = reader.GetString(reader.GetOrdinal("Operation"));

            if (op == "D")
            {
                deletes.Add(reader.IsDBNull(reader.GetOrdinal("CodClient"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("CodClient")));
                total++;
            }
            else
            {
                upserts.Add(ReadRow(reader));
                if (upserts.Count >= _opts.BatchSize)
                {
                    total += await UpsertBatchAsync(upserts, activo: true, ct);
                    upserts.Clear();
                }
            }
        }

        if (upserts.Count > 0)
            total += await UpsertBatchAsync(upserts, activo: true, ct);

        if (deletes.Count > 0)
            total += await MarkInactiveAsync(deletes, ct);

        _logger.LogInformation("[{E}] Delta completado: {N} registros.", Entidad, total);
        return total;
    }

    // -----------------------------------------------------------------------
    // Upsert por lotes (MySQL multi-row parametrizado)
    // -----------------------------------------------------------------------

    private async Task<int> UpsertBatchAsync(
        List<ClienteRow> batch, bool activo, CancellationToken ct)
    {
        if (batch.Count == 0) return 0;

        // Construcción dinámica del multi-row VALUES con parámetros nombrados
        var sb     = new StringBuilder();
        var @params = new DynamicParameters();
        var now    = DateTime.UtcNow;

        sb.AppendLine(@"
INSERT INTO clientes
  (cod_client, razon_soci, cuit, es_obra_social, nro_carnet, obra_social_cod, nro_lista, activo, sincronizado_en)
VALUES");

        for (int i = 0; i < batch.Count; i++)
        {
            var r = batch[i];
            if (i > 0) sb.AppendLine(",");
            sb.Append($"(@c{i},@r{i},@cu{i},@eo{i},@nc{i},@oc{i},@nl{i},@a{i},@s{i})");

            var cuit = r.Cuit?.Trim();
            if (cuit?.Length > 20) cuit = cuit[..20];

            var obraSocCod = r.ObraSocialCod?.Trim();
            if (string.IsNullOrEmpty(obraSocCod)) obraSocCod = null;

            @params.Add($"c{i}",  r.CodClient);
            @params.Add($"r{i}",  r.RazonSoci ?? string.Empty);
            @params.Add($"cu{i}", cuit);
            @params.Add($"eo{i}", r.EsObraSocial ? 1 : 0);
            @params.Add($"nc{i}", r.NroCarnet);
            @params.Add($"oc{i}", obraSocCod);
            @params.Add($"nl{i}", r.NroLista);
            @params.Add($"a{i}",  activo ? 1 : 0);
            @params.Add($"s{i}",  now);
        }

        sb.AppendLine(@"
ON DUPLICATE KEY UPDATE
  razon_soci      = VALUES(razon_soci),
  cuit            = VALUES(cuit),
  es_obra_social  = VALUES(es_obra_social),
  nro_carnet      = VALUES(nro_carnet),
  obra_social_cod = VALUES(obra_social_cod),
  nro_lista       = VALUES(nro_lista),
  activo          = VALUES(activo),
  sincronizado_en = VALUES(sincronizado_en);");

        using var conn = (MySqlConnection)_mysql.CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            new CommandDefinition(sb.ToString(), @params, cancellationToken: ct));

        return batch.Count;
    }

    private async Task<int> MarkInactiveAsync(List<string> codClients, CancellationToken ct)
    {
        if (codClients.Count == 0) return 0;

        // Construir IN parametrizado
        var @params = new DynamicParameters();
        var inClause = new StringBuilder();
        for (int i = 0; i < codClients.Count; i++)
        {
            if (i > 0) inClause.Append(',');
            inClause.Append($"@dc{i}");
            @params.Add($"dc{i}", codClients[i]);
        }

        var sql = $"UPDATE clientes SET activo = 0, sincronizado_en = UTC_TIMESTAMP() WHERE cod_client IN ({inClause})";

        using var conn = _mysql.CreateConnection();
        conn.Open();
        await conn.ExecuteAsync(new CommandDefinition(sql, @params, cancellationToken: ct));
        return codClients.Count;
    }

    // -----------------------------------------------------------------------
    // Mapeo de fila SQL Server → ClienteRow
    // -----------------------------------------------------------------------

    private static ClienteRow ReadRow(System.Data.IDataReader r)
    {
        int ordCodClient     = r.GetOrdinal("CodClient");
        int ordRazonSoci     = r.GetOrdinal("RazonSoci");
        int ordCuit          = r.GetOrdinal("Cuit");
        int ordEsObraSocial  = r.GetOrdinal("EsObraSocial");
        int ordNroCarnet     = r.GetOrdinal("NroCarnet");
        int ordObraSocialCod = r.GetOrdinal("ObraSocialCod");
        int ordNroLista      = r.GetOrdinal("NroLista");

        return new ClienteRow(
            CodClient:     r.IsDBNull(ordCodClient)     ? string.Empty : r.GetString(ordCodClient),
            RazonSoci:     r.IsDBNull(ordRazonSoci)     ? string.Empty : r.GetString(ordRazonSoci),
            Cuit:          r.IsDBNull(ordCuit)          ? null         : r.GetString(ordCuit),
            EsObraSocial:  !r.IsDBNull(ordEsObraSocial) && r.GetInt32(ordEsObraSocial) == 1,
            NroCarnet:     r.IsDBNull(ordNroCarnet)     ? null         : r.GetString(ordNroCarnet),
            ObraSocialCod: r.IsDBNull(ordObraSocialCod) ? null         : r.GetString(ordObraSocialCod),
            NroLista:      r.IsDBNull(ordNroLista)      ? null         : (short?)Convert.ToInt16(r.GetValue(ordNroLista))
        );
    }

    // DTO interno; privado y anidado para evitar fuga del tipo fuera del servicio
    private sealed record ClienteRow(
        string  CodClient,
        string  RazonSoci,
        string? Cuit,
        bool    EsObraSocial,
        string? NroCarnet,
        string? ObraSocialCod,
        short?  NroLista);
}
