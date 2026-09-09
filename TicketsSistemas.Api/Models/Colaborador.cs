namespace TicketsSistemas.Api.Models;

// Nombre del claim que dice si el colaborador es administrador. Se agrega a
// la identidad en Program.cs (OnTokenValidated) después de resolver el
// Colaborador local a partir del correo del token de Microsoft — Entra ID no
// sabe nada de roles de esta app, así que este claim nunca viaja en el token
// que emite Microsoft.
public static class ClaimesColaborador
{
    public const string EsAdministrador = "esAdministrador";
}

public class Colaborador
{
    public int Id { get; set; }

    public string NombreCompleto { get; set; } = string.Empty;

    // Es el usuario de login (cuenta de Microsoft/Entra ID de bisoft.com.mx).
    public string Email { get; set; } = string.Empty;

    public bool EsAdministrador { get; set; }

    // Se desactiva en vez de borrar, para no romper AsignadoAId/ActualizadoPorId
    // de tickets viejos que ya lo referencian.
    public bool Activo { get; set; } = true;

    public DateTime Creado { get; set; } = DateTime.UtcNow;
    public DateTime? Actualizado { get; set; }
}
