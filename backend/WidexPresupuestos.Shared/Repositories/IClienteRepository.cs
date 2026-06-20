using WidexPresupuestos.Shared.Models;

namespace WidexPresupuestos.Shared.Repositories;

public interface IClienteRepository
{
    Task<IEnumerable<Cliente>> BuscarPacientesAsync(string? busqueda);
    Task<IEnumerable<Cliente>> GetObrasSocialesAsync();
}
