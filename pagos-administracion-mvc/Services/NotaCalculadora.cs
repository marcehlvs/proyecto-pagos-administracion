using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Services
{
    // Calcula el valor de una Nota para un Periodo "contenedor" (Trimestral/Cuatrimestral/Anual),
    // subiendo recursivamente por Periodo.Subperiodos. Reglas (confirmadas con el usuario):
    //
    // - Si ya hay una Nota cargada para ese Periodo con EsPromedioAutomatico = false, esa gana
    //   siempre (el Docente la cargó a mano: "a veces no se coloca la real sino que se tiene
    //   en cuenta todo el cuatrimestre").
    // - Si no, se promedian los Subperiodos (recursivo, así que un Cuatrimestral promedia sus
    //   Trimestrales, que a su vez promedian sus Parciales).
    // - Un Periodo sin Subperiodos (un Parcial) no se calcula: solo vale lo que el Docente cargó
    //   a mano para ese Parcial (no hay de dónde promediar).
    // - El resultado se persiste como Nota con EsPromedioAutomatico = true, a modo de caché, para
    //   no recalcular todo de nuevo cada vez que se arma un boletín.
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

            var notaExistente = await _context.Notas.FirstOrDefaultAsync(n =>
                n.InscripcionId == inscripcionId &&
                n.CursoAsignaturaId == cursoAsignaturaId &&
                n.PeriodoId == periodoId);

            if (notaExistente != null && !notaExistente.EsPromedioAutomatico)
                return notaExistente.Valor;

            if (!periodo.Subperiodos.Any())
                return notaExistente?.Valor; // Parcial: no hay de dónde promediar, solo lo cargado a mano.

            var valores = new List<decimal>();
            foreach (var sub in periodo.Subperiodos)
            {
                var valor = await CalcularAsync(inscripcionId, cursoAsignaturaId, sub.Id);
                if (valor.HasValue) valores.Add(valor.Value);
            }

            if (!valores.Any()) return null; // Ningún subperiodo tiene nota cargada todavía.

            var promedio = Math.Round(valores.Average(), 2);
            await GuardarComoAutomaticaAsync(notaExistente, inscripcionId, cursoAsignaturaId, periodoId, promedio);

            return promedio;
        }

        private async Task GuardarComoAutomaticaAsync(Nota? notaExistente, int inscripcionId, int cursoAsignaturaId, int periodoId, decimal valor)
        {
            if (notaExistente != null)
            {
                notaExistente.Valor = valor;
                notaExistente.EsPromedioAutomatico = true;
                notaExistente.FechaModificacion = DateTime.Now;
            }
            else
            {
                _context.Notas.Add(new Nota
                {
                    InscripcionId = inscripcionId,
                    CursoAsignaturaId = cursoAsignaturaId,
                    PeriodoId = periodoId,
                    Valor = valor,
                    EsPromedioAutomatico = true
                });
            }
            await _context.SaveChangesAsync();
        }

        // Recalcula todos los Periodos "contenedores" de una Asignatura para un Alumno, de abajo
        // hacia arriba. Se usa después de guardar Notas de Parciales, para que el boletín no
        // muestre un Trimestral desactualizado. Los Periodos con carga manual (override) no se
        // tocan: CalcularAsync ya los respeta.
        public async Task RecalcularJerarquiaAsync(int inscripcionId, int cursoAsignaturaId, int anioLectivo)
        {
            var periodosContenedores = await _context.Periodos
                .Where(p => p.AnioLectivo == anioLectivo && p.Subperiodos.Any())
                .ToListAsync();

            foreach (var periodo in periodosContenedores)
                await CalcularAsync(inscripcionId, cursoAsignaturaId, periodo.Id);
        }
    }
}
