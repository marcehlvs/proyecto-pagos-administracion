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

        // Solo se completa (y solo importa) cuando el Periodo es Cuatrimestral: valoración TEA/
        // TEP/TED que carga el Docente para el RITE, independiente de la calificación numérica.
        public Enums.ValoracionPreliminar? ValoracionPreliminar { get; set; }

        // Intensificación diciembre/febrero (RITE, Fase 3): solo se completan (y solo importan)
        // cuando el Periodo es Anual — mismo criterio que ValoracionPreliminar para Cuatrimestral.
        public decimal? IntensificacionDiciembre { get; set; }
        public decimal? IntensificacionFebrero { get; set; }
    }

    // Una nota suelta (Orden 1..N) enviada desde el formulario. Valor es TEXTO, no decimal: los
    // <input type="number"> del navegador siempre mandan el punto como separador decimal, pero
    // si esta propiedad fuera decimal? directamente, ASP.NET Core la parsearía usando la cultura
    // del servidor (es-AR, coma decimal) y el bind fallaría o mezclaría el valor. Se parsea a
    // mano con cultura invariante en el controller (ver NotasController.ParsearValor). Se usa una
    // lista plana en vez de un diccionario anidado (notasSueltas[id][orden]) porque el binding de
    // ASP.NET Core es mucho más confiable con listas de objetos indexadas (notasSueltas[0].Valor)
    // que con diccionarios de diccionarios.
    public class NotaSueltaInput
    {
        public int InscripcionId { get; set; }
        public int Orden { get; set; }
        public string? Valor { get; set; }
    }

    // La nota consolidada forzada a mano (Orden 0), un alumno por entrada. Mismo motivo: Valor es
    // texto, se parsea con cultura invariante.
    public class NotaManualInput
    {
        public int InscripcionId { get; set; }
        public string? Valor { get; set; }

        // "TEA"/"TEP"/"TED" o vacío ("—", sin cambios / sin cargar). Texto por el mismo motivo
        // que el resto: más fácil de parsear a mano (y de dejar en blanco) que bindear el enum
        // directamente. Solo se usa cuando el Periodo es Cuatrimestral (ver Cargar.cshtml).
        public string? ValoracionPreliminar { get; set; }

        // Intensificación diciembre/febrero: mismo motivo que Valor, texto parseado a mano con
        // cultura invariante. Solo se usan cuando el Periodo es Anual (ver Cargar.cshtml).
        public string? IntensificacionDiciembre { get; set; }
        public string? IntensificacionFebrero { get; set; }
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

        // Columna "Valoración preliminar" (TEA/TEP/TED, RITE): solo tiene sentido para un
        // Periodo Cuatrimestral, sea o no contenedor (aplica igual si el colegio arma el
        // cuatrimestre a partir de Trimestres o si el Docente carga notas sueltas directamente).
        public bool MostrarValoracionPreliminar => PeriodoSeleccionado?.Tipo == Enums.TipoPeriodo.Cuatrimestral;

        // Columnas "Intensificación diciembre/febrero" (RITE): solo tienen sentido para el
        // Periodo Anual — es el examen de recuperación de fin de ciclo, no aplica por
        // Cuatrimestre (mismo criterio que MostrarValoracionPreliminar, pero para el otro tipo).
        public bool MostrarIntensificacion => PeriodoSeleccionado?.Tipo == Enums.TipoPeriodo.Anual;

        // Cantidad de columnas de nota suelta a mostrar (mínimo 4, se ajusta sola si ya hay
        // cargadas más, y el Docente puede pedir más desde la vista).
        public int Columnas { get; set; } = 4;

        public List<FilaAlumnoNota> Filas { get; set; } = new();
    }
}
