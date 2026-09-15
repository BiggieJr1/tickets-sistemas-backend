namespace TicketsSistemas.Api.Services;

public record DirectoryUsuario(string NombreCompleto, string Email);

public interface IDirectoryService
{
    // Trae las cuentas habilitadas del tenant de Entra ID con correo
    // @bisoft.com.mx (excluye invitados/cuentas externas y deshabilitadas).
    Task<IReadOnlyList<DirectoryUsuario>> ObtenerUsuariosAsync();
}
