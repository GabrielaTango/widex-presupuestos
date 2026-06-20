using WidexPresupuestos.Shared.Models;

namespace WidexPresupuestos.Shared.Repositories;

public interface IVendedorRepository
{
    Task<IEnumerable<Vendedor>> GetVendedoresSeleccionAsync();
    Task<IEnumerable<Vendedor>> GetVendedoresNoSeleccionAsync();
}
