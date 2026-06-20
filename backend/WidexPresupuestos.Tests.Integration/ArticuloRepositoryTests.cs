using FluentAssertions;
using WidexPresupuestos.Api.Repositories;
using WidexPresupuestos.Tests.Integration.Infrastructure;

namespace WidexPresupuestos.Tests.Integration;

[Collection("mysql")]
public sealed class ArticuloRepositoryTests(MysqlFixture fixture) : IntegrationTestBase(fixture)
{
    private ArticuloRepository Repo() => new(Fixture.DbFactory);

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task InsertCategoriaAsync(string idFolder, string descrip,
        string? idParent = null, string? path = null)
    {
        await ExecAsync(
            @"INSERT IGNORE INTO categorias (id_folder, descrip, id_parent, path, sincronizado_en)
              VALUES (@IdFolder, @Descrip, @IdParent, @Path, NOW())",
            new { IdFolder = idFolder, Descrip = descrip, IdParent = idParent,
                  Path = path ?? descrip });
    }

    private async Task InsertArticuloAsync(
        string codArticu, string descripcio, string? descAdic = null,
        bool activo = true, string? perfil = null, string? idFolder = null,
        decimal precio = 0, decimal stock = 0,
        bool coberturaAplicable = false, string? codArticuDif = null)
    {
        await ExecAsync(
            @"INSERT IGNORE INTO articulos
                (cod_articu, descripcio, desc_adic, precio, stock, id_folder,
                 cobertura_aplicable, cod_articu_dif, perfil, activo, sincronizado_en)
              VALUES (@CodArticu, @Descripcio, @DescAdic, @Precio, @Stock, @IdFolder,
                      @CoberturaAplicable, @CodArticuDif, @Perfil, @Activo, NOW())",
            new
            {
                CodArticu = codArticu, Descripcio = descripcio, DescAdic = descAdic,
                Precio = precio, Stock = stock, IdFolder = idFolder,
                CoberturaAplicable = coberturaAplicable ? 1 : 0,
                CodArticuDif = codArticuDif,
                Perfil = perfil, Activo = activo ? 1 : 0
            });
    }

    private async Task CleanupArticulosAsync(params string[] codArticus)
    {
        foreach (var cod in codArticus)
            await ExecAsync("DELETE FROM articulos WHERE cod_articu = @Cod", new { Cod = cod });
    }

    private async Task CleanupCategoriasAsync(params string[] ids)
    {
        foreach (var id in ids)
            await ExecAsync("DELETE FROM categorias WHERE id_folder = @Id", new { Id = id });
    }

    // ── GetArticulosPresupuestoAsync — filtro activo=1 AND perfil<>'N' ────────

    [Fact]
    public async Task GetArticulosPresupuestoAsync_SoloActivosYSinPerfilN()
    {
        await InsertArticuloAsync("TA_PRES1", "Audifono Presup", activo: true,  perfil: null);
        await InsertArticuloAsync("TA_PRES2", "Audifono Inact",  activo: false, perfil: null);
        await InsertArticuloAsync("TA_PRES3", "Audifono PerfilN", activo: true, perfil: "N");

        try
        {
            var result = (await Repo().GetArticulosPresupuestoAsync()).ToList();

            result.Should().Contain(a => a.CodArticu == "TA_PRES1");
            result.Should().NotContain(a => a.CodArticu == "TA_PRES2", "inactivo");
            result.Should().NotContain(a => a.CodArticu == "TA_PRES3", "perfil='N'");
        }
        finally
        {
            await CleanupArticulosAsync("TA_PRES1", "TA_PRES2", "TA_PRES3");
        }
    }

    [Fact]
    public async Task GetArticulosPresupuestoAsync_MapeoContrato_CamposEsperados()
    {
        await InsertArticuloAsync("TA_MAPCONT", "Audifono Base", descAdic: "Extra",
            coberturaAplicable: true, codArticuDif: "TA_DIF99");

        try
        {
            var result = (await Repo().GetArticulosPresupuestoAsync()).ToList();

            var art = result.Should().ContainSingle(a => a.CodArticu == "TA_MAPCONT").Subject;
            art.Descripcio.Should().Be("Audifono Base");
            art.DescAdic.Should().Be("Extra");
            art.CoberturaAplicable.Should().BeTrue();
            art.CodArticuDif.Should().Be("TA_DIF99");
        }
        finally
        {
            await CleanupArticulosAsync("TA_MAPCONT");
        }
    }

    // ── GetByFolderAsync — filtra por path LIKE prefijo ───────────────────────

    [Fact]
    public async Task GetByFolderAsync_PathExacto_DevuelveArticulosDeLaCarpeta()
    {
        await InsertCategoriaAsync("TF_CAT1", "Cat Uno", path: "Raiz/CatUno");
        await InsertCategoriaAsync("TF_CAT2", "Cat Dos", path: "Raiz/CatDos");
        await InsertArticuloAsync("TA_FOLD1", "Art en CatUno", idFolder: "TF_CAT1");
        await InsertArticuloAsync("TA_FOLD2", "Art en CatDos", idFolder: "TF_CAT2");

        try
        {
            var result = (await Repo().GetByFolderAsync("Raiz/CatUno")).ToList();

            result.Should().Contain(a => a.Codigo == "TA_FOLD1");
            result.Should().NotContain(a => a.Codigo == "TA_FOLD2");
        }
        finally
        {
            await CleanupArticulosAsync("TA_FOLD1", "TA_FOLD2");
            await CleanupCategoriasAsync("TF_CAT1", "TF_CAT2");
        }
    }

    [Fact]
    public async Task GetByFolderAsync_DescripcionConcatenada_CuandoHayDescAdic()
    {
        await InsertCategoriaAsync("TF_CATD", "Cat Desc", path: "Raiz/CatDesc");
        await InsertArticuloAsync("TA_CONC", "Nombre Base", descAdic: "Adicion Extra",
            idFolder: "TF_CATD", precio: 5000, stock: 3);

        try
        {
            var result = (await Repo().GetByFolderAsync("Raiz/CatDesc")).ToList();

            var art = result.Should().ContainSingle(a => a.Codigo == "TA_CONC").Subject;
            // CONCAT(descripcio, ' ', desc_adic) cuando desc_adic no es NULL/vacío
            art.Descripcion.Should().Be("Nombre Base Adicion Extra");
            art.Precio.Should().Be(5000);
            art.Stock.Should().Be(3);
        }
        finally
        {
            await CleanupArticulosAsync("TA_CONC");
            await CleanupCategoriasAsync("TF_CATD");
        }
    }

    [Fact]
    public async Task GetByFolderAsync_SinDescAdic_DescripcionSoloBase()
    {
        await InsertCategoriaAsync("TF_CATND", "Cat NoDesc", path: "Raiz/CatNoDesc");
        await InsertArticuloAsync("TA_NOADD", "Solo Nombre", descAdic: null, idFolder: "TF_CATND");

        try
        {
            var result = (await Repo().GetByFolderAsync("Raiz/CatNoDesc")).ToList();

            var art = result.Should().ContainSingle(a => a.Codigo == "TA_NOADD").Subject;
            art.Descripcion.Should().Be("Solo Nombre");
        }
        finally
        {
            await CleanupArticulosAsync("TA_NOADD");
            await CleanupCategoriasAsync("TF_CATND");
        }
    }

    [Fact]
    public async Task GetByFolderAsync_PathPrefijo_InclujeSubcarpetas()
    {
        // CatPadre/CatHija → buscar por prefijo "Raiz/Padre" debe devolver
        // artículos de CatHija (path LIKE 'Raiz/Padre%').
        await InsertCategoriaAsync("TF_PAD", "Padre", path: "Raiz/Padre");
        await InsertCategoriaAsync("TF_HIJ", "Hija", idParent: "TF_PAD", path: "Raiz/Padre/Hija");
        await InsertArticuloAsync("TA_HIJ1", "Art Hija", idFolder: "TF_HIJ");

        try
        {
            var result = (await Repo().GetByFolderAsync("Raiz/Padre")).ToList();

            result.Should().Contain(a => a.Codigo == "TA_HIJ1",
                "LIKE 'Raiz/Padre%' incluye subcarpetas");
        }
        finally
        {
            await CleanupArticulosAsync("TA_HIJ1");
            await CleanupCategoriasAsync("TF_HIJ", "TF_PAD");
        }
    }

    [Fact]
    public async Task GetByFolderAsync_ExcluyeInactivosYPerfilN()
    {
        await InsertCategoriaAsync("TF_CATX", "Cat Exclusion", path: "Raiz/Excl");
        await InsertArticuloAsync("TA_EXCL_OK",  "Activo OK",    activo: true,  perfil: null,  idFolder: "TF_CATX");
        await InsertArticuloAsync("TA_EXCL_IN",  "Inactivo",     activo: false, perfil: null,  idFolder: "TF_CATX");
        await InsertArticuloAsync("TA_EXCL_N",   "Perfil N",     activo: true,  perfil: "N",   idFolder: "TF_CATX");

        try
        {
            var result = (await Repo().GetByFolderAsync("Raiz/Excl")).ToList();

            result.Should().Contain(a => a.Codigo == "TA_EXCL_OK");
            result.Should().NotContain(a => a.Codigo == "TA_EXCL_IN");
            result.Should().NotContain(a => a.Codigo == "TA_EXCL_N");
        }
        finally
        {
            await CleanupArticulosAsync("TA_EXCL_OK", "TA_EXCL_IN", "TA_EXCL_N");
            await CleanupCategoriasAsync("TF_CATX");
        }
    }

    // ── GetCategoriasAsync — jerarquía {padres, seleccionada, hijos} ──────────

    [Fact]
    public async Task GetCategoriasAsync_IdExistente_DevuelveJerarquiaCorrecta()
    {
        // Árbol: Raiz → Rama → Hoja (seleccionada) + dos hijos de Hoja
        await InsertCategoriaAsync("TH_RAIZ", "Raiz Test",  path: "Raiz");
        await InsertCategoriaAsync("TH_RAMA", "Rama Test",  idParent: "TH_RAIZ", path: "Raiz/Rama");
        await InsertCategoriaAsync("TH_HOJA", "Hoja Test",  idParent: "TH_RAMA", path: "Raiz/Rama/Hoja");
        await InsertCategoriaAsync("TH_H1",   "Hijo Uno",   idParent: "TH_HOJA", path: "Raiz/Rama/Hoja/Uno");
        await InsertCategoriaAsync("TH_H2",   "Hijo Dos",   idParent: "TH_HOJA", path: "Raiz/Rama/Hoja/Dos");

        try
        {
            var raw = await Repo().GetCategoriasAsync("TH_HOJA");

            // Deserializar el objeto anónimo a través de reflexión para no
            // acoplar el test a un tipo interno.
            var type = raw.GetType();
            var padres = (IEnumerable<WidexPresupuestos.Shared.Models.Categoria>)
                type.GetProperty("padres")!.GetValue(raw)!;
            var seleccionada = (WidexPresupuestos.Shared.Models.Categoria?)
                type.GetProperty("seleccionada")!.GetValue(raw);
            var hijos = (IEnumerable<WidexPresupuestos.Shared.Models.Categoria>)
                type.GetProperty("hijos")!.GetValue(raw)!;

            seleccionada.Should().NotBeNull();
            seleccionada!.IdFolder.Should().Be("TH_HOJA");

            var padresList = padres.ToList();
            padresList.Should().HaveCount(2, "Raiz y Rama son ancestros de Hoja");
            padresList[0].IdFolder.Should().Be("TH_RAIZ", "primero el abuelo (raíz)");
            padresList[1].IdFolder.Should().Be("TH_RAMA", "luego el padre directo");

            var hijosList = hijos.ToList();
            hijosList.Should().HaveCount(2);
            hijosList.Select(h => h.IdFolder).Should().BeEquivalentTo(["TH_H1", "TH_H2"]);
        }
        finally
        {
            await CleanupCategoriasAsync("TH_H1", "TH_H2", "TH_HOJA", "TH_RAMA", "TH_RAIZ");
        }
    }

    [Fact]
    public async Task GetCategoriasAsync_IdInexistente_DevuelveEstructuraVacia()
    {
        var raw = await Repo().GetCategoriasAsync("ID_QUE_NO_EXISTE_XYZ");

        var type = raw.GetType();
        var padres = (IEnumerable<WidexPresupuestos.Shared.Models.Categoria>)
            type.GetProperty("padres")!.GetValue(raw)!;
        var seleccionada = type.GetProperty("seleccionada")!.GetValue(raw);
        var hijos = (IEnumerable<WidexPresupuestos.Shared.Models.Categoria>)
            type.GetProperty("hijos")!.GetValue(raw)!;

        seleccionada.Should().BeNull();
        padres.Should().BeEmpty();
        hijos.Should().BeEmpty();
    }

    // ── GetTalonariosAsync — solo comprob='COT' ───────────────────────────────

    [Fact]
    public async Task GetTalonariosAsync_SoloComprob_COT()
    {
        await ExecAsync(
            @"INSERT IGNORE INTO talonarios (talonario_id, sucursal, descrip, comprob, sincronizado_en)
              VALUES (@T1, '01', 'Cotizacion Test', 'COT', NOW()),
                     (@T2, '01', 'Remito Test',     'REM', NOW())",
            new { T1 = "TAL_COT_T", T2 = "TAL_REM_T" });

        try
        {
            var result = (await Repo().GetTalonariosAsync()).ToList();

            result.Should().Contain(t => t.TalonarioId == "TAL_COT_T");
            result.Should().NotContain(t => t.TalonarioId == "TAL_REM_T", "comprob != COT");
        }
        finally
        {
            await ExecAsync("DELETE FROM talonarios WHERE talonario_id IN ('TAL_COT_T','TAL_REM_T')");
        }
    }

    [Fact]
    public async Task GetTalonariosAsync_MapeoContrato_TalonarioIdSucursalDescrip()
    {
        await ExecAsync(
            @"INSERT IGNORE INTO talonarios (talonario_id, sucursal, descrip, comprob, sincronizado_en)
              VALUES ('TAL_MAP_T', '05', 'Talonario Mapped', 'COT', NOW())");

        try
        {
            var result = (await Repo().GetTalonariosAsync()).ToList();

            var tal = result.Should().Contain(t => t.TalonarioId == "TAL_MAP_T").Subject;
            tal.Sucursal.Should().Be("05");
            tal.Descrip.Should().Be("Talonario Mapped");
        }
        finally
        {
            await ExecAsync("DELETE FROM talonarios WHERE talonario_id = 'TAL_MAP_T'");
        }
    }
}
