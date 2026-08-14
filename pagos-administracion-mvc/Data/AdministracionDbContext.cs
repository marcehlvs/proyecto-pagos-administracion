using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Data
{
    public class AdministracionDbContext : DbContext
    {
        public AdministracionDbContext(DbContextOptions<AdministracionDbContext> options)
            : base(options){}
        public DbSet<Alumno>Alumnos { get; set; }
        public DbSet<Cuota> Cuotas { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Cuota>()
                .Property(c => c.Monto)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Pago>()
                .Property(p => p.Monto)
                .HasColumnType("decimal(18,2)");
        }
    }
}
