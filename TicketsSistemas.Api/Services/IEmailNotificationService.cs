using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Services;

public interface IEmailNotificationService
{
    Task NotificarTicketCreadoAsync(Ticket ticket, IReadOnlyCollection<string> destinatarios);
}
