using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CursosController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CursosController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<SelectList> ObtenerDocentesSelectListAsync(string? seleccionado = null)
        {
            var docentes = await _userManager.GetUsersInRoleAsync("Docente");
            return new SelectList(docentes.OrderBy(d => d.Email), "Id", "Email", seleccionado);
        }

        private async Task<List<Asignatura>> ObtenerMateriasDelNivelAsync(NivelEducativo nivel, HashSet<int>? excluirIds = null)
        {
            var query = _context.Asignaturas.Where(a => a.Nivel == nivel);
            if (excluirIds != null && excluirIds.Count > 0)
                query = query.Where(a => !excluirIds.Contains(a.Id));
            return await query.OrderBy(a => a.Nombre).ToListAsync();
        }

        // GET: Cursos
        public async Task<IActionResult> Index()
        {
            var cursos = await _context.Cursos
                .Include(c => c.Inscripciones)
                .Include(c => c.ProfesorUser)
                .Include(c => c.CursosAsignaturas)
                .OrderBy(c => c.Nivel).ThenBy(c => c.GradoAnio).ThenBy(c => c.Turno)
                .ToListAsync();

            return View(cursos);
        }

        // GET: Cursos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var curso = await _context.Cursos
                .Include(c => c.Inscripciones).ThenInclude(i => i.Alumno)
                .Include(c => c.ProfesorUser)
                .Include(c => c.CursosAsignaturas).ThenInclude(ca => ca.Asignatura)
                .Include(c => c.CursosAsignaturas).ThenInclude(ca => ca.DocenteUser)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (curso == null) return NotFound();

            // Alumnos activos que coinciden en Nivel + Grado/Año + Turno y todavía no están
            // inscriptos en este curso. Mismo criterio que "Buscar coincidencias" en Create — un
            // alumno de 5to año de Secundaria no puede aparecer acá para un curso de 2do Primaria.
            var idsInscriptos = curso.Inscripciones.Select(i => i.AlumnoId).ToHashSet();
            ViewBag.AlumnosDisponibles = await _context.Alumnos
                .Where(a => !idsInscriptos.Contains(a.Id)
                    && a.Nivel == curso.Nivel && a.GradoAnio == curso.GradoAnio && a.Turno == curso.Turno)
                .OrderBy(a => a.Apellido)
                .ToListAsync();

            // Materias del mismo Nivel que este curso, que todavía no se le asignaron.
            var idsAsignaturasEnCurso = curso.CursosAsignaturas.Select(ca => ca.AsignaturaId).ToHashSet();
            ViewBag.AsignaturasDisponibles = await ObtenerMateriasDelNivelAsync(curso.Nivel, idsAsignaturasEnCurso);
            ViewBag.Docentes = await ObtenerDocentesSelectListAsync();

            return View(curso);
        }

        // GET: Cursos/Create
        // Si vienen nivel/gradoAnio/turno por querystring (desde el botón "Buscar coincidencias"
        // del propio formulario), calcula qué alumnos ya cargados matchean esa combinación,
        // para poder matricularlos de una sin tener que hacerlo a mano desde Details.
        public async Task<IActionResult> Create(NivelEducativo? nivel, int? gradoAnio, Turno? turno, string? nombre, string? profesorUserId, List<int>? diasEF, List<int>? alumnosAMatricular, List<int>? materiasAAsignar)
        {
            var curso = new Curso
            {
                Nivel = nivel ?? default,
                GradoAnio = gradoAnio ?? 0,
                Turno = turno ?? default,
                Nombre = nombre ?? string.Empty,
                ProfesorUserId = profesorUserId,
                DiasEducacionFisica = CombinarDias(diasEF)
            };

            ViewBag.ProfesorUserId = await ObtenerDocentesSelectListAsync(profesorUserId);
            ViewBag.DiasEFSeleccionados = diasEF ?? new List<int>();
            ViewBag.Buscado = nivel.HasValue && gradoAnio.HasValue && turno.HasValue;

            if (ViewBag.Buscado)
            {
                var coincidentes = await _context.Alumnos
                    .Where(a => a.Nivel == nivel && a.GradoAnio == gradoAnio && a.Turno == turno)
                    .OrderBy(a => a.Apellido)
                    .ToListAsync();

                ViewBag.AlumnosCoincidentes = coincidentes;
                // Primera búsqueda: todos tildados por default. Si el admin ya destildó alguno
                // y volvió a buscar (o falló la validación), se respeta lo que venía marcado.
                ViewBag.AlumnosSeleccionados = alumnosAMatricular ?? coincidentes.Select(a => a.Id).ToList();

                // Mismo criterio para materias: se listan las del Nivel elegido, tildadas por
                // default (el admin destilda las que no correspondan; el resto se puede sumar
                // después desde el detalle del curso, junto con el Docente de cada una).
                var materiasDelNivel = await ObtenerMateriasDelNivelAsync(nivel!.Value);
                ViewBag.MateriasDelNivel = materiasDelNivel;
                ViewBag.MateriasSeleccionadas = materiasAAsignar ?? materiasDelNivel.Select(m => m.Id).ToList();
            }

            return View(curso);
        }

        // POST: Cursos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Nivel,GradoAnio,Turno,ProfesorUserId,MetaPresentismo")] Curso curso, List<int>? diasEF, List<int>? alumnosAMatricular, List<int>? materiasAAsignar)
        {
            curso.DiasEducacionFisica = CombinarDias(diasEF);
            curso.Nombre ??= string.Empty;

            if (ModelState.IsValid)
            {
                _context.Add(curso);
                await _context.SaveChangesAsync();

                if (alumnosAMatricular != null && alumnosAMatricular.Any())
                {
                    foreach (var alumnoId in alumnosAMatricular)
                        _context.Inscripciones.Add(new Inscripcion { CursoId = curso.Id, AlumnoId = alumnoId });

                    await _context.SaveChangesAsync();
                }

                if (materiasAAsignar != null && materiasAAsignar.Any())
                {
                    // Sin Docente todavía: se asigna después desde Details, materia por materia
                    // (ahí es más claro elegir "quién dicta qué" que en este mismo formulario).
                    foreach (var asignaturaId in materiasAAsignar)
                        _context.CursosAsignaturas.Add(new CursoAsignatura { CursoId = curso.Id, AsignaturaId = asignaturaId });

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.ProfesorUserId = await ObtenerDocentesSelectListAsync(curso.ProfesorUserId);
            ViewBag.DiasEFSeleccionados = diasEF ?? new List<int>();
            ViewBag.Buscado = true;
            ViewBag.AlumnosCoincidentes = await _context.Alumnos
                .Where(a => a.Nivel == curso.Nivel && a.GradoAnio == curso.GradoAnio && a.Turno == curso.Turno)
                .OrderBy(a => a.Apellido)
                .ToListAsync();
            ViewBag.AlumnosSeleccionados = alumnosAMatricular ?? new List<int>();
            ViewBag.MateriasDelNivel = await ObtenerMateriasDelNivelAsync(curso.Nivel);
            ViewBag.MateriasSeleccionadas = materiasAAsignar ?? new List<int>();

            return View(curso);
        }

        // GET: Cursos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var curso = await _context.Cursos.FindAsync(id);
            if (curso == null) return NotFound();

            ViewBag.ProfesorUserId = await ObtenerDocentesSelectListAsync(curso.ProfesorUserId);
            return View(curso);
        }

        // POST: Cursos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Nivel,GradoAnio,Turno,ProfesorUserId,MetaPresentismo")] Curso curso, List<int>? diasEF)
        {
            if (id != curso.Id) return NotFound();

            curso.DiasEducacionFisica = CombinarDias(diasEF);
            curso.Nombre ??= string.Empty;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(curso);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CursoExists(curso.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ProfesorUserId = await ObtenerDocentesSelectListAsync(curso.ProfesorUserId);
            return View(curso);
        }

        // GET: Cursos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var curso = await _context.Cursos
                .Include(c => c.Inscripciones)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (curso == null) return NotFound();

            ViewBag.AlumnosInscriptos = curso.Inscripciones.Count;

            return View(curso);
        }

        // POST: Cursos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var curso = await _context.Cursos.FindAsync(id);
            if (curso != null)
            {
                // Soft delete: mismo criterio que Alumno, no se borra físicamente para no perder historial.
                curso.Activo = false;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Cursos/Matricular (agrega un Alumno al curso, desde Details)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Matricular(int cursoId, int alumnoId)
        {
            var curso = await _context.Cursos.FindAsync(cursoId);
            var alumno = await _context.Alumnos.FindAsync(alumnoId);
            if (curso == null || alumno == null) return NotFound();

            if (alumno.Nivel != curso.Nivel || alumno.GradoAnio != curso.GradoAnio || alumno.Turno != curso.Turno)
            {
                TempData["Error"] = $"{alumno.Apellido}, {alumno.Nombre} es de {alumno.Nivel} {alumno.GradoAnio}° ({alumno.Turno}) y no coincide con este curso ({curso.Nivel} {curso.GradoAnio}°, {curso.Turno}). No se matriculó.";
                return RedirectToAction(nameof(Details), new { id = cursoId });
            }

            var yaInscripto = await _context.Inscripciones
                .AnyAsync(i => i.CursoId == cursoId && i.AlumnoId == alumnoId);

            if (!yaInscripto)
            {
                _context.Inscripciones.Add(new Inscripcion { CursoId = cursoId, AlumnoId = alumnoId });
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"{alumno.Apellido}, {alumno.Nombre} quedó matriculado en el curso.";
            }

            return RedirectToAction(nameof(Details), new { id = cursoId });
        }

        // POST: Cursos/Desmatricular (baja lógica de una Inscripcion, desde Details)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desmatricular(int inscripcionId, int cursoId)
        {
            var inscripcion = await _context.Inscripciones.FindAsync(inscripcionId);
            if (inscripcion != null)
            {
                // Soft delete: mismo criterio que el resto del proyecto.
                inscripcion.Activo = false;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = cursoId });
        }

        // POST: Cursos/AgregarAsignatura (suma una materia del catálogo a este curso, sin Docente todavía)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarAsignatura(int cursoId, int asignaturaId)
        {
            var yaAsignada = await _context.CursosAsignaturas
                .AnyAsync(ca => ca.CursoId == cursoId && ca.AsignaturaId == asignaturaId);

            if (!yaAsignada)
            {
                _context.CursosAsignaturas.Add(new CursoAsignatura { CursoId = cursoId, AsignaturaId = asignaturaId });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = cursoId });
        }

        // POST: Cursos/QuitarAsignatura (baja lógica de un CursoAsignatura, desde Details)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarAsignatura(int cursoAsignaturaId, int cursoId)
        {
            var cursoAsignatura = await _context.CursosAsignaturas.FindAsync(cursoAsignaturaId);
            if (cursoAsignatura != null)
            {
                // Soft delete: mismo criterio que el resto del proyecto. Si ya tiene Notas
                // cargadas, no se pierde ese historial (Nota -> CursoAsignatura es Restrict).
                cursoAsignatura.Activo = false;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = cursoId });
        }

        // POST: Cursos/AsignarDocente (asigna/cambia el Docente de una materia dentro del curso)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarDocente(int cursoAsignaturaId, int cursoId, string? docenteUserId)
        {
            var cursoAsignatura = await _context.CursosAsignaturas
                .Include(ca => ca.Asignatura)
                .FirstOrDefaultAsync(ca => ca.Id == cursoAsignaturaId);

            if (cursoAsignatura == null)
            {
                TempData["Error"] = "No se encontró esa materia del curso (puede que se haya quitado). Recargá la página.";
                return RedirectToAction(nameof(Details), new { id = cursoId });
            }

            // "" desde el <select> significa "Sin asignar": se guarda como null, no como cadena vacía.
            cursoAsignatura.DocenteUserId = string.IsNullOrEmpty(docenteUserId) ? null : docenteUserId;
            await _context.SaveChangesAsync();

            if (cursoAsignatura.DocenteUserId == null)
                TempData["Mensaje"] = $"Se quitó el Docente de {cursoAsignatura.Asignatura.Nombre}.";
            else
            {
                var docente = await _userManager.FindByIdAsync(cursoAsignatura.DocenteUserId);
                TempData["Mensaje"] = $"{docente?.Email ?? "Docente"} quedó asignado a {cursoAsignatura.Asignatura.Nombre}.";
            }

            return RedirectToAction(nameof(Details), new { id = cursoId });
        }

        private static DiasSemana CombinarDias(List<int>? valores)
        {
            var resultado = DiasSemana.Ninguno;
            if (valores == null) return resultado;
            foreach (var v in valores)
                resultado |= (DiasSemana)v;
            return resultado;
        }

        private bool CursoExists(int id) => _context.Cursos.Any(e => e.Id == id);
    }
}
