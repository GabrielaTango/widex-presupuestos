using Dapper;
using Microsoft.Data.SqlClient;
using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public class VendedorRepository : IVendedorRepository
{
    private readonly string _connectionString;

    public VendedorRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TangoConnection")
            ?? throw new InvalidOperationException("TangoConnection not configured");
    }

    public async Task<IEnumerable<Vendedor>> GetVendedoresSeleccionAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT
                COD_VENDED AS CodVended,
                NOMBRE_VEN AS NombreVen,
                PORC_COMIS AS PorcComis
            FROM GVA23
            WHERE INHABILITA = 0
              AND CAMPOS_ADICIONALES.value('(CAMPOS_ADICIONALES/CA_1118_SELECCION_WIDEX)[1]','varchar(1)') NOT IN ('N', '')
            ORDER BY NOMBRE_VEN";

        return await connection.QueryAsync<Vendedor>(sql);
    }

    public async Task<IEnumerable<Vendedor>> GetVendedoresNoSeleccionAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT
                COD_VENDED AS CodVended,
                NOMBRE_VEN AS NombreVen,
                PORC_COMIS AS PorcComis
            FROM GVA23
            WHERE INHABILITA = 0
              AND CAMPOS_ADICIONALES.value('(CAMPOS_ADICIONALES/CA_1118_SELECCION_WIDEX)[1]','varchar(1)') IN ('N', '')
            ORDER BY NOMBRE_VEN";

        return await connection.QueryAsync<Vendedor>(sql);
    }
}
