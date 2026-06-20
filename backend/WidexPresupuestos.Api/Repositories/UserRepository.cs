using Dapper;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Repositories;

namespace WidexPresupuestos.Api.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _db;

    public UserRepository(IDbConnectionFactory db) => _db = db;

    public async Task<User?> GetByUsuarioAsync(string usuario)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            @"SELECT id, nombre, mail, usuario, password, cod_client, activo,
                     fecha_creacion, fecha_modificacion
              FROM usuarios
              WHERE usuario = @Usuario AND activo = 1",
            new { Usuario = usuario });
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            @"SELECT id, nombre, mail, usuario, password, cod_client, activo,
                     fecha_creacion, fecha_modificacion
              FROM usuarios
              WHERE id = @Id AND activo = 1",
            new { Id = id });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<User>(
            @"SELECT id, nombre, mail, usuario, cod_client, activo,
                     fecha_creacion, fecha_modificacion
              FROM usuarios");
    }

    public async Task<long> CreateAsync(User user)
    {
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO usuarios (nombre, mail, usuario, password, cod_client, activo, fecha_creacion)
              VALUES (@Nombre, @Mail, @Usuario, @Password, @CodClient, @Activo, NOW());
              SELECT LAST_INSERT_ID();",
            user);
    }

    public async Task UpdatePasswordAsync(long id, string hashedPassword)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE usuarios SET password = @Password, fecha_modificacion = NOW() WHERE id = @Id",
            new { Id = id, Password = hashedPassword });
    }
}
