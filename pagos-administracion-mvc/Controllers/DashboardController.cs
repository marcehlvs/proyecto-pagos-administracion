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
            // 4) Alertas operativas — cosas que alguien se puede haber olvidado de cargar/hacer,
            // para que el Admin las vea de entrada en vez de descubrirlas por casualidad.

            // Cuotas venciendo en los próximos 7 días (Pendientes o Parciales).
            var en7Dias = hoy.AddDays(7);
            var cuotasPorVencer = await _context.Cuotas
                .CountAsync(c => (c.Estado == EstadoCuota.Pendiente || c.Estado == EstadoCuota.Parcial)
                    && c.FechaVencimiento >= hoy && c.FechaVencimiento <= en7Dias);

            // Entregas de Tareas ya entregadas pero todavía sin calificar.
            var tareasSinCalificar = await _context.Entregas.CountAsync(e => e.Entregada && e.Calificacion == null);

            // Cursos con alumnos donde no se cargó asistencia (Clase) en los últimos 3 días
            // corridos — aproximado a propósito (no calcula días hábiles exactos por curso, para
            // no sumar otra consulta pesada acá); alcanza para levantar la mano, no para ser exacto.
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

            // Boletines: solo alerta si ya cerró al menos un Cuatrimestre este año lectivo (si
            // ninguno cerró todavía, no corresponde publicar nada, así que no es una alerta real).
            var hayCuatrimestreCerrado = await _context.Periodos.AnyAsync(p =>
                p.Tipo == TipoPeriodo.Cuatrimestral && p.AnioLectivo == anioActual &&
                p.FechaFin != null && p.FechaFin < hoy);
            var boletinesPendientes = 0;
            if (hayCuatrimestreCerrado)
            {
                var totalCursosActivos = await _context.Cursos.CountAsync(c => c.Activo);
                var cursosPublicados = await _context.BoletinPublicaciones
                    .CountAsync(bp => bp.AnioLectivo == anioActual && bp.Publicado);
                boletinesPendientes = Math.Max(0, totalCursosActivos - cursosPublicados);
            }

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
                TotalVencidasHistorico = totalVencidas,
                CuotasPorVencer = cuotasPorVencer,
                TareasSinCalificar = tareasSinCalificar,
                CursosSinAsistenciaReciente = cursosSinAsistenciaReciente,
                BoletinesPendientes = boletinesPendientes
            };

            return View(modelo);
        }
    }
}
