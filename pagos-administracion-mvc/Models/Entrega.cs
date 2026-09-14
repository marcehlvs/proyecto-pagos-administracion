using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models
{
    // La entrega de UN Alumno para UNA Tarea. Se crea sola (en blanco, Entregada=false) cuando
    // se arma la Tarea, una por cada Inscripcion activa del Curso — así el Docente ve de entrada
    // la lista completa de alumnos, no solo los que ya entregaron.
    public class Entrega
    {
        public int Id { get; set; }

        public int TareaId { get; set; }
        [ValidateNever]
        public Tarea Tarea { get; set; } = null!;

        // Mismo ancla que Nota/Asistencia: por qué Inscripcion (Alumno+Curso), no AlumnoId suelto.
        public int InscripcionId { get; set; }
        [ValidateNever]
        public Inscripcion Inscripcion { get; set; } = null!;

        // true si el Alumno subió texto/archivo, O si el Docente la tildó a mano (para una
        // entrega presencial/oral que no pasa por el sistema). Los dos caminos conviven.
        public bool Entregada { get; set; } = false;

        [Display(Name = "Texto de la entrega")]
        public string? Texto { get; set; }

        // Mismo patrón de archivo que Pago.ComprobanteRuta: se guarda en App_Data/tareas con un
        // nombre GUID, y ArchivoNombreOriginal es solo para mostrarlo con su nombre real en la UI.
        public string? ArchivoRuta { get; set; }
        public string? ArchivoNombreOriginal { get; set; }

        public DateTime? FechaEntrega { get; set; }

        [Range(0, 10, ErrorMessage = "La calificación debe estar entre 0 y 10.")]
        [Display(Name = "Calificación")]
        public decimal? Calificacion { get; set; }

        [Display(Name = "Observación del Docente")]
        public string? ObservacionDocente { get; set; }

        // Si Tarea.PeriodoId no es null y el Docente calificó, acá queda la Nota generada (Orden
        // > 0, EsPromedioAutomatico = false) para que este Entrega y esa Nota se mantengan
        // sincronizados si la calificación se corrige después. Null si la Tarea no tiene Periodo
        // asociado, o si todavía no se calificó.
        public int? NotaId { get; set; }
        [ValidateNever]
        public Nota? Nota { get; set; }

        // Soft delete: mismo criterio que el resto del proyecto.
        public bool Activo { get; set; } = true;

        public string? CalificadaPorNombre { get; set; }
        public DateTime? FechaCalificacion { get; set; }
    }
}
