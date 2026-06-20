using Dapper;
using FluentAssertions;
using WidexPresupuestos.Api.Repositories;
using WidexPresupuestos.Tests.Integration.Infrastructure;

namespace WidexPresupuestos.Tests.Integration;

[Collection("mysql")]
public sealed class ClienteRepositoryTests(MysqlFixture fixture) : IntegrationTestBase(fixture)
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private ClienteRepository Repo() => new(Fixture.DbFactory);

    /// <summary>Inserta una fila en clientes y devuelve su id para limpieza.</summary>
    private async Task<long> InsertClienteAsync(
        string codClient, string razonSoci, bool esObraSocial,
        bool activo = true, string? nroCarnet = null, string? obraSocialCod = null)
    {
        return await InsertAndGetIdAsync(
            @"INSERT INTO clientes (cod_client, razon_soci, cuit, es_obra_social, nro_carnet, obra_social_cod, activo, sincronizado_en)
              VALUES (@CodClient, @RazonSoci, NULL, @EsObraSocial, @NroCarnet, @ObraSocialCod, @Activo, NOW())",
            new { CodClient = codClient, RazonSoci = razonSoci, EsObraSocial = esObraSocial ? 1 : 0,
                  NroCarnet = nroCarnet, ObraSocialCod = obraSocialCod, Activo = activo ? 1 : 0 });
    }

    private async Task CleanupClientesAsync(params string[] codClients)
    {
        foreach (var cod in codClients)
            await ExecAsync("DELETE FROM clientes WHERE cod_client = @Cod", new { Cod = cod });
    }

    // ── BuscarPacientesAsync — filtro es_obra_social=0 AND activo=1 ──────────

    [Fact]
    public async Task BuscarPacientesAsync_SinFiltro_DevuelveSoloPacientesActivosNoObraSocial()
    {
        await InsertClienteAsync("TC_PAC1", "Paciente Test Uno", esObraSocial: false, activo: true);
        await InsertClienteAsync("TC_OS1",  "ObraSocial Test",  esObraSocial: true,  activo: true);
        await InsertClienteAsync("TC_PAC2", "Paciente Inactivo", esObraSocial: false, activo: false);

        try
        {
            var result = (await Repo().BuscarPacientesAsync(null)).ToList();

            // Debe contener el paciente activo
            result.Should().Contain(c => c.CodClient == "TC_PAC1");
            // No debe contener obra social ni inactivo
            result.Should().NotContain(c => c.CodClient == "TC_OS1");
            result.Should().NotContain(c => c.CodClient == "TC_PAC2");
        }
        finally
        {
            await CleanupClientesAsync("TC_PAC1", "TC_OS1", "TC_PAC2");
        }
    }

    // ── BuscarPacientesAsync — mapeo de contrato ──────────────────────────────

    [Fact]
    public async Task BuscarPacientesAsync_PacienteConObraSocial_MapeoObraSocialYNroCarnetCorrectos()
    {
        // obra_social_cod → ObraSocial; nro_carnet → NroCarnet
        await InsertClienteAsync("TC_OS_MAP", "OS Mapeada", esObraSocial: true);
        await InsertClienteAsync("TC_PAC_MAP", "Paciente Mapeado", esObraSocial: false,
            nroCarnet: "CARNET-9999", obraSocialCod: "TC_OS_MAP");

        try
        {
            var result = (await Repo().BuscarPacientesAsync("TC_PAC_MAP")).ToList();

            result.Should().HaveCount(1);
            var cliente = result[0];
            cliente.CodClient.Should().Be("TC_PAC_MAP");
            cliente.NroCarnet.Should().Be("CARNET-9999");       // nro_carnet → NroCarnet
            cliente.ObraSocial.Should().Be("TC_OS_MAP");        // obra_social_cod → ObraSocial
        }
        finally
        {
            await CleanupClientesAsync("TC_PAC_MAP", "TC_OS_MAP");
        }
    }

    // ── BuscarPacientesAsync — búsqueda por nombre ────────────────────────────

    [Fact]
    public async Task BuscarPacientesAsync_BusquedaPorNombre_DevuelveSoloCoincidencias()
    {
        await InsertClienteAsync("TC_A1", "Gonzalez Pablo", esObraSocial: false);
        await InsertClienteAsync("TC_A2", "Rodriguez Ana",  esObraSocial: false);

        try
        {
            var result = (await Repo().BuscarPacientesAsync("Gonzalez")).ToList();

            result.Should().Contain(c => c.CodClient == "TC_A1");
            result.Should().NotContain(c => c.CodClient == "TC_A2");
        }
        finally
        {
            await CleanupClientesAsync("TC_A1", "TC_A2");
        }
    }

    // ── BuscarPacientesAsync — escape de wildcards LIKE ───────────────────────

    [Fact]
    public async Task BuscarPacientesAsync_BusquedaConPorcentaje_NoDevuelveTodosLosRegistros()
    {
        // Insertar un cliente cuya razón social contiene literalmente '%'.
        // Al buscar '%' el escape debe impedirlo de actuar como wildcard.
        await InsertClienteAsync("TC_WILD1", "50% Audición", esObraSocial: false);
        await InsertClienteAsync("TC_WILD2", "Sin coincidencia ABC", esObraSocial: false);

        try
        {
            var result = (await Repo().BuscarPacientesAsync("%")).ToList();

            // El '%' buscado debe tratar el literal '%', por lo que sólo
            // debe devolver "50% Audición" y no "Sin coincidencia ABC".
            result.Should().Contain(c => c.CodClient == "TC_WILD1",
                "el cliente con '%' literal en razon_soci debe aparecer");
            result.Should().NotContain(c => c.CodClient == "TC_WILD2",
                "sin '%' literal en razon_soci no debe aparecer cuando se busca '%'");
        }
        finally
        {
            await CleanupClientesAsync("TC_WILD1", "TC_WILD2");
        }
    }

    [Fact]
    public async Task BuscarPacientesAsync_BusquedaConGuionBajo_NoActuaComoWildcard()
    {
        // Usar cod_client sin guiones bajos para que el LIKE en cod_client no interfiera.
        // El campo razon_soci de TCUND1 contiene '_' literal; el de TCUND2 no.
        // Si '_' no se escapa actuaría como wildcard y ambos saldrían; escapado sólo TCUND1.
        await InsertClienteAsync("TCUND1", "A_B Audición",  esObraSocial: false);
        await InsertClienteAsync("TCUND2", "ACB Diferente", esObraSocial: false);

        try
        {
            var result = (await Repo().BuscarPacientesAsync("_")).ToList();

            result.Should().Contain(c => c.CodClient == "TCUND1",
                "razón social con '_' literal debe aparecer");
            result.Should().NotContain(c => c.CodClient == "TCUND2",
                "sin '_' literal no debe aparecer");
        }
        finally
        {
            await CleanupClientesAsync("TCUND1", "TCUND2");
        }
    }

    // ── GetObrasSocialesAsync — filtro es_obra_social=1 ───────────────────────

    [Fact]
    public async Task GetObrasSocialesAsync_DevuelveSoloObrasSocialesActivas()
    {
        await InsertClienteAsync("TC_OS2",  "OSDE Test",    esObraSocial: true,  activo: true);
        await InsertClienteAsync("TC_OS3",  "Swiss Medical", esObraSocial: true,  activo: false);
        await InsertClienteAsync("TC_PAC3", "Paciente Test", esObraSocial: false, activo: true);

        try
        {
            var result = (await Repo().GetObrasSocialesAsync()).ToList();

            result.Should().Contain(c => c.CodClient == "TC_OS2");
            result.Should().NotContain(c => c.CodClient == "TC_OS3",   "inactiva");
            result.Should().NotContain(c => c.CodClient == "TC_PAC3", "es paciente, no OS");
        }
        finally
        {
            await CleanupClientesAsync("TC_OS2", "TC_OS3", "TC_PAC3");
        }
    }
}
