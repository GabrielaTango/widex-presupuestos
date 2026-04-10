using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public interface IClienteRepository
{
    Task<IEnumerable<Cliente>> BuscarPacientesAsync(string? busqueda);
    Task<IEnumerable<Cliente>> GetObrasSocialesAsync();
}
