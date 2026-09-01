using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    // Rol Docente: solo ve los cursos que tiene asignados (Curso.ProfesorUserId), nunca el
    // listado completo de cursos (eso es exclusivo de Admin, vía CursosController).
    [Authorize(Roles = "Docente")]
    public class MisCursosController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisCursosController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: MisCursos
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var cursos = await _context.Cursos
                .Include(c => c.Inscripciones)
                .Where(c => c.ProfesorUserId == userId)
                .OrderBy(c => c.Nivel).ThenBy(c => c.GradoAnio).ThenBy(c => c.Turno)
                .ToListAsync();

            return View(cursos);
        }
    }
}
