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

        // Cantidad de días distintos con al menos un registro de asistencia (Clase o EF).
        // Es la base "sobre cuántos días" se calcula el % de presentismo.
        public static int CalcularDiasRegistrados(IEnumerable<Asistencia> asistencias) =>
            asistencias.Select(a => a.Fecha).Distinct().Count();

        // % de presentismo = 100 - (promedio de falta por día registrado). Si todavía no se
        // tomó asistencia ningún día, se muestra 100% (no hay evidencia de faltas todavía).
        public static decimal CalcularPresentismo(IEnumerable<Asistencia> asistencias, Curso curso)
        {
            var lista = asistencias as ICollection<Asistencia> ?? asistencias.ToList();
            var diasRegistrados = CalcularDiasRegistrados(lista);
            if (diasRegistrados == 0) return 100m;

            var faltas = CalcularTotalFaltas(lista, curso);
            var presentismo = (1m - (faltas / diasRegistrados)) * 100m;
            return Math.Round(Math.Clamp(presentismo, 0m, 100m), 1);
        }

        // Formatea una cantidad de faltas como fracción legible en vez de decimal (ej. "3 ¼" en
        // vez de "3.25"), para el PDF del boletín. No se usa ninguna librería externa: con la
        // tabla de CalcularFraccionFalta, el valor siempre cae en un múltiplo de 1/4 (0, .25,
        // .5, .75), así que alcanza con mapear esos 4 casos a los glifos Unicode de fracción.
        // Si algún día se agregan pesos distintos (octavos, etc.) y el valor no cae en un
        // múltiplo de 1/4, se devuelve el decimal tal cual como respaldo.
        public static string FormatearComoFraccion(decimal valor)
        {
            var entero = Math.Truncate(valor);
            var resto = valor - entero;

            string glifo = resto switch
            {
                0m => "",
                0.25m => "¼",
                0.5m => "½",
                0.75m => "¾",
                _ => null! // caso no contemplado, cae al respaldo decimal más abajo
            };

            if (glifo == null)
                return valor.ToString("0.##");

            if (entero == 0m && resto != 0m)
                return glifo; // ej. "¼" solo, sin "0" adelante

            return entero.ToString("0") + glifo;
        }
    }
}
