using Dapper;
using Microsoft.Data.SqlClient;
using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public class ArticuloRepository : IArticuloRepository
{
    private readonly string _connectionString;

    public ArticuloRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TangoConnection")
            ?? throw new InvalidOperationException("TangoConnection not configured");
    }

    public async Task<IEnumerable<Articulo>> GetByFolderAsync(string idFolder)
    {
        using var connection = new SqlConnection(_connectionString);

        var clasificadores = BuildClasificadoresQuery();

        var sql = $@"
            SELECT
                c.ID_sta11itc AS Id,
                STA11.COD_ARTICU AS Codigo,
                DESCRIPCIO + ' ' + DESC_ADIC AS Descripcion,
                ISNULL(PRECIO, 0) AS Precio,
                ISNULL(CANT_STOCK, 0) AS Stock
            FROM STA11
            LEFT OUTER JOIN GVA17 ON GVA17.ID_STA11 = STA11.ID_STA11 AND GVA17.NRO_DE_LIS = 2
            LEFT OUTER JOIN STA19 ON STA19.COD_ARTICU = STA11.COD_ARTICU AND STA19.COD_DEPOSI = '01'
            INNER JOIN sta11itc c ON c.code = STA11.cod_articu
            INNER JOIN ({clasificadores}) clasif ON c.IDFOLDER = clasif.IDFOLDER
            WHERE [path] LIKE @FolderPattern";

        return await connection.QueryAsync<Articulo>(sql, new { FolderPattern = $"{idFolder}%" });
    }

    public async Task<object> GetCategoriasAsync(string id)
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = BuildClasificadoresQuery();

        var allCats = (await connection.QueryAsync<Categoria>(sql)).ToList();

        var seleccionada = allCats.FirstOrDefault(c => c.IdFolder == id);
        if (seleccionada == null)
            return new { padres = new List<Categoria>(), seleccionada = (Categoria?)null, hijos = new List<Categoria>() };

        // Build parents chain
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

    public async Task<IEnumerable<ArticuloPresupuesto>> GetArticulosPresupuestoAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT
                STA11.COD_ARTICU AS CodArticu,
                DESCRIPCIO AS Descripcio,
                DESC_ADIC AS DescAdic,
                COD_BARRA AS CodBarra,
                CASE WHEN BA_DIFFAC_NEW.COD_DIF_FAC IS NULL THEN 0 ELSE 1 END AS CoberturaAplicable,
                ISNULL(BA_DIFFAC_NEW.COD_DIF_FAC, '') AS CodArticuDif
            FROM STA11
            LEFT OUTER JOIN BA_DIFFAC_NEW
                ON BA_DIFFAC_NEW.COD_ARTICU = STA11.COD_ARTICU COLLATE Modern_Spanish_CI_AI
            WHERE PERFIL <> 'N'";

        return await connection.QueryAsync<ArticuloPresupuesto>(sql);
    }

    public async Task<IEnumerable<Talonario>> GetTalonariosAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT
                TALONARIO AS TalonarioId,
                SUCURSAL,
                DESCRIP
            FROM GVA43
            WHERE COMPROB = 'COT'
              AND FECHA_VTO = '1800-01-01'";

        return await connection.QueryAsync<Talonario>(sql);
    }

    private static string BuildClasificadoresQuery()
    {
        const string idFolderPadre = "45";
        const string padre = "SISTEMA INTEGRAL";

        return $@"
            SELECT
                ISNULL(t8.DESCRIP + '_','') + ISNULL(t7.DESCRIP + '_','') + ISNULL(t6.DESCRIP + '_','') +
                ISNULL(t5.DESCRIP + '_','') + ISNULL(t4.DESCRIP + '_','') + ISNULL(t3.DESCRIP + '_','') +
                ISNULL(t2.DESCRIP + '_','') + ISNULL(t1.DESCRIP,'') AS Path,
                t1.IDFOLDER AS IdFolder, t1.DESCRIP AS Descrip, t1.IDPARENT AS IdParent
            FROM STA11FLD t1
            LEFT OUTER JOIN STA11FLD t2 ON t1.IDPARENT = t2.IDFOLDER AND t2.IDFOLDER >= {idFolderPadre}
            LEFT OUTER JOIN STA11FLD t3 ON t2.IDPARENT = t3.IDFOLDER AND t3.IDFOLDER >= {idFolderPadre}
            LEFT OUTER JOIN STA11FLD t4 ON t3.IDPARENT = t4.IDFOLDER AND t4.IDFOLDER >= {idFolderPadre}
            LEFT OUTER JOIN STA11FLD t5 ON t4.IDPARENT = t5.IDFOLDER AND t5.IDFOLDER >= {idFolderPadre}
            LEFT OUTER JOIN STA11FLD t6 ON t5.IDPARENT = t6.IDFOLDER AND t6.IDFOLDER >= {idFolderPadre}
            LEFT OUTER JOIN STA11FLD t7 ON t6.IDPARENT = t7.IDFOLDER AND t7.IDFOLDER >= {idFolderPadre}
            LEFT OUTER JOIN STA11FLD t8 ON t7.IDPARENT = t8.IDFOLDER AND t8.IDFOLDER >= {idFolderPadre}
            WHERE t1.IDFOLDER >= {idFolderPadre}
            AND SUBSTRING(
                ISNULL(t8.DESCRIP + '_','') + ISNULL(t7.DESCRIP + '_','') + ISNULL(t6.DESCRIP + '_','') +
                ISNULL(t5.DESCRIP + '_','') + ISNULL(t4.DESCRIP + '_','') + ISNULL(t3.DESCRIP + '_','') +
                ISNULL(t2.DESCRIP + '_','') + ISNULL(t1.DESCRIP,''), 1, {padre.Length}
            ) = '{padre}'";
    }
}
