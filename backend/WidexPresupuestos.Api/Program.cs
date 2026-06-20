using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WidexPresupuestos.Api.Infrastructure;
using WidexPresupuestos.Api.Middleware;
using WidexPresupuestos.Api.Repositories;
using WidexPresupuestos.Api.Services;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Shared.Repositories;
using WidexPresupuestos.Shared.Services;

// Configurar Dapper para snake_case ↔ PascalCase
DapperConfig.Configure();

var builder = WebApplication.CreateBuilder(args);

// ── Servicios ─────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB connection factory (MySQL, scoped) — el connection string se resuelve aquí
// para no requerir IConfiguration en la class library Shared.
builder.Services.AddScoped<IDbConnectionFactory>(_ =>
    new MySqlConnectionFactory(
        builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection no configurada.")));

// Repositorios
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IArticuloRepository, ArticuloRepository>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IVendedorRepository, VendedorRepository>();

// Servicios
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT: validar la clave al arranque (fail-fast). Compose interpola una env var
// ausente como cadena vacía, así que '?? throw' no alcanza; validamos longitud.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key no configurada o demasiado corta: HS256 requiere al menos 32 caracteres (256 bits).");

// JWT Authentication (HS256, 8h — igual que antes)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// CORS — permite el frontend React en localhost:5173
builder.Services.AddCors(options =>
{
    var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    var allowedOrigins = configuredOrigins is { Length: > 0 }
        ? configuredOrigins
        : new[] { "http://localhost:5173" };

    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// ── DbUp: aplicar migraciones al iniciar ──────────────────
var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection no configurada.");
var configuredScriptsPath = app.Configuration["DbUp:ScriptsPath"];
var scriptsPath = string.IsNullOrWhiteSpace(configuredScriptsPath)
    ? Path.Combine(app.Environment.ContentRootPath, "..", "..", "database")
    : configuredScriptsPath;
scriptsPath = Path.GetFullPath(scriptsPath);

DbMigrator.Run(connectionString, scriptsPath, app.Logger);

// ── Pipeline ──────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Docker"))
    app.UseHttpsRedirection();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
