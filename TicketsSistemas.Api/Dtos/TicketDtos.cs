using System.ComponentModel.DataAnnotations;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Dtos;

public class TicketCreateDto
{
    [Required, MaxLength(120)]
    public string Titulo { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    public Categoria Categoria { get; set; }

    [Required, MaxLength(80)]
    public string Solicitante { get; set; } = string.Empty;
}

public class TicketUpdateEstadoDto
{
    [Required]
    public Estado Estado { get; set; }
}

public class TicketUpdatePrioridadDto
{
    [Required]
    public Prioridad Prioridad { get; set; }
}

public class TicketResponseDto
{
    public int Id { get; set; }
    public string CodigoTicket { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Prioridad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public DateTime Creado { get; set; }
    public DateTime? Actualizado { get; set; }

    public static TicketResponseDto FromEntity(Ticket t) => new()
    {
        Id = t.Id,
        CodigoTicket = t.CodigoTicket,
        Titulo = t.Titulo,
        Descripcion = t.Descripcion,
        Categoria = t.Categoria.ToString(),
        Prioridad = t.Prioridad.ToString(),
        Estado = t.Estado.ToString(),
        Solicitante = t.Solicitante,
        Creado = t.Creado,
        Actualizado = t.Actualizado
    };
}
