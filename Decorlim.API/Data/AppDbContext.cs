using Decorlim.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Decorlim.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<TurnoCaja> TurnosCaja { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetalleVentas { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Comprobante> Comprobantes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DetalleVenta>(entity =>
            {
                entity.HasOne(d => d.Venta)
                      .WithMany(v => v.Detalles)
                      .HasForeignKey(d => d.IdVenta)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Pago>(entity =>
            {
                entity.HasOne(p => p.Venta)
                      .WithMany(v => v.Pagos)
                      .HasForeignKey(p => p.IdVenta)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}