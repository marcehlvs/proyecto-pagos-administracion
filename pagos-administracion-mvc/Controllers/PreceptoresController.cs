using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace pagos_administracion_mvc.Controllers
{
    // Preceptor: a cargo de Asistencia y de la confección de boletines de los Cursos que tiene
    // asignados (Curso.ProfesorUserId). Rol separado de Docente: un Docente carga notas por
    // materia (CursoAsignatura.DocenteUserId), un Preceptor está a cargo del curso en general.
    // Mismo patrón que DocentesController (alta con contraseña provisoria + mail de bienvenida).
    [Authorize(Roles = "Admin")]
    public class PreceptoresController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;

        public PreceptoresController(AdministracionDbContext context, UserManager<ApplicationUser> userManager, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // GET: Preceptores
        public async Task<IActionResult> Index()
        {
            var preceptores = await _userManager.GetUsersInRoleAsync("Preceptor");

            var cursosPorPreceptor = (await _context.Cursos.Where(c => c.ProfesorUserId != null && c.Activo).ToListAsync())
                .GroupBy(c => c.ProfesorUserId!)
                .ToDictionary(g => g.Key, g => g.Select(c => c.Etiqueta).ToList());

            ViewBag.CursosPorPreceptor = cursosPorPreceptor;

            return View(preceptores.OrderBy(p => p.Email).ToList());
        }

        // GET: Preceptores/Create
        public IActionResult Create() => View();

        // POST: Preceptores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Required, EmailAddress] string email)
        {
            if (!ModelState.IsValid) return View();

            var passwordTemporal = GenerarPasswordTemporal();

            var usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var resultado = await _userManager.CreateAsync(usuario, passwordTemporal);

            if (resultado.Succeeded)
            {
                await _userManager.AddToRoleAsync(usuario, "Preceptor");

                var cuerpoBienvenida = EmailService.EnvolverPlantilla(
                    "¡Bienvenido/a al portal de la escuela!",
                    $@"<p style=""margin:0 0 16px 0;"">Se creó tu cuenta de acceso al portal de la escuela para tomar asistencia y generar boletines de tus cursos.</p>
                    <p style=""margin:0 0 6px 0;""><strong>Usuario:</strong> {usuario.Email}</p>
                    <p style=""margin:0 0 16px 0;""><strong>Contraseña provisoria:</strong> {passwordTemporal}</p>
                    <p style=""margin:0; color:#4A5568; font-size:14px;"">Te recomendamos cambiarla después de tu primer ingreso, desde 'Mi perfil'.</p>");

                var (exito, error) = await _emailService.EnviarAsync(usuario.Email!, "Acceso al Portal de la Escuela José de San Martín", cuerpoBienvenida);

                if (!exito)
                {
                    TempData["EmailError"] = $"El preceptor se creó bien, pero el mail de bienvenida no se pudo enviar: {error}";
                }

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View();
        }

        // GET: Preceptores/Delete/id
        public async Task<IActionResult> Delete(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            ViewBag.CursosAsignados = await _context.Cursos.Where(c => c.ProfesorUserId == id).ToListAsync();
            return View(usuario);
        }

        // POST: Preceptores/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            // Los cursos que tenía asignados quedan sin Preceptor (ProfesorUserId -> null por el
            // OnDelete(SetNull) configurado en el DbContext), no se pierde el curso ni su historial.
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null)
                await _userManager.DeleteAsync(usuario);

            return RedirectToAction(nameof(Index));
        }

        private static string GenerarPasswordTemporal()
        {
            const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
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

            for (int i = clave.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (clave[i], clave[j]) = (clave[j], clave[i]);
            }

            return new string(clave);
        }
    }
}
