using WidexPresupuestos.Shared.Models;

namespace WidexPresupuestos.Shared.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsuarioAsync(string usuario);
    Task<User?> GetByIdAsync(long id);
    Task<IEnumerable<User>> GetAllAsync();
    Task<long> CreateAsync(User user);
    Task UpdatePasswordAsync(long id, string hashedPassword);
}
