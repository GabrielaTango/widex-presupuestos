using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MySql;
using WidexPresupuestos.Api.Infrastructure;
using WidexPresupuestos.Shared.Infrastructure;

namespace WidexPresupuestos.Tests.Integration.Infrastructure;

/// <summary>
/// Levanta un contenedor mysql:8.0 (o usa WIDEX_TEST_MYSQL_CS como fallback) y
/// aplica el esquema con el MISMO <see cref="DbMigrator.Run"/> de producción.
/// Al correr contra una base recién creada, este fixture cubre exactamente el
/// escenario de "deploy nuevo" (esquema → seed en orden), que es donde el bug de
/// ordenamiento de DbUp se manifestaba. Sirve como guardia de regresión.
/// </summary>
public sealed class MysqlFixture : IAsyncLifetime
{
    private MySqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;
    public IDbConnectionFactory DbFactory { get; private set; } = null!;
    public string ScriptsPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var envCs = Environment.GetEnvironmentVariable("WIDEX_TEST_MYSQL_CS");

        if (!string.IsNullOrWhiteSpace(envCs))
        {
            ConnectionString = envCs;
        }
        else
        {
            _container = new MySqlBuilder()
                .WithImage("mysql:8.0")
                .WithDatabase("widex_presupuestos")
                .WithUsername("root")
                .WithPassword("testroot")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        DapperConfig.Configure();

        ScriptsPath = ResolveScriptsPath();

        // Aplica esquema + seed con el migrador de producción (orden garantizado).
        DbMigrator.Run(ConnectionString, ScriptsPath, NullLogger.Instance);

        DbFactory = new MySqlConnectionFactory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }

    // Sube desde el directorio de salida del ensamblado hasta encontrar database/.
    private static string ResolveScriptsPath()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                  ?? AppContext.BaseDirectory;

        var candidate = dir;
        for (var i = 0; i < 8; i++)
        {
            var dbPath = Path.Combine(candidate, "database");
            if (Directory.Exists(dbPath))
                return Path.GetFullPath(dbPath);

            var parent = Directory.GetParent(candidate)?.FullName;
            if (parent == null) break;
            candidate = parent;
        }

        // Fallback: ruta relativa desde el bin del test (6 niveles: bin/Debug/net8.0/../../.. → raíz del repo)
        return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "..", "database"));
    }
}

[CollectionDefinition("mysql")]
public sealed class MysqlCollection : ICollectionFixture<MysqlFixture> { }
