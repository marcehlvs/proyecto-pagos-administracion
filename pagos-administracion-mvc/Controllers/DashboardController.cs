using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly AdministracionDbContext _context;

        public DashboardController(AdministracionDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var hoy = DateTime.Today;
            var mesActual = hoy.Month;
            var anioActual = hoy.Year;

            // 1) Recaudación del mes (pagos aprobados con fecha dentro del mes/año actual)
            var recaudacionMes = await _context.Pagos
                .Where(p => p.Estado == EstadoPago.Aprobado
                    && p.Fecha.Month == mesActual && p.Fecha.Year == anioActual)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;

            // 2) Cuotas del mes/año actual: pagadas vs pendientes (incluye vencidas y parciales dentro de "pendientes")
            var cuotasDelMes = await _context.Cuotas
                .Where(c => c.Mes == mesActual && c.Anio == anioActual)
                .Select(c => c.Estado)
                .ToListAsync();

            var totalCuotasMes = cuotasDelMes.Count;
            var cuotasPagadas = cuotasDelMes.Count(e => e == EstadoCuota.Pagada);
            var cuotasPendientes = cuotasDelMes.Count(e => e == EstadoCuota.Pendiente);
            var cuotasVencidas = cuotasDelMes.Count(e => e == EstadoCuota.Vencida);
            var cuotasParciales = cuotasDelMes.Count(e => e == EstadoCuota.Parcial);

            // 3) % de morosidad: cuotas vencidas sobre el total de cuotas generadas (histórico, no solo el mes)
            var totalCuotas = await _context.Cuotas.CountAsync();
            var totalVencidas = await _context.Cuotas.CountAsync(c => c.Estado == EstadoCuota.Vencida);
            var porcentajeMorosidad = totalCuotas > 0 ? Math.Round((decimal)totalVencidas / totalCuotas * 100, 1) : 0m;

            var modelo = new DashboardViewModel
            {
                RecaudacionMes = recaudacionMes,
                MesActual = mesActual,
                AnioActual = anioActual,
                CuotasPagadas = cuotasPagadas,
                CuotasPendientes = cuotasPendientes,
                CuotasVencidas = cuotasVencidas,
                CuotasParciales = cuotasParciales,
                TotalCuotasMes = totalCuotasMes,
                PorcentajeMorosidad = porcentajeMorosidad,
                TotalCuotasHistorico = totalCuotas,
                TotalVencidasHistorico = totalVencidas
            };

            return View(modelo);
        }
    }
}
