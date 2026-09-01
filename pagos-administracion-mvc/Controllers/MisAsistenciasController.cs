using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;

namespace pagos_administracion_mvc.Controllers
{
    // Rol Alumno: acceso exclusivo a su propia asistencia, nunca a Cuotas/Pagos/estado
    // administrativo de la Familia. El filtro por AlumnoUserId en la query (no solo el
    // [Authorize]) es lo que evita que un Alumno vea las faltas de otro cambiando la URL.
    [Authorize(Roles = "Alumno")]
    public class MisAsistenciasController : Controller
    {
        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisAsistenciasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: MisAsistencias
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var inscripciones = await _context.Inscripciones
                .Include(i => i.Curso)
                .Include(i => i.Asistencias)
                .Where(i => i.Alumno.AlumnoUserId == userId)
                .OrderBy(i => i.Curso.Nivel).ThenBy(i => i.Curso.GradoAnio).ThenBy(i => i.Curso.Turno)
                .ToListAsync();

            // Promedio de faltas del curso completo (todos los inscriptos, no solo el propio alumno),
            // para que pueda compararse sin ver el detalle de sus compañeros — solo un número agregado.
            var promedioPorCurso = new Dictionary<int, decimal>();
            foreach (var cursoId in inscripciones.Select(i => i.CursoId).Distinct())
            {
                var curso = inscripciones.First(i => i.CursoId == cursoId).Curso;
                var inscripcionesDelCurso = await _context.Inscripciones
                    .Include(i => i.Asistencias)
                    .Where(i => i.CursoId == cursoId)
                    .ToListAsync();

                var totales = inscripcionesDelCurso.Select(i => AsistenciaCalculadora.CalcularTotalFaltas(i.Asistencias, curso)).ToList();
                promedioPorCurso[cursoId] = totales.Count > 0 ? Math.Round(totales.Average(), 2) : 0m;
            }

            ViewBag.PromedioPorCurso = promedioPorCurso;
            return View(inscripciones);
        }
    }
}
