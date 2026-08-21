using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Agregado para ILogger
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize]
    public class PagosController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly MercadoPagoService _mpService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PagosController> _logger; // Inyección de ILogger
        private readonly IConfiguration _config;


        // Constructor actualizado
        public PagosController(
            AdministracionDbContext context,
            MercadoPagoService mpService,
            UserManager<ApplicationUser> userManager,
            ILogger<PagosController> logger, 
            IConfiguration config)
        {
            _context = context;
            _mpService = mpService;
            _userManager = userManager;
            _logger = logger;
            _config = config;
        }

        // GET: PAGOS
        public async Task<IActionResult> Index(string? buscarAlumno, EstadoPago? estado)
        {
            var query = _context.Pagos.Include(p => p.Cuota).ThenInclude(c => c.Alumno).AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscarAlumno))
                query = query.Where(p => p.Cuota.Alumno.Apellido.Contains(buscarAlumno)
                                       || p.Cuota.Alumno.Nombre.Contains(buscarAlumno)
                                       || p.Cuota.Alumno.Dni.Contains(buscarAlumno));

            if (estado.HasValue) query = query.Where(p => p.Estado == estado.Value);

            ViewBag.BuscarAlumno = buscarAlumno;
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
        public async Task<IActionResult> Confirmar(int cuotaId)
        {
            var userId = _userManager.GetUserId(User);

            var cuota = await _context.Cuotas.Include(c => c.Alumno)
                .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);

            if (cuota == null) return NotFound();

            ViewBag.Alias = _config["DatosBancarios:Alias"];
            ViewBag.Titular = _config["DatosBancarios:Titular"];
            ViewBag.Cbu = _config["DatosBancarios:Cbu"];

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

        // Mercado Pago Webhook Endpoint (Con Idempotencia y Logging)
        [AllowAnonymous]
        [HttpPost("api/mercadopago/webhook")]
        public async Task<IActionResult> Webhook()
        {
            try
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

                if (pago == null)
                {
                    _logger.LogWarning("Webhook de MP recibido para Pago inexistente. ExternalReference={Referencia}", payment.ExternalReference);
                    return Ok();
                }

                // Idempotencia: si ya estaba en estado final, evitamos reprocesar
                if (pago.Estado == EstadoPago.Aprobado || pago.Estado == EstadoPago.Rechazado)
                    return Ok();

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
                _logger.LogInformation("Pago {PagoId} actualizado a {Estado} vía webhook.", pago.Id, pago.Estado);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando webhook de Mercado Pago.");
                return Ok(); // Devolvemos 200 OK para evitar reintentos infinitos de MP ante fallos internos
            }
        }

        [AllowAnonymous]
        public IActionResult PagoExitoso() => View();

        [AllowAnonymous]
        public IActionResult PagoFallido() => View();

        [AllowAnonymous]
        public IActionResult PagoPendiente() => View();

        [Authorize(Roles = "Familia")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirComprobante(int cuotaId, IFormFile archivo)
        {
            var userId = _userManager.GetUserId(User);

            var cuota = await _context.Cuotas.Include(c => c.Alumno)
                .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);

            if (cuota == null) return NotFound();

            if (archivo == null || archivo.Length == 0)
            {
                TempData["ErrorComprobante"] = "Seleccioná un archivo.";
                return RedirectToAction("Details", "MisCuotas", new { id = cuotaId });
            }

            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!extensionesPermitidas.Contains(extension) || archivo.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorComprobante"] = "Archivo inválido (solo jpg/png/pdf, máx 5MB).";
                return RedirectToAction("Details", "MisCuotas", new { id = cuotaId });
            }

            var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "comprobantes");
            Directory.CreateDirectory(carpeta);
            var nombreArchivo = $"{Guid.NewGuid()}{extension}";
            using (var stream = new FileStream(Path.Combine(carpeta, nombreArchivo), FileMode.Create))
                await archivo.CopyToAsync(stream);

            // Reutilizamos un Pago pendiente existente para esta cuota, o creamos uno nuevo
            var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.CuotaId == cuotaId && p.Estado == EstadoPago.Pendiente);
            if (pago == null)
            {
                pago = new Pago { CuotaId = cuotaId, Monto = cuota.Monto, Fecha = DateTime.Now };
                _context.Pagos.Add(pago);
            }

            pago.ComprobanteRuta = nombreArchivo;
            pago.Estado = EstadoPago.EnRevision;
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "MisCuotas", new { id = cuotaId });
        }

        [Authorize]
        public async Task<IActionResult> VerComprobante(int pagoId)
        {
            var pago = await _context.Pagos.Include(p => p.Cuota).ThenInclude(c => c.Alumno)
                .FirstOrDefaultAsync(p => p.Id == pagoId);

            if (pago?.ComprobanteRuta == null) return NotFound();

            var esAdmin = User.IsInRole("Admin");
            var esDueño = pago.Cuota.Alumno.FamiliaUserId == _userManager.GetUserId(User);
            if (!esAdmin && !esDueño) return Forbid();

            var ruta = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "comprobantes", pago.ComprobanteRuta);
            if (!System.IO.File.Exists(ruta)) return NotFound();

            var contentType = Path.GetExtension(ruta) == ".pdf" ? "application/pdf" : "image/" + Path.GetExtension(ruta).TrimStart('.');
            return PhysicalFile(ruta, contentType);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarComprobante(int id)
        {
            var pago = await _context.Pagos.Include(p => p.Cuota).FirstOrDefaultAsync(p => p.Id == id);
            if (pago == null) return NotFound();

            pago.Estado = EstadoPago.Aprobado;
            pago.Cuota.Estado = EstadoCuota.Pagada;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarComprobante(int id)
        {
            var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.Id == id);
            if (pago == null) return NotFound();

            pago.Estado = EstadoPago.Rechazado;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}