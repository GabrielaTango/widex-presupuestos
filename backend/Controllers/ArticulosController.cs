using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Api.Models.DTOs;
using WidexPresupuestos.Api.Repositories;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArticulosController : ControllerBase
{
    private readonly IArticuloRepository _articuloRepository;

    public ArticulosController(IArticuloRepository articuloRepository)
    {
        _articuloRepository = articuloRepository;
    }

    [HttpGet("listar/{idFolder}")]
    public async Task<IActionResult> Listar(string idFolder)
    {
        var articulos = await _articuloRepository.GetByFolderAsync(idFolder);
        return Ok(ApiResponse<object>.Ok(articulos));
    }

    [HttpGet("categorias/{id}")]
    public async Task<IActionResult> GetCategorias(string id)
    {
        var result = await _articuloRepository.GetCategoriasAsync(id);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("presupuesto")]
    public async Task<IActionResult> GetArticulosPresupuesto()
    {
        var articulos = await _articuloRepository.GetArticulosPresupuestoAsync();
        return Ok(ApiResponse<object>.Ok(articulos));
    }

    [HttpGet("talonarios")]
    public async Task<IActionResult> GetTalonarios()
    {
        var talonarios = await _articuloRepository.GetTalonariosAsync();
        return Ok(ApiResponse<object>.Ok(talonarios));
    }
}
