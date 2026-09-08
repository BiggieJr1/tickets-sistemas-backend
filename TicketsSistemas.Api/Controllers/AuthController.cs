using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Dtos;
using TicketsSistemas.Api.Models;
using TicketsSistemas.Api.Services;

namespace TicketsSistemas.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        var colaborador = await _db.Colaboradores.FirstOrDefaultAsync(c => c.Email.ToLower() == email);

        if (colaborador is null || !colaborador.Activo ||
            !BCrypt.Net.BCrypt.Verify(dto.Password, colaborador.PasswordHash))
        {
            return Unauthorized(new { message = "Correo o contraseña incorrectos." });
        }

        var (token, expira) = _jwt.GenerarToken(colaborador);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Expira = expira,
            Colaborador = ColaboradorResponseDto.FromEntity(colaborador)
        });
    }

    // GET /api/auth/me
    [HttpGet("me")]
    public async Task<ActionResult<ColaboradorResponseDto>> Me()
    {
        var colaborador = await ColaboradorActualAsync();
        if (colaborador is null) return Unauthorized();
        return Ok(ColaboradorResponseDto.FromEntity(colaborador));
    }

    // PATCH /api/auth/me/password
    [HttpPatch("me/password")]
    public async Task<IActionResult> CambiarMiPassword(CambiarPasswordDto dto)
    {
        var colaborador = await ColaboradorActualAsync();
        if (colaborador is null) return Unauthorized();

        colaborador.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        colaborador.Actualizado = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<Colaborador?> ColaboradorActualAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (idClaim is null || !int.TryParse(idClaim, out var id)) return null;
        return await _db.Colaboradores.FindAsync(id);
    }
}
