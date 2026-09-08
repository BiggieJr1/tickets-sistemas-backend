namespace TicketsSistemas.Api.Models;

public class Ticket
{
    public int Id { get; set; }

    // Código visible tipo SIS-0001, se genera al crear el ticket
    public string CodigoTicket { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public Categoria Categoria { get; set; }
    public Prioridad Prioridad { get; set; }
    public Estado Estado { get; set; } = Estado.Abierto;

    public string Solicitante { get; set; } = string.Empty;

    public DateTime Creado { get; set; } = DateTime.UtcNow;
    public DateTime? Actualizado { get; set; }
}
