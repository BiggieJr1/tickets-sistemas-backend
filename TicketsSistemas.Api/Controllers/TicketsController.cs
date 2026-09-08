using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Dtos;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TicketsController(AppDbContext db)
    {
        _db = db;
    }

    private static readonly Dictionary<Prioridad, int> OrdenPrioridad = new()
    {
        // Sin asignar queda primero: son los tickets que el equipo todavía
        // no ha revisado para darles una prioridad real.
        [Prioridad.SinAsignar] = -1,
        [Prioridad.Critica] = 0,
        [Prioridad.Alta] = 1,
        [Prioridad.Media] = 2,
        [Prioridad.Baja] = 3
    };

    // GET /api/tickets?categoria=&prioridad=&estado=&search=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketResponseDto>>> GetAll(
        [FromQuery] Categoria? categoria,
        [FromQuery] Prioridad? prioridad,
        [FromQuery] Estado? estado,
        [FromQuery] string? search)
    {
        var query = _db.Tickets.AsQueryable();

        if (categoria.HasValue) query = query.Where(t => t.Categoria == categoria);
        if (prioridad.HasValue) query = query.Where(t => t.Prioridad == prioridad);
        if (estado.HasValue) query = query.Where(t => t.Estado == estado);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // ToLower() en ambos lados para que la búsqueda sea case-insensitive
            // igual en SQLite que en Postgres (con Postgres, Contains() solo,
            // sin ToLower, es sensible a mayúsculas/minúsculas).
            var searchLower = search.ToLower();
            query = query.Where(t => t.Titulo.ToLower().Contains(searchLower));
        }

        var tickets = await query.ToListAsync();

        var ordenados = tickets
            .OrderBy(t => OrdenPrioridad[t.Prioridad])
            .ThenByDescending(t => t.Creado)
            .Select(TicketResponseDto.FromEntity);

        return Ok(ordenados);
    }

    // GET /api/tickets/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketResponseDto>> GetById(int id)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();
        return Ok(TicketResponseDto.FromEntity(ticket));
    }

    // POST /api/tickets
    [HttpPost]
    public async Task<ActionResult<TicketResponseDto>> Create(TicketCreateDto dto)
    {
        var codigo = await GenerarCodigoAsync();

        var ticket = new Ticket
        {
            CodigoTicket = codigo,
            Titulo = dto.Titulo.Trim(),
            Descripcion = dto.Descripcion.Trim(),
            Categoria = dto.Categoria,
            Prioridad = Prioridad.SinAsignar,
            Estado = Estado.Abierto,
            Solicitante = dto.Solicitante.Trim(),
            Creado = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, TicketResponseDto.FromEntity(ticket));
    }

    // PATCH /api/tickets/5/estado
    [HttpPatch("{id:int}/estado")]
    public async Task<ActionResult<TicketResponseDto>> UpdateEstado(int id, TicketUpdateEstadoDto dto)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();

        ticket.Estado = dto.Estado;
        ticket.Actualizado = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(TicketResponseDto.FromEntity(ticket));
    }

    // PATCH /api/tickets/5/prioridad
    [HttpPatch("{id:int}/prioridad")]
    public async Task<ActionResult<TicketResponseDto>> UpdatePrioridad(int id, TicketUpdatePrioridadDto dto)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();

        ticket.Prioridad = dto.Prioridad;
        ticket.Actualizado = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(TicketResponseDto.FromEntity(ticket));
    }

    // DELETE /api/tickets/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerarCodigoAsync()
    {
        // Uso interno / bajo volumen: suficiente con max(id)+1.
        // Si en el futuro hay alta concurrencia, mover a una secuencia de BD.
        var maxId = await _db.Tickets.OrderByDescending(t => t.Id)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync() ?? 0;

        return $"SIS-{(maxId + 1):D4}";
    }
}
