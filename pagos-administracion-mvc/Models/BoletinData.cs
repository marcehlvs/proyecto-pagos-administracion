using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Models
{
    // Una fila del boletín: una materia, con su valor para cada Periodo "columna" (por ejemplo,
    // 1er Trimestre / 2do Trimestre / 3er Trimestre / Nota Final). El valor puede ser null si
    // todavía no se cargó ninguna nota para ese período.
    public class FilaBoletinMateria
    {
        public string CursoEtiqueta { get; set; } = string.Empty;
        public string AsignaturaNombre { get; set; } = string.Empty;
        public Dictionary<int, decimal?> ValoresPorPeriodoId { get; set; } = new();

        // Solo tiene entradas para columnas Cuatrimestrales (ver BoletinService): la Valoración
        // Preliminar (TEA/TEP/TED) del RITE, independiente de la calificación numérica de arriba.
        public Dictionary<int, ValoracionPreliminar?> ValoracionesPorPeriodoId { get; set; } = new();
    }

    // Días hábiles e inasistencias de un Alumno en un Cuatrimestre, cruzando Asistencia con el
    // rango FechaInicio/FechaFin de ese Periodo (ver BoletinService). Si el Periodo no tiene esas
    // fechas cargadas, no hay entrada para él en BoletinData.AsistenciasPorPeriodoId — el boletín
    // lo imprime en blanco en vez de mostrar un 0 que no reflejaría nada real.
    public class ResumenAsistenciaPeriodo
    {
        public int DiasHabiles { get; set; }
        public int Inasistencias { get; set; }
    }

    // Todo lo que necesita una IBoletinPlantilla para armar el PDF de un alumno.
    public class BoletinData
    {
        public Alumno Alumno { get; set; } = null!;
        public int AnioLectivo { get; set; }

        // Curso.Nombre de la (primera) Inscripcion del alumno para este año — es lo que se
        // imprime como "SECCIÓN" en el RITE (ej. "6to B"). En blanco si el Curso no tiene un
        // Nombre propio cargado (ver Curso.Etiqueta).
        public string? Seccion { get; set; }

        // Las columnas del boletín, en orden: los Periodo "no Parcial" (Trimestral, Cuatrimestral,
        // Anual) del año lectivo. Los Parciales no se muestran acá (son el detalle que arma cada
        // Trimestral, no algo que la familia necesite ver fila por fila).
        public List<Periodo> Columnas { get; set; } = new();

        public List<FilaBoletinMateria> Filas { get; set; } = new();

        // Por PeriodoId de una columna Cuatrimestral con FechaInicio/FechaFin cargadas.
        public Dictionary<int, ResumenAsistenciaPeriodo> AsistenciasPorPeriodoId { get; set; } = new();
    }
}
