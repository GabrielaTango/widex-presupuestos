using System.Data;

namespace WidexPresupuestos.Sync.Infrastructure;

/// <summary>
/// Factory de conexión a Tango (SQL Server / MSSQL).
/// Solo existe en el proyecto Sync; la Api nunca referencia Tango.
/// </summary>
public interface ITangoConnectionFactory
{
    IDbConnection CreateConnection();
}
