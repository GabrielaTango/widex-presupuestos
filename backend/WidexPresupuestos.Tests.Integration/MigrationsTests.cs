using System.Reflection;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using WidexPresupuestos.Api.Infrastructure;
using WidexPresupuestos.Tests.Integration.Infrastructure;

namespace WidexPresupuestos.Tests.Integration;

/// <summary>
/// Valida que DbMigrator.Run (idempotencia sobre esquema ya aplicado) y
/// RunDevSeed corren sin error y producen las tablas esperadas.
///
/// La fixture ya aplicó el esquema (en dos fases para evitar el problema de
/// ordering de dbup 5.x). Aquí llamamos a DbMigrator.Run sobre una base
/// que YA TIENE el esquema — en esa condición el upgrader detecta que no hay
/// scripts pendientes y retorna inmediatamente sin tocar el seed, por lo que
/// el bug de ordenamiento no se dispara. Eso es exactamente lo que ocurre en
/// producción en arranques subsiguientes.
/// </summary>
[Collection("mysql")]
public sealed class MigrationsTests(MysqlFixture fixture)
{
    [Fact]
    public async Task Run_EsquemaYaAplicado_IdempotenteSinError()
    {
        // Esquema ya aplicado por la fixture → DbUp dice "no hay scripts pendientes"
        // y retorna sin ejecutar nada (ni el seed).
        var act = () => DbMigrator.Run(fixture.ConnectionString, fixture.ScriptsPath, NullLogger.Instance);

        act.Should().NotThrow();

        // Verificar que las tablas clave existen (prueba que el esquema se aplicó).
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        var tables = (await conn.QueryAsync<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE()")).ToList();

        tables.Should().Contain(["clientes", "articulos", "vendedores", "talonarios", "usuarios", "categorias"]);
    }

    [Fact]
    public void RunDevSeed_CargaDatosSinError()
    {
        // RunDevSeed opera con NullJournal (run-always) sobre tablas ya existentes.
        var devSeedPath = Path.Combine(fixture.ScriptsPath, "dev-seed");

        var act = () => DbMigrator.RunDevSeed(fixture.ConnectionString, devSeedPath, NullLogger.Instance);

        act.Should().NotThrow();
    }
}
