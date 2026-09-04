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
    Baja
}

public enum Estado
{
    Abierto,
    EnProgreso,
    Resuelto,
    Cerrado
}
