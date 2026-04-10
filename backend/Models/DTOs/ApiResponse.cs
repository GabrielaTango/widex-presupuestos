namespace WidexPresupuestos.Api.Models.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Operacion exitosa")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Error(string message)
        => new() { Success = false, Message = message };
}
