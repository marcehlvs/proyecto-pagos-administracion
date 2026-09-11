using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    // CRUD del catálogo de materias curriculares (Matemática, Lengua, etc.). Las Asignaturas se
    // cargan acá una sola vez; después se van asignando a cada Curso desde CursosController
    // (Create y Details), donde también se les asigna el Docente que las dicta.
    [Authorize(Roles = "Admin")]
    public class AsignaturasController : Controller
    {
        private readonly AdministracionDbContext _context;
        public AsignaturasController(AdministracionDbContext context) => _context = context;

        public async Task<IActionResult> Index() =>
            View(await _context.Asignaturas
                .OrderBy(a => a.Nivel).ThenBy(a => a.Nombre)
                .ToListAsync());

        public IActionResult Create(NivelEducativo? nivel) => View(new Asignatura { Nivel = nivel ?? default });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Nivel")] Asignatura asignatura)
        {
            if (ModelState.IsValid)
            {
                _context.Add(asignatura);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(asignatura);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var asignatura = await _context.Asignaturas.FindAsync(id);
            if (asignatura == null) return NotFound();
            return View(asignatura);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Nivel")] Asignatura asignatura)
        {
            if (id != asignatura.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(asignatura);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Asignaturas.Any(a => a.Id == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(asignatura);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var asignatura = await _context.Asignaturas.FirstOrDefaultAsync(a => a.Id == id);
            if (asignatura == null) return NotFound();
            return View(asignatura);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var asignatura = await _context.Asignaturas.FindAsync(id);
            if (asignatura != null)
            {
                // Soft delete: mismo criterio que el resto del proyecto. Si ya está asignada a
                // algún Curso o tiene Notas cargadas, no se pierde ese historial.
                asignatura.Activo = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
