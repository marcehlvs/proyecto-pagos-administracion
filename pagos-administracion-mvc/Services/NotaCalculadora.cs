using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Services
{
    // Calcula el valor "consolidado" (Orden = 0) de un Periodo para el boletín. Reglas:
    //
    // - Si ya hay una Nota con Orden = 0 y EsPromedioAutomatico = false, esa gana siempre (el
    //   Docente la cargó a mano por encima del cálculo — "a veces no se coloca la real sino que
    //   se tiene en cuenta todo el cuatrimestre").
    // - Si el Periodo tiene Subperiodos (ej. un Cuatrimestre hecho de Trimestres), se promedian
    //   recursivamente sus valores consolidados.
    // - Si NO tiene Subperiodos (ej. un Trimestre sin Sub-Periodos creados), se promedian las
    //   notas sueltas que el Docente fue cargando ahí mismo (Orden 1, 2, 3... — tantas como haya
    //   cargado, sin mínimo ni máximo fijo). No hace falta que el Admin cree un Periodo por cada
    //   parcial: el Docente carga columnas de nota directamente dentro del Trimestre.
    // - El resultado se persiste como Nota (Orden = 0, EsPromedioAutomatico = true), a modo de
    //   caché, para no recalcular todo de nuevo cada vez que se arma el boletín.
    public class NotaCalculadora
    {
        private readonly AdministracionDbContext _context;
        public NotaCalculadora(AdministracionDbContext context) => _context = context;

        public async Task<decimal?> CalcularAsync(int inscripcionId, int cursoAsignaturaId, int periodoId)
        {
            var periodo = await _context.Periodos
                .Include(p => p.Subperiodos)
                .FirstOrDefaultAsync(p => p.Id == periodoId);
            if (periodo == null) return null;

            var consolidada = await _context.Notas.FirstOrDefaultAsync(n =>
                n.InscripcionId == inscripcionId && n.CursoAsignaturaId == cursoAsignaturaId &&
                n.PeriodoId == periodoId && n.Orden == 0);

            if (consolidada != null && !consolidada.EsPromedioAutomatico)
                return consolidada.Valor;

            List<decimal> valores;

            if (periodo.Subperiodos.Any())
            {
                valores = new List<decimal>();
                foreach (var sub in periodo.Subperiodos)
                {
                    var valor = await CalcularAsync(inscripcionId, cursoAsignaturaId, sub.Id);
                    if (valor.HasValue) valores.Add(valor.Value);
                }
            }
            else
            {
                valores = await _context.Notas
                    .Where(n => n.InscripcionId == inscripcionId && n.CursoAsignaturaId == cursoAsignaturaId &&
                        n.PeriodoId == periodoId && n.Orden > 0)
                    .Select(n => n.Valor)
                    .ToListAsync();
            }

            if (!valores.Any()) return null; // Todavía no hay nada cargado de dónde promediar.

            var promedio = Math.Round(valores.Average(), 2);
            await GuardarComoAutomaticaAsync(consolidada, inscripcionId, cursoAsignaturaId, periodoId, promedio);

            return promedio;
        }

        private async Task GuardarComoAutomaticaAsync(Nota? consolidada, int inscripcionId, int cursoAsignaturaId, int periodoId, decimal valor)
        {
            if (consolidada != null)
            {
                consolidada.Valor = valor;
                consolidada.EsPromedioAutomatico = true;
                consolidada.FechaModificacion = DateTime.Now;
            }
            else
            {
                _context.Notas.Add(new Nota
                {
                    InscripcionId = inscripcionId,
                    CursoAsignaturaId = cursoAsignaturaId,
                    PeriodoId = periodoId,
                    Orden = 0,
                    Valor = valor,
                    EsPromedioAutomatico = true
                });
            }
            await _context.SaveChangesAsync();
        }

        // Después de guardar notas sueltas (Orden > 0) en un Periodo, ese Periodo y todos sus
        // ancestros (vía PeriodoPadre) quedan con la nota consolidada desactualizada. Se sube por
        // la cadena recalculando cada nivel — mucho más preciso que recorrer todos los Periodos
        // del año, y funciona igual de bien para un Trimestre "hoja" que para un contenedor.
        public async Task RecalcularHaciaArribaAsync(int inscripcionId, int cursoAsignaturaId, int periodoId)
        {
            int? actualId = periodoId;
            while (actualId.HasValue)
            {
                await CalcularAsync(inscripcionId, cursoAsignaturaId, actualId.Value);
                var periodo = await _context.Periodos.FindAsync(actualId.Value);
                actualId = periodo?.PeriodoPadreId;
            }
        }
    }
}
