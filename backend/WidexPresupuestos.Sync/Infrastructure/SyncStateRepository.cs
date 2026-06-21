using Dapper;
using WidexPresupuestos.Shared.Infrastructure;

namespace WidexPresupuestos.Sync.Infrastructure;

/// <summary>
/// Acceso a sync_state: persiste la última versión de Change Tracking consumida
/// y la fecha del último full load, por tabla-origen del ERP.
/// </summary>
public sealed class SyncStateRepository
{
    private readonly IDbConnectionFactory _mysql;

    public SyncStateRepository(IDbConnectionFactory mysql) => _mysql = mysql;

    /// <summary>
    /// Devuelve (last_change_version, last_full_load) para la tabla dada.
    /// Si la fila no existe la crea con NULLs y devuelve (null, null).
    /// </summary>
    public async Task<(long? lastChangeVersion, DateTime? lastFullLoad)> GetAsync(string tablaOrigen)
    {
        using var conn = _mysql.CreateConnection();
        conn.Open();

        // Asegura la fila de forma idempotente. No se puede distinguir "no hay fila"
        // de "fila con ambos campos NULL" mirando el SELECT (ambos son el default del
        // tuple), así que garantizamos su existencia con INSERT IGNORE y luego leemos.
        const string ensureSql = @"
            INSERT IGNORE INTO sync_state (tabla_origen, last_change_version, last_full_load, updated_at)
            VALUES (@tabla, NULL, NULL, UTC_TIMESTAMP())";
        await conn.ExecuteAsync(ensureSql, new { tabla = tablaOrigen });

        const string selectSql = @"
            SELECT last_change_version, last_full_load
            FROM sync_state
            WHERE tabla_origen = @tabla";

        var row = await conn.QuerySingleAsync<(long? v, DateTime? f)>(
            selectSql, new { tabla = tablaOrigen });

        return (row.v, row.f);
    }

    /// <summary>Guarda la versión de CT consumida (y opcionalmente la fecha de full load).</summary>
    public async Task SaveAsync(string tablaOrigen, long? changeVersion, DateTime? fullLoadAt = null)
    {
        using var conn = _mysql.CreateConnection();
        conn.Open();

        const string sql = @"
            UPDATE sync_state
            SET last_change_version = @ver,
                last_full_load      = COALESCE(@full, last_full_load),
                updated_at          = UTC_TIMESTAMP()
            WHERE tabla_origen = @tabla";

        await conn.ExecuteAsync(sql, new
        {
            tabla = tablaOrigen,
            ver   = changeVersion,
            full  = fullLoadAt
        });
    }
}
