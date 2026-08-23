using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Data
{
    public class AdministracionDbContext : IdentityDbContext<ApplicationUser>
    {
        public AdministracionDbContext(DbContextOptions<AdministracionDbContext> options)
            : base(options){}
        public DbSet<Alumno>Alumnos { get; set; }
        public DbSet<Cuota> Cuotas { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<ContactoManual> ContactosManuales { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Cuota>()
                .Property(c => c.Monto)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Pago>()
                .Property(p => p.Monto)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Cuota>()
                .HasOne(c => c.Alumno)
                .WithMany(a => a.Cuotas)
                .HasForeignKey(c => c.AlumnoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pago>()
                .HasOne(p => p.Cuota)
                .WithMany(c => c.Pagos)
                .HasForeignKey(p => p.CuotaId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Alumno>()
                .HasOne(a => a.FamiliaUser)
                .WithMany() // un ApplicationUser puede tener varios Alumnos, pero no navegamos la colección desde ApplicationUser
                .HasForeignKey(a => a.FamiliaUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Soft delete: por defecto, todas las consultas a Pagos ignoran los "eliminados" (Activo = false).
            // Para incluirlos explícitamente (ej. auditoría), usar .IgnoreQueryFilters().
            modelBuilder.Entity<Pago>().HasQueryFilter(p => p.Activo);
        }
    }
}
