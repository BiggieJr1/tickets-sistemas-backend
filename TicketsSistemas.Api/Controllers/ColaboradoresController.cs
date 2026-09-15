using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Dtos;
using TicketsSistemas.Api.Models;
using TicketsSistemas.Api.Services;

namespace TicketsSistemas.Api.Controllers;

[ApiController]
[Route("api/colaboradores")]
[Authorize]
public class ColaboradoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDirectoryService _directory;

    public ColaboradoresController(AppDbContext db, IDirectoryService directory)
    {
        _db = db;
        _directory = directory;
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

    // POST /api/colaboradores/importar-entra — trae todo el directorio de
    // Entra ID (cuentas @bisoft.com.mx habilitadas, sin invitados) y da de
    // alta como Colaborador no-admin/activo a quien no exista ya (por
    // correo). No toca a quien ya está dado de alta: no pisa el rol de
    // admin ni el estado activo/inactivo de nadie.
    [HttpPost("importar-entra")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ColaboradorImportResultDto>> ImportarDesdeEntra()
    {
        IReadOnlyList<DirectoryUsuario> usuarios;
        try
        {
            usuarios = await _directory.ObtenerUsuariosAsync();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var existentes = (await _db.Colaboradores.Select(c => c.Email).ToListAsync())
            .Select(e => e.ToLower())
            .ToHashSet();

        var nuevos = new List<Colaborador>();
        foreach (var u in usuarios)
        {
            if (!existentes.Add(u.Email)) continue; // ya existía o duplicado en el directorio

            nuevos.Add(new Colaborador
            {
                NombreCompleto = u.NombreCompleto,
                Email = u.Email,
                EsAdministrador = false,
                Activo = true,
            });
        }

        _db.Colaboradores.AddRange(nuevos);
        await _db.SaveChangesAsync();

        return Ok(new ColaboradorImportResultDto
        {
            Total = usuarios.Count,
            Importados = nuevos.Count,
            YaExistian = usuarios.Count - nuevos.Count,
        });
    }
}
