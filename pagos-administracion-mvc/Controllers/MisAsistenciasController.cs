using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using pagos_administracion_mvc.Services;
using System.Globalization;

namespace pagos_administracion_mvc.Controllers
{
    // Rol Alumno: acceso exclusivo a su propia asistencia, nunca a Cuotas/Pagos/estado
    // administrativo de la Familia. El filtro por AlumnoUserId en la query (no solo el
    // [Authorize]) es lo que evita que un Alumno vea las faltas de otro cambiando la URL.
    [Authorize(Roles = "Alumno")]
    public class MisAsistenciasController : Controller
    {
        private static readonly CultureInfo CulturaEs = new("es-AR");

        private readonly AdministracionDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MisAsistenciasController(AdministracionDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: MisAsistencias?vista=semana|mes|todo&fecha=2026-09-01
        // "fecha" es cualquier día dentro del período que se quiere ver; sirve como referencia
        // para calcular el rango y para la navegación anterior/siguiente.
        public async Task<IActionResult> Index(string vista = "semana", DateTime? fecha = null)
        {
            var userId = _userManager.GetUserId(User);
            var fechaRef = (fecha ?? DateTime.Today).Date;

            var inscripciones = await _context.Inscripciones
                .Include(i => i.Curso)
                .Include(i => i.Asistencias)
                .Where(i => i.Alumno.AlumnoUserId == userId)
                .OrderBy(i => i.Curso.Nivel).ThenBy(i => i.Curso.GradoAnio).ThenBy(i => i.Curso.Turno)
                .ToListAsync();

            DateTime? inicio;
            DateTime? fin;
            DateTime fechaPrev;
            DateTime fechaNext;
            string tituloPeriodo;

            switch (vista)
            {
                case "mes":
                    inicio = new DateTime(fechaRef.Year, fechaRef.Month, 1);
                    fin = inicio.Value.AddMonths(1).AddDays(-1);
                    fechaPrev = fechaRef.AddMonths(-1);
                    fechaNext = fechaRef.AddMonths(1);
                    tituloPeriodo = inicio.Value.ToString("MMMM yyyy", CulturaEs);
                    break;

                case "todo":
                    inicio = null;
                    fin = null;
                    fechaPrev = fechaRef;
                    fechaNext = fechaRef;
                    tituloPeriodo = "Todo el historial";
                    break;

                default: // "semana"
                    vista = "semana";
                    var diasDesdeLunes = ((int)fechaRef.DayOfWeek + 6) % 7; // Lunes = 0 ... Domingo = 6
                    inicio = fechaRef.AddDays(-diasDesdeLunes);
                    fin = inicio.Value.AddDays(6);
                    fechaPrev = fechaRef.AddDays(-7);
                    fechaNext = fechaRef.AddDays(7);
                    tituloPeriodo = $"Semana del {inicio:dd/MM} al {fin:dd/MM/yyyy}";
                    break;
            }

            ViewBag.Vista = vista;
            ViewBag.Fecha = fechaRef;
            ViewBag.Inicio = inicio;
            ViewBag.Fin = fin;
            ViewBag.FechaPrev = fechaPrev;
            ViewBag.FechaNext = fechaNext;
            ViewBag.TituloPeriodo = tituloPeriodo;

            // Promedio de faltas del curso completo (todos los inscriptos, no solo el propio alumno),
            // para que pueda compararse sin ver el detalle de sus compañeros — solo un número agregado.
            // Es histórico (no se filtra por período): compararse contra "todo el año" del curso.
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
