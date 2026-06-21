using Dapper;
using WidexPresupuestos.Shared.Infrastructure;
using WidexPresupuestos.Sync;
using WidexPresupuestos.Sync.Config;
using WidexPresupuestos.Sync.Infrastructure;
using WidexPresupuestos.Sync.Services;

// snake_case ↔ PascalCase sin alias para Dapper
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuración ─────────────────────────────────────────────────────────
builder.Services.Configure<SyncOptions>(
    builder.Configuration.GetSection(SyncOptions.SectionName));

// ── Conexión Tango (SQL Server) ────────────────────────────────────────────
var tangoCs = builder.Configuration.GetConnectionString("TangoConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:TangoConnection.");
builder.Services.AddSingleton<ITangoConnectionFactory>(
    _ => new TangoConnectionFactory(tangoCs));

// ── Conexión MySQL (Shared) ────────────────────────────────────────────────
var mysqlCs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");
builder.Services.AddSingleton<IDbConnectionFactory>(
    _ => new MySqlConnectionFactory(mysqlCs));

// ── Infra del Sync ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<SyncStateRepository>();
builder.Services.AddSingleton<SyncLogRepository>();

// ── Servicios de entidades (cada uno como EntitySyncService → el Worker los recorre) ──
builder.Services.AddSingleton<EntitySyncService, ClientesSyncService>();
builder.Services.AddSingleton<EntitySyncService, VendedoresSyncService>();

// ── Worker ─────────────────────────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
