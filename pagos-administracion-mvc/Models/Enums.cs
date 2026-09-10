namespace pagos_administracion_mvc.Models
{
    public class Enums
    {
        public enum NivelEducativo
        {
            Primaria,
            Secundaria
        }
        public enum Turno
        {
            Mañana,
            Tarde
        }
        public enum EstadoCuota
        {
            Pendiente,
            Pagada,
            Vencida,
            Parcial
        }
        public enum EstadoPago
        {
            Pendiente,      // 0 - sin tocar
            Aprobado,       // 1 - vuelve a su valor original
            Rechazado,      // 2 - vuelve a su valor original
            Cancelado,      // 3 - vuelve a su valor original
            EnRevision      // 4 - nuevo, al final, no pisa nada existente
        }
        public enum EstadoAsistencia
        {
            Presente,
            Ausente,
            Tarde,
            Justificada
        }

        // Distingue el registro de asistencia a la clase regular del registro de Educación
        // Física, porque cada uno pesa distinto en el cálculo de la falta del día.
        public enum Materia
        {
            Clase,
            EducacionFisica
        }

        // [Flags] para poder marcar varios días de Educación Física por curso (ej: Lunes y Viernes).
        [Flags]
        public enum DiasSemana
        {
            Ninguno = 0,
            Lunes = 1,
            Martes = 2,
            Miercoles = 4,
            Jueves = 8,
            Viernes = 16,
            Sabado = 32
        }

        // Nivel de la jerarquía de un Periodo de evaluación. Parcial = una nota suelta dentro de
        // un trimestre. Trimestral/Cuatrimestral/Anual son "periodos contenedores": su nota puede
        // salir de promediar los periodos que tienen como padre a este, o puede ser cargada a mano
        // por el Docente (ver Nota.EsPromedioAutomatico), que es lo que pidió el usuario para
        // cuando "no se coloca la real sino que se tiene en cuenta todo el cuatrimestre".
        public enum TipoPeriodo
        {
            Parcial,
            Trimestral,
            Cuatrimestral,
            Anual
        }
    }
}
