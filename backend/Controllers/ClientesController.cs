using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Api.Models.DTOs;
using WidexPresupuestos.Api.Repositories;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly IClienteRepository _clienteRepository;

    public ClientesController(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository;
    }

    [HttpGet("pacientes")]
    public async Task<IActionResult> BuscarPacientes([FromQuery] string? busqueda)
    {
        var pacientes = await _clienteRepository.BuscarPacientesAsync(busqueda);
        return Ok(ApiResponse<object>.Ok(pacientes));
    }

    [HttpGet("obras-sociales")]
    public async Task<IActionResult> GetObrasSociales()
    {
        var obrasSociales = await _clienteRepository.GetObrasSocialesAsync();
        return Ok(ApiResponse<object>.Ok(obrasSociales));
    }
}
