using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Services
{
    // Centraliza el cálculo de "cuánto vale" cada registro de asistencia, para no duplicar
    // la regla de negocio en controllers y vistas.
    //
    // Reglas (confirmadas con el usuario):
    // - Día SIN Educación Física para ese curso: Ausente = falta completa (1). Tarde = 1/4 de falta.
    // - Día CON Educación Física para ese curso: la falta se reparte 50/50 entre Clase y EF.
    //     Clase:            Ausente = 1/2 falta. Tarde = 1/4 falta.
    //     Educación Física: Ausente = 1/2 falta. Tarde: no se registra (0, por ahora).
    // - Presente y Justificada nunca suman falta (0).
    // - La suma de Clase + EducacionFisica del mismo día nunca supera 1 con esta tabla,
    //   así que no hace falta un tope explícito.
    public static class AsistenciaCalculadora
    {
        public static decimal CalcularFraccionFalta(Materia materia, EstadoAsistencia estado, bool diaTieneEducacionFisica)
        {
            if (estado == EstadoAsistencia.Presente || estado == EstadoAsistencia.Justificada)
                return 0m;

            if (materia == Materia.EducacionFisica)
            {
                // Solo se registra Ausente en EF por ahora; Tarde no suma (según lo definido).
                return estado == EstadoAsistencia.Ausente ? 0.5m : 0m;
            }

            // Materia.Clase
            if (!diaTieneEducacionFisica)
            {
                return estado switch
                {
                    EstadoAsistencia.Ausente => 1m,
                    EstadoAsistencia.Tarde => 0.25m,
                    _ => 0m
                };
            }

            return estado switch
            {
                EstadoAsistencia.Ausente => 0.5m,
                EstadoAsistencia.Tarde => 0.25m,
                _ => 0m
            };
        }

        // Suma el total de faltas de una inscripción completa (todas sus asistencias, agrupadas
        // por día para calcular correctamente los días con Educación Física). Centraliza la lógica
        // que antes vivía duplicada en la vista de Mis Asistencias; la usa también el dashboard
        // de resumen de faltas (Admin/Docente).
        public static decimal CalcularTotalFaltas(IEnumerable<Asistencia> asistencias, Curso curso)
        {
            return asistencias
                .GroupBy(a => a.Fecha)
                .Sum(g =>
                {
                    var diaTieneEF = curso.TieneEducacionFisica(g.Key);
                    return g.Sum(a => CalcularFraccionFalta(a.Materia, a.Estado, diaTieneEF));
                });
        }
    }
}
