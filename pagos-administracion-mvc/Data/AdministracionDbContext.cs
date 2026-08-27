using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Data
{
    public class AdministracionDbContext : IdentityDbContext<ApplicationUser>
    {
        public AdministracionDbContext(DbContextOptions<AdministracionDbContext> options)
            : base(options) { }
        public DbSet<Alumno> Alumnos { get; set; }
        public DbSet<Cuota> Cuotas { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<ContactoManual> ContactosManuales { get; set; }
        public DbSet<Aviso> Avisos { get; set; }
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
            // Los filtros están encadenados hacia arriba (Cuota depende de Alumno.Activo, Pago y
            // ContactoManual dependen de Cuota.Activo Y de Alumno.Activo): si no se encadenan así,
            // dar de baja un Alumno dejaría sus Cuotas/Pagos visibles igual, con la navegación no-nullable
            // (Cuota.Alumno, Pago.Cuota, ContactoManual.Cuota) resolviendo null en tiempo de ejecución.
            modelBuilder.Entity<Alumno>().HasQueryFilter(a => a.Activo);
            modelBuilder.Entity<Cuota>().HasQueryFilter(c => c.Activo && c.Alumno.Activo);
            modelBuilder.Entity<Pago>().HasQueryFilter(p => p.Activo && p.Cuota.Activo && p.Cuota.Alumno.Activo);
            modelBuilder.Entity<ContactoManual>().HasQueryFilter(cm => cm.Cuota.Activo && cm.Cuota.Alumno.Activo);

            // Precisión explícita para columnas monetarias: sin esto, SQL Server usa decimal(18,2) por
            // default y trunca en silencio cualquier valor con más de 2 decimales (warning EF 30000).
            modelBuilder.Entity<Cuota>().Property(c => c.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<Cuota>().Property(c => c.MontoConDescuento).HasPrecision(18, 2);
            modelBuilder.Entity<Pago>().Property(p => p.Monto).HasPrecision(18, 2);
        }
    }
}