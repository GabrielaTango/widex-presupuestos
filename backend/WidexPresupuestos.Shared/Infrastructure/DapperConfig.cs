using Dapper;

namespace WidexPresupuestos.Shared.Infrastructure;

public static class DapperConfig
{
    public static void Configure()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
