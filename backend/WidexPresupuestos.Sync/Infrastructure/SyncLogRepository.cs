using Dapper;
using WidexPresupuestos.Shared.Infrastructure;

namespace WidexPresupuestos.Sync.Infrastructure;

public enum SyncEstado { Ok, Error, Parcial }

/// <summary>
/// Registra cada corrida de sync en sync_log.
/// </summary>
public sealed class SyncLogRepository
{
    private readonly IDbConnectionFactory _mysql;

    public SyncLogRepository(IDbConnectionFactory mysql) => _mysql = mysql;

    public async Task<long> InsertInicioAsync(string entidad, DateTime inicio)
    {
        using var conn = _mysql.CreateConnection();
        conn.Open();

        const string sql = @"
            INSERT INTO sync_log (entidad, direccion, inicio, fin, registros, estado, mensaje)
            VALUES (@entidad, 'erp_to_app', @inicio, NULL, 0, 'ok', NULL);
            SELECT LAST_INSERT_ID();";

        return await conn.ExecuteScalarAsync<long>(sql, new { entidad, inicio });
    }

    public async Task UpdateFinAsync(long id, DateTime fin, int registros, SyncEstado estado, string? mensaje)
    {
        using var conn = _mysql.CreateConnection();
        conn.Open();

        const string sql = @"
            UPDATE sync_log
            SET fin       = @fin,
                registros = @registros,
                estado    = @estado,
                mensaje   = @mensaje
            WHERE id = @id";

        await conn.ExecuteAsync(sql, new
        {
            id,
            fin,
            registros,
            estado  = estado.ToString().ToLowerInvariant(),
            mensaje = mensaje?[..Math.Min(mensaje.Length, 1000)]
        });
    }
}
