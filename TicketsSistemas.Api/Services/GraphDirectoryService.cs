using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;

namespace TicketsSistemas.Api.Services;

// Trae el directorio de Entra ID vía Microsoft Graph (GET /users), para el
// alta masiva de Colaboradores (ver ColaboradoresController.ImportarDesdeEntra).
// Reusa el mismo App Registration y client secret que GraphEmailNotificationService
// (AzureAd:TenantId/ClientId + Graph:ClientSecret) — solo hace falta agregarle
// el permiso de aplicación "User.Read.All" (con consentimiento de administrador),
// el mismo trámite que ya se hizo para Mail.Send.
//
// A diferencia del envío de correo (que no debe tumbar la creación de un
// ticket si falla), esta operación la dispara un admin a propósito, así que
// aquí sí se lanza una excepción clara si falta configuración o Graph
// rechaza la llamada, en vez de fallar en silencio.
public class GraphDirectoryService : IDirectoryService
{
    private const string DominioPermitido = "@bisoft.com.mx";

    // El directorio trae también cuentas de servicio/administración
    // (ej. "Administrador", "adminsp2010", "admintf") mezcladas con personas
    // reales — Graph no distingue una de otra. Se acepta solo lo que parece
    // un nombre de persona: "nombre.apellido" (con punto) o un solo nombre
    // con una letra pegada al final (ej. "alejandrou" = Alejandro + inicial
    // del apellido, patrón real usado en la empresa) — nada de dígitos ni
    // guiones, y nunca algo que empiece con "admin".
    private static readonly Regex PatronNombre =
        new(@"^[a-záéíóúñü]+(\.[a-záéíóúñü]+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GraphDirectoryService> _logger;

    public GraphDirectoryService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<GraphDirectoryService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DirectoryUsuario>> ObtenerUsuariosAsync()
    {
        var tenantId = _config["AzureAd:TenantId"];
        var clientId = _config["AzureAd:ClientId"];
        var clientSecret = _config["Graph:ClientSecret"];

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Falta configurar Graph:ClientSecret para poder leer el directorio de Entra ID.");
        }

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var usuarios = new List<DirectoryUsuario>();
        var url = "https://graph.microsoft.com/v1.0/users" +
                  "?$select=displayName,mail,userPrincipalName,accountEnabled,userType" +
                  "&$top=999";

        while (url is not null)
        {
            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var detalle = await response.Content.ReadAsStringAsync();
                _logger.LogError("Graph /users falló ({Status}): {Detalle}", response.StatusCode, detalle);
                throw new InvalidOperationException(
                    $"Microsoft Graph rechazó la consulta del directorio ({(int)response.StatusCode}).");
            }

            var pagina = await response.Content.ReadFromJsonAsync<GraphUsersResponse>();
            foreach (var u in pagina?.Value ?? [])
            {
                if (!u.AccountEnabled) continue;
                if (!string.Equals(u.UserType, "Member", StringComparison.OrdinalIgnoreCase)) continue; // sin invitados

                var email = (u.Mail ?? u.UserPrincipalName)?.Trim();
                if (string.IsNullOrWhiteSpace(email) ||
                    !email.EndsWith(DominioPermitido, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var localPart = email[..email.IndexOf('@')];
                if (!PatronNombre.IsMatch(localPart) ||
                    localPart.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(u.DisplayName)) continue;

                usuarios.Add(new DirectoryUsuario(u.DisplayName.Trim(), email.ToLower()));
            }

            url = pagina?.NextLink;
        }

        return usuarios;
    }

    private class GraphUsersResponse
    {
        [JsonPropertyName("value")]
        public List<GraphUser>? Value { get; set; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    private class GraphUser
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("mail")]
        public string? Mail { get; set; }

        [JsonPropertyName("userPrincipalName")]
        public string? UserPrincipalName { get; set; }

        [JsonPropertyName("accountEnabled")]
        public bool AccountEnabled { get; set; }

        [JsonPropertyName("userType")]
        public string? UserType { get; set; }
    }
}
