using WidexPresupuestos.Shared.Models.DTOs;

namespace WidexPresupuestos.Shared.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    string HashPassword(string password);
}
