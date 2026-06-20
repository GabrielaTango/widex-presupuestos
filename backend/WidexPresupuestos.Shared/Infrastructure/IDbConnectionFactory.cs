using System.Data;

namespace WidexPresupuestos.Shared.Infrastructure;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
