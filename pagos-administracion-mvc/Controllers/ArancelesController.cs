using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ArancelesController : Controller
    {
        private readonly AdministracionDbContext _context;

        public ArancelesController(AdministracionDbContext context)
        {
            _context = context;
        }

        // GET: Aranceles
        public async Task<IActionResult> Index()
        {
            var aranceles = await _context.ArancelesNivel
                .OrderBy(a => a.Nivel)
                .ThenByDescending(a => a.VigenteDesde)
                .ToListAsync();
            return View(aranceles);
        }

        // GET: Aranceles/Create
        public IActionResult Create() => View(new ArancelNivel());

        // POST: Aranceles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nivel,VigenteDesde,Curricular,ExtraCurricular,EquipamientoDidactico,Mantenimiento,EmergenciaMedica,BonificacionPagoATiempo")] ArancelNivel arancel)
        {
            if (ModelState.IsValid)
            {
                arancel.FechaCreacion = DateTime.Now;
                arancel.CreadaPorNombre = User.Identity?.Name;
                _context.Add(arancel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(arancel);
        }

        // GET: Aranceles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var arancel = await _context.ArancelesNivel.FindAsync(id);
            if (arancel == null) return NotFound();

            return View(arancel);
        }

        // POST: Aranceles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nivel,VigenteDesde,Curricular,ExtraCurricular,EquipamientoDidactico,Mantenimiento,EmergenciaMedica,BonificacionPagoATiempo")] ArancelNivel arancelForm)
        {
            if (id != arancelForm.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Igual criterio que en CuotasController: cargamos la entidad trackeada y
                    // pisamos solo los campos del form, para no perder Activo/auditoría con un
                    // Update() sobre un objeto recién armado por el model binder.
                    var arancel = await _context.ArancelesNivel.FirstOrDefaultAsync(a => a.Id == id);
                    if (arancel == null) return NotFound();

                    arancel.Nivel = arancelForm.Nivel;
                    arancel.VigenteDesde = arancelForm.VigenteDesde;
                    arancel.Curricular = arancelForm.Curricular;
                    arancel.ExtraCurricular = arancelForm.ExtraCurricular;
                    arancel.EquipamientoDidactico = arancelForm.EquipamientoDidactico;
                    arancel.Mantenimiento = arancelForm.Mantenimiento;
                    arancel.EmergenciaMedica = arancelForm.EmergenciaMedica;
                    arancel.BonificacionPagoATiempo = arancelForm.BonificacionPagoATiempo;
                    arancel.ModificadaPorNombre = User.Identity?.Name;
                    arancel.FechaModificacion = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ArancelExists(arancelForm.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(arancelForm);
        }

        // GET: Aranceles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var arancel = await _context.ArancelesNivel.FirstOrDefaultAsync(m => m.Id == id);
            if (arancel == null) return NotFound();

            return View(arancel);
        }

        // POST: Aranceles/Delete/5
        // Soft delete, mismo criterio que Cuotas/Alumnos/Pagos.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var arancel = await _context.ArancelesNivel.FindAsync(id);
            if (arancel != null)
            {
                arancel.Activo = false;
                arancel.ModificadaPorNombre = User.Identity?.Name;
                arancel.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ArancelExists(int? id) => _context.ArancelesNivel.Any(e => e.Id == id);
    }
}
