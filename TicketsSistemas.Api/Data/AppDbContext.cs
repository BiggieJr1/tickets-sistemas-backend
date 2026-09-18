using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Colaborador> Colaboradores => Set<Colaborador>();
    public DbSet<TicketEvento> TicketEventos => Set<TicketEvento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(t => t.CodigoTicket).IsUnique();
            entity.Property(t => t.Titulo).IsRequired().HasMaxLength(120);
            entity.Property(t => t.Descripcion).IsRequired().HasMaxLength(4000);
            entity.Property(t => t.Solicitante).IsRequired().HasMaxLength(80);

            // SetNull en vez de Cascade/Restrict: desactivar o (en un caso
            // extremo) borrar un colaborador no debe tumbar sus tickets.
            entity.HasOne(t => t.AsignadoA)
                  .WithMany()
                  .HasForeignKey(t => t.AsignadoAId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.ActualizadoPor)
                  .WithMany()
                  .HasForeignKey(t => t.ActualizadoPorId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Colaborador>(entity =>
        {
            entity.HasIndex(c => c.Email).IsUnique();
            entity.Property(c => c.NombreCompleto).IsRequired().HasMaxLength(120);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(160);
        });

        modelBuilder.Entity<TicketEvento>(entity =>
        {
            entity.Property(e => e.Texto).HasMaxLength(4000);
            entity.HasIndex(e => e.TicketId);

            // Cascade: si se borra el ticket, no tiene sentido dejar su
            // historial huérfano.
            entity.HasOne(e => e.Ticket)
                  .WithMany()
                  .HasForeignKey(e => e.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            // SetNull: igual que AsignadoA/ActualizadoPor en Ticket, borrar
            // un colaborador no debe tumbar el historial que dejó.
            entity.HasOne(e => e.Colaborador)
                  .WithMany()
                  .HasForeignKey(e => e.ColaboradorId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
