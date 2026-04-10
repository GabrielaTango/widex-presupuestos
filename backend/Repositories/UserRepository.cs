using Dapper;
using Microsoft.Data.SqlClient;
using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<User?> GetByUsuarioAsync(string usuario)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Usuarios WHERE Usuario = @Usuario AND Activo = 1",
            new { Usuario = usuario });
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Usuarios WHERE Id = @Id AND Activo = 1",
            new { Id = id });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<User>(
            "SELECT Id, Nombre, Mail, Usuario, Activo, FechaCreacion, FechaModificacion FROM Usuarios");
    }

    public async Task<int> CreateAsync(User user)
    {
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Usuarios (Nombre, Mail, Usuario, Password, Activo)
              VALUES (@Nombre, @Mail, @Usuario, @Password, @Activo);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", user);
    }

    public async Task UpdatePasswordAsync(int id, string hashedPassword)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Usuarios SET Password = @Password, FechaModificacion = GETDATE() WHERE Id = @Id",
            new { Id = id, Password = hashedPassword });
    }
}
