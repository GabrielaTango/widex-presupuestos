using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsuarioAsync(string usuario);
    Task<User?> GetByIdAsync(int id);
    Task<IEnumerable<User>> GetAllAsync();
    Task<int> CreateAsync(User user);
    Task UpdatePasswordAsync(int id, string hashedPassword);
}
