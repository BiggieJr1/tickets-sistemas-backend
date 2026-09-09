using System.ComponentModel.DataAnnotations;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Dtos;

public class ColaboradorCreateDto
{
    [Required, MaxLength(120)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    public bool EsAdministrador { get; set; }
}

// PATCH combinado: es un formulario de admin completo (nombre, email, rol,
// activo), no varios controles independientes como en Ticket (estado/
// prioridad/asignación se guardan cada uno por separado).
public class ColaboradorUpdateDto
{
    [Required, MaxLength(120)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    public bool EsAdministrador { get; set; }
    public bool Activo { get; set; }
}

public class ColaboradorResponseDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EsAdministrador { get; set; }
    public bool Activo { get; set; }

    public static ColaboradorResponseDto FromEntity(Colaborador c) => new()
    {
        Id = c.Id,
        NombreCompleto = c.NombreCompleto,
        Email = c.Email,
        EsAdministrador = c.EsAdministrador,
        Activo = c.Activo,
    };
}
