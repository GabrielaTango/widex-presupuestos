using Dapper;
using MySqlConnector;

namespace WidexPresupuestos.Tests.Integration.Infrastructure;

/// <summary>
/// Clase base: expone helpers de inserción/limpieza para tests de integración.
/// Cada test debe limpiar las filas que insertó (via CleanupAsync o DeleteAsync).
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly MysqlFixture Fixture;

    protected IntegrationTestBase(MysqlFixture fixture) => Fixture = fixture;

    protected MySqlConnection OpenConnection() =>
        new(Fixture.ConnectionString);

    protected async Task ExecAsync(string sql, object? param = null)
    {
        await using var conn = OpenConnection();
        await conn.ExecuteAsync(sql, param);
    }

    protected async Task<long> InsertAndGetIdAsync(string sql, object? param = null)
    {
        await using var conn = OpenConnection();
        await conn.ExecuteAsync(sql, param);
        return await conn.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");
    }

    protected async Task DeleteByConditionAsync(string table, string where, object param)
    {
        await ExecAsync($"DELETE FROM `{table}` WHERE {where}", param);
    }
}
