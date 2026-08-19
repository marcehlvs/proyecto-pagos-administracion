using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using System.Diagnostics;

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
            return View();
        }

        public IActionResult Nosotros() => View();

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}