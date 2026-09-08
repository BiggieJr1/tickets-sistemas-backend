using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(t => t.CodigoTicket).IsUnique();
            entity.Property(t => t.Titulo).IsRequired().HasMaxLength(120);
            entity.Property(t => t.Descripcion).IsRequired().HasMaxLength(4000);
            entity.Property(t => t.Solicitante).IsRequired().HasMaxLength(80);
        });
    }
}
