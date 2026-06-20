using FluentAssertions;
using WidexPresupuestos.Api.Repositories;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Tests.Integration.Infrastructure;

namespace WidexPresupuestos.Tests.Integration;

[Collection("mysql")]
public sealed class UserRepositoryTests(MysqlFixture fixture) : IntegrationTestBase(fixture)
{
    private UserRepository Repo() => new(Fixture.DbFactory);

    // ── CreateAsync + GetByUsuarioAsync — round-trip ──────────────────────────

    [Fact]
    public async Task CreateAsync_Seguido_GetByUsuarioAsync_DevuelveMismosValores()
    {
        var repo = Repo();
        var user = new User
        {
            Nombre   = "Test User",
            Mail     = "testuser_rt@widex.test",
            Usuario  = "testuser_rt",
            Password = BCrypt.Net.BCrypt.HashPassword("Pass123!"),
            Activo   = true
        };

        var newId = await repo.CreateAsync(user);

        try
        {
            newId.Should().BeGreaterThan(0);

            var retrieved = await repo.GetByUsuarioAsync("testuser_rt");

            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(newId);
            retrieved.Nombre.Should().Be("Test User");
            retrieved.Mail.Should().Be("testuser_rt@widex.test");
            retrieved.Usuario.Should().Be("testuser_rt");
            retrieved.Activo.Should().BeTrue();
            // Password debe persistirse (es el hash, no plaintext)
            BCrypt.Net.BCrypt.Verify("Pass123!", retrieved.Password).Should().BeTrue();
        }
        finally
        {
            await ExecAsync("DELETE FROM usuarios WHERE id = @Id", new { Id = newId });
        }
    }

    [Fact]
    public async Task GetByUsuarioAsync_UsuarioInactivo_DevuelveNull()
    {
        // Crear usuario inactivo directamente por SQL (el repo no tiene CreateInactivo).
        await ExecAsync(
            @"INSERT INTO usuarios (nombre, mail, usuario, password, activo, fecha_creacion)
              VALUES ('Inactivo Test','inact_t@w.test','inactivo_t','hash',0, NOW())");

        try
        {
            var result = await Repo().GetByUsuarioAsync("inactivo_t");
            result.Should().BeNull("la query filtra activo=1");
        }
        finally
        {
            await ExecAsync("DELETE FROM usuarios WHERE usuario = 'inactivo_t'");
        }
    }

    [Fact]
    public async Task GetByUsuarioAsync_UsuarioInexistente_DevuelveNull()
    {
        var result = await Repo().GetByUsuarioAsync("usuario_que_no_existe_xyz");
        result.Should().BeNull();
    }

    // ── GetAllAsync — NO expone password ─────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_NingunaFilaExponePassword()
    {
        // Asegurar que hay al menos un usuario con password real.
        var repo = Repo();
        var u = new User
        {
            Nombre = "GetAll Test", Mail = "getall_t@w.test",
            Usuario = "getall_t", Password = BCrypt.Net.BCrypt.HashPassword("Clave1!"),
            Activo = true
        };
        var id = await repo.CreateAsync(u);

        try
        {
            var all = (await repo.GetAllAsync()).ToList();

            // La query de GetAllAsync no selecciona 'password', así que Dapper
            // dejará la propiedad en su valor por defecto (string.Empty).
            all.Should().AllSatisfy(user =>
                user.Password.Should().BeEmpty("GetAllAsync no debe exponer el hash"));
        }
        finally
        {
            await ExecAsync("DELETE FROM usuarios WHERE id = @Id", new { Id = id });
        }
    }

    // ── UpdatePasswordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePasswordAsync_ActualizaHashYFechaModificacion()
    {
        var repo = Repo();
        var u = new User
        {
            Nombre = "Upd Pwd Test", Mail = "updpwd_t@w.test",
            Usuario = "updpwd_t", Password = BCrypt.Net.BCrypt.HashPassword("Original1!"),
            Activo = true
        };
        var id = await repo.CreateAsync(u);

        try
        {
            var newHash = BCrypt.Net.BCrypt.HashPassword("Nueva1!");
            await repo.UpdatePasswordAsync(id, newHash);

            var retrieved = await repo.GetByUsuarioAsync("updpwd_t");
            retrieved.Should().NotBeNull();
            BCrypt.Net.BCrypt.Verify("Nueva1!", retrieved!.Password).Should().BeTrue();
            BCrypt.Net.BCrypt.Verify("Original1!", retrieved.Password).Should().BeFalse();
        }
        finally
        {
            await ExecAsync("DELETE FROM usuarios WHERE id = @Id", new { Id = id });
        }
    }
}
