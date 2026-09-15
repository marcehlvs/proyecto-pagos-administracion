using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // Rol Alumno: acceso exclusivo a sus propias Tareas/Entregas (mismo criterio que
    // MisAsistenciasController). Rol Familia: las de todos sus hijos, con selector si tiene más
    // de uno (mismo criterio que BoletinesController) — puede entregar en nombre del Alumno,
    // útil para los grados más chicos. El filtro por AlumnoUserId/FamiliaUserId en la query (no
    // solo el [Authorize]) es lo que evita ver/entregar tareas de un alumno ajeno cambiando la URL.
    [Authorize(Roles = "Alumno,Familia")]
    public class MisTareasController : Controller
    {
        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };
        private const long TamañoMaximoBytes = 5 * 1024 * 1024;

        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisTareasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<List<Alumno>> MisAlumnosAsync()
        {
            var userId = _userManager.GetUserId(User);
            var query = User.IsInRole("Alumno")
                ? _context.Alumnos.Where(a => a.AlumnoUserId == userId)
                : _context.Alumnos.Where(a => a.FamiliaUserId == userId);
            return await query.OrderBy(a => a.Apellido).ToListAsync();
        }

        // GET: MisTareas?alumnoId=5 — sin alumnoId, muestra el primero (o el único, caso Alumno).
        public async Task<IActionResult> Index(int? alumnoId)
        {
            var misAlumnos = await MisAlumnosAsync();
            if (misAlumnos.Count == 0) return NotFound();

            var alumno = alumnoId.HasValue ? misAlumnos.FirstOrDefault(a => a.Id == alumnoId.Value) : misAlumnos.First();
            if (alumno == null) return Forbid(); // pidieron un alumnoId que no es suyo.

            ViewBag.MisAlumnos = misAlumnos;
            ViewBag.AlumnoSeleccionado = alumno;

            var inscripcionIds = await _context.Inscripciones
                .Where(i => i.AlumnoId == alumno.Id)
                .Select(i => i.Id)
                .ToListAsync();

            var entregas = await _context.Entregas
                .Include(e => e.Tarea).ThenInclude(t => t.CursoAsignatura).ThenInclude(ca => ca.Asignatura)
                .Where(e => inscripcionIds.Contains(e.InscripcionId))
                .OrderByDescending(e => e.Tarea.FechaEntrega)
                .ToListAsync();

            return View(entregas);
        }

        // Confirma que la Entrega pedida pertenece a uno de mis Alumnos (Alumno propio o hijo,
        // según el rol) y la devuelve ya con Tarea incluida. Es el único punto de chequeo de
        // permiso para Subir/DescargarArchivo — evita repetir la lógica en cada acción.
        private async Task<Entrega?> ObtenerEntregaPropiaAsync(int entregaId)
        {
            var misAlumnos = await MisAlumnosAsync();
            var misAlumnoIds = misAlumnos.Select(a => a.Id).ToHashSet();

            var entrega = await _context.Entregas
                .Include(e => e.Tarea)
                .Include(e => e.Inscripcion)
                .FirstOrDefaultAsync(e => e.Id == entregaId);

            if (entrega == null || !misAlumnoIds.Contains(entrega.Inscripcion.AlumnoId)) return null;
            return entrega;
        }

        // POST: MisTareas/Subir — texto y/o archivo para la propia Entrega. Se puede completar
        // uno solo, los dos, o volver a subir encima de lo anterior (se pisa). Marca Entregada
        // en true; la fecha real de entrega queda para que el Docente vea si llegó a tiempo.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subir(int entregaId, string? texto, IFormFile? archivo)
        {
            var entrega = await ObtenerEntregaPropiaAsync(entregaId);
            if (entrega == null) return NotFound();

            if (string.IsNullOrWhiteSpace(texto) && (archivo == null || archivo.Length == 0))
            {
                TempData["ErrorTarea"] = "Escribí algo o adjuntá un archivo antes de entregar.";
                return RedirectToAction(nameof(Index), new { alumnoId = entrega.Inscripcion.AlumnoId });
            }

            if (archivo != null && archivo.Length > 0)
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!ExtensionesPermitidas.Contains(extension) || archivo.Length > TamañoMaximoBytes)
                {
                    TempData["ErrorTarea"] = "Archivo inválido (jpg/png/pdf/doc/docx, máx 5MB).";
                    return RedirectToAction(nameof(Index), new { alumnoId = entrega.Inscripcion.AlumnoId });
                }

                var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "tareas");
                Directory.CreateDirectory(carpeta);
                var nombreArchivo = $"{Guid.NewGuid()}{extension}";
                using (var stream = new FileStream(Path.Combine(carpeta, nombreArchivo), FileMode.Create))
                    await archivo.CopyToAsync(stream);

                entrega.ArchivoRuta = nombreArchivo;
                entrega.ArchivoNombreOriginal = archivo.FileName;
            }

            if (!string.IsNullOrWhiteSpace(texto)) entrega.Texto = texto;
            entrega.Entregada = true;
            entrega.FechaEntrega = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Entrega guardada.";
            return RedirectToAction(nameof(Index), new { alumnoId = entrega.Inscripcion.AlumnoId });
        }

        // GET: MisTareas/DescargarArchivo/5 — para volver a ver lo que ya subiste. La versión
        // para el Docente/Admin (ver lo que subió el Alumno) vive en TareasController, con su
        // propio chequeo de permiso.
        public async Task<IActionResult> DescargarArchivo(int entregaId)
        {
            var entrega = await ObtenerEntregaPropiaAsync(entregaId);
            if (entrega?.ArchivoRuta == null) return NotFound();

            var ruta = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "tareas", entrega.ArchivoRuta);
            if (!System.IO.File.Exists(ruta)) return NotFound();

            var contentType = Path.GetExtension(ruta) == ".pdf" ? "application/pdf" : "application/octet-stream";
            return PhysicalFile(ruta, contentType, entrega.ArchivoNombreOriginal ?? entrega.ArchivoRuta);
        }

        // GET: MisTareas/DescargarArchivoTarea/5 — el adjunto que puso el Docente en la consigna
        // de la Tarea. Válido solo si la Tarea tiene una Entrega para alguno de mis alumnos (o
        // sea, es de un curso donde está inscripto) — mismo espíritu que ObtenerEntregaPropiaAsync.
        public async Task<IActionResult> DescargarArchivoTarea(int tareaId)
        {
            var tarea = await _context.Tareas.FindAsync(tareaId);
            if (tarea?.ArchivoRuta == null) return NotFound();

            var misAlumnoIds = (await MisAlumnosAsync()).Select(a => a.Id).ToHashSet();
            var esVisible = await _context.Entregas
                .Include(e => e.Inscripcion)
                .AnyAsync(e => e.TareaId == tareaId && misAlumnoIds.Contains(e.Inscripcion.AlumnoId));
            if (!esVisible) return NotFound();

            var ruta = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "tareas", tarea.ArchivoRuta);
            if (!System.IO.File.Exists(ruta)) return NotFound();

            var contentType = Path.GetExtension(ruta) == ".pdf" ? "application/pdf" : "application/octet-stream";
            return PhysicalFile(ruta, contentType, tarea.ArchivoNombreOriginal ?? tarea.ArchivoRuta);
        }
    }
}
