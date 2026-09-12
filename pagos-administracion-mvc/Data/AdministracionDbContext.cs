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
        public DbSet<ConfiguracionSitio> ConfiguracionSitio { get; set; }
        public DbSet<Asignatura> Asignaturas { get; set; }
        public DbSet<Periodo> Periodos { get; set; }
        public DbSet<CursoAsignatura> CursosAsignaturas { get; set; }
        public DbSet<Nota> Notas { get; set; }
        public DbSet<MateriaPendiente> MateriasPendientes { get; set; }
        public DbSet<Feriado> Feriados { get; set; }
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

            // Periodo es autorreferenciado (PeriodoPadreId -> Periodo). Restrict, no Cascade/SetNull:
            // con self-reference, SQL Server no acepta cascada (mismo motivo que Alumno.AlumnoUserId
            // más arriba) y un padre borrado no puede dejar huérfanos sus hijos en silencio.
            modelBuilder.Entity<Periodo>()
                .HasOne(p => p.PeriodoPadre)
                .WithMany(p => p.Subperiodos)
                .HasForeignKey(p => p.PeriodoPadreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asignatura>().HasQueryFilter(a => a.Activo);
            modelBuilder.Entity<Periodo>().HasQueryFilter(p => p.Activo);

            // Un feriado no puede cargarse dos veces para la misma fecha.
            modelBuilder.Entity<Feriado>()
                .HasIndex(f => f.Fecha)
                .IsUnique();

            modelBuilder.Entity<Feriado>().HasQueryFilter(f => f.Activo);
            modelBuilder.Entity<CursoAsignatura>()
                .HasOne(ca => ca.Curso)
                .WithMany(c => c.CursosAsignaturas)
                .HasForeignKey(ca => ca.CursoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CursoAsignatura>()
                .HasOne(ca => ca.Asignatura)
                .WithMany()
                .HasForeignKey(ca => ca.AsignaturaId)
                .OnDelete(DeleteBehavior.Restrict);

            // El Docente es opcional (ver CursoAsignatura.DocenteUserId), así que si el usuario
            // Docente se borra, la materia del curso queda sin asignar en vez de romperse.
            modelBuilder.Entity<CursoAsignatura>()
                .HasOne(ca => ca.DocenteUser)
                .WithMany()
                .HasForeignKey(ca => ca.DocenteUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Una misma Asignatura no puede cargarse dos veces para el mismo Curso (si mañana un
            // Curso necesita dos Docentes para la misma materia -ej. divididos por comisión-, hay
            // que resolverlo con una Asignatura propia por comisión, no duplicando esta fila).
            modelBuilder.Entity<CursoAsignatura>()
                .HasIndex(ca => new { ca.CursoId, ca.AsignaturaId })
                .IsUnique();

            modelBuilder.Entity<CursoAsignatura>()
                .HasQueryFilter(ca => ca.Activo && ca.Curso.Activo && ca.Asignatura.Activo);

            modelBuilder.Entity<Nota>()
                .HasOne(n => n.Inscripcion)
                .WithMany()
                .HasForeignKey(n => n.InscripcionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Nota>()
                .HasOne(n => n.CursoAsignatura)
                .WithMany(ca => ca.Notas)
                .HasForeignKey(n => n.CursoAsignaturaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Nota>()
                .HasOne(n => n.Periodo)
                .WithMany()
                .HasForeignKey(n => n.PeriodoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Nota>().Property(n => n.Valor).HasPrecision(4, 2);
            modelBuilder.Entity<Nota>().Property(n => n.IntensificacionDiciembre).HasPrecision(4, 2);
            modelBuilder.Entity<Nota>().Property(n => n.IntensificacionFebrero).HasPrecision(4, 2);

            // Una sola Nota por Alumno+Materia+Periodo+Orden (Orden distingue cada nota suelta
            // dentro del Periodo; Orden=0 es la fila "consolidada" del Periodo para el boletín).
            modelBuilder.Entity<Nota>()
                .HasIndex(n => new { n.InscripcionId, n.CursoAsignaturaId, n.PeriodoId, n.Orden })
                .IsUnique();

            modelBuilder.Entity<Nota>().HasQueryFilter(n =>
                n.Activo && n.Inscripcion.Activo && n.Inscripcion.Alumno.Activo &&
                n.CursoAsignatura.Activo && n.Periodo.Activo);

            // Materias pendientes de años anteriores (RITE, Fase 3): igual criterio que el resto,
            // Restrict en las dos FK (un Alumno o una Asignatura con pendientes cargadas no se
            // borran físicamente) y el filtro encadena con Activo del Alumno y de la Asignatura.
            modelBuilder.Entity<MateriaPendiente>()
                .HasOne(mp => mp.Alumno)
                .WithMany()
                .HasForeignKey(mp => mp.AlumnoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MateriaPendiente>()
                .HasOne(mp => mp.Asignatura)
                .WithMany()
                .HasForeignKey(mp => mp.AsignaturaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MateriaPendiente>()
                .HasQueryFilter(mp => mp.Activo && mp.Alumno.Activo && mp.Asignatura.Activo);

            modelBuilder.Entity<ArancelNivel>().HasQueryFilter(a => a.Activo);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.Curricular).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.ExtraCurricular).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.EquipamientoDidactico).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.Mantenimiento).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.EmergenciaMedica).HasPrecision(18, 2);
            modelBuilder.Entity<ArancelNivel>().Property(a => a.BonificacionPagoATiempo).HasPrecision(18, 2);

            // Fila única (Id=1) con los colores de marca actuales (los mismos que están hardcodeados
            // hoy en site.css :root), para que la tabla nunca esté vacía y el sitio no se quede sin
            // estilo mientras el Admin no haya guardado nada todavía.
            modelBuilder.Entity<ConfiguracionSitio>().Property(c => c.Id).ValueGeneratedNever();
            modelBuilder.Entity<ConfiguracionSitio>().HasData(new ConfiguracionSitio
            {
                Id = 1,
                ColorPrimario = "#1A365D",
                ColorPrimarioOscuro = "#002045",
                ColorExito = "#10B981",
                ColorAdvertencia = "#F59E0B",
                NombrePreset = "Institucional (por defecto)"
            });

            // Precisión explícita para columnas monetarias: sin esto, SQL Server usa decimal(18,2) por
            // default y trunca en silencio cualquier valor con más de 2 decimales (warning EF 30000).
            modelBuilder.Entity<Cuota>().Property(c => c.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<Cuota>().Property(c => c.MontoConDescuento).HasPrecision(18, 2);
            modelBuilder.Entity<Pago>().Property(p => p.Monto).HasPrecision(18, 2);
        }
    }
}