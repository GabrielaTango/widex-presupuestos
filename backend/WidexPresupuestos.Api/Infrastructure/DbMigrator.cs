using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

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

        // IMPORTANTE: esquema y seed van en DOS upgraders SECUENCIALES, no en uno solo.
        // Mezclar run-once + run-always en un mismo UpgradeEngine no garantiza el orden
        // (DbUp puede correr el seed antes de crear las tablas) y rompe en base vacía.

        // 1) Esquema (run-once): se registra en schemaversions y no se repite.
        //    Excluye los .sql viejos de MSSQL, el create_database y el seed.
        var schema = DeployChanges.To
            .MySqlDatabase(connectionString)
            .WithScriptsFromFileSystem(scriptsPath, f => IsMySql(f) && !IsCreateDb(f) && !IsSeed(f))
            .LogToConsole()
            .Build();
        Aplicar(schema, "esquema", logger);

        // 2) Seed (run-always): corre SIEMPRE, después del esquema. NullJournal = no
        //    se registra, y los scripts son idempotentes, así que un seed editado
        //    (p. ej. la sucursal del correlativo PED) se re-aplica sin versionar.
        var seed = DeployChanges.To
            .MySqlDatabase(connectionString)
            .WithScriptsFromFileSystem(scriptsPath, f => IsMySql(f) && IsSeed(f))
            .JournalTo(new NullJournal())
            .LogToConsole()
            .Build();
        Aplicar(seed, "seed", logger);
    }

    private static void Aplicar(UpgradeEngine upgrader, string nombre, ILogger logger)
    {
        if (!upgrader.IsUpgradeRequired())
        {
            logger.LogInformation("DbUp ({Nombre}): sin scripts pendientes.", nombre);
            return;
        }

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            logger.LogError(result.Error, "DbUp ({Nombre}): error al aplicar scripts.", nombre);
            throw new InvalidOperationException($"Fallo la migración de base de datos ({nombre}).", result.Error);
        }

        logger.LogInformation("DbUp ({Nombre}): scripts aplicados correctamente.", nombre);
    }

    /// <summary>
    /// Carga datos de PRUEBA (maestros) — sólo para desarrollo, mientras el Sync
    /// no existe. Usa NullJournal (corre en cada arranque) y los scripts son
    /// idempotentes (INSERT IGNORE), así que re-ejecutarlos es inofensivo.
    /// NO debe llamarse en producción.
    /// </summary>
    public static void RunDevSeed(string connectionString, string seedPath, ILogger logger)
    {
        if (!Directory.Exists(seedPath))
        {
            logger.LogWarning("Dev-seed: la carpeta '{Path}' no existe. Se omite.", seedPath);
            return;
        }

        var upgrader = DeployChanges.To
            .MySqlDatabase(connectionString)
            .WithScriptsFromFileSystem(seedPath, f => f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .JournalTo(new NullJournal())   // no registra: corre siempre
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            logger.LogError(result.Error, "Dev-seed: error al cargar datos de prueba.");
            throw new InvalidOperationException("Fallo el dev-seed.", result.Error);
        }

        logger.LogInformation("Dev-seed: datos de prueba cargados.");
    }
}
