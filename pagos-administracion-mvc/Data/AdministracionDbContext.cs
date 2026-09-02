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
        public DbSet<Curso> Cursos { get; set; }
        public DbSet<Inscripcion> Inscripciones { get; set; }
        public DbSet<Asistencia> Asistencias { get; set; }
        public DbSet<ArancelNivel> ArancelesNivel { get; set; }
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

            // Login propio del Alumno: 1 a 1 con ApplicationUser, independiente de FamiliaUser.
            // Restrict (no SetNull): Alumnos ya tiene otra FK nullable a AspNetUsers (FamiliaUserId)
            // con SetNull. Si ambas columnas usaran SetNull, SQL Server rechaza la constraint
            // (Error 1785, "may cause cycles or multiple cascade paths") porque no puede garantizar
            // cómo resolver el SET NULL de las dos columnas si la misma fila de AspNetUsers
            // terminara referenciada por ambas en un mismo Alumno.
            modelBuilder.Entity<Alumno>()
                .HasOne(a => a.AlumnoUser)
                .WithMany()
                .HasForeignKey(a => a.AlumnoUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Inscripcion>()
                .HasOne(i => i.Alumno)
                .WithMany(a => a.Inscripciones)
                .HasForeignKey(i => i.AlumnoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Inscripcion>()
                .HasOne(i => i.Curso)
                .WithMany(c => c.Inscripciones)
                .HasForeignKey(i => i.CursoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Docente asignado a un Curso: 1 a muchos con ApplicationUser, sin navegación inversa.
            modelBuilder.Entity<Curso>()
                .HasOne(c => c.ProfesorUser)
                .WithMany()
                .HasForeignKey(c => c.ProfesorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Nombre es "string?" en C# solo para desactivar el [Required] implícito de MVC
            // (Nullable Reference Types). La columna en la DB sigue siendo NOT NULL: esto evita
            // que EF quiera generar una migración para volverla nullable.
            modelBuilder.Entity<Curso>()
                .Property(c => c.Nombre)
                .IsRequired();

            // Default en DB para que los cursos ya existentes (creados antes de este campo)
            // no queden con meta 0 tras la migración.
            modelBuilder.Entity<Curso>()
                .Property(c => c.MetaPresentismo)
                .HasDefaultValue(90);

            modelBuilder.Entity<Asistencia>()
                .HasOne(a => a.Inscripcion)
                .WithMany(i => i.Asistencias)
                .HasForeignKey(a => a.InscripcionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Un alumno no puede tener dos asistencias el mismo día para la misma Materia
            // (Clase o Educación Física), pero sí una de cada una (2 registros por día).
            modelBuilder.Entity<Asistencia>()
                .HasIndex(a => new { a.InscripcionId, a.Fecha, a.Materia })
                .IsUnique();

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

            // Mismo criterio de soft delete encadenado: Curso -> Inscripcion -> Asistencia.
            modelBuilder.Entity<Curso>().HasQueryFilter(c => c.Activo);
            modelBuilder.Entity<Inscripcion>().HasQueryFilter(i => i.Activo && i.Alumno.Activo && i.Curso.Activo);
            modelBuilder.Entity<Asistencia>().HasQueryFilter(a => a.Activo && a.Inscripcion.Activo && a.Inscripcion.Alumno.Activo && a.Inscripcion.Curso.Activo);

            modelBuilder.Entity<ArancelNivel>().HasQueryFilter(a => a.Activo);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.Curricular).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.ExtraCurricular).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.EquipamientoDidactico).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.Mantenimiento).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.EmergenciaMedica).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.BonificacionPagoATiempo).HasPrecision(18, 2);

            // Precisión explícita para columnas monetarias: sin esto, SQL Server usa decimal(18,2) por
            // default y trunca en silencio cualquier valor con más de 2 decimales (warning EF 30000).
            modelBuilder.Entity<Cuota>().Property(c => c.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<Cuota>().Property(c => c.MontoConDescuento).HasPrecision(18, 2);
            modelBuilder.Entity<Pago>().Property(p => p.Monto).HasPrecision(18, 2);
        }
    }
}