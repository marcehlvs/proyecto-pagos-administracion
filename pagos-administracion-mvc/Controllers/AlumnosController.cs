using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;
[Authorize]
public class AlumnosController : Controller
{
    private readonly AdministracionDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AlumnosController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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

    private bool AlumnoExists(int? id)
    {
        return _context.Alumnos.Any(e => e.Id == id);
    }
}