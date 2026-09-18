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
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailNotificationService _email;

    public TicketsController(AppDbContext db, IEmailNotificationService email)
    {
        _db = db;
        _email = email;
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

    // Incluye los nombres de "asignado a" / "actualizado por" en la
    // respuesta (TicketResponseDto.FromEntity los necesita cargados).
    private IQueryable<Ticket> TicketsConNombres() =>
        _db.Tickets.Include(t => t.AsignadoA).Include(t => t.ActualizadoPor);

    // GET /api/tickets?categoria=&prioridad=&estado=&asignadoAId=&search=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketResponseDto>>> GetAll(
        [FromQuery] Categoria? categoria,
        [FromQuery] Prioridad? prioridad,
        [FromQuery] Estado? estado,
        [FromQuery] int? asignadoAId,
        [FromQuery] string? search)
    {
        var query = TicketsConNombres();

        if (categoria.HasValue) query = query.Where(t => t.Categoria == categoria);
        if (prioridad.HasValue) query = query.Where(t => t.Prioridad == prioridad);
        if (estado.HasValue) query = query.Where(t => t.Estado == estado);
        if (asignadoAId.HasValue) query = query.Where(t => t.AsignadoAId == asignadoAId);
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
        var ticket = await TicketsConNombres().FirstOrDefaultAsync(t => t.Id == id);
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

        var correosAdmins = await _db.Colaboradores
            .Where(c => c.EsAdministrador && c.Activo)
            .Select(c => c.Email)
            .ToListAsync();
        await _email.NotificarTicketCreadoAsync(ticket, correosAdmins);

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, TicketResponseDto.FromEntity(ticket));
    }

    // PATCH /api/tickets/5/estado — solo administradores: un colaborador
    // regular no debe poder cerrar o reabrir tickets ajenos.
    [HttpPatch("{id:int}/estado")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<TicketResponseDto>> UpdateEstado(int id, TicketUpdateEstadoDto dto)
    {
        var ticket = await TicketsConNombres().FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        var estadoAnterior = ticket.Estado;
        ticket.Estado = dto.Estado;
        MarcarActualizado(ticket);
        RegistrarEvento(ticket.Id, TipoEventoTicket.CambioEstado, estadoAnterior.ToString(), dto.Estado.ToString());
        await _db.SaveChangesAsync();

        return Ok(TicketResponseDto.FromEntity(ticket));
    }

    // PATCH /api/tickets/5/prioridad — solo administradores: es quien
    // triagea y decide qué tan urgente es cada ticket.
    [HttpPatch("{id:int}/prioridad")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<TicketResponseDto>> UpdatePrioridad(int id, TicketUpdatePrioridadDto dto)
    {
        var ticket = await TicketsConNombres().FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        var prioridadAnterior = ticket.Prioridad;
        ticket.Prioridad = dto.Prioridad;
        MarcarActualizado(ticket);
        RegistrarEvento(ticket.Id, TipoEventoTicket.CambioPrioridad, prioridadAnterior.ToString(), dto.Prioridad.ToString());
        await _db.SaveChangesAsync();

        return Ok(TicketResponseDto.FromEntity(ticket));
    }

    // PATCH /api/tickets/5/asignacion — ColaboradorId null desasigna.
    // Solo administradores: se detectó en pruebas que cualquier colaborador
    // logueado podía reasignar tickets ajenos, no solo los propios.
    [HttpPatch("{id:int}/asignacion")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<TicketResponseDto>> UpdateAsignacion(int id, TicketUpdateAsignacionDto dto)
    {
        var ticket = await TicketsConNombres().FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        // Solo se puede asignar a colaboradores administradores: son quienes
        // dan seguimiento a los tickets, no cualquier colaborador dado de alta.
        if (dto.ColaboradorId.HasValue &&
            !await _db.Colaboradores.AnyAsync(c => c.Id == dto.ColaboradorId && c.Activo && c.EsAdministrador))
        {
            return BadRequest(new { message = "El colaborador no existe, está desactivado o no es administrador." });
        }

        var nombreAnterior = ticket.AsignadoA?.NombreCompleto ?? "Sin asignar";

        ticket.AsignadoAId = dto.ColaboradorId;
        MarcarActualizado(ticket);

        // Recargar para traer el nombre del nuevo AsignadoA (para el evento
        // y la respuesta).
        await _db.Entry(ticket).Reference(t => t.AsignadoA).LoadAsync();
        var nombreNuevo = ticket.AsignadoA?.NombreCompleto ?? "Sin asignar";

        RegistrarEvento(ticket.Id, TipoEventoTicket.CambioAsignacion, nombreAnterior, nombreNuevo);
        await _db.SaveChangesAsync();

        return Ok(TicketResponseDto.FromEntity(ticket));
    }

    // GET /api/tickets/5/historial — cualquier colaborador logueado puede
    // ver la línea de tiempo (cambios automáticos + comentarios), aunque
    // solo un admin pueda escribir comentarios.
    [HttpGet("{id:int}/historial")]
    public async Task<ActionResult<IEnumerable<TicketEventoResponseDto>>> GetHistorial(int id)
    {
        if (!await _db.Tickets.AnyAsync(t => t.Id == id)) return NotFound();

        var eventos = await _db.TicketEventos
            .Include(e => e.Colaborador)
            .Where(e => e.TicketId == id)
            .OrderBy(e => e.Creado)
            .ToListAsync();

        return Ok(eventos.Select(TicketEventoResponseDto.FromEntity));
    }

    // POST /api/tickets/5/comentarios — solo administradores: son quienes
    // resuelven y dan seguimiento a los tickets, no cualquier colaborador.
    [HttpPost("{id:int}/comentarios")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<TicketEventoResponseDto>> CrearComentario(int id, TicketComentarioCreateDto dto)
    {
        if (!await _db.Tickets.AnyAsync(t => t.Id == id)) return NotFound();

        var evento = new TicketEvento
        {
            TicketId = id,
            Tipo = TipoEventoTicket.Comentario,
            ColaboradorId = ColaboradorIdActual(),
            Texto = dto.Texto.Trim(),
        };
        _db.TicketEventos.Add(evento);
        await _db.SaveChangesAsync();

        // Recargar con el nombre del colaborador para la respuesta.
        await _db.Entry(evento).Reference(e => e.Colaborador).LoadAsync();
        return Ok(TicketEventoResponseDto.FromEntity(evento));
    }

    // DELETE /api/tickets/5 — solo administradores: es irreversible, más
    // sensible que asignar o cambiar estado/prioridad.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> Delete(int id)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private void MarcarActualizado(Ticket ticket)
    {
        ticket.Actualizado = DateTime.UtcNow;
        ticket.ActualizadoPorId = ColaboradorIdActual();
    }

    // No hace SaveChangesAsync: se agrega al mismo change tracker que el
    // update del ticket, para que ambos queden en una sola transacción.
    private void RegistrarEvento(int ticketId, TipoEventoTicket tipo, string? valorAnterior, string? valorNuevo)
    {
        _db.TicketEventos.Add(new TicketEvento
        {
            TicketId = ticketId,
            Tipo = tipo,
            ColaboradorId = ColaboradorIdActual(),
            ValorAnterior = valorAnterior,
            ValorNuevo = valorNuevo,
        });
    }

    private int? ColaboradorIdActual()
    {
        var idClaim = User.FindFirstValue(ClaimesColaborador.ColaboradorId);
        return idClaim is not null && int.TryParse(idClaim, out var id) ? id : null;
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
