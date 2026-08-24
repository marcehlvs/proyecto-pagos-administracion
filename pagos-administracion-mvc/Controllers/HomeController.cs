using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using System.Diagnostics;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly AdministracionDbContext _context;

        public HomeController(AdministracionDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.CantidadAlumnos = await _context.Alumnos.CountAsync();
            var random = new Random();
            ViewBag.HeroVideo = $"hero-san-martin-{random.Next(1, 5)}.mp4"; // 1 a 4
            if (User.IsInRole("Admin"))
            {
                ViewBag.PagosRecientes = await _context.Pagos
                    .Include(p => p.Cuota).ThenInclude(c => c.Alumno)
                    .Where(p => p.Estado == EstadoPago.Aprobado && p.Fecha >= DateTime.Now.AddDays(-2))
                    .OrderByDescending(p => p.Fecha)
                    .Take(10)
                    .ToListAsync();

                ViewBag.ComprobantesPendientes = await _context.Pagos
                    .Include(p => p.Cuota).ThenInclude(c => c.Alumno)
                    .Where(p => p.Estado == EstadoPago.EnRevision)
                    .OrderBy(p => p.Fecha)
                    .ToListAsync();
            }
                ViewBag.Avisos = await _context.Avisos
                    .Where(a => a.Activo)
                    .OrderByDescending(a => a.FechaPublicacion)
                    .Take(3)
                    .ToListAsync();
            return View();
        }

        public IActionResult Nosotros() => View();

        public async Task<IActionResult> Calendario()
        {
            var eventos = await _context.Avisos
                .Where(a => a.Activo && a.Tipo == TipoAviso.Calendario)
                .OrderBy(a => a.FechaPublicacion)
                .ToListAsync();
            return View(eventos);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}