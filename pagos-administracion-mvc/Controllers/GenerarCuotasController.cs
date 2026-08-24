using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class GenerarCuotasController : Controller
    {
        private readonly AdministracionDbContext _context;

        public GenerarCuotasController(AdministracionDbContext context)
        {
            _context = context;
        }

        public IActionResult Index() => View(new GenerarCuotasViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(GenerarCuotasViewModel modelo)
        {
            var query = _context.Alumnos.AsQueryable();

            if (modelo.Nivel.HasValue) query = query.Where(a => a.Nivel == modelo.Nivel.Value);
            if (modelo.Turno.HasValue) query = query.Where(a => a.Turno == modelo.Turno.Value);

            var alumnos = await query.ToListAsync();

            // cantidad de hermanos por familia, para el descuento
            var alumnosConFamilia = await _context.Alumnos
                .Where(a => a.FamiliaUserId != null)
                .ToListAsync();
            var cantidadPorFamilia = alumnosConFamilia
                .GroupBy(a => a.FamiliaUserId!)
                .ToDictionary(g => g.Key, g => g.Count());

            int creadas = 0, omitidas = 0;

            foreach (var alumno in alumnos)
            {
                var yaExiste = await _context.Cuotas.AnyAsync(c =>
                    c.AlumnoId == alumno.Id && c.Mes == modelo.Mes && c.Anio == modelo.Anio);

                if (yaExiste) { omitidas++; continue; }

                var montoBase = modelo.MontoBase;
                var esHermano = alumno.FamiliaUserId != null
                    && cantidadPorFamilia.TryGetValue(alumno.FamiliaUserId, out var cant) && cant > 1;

                if (esHermano && modelo.DescuentoHermanoPorcentaje > 0)
                    montoBase -= montoBase * (modelo.DescuentoHermanoPorcentaje / 100m);

                decimal? montoConDescuento = null;
                DateTime? fechaLimiteDescuento = null;
                if (modelo.DescuentoPagoATiempoPorcentaje > 0 && modelo.DiasParaDescuento > 0)
                {
                    montoConDescuento = montoBase - (montoBase * (modelo.DescuentoPagoATiempoPorcentaje / 100m));
                    fechaLimiteDescuento = modelo.FechaVencimiento.AddDays(-modelo.DiasParaDescuento);
                }

                _context.Cuotas.Add(new Cuota
                {
                    AlumnoId = alumno.Id,
                    Mes = modelo.Mes,
                    Anio = modelo.Anio,
                    Monto = montoBase,
                    MontoConDescuento = montoConDescuento,
                    FechaLimiteDescuento = fechaLimiteDescuento,
                    FechaVencimiento = modelo.FechaVencimiento,
                    Estado = EstadoCuota.Pendiente,
                    FechaCreacion = DateTime.Now,
                    CreadaPorNombre = $"Generación masiva ({User.Identity?.Name})"
                });
                creadas++;
            }

            await _context.SaveChangesAsync();

            TempData["Resultado"] = $"Se crearon {creadas} cuotas. Se omitieron {omitidas} (ya existían para ese mes/año).";
            return RedirectToAction(nameof(Index));
        }
    }
}