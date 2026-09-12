using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // CRUD de Periodo (1er Parcial -> 1er Trimestre -> 1er Cuatrimestre -> Nota Final). El Admin
    // arma esta jerarquía una vez por año lectivo; NotasController y NotaCalculadora la usan
    // para saber qué promedia a qué.
    [Authorize(Roles = "Admin")]
    public class PeriodosController : Controller
    {
        private readonly AdministracionDbContext _context;
        public PeriodosController(AdministracionDbContext context) => _context = context;

        private async Task<SelectList> ObtenerPadresSelectListAsync(int anioLectivo, int? excluirId = null, object? seleccionado = null)
        {
            var query = _context.Periodos.Where(p => p.AnioLectivo == anioLectivo);
            if (excluirId.HasValue) query = query.Where(p => p.Id != excluirId);
            var periodos = await query.OrderBy(p => p.Tipo).ThenBy(p => p.Nombre).ToListAsync();
            return new SelectList(periodos, "Id", "Nombre", seleccionado);
        }

        public async Task<IActionResult> Index(int? anioLectivo)
        {
            var anio = anioLectivo ?? DateTime.Today.Year;
            ViewBag.AnioLectivo = anio;

            var periodos = await _context.Periodos
                .Include(p => p.PeriodoPadre)
                .Where(p => p.AnioLectivo == anio)
                .OrderBy(p => p.Tipo).ThenBy(p => p.Nombre)
                .ToListAsync();

            return View(periodos);
        }

        public async Task<IActionResult> Create(int? anioLectivo)
        {
            var anio = anioLectivo ?? DateTime.Today.Year;
            ViewBag.PeriodoPadreId = await ObtenerPadresSelectListAsync(anio);
            return View(new Periodo { AnioLectivo = anio });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Tipo,AnioLectivo,PeriodoPadreId,FechaInicio,FechaFin")] Periodo periodo)
        {
            if (ModelState.IsValid)
            {
                _context.Add(periodo);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { anioLectivo = periodo.AnioLectivo });
            }
            ViewBag.PeriodoPadreId = await ObtenerPadresSelectListAsync(periodo.AnioLectivo, seleccionado: periodo.PeriodoPadreId);
            return View(periodo);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var periodo = await _context.Periodos.FindAsync(id);
            if (periodo == null) return NotFound();

            ViewBag.PeriodoPadreId = await ObtenerPadresSelectListAsync(periodo.AnioLectivo, id, periodo.PeriodoPadreId);
            return View(periodo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Tipo,AnioLectivo,PeriodoPadreId,FechaInicio,FechaFin")] Periodo periodo)
        {
            if (id != periodo.Id) return NotFound();

            // Un Periodo no puede ser padre de sí mismo (rompería la recursión de NotaCalculadora).
            if (periodo.PeriodoPadreId == periodo.Id)
                ModelState.AddModelError(nameof(periodo.PeriodoPadreId), "Un período no puede ser padre de sí mismo.");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(periodo);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Periodos.Any(p => p.Id == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index), new { anioLectivo = periodo.AnioLectivo });
            }
            ViewBag.PeriodoPadreId = await ObtenerPadresSelectListAsync(periodo.AnioLectivo, id, periodo.PeriodoPadreId);
            return View(periodo);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var periodo = await _context.Periodos.Include(p => p.PeriodoPadre).FirstOrDefaultAsync(p => p.Id == id);
            if (periodo == null) return NotFound();
            return View(periodo);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var periodo = await _context.Periodos.FindAsync(id);
            if (periodo != null)
            {
                var tieneHijos = await _context.Periodos.AnyAsync(p => p.PeriodoPadreId == id);
                if (tieneHijos)
                {
                    // No se da de baja un período que todavía tiene subperiodos colgando: se
                    // quedarían "huérfanos" para el cálculo (NotaCalculadora ya no lo vería).
                    TempData["Error"] = "No se puede eliminar: tiene períodos hijos. Eliminá primero esos.";
                    return RedirectToAction(nameof(Index), new { anioLectivo = periodo.AnioLectivo });
                }

                // Soft delete: mismo criterio que el resto del proyecto.
                periodo.Activo = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
