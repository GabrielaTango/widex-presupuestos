using Dapper;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Repositories;

namespace WidexPresupuestos.Api.Repositories;

public sealed class ClienteRepository : IClienteRepository
{
    private readonly IDbConnectionFactory _db;

    public ClienteRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Cliente>> BuscarPacientesAsync(string? busqueda)
    {
        using var conn = _db.CreateConnection();

        var sql = @"SELECT cod_client, razon_soci, cuit, nro_carnet, obra_social_cod AS obra_social
                    FROM clientes
                    WHERE es_obra_social = 0 AND activo = 1";

        if (!string.IsNullOrWhiteSpace(busqueda))
            sql += " AND (cod_client LIKE @Busqueda OR razon_soci LIKE @Busqueda)";

        sql += " ORDER BY razon_soci LIMIT 100";

        // Escapar %, _ y \ para que el usuario no altere la semántica del LIKE.
        var term = $"%{EscapeLike(busqueda)}%";
        return await conn.QueryAsync<Cliente>(sql, new { Busqueda = term });
    }

    private static string EscapeLike(string? input) =>
        (input ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

    public async Task<IEnumerable<Cliente>> GetObrasSocialesAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Cliente>(
            @"SELECT cod_client, razon_soci, cuit
              FROM clientes
              WHERE es_obra_social = 1 AND activo = 1
              ORDER BY razon_soci");
    }
}
