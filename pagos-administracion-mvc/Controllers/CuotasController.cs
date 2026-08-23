
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;
[Authorize]
public class CuotasController : Controller
{
    private readonly AdministracionDbContext _context;

    public CuotasController(AdministracionDbContext context)
    {
        _context = context;
    }
    [Authorize(Roles = "admin")]
    // GET: CUOTAS
    public async Task<IActionResult> Index(string? buscarAlumno, EstadoCuota? estado, NivelEducativo? nivel, int? gradoAnio, Turno? turno)
    {
        var query = _context.Cuotas.Include(c => c.Alumno).AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscarAlumno))
            query = query.Where(c => c.Alumno.Apellido.Contains(buscarAlumno)
                                   || c.Alumno.Nombre.Contains(buscarAlumno)
                                   || c.Alumno.Dni.Contains(buscarAlumno));

        if (estado.HasValue) query = query.Where(c => c.Estado == estado.Value);

        // Aplicamos los nuevos filtros
        if (nivel.HasValue) query = query.Where(c => c.Alumno.Nivel == nivel.Value);
        if (gradoAnio.HasValue) query = query.Where(c => c.Alumno.GradoAnio == gradoAnio.Value);
        if (turno.HasValue) query = query.Where(c => c.Alumno.Turno == turno.Value);

        ViewBag.BuscarAlumno = buscarAlumno;
        ViewBag.EstadoSeleccionado = estado;
        ViewBag.NivelSeleccionado = nivel;
        ViewBag.GradoSeleccionado = gradoAnio;
        ViewBag.TurnoSeleccionado = turno;

        return View(await query.OrderByDescending(c => c.Anio).ThenBy(c => c.Mes).ToListAsync());
    }
    [Authorize(Roles = "admin")]
    // GET: CUOTAS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var cuota = await _context.Cuotas
            .Include(c => c.Alumno)
                .ThenInclude(a => a.FamiliaUser)
            .Include(c => c.Pagos)
            .Include(c => c.ContactosManuales)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (cuota == null)
        {
            return NotFound();
        }

        return View(cuota);
    }
    [Authorize(Roles = "Admin")]
    // GET: CUOTAS/Create
    public IActionResult Create()
    {
        ViewData["AlumnoId"] = new SelectList(_context.Alumnos, "Id", "Apellido");
        return View();
    }
    [Authorize(Roles = "Admin")]
    // POST: CUOTAS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,AlumnoId,Mes,Anio,Monto,FechaVencimiento,Estado")] Cuota cuota)
    {
        if (ModelState.IsValid)
        {
            _context.Add(cuota);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewData["AlumnoId"] = new SelectList(_context.Alumnos, "Id", "Apellido", cuota.AlumnoId);
        return View(cuota);
    }
    [Authorize(Roles = "Admin")]

    // GET: CUOTAS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var cuota = await _context.Cuotas.FindAsync(id);
        if (cuota == null)
        {
            return NotFound();
        }
        ViewData["AlumnoId"] = new SelectList(_context.Alumnos, "Id", "Apellido", cuota.AlumnoId);
        return View(cuota);
    }
    [Authorize(Roles = "Admin")]
    // POST: CUOTAS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,AlumnoId,Mes,Anio,Monto,FechaVencimiento,Estado")] Cuota cuota)
    {
        if (id != cuota.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(cuota);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CuotaExists(cuota.Id))
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
        ViewData["AlumnoId"] = new SelectList(_context.Alumnos, "Id", "Apellido", cuota.AlumnoId);
        return View(cuota);
    }
    [Authorize(Roles = "Admin")]
    // GET: CUOTAS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var cuota = await _context.Cuotas
            .FirstOrDefaultAsync(m => m.Id == id);
        if (cuota == null)
        {
            return NotFound();
        }

        return View(cuota);
    }
    [Authorize(Roles = "Admin")]
    // POST: CUOTAS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var cuota = await _context.Cuotas.FindAsync(id);
        if (cuota != null)
        {
            _context.Cuotas.Remove(cuota);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool CuotaExists(int? id)
    {
        return _context.Cuotas.Any(e => e.Id == id);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarContacto(int cuotaId, string medio, string? notas)
    {
        _context.ContactosManuales.Add(new ContactoManual { CuotaId = cuotaId, Medio = medio, Notas = notas });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = cuotaId });
    }
}
