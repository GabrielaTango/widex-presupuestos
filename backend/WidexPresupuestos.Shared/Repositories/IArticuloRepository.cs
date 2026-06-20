using WidexPresupuestos.Shared.Models;

namespace WidexPresupuestos.Shared.Repositories;

public interface IArticuloRepository
{
    Task<IEnumerable<Articulo>> GetByFolderAsync(string path);
    Task<object> GetCategoriasAsync(string id);
    Task<IEnumerable<ArticuloPresupuesto>> GetArticulosPresupuestoAsync();
    Task<IEnumerable<Talonario>> GetTalonariosAsync();
}
