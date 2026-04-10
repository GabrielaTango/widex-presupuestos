namespace WidexPresupuestos.Api.Models;

public class Articulo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public decimal Stock { get; set; }
}

public class ArticuloPresupuesto
{
    public string CodArticu { get; set; } = string.Empty;
    public string Descripcio { get; set; } = string.Empty;
    public string DescAdic { get; set; } = string.Empty;
    public string CodBarra { get; set; } = string.Empty;
    public bool CoberturaAplicable { get; set; }
    public string CodArticuDif { get; set; } = string.Empty;
}
