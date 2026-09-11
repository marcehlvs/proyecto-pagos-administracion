using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;

namespace pagos_administracion_mvc.Controllers
{
    // Admin puede cargar notas de cualquier materia. Docente solo de las CursoAsignatura donde
    // figura como DocenteUserId (se valida en cada acción, mismo criterio que AsistenciasController
    // con Curso.ProfesorUserId, para que no alcance con cambiar el id en la URL).
    [Authorize(Roles = "Admin,Docente")]
    public class NotasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotaCalculadora _calculadora;

        public NotasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager, NotaCalculadora calculadora)
        {
            _context = context;
            _userManager = userManager;
            _calculadora = calculadora;
        }

        private async Task<(CursoAsignatura? cursoAsignatura, IActionResult? error)> ObtenerConPermiso(int cursoAsignaturaId)
        {
            var cursoAsignatura = await _context.CursosAsignaturas
                .Include(ca => ca.Curso)
                .Include(ca => ca.Asignatura)
                .FirstOrDefaultAsync(ca => ca.Id == cursoAsignaturaId);
            if (cursoAsignatura == null) return (null, NotFound());

            if (User.IsInRole("Docente") && !User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                if (cursoAsignatura.DocenteUserId != userId) return (null, Forbid());
            }

            return (cursoAsignatura, null);
        }

        // GET: Notas — mis materias (Docente) o todas (Admin), para elegir dónde cargar notas.
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

        // GET: Notas/Cargar?cursoAsignaturaId=1&periodoId=3
        // Sin periodoId: solo pide elegir el Periodo. Con periodoId: lista los alumnos
        // inscriptos con su nota actual para ese Periodo (calculada si es contenedor, o la
        // cargada a mano si es un Parcial u override).
        public async Task<IActionResult> Cargar(int cursoAsignaturaId, int? periodoId)
        {
            var (cursoAsignatura, error) = await ObtenerConPermiso(cursoAsignaturaId);
            if (error != null) return error;

            var anioActual = DateTime.Today.Year;
            var modelo = new CargarNotasViewModel
            {
                CursoAsignatura = cursoAsignatura!,
                PeriodosDisponibles = await _context.Periodos
                    .Where(p => p.AnioLectivo == anioActual)
                    .OrderBy(p => p.Tipo).ThenBy(p => p.Nombre)
                    .ToListAsync()
            };

            if (periodoId.HasValue)
            {
                modelo.PeriodoSeleccionado = await _context.Periodos
                    .Include(p => p.Subperiodos)
                    .FirstOrDefaultAsync(p => p.Id == periodoId);
                if (modelo.PeriodoSeleccionado == null) return NotFound();

                var inscripciones = await _context.Inscripciones
                    .Include(i => i.Alumno)
                    .Where(i => i.CursoId == cursoAsignatura!.CursoId)
                    .OrderBy(i => i.Alumno.Apellido)
                    .ToListAsync();

                foreach (var inscripcion in inscripciones)
                {
                    decimal? valor;
                    bool esAutomatico;

                    if (modelo.EsPeriodoContenedor)
                    {
                        // Contenedor: se calcula (o se respeta el override existente). Esto puede
                        // crear/actualizar la Nota "caché" en la base, por eso el orden importa:
                        // primero se muestra, recién al tocar "Guardar" se fija como manual.
                        valor = await _calculadora.CalcularAsync(inscripcion.Id, cursoAsignaturaId, periodoId.Value);
                        var notaCache = await _context.Notas.FirstOrDefaultAsync(n =>
                            n.InscripcionId == inscripcion.Id && n.CursoAsignaturaId == cursoAsignaturaId && n.PeriodoId == periodoId);
                        esAutomatico = notaCache?.EsPromedioAutomatico ?? true;
                    }
                    else
                    {
                        // Parcial: solo lo que ya se cargó a mano, si se cargó.
                        var nota = await _context.Notas.FirstOrDefaultAsync(n =>
                            n.InscripcionId == inscripcion.Id && n.CursoAsignaturaId == cursoAsignaturaId && n.PeriodoId == periodoId);
                        valor = nota?.Valor;
                        esAutomatico = false;
                    }

                    modelo.Filas.Add(new FilaAlumnoNota
                    {
                        InscripcionId = inscripcion.Id,
                        AlumnoNombre = $"{inscripcion.Alumno.Apellido}, {inscripcion.Alumno.Nombre}",
                        Valor = valor,
                        EsPromedioAutomatico = esAutomatico
                    });
                }
            }

            return View(modelo);
        }

        // POST: Notas/Guardar
        // Todo lo que llega en "valores" se guarda como carga manual (EsPromedioAutomatico =
        // false): si es un Parcial, es la única forma de cargarlo; si es un Periodo contenedor,
        // el Docente está pisando el promedio a propósito (ver NotaCalculadora). Los campos que
        // llegan vacíos no se tocan.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(int cursoAsignaturaId, int periodoId, Dictionary<int, decimal?> valores)
        {
            var (cursoAsignatura, error) = await ObtenerConPermiso(cursoAsignaturaId);
            if (error != null) return error;

            var nombreDocente = User.Identity?.Name ?? "Docente";
            var idsConValor = valores.Where(v => v.Value.HasValue).Select(v => v.Key).ToList();
            var existentes = await _context.Notas
                .Where(n => idsConValor.Contains(n.InscripcionId) && n.CursoAsignaturaId == cursoAsignaturaId && n.PeriodoId == periodoId)
                .ToListAsync();

            foreach (var (inscripcionId, valor) in valores)
            {
                if (!valor.HasValue) continue;

                var nota = existentes.FirstOrDefault(n => n.InscripcionId == inscripcionId);
                if (nota != null)
                {
                    nota.Valor = valor.Value;
                    nota.EsPromedioAutomatico = false;
                    nota.ModificadaPorNombre = nombreDocente;
                    nota.FechaModificacion = DateTime.Now;
                }
                else
                {
                    _context.Notas.Add(new Nota
                    {
                        InscripcionId = inscripcionId,
                        CursoAsignaturaId = cursoAsignaturaId,
                        PeriodoId = periodoId,
                        Valor = valor.Value,
                        EsPromedioAutomatico = false,
                        CargadaPorNombre = nombreDocente
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Si lo que se acaba de cargar son Parciales (o cualquier hoja), los períodos de
            // arriba (Trimestral/Cuatrimestral/Anual) quedan desactualizados hasta que alguien
            // entra a verlos. Se recalculan ahora para que el boletín ya los tenga al día.
            foreach (var inscripcionId in idsConValor)
                await _calculadora.RecalcularJerarquiaAsync(inscripcionId, cursoAsignaturaId, DateTime.Today.Year);

            TempData["Mensaje"] = "Notas guardadas.";
            return RedirectToAction(nameof(Cargar), new { cursoAsignaturaId, periodoId });
        }

        // POST: Notas/Recalcular — descarta un override manual y vuelve a dejar que el Periodo
        // (contenedor) se calcule solo a partir de sus Subperiodos.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recalcular(int cursoAsignaturaId, int periodoId, int inscripcionId)
        {
            var (cursoAsignatura, error) = await ObtenerConPermiso(cursoAsignaturaId);
            if (error != null) return error;

            var nota = await _context.Notas.FirstOrDefaultAsync(n =>
                n.InscripcionId == inscripcionId && n.CursoAsignaturaId == cursoAsignaturaId && n.PeriodoId == periodoId);
            if (nota != null)
            {
                nota.EsPromedioAutomatico = true;
                await _context.SaveChangesAsync();
            }

            await _calculadora.CalcularAsync(inscripcionId, cursoAsignaturaId, periodoId);
            return RedirectToAction(nameof(Cargar), new { cursoAsignaturaId, periodoId });
        }
    }
}
