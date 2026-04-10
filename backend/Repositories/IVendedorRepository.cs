using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public interface IVendedorRepository
{
    Task<IEnumerable<Vendedor>> GetVendedoresSeleccionAsync();
    Task<IEnumerable<Vendedor>> GetVendedoresNoSeleccionAsync();
}
