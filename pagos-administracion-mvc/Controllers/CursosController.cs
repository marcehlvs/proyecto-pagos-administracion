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

        // GET: Cursos
        public async Task<IActionResult> Index()
        {
            var cursos = await _context.Cursos
                .Include(c => c.Inscripciones)
                .Include(c => c.ProfesorUser)
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
                .FirstOrDefaultAsync(c => c.Id == id);
            if (curso == null) return NotFound();

            // Alumnos activos que todavía no están inscriptos en este curso, para el formulario de matriculación.
            var idsInscriptos = curso.Inscripciones.Select(i => i.AlumnoId).ToHashSet();
            ViewBag.AlumnosDisponibles = await _context.Alumnos
                .Where(a => !idsInscriptos.Contains(a.Id))
                .OrderBy(a => a.Apellido)
                .ToListAsync();

            return View(curso);
        }

        // GET: Cursos/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.ProfesorUserId = await ObtenerDocentesSelectListAsync();
            return View();
        }

        // POST: Cursos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Nivel,GradoAnio,Turno,ProfesorUserId")] Curso curso, List<int>? diasEF)
        {
            curso.DiasEducacionFisica = CombinarDias(diasEF);

            if (ModelState.IsValid)
            {
                _context.Add(curso);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ProfesorUserId = await ObtenerDocentesSelectListAsync(curso.ProfesorUserId);
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
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Nivel,GradoAnio,Turno,ProfesorUserId")] Curso curso, List<int>? diasEF)
        {
            if (id != curso.Id) return NotFound();

            curso.DiasEducacionFisica = CombinarDias(diasEF);

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
            var yaInscripto = await _context.Inscripciones
                .AnyAsync(i => i.CursoId == cursoId && i.AlumnoId == alumnoId);

            if (!yaInscripto)
            {
                _context.Inscripciones.Add(new Inscripcion { CursoId = cursoId, AlumnoId = alumnoId });
                await _context.SaveChangesAsync();
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
