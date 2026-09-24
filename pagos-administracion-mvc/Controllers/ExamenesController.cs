using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // Gestión de Exámenes: solo Docente de la materia (o Admin en lectura).
    // Mismo criterio de permisos que TareasController.
    [Authorize(Roles = "Admin,Docente")]
    public class ExamenesController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExamenesController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper de permisos: igual patrón que TareasController.ObtenerConPermisoAsync.
        private async Task<(CursoAsignatura? ca, bool esDocentePropio, IActionResult? error)>
            ObtenerConPermisoAsync(int cursoAsignaturaId, bool soloLectura)
        {
            var ca = await _context.CursosAsignaturas
                .Include(x => x.Curso)
                .Include(x => x.Asignatura)
                .FirstOrDefaultAsync(x => x.Id == cursoAsignaturaId);

            if (ca == null) return (null, false, NotFound());

            var esDocentePropio = User.IsInRole("Docente") &&
                                  ca.DocenteUserId == _userManager.GetUserId(User);
            var esAdmin = User.IsInRole("Admin");

            if (soloLectura)
            {
                if (!esAdmin && !esDocentePropio) return (null, false, Forbid());
            }
            else if (!esDocentePropio)
            {
                return (null, false, Forbid());
            }

            return (ca, esDocentePropio, null);
        }

        // GET: Examenes — listado de materias donde el docente puede gestionar exámenes.
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
                .OrderBy(ca => ca.Curso.Nivel)
                .ThenBy(ca => ca.Curso.GradoAnio)
                .ThenBy(ca => ca.Asignatura.Nombre)
                .ToListAsync();

            return View(materias);
        }

        // GET: Examenes/ListarPorMateria/5
        public async Task<IActionResult> ListarPorMateria(int id)
        {
            var (ca, esDocentePropio, error) = await ObtenerConPermisoAsync(id, soloLectura: true);
            if (error != null) return error;

            var examenes = await _context.Examenes
                .Where(e => e.CursoAsignaturaId == id)
                .Include(e => e.Preguntas)
                .Include(e => e.Intentos)
                .OrderByDescending(e => e.FechaCreacion)
                .ToListAsync();

            ViewBag.CursoAsignatura = ca;
            ViewBag.EsDocentePropio = esDocentePropio;
            return View(examenes);
        }

        // GET: Examenes/Crear?cursoAsignaturaId=5
        [HttpGet]
        public async Task<IActionResult> Crear(int cursoAsignaturaId)
        {
            var (ca, _, error) = await ObtenerConPermisoAsync(cursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            var periodos = await _context.Periodos
                .Where(p => p.AnioLectivo == DateTime.Today.Year)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            var vm = new ExamenCreateEditViewModel
            {
                CursoAsignaturaId = cursoAsignaturaId,
                NombreCurso = $"{ca!.Curso.GradoAnio}° {ca.Curso.Nombre}",
                NombreAsignatura = ca.Asignatura.Nombre,
                FechaDesde = DateTime.Today,
                FechaHasta = DateTime.Today.AddDays(7),
                Periodos = new SelectList(periodos, "Id", "Nombre"),
                Preguntas = new List<PreguntaViewModel>
                {
                    new() { Orden = 1 }
                }
            };

            return View(vm);
        }

        // POST: Examenes/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ExamenCreateEditViewModel vm)
        {
            var (ca, _, error) = await ObtenerConPermisoAsync(vm.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            // Validar que cada pregunta tenga al menos 2 opciones con texto
            foreach (var p in vm.Preguntas)
            {
                var opcionesConTexto = p.Opciones.Where(o => !string.IsNullOrWhiteSpace(o.Texto)).ToList();
                if (opcionesConTexto.Count < 2)
                    ModelState.AddModelError("", $"La pregunta {p.Orden} debe tener al menos 2 opciones.");
                if (p.OpcionCorrectaIndex < 0 || p.OpcionCorrectaIndex >= opcionesConTexto.Count)
                    ModelState.AddModelError("", $"La pregunta {p.Orden} debe tener una opción correcta válida.");
            }

            if (!ModelState.IsValid)
            {
                await RecargarSelectLists(vm, ca!.CursoId);
                vm.NombreCurso = $"{ca!.Curso.GradoAnio}° {ca.Curso.Nombre}";
                vm.NombreAsignatura = ca.Asignatura.Nombre;
                return View(vm);
            }

            var examen = new Examen
            {
                CursoAsignaturaId = vm.CursoAsignaturaId,
                Titulo = vm.Titulo,
                Descripcion = vm.Descripcion,
                FechaDesde = vm.FechaDesde,
                FechaHasta = vm.FechaHasta,
                TiempoLimiteMinutos = vm.TiempoLimiteMinutos,
                NotaMinimaAprobatoria = vm.NotaMinimaAprobatoria,
                SoloUnIntento = vm.SoloUnIntento,
                OrdenAleatorio = vm.OrdenAleatorio,
                MostrarResultado = vm.MostrarResultado,
                PeriodoId = vm.PeriodoId,
                CreadoPorNombre = User.Identity?.Name,
                FechaCreacion = DateTime.Now,
            };

            foreach (var (pVm, idx) in vm.Preguntas.Select((p, i) => (p, i)))
            {
                var pregunta = new PreguntaExamen
                {
                    Enunciado = pVm.Enunciado,
                    Orden = idx + 1,
                    Puntaje = pVm.Puntaje,
                };

                var opcionesConTexto = pVm.Opciones
                    .Where(o => !string.IsNullOrWhiteSpace(o.Texto))
                    .ToList();

                var letras = new[] { "A", "B", "C", "D", "E", "F" };
                for (int i = 0; i < opcionesConTexto.Count; i++)
                {
                    pregunta.Opciones.Add(new OpcionRespuesta
                    {
                        Texto = opcionesConTexto[i].Texto,
                        Letra = letras[i],
                        EsCorrecta = i == pVm.OpcionCorrectaIndex,
                    });
                }
                examen.Preguntas.Add(pregunta);
            }

            _context.Examenes.Add(examen);
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Examen \"{examen.Titulo}\" creado correctamente.";
            return RedirectToAction(nameof(ListarPorMateria), new { id = vm.CursoAsignaturaId });
        }

        // GET: Examenes/Editar/5
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var examen = await _context.Examenes
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Curso)
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Asignatura)
                .Include(e => e.Preguntas.Where(p => p.Activo)).ThenInclude(p => p.Opciones)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (examen == null) return NotFound();

            var (_, _, error) = await ObtenerConPermisoAsync(examen.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            // No se puede editar si ya hay intentos
            if (await _context.IntentosExamen.AnyAsync(i => i.ExamenId == id))
            {
                TempData["Error"] = "No se puede editar el examen porque ya tiene intentos registrados.";
                return RedirectToAction(nameof(ListarPorMateria), new { id = examen.CursoAsignaturaId });
            }

            var periodos = await _context.Periodos
                .Where(p => p.AnioLectivo == DateTime.Today.Year)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            var vm = new ExamenCreateEditViewModel
            {
                Id = examen.Id,
                CursoAsignaturaId = examen.CursoAsignaturaId,
                Titulo = examen.Titulo,
                Descripcion = examen.Descripcion,
                FechaDesde = examen.FechaDesde,
                FechaHasta = examen.FechaHasta,
                TiempoLimiteMinutos = examen.TiempoLimiteMinutos,
                NotaMinimaAprobatoria = examen.NotaMinimaAprobatoria,
                SoloUnIntento = examen.SoloUnIntento,
                OrdenAleatorio = examen.OrdenAleatorio,
                MostrarResultado = examen.MostrarResultado,
                PeriodoId = examen.PeriodoId,
                NombreCurso = $"{examen.CursoAsignatura.Curso.GradoAnio}° {examen.CursoAsignatura.Curso.Nombre}",
                NombreAsignatura = examen.CursoAsignatura.Asignatura.Nombre,
                Periodos = new SelectList(periodos, "Id", "Nombre", examen.PeriodoId),
                Preguntas = examen.Preguntas
                    .OrderBy(p => p.Orden)
                    .Select(p => new PreguntaViewModel
                    {
                        Id = p.Id,
                        Enunciado = p.Enunciado,
                        Orden = p.Orden,
                        Puntaje = p.Puntaje,
                        OpcionCorrectaIndex = p.Opciones
                            .OrderBy(o => o.Letra)
                            .ToList()
                            .FindIndex(o => o.EsCorrecta),
                        Opciones = p.Opciones
                            .OrderBy(o => o.Letra)
                            .Select(o => new OpcionViewModel
                            {
                                Id = o.Id,
                                Texto = o.Texto,
                                Letra = o.Letra,
                            }).ToList()
                    }).ToList()
            };

            return View(vm);
        }

        // POST: Examenes/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, ExamenCreateEditViewModel vm)
        {
            var examen = await _context.Examenes
                .Include(e => e.Preguntas.Where(p => p.Activo)).ThenInclude(p => p.Opciones)
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Curso)
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Asignatura)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (examen == null) return NotFound();

            var (ca, _, error) = await ObtenerConPermisoAsync(examen.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            if (!ModelState.IsValid)
            {
                await RecargarSelectLists(vm, ca!.CursoId);
                vm.NombreCurso = $"{ca!.Curso.GradoAnio}° {ca.Curso.Nombre}";
                vm.NombreAsignatura = ca.Asignatura.Nombre;
                return View(vm);
            }

            // Actualizar campos del examen
            examen.Titulo = vm.Titulo;
            examen.Descripcion = vm.Descripcion;
            examen.FechaDesde = vm.FechaDesde;
            examen.FechaHasta = vm.FechaHasta;
            examen.TiempoLimiteMinutos = vm.TiempoLimiteMinutos;
            examen.NotaMinimaAprobatoria = vm.NotaMinimaAprobatoria;
            examen.SoloUnIntento = vm.SoloUnIntento;
            examen.OrdenAleatorio = vm.OrdenAleatorio;
            examen.MostrarResultado = vm.MostrarResultado;
            examen.PeriodoId = vm.PeriodoId;

            // Soft-delete de preguntas antiguas y recrear
            foreach (var p in examen.Preguntas) p.Activo = false;

            var letras = new[] { "A", "B", "C", "D", "E", "F" };
            foreach (var (pVm, idx) in vm.Preguntas.Select((p, i) => (p, i)))
            {
                var nuevaPregunta = new PreguntaExamen
                {
                    ExamenId = examen.Id,
                    Enunciado = pVm.Enunciado,
                    Orden = idx + 1,
                    Puntaje = pVm.Puntaje,
                };

                var opcionesConTexto = pVm.Opciones
                    .Where(o => !string.IsNullOrWhiteSpace(o.Texto))
                    .ToList();

                for (int i = 0; i < opcionesConTexto.Count; i++)
                {
                    nuevaPregunta.Opciones.Add(new OpcionRespuesta
                    {
                        Texto = opcionesConTexto[i].Texto,
                        Letra = letras[i],
                        EsCorrecta = i == pVm.OpcionCorrectaIndex,
                    });
                }
                _context.PreguntasExamen.Add(nuevaPregunta);
            }

            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Examen \"{examen.Titulo}\" actualizado correctamente.";
            return RedirectToAction(nameof(ListarPorMateria), new { id = examen.CursoAsignaturaId });
        }

        // GET: Examenes/Resultados/5 — ver intentos de todos los alumnos para un examen
        public async Task<IActionResult> Resultados(int id)
        {
            var examen = await _context.Examenes
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Curso)
                .Include(e => e.CursoAsignatura).ThenInclude(ca => ca.Asignatura)
                .Include(e => e.Intentos)
                    .ThenInclude(i => i.Inscripcion)
                    .ThenInclude(ins => ins.Alumno)
                .Include(e => e.Intentos)
                    .ThenInclude(i => i.Respuestas)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (examen == null) return NotFound();

            var (_, _, error) = await ObtenerConPermisoAsync(examen.CursoAsignaturaId, soloLectura: true);
            if (error != null) return error;

            return View(examen);
        }

        // POST: Examenes/Eliminar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var examen = await _context.Examenes
                .Include(e => e.Intentos)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (examen == null) return NotFound();

            var (_, _, error) = await ObtenerConPermisoAsync(examen.CursoAsignaturaId, soloLectura: false);
            if (error != null) return error;

            if (examen.Intentos.Any())
            {
                TempData["Error"] = "No se puede eliminar el examen porque ya tiene intentos registrados.";
                return RedirectToAction(nameof(ListarPorMateria), new { id = examen.CursoAsignaturaId });
            }

            examen.Activo = false;
            await _context.SaveChangesAsync();

            TempData["Exito"] = "Examen eliminado correctamente.";
            return RedirectToAction(nameof(ListarPorMateria), new { id = examen.CursoAsignaturaId });
        }

        private async Task RecargarSelectLists(ExamenCreateEditViewModel vm, int cursoId)
        {
            var periodos = await _context.Periodos
                .Where(p => p.AnioLectivo == DateTime.Today.Year)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            vm.Periodos = new SelectList(periodos, "Id", "Nombre", vm.PeriodoId);
        }
    }
}
