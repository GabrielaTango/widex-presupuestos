using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Shared.Models;
using WidexPresupuestos.Shared.Models.DTOs;
using WidexPresupuestos.Shared.Repositories;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
        => _userRepository = userRepository;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<User>>>> GetAll()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<User>>.Ok(users));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<User>>> GetById(long id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound(ApiResponse<User>.Error("Usuario no encontrado"));

        return Ok(ApiResponse<User>.Ok(user));
    }
}
