using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Familia")]
    public class MisCuotasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisCuotasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: MisCuotas
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var cuotas = await _context.Cuotas
                .Include(c => c.Alumno)
                .Where(c => c.Alumno.FamiliaUserId == userId)
                .OrderBy(c => c.Alumno.Apellido)
                .ThenBy(c => c.Anio)
                .ThenBy(c => c.Mes)
                .ToListAsync();

            return View(cuotas);
        }

        // GET: MisCuotas/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            var cuota = await _context.Cuotas
                .Include(c => c.Alumno)
                .Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.Id == id && c.Alumno.FamiliaUserId == userId);

            if (cuota == null) return NotFound();

            return View(cuota);
        }
    }
}