using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WidexPresupuestos.Api.Models.DTOs;
using WidexPresupuestos.Api.Models;
using WidexPresupuestos.Api.Repositories;

namespace WidexPresupuestos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<User>>>> GetAll()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<User>>.Ok(users));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<User>>> GetById(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound(ApiResponse<User>.Error("Usuario no encontrado"));

        return Ok(ApiResponse<User>.Ok(user));
    }
}
