namespace pagos_administracion_mvc.Models
{
    // Una fila de la pantalla de carga: un alumno inscripto, sus notas sueltas (si el Periodo no
    // es contenedor) y el valor consolidado resultante para el Periodo que se está viendo.
    public class FilaAlumnoNota
    {
        public int InscripcionId { get; set; }
        public string AlumnoNombre { get; set; } = string.Empty;

        // orden (1..N) -> valor. Vacío si el Periodo es contenedor (ahí no hay notas sueltas
        // que cargar, se promedian los Subperiodos directamente).
        public Dictionary<int, decimal?> ValoresPorOrden { get; set; } = new();

        // Valor consolidado del Periodo para este alumno: el promedio automático, o la nota que
        // el Docente cargó a mano por encima de ese promedio.
        public decimal? Promedio { get; set; }
        public bool EsPromedioAutomatico { get; set; } = true;
    }

    // Una nota suelta (Orden 1..N) enviada desde el formulario. Se usa una lista plana en vez de
    // un diccionario anidado (notasSueltas[id][orden]) porque el binding de ASP.NET Core es mucho
    // más confiable con listas de objetos indexadas (notasSueltas[0].Valor) que con diccionarios
    // de diccionarios.
    public class NotaSueltaInput
    {
        public int InscripcionId { get; set; }
        public int Orden { get; set; }
        public decimal? Valor { get; set; }
    }

    // La nota consolidada forzada a mano (Orden 0), un alumno por entrada.
    public class NotaManualInput
    {
        public int InscripcionId { get; set; }
        public decimal? Valor { get; set; }
    }

    // Modelo completo de la pantalla NotasController/Cargar.
    public class CargarNotasViewModel
    {
        public CursoAsignatura CursoAsignatura { get; set; } = null!;
        public List<Periodo> PeriodosDisponibles { get; set; } = new();
        public Periodo? PeriodoSeleccionado { get; set; }

        // true = Admin mirando una materia que no es suya: puede ver, no puede cargar ni editar.
        public bool SoloLectura { get; set; }

        // Si el Periodo elegido tiene Subperiodos (ej. un Cuatrimestre hecho de Trimestres), no
        // hay notas sueltas que cargar acá: se promedian los Subperiodos. Si no tiene Subperiodos
        // (ej. un Trimestre), el Docente carga notas sueltas directamente (ver Columnas).
        public bool EsPeriodoContenedor => PeriodoSeleccionado?.Subperiodos.Any() == true;

        // Cantidad de columnas de nota suelta a mostrar (mínimo 4, se ajusta sola si ya hay
        // cargadas más, y el Docente puede pedir más desde la vista).
        public int Columnas { get; set; } = 4;

        public List<FilaAlumnoNota> Filas { get; set; } = new();
    }
}
