using Dapper;
using Microsoft.Data.SqlClient;
using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public class ClienteRepository : IClienteRepository
{
    private readonly string _connectionString;

    public ClienteRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TangoConnection")
            ?? throw new InvalidOperationException("TangoConnection not configured");
    }

    public async Task<IEnumerable<Cliente>> BuscarPacientesAsync(string? busqueda)
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT TOP 100
                COD_CLIENT AS CodClient,
                RAZON_SOCI AS RazonSoci,
                CUIT,
                ISNULL(CAMPOS_ADICIONALES.value('(/CAMPOS_ADICIONALES/CA_1096_NRO_CARNETAFILIADOO)[1]', 'nvarchar(30)'), '') AS NroCarnet,
                ISNULL(CAMPOS_ADICIONALES.value('(/CAMPOS_ADICIONALES/CA_1096_OBRA_SOCIAL)[1]', 'nvarchar(30)'), '') AS ObraSocial
            FROM GVA14
            WHERE GRUPO_EMPR = ''";

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            sql += @"
              AND (COD_CLIENT LIKE @Busqueda OR RAZON_SOCI LIKE @Busqueda)";
        }

        sql += " ORDER BY RAZON_SOCI";

        return await connection.QueryAsync<Cliente>(sql, new { Busqueda = $"%{busqueda}%" });
    }

    public async Task<IEnumerable<Cliente>> GetObrasSocialesAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT
                COD_CLIENT AS CodClient,
                RAZON_SOCI AS RazonSoci,
                CUIT
            FROM GVA14
            WHERE GRUPO_EMPR = 'OB.SOC'";

        return await connection.QueryAsync<Cliente>(sql);
    }
}
