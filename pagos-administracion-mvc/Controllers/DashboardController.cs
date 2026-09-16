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

        // Página de registros en la tabla de Auditoría del Panel.
        private const int TamanioPagina = 25;

        public async Task<IActionResult> Index(
            string tab = "metricas",
            string? entidad = null,
            string? accion = null,
            int pagina = 1)
        {
            var hoy = DateTime.Today;
            var mesActual = hoy.Month;
            var anioActual = hoy.Year;

            // ── Sección 1: Métricas de pagos ─────────────────────────────────
            var recaudacionMes = await _context.Pagos
                .Where(p => p.Estado == EstadoPago.Aprobado
                    && p.Fecha.Month == mesActual && p.Fecha.Year == anioActual)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;

            var cuotasDelMes = await _context.Cuotas
                .Where(c => c.Mes == mesActual && c.Anio == anioActual)
                .Select(c => c.Estado)
                .ToListAsync();

            var totalCuotasMes    = cuotasDelMes.Count;
            var cuotasPagadas     = cuotasDelMes.Count(e => e == EstadoCuota.Pagada);
            var cuotasPendientes  = cuotasDelMes.Count(e => e == EstadoCuota.Pendiente);
            var cuotasVencidas    = cuotasDelMes.Count(e => e == EstadoCuota.Vencida);
            var cuotasParciales   = cuotasDelMes.Count(e => e == EstadoCuota.Parcial);

            var totalCuotas       = await _context.Cuotas.CountAsync();
            var totalVencidas     = await _context.Cuotas.CountAsync(c => c.Estado == EstadoCuota.Vencida);
            var porcentajeMorosidad = totalCuotas > 0
                ? Math.Round((decimal)totalVencidas / totalCuotas * 100, 1)
                : 0m;

            // Alertas operativas
            var en7Dias = hoy.AddDays(7);
            var cuotasPorVencer = await _context.Cuotas
                .CountAsync(c => (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Parcial)
                    && c.FechaVencimiento >= hoy && c.FechaVencimiento <= en7Dias);

            var tareasSinCalificar = await _context.Entregas
                .CountAsync(e => e.Entregada && e.Calificacion == null);

            var limiteAsistencia = hoy.AddDays(-3);
            var cursosConAlumnos = await _context.Cursos
                .Where(c => c.Activo && c.Inscripciones.Any(i => i.Activo))
                .Select(c => c.Id)
                .ToListAsync();
            var ultimaAsistenciaPorCurso = await _context.Asistencias
                .Where(a => a.Materia == Materia.Clase)
                .GroupBy(a => a.Inscripcion.CursoId)
                .Select(g => new { CursoId = g.Key, Ultima = g.Max(a => a.Fecha) })
                .ToListAsync();
            var cursosSinAsistenciaReciente = cursosConAlumnos.Count(id =>
            {
                var ultima = ultimaAsistenciaPorCurso.FirstOrDefault(u => u.CursoId == id)?.Ultima;
                return ultima == null || ultima < limiteAsistencia;
            });

            var hayCuatrimestreCerrado = await _context.Periodos.AnyAsync(p =>
                p.Tipo == TipoPeriodo.Cuatrimestral && p.AnioLectivo == anioActual &&
                p.FechaFin != null && p.FechaFin < hoy);
            var boletinesPendientes = 0;
            if (hayCuatrimestreCerrado)
            {
                var totalCursosActivos = await _context.Cursos.CountAsync(c => c.Activo);
                var cursosPublicados   = await _context.BoletinPublicaciones
                    .CountAsync(bp => bp.AnioLectivo == anioActual && bp.Publicado);
                boletinesPendientes = Math.Max(0, totalCursosActivos - cursosPublicados);
            }

            var metricas = new DashboardViewModel
            {
                RecaudacionMes           = recaudacionMes,
                MesActual                = mesActual,
                AnioActual               = anioActual,
                CuotasPagadas            = cuotasPagadas,
                CuotasPendientes         = cuotasPendientes,
                CuotasVencidas           = cuotasVencidas,
                CuotasParciales          = cuotasParciales,
                TotalCuotasMes           = totalCuotasMes,
                PorcentajeMorosidad      = porcentajeMorosidad,
                TotalCuotasHistorico     = totalCuotas,
                TotalVencidasHistorico   = totalVencidas,
                CuotasPorVencer          = cuotasPorVencer,
                TareasSinCalificar       = tareasSinCalificar,
                CursosSinAsistenciaReciente = cursosSinAsistenciaReciente,
                BoletinesPendientes      = boletinesPendientes,
            };

            // ── Sección 2: Auditoría ──────────────────────────────────────────
            // Solo cargamos los logs si el usuario está en esa sección, para no
            // pagar el costo de la query cuando no hace falta.
            var queryAudit = _context.AuditLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(entidad))
                queryAudit = queryAudit.Where(a => a.Entidad == entidad);
            if (!string.IsNullOrWhiteSpace(accion))
                queryAudit = queryAudit.Where(a => a.Accion == accion);

            pagina = Math.Max(1, pagina);
            var totalLogs = await queryAudit.CountAsync();
            var logs = await queryAudit
                .OrderByDescending(a => a.Fecha)
                .Skip((pagina - 1) * TamanioPagina)
                .Take(TamanioPagina)
                .ToListAsync();

            // ── Sección 3: Configuración del sistema ──────────────────────────
            var configSitio = await _context.ConfiguracionSitio
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == 1);

            var vm = new PanelControlViewModel
            {
                Metricas           = metricas,
                UltimosAuditLogs   = logs,
                TotalAuditLogs     = totalLogs,
                PaginaAudit        = pagina,
                TamanioPaginaAudit = TamanioPagina,
                FiltroEntidad      = entidad,
                FiltroAccion       = accion,
                ConfigSitio        = configSitio,
                TabActiva          = tab,
            };

            return View(vm);
        }
    }
}