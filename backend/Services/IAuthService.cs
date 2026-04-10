using WidexPresupuestos.Api.Models.DTOs;

namespace WidexPresupuestos.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    string HashPassword(string password);
}
