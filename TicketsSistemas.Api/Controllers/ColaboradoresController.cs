using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Dtos;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Controllers;

[ApiController]
[Route("api/colaboradores")]
[Authorize]
public class ColaboradoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public ColaboradoresController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/colaboradores?soloActivos=true
    // soloActivos=true es lo que usa el selector "asignar a" de un ticket;
    // sin el filtro (para la pantalla de administración) trae a todos.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ColaboradorResponseDto>>> GetAll([FromQuery] bool? soloActivos)
    {
        var query = _db.Colaboradores.AsQueryable();
        if (soloActivos == true) query = query.Where(c => c.Activo);

        var colaboradores = await query
            .OrderBy(c => c.NombreCompleto)
            .ToListAsync();

        return Ok(colaboradores.Select(ColaboradorResponseDto.FromEntity));
    }

    // POST /api/colaboradores
    [HttpPost]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ColaboradorResponseDto>> Create(ColaboradorCreateDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        if (await _db.Colaboradores.AnyAsync(c => c.Email.ToLower() == email))
            return Conflict(new { message = "Ya existe un colaborador con ese correo." });

        var colaborador = new Colaborador
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            EsAdministrador = dto.EsAdministrador,
            Activo = true,
        };

        _db.Colaboradores.Add(colaborador);
        await _db.SaveChangesAsync();

        return Ok(ColaboradorResponseDto.FromEntity(colaborador));
    }

    // PATCH /api/colaboradores/5
    [HttpPatch("{id:int}")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ColaboradorResponseDto>> Update(int id, ColaboradorUpdateDto dto)
    {
        var colaborador = await _db.Colaboradores.FindAsync(id);
        if (colaborador is null) return NotFound();

        var email = dto.Email.Trim().ToLower();
        if (await _db.Colaboradores.AnyAsync(c => c.Id != id && c.Email.ToLower() == email))
            return Conflict(new { message = "Ya existe otro colaborador con ese correo." });

        colaborador.NombreCompleto = dto.NombreCompleto.Trim();
        colaborador.Email = email;
        colaborador.EsAdministrador = dto.EsAdministrador;
        colaborador.Activo = dto.Activo;
        colaborador.Actualizado = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ColaboradorResponseDto.FromEntity(colaborador));
    }

    // PATCH /api/colaboradores/5/password
    [HttpPatch("{id:int}/password")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> CambiarPassword(int id, CambiarPasswordDto dto)
    {
        var colaborador = await _db.Colaboradores.FindAsync(id);
        if (colaborador is null) return NotFound();

        colaborador.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        colaborador.Actualizado = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
