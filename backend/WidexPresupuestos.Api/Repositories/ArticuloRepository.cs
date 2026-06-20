using Dapper;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Repositories;

namespace WidexPresupuestos.Api.Repositories;

public sealed class ArticuloRepository : IArticuloRepository
{
    private readonly IDbConnectionFactory _db;

    public ArticuloRepository(IDbConnectionFactory db) => _db = db;

    /// <summary>
    /// Artículos para la pantalla de pedidos: filtra por path de categoría (LIKE path%).
    /// Devuelve id, codigo, descripcion (descripcio + desc_adic), precio, stock.
    /// </summary>
    public async Task<IEnumerable<Articulo>> GetByFolderAsync(string path)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Articulo>(
            @"SELECT a.id,
                     a.cod_articu AS codigo,
                     CONCAT(a.descripcio, IF(a.desc_adic IS NOT NULL AND a.desc_adic <> '', CONCAT(' ', a.desc_adic), '')) AS descripcion,
                     a.precio,
                     a.stock
              FROM articulos a
              INNER JOIN categorias c ON a.id_folder = c.id_folder
              WHERE c.path LIKE @PathPattern
                AND a.activo = 1
                AND (a.perfil IS NULL OR a.perfil <> 'N')",
            new { PathPattern = $"{path}%" });
    }

    /// <summary>
    /// Estructura jerárquica de categorías para navegación en pedidos.
    /// Devuelve { padres, seleccionada, hijos } igual que el endpoint original.
    /// </summary>
    public async Task<object> GetCategoriasAsync(string id)
    {
        using var conn = _db.CreateConnection();
        var allCats = (await conn.QueryAsync<Categoria>(
            "SELECT id_folder, descrip, id_parent, path FROM categorias ORDER BY path")).ToList();

        var seleccionada = allCats.FirstOrDefault(c => c.IdFolder == id);
        if (seleccionada == null)
            return new { padres = Array.Empty<Categoria>(), seleccionada = (Categoria?)null, hijos = Array.Empty<Categoria>() };

        var padres = new List<Categoria>();
        var current = allCats.FirstOrDefault(c => c.IdFolder == seleccionada.IdParent);
        while (current != null)
        {
            padres.Add(current);
            current = allCats.FirstOrDefault(c => c.IdFolder == current.IdParent);
        }
        padres.Reverse();

        var hijos = allCats.Where(c => c.IdParent == seleccionada.IdFolder).ToList();

        return new { padres, seleccionada, hijos };
    }

    /// <summary>
    /// Artículos para presupuestos: todos los activos con cobertura y cod_articu_dif.
    /// </summary>
    public async Task<IEnumerable<ArticuloPresupuesto>> GetArticulosPresupuestoAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ArticuloPresupuesto>(
            @"SELECT cod_articu,
                     descripcio,
                     desc_adic,
                     cod_barra,
                     cobertura_aplicable,
                     cod_articu_dif
              FROM articulos
              WHERE activo = 1
                AND (perfil IS NULL OR perfil <> 'N')");
    }

    /// <summary>
    /// Talonarios de cotización disponibles.
    /// </summary>
    public async Task<IEnumerable<Talonario>> GetTalonariosAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Talonario>(
            @"SELECT talonario_id, sucursal, descrip
              FROM talonarios
              WHERE comprob = 'COT'
              ORDER BY descrip");
    }
}
