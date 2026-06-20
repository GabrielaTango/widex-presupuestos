using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using WidexPresupuestos.Api.Services;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Models.DTOs;
using WidexPresupuestos.Shared.Repositories;

namespace WidexPresupuestos.Tests.Unit;

/// <summary>
/// Tests de AuthService: hashing BCrypt y lógica de LoginAsync.
/// Sin base de datos — IUserRepository se mockea con NSubstitute.
/// </summary>
public class AuthServiceTests
{
    // Clave de 32+ chars requerida por HS256 (mismo largo mínimo que valida Program.cs).
    private const string TestJwtKey = "clave-de-test-con-32-chars-minimo!!";

    private static IConfiguration BuildConfig(string key = TestJwtKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]      = key,
                ["Jwt:Issuer"]   = "widex-test",
                ["Jwt:Audience"] = "widex-test"
            })
            .Build();

    private static AuthService BuildService(IUserRepository repo, IConfiguration? cfg = null) =>
        new(repo, cfg ?? BuildConfig());

    // ── HashPassword ─────────────────────────────────────────────────────────

    [Fact]
    public void HashPassword_ConPasswordCorrecta_DevuelveHashBCryptVerificable()
    {
        var svc = BuildService(Substitute.For<IUserRepository>());

        var hash = svc.HashPassword("MiPassword1!");

        BCrypt.Net.BCrypt.Verify("MiPassword1!", hash).Should().BeTrue();
    }

    [Fact]
    public void HashPassword_ConPasswordDistinta_VerifyDevuelveFalse()
    {
        var svc = BuildService(Substitute.For<IUserRepository>());

        var hash = svc.HashPassword("PasswordCorrecta1!");

        BCrypt.Net.BCrypt.Verify("PasswordIncorrecta!", hash).Should().BeFalse();
    }

    [Fact]
    public void HashPassword_LlamadasMultiples_DevuelveHashesDistintos()
    {
        // BCrypt usa salt aleatorio: dos hashes del mismo input no deben ser iguales.
        var svc = BuildService(Substitute.For<IUserRepository>());

        var h1 = svc.HashPassword("same");
        var h2 = svc.HashPassword("same");

        h1.Should().NotBe(h2);
    }

    // ── LoginAsync — usuario no encontrado ───────────────────────────────────

    [Fact]
    public async Task LoginAsync_UsuarioNoExiste_DevuelveNull()
    {
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsuarioAsync("noexiste").Returns((User?)null);
        var svc = BuildService(repo);

        var result = await svc.LoginAsync(new LoginRequest { Usuario = "noexiste", Password = "x" });

        result.Should().BeNull();
    }

    // ── LoginAsync — usuario inactivo ────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_UsuarioInactivo_DevuelveNull()
    {
        // GetByUsuarioAsync filtra activo=1 en SQL, así que si la fila existe con
        // activo=false la query no la devuelve → el repositorio retorna null.
        // El AuthService no duplica esa lógica; el mock reproduce el contrato.
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsuarioAsync("inactivo").Returns((User?)null);
        var svc = BuildService(repo);

        var result = await svc.LoginAsync(new LoginRequest { Usuario = "inactivo", Password = "cualquiera" });

        result.Should().BeNull();
    }

    // ── LoginAsync — password incorrecta ─────────────────────────────────────

    [Fact]
    public async Task LoginAsync_PasswordNoMatchea_DevuelveNull()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("PasswordCorrecta1!");
        var user = new User
        {
            Id = 1, Usuario = "jdoe", Nombre = "Juan", Mail = "j@w.com",
            Password = hash, Activo = true
        };
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsuarioAsync("jdoe").Returns(user);
        var svc = BuildService(repo);

        var result = await svc.LoginAsync(new LoginRequest { Usuario = "jdoe", Password = "PasswordMal!" });

        result.Should().BeNull();
    }

    // ── LoginAsync — credenciales correctas ──────────────────────────────────

    [Fact]
    public async Task LoginAsync_CredencialesCorrectas_DevuelveLoginResponseConTokenNoVacio()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Secreto123!");
        var user = new User
        {
            Id = 7, Usuario = "ana", Nombre = "Ana García", Mail = "ana@w.com",
            Password = hash, Activo = true
        };
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsuarioAsync("ana").Returns(user);
        var svc = BuildService(repo);

        var result = await svc.LoginAsync(new LoginRequest { Usuario = "ana", Password = "Secreto123!" });

        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrWhiteSpace();
        result.Usuario.Should().Be("ana");
        result.Nombre.Should().Be("Ana García");
        result.Mail.Should().Be("ana@w.com");
    }

    // ── JWT — claims y expiración ─────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_CredencialesCorrectas_JwtContieneClaimsEsperadosYExpiraEn8h()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Secreto123!");
        var user = new User
        {
            Id = 42, Usuario = "ven01", Nombre = "Vendedor Uno", Mail = "v@w.com",
            Password = hash, Activo = true
        };
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsuarioAsync("ven01").Returns(user);
        var cfg = BuildConfig();
        var svc = BuildService(repo, cfg);

        var before = DateTime.UtcNow;
        var result = await svc.LoginAsync(new LoginRequest { Usuario = "ven01", Password = "Secreto123!" });
        var after = DateTime.UtcNow;

        result.Should().NotBeNull();

        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));

        // Validar firma y estructura; ClockSkew=0 para ser estrictos.
        var principal = handler.ValidateToken(result!.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "widex-test",
            ValidAudience = "widex-test",
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        var jwt = (JwtSecurityToken)validatedToken;

        // Claims de identidad
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42");
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("ven01");
        principal.FindFirst("nombre")!.Value.Should().Be("Vendedor Uno");

        // Expiración: entre 7h 59m y 8h 01m desde ahora (margen de 1 min para CI lento).
        var expectedExpiry = before.AddHours(8);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    // ── JWT — clave ausente lanza excepción ───────────────────────────────────

    [Fact]
    public async Task LoginAsync_SinJwtKey_LanzaInvalidOperationException()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("x");
        var user = new User { Id = 1, Usuario = "u", Nombre = "U", Mail = "u@w.com", Password = hash, Activo = true };
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsuarioAsync("u").Returns(user);
        var cfgSinKey = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()) // sin Jwt:Key
            .Build();
        var svc = BuildService(repo, cfgSinKey);

        Func<Task> act = () => svc.LoginAsync(new LoginRequest { Usuario = "u", Password = "x" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JWT Key*");
    }
}
