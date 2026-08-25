using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FamiliasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;

        public FamiliasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
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
                    var (exito, error) = await _emailService.EnviarAsync(usuario.Email!, "Acceso al Portal de Pagos - Escuela José de San Martín",
    $"<p>Se creó tu cuenta de acceso al portal de la escuela.</p>" +
    $"<p><strong>Usuario:</strong> {usuario.Email}<br/><strong>Contraseña provisoria:</strong> {modelo.Password}</p>" +
    $"<p>Te recomendamos cambiarla después de tu primer ingreso, desde \"Mi perfil\".</p>");

                    if (!exito)
                    {
                        // La familia y su acceso ya quedaron creados igual; solo avisamos que el mail
                        // de bienvenida no salió, para no dejarlo enterrado en el log de consola.
                        TempData["EmailError"] = $"La familia se creó bien, pero el mail de bienvenida no se pudo enviar: {error}";
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



        // GET: Familias/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            var alumnosAsignados = await _context.Alumnos.Where(a => a.FamiliaUserId == id).ToListAsync();
            var alumnosDisponibles = await _context.Alumnos.Where(a => a.FamiliaUserId == null || a.FamiliaUserId == id)
                .OrderBy(a => a.Apellido).ToListAsync();

            ViewBag.AlumnosDisponibles = alumnosDisponibles;
            ViewBag.AlumnoIdsAsignados = alumnosAsignados.Select(a => a.Id).ToList();

            return View(usuario);
        }

        // POST: Familias/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, List<int> alumnoIds)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            // desasignar los que ya no están tildados
            var actuales = await _context.Alumnos.Where(a => a.FamiliaUserId == id).ToListAsync();
            foreach (var alumno in actuales.Where(a => !alumnoIds.Contains(a.Id)))
                alumno.FamiliaUserId = null;

            // asignar los nuevos
            var nuevos = await _context.Alumnos.Where(a => alumnoIds.Contains(a.Id) && a.FamiliaUserId != id).ToListAsync();
            foreach (var alumno in nuevos)
                alumno.FamiliaUserId = id;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Familias/Delete/id
        public async Task<IActionResult> Delete(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            ViewBag.AlumnosAsignados = await _context.Alumnos.Where(a => a.FamiliaUserId == id).ToListAsync();
            return View(usuario);
        }

        // POST: Familias/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null)
                await _userManager.DeleteAsync(usuario);

            return RedirectToAction(nameof(Index));
        }
    }
}