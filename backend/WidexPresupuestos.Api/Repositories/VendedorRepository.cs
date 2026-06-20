using Dapper;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Repositories;

namespace WidexPresupuestos.Api.Repositories;

public sealed class VendedorRepository : IVendedorRepository
{
    private readonly IDbConnectionFactory _db;

    public VendedorRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Vendedor>> GetVendedoresSeleccionAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Vendedor>(
            @"SELECT cod_vended, nombre_ven, porc_comis
              FROM vendedores
              WHERE activo = 1 AND seleccion_widex = 1
              ORDER BY nombre_ven");
    }

    public async Task<IEnumerable<Vendedor>> GetVendedoresNoSeleccionAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Vendedor>(
            @"SELECT cod_vended, nombre_ven, porc_comis
              FROM vendedores
              WHERE activo = 1 AND seleccion_widex = 0
              ORDER BY nombre_ven");
    }
}
