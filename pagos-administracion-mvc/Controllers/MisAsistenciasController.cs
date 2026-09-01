using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // Rol Alumno: acceso exclusivo a su propia asistencia, nunca a Cuotas/Pagos/estado
    // administrativo de la Familia. El filtro por AlumnoUserId en la query (no solo el
    // [Authorize]) es lo que evita que un Alumno vea las faltas de otro cambiando la URL.
    [Authorize(Roles = "Alumno")]
    public class MisAsistenciasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisAsistenciasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: MisAsistencias
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var inscripciones = await _context.Inscripciones
                .Include(i => i.Curso)
                .Include(i => i.Asistencias)
                .Where(i => i.Alumno.AlumnoUserId == userId)
                .OrderBy(i => i.Curso.Nombre)
                .ToListAsync();

            return View(inscripciones);
        }
    }
}
