using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.Security.Cryptography;

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
                var passwordTemporal = GenerarPasswordTemporal();

                var usuario = new ApplicationUser
                {
                    UserName = modelo.Email,
                    Email = modelo.Email,
                    EmailConfirmed = true // el admin lo da de alta directo, no requiere confirmar por mail
                };

                var resultado = await _userManager.CreateAsync(usuario, passwordTemporal);

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
                    var cuerpoBienvenida = EmailService.EnvolverPlantilla(
                        "¡Bienvenido/a al portal de pagos!",
                        $@"<p style=""margin:0 0 16px 0;"">Se creó tu cuenta de acceso al portal de la escuela.</p>
                        <p style=""margin:0 0 6px 0;""><strong>Usuario:</strong> {usuario.Email}</p>
                        <p style=""margin:0 0 16px 0;""><strong>Contraseña provisoria:</strong> {passwordTemporal}</p>
                        <p style=""margin:0; color:#4A5568; font-size:14px;"">Te recomendamos cambiarla después de tu primer ingreso, desde "Mi perfil".</p>");

                    var (exito, error) = await _emailService.EnviarAsync(usuario.Email!, "Acceso al Portal de Pagos - Escuela José de San Martín", cuerpoBienvenida);

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

        // Password temporal random, criptográficamente segura: 10 caracteres combinando
        // mayúsculas, minúsculas, números y símbolos (aunque la política de Identity
        // configurada es laxa, para una clave que viaja por mail conviene que sea fuerte).
        private static string GenerarPasswordTemporal()
        {
            const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // sin I/O para evitar confusión visual
            const string minusculas = "abcdefghijkmnpqrstuvwxyz";
            const string numeros = "23456789";
            const string simbolos = "!@#$%&*";
            const string todos = mayusculas + minusculas + numeros + simbolos;

            Span<char> clave = stackalloc char[10];
            clave[0] = mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)];
            clave[1] = minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)];
            clave[2] = numeros[RandomNumberGenerator.GetInt32(numeros.Length)];
            clave[3] = simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)];
            for (int i = 4; i < clave.Length; i++)
                clave[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];

            // mezclar para que las posiciones fijas de arriba no sean predecibles
            for (int i = clave.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (clave[i], clave[j]) = (clave[j], clave[i]);
            }

            return new string(clave);
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