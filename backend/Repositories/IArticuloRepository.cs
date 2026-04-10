using WidexPresupuestos.Api.Models;

namespace WidexPresupuestos.Api.Repositories;

public interface IArticuloRepository
{
    Task<IEnumerable<Articulo>> GetByFolderAsync(string idFolder);
    Task<object> GetCategoriasAsync(string id);
    Task<IEnumerable<ArticuloPresupuesto>> GetArticulosPresupuestoAsync();
    Task<IEnumerable<Talonario>> GetTalonariosAsync();
}
