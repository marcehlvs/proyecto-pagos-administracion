using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    // Admin puede tomar asistencia de cualquier curso. Docente solo del curso que tiene asignado
    // (se valida en cada acción, no solo con el atributo de Roles, para evitar que un Docente
    // tome asistencia de un curso ajeno cambiando el cursoId en la URL).
    [Authorize(Roles = "Admin,Docente")]
    public class AsistenciasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AsistenciasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<(Curso? curso, IActionResult? error)> ObtenerCursoConPermiso(int cursoId)
        {
            var curso = await _context.Cursos.FindAsync(cursoId);
            if (curso == null) return (null, NotFound());

            if (User.IsInRole("Docente") && !User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                if (curso.ProfesorUserId != userId) return (null, Forbid());
            }

            return (curso, null);
        }

        // GET: Asistencias/Tomar?cursoId=1&fecha=2026-08-31
        // Pantalla de carga: lista los alumnos inscriptos en el curso para la fecha elegida,
        // precargando el estado si ya existe una asistencia cargada ese día (permite corregir).
        public async Task<IActionResult> Tomar(int cursoId, DateTime? fecha)
        {
            var (curso, error) = await ObtenerCursoConPermiso(cursoId);
            if (error != null) return error;

            var fechaClase = (fecha ?? DateTime.Today).Date;
            var diaTieneEF = curso!.TieneEducacionFisica(fechaClase);

            var inscripciones = await _context.Inscripciones
                .Include(i => i.Alumno)
                .Include(i => i.Asistencias.Where(a => a.Fecha == fechaClase))
                .Where(i => i.CursoId == cursoId)
                .OrderBy(i => i.Alumno.Apellido)
                .ToListAsync();

            ViewBag.Curso = curso;
            ViewBag.Fecha = fechaClase;
            ViewBag.DiaTieneEF = diaTieneEF;

            return View(inscripciones);
        }

        // POST: Asistencias/Tomar
        // Recibe un estado de Clase (siempre) y, si el día tiene Educación Física para este
        // curso, también un estado de EF por cada inscripción. Hace upsert por Materia.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tomar(int cursoId, DateTime fecha, Dictionary<int, EstadoAsistencia> estadosClase, Dictionary<int, EstadoAsistencia>? estadosEF)
        {
            var (curso, error) = await ObtenerCursoConPermiso(cursoId);
            if (error != null) return error;

            var fechaClase = fecha.Date;
            var diaTieneEF = curso!.TieneEducacionFisica(fechaClase);
            var nombreDocente = User.Identity?.Name ?? "Docente";

            await GuardarMateria(estadosClase, fechaClase, Materia.Clase, nombreDocente);

            // Solo se guarda EF si el día efectivamente tiene EF para este curso; si alguien
            // manipula el POST agregando estadosEF en un día sin EF, se ignora.
            if (diaTieneEF && estadosEF != null)
                await GuardarMateria(estadosEF, fechaClase, Materia.EducacionFisica, nombreDocente);

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = $"Asistencia del {fechaClase:dd/MM/yyyy} guardada.";
            return RedirectToAction(nameof(Tomar), new { cursoId, fecha = fechaClase });
        }

        private async Task GuardarMateria(Dictionary<int, EstadoAsistencia> estados, DateTime fecha, Materia materia, string nombreDocente)
        {
            var inscripcionIds = estados.Keys.ToList();
            var existentes = await _context.Asistencias
                .Where(a => inscripcionIds.Contains(a.InscripcionId) && a.Fecha == fecha && a.Materia == materia)
                .ToListAsync();

            foreach (var (inscripcionId, estado) in estados)
            {
                var asistencia = existentes.FirstOrDefault(a => a.InscripcionId == inscripcionId);
                if (asistencia != null)
                {
                    asistencia.Estado = estado;
                    asistencia.ModificadaPorNombre = nombreDocente;
                    asistencia.FechaModificacion = DateTime.Now;
                }
                else
                {
                    _context.Asistencias.Add(new Asistencia
                    {
                        InscripcionId = inscripcionId,
                        Fecha = fecha,
                        Materia = materia,
                        Estado = estado,
                        CreadaPorNombre = nombreDocente
                    });
                }
            }
        }
    }
}
