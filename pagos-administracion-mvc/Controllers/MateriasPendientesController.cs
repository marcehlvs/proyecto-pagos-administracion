using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // Materias pendientes de aprobación de años anteriores (RITE, Fase 3): las carga Admin o
    // Preceptor (no el Docente: es un dato administrativo de arrastre, no una calificación del
    // ciclo actual). Preceptor solo para alumnos inscriptos en algún Curso donde figura como
    // ProfesorUserId — mismo criterio de scoping que Asistencias/Boletines.
    [Authorize(Roles = "Admin,Preceptor")]
    public class MateriasPendientesController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MateriasPendientesController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<IActionResult?> ValidarPermisoAsync(int alumnoId)
        {
            if (User.IsInRole("Admin")) return null;

            var userId = _userManager.GetUserId(User);
            var esDeSuCurso = await _context.Inscripciones
                .AnyAsync(i => i.AlumnoId == alumnoId && i.Curso.ProfesorUserId == userId);
            return esDeSuCurso ? null : Forbid();
        }

        private async Task<List<Alumno>> ObtenerAlumnosDisponiblesAsync()
        {
            if (User.IsInRole("Admin"))
                return await _context.Alumnos.OrderBy(a => a.Apellido).ToListAsync();

            var userId = _userManager.GetUserId(User);
            return await _context.Inscripciones
                .Where(i => i.Curso.ProfesorUserId == userId)
                .Select(i => i.Alumno)
                .Distinct()
                .OrderBy(a => a.Apellido)
                .ToListAsync();
        }

        // GET: MateriasPendientes?alumnoId=1
        // Sin alumnoId: solo pide elegir el alumno (Admin: todos; Preceptor: los de sus cursos).
        public async Task<IActionResult> Index(int? alumnoId)
        {
            var modelo = new MateriasPendientesViewModel
            {
                AlumnosDisponibles = await ObtenerAlumnosDisponiblesAsync()
            };

            if (alumnoId.HasValue)
            {
                var error = await ValidarPermisoAsync(alumnoId.Value);
                if (error != null) return error;

                modelo.AlumnoSeleccionado = await _context.Alumnos.FindAsync(alumnoId.Value);
                if (modelo.AlumnoSeleccionado == null) return NotFound();

                modelo.Pendientes = await _context.MateriasPendientes
                    .Include(mp => mp.Asignatura)
                    .Where(mp => mp.AlumnoId == alumnoId.Value)
                    .OrderBy(mp => mp.Aprobada).ThenByDescending(mp => mp.GradoAnio).ThenBy(mp => mp.Asignatura.Nombre)
                    .ToListAsync();

                modelo.Asignaturas = new SelectList(
                    await _context.Asignaturas.Where(a => a.Nivel == modelo.AlumnoSeleccionado.Nivel).OrderBy(a => a.Nombre).ToListAsync(),
                    "Id", "Nombre");
            }

            return View(modelo);
        }

        // POST: MateriasPendientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int alumnoId, int asignaturaId, int gradoAnio, string? observacion)
        {
            var error = await ValidarPermisoAsync(alumnoId);
            if (error != null) return error;

            if (gradoAnio >= 1 && gradoAnio <= 7 && await _context.Asignaturas.AnyAsync(a => a.Id == asignaturaId))
            {
                _context.MateriasPendientes.Add(new MateriaPendiente
                {
                    AlumnoId = alumnoId,
                    AsignaturaId = asignaturaId,
                    GradoAnio = gradoAnio,
                    Observacion = observacion,
                    CargadaPorNombre = User.Identity?.Name
                });
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Materia pendiente agregada.";
            }

            return RedirectToAction(nameof(Index), new { alumnoId });
        }

        // POST: MateriasPendientes/MarcarAprobada
        // fechaAprobacion vacía = "todavía no la aprobó" (deshace una marca anterior por error).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarAprobada(int id, DateTime? fechaAprobacion)
        {
            var pendiente = await _context.MateriasPendientes.FindAsync(id);
            if (pendiente == null) return NotFound();

            var error = await ValidarPermisoAsync(pendiente.AlumnoId);
            if (error != null) return error;

            pendiente.Aprobada = fechaAprobacion.HasValue;
            pendiente.FechaAprobacion = fechaAprobacion;
            pendiente.ModificadaPorNombre = User.Identity?.Name;
            pendiente.FechaModificacion = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { alumnoId = pendiente.AlumnoId });
        }

        // POST: MateriasPendientes/Eliminar — soft delete, mismo criterio que el resto del proyecto.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var pendiente = await _context.MateriasPendientes.FindAsync(id);
            if (pendiente == null) return NotFound();

            var error = await ValidarPermisoAsync(pendiente.AlumnoId);
            if (error != null) return error;

            pendiente.Activo = false;
            pendiente.ModificadaPorNombre = User.Identity?.Name;
            pendiente.FechaModificacion = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Materia pendiente eliminada.";

            return RedirectToAction(nameof(Index), new { alumnoId = pendiente.AlumnoId });
        }
    }

    // Modelo de la pantalla MateriasPendientesController/Index.
    public class MateriasPendientesViewModel
    {
        public List<Alumno> AlumnosDisponibles { get; set; } = new();
        public Alumno? AlumnoSeleccionado { get; set; }
        public List<MateriaPendiente> Pendientes { get; set; } = new();
        public SelectList? Asignaturas { get; set; }
    }
}
