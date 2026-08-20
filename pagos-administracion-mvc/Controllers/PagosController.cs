using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Agregado para UserManager
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers // Asegurate de que el namespace coincida con el tuyo
{
    [Authorize]
    public class PagosController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly MercadoPagoService _mpService;
        private readonly UserManager<ApplicationUser> _userManager; // Nuevo servicio inyectado

        // Constructor actualizado
        public PagosController(AdministracionDbContext context, MercadoPagoService mpService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _mpService = mpService;
            _userManager = userManager;
        }

        // GET: PAGOS
      
        public async Task<IActionResult> Index(int? alumnoId, EstadoPago? estado)
        {
            var query = _context.Pagos.Include(p => p.Cuota).ThenInclude(c => c.Alumno).AsQueryable();

            if (alumnoId.HasValue) query = query.Where(p => p.Cuota.AlumnoId == alumnoId.Value);
            if (estado.HasValue) query = query.Where(p => p.Estado == estado.Value);

            ViewBag.AlumnoId = new SelectList(
                _context.Alumnos.OrderBy(a => a.Apellido).Select(a => new { a.Id, NombreCompleto = a.Apellido + ", " + a.Nombre }),
                "Id", "NombreCompleto", alumnoId);
            ViewBag.EstadoSeleccionado = estado;

            return View(await query.OrderByDescending(p => p.Fecha).ToListAsync());
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

        private bool PagoExists(int? id)
        {
            return _context.Pagos.Any(e => e.Id == id);
        }

        // ==========================================
        // CONFIRMACIÓN DE PAGO (pantalla 5 simplificada: resumen + botón)
        // ==========================================
        [Authorize(Roles = "Familia")]
        public async Task<IActionResult> Confirmar(int cuotaId)
        {
            var userId = _userManager.GetUserId(User);

            var cuota = await _context.Cuotas
                .Include(c => c.Alumno)
                .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);

            if (cuota == null) return NotFound(); // no es su cuota, o no existe

            if (cuota.Estado == EstadoCuota.Pagada)
                return RedirectToAction("Index", "MisCuotas");

            return View(cuota);
        }

        // ==========================================
        // NUEVA ACCIÓN PAGAR (Segura y con validación)
        // ==========================================
        [Authorize(Roles = "Familia")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pagar(int cuotaId)
        {
            var userId = _userManager.GetUserId(User);

            var cuota = await _context.Cuotas
                .Include(c => c.Alumno)
                .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);

            if (cuota == null) return NotFound(); // no es su cuota, o no existe

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

        // Mercado Pago Webhook Endpoint
        [AllowAnonymous]
        [HttpPost("api/mercadopago/webhook")]
        public async Task<IActionResult> Webhook()
        {
            var type = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();
            var paymentId = Request.Query["data.id"].FirstOrDefault() ?? Request.Query["id"].FirstOrDefault();

            if (type != "payment" || string.IsNullOrEmpty(paymentId))
                return Ok();

            var paymentClient = new global::MercadoPago.Client.Payment.PaymentClient();
            var payment = await paymentClient.GetAsync(long.Parse(paymentId));

            if (payment?.ExternalReference == null)
                return Ok();

            var pagoId = int.Parse(payment.ExternalReference);
            var pago = await _context.Pagos.Include(p => p.Cuota).FirstOrDefaultAsync(p => p.Id == pagoId);
            if (pago == null) return Ok();

            pago.MercadoPagoPaymentId = payment.Id.ToString();
            pago.Estado = payment.Status switch
            {
                "approved" => EstadoPago.Aprobado,
                "rejected" => EstadoPago.Rechazado,
                _ => EstadoPago.Pendiente
            };

            if (pago.Estado == EstadoPago.Aprobado)
                pago.Cuota.Estado = EstadoCuota.Pagada;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [AllowAnonymous]
        public IActionResult PagoExitoso() => View();

        [AllowAnonymous]
        public IActionResult PagoFallido() => View();

        [AllowAnonymous]
        public IActionResult PagoPendiente() => View();
    }
}