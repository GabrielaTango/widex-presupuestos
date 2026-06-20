namespace WidexPresupuestos.Shared.Models;

public class User
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? CodClient { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
