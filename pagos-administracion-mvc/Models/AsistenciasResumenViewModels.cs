using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    // Una fila del ranking general: un alumno + curso + su total de faltas acumulado.
    // Se usa tanto en el ranking cruzado (Resumen) como en el detalle de un curso (ResumenCurso).
    public class FilaAlumnoFaltas
    {
        public int InscripcionId { get; set; }
        public string AlumnoNombre { get; set; } = string.Empty;
        public int CursoId { get; set; }
        public string CursoEtiqueta { get; set; } = string.Empty;
        public decimal TotalFaltas { get; set; }
    }

    // Una fila por curso, con sus totales agregados. Alimenta la tabla principal de Resumen.
    public class ResumenCursoFila
    {
        public Curso Curso { get; set; } = null!;
        public int AlumnosInscriptos { get; set; }
        public decimal TotalFaltas { get; set; }
        public decimal PromedioFaltas { get; set; }
    }

    // Modelo completo de la pantalla Asistencias/Resumen.
    public class AsistenciasResumenViewModel
    {
        public List<ResumenCursoFila> Cursos { get; set; } = new();

        // Top alumnos con más faltas, cruzando todos los cursos visibles para quien mira
        // (Admin: todos los cursos activos. Docente: solo los suyos).
        public List<FilaAlumnoFaltas> RankingGeneral { get; set; } = new();
    }

    // Una fila de la grilla Alumno × día hábil del mes (Asistencias/Grilla). El valor es null
    // cuando ese día todavía no tiene asistencia cargada para ese alumno — es justamente lo que
    // la grilla existe para mostrar de un vistazo, sin entrar día por día a Tomar.
    public class FilaAlumnoGrilla
    {
        public string AlumnoNombre { get; set; } = string.Empty;
        public Dictionary<DateTime, EstadoAsistencia?> EstadosPorDia { get; set; } = new();
    }

    public class GrillaAsistenciaViewModel
    {
        public List<DateTime> DiasHabiles { get; set; } = new();
        public List<FilaAlumnoGrilla> Filas { get; set; } = new();
    }
}
