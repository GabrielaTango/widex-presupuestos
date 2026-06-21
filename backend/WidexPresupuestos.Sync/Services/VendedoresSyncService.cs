using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Infrastructure;

namespace WidexPresupuestos.Sync.Services;

/// <summary>
/// Sincroniza GVA23 (vendedores) desde Tango hacia MySQL.
/// activo = (INHABILITA = 0); seleccion_widex = 1 sólo si el XML
/// CA_1118_SELECCION_WIDEX = 'S'.
/// </summary>
public sealed class VendedoresSyncService : EntitySyncService
{
    protected override string TablaOrigen => "GVA23";
    protected override string Entidad     => "vendedores";

    private const string Table  = "vendedores";
    private const string KeyCol = "cod_vended";
    private static readonly string[] OtherCols =
        { "nombre_ven", "porc_comis", "seleccion_widex", "activo", "sincronizado_en" };

    private static string SelectFields(string a) => $@"
  LTRIM(RTRIM({a}COD_VENDED)) AS CodVended,
  {a}NOMBRE_VEN               AS NombreVen,
  {a}PORC_COMIS               AS PorcComis,
  CASE WHEN {a}INHABILITA = 0 THEN 1 ELSE 0 END AS Activo,
  CASE WHEN CAST(CAST({a}CAMPOS_ADICIONALES AS nvarchar(max)) AS xml)
      .value('(//CA_1118_SELECCION_WIDEX)[1]','varchar(10)') = 'S' THEN 1 ELSE 0 END AS SeleccionWidex";

    public VendedoresSyncService(
        ITangoConnectionFactory tango, IDbConnectionFactory mysql,
        SyncStateRepository state, SyncLogRepository log,
        IOptions<SyncOptions> opts, ILogger<VendedoresSyncService> logger)
        : base(tango, mysql, state, log, opts, logger) { }

    protected override async Task<int> FullLoadAsync(CancellationToken ct)
    {
        string sql = $"SET QUOTED_IDENTIFIER ON;\nSELECT {SelectFields("")}\nFROM GVA23";

        var (conn, reader) = await OpenTangoReaderAsync(sql, commandTimeout: 120, ct);
        using (conn) using (reader)
        {
            var batch = new List<object?[]>(Opts.BatchSize);
            int total = 0;
            while (await reader.ReadAsync(ct))
            {
                batch.Add(MapRow(reader));
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
  LTRIM(RTRIM(ct.COD_VENDED)) AS CodVendedKey,{SelectFields("g.")}
FROM CHANGETABLE(CHANGES GVA23, @last) ct
LEFT JOIN GVA23 g ON g.COD_VENDED = ct.COD_VENDED";

        var (conn, reader) = await OpenTangoReaderAsync(sql, commandTimeout: 120, ct, ("@last", lastVersion));
        using (conn) using (reader)
        {
            var upserts = new List<object?[]>(Opts.BatchSize);
            var deletes = new List<string>();
            int total = 0;

            int ordOp  = reader.GetOrdinal("Operation");
            int ordKey = reader.GetOrdinal("CodVendedKey");

            while (await reader.ReadAsync(ct))
            {
                if (reader.GetString(ordOp) == "D")
                {
                    deletes.Add(reader.IsDBNull(ordKey) ? string.Empty : reader.GetString(ordKey));
                    total++;
                }
                else
                {
                    upserts.Add(MapRow(reader));
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

    // Fila → valores en el orden [cod_vended, nombre_ven, porc_comis, seleccion_widex, activo, sincronizado_en].
    private static object?[] MapRow(SqlDataReader r)
    {
        string Str(string c) => r.IsDBNull(r.GetOrdinal(c)) ? string.Empty : r.GetString(r.GetOrdinal(c));

        int ordComis = r.GetOrdinal("PorcComis");
        decimal porcComis = r.IsDBNull(ordComis) ? 0m : r.GetDecimal(ordComis);

        int ordSel = r.GetOrdinal("SeleccionWidex");
        int seleccion = !r.IsDBNull(ordSel) && r.GetInt32(ordSel) == 1 ? 1 : 0;

        int ordAct = r.GetOrdinal("Activo");
        int activo = !r.IsDBNull(ordAct) && r.GetInt32(ordAct) == 1 ? 1 : 0;

        return new object?[]
        {
            Str("CodVended"),    // cod_vended (key)
            Str("NombreVen"),    // nombre_ven
            porcComis,           // porc_comis
            seleccion,           // seleccion_widex
            activo,              // activo
            DateTime.UtcNow      // sincronizado_en
        };
    }
}
