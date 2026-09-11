namespace pagos_administracion_mvc.Models
{
    // Una fila de la pantalla de carga: un alumno inscripto + su nota (si ya tiene) para el
    // Periodo que se está viendo.
    public class FilaAlumnoNota
    {
        public int InscripcionId { get; set; }
        public string AlumnoNombre { get; set; } = string.Empty;
        public decimal? Valor { get; set; }
        public bool EsPromedioAutomatico { get; set; } = true;
    }

    // Modelo completo de la pantalla NotasController/Cargar.
    public class CargarNotasViewModel
    {
        public CursoAsignatura CursoAsignatura { get; set; } = null!;
        public List<Periodo> PeriodosDisponibles { get; set; } = new();
        public Periodo? PeriodoSeleccionado { get; set; }

        // Si el Periodo seleccionado tiene Subperiodos (es un Trimestral/Cuatrimestral/Anual),
        // la carga es un override manual: por defecto se calcula solo. Un Parcial siempre se
        // carga a mano (no hay de dónde promediar).
        public bool EsPeriodoContenedor => PeriodoSeleccionado?.Subperiodos.Any() == true;

        public List<FilaAlumnoNota> Filas { get; set; } = new();
    }
}
