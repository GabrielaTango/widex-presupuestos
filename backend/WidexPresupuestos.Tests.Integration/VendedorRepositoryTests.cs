using FluentAssertions;
using WidexPresupuestos.Api.Repositories;
using WidexPresupuestos.Tests.Integration.Infrastructure;

namespace WidexPresupuestos.Tests.Integration;

[Collection("mysql")]
public sealed class VendedorRepositoryTests(MysqlFixture fixture) : IntegrationTestBase(fixture)
{
    private VendedorRepository Repo() => new(Fixture.DbFactory);

    private async Task InsertVendedorAsync(string codVended, string nombreVen,
        bool seleccionWidex, bool activo = true, decimal porcComis = 0)
    {
        await ExecAsync(
            @"INSERT IGNORE INTO vendedores (cod_vended, nombre_ven, porc_comis, seleccion_widex, activo, sincronizado_en)
              VALUES (@CodVended, @NombreVen, @PorcComis, @SeleccionWidex, @Activo, NOW())",
            new
            {
                CodVended = codVended, NombreVen = nombreVen, PorcComis = porcComis,
                SeleccionWidex = seleccionWidex ? 1 : 0, Activo = activo ? 1 : 0
            });
    }

    private async Task CleanupVendedoresAsync(params string[] codVendeds)
    {
        foreach (var cod in codVendeds)
            await ExecAsync("DELETE FROM vendedores WHERE cod_vended = @Cod", new { Cod = cod });
    }

    // ── GetVendedoresSeleccionAsync — seleccion_widex=1 AND activo=1 ──────────

    [Fact]
    public async Task GetVendedoresSeleccionAsync_SoloActivos_SeleccionWidex1()
    {
        await InsertVendedorAsync("TV_SEL1",  "Vendedor Seleccion",  seleccionWidex: true,  activo: true);
        await InsertVendedorAsync("TV_SEL2",  "Vendedor Inactivo",   seleccionWidex: true,  activo: false);
        await InsertVendedorAsync("TV_NSEL1", "Vendedor No Sel",     seleccionWidex: false, activo: true);

        try
        {
            var result = (await Repo().GetVendedoresSeleccionAsync()).ToList();

            result.Should().Contain(v => v.CodVended == "TV_SEL1");
            result.Should().NotContain(v => v.CodVended == "TV_SEL2",  "inactivo");
            result.Should().NotContain(v => v.CodVended == "TV_NSEL1", "seleccion_widex=0");
        }
        finally
        {
            await CleanupVendedoresAsync("TV_SEL1", "TV_SEL2", "TV_NSEL1");
        }
    }

    [Fact]
    public async Task GetVendedoresSeleccionAsync_MapeoContrato_CamposEsperados()
    {
        await InsertVendedorAsync("TV_MAPSEL", "Juan Vendedor", seleccionWidex: true, porcComis: 7.5m);

        try
        {
            var result = (await Repo().GetVendedoresSeleccionAsync()).ToList();

            var v = result.Should().Contain(x => x.CodVended == "TV_MAPSEL").Subject;
            v.NombreVen.Should().Be("Juan Vendedor");
            v.PorcComis.Should().Be(7.5m);
        }
        finally
        {
            await CleanupVendedoresAsync("TV_MAPSEL");
        }
    }

    // ── GetVendedoresNoSeleccionAsync — seleccion_widex=0 AND activo=1 ────────

    [Fact]
    public async Task GetVendedoresNoSeleccionAsync_SoloActivos_SeleccionWidex0()
    {
        await InsertVendedorAsync("TV_NS1",  "No Seleccion Activo",   seleccionWidex: false, activo: true);
        await InsertVendedorAsync("TV_NS2",  "No Seleccion Inactivo", seleccionWidex: false, activo: false);
        await InsertVendedorAsync("TV_S1",   "Seleccion Widex",       seleccionWidex: true,  activo: true);

        try
        {
            var result = (await Repo().GetVendedoresNoSeleccionAsync()).ToList();

            result.Should().Contain(v => v.CodVended == "TV_NS1");
            result.Should().NotContain(v => v.CodVended == "TV_NS2", "inactivo");
            result.Should().NotContain(v => v.CodVended == "TV_S1",  "seleccion_widex=1");
        }
        finally
        {
            await CleanupVendedoresAsync("TV_NS1", "TV_NS2", "TV_S1");
        }
    }

    [Fact]
    public async Task GetVendedoresNoSeleccionAsync_MapeoContrato_CamposEsperados()
    {
        await InsertVendedorAsync("TV_MAPNS", "Pedro Externo", seleccionWidex: false, porcComis: 3.25m);

        try
        {
            var result = (await Repo().GetVendedoresNoSeleccionAsync()).ToList();

            var v = result.Should().Contain(x => x.CodVended == "TV_MAPNS").Subject;
            v.NombreVen.Should().Be("Pedro Externo");
            v.PorcComis.Should().Be(3.25m);
        }
        finally
        {
            await CleanupVendedoresAsync("TV_MAPNS");
        }
    }
}
