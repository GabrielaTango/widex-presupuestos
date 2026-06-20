using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Models.DTOs;
using WidexPresupuestos.Shared.Repositories;
using WidexPresupuestos.Shared.Services;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, IUserRepository userRepository, IWebHostEnvironment env)
    {
        _authService = authService;
        _userRepository = userRepository;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(ApiResponse<LoginResponse>.Error("Usuario o contrasena incorrectos"));

        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login exitoso"));
    }

    // Bootstrap del usuario admin. Sólo disponible en Development: en producción
    // dejarlo abierto permitiría a cualquiera crear/pisar la cuenta admin.
    // El alta inicial en prod se hace vía 03_seed_data_mysql.sql.
    [HttpPost("seed")]
    public async Task<ActionResult<ApiResponse<string>>> Seed()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var existing = await _userRepository.GetByUsuarioAsync("admin");
        if (existing != null)
            return Ok(ApiResponse<string>.Ok("El usuario admin ya existe; no se modifica."));

        var user = new User
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
