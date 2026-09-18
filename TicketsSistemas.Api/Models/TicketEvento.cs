namespace TicketsSistemas.Api.Models;

public enum TipoEventoTicket
{
    CambioEstado,
    CambioPrioridad,
    CambioAsignacion,
    Comentario
}

// Línea de tiempo de un ticket: junta los cambios automáticos (estado,
// prioridad, asignación — antes solo se guardaba el último en el propio
// Ticket) y los comentarios manuales de un admin (avance/resolución). Nunca
// se edita ni se borra un evento, es un log.
public class TicketEvento
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public TipoEventoTicket Tipo { get; set; }

    // Quién generó el evento: quien hizo el PATCH, o quien escribió el
    // comentario. Nullable por si el colaborador se borra más adelante.
    public int? ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }

    // Para los cambios automáticos: valores ya en texto, listos para
    // mostrar (ej. "Abierto" / "En progreso"). Null para Comentario.
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }

    // Para Comentario. Null en los eventos automáticos.
    public string? Texto { get; set; }

    public DateTime Creado { get; set; } = DateTime.UtcNow;
}
