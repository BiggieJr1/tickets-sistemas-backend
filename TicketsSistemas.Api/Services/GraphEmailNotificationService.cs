using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Services;

// Envía correos con Microsoft Graph (POST /users/{buzon}/sendMail), en vez de
// SMTP, para reusar el mismo App Registration de Entra ID que ya existe para
// el login (AzureAd:TenantId / AzureAd:ClientId). Solo hace falta agregarle
// el permiso de aplicación "Mail.Send" (con consentimiento de administrador)
// y un client secret nuevo — no hay que dar de alta una cuenta SMTP aparte.
//
// Config nueva, mismo patrón de variables de entorno que ConnectionStrings__Default:
//   Graph__ClientSecret  — secreto del App Registration, NUNCA en appsettings.json
//   Graph__SenderUpn     — buzón desde el que se envía (ej. admin@bisoft.com.mx);
//                          debe ser un buzón real de Microsoft 365 del tenant
//
// Si falta configuración (ej. en desarrollo local), no lanza excepción: solo
// registra un warning y no envía nada. Un correo que no sale no debe tumbar
// la creación de un ticket.
public class GraphEmailNotificationService : IEmailNotificationService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GraphEmailNotificationService> _logger;

    public GraphEmailNotificationService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<GraphEmailNotificationService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotificarTicketCreadoAsync(Ticket ticket, IReadOnlyCollection<string> destinatarios)
    {
        if (destinatarios.Count == 0)
        {
            _logger.LogInformation(
                "Ticket {Codigo} creado sin notificar por correo: no hay administradores activos con correo.",
                ticket.CodigoTicket);
            return;
        }

        var asunto = $"[{ticket.CodigoTicket}] Nuevo ticket: {ticket.Titulo}";
        var cuerpo = $"""
            <p>Se registró un nuevo ticket.</p>
            <ul>
              <li><strong>Código:</strong> {ticket.CodigoTicket}</li>
              <li><strong>Título:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Titulo)}</li>
              <li><strong>Categoría:</strong> {ticket.Categoria}</li>
              <li><strong>Solicitante:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Solicitante)}</li>
            </ul>
            <p>{System.Net.WebUtility.HtmlEncode(ticket.Descripcion)}</p>
            """;

        await EnviarAsync(destinatarios, asunto, cuerpo);
    }

    private async Task EnviarAsync(IReadOnlyCollection<string> destinatarios, string asunto, string cuerpoHtml)
    {
        var tenantId = _config["AzureAd:TenantId"];
        var clientId = _config["AzureAd:ClientId"];
        var clientSecret = _config["Graph:ClientSecret"];
        var senderUpn = _config["Graph:SenderUpn"];

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(senderUpn))
        {
            _logger.LogWarning(
                "Notificaciones por correo desactivadas: falta configurar Graph:ClientSecret y/o Graph:SenderUpn.");
            return;
        }

        try
        {
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            var payload = new
            {
                message = new
                {
                    subject = asunto,
                    body = new { contentType = "HTML", content = cuerpoHtml },
                    toRecipients = destinatarios.Select(email => new { emailAddress = new { address = email } })
                },
                saveToSentItems = false
            };

            var response = await http.PostAsJsonAsync(
                $"https://graph.microsoft.com/v1.0/users/{senderUpn}/sendMail", payload);

            if (!response.IsSuccessStatusCode)
            {
                var detalle = await response.Content.ReadAsStringAsync();
                _logger.LogError("Graph sendMail falló ({Status}): {Detalle}", response.StatusCode, detalle);
            }
        }
        catch (Exception ex)
        {
            // Un correo que falla no debe tumbar la operación que lo disparó.
            _logger.LogError(ex, "Error enviando notificación por correo.");
        }
    }
}
