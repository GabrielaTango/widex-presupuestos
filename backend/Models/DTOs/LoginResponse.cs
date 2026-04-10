namespace WidexPresupuestos.Api.Models.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}
