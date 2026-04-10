namespace WidexPresupuestos.Api.Models;

public class Categoria
{
    public string IdFolder { get; set; } = string.Empty;
    public string Descrip { get; set; } = string.Empty;
    public string IdParent { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
