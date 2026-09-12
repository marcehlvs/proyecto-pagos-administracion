using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Services
{
    // Arma los datos del boletín de un Alumno para un año lectivo: junta todas sus Inscripciones
    // (todos los cursos en los que está anotado), y para cada materia de cada curso, el valor de
    // cada Periodo "columna" (ver BoletinData.Columnas), usando NotaCalculadora para que los
    // Trimestrales/Cuatrimestrales estén al día aunque nadie haya entrado a verlos todavía.
    public class BoletinService
    {
        private readonly AdministracionDbContext _context;
        private readonly NotaCalculadora _calculadora;

        public BoletinService(AdministracionDbContext context, NotaCalculadora calculadora)
        {
            _context = context;
            _calculadora = calculadora;
        }

        public async Task<BoletinData?> ObtenerDatosAsync(int alumnoId, int anioLectivo)
        {
            var alumno = await _context.Alumnos.FindAsync(alumnoId);
            if (alumno == null) return null;

            var columnas = await _context.Periodos
                .Where(p => p.AnioLectivo == anioLectivo && p.Tipo != TipoPeriodo.Parcial)
                .OrderBy(p => p.Tipo).ThenBy(p => p.Nombre)
                .ToListAsync();

            var inscripciones = await _context.Inscripciones
                .Include(i => i.Curso)
                .Where(i => i.AlumnoId == alumnoId)
                .ToListAsync();

            var datos = new BoletinData
            {
                Alumno = alumno,
                AnioLectivo = anioLectivo,
                Columnas = columnas,
                // SECCIÓN del RITE: el Nombre del Curso de la primera Inscripcion (ej. "6to B").
                // Si el alumno tuviera más de una Inscripcion (caso raro), alcanza con la primera:
                // el boletín es "por alumno", no arma una fila de Sección por Curso.
                Seccion = inscripciones.FirstOrDefault()?.Curso.Nombre
            };

            // Días hábiles / inasistencias por Cuatrimestre: se calculan una sola vez para el
            // alumno (no por materia, la asistencia no es por Asignatura), cruzando Asistencia
            // con el rango FechaInicio/FechaFin del Periodo. Si el Admin no cargó esas fechas
            // para un Cuatrimestre, esa columna directamente no entra en el diccionario — la
            // plantilla la imprime en blanco en vez de mostrar un 0 que no reflejaría nada real.
            var inscripcionIds = inscripciones.Select(i => i.Id).ToList();
            foreach (var columna in columnas.Where(c => c.Tipo == TipoPeriodo.Cuatrimestral && c.FechaInicio.HasValue && c.FechaFin.HasValue))
            {
                var asistenciasDelCuatrimestre = await _context.Asistencias
                    .Where(a => inscripcionIds.Contains(a.InscripcionId) && a.Activo &&
                                a.Materia == Materia.Clase &&
                                a.Fecha >= columna.FechaInicio!.Value && a.Fecha <= columna.FechaFin!.Value)
                    .ToListAsync();

                datos.AsistenciasPorPeriodoId[columna.Id] = new ResumenAsistenciaPeriodo
                {
                    DiasHabiles = asistenciasDelCuatrimestre.Select(a => a.Fecha.Date).Distinct().Count(),
                    // Justificada cuenta como inasistencia igual que Ausente (el alumno no
                    // estuvo en clase ese día); Tarde no cuenta como falta.
                    Inasistencias = asistenciasDelCuatrimestre.Count(a => a.Estado == EstadoAsistencia.Ausente || a.Estado == EstadoAsistencia.Justificada)
                };
            }

            foreach (var inscripcion in inscripciones)
            {
                var materias = await _context.CursosAsignaturas
                    .Include(ca => ca.Asignatura)
                    .Where(ca => ca.CursoId == inscripcion.CursoId)
                    .OrderBy(ca => ca.Asignatura.Nombre)
                    .ToListAsync();

                foreach (var materia in materias)
                {
                    var fila = new FilaBoletinMateria
                    {
                        CursoEtiqueta = inscripcion.Curso.Etiqueta,
                        AsignaturaNombre = materia.Asignatura.Nombre
                    };

                    foreach (var columna in columnas)
                    {
                        fila.ValoresPorPeriodoId[columna.Id] = await _calculadora.CalcularAsync(inscripcion.Id, materia.Id, columna.Id);

                        if (columna.Tipo == TipoPeriodo.Cuatrimestral)
                        {
                            var consolidada = await _context.Notas.FirstOrDefaultAsync(n =>
                                n.InscripcionId == inscripcion.Id && n.CursoAsignaturaId == materia.Id &&
                                n.PeriodoId == columna.Id && n.Orden == 0);
                            fila.ValoracionesPorPeriodoId[columna.Id] = consolidada?.ValoracionPreliminar;
                        }
                    }

                    datos.Filas.Add(fila);
                }
            }

            return datos;
        }
    }
}