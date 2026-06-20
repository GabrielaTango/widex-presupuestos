namespace WidexPresupuestos.Shared.Models;

public class Cliente
{
    public string CodClient { get; set; } = string.Empty;
    public string RazonSoci { get; set; } = string.Empty;
    public string? Cuit { get; set; }
    public string? NroCarnet { get; set; }
    public string? ObraSocial { get; set; }
}
