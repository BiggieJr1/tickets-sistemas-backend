namespace TicketsSistemas.Api.Models;

public enum Categoria
{
    Hardware,
    Software,
    Red,
    Accesos,
    Servidor,
    Otro
}

public enum Prioridad
{
    Critica,
    Alta,
    Media,
    Baja,

    // Se asigna sola al crear un ticket: la prioridad ya no la elige quien
    // reporta, la define el equipo de soporte al revisarlo.
    SinAsignar
}

public enum Estado
{
    Abierto,
    EnProgreso,
    Resuelto,
    Cerrado
}
