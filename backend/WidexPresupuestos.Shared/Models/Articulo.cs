namespace WidexPresupuestos.Shared.Models;

// Usado por pedidos: listar por carpeta/path
public class Articulo
{
    public long Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal Stock { get; set; }
}

// Usado por presupuestos
public class ArticuloPresupuesto
{
    public string CodArticu { get; set; } = string.Empty;
    public string Descripcio { get; set; } = string.Empty;
    public string? DescAdic { get; set; }
    public string? CodBarra { get; set; }
    public bool CoberturaAplicable { get; set; }
    public string? CodArticuDif { get; set; }
}
