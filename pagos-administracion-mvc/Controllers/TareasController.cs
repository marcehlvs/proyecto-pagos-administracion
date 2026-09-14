using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;

namespace pagos_administracion_mvc.Controllers
{
    // Crear/editar/calificar Tareas: SOLO el Docente asignado a esa materia (igual criterio que
    // NotasController — ni el Admin puede, aunque también sea Docente de otra materia). Ver: Admin
    // (cualquier materia, sin editar) o el Docente propio. La entrega/lectura del lado Alumno y
    // Familia vive en MisTareasController.
    [Authorize(Roles = "Admin,Docente")]
    public class TareasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotaCalculadora _calculadora;

        public TareasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager, NotaCalculadora calculadora)
        {
            _context = context;
            _userManager = userManager;
            _calculadora = calculadora;
        }

        // Mismo helper que NotasController.ObtenerConPermisoAsync — se repite acá a propósito
        // (no hay controller base compartido en el proyecto, ver BoletinesController.ValidarPermisoAsync
        // como otro ejemplo del mismo criterio implementado por separado en cada controller).
        private async Task<(CursoAsignatura? cursoAsignatura, bool esDocentePropio, IActionResult? error)> ObtenerConPermisoAsync(int cursoAsignaturaId, bool soloLectura)
        {
            var cursoAsignatura = await _context.CursosAsignaturas
                .Include(ca => ca.Curso)
                .Include(ca => ca.Asignatura)
                .FirstOrDefaultAsync(ca => ca.Id == cursoAsignaturaId);
            if (cursoAsignatura == null) return (null, false, NotFound());

            var esDocentePropio = User.IsInRole("Docente") && cursoAsignatura.DocenteUserId == _userManager.GetUserId(User);
            var esAdmin = User.IsInRole("Admin");

            if (soloLectura)
            {
                if (!esAdmin && !esDocentePropio) return (null, false, Forbid());
            }
            else if (!esDocentePropio)
            {
                return (null, false, Forbid());
            }

            return (cursoAsignatura, esDocentePropio, null);
        }

        // GET: Tareas — mis materias (Docente) o todas (Admin), para elegir dónde ver/crear tareas.
        public async Task<IActionResult> Index()
        {
            var query = _context.CursosAsignaturas
                .Include(ca => ca.Curso)
                .Include(ca => ca.Asignatura)
                .AsQueryable();

            if (User.IsInRole("Docente") && !User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(ca => ca.DocenteUserId == userId);
            }

            var materias = await query
                .OrderBy(ca => ca.Curso.Nivel).ThenBy(ca => ca.Curso.GradoAnio).ThenBy(ca => ca.Asignatura.Nombre)
                .ToListAsync();

            return View(materias);
        }

        // GET: Tareas/PorMateria?cursoAsignaturaId=1 — lista de Tareas de esa materia.
        public async Task<IActionResult> PorMateria(int cursoAsignaturaId)
        {
            var (cursoAsignatura, esDocentePropio, error) = await ObtenerConPermisoAsync(cursoAsignaturaId, soloLectura: true);
            if (error != null) return error;

            ViewBag.CursoAsignatura = cursoAsignatura;
            ViewBag.SoloLectura = !esDocentePropio;

            var tareas = await _context.Tareas
                .Include(t => t.Periodo)
                .Include(t => t.Entregas)
                .Where(t => t.CursoAsignaturaId == cursoAsignaturaId)
                .OrderByDescending(t => t.FechaEntrega)
                .ToListAsync();

            return View(tareas);
        }

        // GET: Tareas/Crear?cursoAsignaturaId=1
        public async Task<IActionResult> Crear(int cursoAsignaturaId)
        {
            var (cursoAsignatura, _, error) = await ObtenerConPermisoAsync(cursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            ViewBag.CursoAsignatura = cursoAsignatura;
            ViewBag.Periodos = await PeriodosDelAnioAsync();
            return View(new Tarea { CursoAsignaturaId = cursoAsignaturaId, FechaEntrega = DateTime.Today.AddDays(7) });
        }

        // POST: Tareas/Crear — al crear la Tarea, se arma una Entrega en blanco por cada
        // Inscripcion activa del Curso, para que el Docente vea de entrada la lista completa de
        // alumnos (no solo los que ya entregaron).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Tarea tarea)
        {
            var (cursoAsignatura, _, error) = await ObtenerConPermisoAsync(tarea.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            ModelState.Remove(nameof(Tarea.CursoAsignatura));
            ModelState.Remove(nameof(Tarea.Periodo));
            if (!ModelState.IsValid)
            {
                ViewBag.CursoAsignatura = cursoAsignatura;
                ViewBag.Periodos = await PeriodosDelAnioAsync();
                return View(tarea);
            }

            tarea.CreadaPorNombre = User.Identity?.Name;
            _context.Tareas.Add(tarea);
            await _context.SaveChangesAsync();

            var inscripciones = await _context.Inscripciones
                .Where(i => i.CursoId == cursoAsignatura!.CursoId)
                .Select(i => i.Id)
                .ToListAsync();

            foreach (var inscripcionId in inscripciones)
                _context.Entregas.Add(new Entrega { TareaId = tarea.Id, InscripcionId = inscripcionId });

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Tarea creada para {inscripciones.Count} alumno(s).";
            return RedirectToAction(nameof(PorMateria), new { cursoAsignaturaId = tarea.CursoAsignaturaId });
        }

        // GET: Tareas/Detalle/5 — lista de Entregas de la Tarea, una fila por alumno, para
        // calificar/tildar/ver lo que subieron.
        public async Task<IActionResult> Detalle(int id)
        {
            var tarea = await _context.Tareas
                .Include(t => t.CursoAsignatura).ThenInclude(ca => ca.Curso)
                .Include(t => t.CursoAsignatura).ThenInclude(ca => ca.Asignatura)
                .Include(t => t.Periodo)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tarea == null) return NotFound();

            var (_, esDocentePropio, error) = await ObtenerConPermisoAsync(tarea.CursoAsignaturaId, soloLectura: true);
            if (error != null) return error;

            ViewBag.SoloLectura = !esDocentePropio;

            var entregas = await _context.Entregas
                .Include(e => e.Inscripcion).ThenInclude(i => i.Alumno)
                .Where(e => e.TareaId == id)
                .OrderBy(e => e.Inscripcion.Alumno.Apellido)
                .ToListAsync();

            ViewBag.Tarea = tarea;
            return View(entregas);
        }

        // POST: Tareas/Calificar — carga/edita Calificación + Observación de UNA Entrega.
        // Calificar implica considerarla entregada (útil para una entrega presencial que el
        // Docente evalúa sin que haya pasado por el sistema). Si la Tarea tiene Periodo asociado,
        // sincroniza (crea/actualiza) la Nota suelta correspondiente y recalcula hacia arriba.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calificar(int entregaId, decimal? calificacion, string? observacion)
        {
            var entrega = await _context.Entregas
                .Include(e => e.Tarea)
                .FirstOrDefaultAsync(e => e.Id == entregaId);
            if (entrega == null) return NotFound();

            var (_, _, error) = await ObtenerConPermisoAsync(entrega.Tarea.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            if (calificacion.HasValue && (calificacion < 0 || calificacion > 10))
            {
                TempData["ErrorTarea"] = "La calificación debe estar entre 0 y 10.";
                return RedirectToAction(nameof(Detalle), new { id = entrega.TareaId });
            }

            entrega.Calificacion = calificacion;
            entrega.ObservacionDocente = observacion;
            entrega.CalificadaPorNombre = User.Identity?.Name;
            entrega.FechaCalificacion = DateTime.Now;
            if (calificacion.HasValue) entrega.Entregada = true;

            await SincronizarNotaAsync(entrega);
            await _context.SaveChangesAsync();

            if (entrega.Tarea.PeriodoId.HasValue)
                await _calculadora.RecalcularHaciaArribaAsync(entrega.InscripcionId, entrega.Tarea.CursoAsignaturaId, entrega.Tarea.PeriodoId.Value);

            TempData["Mensaje"] = "Calificación guardada.";
            return RedirectToAction(nameof(Detalle), new { id = entrega.TareaId });
        }

        // POST: Tareas/MarcarEntregada — toggle manual, para una entrega presencial/oral que no
        // pasa por el sistema (no toca Calificación).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarEntregada(int entregaId, bool entregada)
        {
            var entrega = await _context.Entregas.Include(e => e.Tarea).FirstOrDefaultAsync(e => e.Id == entregaId);
            if (entrega == null) return NotFound();

            var (_, _, error) = await ObtenerConPermisoAsync(entrega.Tarea.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            entrega.Entregada = entregada;
            if (entregada && entrega.FechaEntrega == null) entrega.FechaEntrega = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Detalle), new { id = entrega.TareaId });
        }

        // Crea/actualiza/desvincula la Nota generada por esta Entrega, según Tarea.PeriodoId y si
        // hay Calificación cargada. Ver Entrega.NotaId para el detalle de cada caso.
        private async Task SincronizarNotaAsync(Entrega entrega)
        {
            var tarea = entrega.Tarea;

            if (!tarea.PeriodoId.HasValue || !entrega.Calificacion.HasValue)
            {
                if (entrega.NotaId.HasValue)
                {
                    var notaVieja = await _context.Notas.FindAsync(entrega.NotaId.Value);
                    if (notaVieja != null) notaVieja.Activo = false;
                    entrega.NotaId = null;
                }
                return;
            }

            var nota = entrega.NotaId.HasValue ? await _context.Notas.FindAsync(entrega.NotaId.Value) : null;

            if (nota == null || nota.PeriodoId != tarea.PeriodoId.Value)
            {
                if (nota != null) nota.Activo = false; // la Tarea cambió de Periodo: la vieja queda de baja.

                var maxOrden = await _context.Notas
                    .Where(n => n.InscripcionId == entrega.InscripcionId && n.CursoAsignaturaId == tarea.CursoAsignaturaId &&
                                n.PeriodoId == tarea.PeriodoId.Value && n.Orden > 0)
                    .Select(n => (int?)n.Orden).MaxAsync() ?? 0;

                nota = new Nota
                {
                    InscripcionId = entrega.InscripcionId,
                    CursoAsignaturaId = tarea.CursoAsignaturaId,
                    PeriodoId = tarea.PeriodoId.Value,
                    Orden = maxOrden + 1,
                    Valor = entrega.Calificacion.Value,
                    EsPromedioAutomatico = false,
                    Observacion = $"Tarea: {tarea.Titulo}",
                    CargadaPorNombre = entrega.CalificadaPorNombre
                };
                _context.Notas.Add(nota);
                await _context.SaveChangesAsync(); // necesitamos nota.Id antes de asignarlo.
                entrega.NotaId = nota.Id;
            }
            else
            {
                nota.Valor = entrega.Calificacion.Value;
                nota.ModificadaPorNombre = entrega.CalificadaPorNombre;
                nota.FechaModificacion = DateTime.Now;
            }
        }

        private async Task<List<Periodo>> PeriodosDelAnioAsync()
        {
            var anioActual = DateTime.Today.Year;
            return await _context.Periodos
                .Where(p => p.AnioLectivo == anioActual)
                .OrderBy(p => p.Tipo).ThenBy(p => p.Nombre)
                .ToListAsync();
        }
    }
}
