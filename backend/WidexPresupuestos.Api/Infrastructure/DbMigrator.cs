using DbUp;
using DbUp.Engine;
using DbUp.Support;

namespace WidexPresupuestos.Api.Infrastructure;

public static class DbMigrator
{
    /// <summary>
    /// Aplica los scripts SQL de database/ en orden. Idempotente: DbUp registra
    /// los scripts ya aplicados en la tabla 'schemaversions' y no los repite.
    /// </summary>
    public static void Run(string connectionString, string scriptsPath, ILogger logger)
    {
        if (!Directory.Exists(scriptsPath))
        {
            logger.LogWarning("DbUp: la carpeta de scripts '{Path}' no existe. Se omite la migración.", scriptsPath);
            return;
        }

        // NOTA: la base de datos debe existir previamente (la crea el contenedor MySQL
        // vía MYSQL_DATABASE, o un DBA con el script 01). No se usa EnsureDatabase
        // porque el usuario de la app sólo tiene privilegios sobre su propia base,
        // no sobre el schema de sistema 'mysql' que ese paso necesita.
        static bool IsMySql(string f) => f.EndsWith("_mysql.sql", StringComparison.OrdinalIgnoreCase);
        static bool IsSeed(string f) => f.Contains("seed_data", StringComparison.OrdinalIgnoreCase);
        // CREATE DATABASE requiere privilegio global que el usuario de app no tiene; la base ya existe.
        static bool IsCreateDb(string f) => f.Contains("create_database", StringComparison.OrdinalIgnoreCase);

        var upgrader = DeployChanges.To
            .MySqlDatabase(connectionString)
            // Esquema (run-once): se registra en schemaversions y no se repite.
            // Excluye los .sql viejos de MSSQL, el create_database y el seed.
            .WithScriptsFromFileSystem(
                scriptsPath,
                f => IsMySql(f) && !IsCreateDb(f) && !IsSeed(f))
            // Seed (run-always): idempotente; se re-aplica en cada arranque para que
            // un cambio en los datos semilla (p. ej. la sucursal del correlativo PED)
            // tome efecto sin tener que versionar un script nuevo.
            .WithScriptsFromFileSystem(
                scriptsPath,
                f => IsMySql(f) && IsSeed(f),
                new SqlScriptOptions { ScriptType = ScriptType.RunAlways, RunGroupOrder = 1 })
            // Sin transacción por script: el DDL de MySQL hace commit implícito,
            // así que envolverlo en transacción no aporta y puede generar conflictos.
            .LogToConsole()
            .Build();

        if (!upgrader.IsUpgradeRequired())
        {
            logger.LogInformation("DbUp: base de datos actualizada, no hay scripts pendientes.");
            return;
        }

        logger.LogInformation("DbUp: aplicando scripts desde '{Path}'...", scriptsPath);
        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "DbUp: error al aplicar migraciones.");
            throw new InvalidOperationException("Fallo la migración de base de datos.", result.Error);
        }

        logger.LogInformation("DbUp: migraciones aplicadas correctamente.");
    }
}
