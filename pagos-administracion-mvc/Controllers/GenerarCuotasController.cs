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

            // Arancel vigente por Nivel: el registro Activo con VigenteDesde más reciente que sea
            // <= hoy. Se busca uno por Nivel (no por alumno), porque el desglose Curricular /
            // Extracurricular / etc. es el mismo para todos los alumnos de ese Nivel.
            var arancelesPorNivel = new Dictionary<NivelEducativo, ArancelNivel>();
            foreach (NivelEducativo nivel in Enum.GetValues(typeof(NivelEducativo)))
            {
                var arancel = await _context.ArancelesNivel
                    .Where(a => a.Nivel == nivel && a.VigenteDesde <= DateTime.Today)
                    .OrderByDescending(a => a.VigenteDesde)
                    .FirstOrDefaultAsync();
                if (arancel != null) arancelesPorNivel[nivel] = arancel;
            }

            // cantidad de hermanos por familia, para el descuento
            var alumnosConFamilia = await _context.Alumnos
                .Where(a => a.FamiliaUserId != null)
                .ToListAsync();
            var cantidadPorFamilia = alumnosConFamilia
                .GroupBy(a => a.FamiliaUserId!)
                .ToDictionary(g => g.Key, g => g.Count());

            int creadas = 0, omitidas = 0, sinArancel = 0;

            foreach (var alumno in alumnos)
            {
                if (!arancelesPorNivel.TryGetValue(alumno.Nivel, out var arancel))
                {
                    sinArancel++;
                    continue;
                }

                var yaExiste = await _context.Cuotas.AnyAsync(c =>
                    c.AlumnoId == alumno.Id && c.Mes == modelo.Mes && c.Anio == modelo.Anio);

                if (yaExiste) { omitidas++; continue; }

                // Monto base = suma de los 5 conceptos del arancel del Nivel del alumno
                // (Curricular + Extra curricular + Equip. Didáctico + Mantenimiento + Emerg. Médica).
                var montoBase = arancel.CuotaReal;
                var esHermano = alumno.FamiliaUserId != null
                    && cantidadPorFamilia.TryGetValue(alumno.FamiliaUserId, out var cant) && cant > 1;

                if (esHermano && modelo.DescuentoHermanoPorcentaje > 0)
                    montoBase -= montoBase * (modelo.DescuentoHermanoPorcentaje / 100m);

                // Monto con descuento por pago a tiempo: la bonificación fija del arancel
                // (ej. -1468 en Primaria) se aplica siempre que exista, y opcionalmente se le
                // suma el % adicional configurado en el formulario.
                decimal? montoConDescuento = null;
                DateTime? fechaLimiteDescuento = null;
                var hayBonificacionFija = arancel.BonificacionPagoATiempo > 0;
                var hayDescuentoPorcentual = modelo.DescuentoPagoATiempoPorcentaje > 0;
                if (hayBonificacionFija || hayDescuentoPorcentual)
                {
                    var monto = hayDescuentoPorcentual
                        ? montoBase - (montoBase * (modelo.DescuentoPagoATiempoPorcentaje / 100m))
                        : montoBase;
                    montoConDescuento = monto - arancel.BonificacionPagoATiempo;
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

            var mensaje = $"Se crearon {creadas} cuotas. Se omitieron {omitidas} (ya existían para ese mes/año).";
            if (sinArancel > 0)
                mensaje += $" {sinArancel} alumno(s) se saltearon por no tener un arancel vigente cargado para su Nivel (cargalo en Aranceles).";
            TempData["Resultado"] = mensaje;
            return RedirectToAction(nameof(Index));
        }
    }
}