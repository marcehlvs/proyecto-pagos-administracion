using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AvisosController : Controller
    {
        private readonly AdministracionDbContext _context;
        public AvisosController(AdministracionDbContext context) => _context = context;

        public async Task<IActionResult> Index() =>
            View(await _context.Avisos.OrderByDescending(a => a.FechaPublicacion).ToListAsync());

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Titulo,Descripcion,Tipo")] Aviso aviso)
        {
            if (ModelState.IsValid)
            {
                _context.Add(aviso);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(aviso);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var aviso = await _context.Avisos.FindAsync(id);
            if (aviso == null) return NotFound();
            return View(aviso);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Titulo,Descripcion,Tipo,Activo")] Aviso aviso)
        {
            if (id != aviso.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(aviso);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(aviso);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var aviso = await _context.Avisos.FirstOrDefaultAsync(m => m.Id == id);
            if (aviso == null) return NotFound();
            return View(aviso);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var aviso = await _context.Avisos.FindAsync(id);
            if (aviso != null) _context.Avisos.Remove(aviso);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}