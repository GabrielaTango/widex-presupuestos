using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WidexPresupuestos.Api.Models.DTOs;
using WidexPresupuestos.Api.Repositories;

namespace WidexPresupuestos.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByUsuarioAsync(request.Usuario);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return null;

        var token = GenerateJwtToken(user.Id, user.Usuario, user.Nombre);

        return new LoginResponse
        {
            Token = token,
            Nombre = user.Nombre,
            Mail = user.Mail,
            Usuario = user.Usuario
        };
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private string GenerateJwtToken(int userId, string usuario, string nombre)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not configured")));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, usuario),
            new Claim("nombre", nombre)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
