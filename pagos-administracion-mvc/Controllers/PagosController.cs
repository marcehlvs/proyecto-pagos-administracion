
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using static pagos_administracion_mvc.Models.Enums;
[Authorize]
public class PagosController : Controller
{
    private readonly AdministracionDbContext _context; 
    private readonly MercadoPagoService _mpService;

    public PagosController(AdministracionDbContext context, MercadoPagoService mpService)
    {
        _context = context;
        _mpService = mpService;
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
    public IActionResult Create()
    {
        ViewBag.CuotaId = new SelectList(
            _context.Cuotas.Include(c => c.Alumno)
                .OrderBy(c => c.Alumno.Apellido).ThenBy(c => c.Anio).ThenBy(c => c.Mes)
                .Select(c => new { c.Id, Detalle = c.Alumno.Apellido + " " + c.Alumno.Nombre + " - " + c.Mes + "/" + c.Anio }),
            "Id", "Detalle");
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,CuotaId,Monto,Fecha,Estado,MercadoPagoPaymentId,MercadoPagoPreferenceId")] Pago pago)
    {
        if (ModelState.IsValid)
        {
            _context.Add(pago);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.CuotaId = new SelectList(
            _context.Cuotas.Include(c => c.Alumno)
                .OrderBy(c => c.Alumno.Apellido).ThenBy(c => c.Anio).ThenBy(c => c.Mes)
                .Select(c => new { c.Id, Detalle = c.Alumno.Apellido + " " + c.Alumno.Nombre + " - " + c.Mes + "/" + c.Anio }),
            "Id", "Detalle", pago.CuotaId);
        return View(pago);
    }
    [Authorize(Roles = "Admin")]
    // GET: PAGOS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var pago = await _context.Pagos.FindAsync(id);
        if (pago == null) return NotFound();

        ViewBag.CuotaId = new SelectList(
            _context.Cuotas.Include(c => c.Alumno)
                .OrderBy(c => c.Alumno.Apellido).ThenBy(c => c.Anio).ThenBy(c => c.Mes)
                .Select(c => new { c.Id, Detalle = c.Alumno.Apellido + " " + c.Alumno.Nombre + " - " + c.Mes + "/" + c.Anio }),
            "Id", "Detalle", pago.CuotaId);
        return View(pago);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,CuotaId,Monto,Fecha,Estado,MercadoPagoPaymentId,MercadoPagoPreferenceId")] Pago pago)
    {
        if (id != pago.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(pago);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PagoExists(pago.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewBag.CuotaId = new SelectList(
            _context.Cuotas.Include(c => c.Alumno)
                .OrderBy(c => c.Alumno.Apellido).ThenBy(c => c.Anio).ThenBy(c => c.Mes)
                .Select(c => new { c.Id, Detalle = c.Alumno.Apellido + " " + c.Alumno.Nombre + " - " + c.Mes + "/" + c.Anio }),
            "Id", "Detalle", pago.CuotaId);
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
    //MercadoPago Webhook Endpoint
    private bool PagoExists(int? id)
    {
        return _context.Pagos.Any(e => e.Id == id);
    }

    [Authorize]
    public async Task<IActionResult> Pagar(int cuotaId)
    {
        var cuota = await _context.Cuotas.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == cuotaId);
        if (cuota == null) return NotFound();

        var pago = new Pago
        {
            CuotaId = cuota.Id,
            Monto = cuota.Monto,
            Fecha = DateTime.Now,
            Estado = EstadoPago.Pendiente
        };
        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync();

        var preferencia = await _mpService.CrearPreferenciaAsync(pago, cuota);

        pago.MercadoPagoPreferenceId = preferencia.Id;
        await _context.SaveChangesAsync();

        return Redirect(preferencia.InitPoint);
    }
}
