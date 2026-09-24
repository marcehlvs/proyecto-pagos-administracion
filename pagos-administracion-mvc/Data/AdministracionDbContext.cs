using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using pagos_administracion_mvc.Models;
using System.Text.Json;

namespace pagos_administracion_mvc.Data
{
    public class AdministracionDbContext : IdentityDbContext<ApplicationUser>
    {
        // IHttpContextAccessor permite al DbContext obtener el usuario autenticado en cada
        // request HTTP. Es nullable a propósito: los background services (RevisorVencimientosService)
        // no tienen contexto HTTP y pasan null — en ese caso el autor del log es "Sistema".
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public AdministracionDbContext(
            DbContextOptions<AdministracionDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }
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
        public DbSet<Tarea> Tareas { get; set; }
        public DbSet<Entrega> Entregas { get; set; }
        public DbSet<Feriado> Feriados { get; set; }
        public DbSet<BoletinPublicacion> BoletinPublicaciones { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        // ── Módulo de Exámenes ────────────────────────────────────────────────────
        public DbSet<Examen> Examenes { get; set; }
        public DbSet<PreguntaExamen> PreguntasExamen { get; set; }
        public DbSet<OpcionRespuesta> OpcionesRespuesta { get; set; }
        public DbSet<IntentoExamen> IntentosExamen { get; set; }
        public DbSet<IntentoPregunta> IntentosPreguntas { get; set; }
        // ── Audit Log ────────────────────────────────────────────────────────────
        // Tipos C# que queremos auditar. Cualquier cambio (Add/Modify/Delete) sobre
        // instancias de estos tipos genera automáticamente una fila en AuditLogs.
        private static readonly HashSet<Type> _tiposAuditables = new()
        {
            typeof(Nota),
            typeof(Pago),
            typeof(Cuota),
            typeof(Alumno),
            typeof(Inscripcion),
            typeof(ArancelNivel),
        };

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Capturamos los entries ANTES de llamar a base.SaveChangesAsync porque
            // después el ChangeTracker ya no tiene la información de estado original.
            var entries = ChangeTracker.Entries()
                .Where(e => _tiposAuditables.Contains(e.Entity.GetType())
                         && e.State is EntityState.Added
                                    or EntityState.Modified
                                    or EntityState.Deleted)
                .ToList();

            // Identificar al autor del cambio.
            // User.FindFirst(ClaimTypes.NameIdentifier) es el Id del ApplicationUser.
            var httpContext = _httpContextAccessor?.HttpContext;
            var usuarioId    = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var usuarioEmail = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                            ?? httpContext?.User?.Identity?.Name;

            // Para entries en estado Added, la PK todavía no existe hasta que SaveChanges
            // escriba en la BD. Los resolvemos en dos tandas:
            //   - Tanda 1: Modified y Deleted → PK ya conocida, generamos log inmediatamente.
            //   - Tanda 2: Added → guardamos el entry para leerlo DESPUÉS del base.SaveChangesAsync.
            var logsInmediatos = new List<AuditLog>();
            var entriesPendientesAdd = new List<EntityEntry>(); // Added, esperan la PK

            foreach (var entry in entries)
            {
                var accion = entry.State switch
                {
                    EntityState.Added    => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted  => "Delete",
                    _                    => "Unknown"
                };

                if (entry.State == EntityState.Added)
                {
                    entriesPendientesAdd.Add(entry);
                    continue;
                }

                // PK de la entidad (puede ser int, string, Guid — la convertimos a string).
                var pkValue = entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "?";

                string? jsonAnterior = null;
                string? jsonNuevo   = null;

                if (entry.State == EntityState.Modified)
                {
                    jsonAnterior = JsonSerializer.Serialize(
                        entry.OriginalValues.Properties
                             .ToDictionary(p => p.Name,
                                          p => entry.OriginalValues[p]));
                    jsonNuevo = JsonSerializer.Serialize(
                        entry.CurrentValues.Properties
                             .ToDictionary(p => p.Name,
                                          p => entry.CurrentValues[p]));
                }
                else if (entry.State == EntityState.Deleted)
                {
                    jsonAnterior = JsonSerializer.Serialize(
                        entry.OriginalValues.Properties
                             .ToDictionary(p => p.Name,
                                          p => entry.OriginalValues[p]));
                }

                logsInmediatos.Add(new AuditLog
                {
                    Fecha            = DateTime.UtcNow,
                    UsuarioId        = usuarioId,
                    UsuarioEmail     = usuarioEmail ?? (usuarioId == null ? "Sistema" : null),
                    Entidad          = entry.Entity.GetType().Name,
                    EntidadId        = pkValue,
                    Accion           = accion,
                    ValoresAnteriores = jsonAnterior,
                    ValoresNuevos    = jsonNuevo,
                });
            }

            // Agregar los logs inmediatos ANTES de persistir (misma transacción implícita de EF).
            // Los de tipo Added se agregan DESPUÉS porque aún no tienen PK.
            AuditLogs.AddRange(logsInmediatos);

            // Persistir todo (entidades originales + logs inmediatos).
            var resultado = await base.SaveChangesAsync(cancellationToken);

            // Tanda 2: ahora las entidades Added ya tienen su PK generada por la BD.
            if (entriesPendientesAdd.Any())
            {
                var logsAdd = entriesPendientesAdd.Select(entry => new AuditLog
                {
                    Fecha        = DateTime.UtcNow,
                    UsuarioId    = usuarioId,
                    UsuarioEmail = usuarioEmail ?? (usuarioId == null ? "Sistema" : null),
                    Entidad      = entry.Entity.GetType().Name,
                    EntidadId    = entry.Properties
                                       .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                                       ?.CurrentValue?.ToString() ?? "?",
                    Accion       = "Create",
                    ValoresNuevos = JsonSerializer.Serialize(
                        entry.CurrentValues.Properties
                             .ToDictionary(p => p.Name,
                                          p => entry.CurrentValues[p])),
                }).ToList();

                AuditLogs.AddRange(logsAdd);
                await base.SaveChangesAsync(cancellationToken); // solo los logs de Add
            }

            return resultado;
        }

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

            modelBuilder.Entity<BoletinPublicacion>()
                .HasOne(bp => bp.Curso)
                .WithMany()
                .HasForeignKey(bp => bp.CursoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Un solo estado de publicación por Curso+AnioLectivo (se hace upsert sobre esta fila,
            // nunca se insertan duplicados).
            modelBuilder.Entity<BoletinPublicacion>()
                .HasIndex(bp => new { bp.CursoId, bp.AnioLectivo })
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
            // BoletinPublicacion no tiene su propio Activo, pero su FK a Curso es obligatoria (no
            // nullable) y Curso sí tiene query filter — sin este filtro, EF tira
            // "required end of a relationship... may lead to unexpected results" al migrar.
            modelBuilder.Entity<BoletinPublicacion>().HasQueryFilter(bp => bp.Curso.Activo);

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

            // --- Tareas (tipo Classroom) ---

            modelBuilder.Entity<Tarea>()
                .HasOne(t => t.CursoAsignatura)
                .WithMany()
                .HasForeignKey(t => t.CursoAsignaturaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tarea>()
                .HasOne(t => t.Periodo)
                .WithMany()
                .HasForeignKey(t => t.PeriodoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tarea>().HasQueryFilter(t =>
                t.Activo && t.CursoAsignatura.Activo);

            modelBuilder.Entity<Entrega>()
                .HasOne(e => e.Tarea)
                .WithMany(t => t.Entregas)
                .HasForeignKey(e => e.TareaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Entrega>()
                .HasOne(e => e.Inscripcion)
                .WithMany()
                .HasForeignKey(e => e.InscripcionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict, no Cascade: si se borra (soft-delete) la Nota generada por esta Entrega,
            // no queremos que la Entrega se borre con ella — se desvincula (NotaId vuelve a
            // setearse null) desde el código, ver TareasController.
            modelBuilder.Entity<Entrega>()
                .HasOne(e => e.Nota)
                .WithMany()
                .HasForeignKey(e => e.NotaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Entrega>().Property(e => e.Calificacion).HasPrecision(4, 2);

            // Una sola Entrega por Alumno+Tarea.
            modelBuilder.Entity<Entrega>()
                .HasIndex(e => new { e.TareaId, e.InscripcionId })
                .IsUnique();

            modelBuilder.Entity<Entrega>().HasQueryFilter(e =>
                e.Activo && e.Tarea.Activo && e.Inscripcion.Activo && e.Inscripcion.Alumno.Activo);

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

            // ── Módulo de Exámenes ────────────────────────────────────────────────

            modelBuilder.Entity<Examen>()
                .HasOne(e => e.CursoAsignatura)
                .WithMany()
                .HasForeignKey(e => e.CursoAsignaturaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Examen>()
                .HasOne(e => e.Periodo)
                .WithMany()
                .HasForeignKey(e => e.PeriodoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Examen>()
                .Property(e => e.NotaMinimaAprobatoria)
                .HasPrecision(4, 2);

            modelBuilder.Entity<Examen>().HasQueryFilter(e =>
                e.Activo && e.CursoAsignatura.Activo);

            modelBuilder.Entity<PreguntaExamen>()
                .HasOne(p => p.Examen)
                .WithMany(e => e.Preguntas)
                .HasForeignKey(p => p.ExamenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PreguntaExamen>().HasQueryFilter(p =>
                p.Activo && p.Examen.Activo);

            modelBuilder.Entity<OpcionRespuesta>()
                .HasOne(o => o.PreguntaExamen)
                .WithMany(p => p.Opciones)
                .HasForeignKey(o => o.PreguntaExamenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IntentoExamen>()
                .HasOne(i => i.Examen)
                .WithMany(e => e.Intentos)
                .HasForeignKey(i => i.ExamenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IntentoExamen>()
                .HasOne(i => i.Inscripcion)
                .WithMany()
                .HasForeignKey(i => i.InscripcionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict (no SetNull): mismo motivo que Entrega.NotaId — la Nota no debe borrar
            // el intento si se da de baja; se desvincula desde el controller.
            modelBuilder.Entity<IntentoExamen>()
                .HasOne(i => i.NotaBoletín)
                .WithMany()
                .HasForeignKey(i => i.NotaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IntentoExamen>()
                .Property(i => i.NotaValor)
                .HasPrecision(4, 2);

            modelBuilder.Entity<IntentoPregunta>()
                .HasOne(ip => ip.IntentoExamen)
                .WithMany(i => i.Respuestas)
                .HasForeignKey(ip => ip.IntentoExamenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IntentoPregunta>()
                .HasOne(ip => ip.PreguntaExamen)
                .WithMany(p => p.IntentosPreguntas)
                .HasForeignKey(ip => ip.PreguntaExamenId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IntentoPregunta>()
                .HasOne(ip => ip.OpcionRespuesta)
                .WithMany()
                .HasForeignKey(ip => ip.OpcionRespuestaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Un alumno solo puede responder una vez por pregunta dentro del mismo intento.
            modelBuilder.Entity<IntentoPregunta>()
                .HasIndex(ip => new { ip.IntentoExamenId, ip.PreguntaExamenId })
                .IsUnique();
        }
    }
}