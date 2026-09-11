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
    }

    // Todo lo que necesita BoletinPdfBuilder para armar el PDF de un alumno.
    public class BoletinData
    {
        public Alumno Alumno { get; set; } = null!;
        public int AnioLectivo { get; set; }

        // Las columnas del boletín, en orden: los Periodo "no Parcial" (Trimestral, Cuatrimestral,
        // Anual) del año lectivo. Los Parciales no se muestran acá (son el detalle que arma cada
        // Trimestral, no algo que la familia necesite ver fila por fila).
        public List<Periodo> Columnas { get; set; } = new();

        public List<FilaBoletinMateria> Filas { get; set; } = new();
    }
}
