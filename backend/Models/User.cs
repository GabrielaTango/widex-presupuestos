namespace WidexPresupuestos.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
