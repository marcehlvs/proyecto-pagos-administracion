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
        private readonly EmailService _emailService;


        // Constructor actualizado
        public PagosController(
            AdministracionDbContext context,
            MercadoPagoService mpService,
            UserManager<ApplicationUser> userManager,
            ILogger<PagosController> logger, 
            IConfiguration config,
            EmailService emailService)
            

        {
            _context = context;
            _mpService = mpService;
            _userManager = userManager;
            _logger = logger;
            _config = config;
            _emailService = emailService;
        }

        // GET: PAGOS
        public async Task<IActionResult> Index(string? buscarAlumno, EstadoPago? estado, NivelEducativo? nivel, int? gradoAnio, Turno? turno)
        {
            var query = ConstruirConsultaFiltrada(buscarAlumno, estado, nivel, gradoAnio, turno);

            ViewBag.BuscarAlumno = buscarAlumno;
            ViewBag.EstadoSeleccionado = estado;
            ViewBag.NivelSeleccionado = nivel;
            ViewBag.GradoSeleccionado = gradoAnio;
            ViewBag.TurnoSeleccionado = turno;

            return View(await query.ToListAsync());
        }

        // Arma la misma consulta filtrada que Index, para reutilizarla en las exportaciones.
        private IQueryable<Pago> ConstruirConsultaFiltrada(string? buscarAlumno, EstadoPago? estado, NivelEducativo? nivel, int? gradoAnio, Turno? turno)
        {
            var query = _context.Pagos.Include(p => p.Cuota).ThenInclude(c => c.Alumno).AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscarAlumno))
                query = query.Where(p => p.Cuota.Alumno.Apellido.Contains(buscarAlumno)
                                       || p.Cuota.Alumno.Nombre.Contains(buscarAlumno)
                                       || p.Cuota.Alumno.Dni.Contains(buscarAlumno));

            if (estado.HasValue) query = query.Where(p => p.Estado == estado.Value);
            if (nivel.HasValue) query = query.Where(p => p.Cuota.Alumno.Nivel == nivel.Value);
            if (gradoAnio.HasValue) query = query.Where(p => p.Cuota.Alumno.GradoAnio == gradoAnio.Value);
            if (turno.HasValue) query = query.Where(p => p.Cuota.Alumno.Turno == turno.Value);

            return query.OrderByDescending(p => p.Fecha);
        }

        // GET: PAGOS/ExportarCsv — respeta los mismos filtros que la grilla de Index
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportarCsv(string? buscarAlumno, EstadoPago? estado, NivelEducativo? nivel, int? gradoAnio, Turno? turno)
        {
            var pagos = await ConstruirConsultaFiltrada(buscarAlumno, estado, nivel, gradoAnio, turno).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Alumno;DNI;Nivel;Grado-Año;Turno;Mes/Año Cuota;Fecha Pago;Monto;Estado");

            static string Csv(string valor) => "\"" + (valor ?? string.Empty).Replace("\"", "\"\"") + "\"";

            foreach (var p in pagos)
            {
                var a = p.Cuota.Alumno;
                sb.AppendLine(string.Join(";",
                    Csv($"{a.Apellido}, {a.Nombre}"),
                    Csv(a.Dni),
                    Csv(a.Nivel.ToString()),
                    Csv(a.GradoAnio.ToString()),
                    Csv(a.Turno.ToString()),
                    Csv($"{p.Cuota.Mes}/{p.Cuota.Anio}"),
                    Csv(p.Fecha.ToString("dd/MM/yyyy")),
                    Csv(p.Monto.ToString("0.00")),
                    Csv(p.Estado.ToString())));
            }

            // BOM UTF-8 para que Excel abra bien los acentos
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"pagos_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // GET: PAGOS/ExportarExcel — respeta los mismos filtros que la grilla de Index
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportarExcel(string? buscarAlumno, EstadoPago? estado, NivelEducativo? nivel, int? gradoAnio, Turno? turno)
        {
            var pagos = await ConstruirConsultaFiltrada(buscarAlumno, estado, nivel, gradoAnio, turno).ToListAsync();

            using var libro = new ClosedXML.Excel.XLWorkbook();
            var hoja = libro.Worksheets.Add("Pagos");

            string[] encabezados = { "Alumno", "DNI", "Nivel", "Grado-Año", "Turno", "Mes/Año Cuota", "Fecha Pago", "Monto", "Estado" };
            for (int i = 0; i < encabezados.Length; i++)
                hoja.Cell(1, i + 1).Value = encabezados[i];
            hoja.Row(1).Style.Font.Bold = true;

            int fila = 2;
            foreach (var p in pagos)
            {
                var a = p.Cuota.Alumno;
                hoja.Cell(fila, 1).Value = $"{a.Apellido}, {a.Nombre}";
                hoja.Cell(fila, 2).Value = a.Dni;
                hoja.Cell(fila, 3).Value = a.Nivel.ToString();
                hoja.Cell(fila, 4).Value = a.GradoAnio;
                hoja.Cell(fila, 5).Value = a.Turno.ToString();
                hoja.Cell(fila, 6).Value = $"{p.Cuota.Mes}/{p.Cuota.Anio}";
                hoja.Cell(fila, 7).Value = p.Fecha;
                hoja.Cell(fila, 7).Style.DateFormat.Format = "dd/MM/yyyy";
                hoja.Cell(fila, 8).Value = p.Monto;
                hoja.Cell(fila, 8).Style.NumberFormat.Format = "#,##0.00";
                hoja.Cell(fila, 9).Value = p.Estado.ToString();
                fila++;
            }

            hoja.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            libro.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"pagos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // GET: PAGOS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pago = await _context.Pagos
    .Include(p => p.Cuota).ThenInclude(c => c.Alumno)
    .FirstOrDefaultAsync(m => m.Id == id);
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
        // Soft delete: nunca se borra el registro físicamente (auditoría). Se marca Activo = false
        // y el HasQueryFilter en el DbContext lo excluye automáticamente del resto de las consultas.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var pago = await _context.Pagos.FindAsync(id);
            if (pago != null)
            {
                pago.Activo = false;
                await _context.SaveChangesAsync();
            }

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
                Monto = cuota.MontoAPagar,
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
                var pago = await _context.Pagos.Include(p => p.Cuota).ThenInclude(c => c.Alumno).FirstOrDefaultAsync(p => p.Id == pagoId);

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
                {
                    pago.Cuota.Estado = EstadoCuota.Pagada;

                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins.Where(a => a.Email != null))
                    {
                        await _emailService.EnviarAsync(
                        admin.Email!,
                        $"Pago recibido - {pago.Cuota.Alumno.Apellido}, {pago.Cuota.Alumno.Nombre}",
                        $"<p>Se registró un pago de {pago.Monto:C} para la cuota {pago.Cuota.Mes}/{pago.Cuota.Anio} " +
                        $"de {pago.Cuota.Alumno.Apellido}, {pago.Cuota.Alumno.Nombre}.</p>"
                        );
                    }
                }
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
                pago = new Pago { CuotaId = cuotaId, Monto = cuota.MontoAPagar, Fecha = DateTime.Now };
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


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegistrarManual(int cuotaId)
        {
            var cuota = await _context.Cuotas.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == cuotaId);
            if (cuota == null) return NotFound();
            return View(cuota);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarManual(int cuotaId, decimal monto, DateTime fecha, IFormFile? comprobante)
        {
            var cuota = await _context.Cuotas.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == cuotaId);
            if (cuota == null) return NotFound();

            string? nombreArchivo = null;
            if (comprobante != null && comprobante.Length > 0)
            {
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var extension = Path.GetExtension(comprobante.FileName).ToLowerInvariant();
                if (extensionesPermitidas.Contains(extension) && comprobante.Length <= 5 * 1024 * 1024)
                {
                    var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "comprobantes");
                    Directory.CreateDirectory(carpeta);
                    nombreArchivo = $"{Guid.NewGuid()}{extension}";
                    using var stream = new FileStream(Path.Combine(carpeta, nombreArchivo), FileMode.Create);
                    await comprobante.CopyToAsync(stream);
                }
            }

            var pago = new Pago
            {
                CuotaId = cuota.Id,
                Monto = monto,
                Fecha = fecha,
                Estado = EstadoPago.Aprobado,
                ComprobanteRuta = nombreArchivo
            };
            _context.Pagos.Add(pago);
            cuota.Estado = EstadoCuota.Pagada;

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Cuotas", new { id = cuotaId });
        }
    }
}