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

            var datos = new BoletinData { Alumno = alumno, AnioLectivo = anioLectivo, Columnas = columnas };

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
                        fila.ValoresPorPeriodoId[columna.Id] = await _calculadora.CalcularAsync(inscripcion.Id, materia.Id, columna.Id);

                    datos.Filas.Add(fila);
                }
            }

            return datos;
        }
    }
}
