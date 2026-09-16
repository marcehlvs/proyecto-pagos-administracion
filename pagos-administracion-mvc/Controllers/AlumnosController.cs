using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using static pagos_administracion_mvc.Models.Enums;
[Authorize]
public class AlumnosController : Controller
{
    private readonly AdministracionDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailService _emailService;

    public AlumnosController(AdministracionDbContext context, UserManager<ApplicationUser> userManager, EmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
    }

    private async Task<SelectList> ObtenerFamiliasSelectListAsync(string? seleccionado = null)
    {
        var familias = await _userManager.GetUsersInRoleAsync("Familia");
        return new SelectList(familias.OrderBy(f => f.Email), "Id", "Email", seleccionado);
    }

    [Authorize(Roles = "Admin")]
    // GET: ALUMNOS
    public async Task<IActionResult> Index(NivelEducativo? nivel, int? gradoAnio, Turno? turno)
    {
        var query = _context.Alumnos.Include(a => a.Cuotas).AsQueryable();

        if (nivel.HasValue) query = query.Where(a => a.Nivel == nivel.Value);
        if (gradoAnio.HasValue) query = query.Where(a => a.GradoAnio == gradoAnio.Value);
        if (turno.HasValue) query = query.Where(a => a.Turno == turno.Value);

        ViewBag.NivelSeleccionado = nivel;
        ViewBag.GradoSeleccionado = gradoAnio;
        ViewBag.TurnoSeleccionado = turno;

        return View(await query.OrderBy(a => a.Apellido).ToListAsync());
    }
    [Authorize(Roles = "Admin")]
    // GET: ALUMNOS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var alumno = await _context.Alumnos
            .Include(a => a.FamiliaUser)
            .Include(a => a.AlumnoUser)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (alumno == null)
        {
            return NotFound();
        }

        return View(alumno);
    }
    // GET: ALUMNOS/Create
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        // Cargar la lista para el dropdown en el GET
        ViewBag.FamiliaUserId = await ObtenerFamiliasSelectListAsync();
        return View();
    }

    // POST: ALUMNOS/Create
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    // IMPORTANTE: Asegúrate de agregar FamiliaUserId al [Bind]
    public async Task<IActionResult> Create([Bind("Id,Nombre,Apellido,Dni,Nivel,GradoAnio,Turno,EsRecursante,FamiliaUserId")] Alumno alumno)
    {
        if (ModelState.IsValid)
        {
            _context.Add(alumno);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Si el ModelState es inválido, recargar la lista manteniendo la selección
        ViewBag.FamiliaUserId = await ObtenerFamiliasSelectListAsync(alumno.FamiliaUserId);
        return View(alumno);
    }

    // GET: ALUMNOS/Edit/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var alumno = await _context.Alumnos.FindAsync(id);
        if (alumno == null)
        {
            return NotFound();
        }

        // Cargar la lista pasando el usuario actualmente asignado
        ViewBag.FamiliaUserId = await ObtenerFamiliasSelectListAsync(alumno.FamiliaUserId);
        return View(alumno);
    }

    // POST: ALUMNOS/Edit/5
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Apellido,Dni,Nivel,GradoAnio,Turno,EsRecursante,FamiliaUserId")] Alumno alumnoViewModel)
    {
        if (id != alumnoViewModel.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Cargamos la entidad trackeada por EF para solo pisar los campos del formulario.
            // Si usáramos _context.Update(alumnoViewModel), los campos que no están en el
            // [Bind] (Activo, AlumnoUserId, etc.) quedarían en null en la BD.
            var alumno = await _context.Alumnos.FindAsync(id);
            if (alumno == null) return NotFound();

            alumno.Nombre        = alumnoViewModel.Nombre;
            alumno.Apellido      = alumnoViewModel.Apellido;
            alumno.Dni           = alumnoViewModel.Dni;
            alumno.Nivel         = alumnoViewModel.Nivel;
            alumno.GradoAnio     = alumnoViewModel.GradoAnio;
            alumno.Turno         = alumnoViewModel.Turno;
            alumno.EsRecursante  = alumnoViewModel.EsRecursante;
            alumno.FamiliaUserId = alumnoViewModel.FamiliaUserId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AlumnoExists(alumno.Id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // Si el ModelState es inválido, recargar la lista manteniendo la selección
        ViewBag.FamiliaUserId = await ObtenerFamiliasSelectListAsync(alumnoViewModel.FamiliaUserId);
        return View(alumnoViewModel);
    }

    [Authorize(Roles = "Admin")]
    // GET: ALUMNOS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var alumno = await _context.Alumnos
            .Include(a => a.Cuotas).ThenInclude(c => c.Pagos)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (alumno == null)
        {
            return NotFound();
        }

        // Cuotas con saldo pendiente (Pendiente/Parcial/Vencida): se muestra como advertencia
        // antes de confirmar, no bloquea la baja (el historial de todas formas queda intacto).
        var cuotasPendientes = alumno.Cuotas
            .Where(c => c.Estado != EstadoCuota.Pagada)
            .ToList();
        ViewBag.CuotasPendientes = cuotasPendientes.Count;
        ViewBag.SaldoPendiente = cuotasPendientes.Sum(c => c.SaldoPendiente);

        return View(alumno);
    }

    [Authorize(Roles = "Admin")]
    // POST: ALUMNOS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var alumno = await _context.Alumnos.FindAsync(id);
        if (alumno != null)
        {
            // Soft delete: un alumno con cuotas ya cargadas no se puede borrar físicamente
            // (Cuota -> Alumno es Restrict a propósito, para no perder historial de pagos).
            alumno.Activo = false;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: ALUMNOS/CrearAcceso/5
    // Alta independiente del login del Alumno: no requiere que tenga FamiliaUserId cargado.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CrearAcceso(int? id)
    {
        if (id == null) return NotFound();

        var alumno = await _context.Alumnos.FindAsync(id);
        if (alumno == null) return NotFound();
        if (alumno.AlumnoUserId != null)
        {
            TempData["EmailError"] = "Este alumno ya tiene un acceso creado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(alumno);
    }

    // POST: ALUMNOS/CrearAcceso/5
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearAcceso(int id, string email)
    {
        var alumno = await _context.Alumnos.FindAsync(id);
        if (alumno == null) return NotFound();
        if (alumno.AlumnoUserId != null)
        {
            TempData["EmailError"] = "Este alumno ya tiene un acceso creado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var passwordTemporal = GenerarPasswordTemporal();

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true // el admin lo da de alta directo, no requiere confirmar por mail
        };

        var resultado = await _userManager.CreateAsync(usuario, passwordTemporal);

        if (resultado.Succeeded)
        {
            await _userManager.AddToRoleAsync(usuario, "Alumno");
            alumno.AlumnoUserId = usuario.Id;
            await _context.SaveChangesAsync();

            var cuerpoBienvenida = EmailService.EnvolverPlantilla(
                "¡Bienvenido/a al portal de la escuela!",
                $@"<p style=""margin:0 0 16px 0;"">Se creó tu cuenta de acceso al portal de la escuela para ver tu asistencia.</p>
                <p style=""margin:0 0 6px 0;""><strong>Usuario:</strong> {usuario.Email}</p>
                <p style=""margin:0 0 16px 0;""><strong>Contraseña provisoria:</strong> {passwordTemporal}</p>
                <p style=""margin:0; color:#4A5568; font-size:14px;"">Te recomendamos cambiarla después de tu primer ingreso, desde 'Mi perfil'.</p>");

            var (exito, error) = await _emailService.EnviarAsync(usuario.Email!, "Acceso al Portal de la Escuela José de San Martín", cuerpoBienvenida);

            if (!exito)
            {
                // El alumno y su acceso ya quedaron creados igual; solo avisamos que el mail
                // de bienvenida no salió, mismo criterio que FamiliasController.
                TempData["EmailError"] = $"El acceso se creó bien, pero el mail de bienvenida no se pudo enviar: {error}";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var error in resultado.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(alumno);
    }

    // Delegamos a SecurityHelpers para no duplicar la implementación que también usa FamiliasController.
    private static string GenerarPasswordTemporal() => SecurityHelpers.GenerarPasswordTemporal();

    private bool AlumnoExists(int? id)
    {
        return _context.Alumnos.Any(e => e.Id == id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IMPORTACIÓN MASIVA DE ALUMNOS
    // ─────────────────────────────────────────────────────────────────────────

    // GET: Alumnos/Importar
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Importar()
    {
        ViewBag.Cursos = await _context.Cursos
            .Where(c => c.Activo)
            .OrderBy(c => c.Nivel).ThenBy(c => c.GradoAnio)
            .ToListAsync();

        return View(new ImportarAlumnosViewModel());
    }

    // POST: Alumnos/Importar
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Importar(IFormFile? archivo)
    {
        var vm = new ImportarAlumnosViewModel { ProcesadoAlMenosUnaVez = true };

        ViewBag.Cursos = await _context.Cursos
            .Where(c => c.Activo)
            .OrderBy(c => c.Nivel).ThenBy(c => c.GradoAnio)
            .ToListAsync();

        if (archivo == null || archivo.Length == 0)
        {
            vm.Errores.Add("No se seleccionó ningún archivo, o el archivo está vacío.");
            return View(vm);
        }

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            vm.Errores.Add($"El formato '{extension}' no es válido. Usá .csv o .xlsx.");
            return View(vm);
        }

        // Obtenemos los DNIs ya existentes en un HashSet para O(1) de lookup.
        var dnisExistentes = await _context.Alumnos
            .Select(a => a.Dni)
            .ToHashSetAsync();

        // Obtenemos los IDs de cursos activos para validar CursoId.
        var cursosActivos = await _context.Cursos
            .Where(c => c.Activo)
            .Select(c => c.Id)
            .ToHashSetAsync();

        var filas = new List<AlumnoCsvRow>();

        // ── Parseo según extensión ──────────────────────────────────────────
        try
        {
            if (extension == ".csv")
            {
                using var stream = archivo.OpenReadStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    // Toleramos cabeceras con espacios extra y mayúsculas mixtas.
                    PrepareHeaderForMatch = args => args.Header.Trim().ToLower(),
                    MissingFieldFound = null,       // ignora columnas opcionales ausentes
                    HeaderValidated = null,         // no lanza si faltan columnas opcionales
                    BadDataFound = null,            // ignora celdas con datos raros en vez de lanzar
                };
                using var csv = new CsvReader(reader, config);
                filas = csv.GetRecords<AlumnoCsvRow>().ToList();
            }
            else // .xlsx
            {
                using var stream = archivo.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.First();
                var rows = ws.RowsUsed().ToList();

                if (rows.Count < 2)
                {
                    vm.Errores.Add("El archivo Excel está vacío o solo tiene cabecera.");
                    return View(vm);
                }

                // Mapeamos cabeceras (fila 1) → índice de columna.
                var headerRow = rows[0];
                var headers = headerRow.Cells()
                    .ToDictionary(
                        c => c.Value.ToString().Trim().ToLower(),
                        c => c.Address.ColumnNumber);

                string CellVal(IXLRow row, string header)
                {
                    if (!headers.TryGetValue(header, out var col)) return string.Empty;
                    return row.Cell(col).Value.ToString().Trim();
                }

                for (int i = 1; i < rows.Count; i++)
                {
                    var r = rows[i];
                    filas.Add(new AlumnoCsvRow
                    {
                        Nombre       = CellVal(r, "nombre"),
                        Apellido     = CellVal(r, "apellido"),
                        Dni          = CellVal(r, "dni"),
                        Nivel        = CellVal(r, "nivel"),
                        GradoAnio    = CellVal(r, "gradoanio"),
                        Turno        = CellVal(r, "turno"),
                        EsRecursante = CellVal(r, "esrecursante"),
                        CursoId      = CellVal(r, "cursoid"),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            vm.Errores.Add($"No se pudo leer el archivo: {ex.Message}");
            return View(vm);
        }

        // ── Procesamiento fila por fila ─────────────────────────────────────
        var alumnosNuevos        = new List<Alumno>();
        var inscripcionesNuevas  = new List<(Alumno alumno, int cursoId)>();

        for (int i = 0; i < filas.Count; i++)
        {
            var fila       = filas[i];
            int nroFila    = i + 2; // +2 porque la fila 1 es la cabecera
            var erroresFila = new List<string>();

            // — Validación de campos obligatorios —
            if (string.IsNullOrWhiteSpace(fila.Nombre))
                erroresFila.Add("Nombre vacío");
            if (string.IsNullOrWhiteSpace(fila.Apellido))
                erroresFila.Add("Apellido vacío");
            if (string.IsNullOrWhiteSpace(fila.Dni))
                erroresFila.Add("DNI vacío");

            // — Nivel —
            NivelEducativo nivel = default;
            if (!Enum.TryParse<NivelEducativo>(fila.Nivel?.Trim(), ignoreCase: true, out nivel))
                erroresFila.Add($"Nivel '{fila.Nivel}' inválido (usá 'Primaria' o 'Secundaria')");

            // — GradoAnio —
            if (!int.TryParse(fila.GradoAnio?.Trim(), out int grado) || grado < 1 || grado > 6)
                erroresFila.Add($"GradoAnio '{fila.GradoAnio}' inválido (debe ser 1-6)");

            // — Turno —
            Turno turno = default;
            if (!Enum.TryParse<Turno>(fila.Turno?.Trim(), ignoreCase: true, out turno))
                erroresFila.Add($"Turno '{fila.Turno}' inválido (usá 'Mañana' o 'Tarde')");

            // — DNI duplicado —
            var dniLimpio = fila.Dni?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dniLimpio) && dnisExistentes.Contains(dniLimpio))
                erroresFila.Add($"DNI {dniLimpio} ya existe en la base de datos");

            if (erroresFila.Any())
            {
                vm.Errores.Add($"Fila {nroFila} ({fila.Apellido}, {fila.Nombre}) → {string.Join(" / ", erroresFila)}.");
                vm.FilasFallidas++;
                continue;
            }

            // — CursoId (opcional) —
            int? cursoIdValido = null;
            if (!string.IsNullOrWhiteSpace(fila.CursoId))
            {
                if (int.TryParse(fila.CursoId.Trim(), out int cid) && cursosActivos.Contains(cid))
                {
                    cursoIdValido = cid;
                }
                else
                {
                    // CursoId inválido no aborta: el alumno se importa sin inscripción
                    // y se avisa con una advertencia.
                    vm.Errores.Add($"Fila {nroFila} ({fila.Apellido}, {fila.Nombre}) — advertencia: CursoId '{fila.CursoId}' no existe o no está activo. El alumno fue importado sin inscripción.");
                }
            }

            // — EsRecursante (opcional, default false) —
            bool esRecursante = false;
            if (!string.IsNullOrWhiteSpace(fila.EsRecursante))
                bool.TryParse(fila.EsRecursante.Trim(), out esRecursante);

            var alumno = new Alumno
            {
                Nombre       = fila.Nombre!.Trim(),
                Apellido     = fila.Apellido!.Trim(),
                Dni          = dniLimpio,
                Nivel        = nivel,
                GradoAnio    = grado,
                Turno        = turno,
                EsRecursante = esRecursante,
                Activo       = true,
            };

            alumnosNuevos.Add(alumno);
            dnisExistentes.Add(dniLimpio); // evita duplicados dentro del mismo archivo

            if (cursoIdValido.HasValue)
                inscripcionesNuevas.Add((alumno, cursoIdValido.Value));
        }

        // ── Persistencia ────────────────────────────────────────────────────
        if (alumnosNuevos.Any())
        {
            _context.Alumnos.AddRange(alumnosNuevos);
            await _context.SaveChangesAsync(); // los Alumno.Id ya están populados aquí

            var inscripciones = inscripcionesNuevas.Select(x => new Inscripcion
            {
                AlumnoId         = x.alumno.Id,
                CursoId          = x.cursoId,
                FechaInscripcion = DateTime.Now,
                Activo           = true,
            }).ToList();

            if (inscripciones.Any())
            {
                _context.Inscripciones.AddRange(inscripciones);
                await _context.SaveChangesAsync();
            }

            vm.ImportadosOk = alumnosNuevos.Count;
        }

        return View(vm);
    }

    // GET: Alumnos/DescargarExcelEjemplo
    // Genera una plantilla .xlsx con ClosedXML lista para completar y reimportar.
    [Authorize(Roles = "Admin")]
    public IActionResult DescargarExcelEjemplo()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Alumnos");

        // ── Cabeceras ────────────────────────────────────────────────────────
        string[] headers = ["Nombre", "Apellido", "Dni", "Nivel", "GradoAnio", "Turno", "EsRecursante", "CursoId"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB"); // azul Bootstrap
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ── Filas de ejemplo ─────────────────────────────────────────────────
        object?[][] ejemplos =
        [
            ["Juan",  "Pérez",    "12345678", "Primaria",   3, "Mañana", false, null],
            ["María", "González", "87654321", "Secundaria", 1, "Tarde",  true,  5   ],
        ];

        for (int r = 0; r < ejemplos.Length; r++)
        {
            for (int c = 0; c < ejemplos[r].Length; c++)
            {
                var val = ejemplos[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                if (val is null)
                    cell.Value = Blank.Value;
                else if (val is bool b)
                    cell.Value = b;
                else if (val is int n)
                    cell.Value = n;
                else
                    cell.Value = val.ToString();

                // Filas de ejemplo en gris claro alternado
                cell.Style.Fill.BackgroundColor = r % 2 == 0
                    ? XLColor.FromHtml("#F1F5F9")
                    : XLColor.White;
            }
        }

        // ── Formato general ──────────────────────────────────────────────────
        var rango = ws.Range(1, 1, ejemplos.Length + 1, headers.Length);
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        rango.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        rango.Style.Border.InsideBorderColor  = XLColor.FromHtml("#CBD5E1");

        // Nota de referencia de valores válidos en la fila 5
        ws.Cell(5, 1).Value = "Nivel: Primaria | Secundaria     GradoAnio: 1-6     Turno: Mañana | Tarde     EsRecursante: true | false     CursoId: ID numérico (opcional)";
        ws.Cell(5, 1).Style.Font.Italic = true;
        ws.Cell(5, 1).Style.Font.FontColor = XLColor.FromHtml("#64748B");
        ws.Range(5, 1, 5, headers.Length).Merge();

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_alumnos.xlsx");
    }

    // GET: Alumnos/ExportarExcel
    // Exporta todos los alumnos activos con su inscripción y curso al archivo .xlsx.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportarExcel()
    {
        var alumnos = await _context.Alumnos
            .Where(a => a.Activo)
            .Include(a => a.Inscripciones.Where(i => i.Activo))
                .ThenInclude(i => i.Curso)
            .OrderBy(a => a.Apellido).ThenBy(a => a.Nombre)
            .AsNoTracking()
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Padrón de Alumnos");

        // ── Título ────────────────────────────────────────────────────────────
        ws.Cell(1, 1).Value = $"Padrón de Alumnos — exportado el {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Range(1, 1, 1, 10).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ── Cabeceras (fila 3) ────────────────────────────────────────────────
        string[] headers = ["#", "Apellido", "Nombre", "DNI", "Nivel", "Grado/Año", "Turno", "Recursante", "CursoId", "Curso"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(3, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ── Datos ─────────────────────────────────────────────────────────────
        for (int i = 0; i < alumnos.Count; i++)
        {
            var a          = alumnos[i];
            int fila       = i + 4;   // empieza en fila 4 (título en 1, vacía en 2, header en 3)
            var inscripcion = a.Inscripciones.FirstOrDefault();
            var curso       = inscripcion?.Curso;

            // Colores alternados para legibilidad
            var bgColor = i % 2 == 0 ? XLColor.FromHtml("#EFF6FF") : XLColor.White;

            object?[] valores =
            [
                i + 1,
                a.Apellido,
                a.Nombre,
                a.Dni,
                a.Nivel.ToString(),
                a.GradoAnio,
                a.Turno.ToString(),
                a.EsRecursante ? "Sí" : "No",
                curso != null ? (object?)curso.Id : Blank.Value,
                curso?.Etiqueta ?? "—",
            ];

            for (int c = 0; c < valores.Length; c++)
            {
                var cell = ws.Cell(fila, c + 1);
                var val  = valores[c];

                if (val is null || val is Blank)
                    cell.Value = Blank.Value;
                else if (val is int n)
                    cell.Value = n;
                else
                    cell.Value = val.ToString();

                cell.Style.Fill.BackgroundColor = bgColor;

                // Columna "Recursante": resaltar los que recursan
                if (c == 7 && a.EsRecursante)
                    cell.Style.Font.FontColor = XLColor.FromHtml("#D97706");
            }
        }

        // ── Bordes y ajuste de columnas ───────────────────────────────────────
        if (alumnos.Count > 0)
        {
            var rango = ws.Range(3, 1, alumnos.Count + 3, headers.Length);
            rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rango.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
            rango.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BFDBFE");
            rango.Style.Border.InsideBorderColor  = XLColor.FromHtml("#BFDBFE");
        }

        // Congelar la fila de cabeceras para scroll cómodo
        ws.SheetView.FreezeRows(3);
        ws.Columns().AdjustToContents();
        // Mínimo ancho para la columna Curso
        if (ws.Column(10).Width < 25) ws.Column(10).Width = 25;

        // ── Fila de totales ────────────────────────────────────────────────────
        int filaTotales = alumnos.Count + 4;
        ws.Cell(filaTotales, 1).Value = $"Total: {alumnos.Count} alumno{(alumnos.Count == 1 ? "" : "s")}";
        ws.Range(filaTotales, 1, filaTotales, headers.Length).Merge();
        ws.Cell(filaTotales, 1).Style.Font.Bold = true;
        ws.Cell(filaTotales, 1).Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
        ws.Cell(filaTotales, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        ws.Cell(filaTotales, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var nombreArchivo = $"padron_alumnos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            nombreArchivo);
    }

    // ── Buscador global ───────────────────────────────────────────────────────
    // Endpoint liviano para el autocomplete del navbar. Devuelve JSON puro
    // (sin vista) para que el frontend pueda llamarlo con fetch() sin overhead
    // de Razor. Solo accesible para Admin: si otro rol llega a la URL, MVC
    // responde 403 automáticamente por el atributo de la clase.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Buscar(string? q)
    {
        // Umbral mínimo: menos de 2 caracteres genera demasiados resultados y
        // carga innecesaria en la BD. El frontend también lo valida, pero lo
        // verificamos acá por si alguien llama al endpoint directamente.
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var termino = q.Trim();

        // SQL Server es case-insensitive por collation por defecto, así que
        // .Contains() ya busca sin distinguir mayúsculas. EF lo traduce a
        // LIKE '%termino%' en la query SQL — eficiente para tablas de 500-600
        // alumnos sin necesidad de índice de texto completo.
        var resultados = await _context.Alumnos
            .Where(a => a.Nombre.Contains(termino)
                     || a.Apellido.Contains(termino)
                     || a.Dni.Contains(termino))
            .OrderBy(a => a.Apellido)
            .ThenBy(a => a.Nombre)
            .Take(10)
            .Select(a => new
            {
                a.Id,
                a.Nombre,
                a.Apellido,
                a.Dni,
            })
            .ToListAsync();

        return Json(resultados);
    }
}