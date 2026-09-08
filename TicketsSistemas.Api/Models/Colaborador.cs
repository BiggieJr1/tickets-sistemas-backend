namespace TicketsSistemas.Api.Models;

public class Colaborador
{
    public int Id { get; set; }

    public string NombreCompleto { get; set; } = string.Empty;

    // Es el usuario de login.
    public string Email { get; set; } = string.Empty;

    // Hash de BCrypt, nunca la contraseña en texto plano.
    public string PasswordHash { get; set; } = string.Empty;

    public bool EsAdministrador { get; set; }

    // Se desactiva en vez de borrar, para no romper AsignadoAId/ActualizadoPorId
    // de tickets viejos que ya lo referencian.
    public bool Activo { get; set; } = true;

    public DateTime Creado { get; set; } = DateTime.UtcNow;
    public DateTime? Actualizado { get; set; }
}
