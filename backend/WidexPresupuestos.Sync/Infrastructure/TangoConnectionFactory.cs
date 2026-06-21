using System.Data;
using Microsoft.Data.SqlClient;

namespace WidexPresupuestos.Sync.Infrastructure;

public sealed class TangoConnectionFactory : ITangoConnectionFactory
{
    private readonly string _connectionString;

    public TangoConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La cadena de conexión TangoConnection no puede estar vacía.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
