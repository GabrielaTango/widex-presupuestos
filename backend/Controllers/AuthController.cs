using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Api.Models.DTOs;
using WidexPresupuestos.Api.Repositories;
using WidexPresupuestos.Api.Services;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;

    public AuthController(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (result == null)
            return Unauthorized(ApiResponse<LoginResponse>.Error("Usuario o contrasena incorrectos"));

        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login exitoso"));
    }

    [HttpPost("seed")]
    public async Task<ActionResult<ApiResponse<string>>> Seed()
    {
        var existing = await _userRepository.GetByUsuarioAsync("admin");
        if (existing != null)
        {
            // Actualizar password con hash BCrypt
            await _userRepository.UpdatePasswordAsync(existing.Id, _authService.HashPassword("Admin123!"));
            return Ok(ApiResponse<string>.Ok("Password del admin actualizado con hash BCrypt. Usuario: admin / Password: Admin123!"));
        }

        var user = new Models.User
        {
            Nombre = "Administrador",
            Mail = "admin@widex.com",
            Usuario = "admin",
            Password = _authService.HashPassword("Admin123!"),
            Activo = true
        };
        await _userRepository.CreateAsync(user);
        return Ok(ApiResponse<string>.Ok("Usuario admin creado. Usuario: admin / Password: Admin123!"));
    }
}
