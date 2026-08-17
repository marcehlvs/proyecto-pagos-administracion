
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Data;
using Microsoft.AspNetCore.Authorization;
[Authorize]
public class PagosController : Controller
{
    private readonly AdministracionDbContext _context;

    public PagosController(AdministracionDbContext context)
    {
        _context = context;
    }

    // GET: PAGOS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Pagos.ToListAsync());
    }

    // GET: PAGOS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var pago = await _context.Pagos
            .FirstOrDefaultAsync(m => m.Id == id);
        if (pago == null)
        {
            return NotFound();
        }

        return View(pago);
    }
    [Authorize(Roles = "Admin")]
    // GET: PAGOS/Create
    public IActionResult Create()
    {
        return View();
    }
    [Authorize(Roles ="Admin")]
    // POST: PAGOS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,CuotaId,Cuota,Monto,Fecha,Estado,MercadoPagoPaymentId,MercadoPagoPreferenceId")] Pago pago)
    {
        if (ModelState.IsValid)
        {
            _context.Add(pago);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(pago);
    }
    [Authorize(Roles = "Admin")]
    // GET: PAGOS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var pago = await _context.Pagos.FindAsync(id);
        if (pago == null)
        {
            return NotFound();
        }
        return View(pago);
    }
    [Authorize(Roles = "Admin")]
    // POST: PAGOS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,CuotaId,Cuota,Monto,Fecha,Estado,MercadoPagoPaymentId,MercadoPagoPreferenceId")] Pago pago)
    {
        if (id != pago.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(pago);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PagoExists(pago.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(pago);
    }
    [Authorize(Roles = "Admin")]
    // GET: PAGOS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var pago = await _context.Pagos
            .FirstOrDefaultAsync(m => m.Id == id);
        if (pago == null)
        {
            return NotFound();
        }

        return View(pago);
    }
    [Authorize(Roles = "Admin")]
    // POST: PAGOS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var pago = await _context.Pagos.FindAsync(id);
        if (pago != null)
        {
            _context.Pagos.Remove(pago);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool PagoExists(int? id)
    {
        return _context.Pagos.Any(e => e.Id == id);
    }
}
