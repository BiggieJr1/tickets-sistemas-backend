using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Dtos;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/auth/me
    // El login en sí ya pasó en Microsoft (Entra ID) antes de llegar aquí:
    // este endpoint solo devuelve el perfil local (id, admin, activo) que
    // Microsoft no conoce, a partir del colaborador que Program.cs ya
    // resolvió y adjuntó como claims durante la validación del token.
    [HttpGet("me")]
    public async Task<ActionResult<ColaboradorResponseDto>> Me()
    {
        var colaborador = await ColaboradorActualAsync();
        if (colaborador is null) return Unauthorized();
        return Ok(ColaboradorResponseDto.FromEntity(colaborador));
    }

    private async Task<Colaborador?> ColaboradorActualAsync()
    {
        var idClaim = User.FindFirstValue(ClaimesColaborador.ColaboradorId);
        if (idClaim is null || !int.TryParse(idClaim, out var id)) return null;
        return await _db.Colaboradores.FindAsync(id);
    }
}
