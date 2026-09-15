namespace pagos_administracion_mvc.Models
{
    /// <summary>
    /// ViewModel que transporta el resultado de una importación masiva de alumnos
    /// desde un archivo CSV o XLSX hacia la vista Importar.cshtml.
    /// </summary>
    public class ImportarAlumnosViewModel
    {
        /// <summary>
        /// Cantidad de alumnos efectivamente guardados en la base de datos.
        /// </summary>
        public int ImportadosOk { get; set; }

        /// <summary>
        /// Cantidad de filas del archivo que no se procesaron (DNI duplicado,
        /// datos inválidos, CursoId inexistente, etc.).
        /// </summary>
        public int FilasFallidas { get; set; }

        /// <summary>
        /// Mensajes descriptivos de cada fila que falló.
        /// Ejemplo: "Fila 3 — DNI 12345678: ya existe en la base de datos."
        /// </summary>
        public List<string> Errores { get; set; } = new();

        /// <summary>
        /// True si ya se procesó al menos un archivo (para saber si mostrar
        /// el bloque de resultados en la vista).
        /// </summary>
        public bool ProcesadoAlMenosUnaVez { get; set; } = false;
    }

    /// <summary>
    /// POCO que mapea 1-a-1 con las columnas del CSV/XLSX de importación.
    /// CsvHelper lo populará automáticamente a partir de las cabeceras.
    /// Los campos son string? para tolerar celdas vacías sin lanzar excepción.
    /// </summary>
    public class AlumnoCsvRow
    {
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Dni { get; set; }
        public string? Nivel { get; set; }
        public string? GradoAnio { get; set; }
        public string? Turno { get; set; }
        public string? EsRecursante { get; set; }
        public string? CursoId { get; set; }
    }
}
