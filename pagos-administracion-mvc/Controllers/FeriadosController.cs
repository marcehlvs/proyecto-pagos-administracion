using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // CRUD de Feriado. El Admin lo carga una vez al año (los feriados "puente" recién se
    // decretan a fines del año anterior, así que no hay forma de calcularlos solos). BoletinService
    // los cruza con el rango FechaInicio/FechaFin de cada Periodo Cuatrimestral para descontarlos
    // de "Días hábiles" del RITE.
    [Authorize(Roles = "Admin")]
    public class FeriadosController : Controller
    {
        private readonly AdministracionDbContext _context;
        public FeriadosController(AdministracionDbContext context) => _context = context;

        public async Task<IActionResult> Index(int? anio)
        {
            var anioFiltro = anio ?? DateTime.Today.Year;
            ViewBag.Anio = anioFiltro;

            var feriados = await _context.Feriados
                .Where(f => f.Fecha.Year == anioFiltro)
                .OrderBy(f => f.Fecha)
                .ToListAsync();

            return View(feriados);
        }

        public IActionResult Create(int? anio)
        {
            var anioFiltro = anio ?? DateTime.Today.Year;
            return View(new Feriado { Fecha = new DateTime(anioFiltro, 1, 1) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Fecha,Descripcion")] Feriado feriado)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(feriado);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { anio = feriado.Fecha.Year });
                }
                catch (DbUpdateException)
                {
                    // Salta por el índice único IX_Feriados_Fecha (ver AdministracionDbContext).
                    ModelState.AddModelError(nameof(feriado.Fecha), "Ya hay un feriado cargado para esa fecha.");
                }
            }
            return View(feriado);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var feriado = await _context.Feriados.FindAsync(id);
            if (feriado == null) return NotFound();
            return View(feriado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Fecha,Descripcion")] Feriado feriado)
        {
            if (id != feriado.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(feriado);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { anio = feriado.Fecha.Year });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Feriados.Any(f => f.Id == id)) return NotFound();
                    throw;
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(nameof(feriado.Fecha), "Ya hay un feriado cargado para esa fecha.");
                }
            }
            return View(feriado);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var feriado = await _context.Feriados.FindAsync(id);
            if (feriado == null) return NotFound();
            return View(feriado);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var feriado = await _context.Feriados.FindAsync(id);
            if (feriado != null)
            {
                // Soft delete: mismo criterio que el resto del proyecto.
                feriado.Activo = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
