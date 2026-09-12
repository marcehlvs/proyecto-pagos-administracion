using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;

namespace pagos_administracion_mvc.Controllers
{
    // Admin puede generar el boletín de cualquier Alumno. Familia solo el de sus propios hijos
    // (Alumno.FamiliaUserId). Alumno solo el propio (Alumno.AlumnoUserId). Preceptor solo el de
    // alumnos inscriptos en algún Curso donde figura como ProfesorUserId (mismo criterio que
    // Asistencias). Se valida en la acción, mismo criterio que el resto de los controllers con
    // datos sensibles por rol.
    [Authorize(Roles = "Admin,Familia,Alumno,Preceptor")]
    public class BoletinesController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly BoletinService _boletinService;
        private readonly IBoletinPlantilla _plantillaPredeterminada;
        private readonly BoletinPlantillaRite _plantillaRite;

        public BoletinesController(AdministracionDbContext context, UserManager<ApplicationUser> userManager,
            BoletinService boletinService, IBoletinPlantilla plantillaPredeterminada, BoletinPlantillaRite plantillaRite)
        {
            _context = context;
            _userManager = userManager;
            _boletinService = boletinService;
            _plantillaPredeterminada = plantillaPredeterminada;
            _plantillaRite = plantillaRite;
        }

        private async Task<IActionResult?> ValidarPermisoAsync(int alumnoId)
        {
            if (User.IsInRole("Admin")) return null;

            var userId = _userManager.GetUserId(User);
            var alumno = await _context.Alumnos.FindAsync(alumnoId);
            if (alumno == null) return NotFound();

            if (User.IsInRole("Familia") && alumno.FamiliaUserId == userId) return null;
            if (User.IsInRole("Alumno") && alumno.AlumnoUserId == userId) return null;

            if (User.IsInRole("Preceptor"))
            {
                var esDeSuCurso = await _context.Inscripciones
                    .AnyAsync(i => i.AlumnoId == alumnoId && i.Curso.ProfesorUserId == userId);
                if (esDeSuCurso) return null;
            }

            return Forbid();
        }

        // GET: Boletines?alumnoId=1&anioLectivo=2026
        // Pantalla simple: Admin elige alumno + año. Familia/Alumno arrancan con su(s) propio(s)
        // alumno(s) ya resuelto(s), sin tener que elegir un id ajeno a mano.
        public async Task<IActionResult> Index(int? alumnoId, int? anioLectivo)
        {
            var anio = anioLectivo ?? DateTime.Today.Year;

            if (User.IsInRole("Admin"))
            {
                ViewBag.Alumnos = await _context.Alumnos.OrderBy(a => a.Apellido).ToListAsync();
            }
            else if (User.IsInRole("Familia"))
            {
                var userId = _userManager.GetUserId(User);
                ViewBag.Alumnos = await _context.Alumnos.Where(a => a.FamiliaUserId == userId).OrderBy(a => a.Apellido).ToListAsync();
            }
            else if (User.IsInRole("Preceptor"))
            {
                var userId = _userManager.GetUserId(User);
                ViewBag.Alumnos = await _context.Inscripciones
                    .Where(i => i.Curso.ProfesorUserId == userId)
                    .Select(i => i.Alumno)
                    .Distinct()
                    .OrderBy(a => a.Apellido)
                    .ToListAsync();
            }
            else // Alumno
            {
                var userId = _userManager.GetUserId(User);
                var propio = await _context.Alumnos.FirstOrDefaultAsync(a => a.AlumnoUserId == userId);
                ViewBag.Alumnos = propio != null ? new List<Alumno> { propio } : new List<Alumno>();
                alumnoId ??= propio?.Id;
            }

            ViewBag.AlumnoIdSeleccionado = alumnoId;
            ViewBag.AnioLectivo = anio;
            return View();
        }

        // GET: Boletines/Pdf?alumnoId=1&anioLectivo=2026&plantilla=rite
        // plantilla=rite usa el formulario oficial (RITE, Fase 1 — ver BoletinPlantillaRite);
        // cualquier otro valor (o ninguno) usa la plantilla propia de siempre.
        public async Task<IActionResult> Pdf(int alumnoId, int anioLectivo, string? plantilla)
        {
            var error = await ValidarPermisoAsync(alumnoId);
            if (error != null) return error;

            var datos = await _boletinService.ObtenerDatosAsync(alumnoId, anioLectivo);
            if (datos == null) return NotFound();

            byte[] pdf;
            if (plantilla == "rite")
            {
                pdf = _plantillaRite.Generar(datos, null);
            }
            else
            {
                var configuracionSitio = await _context.ConfiguracionSitio.FirstOrDefaultAsync(c => c.Id == 1);
                pdf = _plantillaPredeterminada.Generar(datos, configuracionSitio);
            }

            var nombreArchivo = $"boletin-{datos.Alumno.Apellido}-{datos.Alumno.Nombre}-{anioLectivo}.pdf".Replace(" ", "_");
            return File(pdf, "application/pdf", nombreArchivo);
        }
    }
}
