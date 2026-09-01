using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.Security.Cryptography;
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
    public async Task<IActionResult> Create([Bind("Id,Nombre,Apellido,Dni,Nivel,GradoAnio,Turno,FamiliaUserId")] Alumno alumno)
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
    // IMPORTANTE: Asegúrate de agregar FamiliaUserId al [Bind]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Apellido,Dni,Nivel,GradoAnio,Turno,FamiliaUserId")] Alumno alumno)
    {
        if (id != alumno.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(alumno);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AlumnoExists(alumno.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // Si el ModelState es inválido, recargar la lista manteniendo la selección
        ViewBag.FamiliaUserId = await ObtenerFamiliasSelectListAsync(alumno.FamiliaUserId);
        return View(alumno);
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

    // Idéntico al de FamiliasController: password temporal random, criptográficamente segura.
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

        for (int i = clave.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (clave[i], clave[j]) = (clave[j], clave[i]);
        }

        return new string(clave);
    }

    private bool AlumnoExists(int? id)
    {
        return _context.Alumnos.Any(e => e.Id == id);
    }
}