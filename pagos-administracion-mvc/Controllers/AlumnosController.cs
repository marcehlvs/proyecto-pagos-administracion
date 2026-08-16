
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Data;
using Microsoft.AspNetCore.Authorization;
[Authorize]
public class AlumnosController : Controller
{
    private readonly AdministracionDbContext _context;

    public AlumnosController(AdministracionDbContext context)
    {
        _context = context;
    }

    // GET: ALUMNOS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Alumnos.ToListAsync());
    }

    // GET: ALUMNOS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var alumno = await _context.Alumnos
            .FirstOrDefaultAsync(m => m.Id == id);
        if (alumno == null)
        {
            return NotFound();
        }

        return View(alumno);
    }

    // GET: ALUMNOS/Create
    [Authorize(Roles ="Admin")]
    public IActionResult Create()
    {
        return View();
    }

    // POST: ALUMNOS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Nombre,Apellido,Dni,Nivel,GradoAnio,Turno,Cuotas")] Alumno alumno)
    {
        if (ModelState.IsValid)
        {
            _context.Add(alumno);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
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
        return View(alumno);
    }

    // POST: ALUMNOS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Apellido,Dni,Nivel,GradoAnio,Turno,Cuotas")] Alumno alumno)
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
            .FirstOrDefaultAsync(m => m.Id == id);
        if (alumno == null)
        {
            return NotFound();
        }

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
            _context.Alumnos.Remove(alumno);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool AlumnoExists(int? id)
    {
        return _context.Alumnos.Any(e => e.Id == id);
    }
}
