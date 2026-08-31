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
    // Autorización por defecto: cualquier acción nueva que se agregue queda protegida
    // salvo que se marque explícitamente [AllowAnonymous] (Webhook, PagoExitoso, PagoFallido, PagoPendiente).
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
        [Authorize(Roles = "Admin")]
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

            // Paleta alineada al design system de la app (wwwroot/css/site.css)
            var colorPrimario = ClosedXML.Excel.XLColor.FromHtml("#1A365D");
            var colorPrimarioOscuro = ClosedXML.Excel.XLColor.FromHtml("#002045");
            var colorFilaAlterna = ClosedXML.Excel.XLColor.FromHtml("#F7FAFC");
            var colorBorde = ClosedXML.Excel.XLColor.FromHtml("#E2E8F0");
            var colorAprobado = ClosedXML.Excel.XLColor.FromHtml("#10B981");
            var colorPendiente = ClosedXML.Excel.XLColor.FromHtml("#F59E0B");
            var colorRechazado = ClosedXML.Excel.XLColor.FromHtml("#E53E3E");
            var colorTextoClaro = ClosedXML.Excel.XLColor.White;

            using var libro = new ClosedXML.Excel.XLWorkbook();
            var hoja = libro.Worksheets.Add("Pagos");
            hoja.ShowGridLines = false;
            hoja.TabColor = colorPrimario;

            string[] encabezados = { "Alumno", "DNI", "Nivel", "Grado-Año", "Turno", "Mes/Año Cuota", "Fecha Pago", "Monto", "Estado" };
            const int colInicio = 1;
            int colFin = encabezados.Length;

            // --- Encabezado del reporte: título + resumen de filtros aplicados ---
            hoja.Cell(1, colInicio).Value = "Reporte de Pagos";
            hoja.Range(1, colInicio, 1, colFin).Merge();
            hoja.Cell(1, colInicio).Style.Font.FontSize = 16;
            hoja.Cell(1, colInicio).Style.Font.Bold = true;
            hoja.Cell(1, colInicio).Style.Font.FontColor = colorPrimarioOscuro;

            var filtrosAplicados = new List<string>();
            if (!string.IsNullOrWhiteSpace(buscarAlumno)) filtrosAplicados.Add($"Alumno: \"{buscarAlumno}\"");
            if (estado.HasValue) filtrosAplicados.Add($"Estado: {estado}");
            if (nivel.HasValue) filtrosAplicados.Add($"Nivel: {nivel}");
            if (gradoAnio.HasValue) filtrosAplicados.Add($"Grado/Año: {gradoAnio}");
            if (turno.HasValue) filtrosAplicados.Add($"Turno: {turno}");

            hoja.Cell(2, colInicio).Value = $"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} · {pagos.Count} registro(s)" +
                (filtrosAplicados.Count > 0 ? $" · Filtros: {string.Join(", ", filtrosAplicados)}" : " · Sin filtros aplicados");
            hoja.Range(2, colInicio, 2, colFin).Merge();
            hoja.Cell(2, colInicio).Style.Font.FontSize = 9;
            hoja.Cell(2, colInicio).Style.Font.FontColor = ClosedXML.Excel.XLColor.FromHtml("#4A5568");
            hoja.Cell(2, colInicio).Style.Font.Italic = true;

            // --- Encabezados de columna ---
            const int filaEncabezados = 4;
            for (int i = 0; i < encabezados.Length; i++)
                hoja.Cell(filaEncabezados, i + 1).Value = encabezados[i];

            var rangoEncabezados = hoja.Range(filaEncabezados, colInicio, filaEncabezados, colFin);
            rangoEncabezados.Style.Font.Bold = true;
            rangoEncabezados.Style.Font.FontColor = colorTextoClaro;
            rangoEncabezados.Style.Fill.BackgroundColor = colorPrimario;
            rangoEncabezados.Style.Alignment.Vertical = ClosedXML.Excel.XLAlignmentVerticalValues.Center;
            hoja.Row(filaEncabezados).Height = 22;

            // --- Filas de datos ---
            int fila = filaEncabezados + 1;
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
                hoja.Cell(fila, 8).Style.NumberFormat.Format = "$ #,##0.00";
                hoja.Cell(fila, 9).Value = p.Estado.ToString();

                // Zebra striping para lectura rápida en filas largas
                if ((fila - filaEncabezados) % 2 == 0)
                    hoja.Range(fila, colInicio, fila, colFin).Style.Fill.BackgroundColor = colorFilaAlterna;

                // Semáforo de color en la columna Estado, igual que las badges de la UI
                var celdaEstado = hoja.Cell(fila, 9);
                celdaEstado.Style.Font.Bold = true;
                celdaEstado.Style.Font.FontColor = p.Estado switch
                {
                    EstadoPago.Aprobado => colorAprobado,
                    EstadoPago.Rechazado => colorRechazado,
                    _ => colorPendiente // Pendiente, Cancelado, EnRevision
                };

                fila++;
            }

            int filaTotales = fila;
            int totalRegistros = pagos.Count;

            // --- Fila de totales ---
            if (totalRegistros > 0)
            {
                hoja.Cell(filaTotales, 7).Value = "Total:";
                hoja.Cell(filaTotales, 7).Style.Font.Bold = true;
                hoja.Cell(filaTotales, 7).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Right;
                hoja.Cell(filaTotales, 8).FormulaA1 = $"=SUM(H{filaEncabezados + 1}:H{filaTotales - 1})";
                hoja.Cell(filaTotales, 8).Style.NumberFormat.Format = "$ #,##0.00";
                hoja.Cell(filaTotales, 8).Style.Font.Bold = true;
                var rangoTotales = hoja.Range(filaTotales, colInicio, filaTotales, colFin);
                rangoTotales.Style.Border.TopBorder = ClosedXML.Excel.XLBorderStyleValues.Medium;
                rangoTotales.Style.Border.TopBorderColor = colorPrimario;
            }

            // --- Bordes finos en toda la tabla (encabezados + datos) ---
            var rangoTabla = hoja.Range(filaEncabezados, colInicio, Math.Max(filaTotales, filaEncabezados), colFin);
            rangoTabla.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            rangoTabla.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            rangoTabla.Style.Border.OutsideBorderColor = colorBorde;
            rangoTabla.Style.Border.InsideBorderColor = colorBorde;

            // --- Filtro automático + panel congelado (encabezado siempre visible al scrollear) ---
            if (totalRegistros > 0)
                hoja.Range(filaEncabezados, colInicio, filaTotales - 1, colFin).SetAutoFilter();
            hoja.SheetView.FreezeRows(filaEncabezados);

            hoja.Columns().AdjustToContents();
            hoja.Column(1).Width = Math.Max(hoja.Column(1).Width, 24); // nombre de alumno no se corta

            // --- Configuración de impresión: horizontal, encabezado repetido, ajustado al ancho ---
            hoja.PageSetup.PageOrientation = ClosedXML.Excel.XLPageOrientation.Landscape;
            hoja.PageSetup.FitToPages(1, 0);
            hoja.PageSetup.SetRowsToRepeatAtTop(filaEncabezados, filaEncabezados);

            libro.Properties.Title = "Reporte de Pagos";
            libro.Properties.Author = "Sistema de Administración de Pagos";

            using var stream = new MemoryStream();
            libro.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"pagos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }
        [Authorize(Roles = "Admin")]
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

            if (pago == null)
            {
                return NotFound();
            }

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
        public async Task<IActionResult> Edit(int? id, [Bind("Id,CuotaId,Monto,Fecha,Estado,MercadoPagoPaymentId,MercadoPagoPreferenceId")] Pago pagoForm)
        {
            if (id != pagoForm.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Igual que en CuotasController: cargamos la entidad trackeada en vez de
                    // "_context.Update" sobre un objeto recién armado por el binder, para no pisar
                    // con null lo que no está en el [Bind] (ComprobanteRuta, Activo, etc.).
                    var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.Id == id);
                    if (pago == null) return NotFound();

                    pago.CuotaId = pagoForm.CuotaId;
                    pago.Monto = pagoForm.Monto;
                    pago.Fecha = pagoForm.Fecha;
                    pago.Estado = pagoForm.Estado;
                    pago.MercadoPagoPaymentId = pagoForm.MercadoPagoPaymentId;
                    pago.MercadoPagoPreferenceId = pagoForm.MercadoPagoPreferenceId;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PagoExists(pagoForm.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.CuotaId = new SelectList(
                _context.Cuotas.Include(c => c.Alumno)
                    .OrderBy(c => c.Alumno.Apellido).ThenBy(c => c.Anio).ThenBy(c => c.Mes)
                    .Select(c => new { c.Id, Detalle = c.Alumno.Apellido + " " + c.Alumno.Nombre + " - " + c.Mes + "/" + c.Anio }),
                "Id", "Detalle", pagoForm.CuotaId);
            return View(pagoForm);
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
        [Authorize(Roles = "Familia")]
        public async Task<IActionResult> Confirmar(int cuotaId)
        {
            var userId = _userManager.GetUserId(User);

            var cuota = await _context.Cuotas.Include(c => c.Alumno).Include(c => c.Pagos)
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
                .Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.Id == cuotaId && c.Alumno.FamiliaUserId == userId);

            if (cuota == null) return NotFound(); // no es su cuota, o no existe

            var pago = new Pago
            {
                CuotaId = cuota.Id,
                Monto = cuota.SaldoPendiente,
                Fecha = DateTime.Now,
                Estado = EstadoPago.Pendiente,
                RegistradoPorUserId = userId,
                RegistradoPorNombre = User.Identity?.Name,
                FechaRegistro = DateTime.Now
            };
            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            var preferencia = await _mpService.CrearPreferenciaAsync(pago, cuota);

            pago.MercadoPagoPreferenceId = preferencia.Id;
            await _context.SaveChangesAsync();

            return Redirect(preferencia.InitPoint);
        }

        // Mercado Pago Webhook Endpoint (Con Idempotencia, Logging y validación de firma)
        [AllowAnonymous]
        [HttpPost("api/mercadopago/webhook")]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                var paymentId = Request.Query["data.id"].FirstOrDefault() ?? Request.Query["id"].FirstOrDefault();

                // Validamos x-signature ANTES de tocar la base o llamar a la API de MP.
                // Sin esto, cualquiera que conozca la URL del webhook puede pegarle con un
                // data.id de un pago real (propio, ajeno, o de otro comercio) y forzar que
                // reprocesemos ese pago como si MP lo hubiera notificado.
                var webhookSecret = _config["MercadoPago:WebhookSecret"];
                if (!string.IsNullOrEmpty(webhookSecret))
                {
                    if (!FirmaWebhookValida(webhookSecret, paymentId))
                    {
                        _logger.LogWarning("Webhook de MP rechazado: firma x-signature inválida o ausente. data.id={PaymentId}", paymentId);
                        return Unauthorized();
                    }
                }
                else
                {
                    // Configurá MercadoPago:WebhookSecret (user-secrets en dev, variable de entorno
                    // MercadoPago__WebhookSecret en producción) para que esta validación se active.
                    // Hasta entonces seguimos procesando sin validar, igual que antes.
                    _logger.LogWarning("MercadoPago:WebhookSecret no configurado: la firma del webhook no se está validando.");
                }

                var type = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();

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
                pago.ActualizadoPorNombre = "Mercado Pago (automático)";
                pago.FechaActualizacion = DateTime.Now;

                if (pago.Estado == EstadoPago.Aprobado)
                {
                    pago.Cuota.Estado = EstadoCuota.Pagada;

                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins.Where(a => a.Email != null))
                    {
                        var cuerpoPago = EmailService.EnvolverPlantilla(
                            "Pago recibido",
                            $@"<p style=""margin:0 0 10px 0;"">Se registró un pago de <strong>{pago.Monto:C}</strong> para la cuota {pago.Cuota.Mes}/{pago.Cuota.Anio}.</p>
                            <p style=""margin:0;"">Alumno: <strong>{pago.Cuota.Alumno.Apellido}, {pago.Cuota.Alumno.Nombre}</strong></p>");

                        await _emailService.EnviarAsync(
                        admin.Email!,
                        $"Pago recibido - {pago.Cuota.Alumno.Apellido}, {pago.Cuota.Alumno.Nombre}",
                        cuerpoPago
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

        // Valida el header x-signature que Mercado Pago manda en cada notificación de webhook.
        // Formato: "ts=1704908010,v1=<hmac-sha256 hex>". El hash se calcula sobre un "manifest"
        // armado con data.id + x-request-id + ts, usando el secreto que figura en el panel de MP
        // (Tu negocio > Configuración > Webhooks > Firma secreta). Documentación:
        // https://www.mercadopago.com.ar/developers/es/docs/checkout-api/additional-content/notifications/webhooks
        private bool FirmaWebhookValida(string webhookSecret, string? paymentId)
        {
            var xSignature = Request.Headers["x-signature"].FirstOrDefault();
            var xRequestId = Request.Headers["x-request-id"].FirstOrDefault();

            if (string.IsNullOrEmpty(xSignature) || string.IsNullOrEmpty(paymentId))
                return false;

            string? ts = null, v1 = null;
            foreach (var parte in xSignature.Split(','))
            {
                var kv = parte.Split('=', 2);
                if (kv.Length != 2) continue;

                var clave = kv[0].Trim();
                var valor = kv[1].Trim();
                if (clave == "ts") ts = valor;
                else if (clave == "v1") v1 = valor;
            }

            if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
                return false;

            // MP pide el data.id en minúsculas dentro del manifest cuando es alfanumérico.
            var manifest = $"id:{paymentId.ToLowerInvariant()};request-id:{xRequestId};ts:{ts};";

            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(webhookSecret));
            var hashCalculado = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

            // Comparación en tiempo constante para no filtrar el hash por timing attack.
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(hashCalculado),
                System.Text.Encoding.UTF8.GetBytes(v1));
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

            var cuota = await _context.Cuotas.Include(c => c.Alumno).Include(c => c.Pagos)
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
                pago = new Pago
                {
                    CuotaId = cuotaId,
                    Monto = cuota.SaldoPendiente,
                    Fecha = DateTime.Now,
                    RegistradoPorUserId = userId,
                    RegistradoPorNombre = User.Identity?.Name,
                    FechaRegistro = DateTime.Now
                };
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
            pago.ActualizadoPorNombre = User.Identity?.Name;
            pago.FechaActualizacion = DateTime.Now;
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
            pago.ActualizadoPorNombre = User.Identity?.Name;
            pago.FechaActualizacion = DateTime.Now;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegistrarManual(int cuotaId)
        {
            var cuota = await _context.Cuotas.Include(c => c.Alumno).Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.Id == cuotaId);
            if (cuota == null) return NotFound();

            ViewBag.TotalPagado = cuota.TotalPagado;
            ViewBag.Saldo = cuota.SaldoPendiente;

            return View(cuota);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarManual(int cuotaId, decimal monto, DateTime fecha, IFormFile? comprobante)
        {
            var cuota = await _context.Cuotas.Include(c => c.Alumno).Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.Id == cuotaId);
            if (cuota == null) return NotFound();

            if (monto <= 0)
            {
                ModelState.AddModelError(string.Empty, "El monto debe ser mayor a 0.");
                ViewBag.TotalPagado = cuota.TotalPagado;
                ViewBag.Saldo = cuota.SaldoPendiente;
                return View(cuota);
            }

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
                else
                {
                    // Antes se descartaba el archivo en silencio y el pago se guardaba sin comprobante.
                    // Ahora avisamos al admin para que sepa que el archivo no se adjuntó.
                    TempData["ErrorComprobante"] = "El pago se registró, pero el archivo adjunto no era válido (solo jpg/png/pdf, máx 5MB) y no se guardó.";
                }
            }

            // Calculamos el estado ANTES de agregar el pago a la colección en memoria
            // (cuota.TotalPagado ya refleja los pagos aprobados existentes).
            var totalPagadoAcumulado = cuota.TotalPagado + monto;

            var pago = new Pago
            {
                CuotaId = cuota.Id,
                Monto = monto,
                Fecha = fecha,
                Estado = EstadoPago.Aprobado,
                ComprobanteRuta = nombreArchivo,
                RegistradoPorUserId = _userManager.GetUserId(User),
                RegistradoPorNombre = User.Identity?.Name,
                FechaRegistro = DateTime.Now,
                ActualizadoPorNombre = User.Identity?.Name,
                FechaActualizacion = DateTime.Now
            };
            _context.Pagos.Add(pago);

            // El monto cargado puede no cubrir el total de la cuota (pago parcial / en cuotas),
            // así que el estado se decide comparando lo acumulado contra lo que falta pagar,
            // no asumiendo que cualquier pago manual salda la cuota entera.
            cuota.Estado = totalPagadoAcumulado >= cuota.MontoAPagar ? EstadoCuota.Pagada : EstadoCuota.Parcial;

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Cuotas", new { id = cuotaId });
        }
    }
}