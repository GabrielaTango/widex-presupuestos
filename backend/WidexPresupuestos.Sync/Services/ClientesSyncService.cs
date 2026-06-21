using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Infrastructure;

namespace WidexPresupuestos.Sync.Services;

/// <summary>
/// Sincroniza GVA14 (clientes y obras sociales) desde Tango hacia MySQL.
/// nro_carnet/obra_social salen del XML CAMPOS_ADICIONALES; nro_lista de la
/// columna directa NRO_LISTA.
/// </summary>
public sealed class ClientesSyncService : EntitySyncService
{
    protected override string TablaOrigen => "GVA14";
    protected override string Entidad     => "clientes";

    private const string Table   = "clientes";
    private const string KeyCol  = "cod_client";
    private static readonly string[] OtherCols =
        { "razon_soci", "cuit", "es_obra_social", "nro_carnet", "obra_social_cod", "nro_lista", "activo", "sincronizado_en" };

    // Campos calculados, idénticos en full y delta (en delta se aplican sobre g.*).
    private static string SelectFields(string a) => $@"
  LTRIM(RTRIM({a}COD_CLIENT)) AS CodClient,
  {a}RAZON_SOCI               AS RazonSoci,
  {a}CUIT                     AS Cuit,
  CASE WHEN {a}GRUPO_EMPR = 'OB.SOC' THEN 1 ELSE 0 END AS EsObraSocial,
  CAST(CAST({a}CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1096_NRO_CARNETAFILIADOO)[1]','varchar(40)') AS NroCarnet,
  CAST(CAST({a}CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1096_OBRA_SOCIAL)[1]','varchar(6)')          AS ObraSocialCod,
  NULLIF({a}NRO_LISTA, 0)     AS NroLista";

    public ClientesSyncService(
        ITangoConnectionFactory tango, IDbConnectionFactory mysql,
        SyncStateRepository state, SyncLogRepository log,
        IOptions<SyncOptions> opts, ILogger<ClientesSyncService> logger)
        : base(tango, mysql, state, log, opts, logger) { }

    protected override async Task<int> FullLoadAsync(CancellationToken ct)
    {
        // SET QUOTED_IDENTIFIER ON es requerido por los métodos XML de SQL Server.
        string sql = $"SET QUOTED_IDENTIFIER ON;\nSELECT {SelectFields("")}\nFROM GVA14";

        var (conn, reader) = await OpenTangoReaderAsync(sql, commandTimeout: 300, ct);
        using (conn) using (reader)
        {
            var batch = new List<object?[]>(Opts.BatchSize);
            int total = 0;
            while (await reader.ReadAsync(ct))
            {
                batch.Add(MapRow(reader, activo: true));
                if (batch.Count >= Opts.BatchSize)
                {
                    total += await UpsertBatchAsync(Table, KeyCol, OtherCols, batch, ct);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
                total += await UpsertBatchAsync(Table, KeyCol, OtherCols, batch, ct);
            return total;
        }
    }

    protected override async Task<int> DeltaLoadAsync(long lastVersion, CancellationToken ct)
    {
        string sql = $@"SET QUOTED_IDENTIFIER ON;
SELECT
  ct.SYS_CHANGE_OPERATION AS Operation,
  LTRIM(RTRIM(ct.COD_CLIENT)) AS CodClientKey,{SelectFields("g.")}
FROM CHANGETABLE(CHANGES GVA14, @last) ct
LEFT JOIN GVA14 g ON g.COD_CLIENT = ct.COD_CLIENT";

        var (conn, reader) = await OpenTangoReaderAsync(sql, commandTimeout: 120, ct, ("@last", lastVersion));
        using (conn) using (reader)
        {
            var upserts = new List<object?[]>(Opts.BatchSize);
            var deletes = new List<string>();
            int total = 0;

            int ordOp  = reader.GetOrdinal("Operation");
            int ordKey = reader.GetOrdinal("CodClientKey");

            while (await reader.ReadAsync(ct))
            {
                if (reader.GetString(ordOp) == "D")
                {
                    deletes.Add(reader.IsDBNull(ordKey) ? string.Empty : reader.GetString(ordKey));
                    total++;
                }
                else
                {
                    upserts.Add(MapRow(reader, activo: true));
                    if (upserts.Count >= Opts.BatchSize)
                    {
                        total += await UpsertBatchAsync(Table, KeyCol, OtherCols, upserts, ct);
                        upserts.Clear();
                    }
                }
            }
            if (upserts.Count > 0)
                total += await UpsertBatchAsync(Table, KeyCol, OtherCols, upserts, ct);
            if (deletes.Count > 0)
                total += await MarkInactiveAsync(Table, KeyCol, deletes, ct);
            return total;
        }
    }

    // Fila → valores en el orden [keyCol, ...OtherCols].
    private static object?[] MapRow(SqlDataReader r, bool activo)
    {
        string Str(string c) => r.IsDBNull(r.GetOrdinal(c)) ? string.Empty : r.GetString(r.GetOrdinal(c));
        string? StrN(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetString(r.GetOrdinal(c));

        var cuit = StrN("Cuit")?.Trim();
        if (cuit?.Length > 20) cuit = cuit[..20];   // CUIT basura (ej. CONSUMIDOR FINAL)

        var obraSoc = StrN("ObraSocialCod")?.Trim();
        if (string.IsNullOrEmpty(obraSoc)) obraSoc = null;

        int ordEs = r.GetOrdinal("EsObraSocial");
        bool esObraSocial = !r.IsDBNull(ordEs) && r.GetInt32(ordEs) == 1;

        int ordNl = r.GetOrdinal("NroLista");
        short? nroLista = r.IsDBNull(ordNl) ? null : Convert.ToInt16(r.GetValue(ordNl));

        return new object?[]
        {
            Str("CodClient"),                          // cod_client (key)
            Str("RazonSoci"),                          // razon_soci
            cuit,                                      // cuit
            esObraSocial ? 1 : 0,                      // es_obra_social
            StrN("NroCarnet"),                         // nro_carnet
            obraSoc,                                   // obra_social_cod
            (object?)nroLista,                         // nro_lista
            activo ? 1 : 0,                            // activo
            DateTime.UtcNow                            // sincronizado_en
        };
    }
}
