namespace TicketsSistemas.Api.Models;

// Nombres de los claims que esta app le agrega a la identidad en Program.cs
// (OnTokenValidated) después de resolver el Colaborador local a partir del
// correo del token de Microsoft — Entra ID no sabe nada de esto, así que
// nunca viajan en el token que emite Microsoft.
// ColaboradorId usa un nombre propio (no ClaimTypes.NameIdentifier) porque
// Microsoft.Identity.Web ya mapea el claim "sub" del token a
// ClaimTypes.NameIdentifier automáticamente — reusar ese tipo chocaba con el
// "sub" (un id opaco de Microsoft, no numérico) y User.FindFirstValue()
// devolvía ese en vez del id local.
public static class ClaimesColaborador
{
    public const string ColaboradorId = "colaboradorId";
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
