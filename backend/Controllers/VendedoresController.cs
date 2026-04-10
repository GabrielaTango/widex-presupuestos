using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Api.Models.DTOs;
using WidexPresupuestos.Api.Repositories;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VendedoresController : ControllerBase
{
    private readonly IVendedorRepository _vendedorRepository;

    public VendedoresController(IVendedorRepository vendedorRepository)
    {
        _vendedorRepository = vendedorRepository;
    }

    [HttpGet("seleccion")]
    public async Task<IActionResult> GetVendedoresSeleccion()
    {
        var vendedores = await _vendedorRepository.GetVendedoresSeleccionAsync();
        return Ok(ApiResponse<object>.Ok(vendedores));
    }

    [HttpGet("no-seleccion")]
    public async Task<IActionResult> GetVendedoresNoSeleccion()
    {
        var vendedores = await _vendedorRepository.GetVendedoresNoSeleccionAsync();
        return Ok(ApiResponse<object>.Ok(vendedores));
    }
}
