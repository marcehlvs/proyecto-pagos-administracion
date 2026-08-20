using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FamiliasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FamiliasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Familias
        public async Task<IActionResult> Index(string? busqueda, bool? sinAsignar)
        {
            var familias = (await _userManager.GetUsersInRoleAsync("Familia")).AsEnumerable();

            var alumnosPorFamilia = (await _context.Alumnos.Where(a => a.FamiliaUserId != null).ToListAsync())
                .GroupBy(a => a.FamiliaUserId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (!string.IsNullOrWhiteSpace(busqueda))
                familias = familias.Where(f => f.Email!.Contains(busqueda, StringComparison.OrdinalIgnoreCase));

            if (sinAsignar == true)
                familias = familias.Where(f => !alumnosPorFamilia.ContainsKey(f.Id));

            ViewBag.AlumnosPorFamilia = alumnosPorFamilia;
            ViewBag.Busqueda = busqueda;
            ViewBag.SinAsignar = sinAsignar;

            return View(familias.OrderBy(f => f.Email).ToList());
        }

        // GET: Familias/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.AlumnosDisponibles = await _context.Alumnos
                .Where(a => a.FamiliaUserId == null)
                .OrderBy(a => a.Apellido)
                .ToListAsync();
            return View();
        }

        // POST: Familias/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FamiliaCreateViewModel modelo)
        {
            if (ModelState.IsValid)
            {
                var usuario = new ApplicationUser
                {
                    UserName = modelo.Email,
                    Email = modelo.Email,
                    EmailConfirmed = true // el admin lo da de alta directo, no requiere confirmar por mail
                };

                var resultado = await _userManager.CreateAsync(usuario, modelo.Password);

                if (resultado.Succeeded)
                {
                    await _userManager.AddToRoleAsync(usuario, "Familia");

                    if (modelo.AlumnoIds.Any())
                    {
                        var alumnos = await _context.Alumnos
                            .Where(a => modelo.AlumnoIds.Contains(a.Id))
                            .ToListAsync();

                        foreach (var alumno in alumnos)
                            alumno.FamiliaUserId = usuario.Id;

                        await _context.SaveChangesAsync();
                    }

                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in resultado.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.AlumnosDisponibles = await _context.Alumnos
                .Where(a => a.FamiliaUserId == null)
                .OrderBy(a => a.Apellido)
                .ToListAsync();
            return View(modelo);
        }
    }
}